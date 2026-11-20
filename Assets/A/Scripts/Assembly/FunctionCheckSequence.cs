using System.Collections.Generic;
using MilTrainSimVR.A.Common;
using MilTrainSimVR.A.Rifle;
using UnityEngine;

namespace MilTrainSimVR.A.Assembly
{
    public enum FunctionCheckStep
    {
        HoldRifle,        // 총 들기
        SafeCheck,        // 조정간 안전에서 방아쇠 — 격발되지 않는지 확인
        ChargingHandle,   // 장전 손잡이를 끝까지 당겼다 놓기
        SelectSemi,       // 조정간 단발
        DryFire,          // 방아쇠 — 빈 격발 (소리 없이 진동)
        SelectSafe,       // 조정간 안전
    }

    public enum FunctionCheckMistake
    {
        TriggerOnSafe,    // 안전인데 방아쇠를 당김 (2단계가 아닐 때)
        FiredOutOfOrder,  // 5단계가 아닌데 격발됨
        HammerDown,       // 공이치기가 떨어져 있어 아무 일 없음 — 장전 손잡이를 다시 당겨야 한다
        NotOnSafe,        // 2단계인데 조정간이 안전이 아님
        NotAimedUp,       // 5단계 공격발인데 총구가 위를 향하지 않음 — 공이치기가 떨어졌으니 장전 손잡이를 다시 당겨야 한다
        NotSemi,          // 5단계 공격발인데 조정간이 단발이 아님 (연발) — 11/03: B/Y 는 안전→단발→연발 순이라 단발에서 한 번 더 누르면 연발이 된다
    }

    // 결합 뒤 기능 점검 6단계. 사격용 총(K2C1Rifle)의 이벤트·상태로 판정한다 — 총은 막지 않고, 순서와 다른 동작만 실수로 센다.
    // 단계 문구는 AssemblyTexts.FunctionCheckSteps 의 같은 번호. 글자는 이벤트로 알리고 흐름(AssemblyTrainingFlow)이 보여 준다.
    // 시작(Begin)·멈춤(Stop)은 흐름이 부른다. 총을 떨어뜨리면 시작한 자리(작업대 위)로 되돌린다.
    public class FunctionCheckSequence : MonoBehaviour
    {
        [SerializeField] K2C1Rifle m_Rifle;
        [SerializeField] TrainingFeedback m_Feedback;

        [Tooltip("총이 시작 자리보다 이만큼(m) 아래로 떨어지면 시작 자리로 되돌린다")]
        [SerializeField] float m_DropDistance = 0.5f;

        [Tooltip("공격발(5단계)은 총구가 수직 위에서 이 각도(°) 안을 향할 때만 통과 — 안전한 방향(위)으로 쏘기")]
        [SerializeField] float m_AimUpAngle = 60f;

        [Tooltip("같은 실수 피드백을 다시 내기까지 기다리는 시간(초)")]
        [SerializeField] float m_ErrorCooldown = 0.8f;

        static readonly int k_StepCount = System.Enum.GetValues(typeof(FunctionCheckStep)).Length;

        int m_Index;
        int m_Errors;
        readonly int[] m_StepErrors = new int[k_StepCount];
        bool m_Running;
        bool m_TestHeld;
        float m_NextErrorTime;
        Pose m_StartPose;

        public event System.Action<int, int> StepChanged;            // (지금 단계 번호, 전체 단계 수)
        public event System.Action<FunctionCheckMistake> Mistake;
        public event System.Action Finished;

        public bool Running => m_Running;
        public int Errors => m_Errors;

        // 단계별 실수 수 — 평가 결과용. 시작·멈춤 때 비운다
        public IReadOnlyList<int> StepErrors => m_StepErrors;
        public int StepCount => k_StepCount;
        public int CurrentIndex => m_Index;
        public FunctionCheckStep Current => (FunctionCheckStep)Mathf.Min(m_Index, k_StepCount - 1);
        public K2C1Rifle Rifle => m_Rifle;

        void OnEnable()
        {
            m_Rifle.TriggerBlocked += OnTriggerBlocked;
            m_Rifle.HammerDown += OnHammerDown;
            m_Rifle.DryFired += OnDryFired;
            m_Rifle.Fired += OnFired;
            m_Rifle.ChargingHandleReturned += OnHandleReturned;
        }

        void OnDisable()
        {
            m_Rifle.TriggerBlocked -= OnTriggerBlocked;
            m_Rifle.HammerDown -= OnHammerDown;
            m_Rifle.DryFired -= OnDryFired;
            m_Rifle.Fired -= OnFired;
            m_Rifle.ChargingHandleReturned -= OnHandleReturned;
        }

        // 총을 처음 상태로 두고 첫 단계부터. 총은 이미 시작 자리에 켜져 있어야 한다
        public void Begin()
        {
            m_Rifle.ResetState();
            m_StartPose = new Pose(m_Rifle.transform.position, m_Rifle.transform.rotation);
            m_Index = 0;
            m_Errors = 0;
            System.Array.Clear(m_StepErrors, 0, m_StepErrors.Length);
            m_NextErrorTime = 0f;
            m_TestHeld = false;
            m_Running = true;
            NotifyStep();
        }

        public void Stop()
        {
            m_Running = false;
            m_Index = 0;
            m_Errors = 0;
            System.Array.Clear(m_StepErrors, 0, m_StepErrors.Length);
            m_TestHeld = false;
        }

        void Update()
        {
            if (!m_Running)
                return;

            ReturnIfDropped();

            // 상태로 끝나는 단계 — 총 들기, 조정간 단발·안전
            bool done = Current switch
            {
                FunctionCheckStep.HoldRifle => m_Rifle.IsHeld || m_TestHeld,
                FunctionCheckStep.SelectSemi => m_Rifle.Mode == K2C1Rifle.FireMode.Semi,
                FunctionCheckStep.SelectSafe => m_Rifle.Mode == K2C1Rifle.FireMode.Safe,
                _ => false,
            };
            if (done)
                Advance();
        }

        // --- 총이 알리는 것 ---

        void OnTriggerBlocked()
        {
            if (!m_Running) return;
            if (Current == FunctionCheckStep.SafeCheck)
                Advance();   // 안전에서 당겨지지 않는 것을 확인했다
            else
                Report(FunctionCheckMistake.TriggerOnSafe);
        }

        void OnHammerDown()
        {
            if (!m_Running) return;
            Report(Current == FunctionCheckStep.SafeCheck ? FunctionCheckMistake.NotOnSafe : FunctionCheckMistake.HammerDown);
        }

        void OnDryFired()
        {
            if (!m_Running) return;
            if (Current != FunctionCheckStep.DryFire)
                Report(FunctionCheckMistake.FiredOutOfOrder);
            else if (m_Rifle.Mode != K2C1Rifle.FireMode.Semi)
                Report(FunctionCheckMistake.NotSemi);      // 단계 그대로 — 단발로 돌리고 장전 손잡이를 다시 당긴 뒤 다시 쏜다
            else if (Vector3.Angle(m_Rifle.MuzzleForward, Vector3.up) > m_AimUpAngle)
                Report(FunctionCheckMistake.NotAimedUp);   // 단계 그대로 — 장전 손잡이를 다시 당기고 위로 향해 다시 쏜다
            else
                Advance();
        }

        // 기능 점검은 탄 없이 한다 — 실탄이 나갔다면 순서 밖으로 본다
        void OnFired()
        {
            if (m_Running)
                Report(FunctionCheckMistake.FiredOutOfOrder);
        }

        void OnHandleReturned()
        {
            if (m_Running && Current == FunctionCheckStep.ChargingHandle)
                Advance();
        }

        // --- ---

        void Advance()
        {
            m_Index++;
            m_Feedback.PlayStep();
            if (m_Index >= k_StepCount)
            {
                m_Running = false;
                Debug.Log($"[FunctionCheck] 완료 — 실수 {m_Errors}회");
                Finished?.Invoke();
                return;
            }
            NotifyStep();
        }

        void NotifyStep()
        {
            Debug.Log($"[FunctionCheck] 단계 {m_Index + 1}/{k_StepCount}: {AssemblyTexts.FunctionCheckStepText(m_Index)}");
            StepChanged?.Invoke(m_Index, k_StepCount);
        }

        // 실수는 매번 세고, 소리·자막은 m_ErrorCooldown 간격으로만 낸다
        void Report(FunctionCheckMistake mistake)
        {
            m_Errors++;
            m_StepErrors[Mathf.Min(m_Index, k_StepCount - 1)]++;
            Debug.Log($"[FunctionCheck] 실수: {mistake}");
            if (Time.time < m_NextErrorTime)
                return;
            m_NextErrorTime = Time.time + m_ErrorCooldown;
            m_Feedback.PlayError();
            Mistake?.Invoke(mistake);
        }

        void ReturnIfDropped()
        {
            if (m_Rifle.IsHeld || m_Rifle.transform.position.y > m_StartPose.position.y - m_DropDistance)
                return;
            var body = m_Rifle.GetComponent<Rigidbody>();
            if (body != null && !body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            m_Rifle.transform.SetPositionAndRotation(m_StartPose.position, m_StartPose.rotation);
        }

        // --- 시험용 (에디터 메뉴 A/시험) — 손 대신 총 메서드로 지금 단계를 한다 ---
        internal string TestCompleteCurrent()
        {
            if (!m_Running)
                return "기능 점검 진행 중 아님";
            string text = $"기능 점검 {m_Index + 1}/{k_StepCount} {AssemblyTexts.FunctionCheckStepText(m_Index)}";
            switch (Current)
            {
                case FunctionCheckStep.HoldRifle: m_TestHeld = true; break;
                case FunctionCheckStep.SafeCheck: m_Rifle.PullTrigger(); m_Rifle.ReleaseTrigger(); break;
                case FunctionCheckStep.DryFire:
                    // 총구를 위로 돌린 뒤 쏜다
                    m_Rifle.transform.rotation = Quaternion.FromToRotation(m_Rifle.MuzzleForward, Vector3.up) * m_Rifle.transform.rotation;
                    m_Rifle.PullTrigger(); m_Rifle.ReleaseTrigger();
                    break;
                case FunctionCheckStep.ChargingHandle: m_Rifle.ChargingHandle.SetOffset(m_Rifle.ChargingHandle.Stroke); break;
                case FunctionCheckStep.SelectSemi: m_Rifle.SetFireMode(K2C1Rifle.FireMode.Semi); break;
                case FunctionCheckStep.SelectSafe: m_Rifle.SetFireMode(K2C1Rifle.FireMode.Safe); break;
            }
            return text;
        }
    }
}
