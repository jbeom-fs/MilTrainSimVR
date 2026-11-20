using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MilTrainSimVR.B.BleedingControl.Tourniquet.Tutorial
{
    // Start screen and the one owner of the board's screens. The board shows one of four panels: start (this component's texts
    // and start button), operation (step line and action by TourniquetTrainingGuide, status and detail by TourniquetStatusBoard),
    // final check (TourniquetFinalCheck) and completion (TourniquetCompletionPanel); the prototype notice outside the panels
    // stays visible and stays owned by TourniquetStatusBoard. Only this component activates or hides panels; the others write
    // their own texts only. The screen follows the real state: start until TourniquetTrainingStart accepts the start (which
    // starts the training runner), then final check while the runner waits on the final check step of that run, completion only
    // once the link holds the summary of the runner's Completed run, operation otherwise (also a stopped run). On a refused start
    // the start panel stays with an unavailable line and the equipment stays locked. After a completed run the retry
    // (TourniquetTrainingReset) brings the board back to the start panel through ReturnToStart (or ShowResetFailed); there is
    // no retry during the practice or the final check. The board's world pose is never changed. The button is subscribed in code only; keep its Inspector OnClick list empty.
    [DefaultExecutionOrder(112)]
    [DisallowMultipleComponent]
    public sealed class TourniquetStartScreen : MonoBehaviour
    {
        enum BoardScreen
        {
            Start = 0,
            Operation = 1,
            FinalCheck = 2,
            Completion = 3
        }

        [SerializeField] TourniquetTrainingStart trainingStart;
        [Tooltip("Writes the operation panel texts and the shared notice; must be enabled for the operation guide to show.")]
        [SerializeField] TourniquetStatusBoard statusBoard;
        [Tooltip("Writes the operation panel step line and action; must be enabled for the step guide to show.")]
        [SerializeField] TourniquetTrainingGuide trainingGuide;
        [Tooltip("Writes the final check panel and owns its confirm button; its panel is shown by this component.")]
        [SerializeField] TourniquetFinalCheck finalCheck;
        [Tooltip("Writes the completion panel; its panel is shown by this component.")]
        [SerializeField] TourniquetCompletionPanel completionPanel;
        [SerializeField] TMP_FontAsset koreanFont;

        [Header("Start panel")]
        [SerializeField] GameObject startPanel;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text descriptionText;
        [SerializeField] TMP_Text readyText;
        [SerializeField] Button startButton;
        [SerializeField] TMP_Text startButtonText;

        [Header("Operation panel (texts written by TourniquetTrainingGuide and TourniquetStatusBoard)")]
        [SerializeField] GameObject operationPanel;

        Button subscribedButton;
        bool valid;
        BoardScreen shownScreen;

        public TourniquetTrainingStart TrainingStart => trainingStart;
        public TourniquetStatusBoard StatusBoard => statusBoard;
        public TourniquetTrainingGuide TrainingGuide => trainingGuide;
        public TourniquetFinalCheck FinalCheck => finalCheck;
        public TourniquetCompletionPanel CompletionPanel => completionPanel;
        public GameObject StartPanel => startPanel;
        public GameObject OperationPanel => operationPanel;
        public Button StartButton => startButton;

        void Awake()
        {
            valid = TryValidate(out var error);
            if (!valid)
            {
                Debug.LogError($"[Tourniquet] {error} Equipment input stays locked.", this);
                if (startButton != null)
                    startButton.interactable = false;
                return;
            }

            ApplyText(titleText, TrainingSubtitles.StartTitle);
            ApplyText(descriptionText, TrainingSubtitles.StartDescription);
            ApplyText(readyText, TrainingSubtitles.StartReady);
            ApplyText(startButtonText, TrainingSubtitles.StartButton);
            ShowScreen(BoardScreen.Start);
            startButton.interactable = true;
        }

        // After the runner (0), the link (106), the guide and the panels (110): the screen of this frame's state.
        void LateUpdate()
        {
            if (!valid)
                return;

            var screen = BoardScreen.Start;
            if (trainingStart.State == TourniquetTrainingStartState.Started)
            {
                var link = trainingStart.Link;
                screen = link.Summary != null ? BoardScreen.Completion
                    : link.IsFinalCheckStep ? BoardScreen.FinalCheck
                    : BoardScreen.Operation;
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
            if (screen != BoardScreen.Operation)
                operationPanel.SetActive(false);
            if (screen != BoardScreen.FinalCheck)
                finalCheck.Panel.SetActive(false);
            if (screen != BoardScreen.Completion)
                completionPanel.Panel.SetActive(false);
            PanelOf(screen).SetActive(true);
        }

        GameObject PanelOf(BoardScreen screen) => screen switch
        {
            BoardScreen.Operation => operationPanel,
            BoardScreen.FinalCheck => finalCheck.Panel,
            BoardScreen.Completion => completionPanel.Panel,
            _ => startPanel
        };

        // After every Awake: a gate that faulted on its own references shows as unavailable from the first frame.
        void Start()
        {
            if (valid && trainingStart.State == TourniquetTrainingStartState.Faulted)
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

        // Only reachable through the button. Repeated clicks or a second hand in the same frame find the button off and the gate
        // no longer waiting.
        void OnStartClicked()
        {
            if (!valid || trainingStart.State != TourniquetTrainingStartState.WaitingForStart || !startButton.interactable)
                return;

            startButton.interactable = false;
            string error = null;
            if (!statusBoard.isActiveAndEnabled || !statusBoard.TryValidate(out error)
                || !trainingGuide.isActiveAndEnabled || !trainingGuide.TryValidate(out error)
                || !finalCheck.isActiveAndEnabled || !finalCheck.TryValidate(out error)
                || !completionPanel.isActiveAndEnabled || !completionPanel.TryValidate(out error))
            {
                Debug.LogError("[Tourniquet] Training start refused: a board part (TourniquetStatusBoard, TourniquetTrainingGuide, " +
                               "TourniquetFinalCheck or TourniquetCompletionPanel) is not running" +
                               (error != null ? ": " + error : ".") + " Equipment input stays locked.", this);
                ShowUnavailable();
                return;
            }
            if (!trainingStart.TryStart(out error))
            {
                // TourniquetTrainingStart logged the reason. Still waiting: refused by the training field (another course in use),
                // nothing changed, so the button stays usable.
                if (trainingStart.State == TourniquetTrainingStartState.WaitingForStart)
                {
                    startButton.interactable = true;
                    return;
                }
                ShowUnavailable();
                return;
            }

            ShowScreen(BoardScreen.Operation);
        }

        // Retry entry point, last step (TourniquetTrainingReset, gate back to WaitingForStart): the start panel as at scene start.
        public void ReturnToStart()
        {
            if (!valid)
                return;
            ApplyText(readyText, TrainingSubtitles.StartReady);
            startButton.interactable = true;
            ShowScreen(BoardScreen.Start);
        }

        // Retry failed (gate faulted, equipment locked): the start panel with the button off and the reason line.
        public void ShowResetFailed()
        {
            if (!valid)
                return;
            ShowScreen(BoardScreen.Start);
            startButton.interactable = false;
            ApplyText(readyText, TrainingSubtitles.RetryFailed);
        }

        void ShowUnavailable()
        {
            startButton.interactable = false;
            ApplyText(readyText, TrainingSubtitles.StartUnavailable);
        }

        void ApplyText(TMP_Text target, string value)
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
            if (finalCheck == null || finalCheck.Panel == null)
                missing.Add(nameof(finalCheck));
            if (completionPanel == null || completionPanel.Panel == null)
                missing.Add(nameof(completionPanel));
            if (koreanFont == null)
                missing.Add(nameof(koreanFont));
            if (startPanel == null)
                missing.Add(nameof(startPanel));
            if (titleText == null)
                missing.Add(nameof(titleText));
            if (descriptionText == null)
                missing.Add(nameof(descriptionText));
            if (readyText == null)
                missing.Add(nameof(readyText));
            if (startButton == null)
                missing.Add(nameof(startButton));
            if (startButtonText == null)
                missing.Add(nameof(startButtonText));
            if (operationPanel == null)
                missing.Add(nameof(operationPanel));

            error = missing.Count > 0 ? $"TourniquetStartScreen '{name}' missing references: {string.Join(", ", missing)}." : null;
            if (error == null)
            {
                var panels = new[] { startPanel, operationPanel, finalCheck.Panel, completionPanel.Panel };
                if (new HashSet<GameObject>(panels).Count != panels.Length || System.Array.Exists(panels, p => p.transform.parent != transform))
                    error = $"TourniquetStartScreen '{name}': start, operation, final check and completion panels must be four different children of the board.";
            }
            if (error == null && (trainingStart.Link == null || finalCheck.Link != trainingStart.Link || completionPanel.Link != trainingStart.Link))
                error = $"TourniquetStartScreen '{name}': the final check and completion panels must read the start gate's training link.";
            return error == null;
        }
    }
}
