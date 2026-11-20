using MilTrainSimVR.A.Common;
using MilTrainSimVR.A.Rifle;
using UnityEngine;
using UnityEngine.UI;

namespace MilTrainSimVR.A.Assembly
{
    public enum TrainingMode { Learning, Evaluation }               // 학습 · 평가
    public enum TrainingStage { Intro, Assembly, FunctionCheck, Result }   // 안내 · 분해·결합 · 기능 점검 · 결과

    // 분해·결합 교육 한 회의 흐름: 안내 → 분해·결합 → (기능 점검) → 결과 → 다시 하기 / 처음으로.
    // 패널(TrainingPanel)에 무엇을 보여 줄지 정하고, 패널 버튼을 구간마다 켜고 끈다.
    // 기능 점검 때는 작업대의 분해용 총을 끄고 같은 자리에 사격용 총을 켠다 — 결합이 끝난 상태라 겉모습이 같다.
    // 헤드셋 없이 시험할 때는 에디터 메뉴 A/시험 을 쓴다.
    // 모드 차이 (기획 4절): 학습 — 힌트(AssemblyHints), 오류 때 지금 단계를 알려 줌. 평가 — 타이머, 오류 때 "순서가 아닙니다"만, 결과에 등급·단계별 오류.
    public class AssemblyTrainingFlow : MonoBehaviour
    {
        [SerializeField] AssemblySequence m_Sequence;
        [SerializeField] TrainingPanel m_Panel;

        [Header("패널 버튼")]
        [SerializeField] Button m_LearningButton;
        [SerializeField] Button m_EvaluationButton;
        [SerializeField] Button m_RetryButton;
        [SerializeField] Button m_HomeButton;

        [Header("기능 점검")]
        [Tooltip("결합 뒤 기능 점검을 거친다. 끄면 결합이 끝나자마자 결과로 간다")]
        [SerializeField] bool m_UseFunctionCheck = true;

        [SerializeField] FunctionCheckSequence m_FunctionCheck;

        [Tooltip("작업대의 분해용 총 (K2C1_Assembly)")]
        [SerializeField] GameObject m_AssemblyRifle;

        [Tooltip("기능 점검 때 바꿔 넣는 사격용 총 (K2C1_Rifle). 씬에서는 꺼 둔다")]
        [SerializeField] K2C1Rifle m_CheckRifle;

        [Tooltip("시험용 — Play 하면 아래 모드로 바로 시작한다. 끄면 안내 화면에서 버튼으로 시작한다")]
        [SerializeField] bool m_StartOnPlay;

        [SerializeField] TrainingMode m_StartMode = TrainingMode.Learning;

        [Header("평가 등급 (임시값 — 헤드셋으로 해 본 뒤 조정)")]
        [SerializeField] GradeCriteria m_Grade = new GradeCriteria();

        float m_StartTime;
        float m_EndTime;

        public TrainingStage Stage { get; private set; } = TrainingStage.Intro;
        public TrainingMode Mode { get; private set; } = TrainingMode.Learning;
        public float ElapsedSeconds => (Stage == TrainingStage.Result ? m_EndTime : Time.time) - m_StartTime;

        // 구간이 바뀔 때 — 힌트가 듣는다
        public event System.Action<TrainingStage> StageChanged;

        public static string ModeName(TrainingMode mode) =>
            mode == TrainingMode.Learning ? AssemblyTexts.LearningMode : AssemblyTexts.EvaluationMode;

        void Awake()
        {
            m_LearningButton.onClick.AddListener(StartLearning);
            m_EvaluationButton.onClick.AddListener(StartEvaluation);
            m_RetryButton.onClick.AddListener(Retry);
            m_HomeButton.onClick.AddListener(ReturnToIntro);

            // 버튼 글자도 문구 한 곳(AssemblyTexts)에서 가져온다
            SetLabel(m_LearningButton, AssemblyTexts.LearningMode);
            SetLabel(m_EvaluationButton, AssemblyTexts.EvaluationMode);
            SetLabel(m_RetryButton, AssemblyTexts.RetryButton);
            SetLabel(m_HomeButton, AssemblyTexts.HomeButton);
        }

        static void SetLabel(Button button, string text)
        {
            var label = button.GetComponentInChildren<Text>(true);
            if (label != null)
                label.text = text;
        }

        void OnEnable()
        {
            m_Sequence.StepChanged += OnStepChanged;
            m_Sequence.OrderError += OnOrderError;
            m_Sequence.Finished += OnAssemblyFinished;
            m_FunctionCheck.StepChanged += OnCheckStepChanged;
            m_FunctionCheck.Mistake += OnCheckMistake;
            m_FunctionCheck.Finished += OnCheckFinished;
            m_FunctionCheck.Rifle.FireModeChanged += OnFireModeChanged;
        }

        void OnDisable()
        {
            m_Sequence.StepChanged -= OnStepChanged;
            m_Sequence.OrderError -= OnOrderError;
            m_Sequence.Finished -= OnAssemblyFinished;
            m_FunctionCheck.StepChanged -= OnCheckStepChanged;
            m_FunctionCheck.Mistake -= OnCheckMistake;
            m_FunctionCheck.Finished -= OnCheckFinished;
            m_FunctionCheck.Rifle.FireModeChanged -= OnFireModeChanged;
        }

        // 기능 점검 동안 머리줄에 지금 조정간을 보여 준다 (11/03)
        void OnFireModeChanged(K2C1Rifle.FireMode mode) => ShowSelector();

        void ShowSelector() => m_Panel.SetSelector(Stage == TrainingStage.FunctionCheck
            ? RifleTexts.SelectorLine(m_FunctionCheck.Rifle.Mode) : "");

        void Start()
        {
            if (m_StartOnPlay)
                StartTraining(m_StartMode);
            else
                ShowIntro();
        }

        void Update()
        {
            // 평가 모드에서만 진행 중 타이머를 보여 준다
            if (Mode == TrainingMode.Evaluation && (Stage == TrainingStage.Assembly || Stage == TrainingStage.FunctionCheck))
                m_Panel.SetTimer(ElapsedSeconds);
        }

        // --- 패널 버튼이 부른다 ---

        public void StartLearning() => StartTraining(TrainingMode.Learning);

        public void StartEvaluation() => StartTraining(TrainingMode.Evaluation);

        // 같은 모드로 처음부터
        public void Retry() => StartTraining(Mode);

        public void ReturnToIntro()
        {
            ResetAll();
            ShowIntro();
        }

        public void StartTraining(TrainingMode mode)
        {
            Mode = mode;
            ResetAll();
            m_StartTime = Time.time;
            m_Panel.SetMode(ModeName(mode));
            m_Panel.SetTimer(mode == TrainingMode.Evaluation ? 0f : -1f);
            m_Panel.ShowMessage("", 0f);
            m_Panel.SetGuide("");
            SetStage(TrainingStage.Assembly);
            m_Sequence.Begin();
        }

        // --- 구간 ---

        void OnStepChanged(int index, int count)
        {
            m_Panel.SetTitle(AssemblyTexts.StepText(index));
            m_Panel.SetGuide(Mode == TrainingMode.Learning ? AssemblyTexts.StepGuide(index) : AssemblyTexts.EvaluationGuide);
            m_Panel.SetMode(PhaseHeader(index < m_Sequence.AssemblyStartIndex ? AssemblyTexts.DisassemblyPhase : AssemblyTexts.AssemblyPhase));
            m_Panel.SetSteps(count, index, m_Sequence.AssemblyStartIndex);   // 분해 1~6 | 결합 7~12
            m_Panel.SetProgress(index + 1, count);
            if (index == m_Sequence.AssemblyStartIndex)
                m_Panel.ShowMessage(AssemblyTexts.DisassemblyDone, 2.5f);
        }

        void OnOrderError(bool insert)
        {
            // 평가 모드는 무엇을 해야 하는지 알려 주지 않는다
            if (Mode == TrainingMode.Evaluation)
            {
                m_Panel.ShowMessage(AssemblyTexts.WrongOrderRemove, 2f);
                return;
            }
            string head = insert ? AssemblyTexts.WrongOrderInsert : AssemblyTexts.WrongOrderRemove;
            m_Panel.ShowMessage($"{head}\n{AssemblyTexts.NowDoHead}{AssemblyTexts.StepText(m_Sequence.CurrentIndex)}", 2f);
        }

        void OnAssemblyFinished()
        {
            if (Stage != TrainingStage.Assembly)
                return;
            if (!m_UseFunctionCheck)
            {
                ShowResult();
                return;
            }
            SwapRifle(true);
            SetStage(TrainingStage.FunctionCheck);
            m_Panel.ShowMessage(AssemblyTexts.FunctionCheckIntro, 3f);
            m_FunctionCheck.Begin();
        }

        void OnCheckStepChanged(int index, int count)
        {
            m_Panel.SetTitle(AssemblyTexts.FunctionCheckStepText(index));
            m_Panel.SetGuide(Mode == TrainingMode.Learning ? AssemblyTexts.FunctionCheckGuide(index) : AssemblyTexts.EvaluationGuide);
            m_Panel.SetMode(PhaseHeader(AssemblyTexts.FunctionCheckTitle));
            m_Panel.SetSteps(count, index);
            m_Panel.SetProgress(index + 1, count);
        }

        // 머리줄 앞 — "학습 모드 · 분해"
        string PhaseHeader(string phase) => $"{ModeName(Mode)}  ·  {phase}";

        void HideSteps()
        {
            m_Panel.SetSteps(0, 0);
        }

        void OnCheckMistake(FunctionCheckMistake mistake)
        {
            // 평가 모드는 실수 내용(무엇을 해야 하는지)을 알려 주지 않는다
            if (Mode == TrainingMode.Evaluation)
            {
                m_Panel.ShowMessage(AssemblyTexts.WrongOrderRemove, 2f);
                return;
            }
            m_Panel.ShowMessage($"{AssemblyTexts.MistakeText(mistake)}\n{AssemblyTexts.NowDoHead}{AssemblyTexts.FunctionCheckStepText(m_FunctionCheck.CurrentIndex)}", 2.5f);
        }

        void OnCheckFinished()
        {
            if (Stage == TrainingStage.FunctionCheck)
                ShowResult();
        }

        // 기능 점검을 멈추고 분해용 총으로 되돌린 뒤 부품을 처음 상태로
        void ResetAll()
        {
            m_FunctionCheck.Stop();
            SwapRifle(false);
            m_Sequence.ResetAll();
        }

        // true: 분해용 총 → 사격용 총 (같은 자리), false: 되돌림
        void SwapRifle(bool toCheckRifle)
        {
            if (toCheckRifle)
            {
                m_CheckRifle.transform.SetPositionAndRotation(m_AssemblyRifle.transform.position, m_AssemblyRifle.transform.rotation);
                var body = m_CheckRifle.GetComponent<Rigidbody>();
                m_CheckRifle.gameObject.SetActive(true);
                if (body != null && !body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                m_AssemblyRifle.SetActive(false);
            }
            else
            {
                if (m_CheckRifle.gameObject.activeSelf)
                {
                    m_CheckRifle.ResetState();   // 손에서 놓게 한다
                    m_CheckRifle.gameObject.SetActive(false);
                }
                m_AssemblyRifle.SetActive(true);
            }
        }

        void ShowIntro()
        {
            SetStage(TrainingStage.Intro);
            m_Panel.SetMode("");
            m_Panel.SetProgress(-1, 0);
            m_Panel.SetTimer(-1f);
            m_Panel.SetTitle(AssemblyTexts.IntroTitle);
            HideSteps();
            m_Panel.ShowMessage("", 0f);
            m_Panel.SetGuide(AssemblyTexts.IntroGuide);
        }

        void ShowResult()
        {
            m_EndTime = Time.time;
            SetStage(TrainingStage.Result);
            float t = ElapsedSeconds;
            int errors = m_Sequence.Errors + m_FunctionCheck.Errors;
            m_Panel.SetProgress(-1, 0);
            m_Panel.SetTimer(-1f);
            m_Panel.SetMode(ModeName(Mode));
            HideSteps();
            m_Panel.ShowMessage("", 0f);
            if (Mode == TrainingMode.Evaluation)
            {
                var grade = AssemblyGrading.Grade(t, errors, m_Grade);
                string steps = AssemblyGrading.StepErrorSummary(m_Sequence.StepErrors, m_UseFunctionCheck ? m_FunctionCheck.StepErrors : null);
                string criteria = string.Format(AssemblyTexts.GradeCriteriaLine,
                    (int)(m_Grade.excellentSeconds / 60f), (int)(m_Grade.excellentSeconds % 60f), m_Grade.excellentMaxErrors,
                    (int)(m_Grade.passSeconds / 60f), (int)(m_Grade.passSeconds % 60f), m_Grade.passMaxErrors);
                m_Panel.SetTitle(string.Format(AssemblyTexts.EvaluationResultTitle, AssemblyTexts.GradeName(grade)));
                m_Panel.SetGuide($"{string.Format(AssemblyTexts.EvaluationSummary, (int)(t / 60f), (int)(t % 60f), errors)}\n{steps}\n{criteria}");
                Debug.Log($"[Training] 평가 결과 — {AssemblyTexts.GradeName(grade)}, {t:F1}초, 오류 {errors}회 / {steps}");
                return;
            }
            m_Panel.SetTitle(AssemblyTexts.CompletedTitle);
            m_Panel.SetGuide(string.Format(AssemblyTexts.ResultSummary, ModeName(Mode), (int)(t / 60f), (int)(t % 60f), errors));
            Debug.Log($"[Training] 결과 — {ModeName(Mode)}, {t:F1}초, 오류 {errors}회 (분해·결합 {m_Sequence.Errors}, 기능 점검 {m_FunctionCheck.Errors})");
        }

        void SetStage(TrainingStage stage)
        {
            Stage = stage;
            UpdateButtons();
            ShowSelector();
            Debug.Log($"[Training] 구간: {stage}");
            StageChanged?.Invoke(stage);
        }

        // 안내: 모드 둘 · 진행 중: 처음으로 · 결과: 다시 하기, 처음으로
        void UpdateButtons()
        {
            bool intro = Stage == TrainingStage.Intro;
            bool result = Stage == TrainingStage.Result;
            m_LearningButton.gameObject.SetActive(intro);
            m_EvaluationButton.gameObject.SetActive(intro);
            m_RetryButton.gameObject.SetActive(result);
            m_HomeButton.gameObject.SetActive(!intro);
        }
    }
}
