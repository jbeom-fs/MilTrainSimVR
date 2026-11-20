using MilTrainSimVR.A.Common;
using MilTrainSimVR.A.Rifle;
using UnityEngine;

namespace MilTrainSimVR.A.Range
{
    public enum RangePhase { Prepare, SafetyCheck }   // 사격 전 준비 · 사격 후 안전 검사

    public enum PrepareStep
    {
        HoldRifle,       // 총 들기
        SafeCheck,       // 조정간 안전 확인 — 방아쇠를 당겨 격발되지 않는지 (11/03: 총이 처음부터 안전이라 상태로 보면 저절로 넘어갔다)
        InsertMagazine,  // 탄창 결합
        Charge,          // 장전 — 약실에 탄
        SelectSemi,      // "사격 개시" → 조정간 단발
    }

    public enum SafetyStep
    {
        SelectSafe,      // 조정간 안전
        RemoveMagazine,  // 탄창 분리
        ChamberCheck,    // 장전 손잡이 후퇴, 약실 확인
        BoltForward,     // 노리쇠 전진 — 장전 손잡이 놓기
        DryFire,         // 조정간 단발 → 공격발 (빈 격발)
        SelectSafeEnd,   // 조정간 안전
    }

    public enum RangeMistake
    {
        DryFireOutOfOrder,   // 공격발 순서가 아닌데 빈 격발
        NotAimedDownrange,   // 공격발인데 총구가 표적 쪽이 아님 — 장전 손잡이를 다시 당겨야 한다
        HammerDown,          // 공이치기가 떨어져 있어 아무 일 없음 — 장전 손잡이를 다시 당겨야 한다
        MagazineStillIn,     // 약실 확인인데 탄창이 꽂혀 있음 — 먼저 빼야 한다
        NotSemi,             // 공격발인데 조정간이 단발이 아님 (연발) — 11/03
        AutoInZeroing,       // 영점사격을 연발로 쏨 — 영점사격은 단발 (11/03)
        NotOnSafe,           // 준비 2단계인데 조정간이 안전이 아닌 채 방아쇠 (11/03)
    }

    // 사격 전 준비 5단계와 사격 후 안전 검사 6단계 판정 (사로 기획 2-1·2-3절). 기능 점검(FunctionCheckSequence)과 같은 방식:
    // 총(RangeRifle — A 총 K2C1Rifle 또는 C 총 CRangeRifle)·약실 확인(ChamberCheck)의 이벤트와 상태로 판정한다 — 총은 막지 않고, 순서와 다른 동작만 실수로 센다.
    // 안전 위반(지시 전 격발, 총구 방향)은 따로 센다 (기획 2-4절, 5번). 단계 문구는 RangeTexts 의 같은 번호.
    public class RangeProcedure : MonoBehaviour
    {
        [SerializeField] RangeRifle m_Rifle;
        [SerializeField] ChamberCheck m_ChamberCheck;
        [SerializeField] TrainingFeedback m_Feedback;

        [Tooltip("표적 — 공격발 때 총구가 이쪽을 향해야 한다")]
        [SerializeField] Transform m_Downrange;

        [Tooltip("공격발은 총구가 표적 방향에서 이 각도(°) 안일 때만 통과 — 안전한 방향(표적 쪽)으로 쏘기. 임시값")]
        [SerializeField] float m_DownrangeAngle = 45f;

        [Tooltip("같은 실수 피드백을 다시 내기까지 기다리는 시간(초)")]
        [SerializeField] float m_ErrorCooldown = 0.8f;

        RangePhase m_Phase;
        int m_Index;
        int m_Errors;
        bool m_Running;
        bool m_TestHeld;
        float m_NextErrorTime;

        public event System.Action<int, int> StepChanged;   // (지금 단계 번호, 전체 단계 수)
        public event System.Action<RangeMistake> Mistake;
        public event System.Action Finished;

        public bool Running => m_Running;
        public RangePhase Phase => m_Phase;
        public int CurrentIndex => m_Index;
        public int StepCount => m_Phase == RangePhase.Prepare ? RangeTexts.PrepareSteps.Length : RangeTexts.SafetyCheckSteps.Length;
        // 준비·안전 검사를 합친 실수 수 — 다시 시작(BeginRun)할 때만 비운다
        public int Errors => m_Errors;
        public ChamberCheck ChamberCheck => m_ChamberCheck;

        // 사격 방식에 따라 총을 바꿀 때 (RifleStyleSwitch, OnEnable 전에)
        internal void UseRifle(RangeRifle rifle) => m_Rifle = rifle;

        PrepareStep CurrentPrepare => (PrepareStep)m_Index;
        SafetyStep CurrentSafety => (SafetyStep)m_Index;

        void OnEnable()
        {
            m_Rifle.DryFired += OnDryFired;
            m_Rifle.TriggerBlocked += OnTriggerBlocked;
            m_Rifle.HammerDown += OnHammerDown;
            m_Rifle.ChargingHandlePulled += OnHandlePulled;
            m_Rifle.ChargingHandleReturned += OnHandleReturned;
            m_ChamberCheck.Confirmed += OnChamberConfirmed;
        }

        void OnDisable()
        {
            m_Rifle.DryFired -= OnDryFired;
            m_Rifle.TriggerBlocked -= OnTriggerBlocked;
            m_Rifle.HammerDown -= OnHammerDown;
            m_Rifle.ChargingHandlePulled -= OnHandlePulled;
            m_Rifle.ChargingHandleReturned -= OnHandleReturned;
            m_ChamberCheck.Confirmed -= OnChamberConfirmed;
        }

        // 한 회를 새로 시작할 때 — 실수 수를 비운다
        public void BeginRun() => m_Errors = 0;

        public void Begin(RangePhase phase)
        {
            m_Phase = phase;
            m_Index = 0;
            m_NextErrorTime = 0f;
            m_TestHeld = false;
            m_Running = true;
            m_ChamberCheck.Stop();
            NotifyStep();
        }

        public void Stop()
        {
            m_Running = false;
            m_TestHeld = false;
            m_ChamberCheck.Stop();
        }

        void Update()
        {
            if (!m_Running)
                return;
            if (Regress())
                return;
            // 상태로 끝나는 단계
            if (StateDone())
                Advance();
        }

        // 사격 전 준비에서 끝낸 단계가 다시 풀렸으면 그 단계로 되돌린다 — 장전 뒤 탄창을 빼면 탄창 결합부터, 약실이 비면 장전부터.
        // (탄창을 뺀 것 자체는 안전 위반으로 따로 센다 — RangeSafetyMonitor)
        bool Regress()
        {
            if (m_Phase != RangePhase.Prepare)
                return false;
            int back = -1;
            if (m_Index > (int)PrepareStep.InsertMagazine && !m_Rifle.HasMagazine)
                back = (int)PrepareStep.InsertMagazine;
            else if (m_Index > (int)PrepareStep.Charge && !m_Rifle.Chambered)
                back = (int)PrepareStep.Charge;
            if (back < 0)
                return false;
            m_Index = back;
            Debug.Log($"[Range] 준비 단계 되돌림 → {m_Index + 1}");
            NotifyStep();
            return true;
        }

        bool StateDone()
        {
            if (m_Phase == RangePhase.Prepare)
            {
                return CurrentPrepare switch
                {
                    PrepareStep.HoldRifle => m_Rifle.IsHeld || m_TestHeld,
                    PrepareStep.InsertMagazine => m_Rifle.HasMagazine,
                    PrepareStep.Charge => m_Rifle.Chambered,
                    PrepareStep.SelectSemi => m_Rifle.Mode == RangeRifle.FireMode.Semi,
                    _ => false,
                };
            }
            return CurrentSafety switch
            {
                SafetyStep.SelectSafe => m_Rifle.Mode == RangeRifle.FireMode.Safe,
                SafetyStep.RemoveMagazine => !m_Rifle.HasMagazine,
                SafetyStep.SelectSafeEnd => m_Rifle.Mode == RangeRifle.FireMode.Safe,
                _ => false,
            };
        }

        // --- 총이 알리는 것 ---

        void OnDryFired()
        {
            if (!m_Running) return;
            if (m_Phase != RangePhase.SafetyCheck || CurrentSafety != SafetyStep.DryFire)
                Report(RangeMistake.DryFireOutOfOrder);
            else if (m_Rifle.Mode != RangeRifle.FireMode.Semi)
                Report(RangeMistake.NotSemi);   // 단계 그대로 — 단발로 돌리고 장전 손잡이를 다시 당긴 뒤 다시
            else if (!AimedDownrange())
                Report(RangeMistake.NotAimedDownrange);   // 단계 그대로 — 장전 손잡이를 다시 당기고 표적 쪽으로 다시
            else
                Advance();
        }

        // 준비 2단계 — 안전에서 방아쇠가 당겨지지 않는 것을 확인했다
        void OnTriggerBlocked()
        {
            if (m_Running && m_Phase == RangePhase.Prepare && CurrentPrepare == PrepareStep.SafeCheck)
                Advance();
        }

        void OnHammerDown()
        {
            if (m_Running && m_Phase == RangePhase.Prepare && CurrentPrepare == PrepareStep.SafeCheck)
            {
                Report(RangeMistake.NotOnSafe);   // 안전이 아닌 채 방아쇠 — 공이치기가 떨어져 아무 일 없음
                return;
            }
            if (m_Running && m_Phase == RangePhase.SafetyCheck && CurrentSafety == SafetyStep.DryFire)
                Report(RangeMistake.HammerDown);
        }

        void OnHandlePulled()
        {
            // 약실 확인 단계인데 탄창이 꽂혀 있다 — 노리쇠가 닫히면 다시 장전된다
            if (m_Running && m_Phase == RangePhase.SafetyCheck && CurrentSafety == SafetyStep.ChamberCheck && m_Rifle.HasMagazine)
                Report(RangeMistake.MagazineStillIn);
        }

        void OnHandleReturned()
        {
            if (m_Running && m_Phase == RangePhase.SafetyCheck && CurrentSafety == SafetyStep.BoltForward)
                Advance();
        }

        void OnChamberConfirmed()
        {
            if (m_Running && m_Phase == RangePhase.SafetyCheck && CurrentSafety == SafetyStep.ChamberCheck)
                Advance();
        }

        bool AimedDownrange()
        {
            if (m_Downrange == null)
                return true;
            Vector3 toTarget = m_Downrange.position - m_Rifle.transform.position;
            return Vector3.Angle(m_Rifle.MuzzleForward, toTarget) <= m_DownrangeAngle;
        }

        // --- ---

        void Advance()
        {
            m_Index++;
            m_Feedback?.PlayStep();
            if (m_Index >= StepCount)
            {
                m_Running = false;
                m_ChamberCheck.Stop();
                Debug.Log($"[Range] {m_Phase} 완료 — 실수 누계 {m_Errors}회");
                Finished?.Invoke();
                return;
            }
            NotifyStep();
        }

        void NotifyStep()
        {
            // 약실 확인은 그 단계에서만 센다 — 사격 중 장전 손잡이를 당겨도 확인으로 치지 않게
            if (m_Phase == RangePhase.SafetyCheck && CurrentSafety == SafetyStep.ChamberCheck)
                m_ChamberCheck.Begin();
            else
                m_ChamberCheck.Stop();
            Debug.Log($"[Range] {m_Phase} {m_Index + 1}/{StepCount}");
            StepChanged?.Invoke(m_Index, StepCount);
        }

        // 영점사격 중 연발로 쐈다 — 흐름(RangeTrainingFlow)이 조마다 한 번 알린다. 절차 실수로 센다 (총은 막지 않는다)
        public void ReportAutoFire() => Report(RangeMistake.AutoInZeroing);

        // 실수는 매번 세고, 소리·자막은 m_ErrorCooldown 간격으로만 낸다
        void Report(RangeMistake mistake)
        {
            m_Errors++;
            Debug.Log($"[Range] 실수: {mistake}");
            if (Time.time < m_NextErrorTime)
                return;
            m_NextErrorTime = Time.time + m_ErrorCooldown;
            m_Feedback?.PlayError();
            Mistake?.Invoke(mistake);
        }

        // --- 시험용 (에디터 메뉴 A/시험/사로) — 손 대신 총 메서드로 지금 단계를 한다. 상태 단계는 다음 프레임에 넘어간다 ---
        public string TestCompleteCurrent()
        {
            if (!m_Running)
                return null;
            // 장전 손잡이를 반만(끝까지 당겨 두기 · 놓기) 움직이는 단계는 A 총만 된다 — C 총은 한 번에 당겼다 놓는다
            var handle = (m_Rifle as K2C1Rifle)?.ChargingHandle;
            if (m_Phase == RangePhase.Prepare)
            {
                switch (CurrentPrepare)
                {
                    case PrepareStep.HoldRifle: m_TestHeld = true; break;
                    case PrepareStep.SafeCheck: m_Rifle.SetFireMode(RangeRifle.FireMode.Safe); m_Rifle.PullTrigger(); m_Rifle.ReleaseTrigger(); break;
                    case PrepareStep.InsertMagazine: m_Rifle.TestInsertMagazine(); break;
                    case PrepareStep.Charge: m_Rifle.TestCycleChargingHandle(); break;
                    case PrepareStep.SelectSemi: m_Rifle.SetFireMode(RangeRifle.FireMode.Semi); break;
                }
                return RangeTexts.Step(RangeTexts.PrepareSteps, m_Index);
            }
            string text = RangeTexts.Step(RangeTexts.SafetyCheckSteps, m_Index);
            switch (CurrentSafety)
            {
                case SafetyStep.SelectSafe:
                case SafetyStep.SelectSafeEnd: m_Rifle.SetFireMode(RangeRifle.FireMode.Safe); break;
                case SafetyStep.RemoveMagazine: m_Rifle.TestRemoveMagazine(); break;
                case SafetyStep.ChamberCheck:
                    // 끝까지 당겨 둔다 — 손이 없으니 다음 프레임부터 앞으로 돌아오며 노리쇠 전진 단계가 끝난다
                    if (handle != null) handle.SetOffset(handle.Stroke);
                    m_ChamberCheck.TestConfirm();
                    break;
                case SafetyStep.BoltForward:
                    if (handle != null) handle.SetOffset(0f);
                    else m_Rifle.TestCycleChargingHandle();
                    break;
                case SafetyStep.DryFire:
                    if (m_Downrange != null)
                        m_Rifle.transform.rotation = Quaternion.LookRotation(m_Downrange.position - m_Rifle.transform.position, Vector3.up);
                    if (!m_Rifle.HammerCocked) m_Rifle.TestCycleChargingHandle();
                    m_Rifle.SetFireMode(RangeRifle.FireMode.Semi);
                    m_Rifle.PullTrigger(); m_Rifle.ReleaseTrigger();
                    break;
            }
            return text;
        }
    }
}
