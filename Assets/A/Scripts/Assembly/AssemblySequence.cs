using System.Collections.Generic;
using MilTrainSimVR.A.Common;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MilTrainSimVR.A.Assembly
{
    public enum StepKind
    {
        Remove,         // 부품을 끝까지 빼서 분리
        PullToDetent,   // 부품을 걸림 위치까지만 당김
        Open,           // 본체 열기
        Close,          // 본체 닫기
        Insert,         // 부품을 끝까지 끼움
    }

    [System.Serializable]
    public class AssemblyStep
    {
        public StepKind kind;
        public AssemblyPart part;
        public AssemblyHinge hinge;

        [Tooltip("이 단계 동안 걸림 위치에 있어야 하는 부품. 걸림 위치가 아니면 이 단계의 부품이 빠지지 않는다")]
        public AssemblyPart holdAtDetent;
    }

    // 분해·결합 순서. 지금 단계의 부품만 움직이고, 나머지는 막는다.
    // 단계 문구는 AssemblyTexts.Steps 의 같은 번호를 쓴다. 글자는 여기서 보여 주지 않고 이벤트로 알린다 — 무엇을 보여 줄지는 흐름(AssemblyTrainingFlow)이 정한다.
    // 소리·진동은 조작에 바로 붙는 피드백이라 여기서 낸다.
    // 시작(Begin)·처음 상태로 되돌리기(ResetAll)는 AssemblyTrainingFlow 가 부른다. 시작 전과 끝난 뒤에는 모든 부품이 잠긴다.
    public class AssemblySequence : MonoBehaviour
    {
        [SerializeField] AssemblyStep[] m_Steps;

        [Tooltip("결합이 시작되는 단계 번호 (0부터)")]
        [SerializeField] int m_AssemblyStartIndex = 7;

        [SerializeField] TrainingFeedback m_Feedback;

        [Tooltip("같은 오류 피드백을 다시 내기까지 기다리는 시간(초)")]
        [SerializeField] float m_ErrorCooldown = 0.8f;

        int m_Index;
        int m_Errors;
        int[] m_StepErrors = new int[0];
        bool m_Running;
        float m_StartTime;
        float m_EndTime;
        float m_NextErrorTime;

        // 지금 단계가 바뀌었을 때 (지금 단계 번호, 전체 단계 수) — 시작할 때도 한 번 알린다
        public event System.Action<int, int> StepChanged;

        // 순서가 틀렸을 때 — true: 끼우려다 틀림, false: 빼거나 움직이려다 막힘. 같은 실수는 m_ErrorCooldown 간격으로만 알린다
        public event System.Action<bool> OrderError;

        // 마지막 단계가 끝났을 때
        public event System.Action Finished;

        public AssemblyStep Current => m_Running && m_Index < m_Steps.Length ? m_Steps[m_Index] : null;
        public bool Completed => m_Index >= m_Steps.Length;
        public bool Running => m_Running;
        public int Errors => m_Errors;
        public int StepCount => m_Steps.Length;
        public int CurrentIndex => m_Index;
        public int AssemblyStartIndex => m_AssemblyStartIndex;
        public float ElapsedSeconds => (m_Running ? Time.time : m_EndTime) - m_StartTime;

        // 단계별 오류 수 — 평가 결과용. 시작·되돌리기 때 비운다
        public IReadOnlyList<int> StepErrors => m_StepErrors;

        void Awake()
        {
            if (m_Steps.Length != AssemblyTexts.Steps.Length)
                Debug.LogWarning($"[Assembly] 단계 수({m_Steps.Length})와 문구 수({AssemblyTexts.Steps.Length})가 다르다", this);

            foreach (var step in m_Steps)
            {
                if (step.part != null) step.part.Bind(this);
                if (step.hinge != null) step.hinge.Bind(this);
                if (step.holdAtDetent != null) step.holdAtDetent.Bind(this);
            }
        }

        // 첫 단계부터 시작한다. 부품이 처음 상태가 아니면 먼저 ResetAll 을 부른다
        public void Begin()
        {
            m_Index = 0;
            m_Errors = 0;
            m_StepErrors = new int[m_Steps.Length];
            m_NextErrorTime = 0f;
            m_StartTime = Time.time;
            m_Running = true;
            NotifyStep();
        }

        // 멈추고 모든 부품·본체를 끼워진·닫힌 상태로 되돌린다 (다시 하기)
        public void ResetAll()
        {
            m_Running = false;
            m_Index = 0;
            m_Errors = 0;
            m_StepErrors = new int[m_Steps.Length];
            m_StartTime = m_EndTime = Time.time;
            foreach (var step in m_Steps)
            {
                if (step.part != null) step.part.ResetToSeat();
                if (step.holdAtDetent != null) step.holdAtDetent.ResetToSeat();
                if (step.hinge != null) step.hinge.ResetClosed();
            }
        }

        void Update()
        {
            if (!m_Running || !IsDone(Current))
                return;

            m_Index++;
            m_Feedback.PlayStep();

            if (Completed)
            {
                m_Running = false;
                m_EndTime = Time.time;
                Debug.Log($"[Assembly] 완료 — {ElapsedSeconds:F1}초, 오류 {m_Errors}회");
                Finished?.Invoke();
                return;
            }
            NotifyStep();
        }

        static bool IsDone(AssemblyStep s)
        {
            switch (s.kind)
            {
                case StepKind.Remove: return s.part.State == PartState.Free;
                case StepKind.PullToDetent: return s.part.AtDetent;
                case StepKind.Open: return s.hinge.IsOpen;
                case StepKind.Close: return s.hinge.IsClosed;
                case StepKind.Insert: return s.part.State == PartState.Seated;
            }
            return false;
        }

        void NotifyStep()
        {
            Debug.Log($"[Assembly] 단계 {m_Index + 1}/{m_Steps.Length}: {AssemblyTexts.StepText(m_Index)}");
            StepChanged?.Invoke(m_Index, m_Steps.Length);
        }

        // --- 부품·힌지가 묻는 것 ---

        // 끼워진 부품이 자리에서 얼마나 빠질 수 있는지(m)
        public float MaxOffset(AssemblyPart part)
        {
            var s = Current;
            if (s == null)
                return part.Offset;

            if (s.part == part)
            {
                if (s.kind == StepKind.PullToDetent)
                    return part.DetentDistance;
                if (s.kind == StepKind.Remove)
                {
                    if (s.holdAtDetent != null && !s.holdAtDetent.AtDetent)
                        return 0f;
                    return part.RemoveDistance;
                }
            }

            if (s.holdAtDetent == part)
                return part.DetentDistance;

            // 지금 단계가 아니면 더 빠지지 않는다 (밀어 넣는 건 된다)
            return part.Offset;
        }

        // 지금 단계에서 움직여야 하는 부품인지 (정해진 범위 밖으로 더 당기는 건 오류가 아니다)
        public bool IsActive(AssemblyPart part)
        {
            var s = Current;
            return s != null && (s.part == part || s.holdAtDetent == part);
        }

        // 지금 단계가 이 본체를 열거나 닫는 단계인지
        public bool IsActive(AssemblyHinge hinge)
        {
            var s = Current;
            return s != null && s.hinge == hinge && (s.kind == StepKind.Open || s.kind == StepKind.Close);
        }

        public bool CanInsert(AssemblyPart part)
        {
            var s = Current;
            return s != null && s.kind == StepKind.Insert && s.part == part;
        }

        // 힌지가 움직일 수 있는 각도 범위 (최소, 최대)
        public Vector2 HingeRange(AssemblyHinge hinge)
        {
            var s = Current;
            if (s != null && s.hinge == hinge && (s.kind == StepKind.Open || s.kind == StepKind.Close))
                return new Vector2(0f, hinge.OpenAngle);
            return new Vector2(hinge.Angle, hinge.Angle);
        }

        // --- 부품·힌지가 알리는 것 ---

        // countError: 이번 잡기에서 처음 난 오류일 때만 true — 오류 횟수는 잡을 때마다 한 번만 센다
        public void ReportBlocked(IXRSelectInteractor hand, bool countError)
        {
            if (!TryFeedback(countError)) return;
            m_Feedback.PlayBlocked();
            m_Feedback.PlayError();
            m_Feedback.Haptic(hand, 0.7f, 0.15f);
            OrderError?.Invoke(false);
        }

        public void ReportWrongInsert(IXRSelectInteractor hand, bool countError)
        {
            if (!TryFeedback(countError)) return;
            m_Feedback.PlayError();
            m_Feedback.Haptic(hand, 0.7f, 0.15f);
            OrderError?.Invoke(true);
        }

        public void ReportSnapped(IXRSelectInteractor hand) => m_Feedback.Haptic(hand, 0.3f, 0.05f);

        public void ReportDetached(IXRSelectInteractor hand) => m_Feedback.Haptic(hand, 0.3f, 0.05f);

        public void ReportSeated(IXRSelectInteractor hand)
        {
            m_Feedback.PlayClick();
            m_Feedback.Haptic(hand, 0.5f, 0.08f);
        }

        // 오류 횟수는 countError 일 때 세고, 소리·자막은 m_ErrorCooldown 간격으로만 낸다
        bool TryFeedback(bool countError)
        {
            // 시작 전·끝난 뒤에는 순서 오류로 치지 않는다 (부품은 잠겨 있다)
            if (!m_Running)
                return false;
            if (countError)
            {
                m_Errors++;
                m_StepErrors[m_Index]++;
            }
            if (Time.time < m_NextErrorTime)
                return false;
            m_NextErrorTime = Time.time + m_ErrorCooldown;
            return true;
        }

        // --- 시험용 (에디터 메뉴 A/시험) ---

        // 손 없이 지금 단계를 끝난 상태로 만든다. 다음 프레임 Update 에서 단계가 넘어간다
        internal string TestCompleteCurrent()
        {
            var s = Current;
            if (s == null)
                return "진행 중인 단계 없음";
            string text = $"{m_Index + 1}/{m_Steps.Length} {AssemblyTexts.StepText(m_Index)}";
            switch (s.kind)
            {
                case StepKind.Remove: s.part.TestDetach(); break;
                case StepKind.PullToDetent: s.part.TestSetOffset(s.part.DetentDistance); break;
                case StepKind.Open: s.hinge.TestSetAngle(s.hinge.OpenAngle); break;
                case StepKind.Close: s.hinge.TestSetAngle(0f); break;
                case StepKind.Insert: s.part.ResetToSeat(); break;
            }
            return text;
        }
    }
}
