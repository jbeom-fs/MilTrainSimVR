using MilTrainSimVR.A.Common;
using MilTrainSimVR.A.Rifle;
using UnityEngine;
using UnityEngine.UI;

namespace MilTrainSimVR.A.Range
{
    public enum RangeMode { Learning, Evaluation }                       // 학습 · 평가
    public enum RangeStage { Intro, Prepare, Firing, SafetyCheck, Result }   // 안내 · 사격 전 준비 · 영점사격 · 사격 후 안전 검사 · 결과

    // 사로 사격 한 회의 흐름: 안내 → 사격 전 준비 → 영점사격 → 사격 후 안전 검사 → 결과 → 다시 하기 / 처음으로 (기획 2절).
    // 패널(TrainingPanel)에 무엇을 보여 줄지 정하고, 패널 버튼을 구간마다 켜고 끈다. 분해·결합 흐름(AssemblyTrainingFlow)과 같은 모양이다.
    // 사격 전 준비·사격 후 안전 검사의 단계 판정은 RangeProcedure 가 한다 (4번). 흐름은 그 이벤트를 받아 패널에 보여 준다.
    // CompleteStep(에디터 메뉴 A/시험/사로)은 손 대신 지금 단계를 한다.
    // 영점사격 (3번): 조마다 총이 쏜 발 수를 세고, 정해진 발 수가 되면 표적지(ZeroTarget)의 그 조를 닫아 결과를 보여 주고 다음 조로 간다.
    // 표적 이송 (11/02): 표적은 사수 앞에서 시작 → 조를 시작하면 25 m 로 가고 멈추면 격발 허용 → 조가 끝나면 돌아온다 → 사수가 패널 "다음 조 시작"을 누르면 다음 조.
    // 탄창 (2번): 거치대 위에 정해진 발 수의 탄창을 하나 놓는다. 총에 꽂으면 탄창 오브젝트가 없어지고 다시 빼면 새로 생기므로
    //   씬에 미리 두지 않고 처음으로·다시 하기 때마다 새로 만든다.
    // 모드 · 버튼 · 타이머는 분해·결합과 겹치지만 지금은 각자 둔다 (기획 8절).
    public class RangeTrainingFlow : MonoBehaviour
    {
        [SerializeField] TrainingPanel m_Panel;

        [Header("패널 버튼")]
        [SerializeField] Button m_LearningButton;
        [SerializeField] Button m_EvaluationButton;
        [SerializeField] Button m_RetryButton;
        [Tooltip("조 사이 — 표적이 돌아온 뒤 다음 조를 시작 (11/02, 사로 패널에만 씬에서 덧붙임)")]
        [SerializeField] Button m_NextGroupButton;
        [SerializeField] Button m_HomeButton;

        [Header("총")]
        [Tooltip("사로에서 쓰는 사격용 총. 지금은 사로에 둔 임시 총 — 나중에 조립실에서 들고 온 총으로 바꾼다")]
        [SerializeField] RangeRifle m_Rifle;

        [Tooltip("처음으로·다시 하기 때 총을 되돌릴 자리 (사대 거치대 위). 비우면 되돌리지 않는다")]
        [SerializeField] Transform m_RifleRest;

        [Header("판정")]
        [SerializeField] RangeProcedure m_Procedure;
        [SerializeField] RangeSafetyMonitor m_Safety;

        [Tooltip("총을 떨어뜨려 거치대보다 이만큼(m) 아래로 가면 거치대 자리로 되돌린다")]
        [SerializeField] float m_DropDistance = 0.5f;

        [Header("탄창 — 총이 자기 탄창(C_Magazine)을 놓는다 (11/17 총기 통합 5단계: A · C 총 모두 C 탄창)")]
        [Tooltip("탄창을 놓을 자리 (사대 거치대 위)")]
        [SerializeField] Transform m_MagazineRest;

        [Header("영점사격 (임시값 — 교안 확인)")]
        [SerializeField] ZeroTarget m_Target;

        [SerializeField] int m_Groups = 2;            // 11/02 3발 × 3조 → 5발 × 2조 (사용자)
        [SerializeField] int m_RoundsPerGroup = 5;

        [Header("결과 (6번)")]
        [Tooltip("결과 화면 표적지 그림 — 사로 패널에 붙어 있다")]
        [SerializeField] TargetMap m_Map;

        [SerializeField] RangeGradeCriteria m_Grade = new RangeGradeCriteria();

        int m_Step;          // 지금 구간 안의 단계 (영점사격은 조)
        int m_GroupShots;    // 지금 조에서 쏜 발 수
        bool m_AutoReported; // 이 조에서 연발 사격을 이미 알렸는지 (11/03 — 조마다 한 번)
        bool m_OutOfAmmo;    // 탄이 떨어져 이 조를 마칠 차례 — 다음 프레임에 (같은 빈 격발을 절차 판정이 받은 뒤에)
        int m_ChamberPercent = -1;   // 약실 확인 진행 표시 — 바뀔 때만 다시 쓴다
        TargetCarrier m_Carrier;     // 표적 이송 — 없으면 표적은 25 m 에 서 있는 것으로 친다
        bool m_TargetReady;          // 표적이 25 m 에 멈췄다 — 이때만 격발 허용, 발 수를 센다
        bool m_BetweenGroups;        // 조 사이 — 표적이 돌아오고, 다음 조 버튼을 기다린다
        string m_LastGroupLine = "";  // 조 사이 안내 위에 지난 조 결과를 같이 둔다
        readonly System.Collections.Generic.List<string> m_ViolationLog = new System.Collections.Generic.List<string>();   // 평가 결과용
        float m_StartTime;
        float m_EndTime;

        public RangeStage Stage { get; private set; } = RangeStage.Intro;
        public RangeMode Mode { get; private set; } = RangeMode.Learning;
        public int StepIndex => m_Step;
        public float ElapsedSeconds => (Stage == RangeStage.Result ? m_EndTime : Time.time) - m_StartTime;
        public bool Running => Stage != RangeStage.Intro && Stage != RangeStage.Result;

        public event System.Action<RangeStage> StageChanged;

        public static string ModeName(RangeMode mode) =>
            mode == RangeMode.Learning ? RangeTexts.LearningMode : RangeTexts.EvaluationMode;

        void Awake()
        {
            m_LearningButton.onClick.AddListener(StartLearning);
            m_EvaluationButton.onClick.AddListener(StartEvaluation);
            m_RetryButton.onClick.AddListener(Retry);
            m_HomeButton.onClick.AddListener(ReturnToIntro);

            SetLabel(m_LearningButton, RangeTexts.LearningMode);
            SetLabel(m_EvaluationButton, RangeTexts.EvaluationMode);
            SetLabel(m_RetryButton, RangeTexts.RetryButton);
            if (m_NextGroupButton != null)
            {
                m_NextGroupButton.onClick.AddListener(() => StartNextGroup());
                SetLabel(m_NextGroupButton, RangeTexts.NextGroupButton);
                // 씬에만 덧붙인 버튼이라 프리팹 색을 따라가지 않는다 — 다른 버튼 색에 맞춘다 (11/19 B 스타일 가이드 색)
                if (m_NextGroupButton.image != null && m_RetryButton.image != null)
                    m_NextGroupButton.image.color = m_RetryButton.image.color;
            }
            SetLabel(m_HomeButton, RangeTexts.HomeButton);
            if (m_Target != null)
                m_Carrier = m_Target.GetComponentInParent<TargetCarrier>();
        }

        // 사격 방식에 따라 총을 바꿀 때 (RifleStyleSwitch, OnEnable 전에)
        internal void UseRifle(RangeRifle rifle) => m_Rifle = rifle;

        void OnEnable()
        {
            if (m_Rifle != null)
            {
                m_Rifle.Fired += OnFired;
                m_Rifle.DryFired += OnDryFired;
                m_Rifle.FireModeChanged += OnFireModeChanged;
            }
            if (m_Carrier != null)
                m_Carrier.Arrived += OnTargetArrived;
            m_Procedure.StepChanged += OnProcedureStep;
            m_Procedure.Mistake += OnProcedureMistake;
            m_Procedure.Finished += OnProcedureFinished;
            m_Safety.Violated += OnViolated;
        }

        void OnDisable()
        {
            if (m_Rifle != null)
            {
                m_Rifle.Fired -= OnFired;
                m_Rifle.DryFired -= OnDryFired;
                m_Rifle.FireModeChanged -= OnFireModeChanged;
            }
            if (m_Carrier != null)
                m_Carrier.Arrived -= OnTargetArrived;
            m_Procedure.StepChanged -= OnProcedureStep;
            m_Procedure.Mistake -= OnProcedureMistake;
            m_Procedure.Finished -= OnProcedureFinished;
            m_Safety.Violated -= OnViolated;
        }

        // 안전 위반은 모드와 관계없이 알려 준다 (기획 2-4절). 평가 결과에 어느 단계였는지 남긴다
        void OnViolated(RangeViolation violation)
        {
            m_ViolationLog.Add(string.Format(RangeTexts.ViolationLine, RangeTexts.ViolationName(violation), StepTitle()));
            m_Panel.ShowMessage(string.Format(RangeTexts.ViolationHead, RangeTexts.ViolationText(violation)), 3f);
        }

        void OnProcedureStep(int index, int count)
        {
            m_Step = index;
            ShowStep();
        }

        void OnProcedureMistake(RangeMistake mistake)
        {
            // 평가 모드는 무엇을 해야 하는지 알려 주지 않는다
            if (Mode == RangeMode.Evaluation)
                m_Panel.ShowMessage(RangeTexts.WrongOrder, 2f);
            else
                m_Panel.ShowMessage(RangeTexts.MistakeText(mistake) + "\n" + RangeTexts.NowDoHead + StepTitle(), 2.5f);
        }

        void OnProcedureFinished()
        {
            if (Stage == RangeStage.Prepare)
                EnterStage(RangeStage.Firing);
            else if (Stage == RangeStage.SafetyCheck)
                ShowResult();
        }

        // 표적지에 맞았으면 총이 Fired 전에 표적의 OnHit 을 부른다 → 여기서 조를 닫아도 그 탄이 들어가 있다
        // 표적이 움직이는 중에 쏜 탄은 세지 않는다 (격발 허용 전이라 안전 위반 — 지시 전 격발)
        void OnFired()
        {
            if (Stage != RangeStage.Firing || !m_TargetReady)
                return;
            // 영점사격은 단발 — 연발로 쏘면 조마다 한 번 절차 실수로 센다 (11/03: 연발이면 한 조가 순식간에 끝나고 남은 탄이 지시 전 격발이 된다)
            if (m_Rifle.Mode == RangeRifle.FireMode.Auto && !m_AutoReported)
            {
                m_AutoReported = true;
                m_Procedure.ReportAutoFire();
            }
            m_GroupShots++;
            if (m_GroupShots >= m_RoundsPerGroup)
                CompleteStep();
        }

        // 영점사격 중 탄이 떨어졌다 — 그때까지 쏜 발로 이 조를 마친다 (11/03 시뮬레이션: 지시 전 격발 등으로 탄을 쓰면
        // 탄창(조 수 × 조당 발 수)이 모자라 마지막 조를 끝낼 수 없어 교육이 멈췄다)
        void OnDryFired()
        {
            if (Stage != RangeStage.Firing || !m_TargetReady || m_BetweenGroups || m_Rifle.Chambered)
                return;
            if (m_Rifle.HasMagazine && m_Rifle.MagazineRounds > 0)
                return;   // 탄창에 탄이 남았다 — 장전 손잡이를 당기면 된다
            // 바로 마치면 다음 구간(안전 검사)이 시작된 뒤에 절차 판정이 같은 빈 격발을 받아 "공격발 순서 아님"으로 센다 → 다음 프레임에
            m_OutOfAmmo = true;
        }

        void FinishOutOfAmmo()
        {
            m_OutOfAmmo = false;
            if (Stage != RangeStage.Firing || m_BetweenGroups)
                return;
            Debug.Log($"[Range] 탄 없음 — {m_Step + 1}조를 {m_GroupShots}발로 마침");
            CompleteStep();
            m_Panel.ShowMessage(RangeTexts.OutOfAmmo + "\n" + m_LastGroupLine, 4f);
        }

        // 사로 교육이 진행 중이면 머리줄에 지금 조정간을 보여 준다 (11/03)
        void OnFireModeChanged(RangeRifle.FireMode mode) => ShowSelector();

        void ShowSelector()
        {
            if (m_Rifle != null)
                m_Panel.SetSelector(Running ? RifleTexts.SelectorLine(m_Rifle.Mode) : "");
        }

        static void SetLabel(Button button, string text)
        {
            var label = button.GetComponentInChildren<Text>(true);
            if (label != null)
                label.text = text;
        }

        // 탄창 하나에 넣는 탄 = 영점사격 전체 발 수
        public int MagazineRounds => m_Groups * m_RoundsPerGroup;

        void Start()
        {
            ResetEquipment();
            ShowIntro();
        }

        void Update()
        {
            if (Mode == RangeMode.Evaluation && Running)
                m_Panel.SetTimer(ElapsedSeconds);
            if (Running)
                ReturnIfDropped();
            if (m_OutOfAmmo)
                FinishOutOfAmmo();
            ShowChamberProgress();
        }

        // 학습 모드에서만 약실 확인이 얼마나 됐는지 보여 준다 (기획 5절)
        void ShowChamberProgress()
        {
            var check = m_Procedure.ChamberCheck;
            int percent = Mode == RangeMode.Learning && check != null && check.Active && check.Progress > 0f ? Mathf.RoundToInt(check.Progress * 100f) : -1;
            if (percent == m_ChamberPercent)
                return;
            if (percent >= 0)
                m_Panel.ShowMessage(string.Format(RangeTexts.ChamberChecking, percent), 0.5f);
            m_ChamberPercent = percent;
        }

        void ReturnIfDropped()
        {
            if (m_Rifle == null || m_RifleRest == null || m_Rifle.IsHeld || m_Rifle.transform.position.y > m_RifleRest.position.y - m_DropDistance)
                return;
            m_Rifle.transform.SetPositionAndRotation(m_RifleRest.position, m_RifleRest.rotation);
            var body = m_Rifle.GetComponent<Rigidbody>();
            if (body != null && !body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        // --- 패널 버튼이 부른다 ---

        public void StartLearning() => StartTraining(RangeMode.Learning);

        public void StartEvaluation() => StartTraining(RangeMode.Evaluation);

        public void Retry() => StartTraining(Mode);

        public void ReturnToIntro()
        {
            ResetEquipment();
            ShowIntro();
        }

        public void StartTraining(RangeMode mode)
        {
            Mode = mode;
            ResetEquipment();
            m_Procedure.BeginRun();
            m_Safety.Begin();
            m_ViolationLog.Clear();
            m_StartTime = Time.time;
            m_Panel.SetMode(ModeName(mode));
            m_Panel.SetTimer(mode == RangeMode.Evaluation ? 0f : -1f);
            m_Panel.ShowMessage("", 0f);
            m_Panel.SetGuide("");
            EnterStage(RangeStage.Prepare);
        }

        // 시험 메뉴 — 지금 단계를 끝낸다. 준비·안전 검사는 손 대신 총을 조작하고(상태 단계는 다음 프레임에 넘어감),
        // 영점사격은 그때까지 쏜 발 수로 조를 닫는다. 한 단계 이름을 돌려준다 (없으면 null)
        public string CompleteStep()
        {
            if (!Running)
                return null;
            if (Stage == RangeStage.Prepare || Stage == RangeStage.SafetyCheck)
                return m_Procedure.TestCompleteCurrent();
            if (m_BetweenGroups)
                return StartNextGroup() ? RangeTexts.NextGroupButton : null;   // 시험 메뉴 — 버튼 대신
            string done = StepTitle();
            if (Stage == RangeStage.Firing)
                FinishGroup();
            m_Step++;
            if (m_Step < StepCount())
            {
                if (Stage == RangeStage.Firing)
                {
                    m_BetweenGroups = true;
                    ShowNextGroupWhenBack();
                }
                else
                    ShowStep();
                return done;
            }
            EnterStage(RangeStage.SafetyCheck);
            return done;
        }

        // --- 구간 ---

        void EnterStage(RangeStage stage)
        {
            m_Step = 0;
            SetStage(stage);
            if (stage == RangeStage.Prepare || stage == RangeStage.SafetyCheck)
            {
                // 판정이 첫 단계를 알려 오면(StepChanged) 패널에 보여 준다
                m_Procedure.Begin(stage == RangeStage.Prepare ? RangePhase.Prepare : RangePhase.SafetyCheck);
                return;
            }
            if (m_Target != null)
                m_Target.Clear();
            BeginGroup();
            ShowStep();
        }

        // 조를 열고 표적을 25 m 로 보낸다 — 멈추면(OnTargetArrived) 격발 허용
        void BeginGroup()
        {
            m_GroupShots = 0;
            m_AutoReported = false;
            m_OutOfAmmo = false;
            m_Target?.BeginGroup();
            if (m_Carrier == null)
            {
                SetTargetReady(true);
                return;
            }
            SetTargetReady(false);
            m_Carrier.MoveFar();
            if (m_Carrier.IsMoving)
                m_Panel.ShowMessage(RangeTexts.TargetGoingFar, 0f);
        }

        void OnTargetArrived(bool far)
        {
            if (Stage != RangeStage.Firing)
                return;
            if (!far)
            {
                if (m_BetweenGroups)
                    ShowNextGroupWhenBack();
                return;
            }
            if (m_BetweenGroups || m_TargetReady)
                return;
            SetTargetReady(true);
            m_Panel.ShowMessage(RangeTexts.TargetReady, 2f);
        }

        void SetTargetReady(bool ready)
        {
            m_TargetReady = ready;
            m_Safety.FiringAllowed = Stage == RangeStage.Firing && ready;
        }

        bool NextGroupReady => m_BetweenGroups && (m_Carrier == null || !m_Carrier.IsMoving);

        // 조 사이: 표적이 사수 앞에 다 왔으면 다음 조 버튼을 보여 준다 (오는 중이면 도착할 때 다시 부른다)
        // 11/10 사용자: 표적지가 돌아오면 지난 조까지의 탄착을 결과 화면처럼 표적지 그림(조별 색 점)으로 보여 준다 — 보고 다음 조 자세를 고친다
        void ShowNextGroupWhenBack()
        {
            UpdateButtons();
            if (!NextGroupReady)
            {
                m_Panel.SetGuide(RangeTexts.TargetComingBack);
                return;
            }
            m_Panel.SetGuide(m_LastGroupLine + "\n" + RangeTexts.NextGroupGuide);
            ShowMap();
        }

        void ShowMap(string caption = "")
        {
            if (m_Map == null || m_Target == null)
                return;
            m_Map.Show(m_Target.Groups, caption);
            m_Panel.SetRightInset(m_Map.Inset);
        }

        void HideMap()
        {
            if (m_Map != null)
                m_Map.Hide();
            m_Panel.SetRightInset(0f);
        }

        // 패널 "다음 조 시작" — 표적이 다시 25 m 로 간다. 시작했으면 true
        public bool StartNextGroup()
        {
            if (Stage != RangeStage.Firing || !NextGroupReady)
                return false;
            m_BetweenGroups = false;
            HideMap();
            UpdateButtons();
            ShowStep();
            BeginGroup();
            return true;
        }

        // 지금 조를 닫고 결과를 패널에 잠깐 보여 준다. 표적은 사수 쪽으로 돌아온다
        void FinishGroup()
        {
            SetTargetReady(false);
            if (m_Carrier != null)
                m_Carrier.MoveNear();
            if (m_Target == null)
                return;
            ShotGroup group = m_Target.EndGroup(m_GroupShots);
            if (group == null)
                return;
            string line = GroupLine(m_Step, group);
            m_LastGroupLine = line;
            m_Panel.ShowMessage(line, 4f);
            Debug.Log($"[Range] {line} (탄착: {string.Join(", ", group.Hits)})");
        }

        static string GroupLine(int index, ShotGroup g) => g.HitCount == 0
            ? string.Format(RangeTexts.GroupNoHit, index + 1, g.Shots, RangeTexts.GroupColorHex(index))
            : string.Format(RangeTexts.GroupResult, index + 1, g.HitCount, g.Shots, g.SizeCm, RangeTexts.Offset(g.CenterCm), RangeTexts.GroupColorHex(index));

        int StepCount() => Stage switch
        {
            RangeStage.Prepare => RangeTexts.PrepareSteps.Length,
            RangeStage.Firing => m_Groups,
            RangeStage.SafetyCheck => RangeTexts.SafetyCheckSteps.Length,
            _ => 0,
        };

        string StepTitle() => Stage switch
        {
            RangeStage.Prepare => RangeTexts.Step(RangeTexts.PrepareSteps, m_Step),
            RangeStage.Firing => string.Format(RangeTexts.FiringTitle, m_Step + 1, m_Groups, m_RoundsPerGroup),
            RangeStage.SafetyCheck => RangeTexts.Step(RangeTexts.SafetyCheckSteps, m_Step),
            _ => "",
        };

        void ShowStep()
        {
            m_Panel.SetTitle(StepTitle());
            m_Panel.SetGuide(StepGuide());
            m_Panel.SetProgress(m_Step + 1, StepCount());
        }

        // 안내 칸 — 준비·안전 검사는 학습 모드만 방법을 알려 준다. 영점사격(쏘는 법)은 두 모드 모두
        string StepGuide() => Stage switch
        {
            RangeStage.Firing => string.Format(RangeTexts.FiringGuide, m_RoundsPerGroup),
            _ when Mode == RangeMode.Evaluation => RangeTexts.EvaluationGuide,
            RangeStage.Prepare => RangeTexts.Step(RangeTexts.PrepareGuides, m_Step),
            RangeStage.SafetyCheck => RangeTexts.Step(RangeTexts.SafetyCheckGuides, m_Step),
            _ => "",
        };

        void ShowIntro()
        {
            SetStage(RangeStage.Intro);
            m_Panel.SetMode("");
            m_Panel.SetProgress(-1, 0);
            m_Panel.SetTimer(-1f);
            m_Panel.SetTitle(RangeTexts.IntroTitle);
            m_Panel.ShowMessage("", 0f);
            m_Panel.SetGuide(string.Format(RangeTexts.IntroGuide, m_Groups, m_RoundsPerGroup));
        }

        void ShowResult()
        {
            m_EndTime = Time.time;
            SetStage(RangeStage.Result);
            float t = ElapsedSeconds;
            m_Panel.SetProgress(-1, 0);
            m_Panel.SetTimer(-1f);
            // 결과 화면: 왼쪽 글자 + 오른쪽 표적지 그림 (조별 색 점)
            m_Safety.Stop();
            var groups = m_Target != null ? m_Target.Groups : (System.Collections.Generic.IReadOnlyList<ShotGroup>)System.Array.Empty<ShotGroup>();
            m_Grade.roundsPerGroup = m_RoundsPerGroup;
            RangeGrade grade = RangeGrading.Grade(groups, m_Procedure.Errors, m_Safety.Total, m_Grade);
            // 평가 모드는 그림 아래에 등급 기준 (기획 3절)
            ShowMap(Mode == RangeMode.Evaluation
                ? string.Format(RangeTexts.GradeCriteriaLine, m_RoundsPerGroup, m_Grade.groupMaxCm,
                    m_Grade.excellentGroups, m_Grade.excellentMaxErrors, m_Grade.passGroups, m_Grade.passMaxErrors)
                : "");
            m_Panel.SetTitle(Mode == RangeMode.Evaluation
                ? string.Format(RangeTexts.EvaluationResultTitle, RangeTexts.GradeName(grade))
                : RangeTexts.CompletedTitle);
            string message = string.Format(RangeTexts.ResultSummary, ModeName(Mode), (int)(t / 60f), (int)(t % 60f), m_Procedure.Errors, m_Safety.Total);
            if (m_Target != null)
                for (int i = 0; i < m_Target.Groups.Count; i++)
                    message += "\n" + GroupLine(i, m_Target.Groups[i]);
            // 평가 모드는 위반이 어느 단계였는지까지 (기획 3절)
            if (Mode == RangeMode.Evaluation)
                foreach (string line in m_ViolationLog)
                    message += "\n" + line;
            m_Panel.ShowMessage("", 0f);
            m_Panel.SetGuide(message);
            Debug.Log($"[Range] 결과 — {ModeName(Mode)}, {t:F1}초, 절차 실수 {m_Procedure.Errors}회, 안전 위반 {m_Safety.Total}회, 합격 조 {RangeGrading.PassedGroups(groups, m_Grade)}, 등급 {RangeTexts.GradeName(grade)} {string.Join(" / ", m_ViolationLog)}");
        }

        // 판정을 멈추고 총과 탄창을 처음 상태로
        void ResetEquipment()
        {
            m_Procedure.Stop();
            m_Safety.Stop();
            ResetRifle();
            ResetMagazine();
            // 표적은 사수 앞에서 시작한다 — 조를 시작할 때 25 m 로 간다
            m_BetweenGroups = false;
            m_TargetReady = false;
            if (m_Carrier != null)
                m_Carrier.SnapNear();
        }

        // 손에서 놓게 하고 처음 상태로(꽂힌 탄창도 없앰), 거치대 위 자리로
        void ResetRifle()
        {
            if (m_Rifle == null)
                return;
            m_Rifle.ResetState();
            if (m_RifleRest == null)
                return;
            m_Rifle.transform.SetPositionAndRotation(m_RifleRest.position, m_RifleRest.rotation);
            var body = m_Rifle.GetComponent<Rigidbody>();
            if (body != null && !body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        // 씬에 있는 탄창(손에 든 것·떨어진 것)을 모두 치우고 거치대 위에 새 탄창 하나
        void ResetMagazine()
        {
            if (m_MagazineRest == null)
                return;
            // 총이 자기 탄창(C_Magazine)을 놓는다 — 씬의 탄창을 다 치우고 거치대 위에 하나
            if (m_Rifle == null || !m_Rifle.ResetMagazines(m_MagazineRest.position, m_MagazineRest.rotation, MagazineRounds))
                Debug.LogWarning("[Range] 사로 총이 탄창을 놓지 못했다 — 탄창 삽입구(IMagazineFeed)를 확인");
        }

        void SetStage(RangeStage stage)
        {
            Stage = stage;
            ShowSelector();
            if (stage != RangeStage.Result)
                HideMap();
            // 격발은 영점사격에서 표적이 25 m 에 멈췄을 때만, 탄창 분리는 사격 후 안전 검사에서만 허용 (그 밖은 안전 위반)
            m_TargetReady = false;
            m_BetweenGroups = false;
            m_Safety.FiringAllowed = false;
            m_Safety.MagazineRemovalAllowed = stage == RangeStage.SafetyCheck;
            UpdateButtons();
            Debug.Log($"[Range] 구간: {stage}");
            StageChanged?.Invoke(stage);
        }

        // 안내: 모드 둘 · 진행 중: 처음으로 (조 사이엔 다음 조 시작도) · 결과: 다시 하기, 처음으로
        void UpdateButtons()
        {
            bool intro = Stage == RangeStage.Intro;
            bool result = Stage == RangeStage.Result;
            m_LearningButton.gameObject.SetActive(intro);
            m_EvaluationButton.gameObject.SetActive(intro);
            m_RetryButton.gameObject.SetActive(result);
            m_HomeButton.gameObject.SetActive(!intro);
            if (m_NextGroupButton != null)
                m_NextGroupButton.gameObject.SetActive(Stage == RangeStage.Firing && NextGroupReady);
        }
    }
}
