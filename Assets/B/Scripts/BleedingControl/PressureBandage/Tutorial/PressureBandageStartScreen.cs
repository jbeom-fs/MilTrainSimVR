using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MilTrainSimVR.B.BleedingControl.PressureBandage.Tutorial
{
    // Start screen and the one owner of the board's screens. The board shows one of three panels: start (this component's texts
    // and start button), practice (instruction, progress line and check card by PressureBandageTrainingGuide) and completion
    // (PressureBandageCompletionPanel). The step row (seven boxes and the step line, written by PressureBandageTrainingGuide) shows
    // above the practice panel only. Only this component activates or hides panels and the step row; PressureBandageStatusBoard
    // writes no board text in the course scene. The screen follows the real state: start until the gate accepts the start (the
    // runner then runs), completion only once the link holds the summary of the runner's Completed run (the runner completes by
    // itself on the post-wrap check with every requirement met; there is no final check screen), practice otherwise. On a
    // refused start the start panel stays with an unavailable line and the equipment stays locked. After a completed run the
    // retry brings the start panel back through ReturnToStart (or ShowResetFailed). The board's world pose is never changed. The
    // button is subscribed in code only; keep its Inspector OnClick list empty.
    [DefaultExecutionOrder(112)]
    [DisallowMultipleComponent]
    public sealed class PressureBandageStartScreen : MonoBehaviour
    {
        enum BoardScreen
        {
            Start = 0,
            Practice = 1,
            Completion = 2
        }

        [SerializeField] PressureBandageTrainingStart trainingStart;
        [SerializeField] PressureBandageStatusBoard statusBoard;
        [SerializeField] PressureBandageTrainingGuide trainingGuide;
        [SerializeField] PressureBandageCompletionPanel completionPanel;
        [SerializeField] TMP_FontAsset koreanFont;
        [Header("Start panel")]
        [SerializeField] GameObject startPanel;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text descriptionText;
        [SerializeField] TMP_Text readyText;
        [SerializeField] Button startButton;
        [SerializeField] TMP_Text startButtonText;
        [Header("Practice panel and step row (texts written by the guide)")]
        [SerializeField] GameObject practicePanel;
        [Tooltip("Step boxes and step line; shown with the practice panel only.")]
        [SerializeField] GameObject stepRow;

        Button subscribedButton;
        bool valid;
        BoardScreen shownScreen;

        public PressureBandageTrainingStart TrainingStart => trainingStart;
        public GameObject StartPanel => startPanel;
        public GameObject PracticePanel => practicePanel;
        public GameObject StepRow => stepRow;
        public Button StartButton => startButton;
        public IEnumerable<TMP_Text> Texts => new[] { titleText, descriptionText, readyText, startButtonText };

        void Awake()
        {
            valid = TryValidate(out var error);
            if (!valid)
            {
                Debug.LogError($"[PressureBandageTraining] {error} Equipment input stays locked.", this);
                if (startButton != null)
                    startButton.interactable = false;
                return;
            }
            Apply(titleText, TrainingSubtitles.CourseTitle);
            Apply(descriptionText, TrainingSubtitles.CourseDescription);
            Apply(readyText, TrainingSubtitles.StartReady);
            Apply(startButtonText, TrainingSubtitles.StartButton);
            ShowScreen(BoardScreen.Start);
            startButton.interactable = true;
        }

        void Start()
        {
            if (valid && trainingStart.State == PressureTrainingStartState.Faulted)
                ShowUnavailable();
        }

        void OnEnable()
        {
            if (subscribedButton == null && startButton != null)
            {
                subscribedButton = startButton;
                subscribedButton.onClick.AddListener(OnStartClicked);
            }
        }

        void OnDisable()
        {
            if (subscribedButton != null)
                subscribedButton.onClick.RemoveListener(OnStartClicked);
            subscribedButton = null;
        }

        // After the runner (0), the link (104), the guide (105) and the panels (110): the screen of this frame's state.
        void LateUpdate()
        {
            if (!valid)
                return;
            var screen = BoardScreen.Start;
            if (trainingStart.State == PressureTrainingStartState.Started)
            {
                screen = trainingStart.Link.Summary != null ? BoardScreen.Completion : BoardScreen.Practice;
            }
            if (screen != shownScreen)
                ShowScreen(screen);
        }

        void ShowScreen(BoardScreen screen)
        {
            shownScreen = screen;
            // Hide first so two panels are never active together.
            if (screen != BoardScreen.Start)
                startPanel.SetActive(false);
            if (screen != BoardScreen.Practice)
                practicePanel.SetActive(false);
            if (screen != BoardScreen.Completion)
                completionPanel.Panel.SetActive(false);
            PanelOf(screen).SetActive(true);
            stepRow.SetActive(screen == BoardScreen.Practice);
        }

        GameObject PanelOf(BoardScreen screen) => screen switch
        {
            BoardScreen.Practice => practicePanel,
            BoardScreen.Completion => completionPanel.Panel,
            _ => startPanel
        };

        // Only reachable through the button; repeated clicks or a second hand find the button off and the gate no longer waiting.
        void OnStartClicked()
        {
            if (!valid || trainingStart.State != PressureTrainingStartState.WaitingForStart || !startButton.interactable)
                return;
            startButton.interactable = false;
            string error = null;
            if (!statusBoard.isActiveAndEnabled || !statusBoard.TryValidate(out error)
                || !trainingGuide.isActiveAndEnabled || !trainingGuide.TryValidate(out error)
                || !completionPanel.isActiveAndEnabled || !completionPanel.TryValidate(out error))
            {
                Debug.LogError("[PressureBandageTraining] Training start refused: a board part is not running" + (error != null ? ": " + error : ".") +
                               " Equipment input stays locked.", this);
                ShowUnavailable();
                return;
            }
            if (!trainingStart.TryStart(out _))
            {
                // Still waiting: refused by the training field (another course in use), nothing changed; the button stays usable.
                if (trainingStart.State == PressureTrainingStartState.WaitingForStart)
                {
                    startButton.interactable = true;
                    return;
                }
                ShowUnavailable();
                return;
            }
            ShowScreen(BoardScreen.Practice);
        }

        public void ReturnToStart()
        {
            if (!valid)
                return;
            Apply(readyText, TrainingSubtitles.StartReady);
            startButton.interactable = true;
            ShowScreen(BoardScreen.Start);
        }

        public void ShowResetFailed()
        {
            if (!valid)
                return;
            ShowScreen(BoardScreen.Start);
            startButton.interactable = false;
            Apply(readyText, TrainingSubtitles.RetryFailed);
        }

        void ShowUnavailable()
        {
            startButton.interactable = false;
            Apply(readyText, TrainingSubtitles.StartUnavailable);
        }

        void Apply(TMP_Text target, string value)
        {
            target.font = koreanFont;
            target.text = value;
        }

        public bool TryValidate(out string error)
        {
            var missing = new List<string>();
            if (trainingStart == null)
                missing.Add(nameof(trainingStart));
            if (statusBoard == null)
                missing.Add(nameof(statusBoard));
            if (trainingGuide == null)
                missing.Add(nameof(trainingGuide));
            if (completionPanel == null || completionPanel.Panel == null)
                missing.Add(nameof(completionPanel));
            if (koreanFont == null)
                missing.Add(nameof(koreanFont));
            if (startPanel == null || practicePanel == null || stepRow == null)
                missing.Add("start or practice panel or step row");
            if (titleText == null || descriptionText == null || readyText == null || startButtonText == null)
                missing.Add("a start text");
            if (startButton == null)
                missing.Add(nameof(startButton));
            error = missing.Count > 0 ? $"PressureBandageStartScreen '{name}' missing references: {string.Join(", ", missing)}." : null;
            if (error == null)
            {
                var panels = new[] { startPanel, practicePanel, completionPanel.Panel };
                if (new HashSet<GameObject>(panels).Count != panels.Length || System.Array.Exists(panels, p => p.transform.parent != transform))
                    error = $"PressureBandageStartScreen '{name}': start, practice and completion panels must be three different children of the board.";
                else if (stepRow.transform.parent != transform || System.Array.IndexOf(panels, stepRow) >= 0)
                    error = $"PressureBandageStartScreen '{name}': the step row must be a child of the board apart from the three panels.";
                else if (statusBoard.WritesGuidance)
                    error = $"PressureBandageStartScreen '{name}': the status board must not write the title and instruction (the course guide does).";
            }
            if (error == null && (trainingStart.Link == null || completionPanel.Link != trainingStart.Link || trainingGuide.Link != trainingStart.Link))
                error = $"PressureBandageStartScreen '{name}': the guide and the completion panel must read the start gate's training link.";
            return error == null;
        }
    }
}
