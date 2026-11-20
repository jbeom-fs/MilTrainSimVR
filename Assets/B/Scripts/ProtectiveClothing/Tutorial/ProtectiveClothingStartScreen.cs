using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MilTrainSimVR.B.ProtectiveClothing.Tutorial
{
    // Start screen and the one owner of the course board's screens. The board shows one of three panels: start (this
    // component's texts and start button), practice (instruction and status line by ProtectiveClothingTrainingGuide) and
    // completion (ProtectiveClothingCompletionPanel). The step row (six boxes and the step line, written by the guide) shows above
    // the practice panel only. Only this component activates or hides panels and the step row. The screen follows the real
    // state: start until the gate accepts the start (the runner then runs this course), completion only once the link holds the
    // summary of this run's Completed runner (completed by itself on the last part; there is no final check), practice
    // otherwise. While waiting, the ready line shows whether the course can start now or why not, and the start button is
    // usable only when it can. On a refused start the start panel stays and the equipment stays locked. After a completed run
    // the retry brings the start panel back through ReturnToStart (or ShowResetFailed). The board's world pose is never changed.
    // The button is subscribed in code only; keep its Inspector OnClick list empty.
    [DefaultExecutionOrder(112)]
    [DisallowMultipleComponent]
    public sealed class ProtectiveClothingStartScreen : MonoBehaviour
    {
        enum BoardScreen
        {
            Start = 0,
            Practice = 1,
            Completion = 2
        }

        [SerializeField] ProtectiveClothingTrainingStart trainingStart;
        [SerializeField] ProtectiveClothingTrainingGuide trainingGuide;
        [SerializeField] ProtectiveClothingCompletionPanel completionPanel;
        [SerializeField] TMP_FontAsset koreanFont;
        [Header("Start panel")]
        [SerializeField] GameObject startPanel;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text descriptionText;
        [SerializeField] TMP_Text controlsText;
        [SerializeField] TMP_Text readyText;
        [SerializeField] Button startButton;
        [SerializeField] TMP_Text startButtonText;
        [Header("Practice panel and step row (texts written by the guide)")]
        [SerializeField] GameObject practicePanel;
        [Tooltip("Step boxes and step line; shown with the practice panel only.")]
        [SerializeField] GameObject stepRow;

        Button subscribedButton;
        bool valid;
        bool resetFailed;
        BoardScreen shownScreen;
        string shownReady;

        public ProtectiveClothingTrainingStart TrainingStart => trainingStart;
        public GameObject StartPanel => startPanel;
        public GameObject PracticePanel => practicePanel;
        public GameObject StepRow => stepRow;
        public Button StartButton => startButton;
        public IEnumerable<TMP_Text> Texts => new[] { titleText, descriptionText, controlsText, readyText, startButtonText };

        void Awake()
        {
            valid = TryValidate(out var error);
            if (!valid)
            {
                Debug.LogError($"[ProtectiveClothingTraining] {error} Equipment input stays locked.", this);
                if (startButton != null)
                    startButton.interactable = false;
                return;
            }
            Apply(titleText, TrainingSubtitles.CourseTitle);
            Apply(descriptionText, TrainingSubtitles.CourseDescription);
            Apply(controlsText, TrainingSubtitles.CourseControls);
            Apply(startButtonText, TrainingSubtitles.StartButton);
            startButton.interactable = false;
            ShowScreen(BoardScreen.Start);
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

        // After the runner (0), the link (55), the guide (105) and the completion panel (110): the screen of this frame's state.
        void LateUpdate()
        {
            if (!valid)
                return;
            var state = trainingStart.State;
            var screen = BoardScreen.Start;
            if (state == ProtectiveClothingStartState.Started)
                screen = trainingStart.Link.Summary != null ? BoardScreen.Completion : BoardScreen.Practice;
            if (screen != shownScreen)
                ShowScreen(screen);
            if (screen != BoardScreen.Start)
                return;

            // Start panel: the ready line and the button follow the current readiness (rechecked every frame).
            var ready = false;
            string line;
            if (resetFailed)
                line = TrainingSubtitles.RetryFailed;
            else if (state == ProtectiveClothingStartState.Faulted)
                line = TrainingSubtitles.StartUnavailable;
            else if (state == ProtectiveClothingStartState.Resetting)
                line = shownReady;
            else
            {
                trainingStart.TryCheckReady(out var block, out _);
                ready = block == ProtectiveClothingStartBlock.None;
                line = block switch
                {
                    ProtectiveClothingStartBlock.None => TrainingSubtitles.StartReady,
                    ProtectiveClothingStartBlock.NotInitial => TrainingSubtitles.StartNotInitial,
                    ProtectiveClothingStartBlock.Held => TrainingSubtitles.StartPutDown,
                    ProtectiveClothingStartBlock.Waiting => TrainingSubtitles.StartBodyWait,
                    ProtectiveClothingStartBlock.Focus => ProtectiveClothingSubtitles.Paused,
                    _ => TrainingSubtitles.StartUnavailable
                };
            }
            if (line != shownReady)
            {
                shownReady = line;
                Apply(readyText, line ?? string.Empty);
            }
            startButton.interactable = ready && state == ProtectiveClothingStartState.WaitingForStart;
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
            if (screen != BoardScreen.Start)
                startButton.interactable = false;
        }

        GameObject PanelOf(BoardScreen screen) => screen switch
        {
            BoardScreen.Practice => practicePanel,
            BoardScreen.Completion => completionPanel.Panel,
            _ => startPanel
        };

        // Only reachable through the button; it goes off at once, so a repeated click or a second hand in the same frame finds it
        // off and the gate no longer waiting.
        void OnStartClicked()
        {
            if (!valid || trainingStart.State != ProtectiveClothingStartState.WaitingForStart || !startButton.interactable)
                return;
            startButton.interactable = false;
            string error = null;
            if (!trainingGuide.isActiveAndEnabled || !trainingGuide.TryValidate(out error)
                || !completionPanel.isActiveAndEnabled || !completionPanel.TryValidate(out error))
            {
                Debug.LogError("[ProtectiveClothingTraining] Training start refused: a board part is not running" + (error != null ? ": " + error : ".") +
                               " Equipment input stays locked.", this);
                trainingStart.FaultReset();
                return;
            }
            if (trainingStart.TryStart(out _))
                ShowScreen(BoardScreen.Practice);
        }

        // Retry done (ProtectiveClothingTrainingReset): start panel back, the ready line rechecked from the next frame.
        public void ReturnToStart()
        {
            if (!valid)
                return;
            resetFailed = false;
            shownReady = null;
            startButton.interactable = false;
            ShowScreen(BoardScreen.Start);
        }

        public void ShowResetFailed()
        {
            if (!valid)
                return;
            resetFailed = true;
            startButton.interactable = false;
            ShowScreen(BoardScreen.Start);
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
            if (trainingGuide == null)
                missing.Add(nameof(trainingGuide));
            if (completionPanel == null || completionPanel.Panel == null)
                missing.Add(nameof(completionPanel));
            if (koreanFont == null)
                missing.Add(nameof(koreanFont));
            if (startPanel == null || practicePanel == null || stepRow == null)
                missing.Add("start or practice panel or step row");
            if (titleText == null || descriptionText == null || controlsText == null || readyText == null || startButtonText == null)
                missing.Add("a start text");
            if (startButton == null)
                missing.Add(nameof(startButton));
            error = missing.Count > 0 ? $"ProtectiveClothingStartScreen '{name}' missing references: {string.Join(", ", missing)}." : null;
            if (error == null)
            {
                var panels = new[] { startPanel, practicePanel, completionPanel.Panel };
                if (new HashSet<GameObject>(panels).Count != panels.Length || System.Array.Exists(panels, p => p.transform.parent != transform))
                    error = $"ProtectiveClothingStartScreen '{name}': start, practice and completion panels must be three different children of the board.";
                else if (stepRow.transform.parent != transform || System.Array.IndexOf(panels, stepRow) >= 0)
                    error = $"ProtectiveClothingStartScreen '{name}': the step row must be a child of the board apart from the three panels.";
                else if (!startButton.transform.IsChildOf(startPanel.transform))
                    error = $"ProtectiveClothingStartScreen '{name}': the start button must be inside the start panel.";
            }
            if (error == null && (trainingStart.Link == null || completionPanel.Link != trainingStart.Link || trainingGuide.Link != trainingStart.Link ||
                                  trainingGuide.TrainingStart != trainingStart))
                error = $"ProtectiveClothingStartScreen '{name}': the guide and the completion panel must read the start gate's training link.";
            return error == null;
        }
    }
}
