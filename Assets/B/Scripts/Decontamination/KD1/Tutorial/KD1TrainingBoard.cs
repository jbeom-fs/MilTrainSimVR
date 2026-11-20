using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MilTrainSimVR.B.Decontamination.KD1.Tutorial
{
    // The KD-1 course board and the one owner of the course scene's screen (the equipment board KD1Guide is kept inactive there).
    // Shows one of three panels from the course state, never from a button: start (waiting, resetting, faulted), practice
    // (running) and completion (completed with its summary).
    // - Start: title, purpose and method, the note that marks and counts are practice records, and the ready line (why the course
    //   cannot start, or that it can). The start button is usable only when the course can start; it asks KD1TrainingCourse.
    // - Practice: four step boxes (done, current, ahead), the step line, the step's purpose, the current action and the hint line
    //   (KD1GuideLines: tracking and focus come first, then the action for what is held or what is next; the same words as the
    //   equipment scene), the areas recorded so far and the restart button.
    // - Completion: title, summary, the areas recorded when the run completed (from the kept summary, not the live state) and the
    //   retry button.
    // Restart and retry both ask KD1TrainingCourse.TryReset (the only reset path; this board never resets the equipment itself).
    // Buttons are subscribed in code only; keep their OnClick lists empty. The board's pose is never changed.
    [DefaultExecutionOrder(65)]
    [DisallowMultipleComponent]
    public sealed class KD1TrainingBoard : MonoBehaviour
    {
        enum BoardScreen
        {
            None = -1,
            Start = 0,
            Practice = 1,
            Completion = 2
        }

        [SerializeField] KD1TrainingCourse course;
        [SerializeField] TMP_FontAsset koreanFont;
        [Header("Start")]
        [SerializeField] GameObject startPanel;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text descriptionText;
        [SerializeField] TMP_Text noteText;
        [SerializeField] TMP_Text readyText;
        [SerializeField] Button startButton;
        [SerializeField] TMP_Text startButtonText;
        [Header("Practice")]
        [SerializeField] GameObject practicePanel;
        [SerializeField] Image[] stepBoxes = new Image[0];
        [SerializeField] TMP_Text[] stepBoxTexts = new TMP_Text[0];
        [SerializeField] TMP_Text stepText;
        [SerializeField] TMP_Text purposeText;
        [SerializeField] TMP_Text actionText;
        [SerializeField] TMP_Text hintText;
        [SerializeField] TMP_Text progressText;
        [SerializeField] Button restartButton;
        [SerializeField] TMP_Text restartButtonText;
        [Header("Completion")]
        [SerializeField] GameObject completionPanel;
        [SerializeField] TMP_Text completionTitleText;
        [SerializeField] TMP_Text summaryText;
        [SerializeField] TMP_Text countText;
        [SerializeField] TMP_Text completionNoteText;
        [SerializeField] Button retryButton;
        [SerializeField] TMP_Text retryButtonText;
        [Header("Step box colours")]
        [SerializeField] Color stepDone = new Color(0.25f, 0.62f, 0.36f, 1f);
        [SerializeField] Color stepCurrent = new Color(0.16f, 0.42f, 0.7f, 1f);
        [SerializeField] Color stepAhead = new Color(0.22f, 0.25f, 0.28f, 1f);

        readonly Dictionary<TMP_Text, string> shown = new Dictionary<TMP_Text, string>();
        readonly List<Button> subscribed = new List<Button>();
        bool valid;
        BoardScreen screen = BoardScreen.None;
        int shownStep = -2;

        public KD1TrainingCourse Course => course;
        public Button StartButton => startButton;
        public Button RestartButton => restartButton;
        public Button RetryButton => retryButton;
        public GameObject StartPanel => startPanel;
        public GameObject PracticePanel => practicePanel;
        public GameObject CompletionPanel => completionPanel;
        public IReadOnlyList<Image> StepBoxes => stepBoxes;

        public IEnumerable<TMP_Text> Texts
        {
            get
            {
                foreach (var t in new[] { titleText, descriptionText, noteText, readyText, startButtonText, stepText, purposeText, actionText, hintText, progressText,
                             restartButtonText, completionTitleText, summaryText, countText, completionNoteText, retryButtonText })
                    yield return t;
                foreach (var t in stepBoxTexts)
                    yield return t;
            }
        }

        void Awake()
        {
            valid = TryValidate(out var error);
            if (!valid)
            {
                Debug.LogError("[KD1Training] " + error, this);
                foreach (var b in new[] { startButton, restartButton, retryButton })
                {
                    if (b != null)
                        b.interactable = false;
                }
                return;
            }
            Show(titleText, TrainingSubtitles.CourseTitle);
            Show(descriptionText, TrainingSubtitles.CourseDescription);
            Show(noteText, TrainingSubtitles.CourseNote);
            Show(startButtonText, TrainingSubtitles.StartButton);
            Show(restartButtonText, TrainingSubtitles.RestartButton);
            Show(completionTitleText, TrainingSubtitles.CompletionTitle);
            Show(summaryText, TrainingSubtitles.CompletionSummary);
            Show(completionNoteText, TrainingSubtitles.CompletionNote);
            Show(retryButtonText, TrainingSubtitles.RetryButton);
            for (var i = 0; i < stepBoxTexts.Length; i++)
                Show(stepBoxTexts[i], $"{i + 1} {TrainingSubtitles.NameOf(KD1TrainingIds.Steps[i].step)}");
            SetScreen(BoardScreen.Start);
        }

        void OnEnable()
        {
            Subscribe(startButton, OnStartClicked);
            Subscribe(restartButton, OnResetClicked);
            Subscribe(retryButton, OnResetClicked);
        }

        void OnDisable()
        {
            if (startButton != null)
                startButton.onClick.RemoveListener(OnStartClicked);
            if (restartButton != null)
                restartButton.onClick.RemoveListener(OnResetClicked);
            if (retryButton != null)
                retryButton.onClick.RemoveListener(OnResetClicked);
            subscribed.Clear();
        }

        void Subscribe(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null || subscribed.Contains(button))
                return;
            button.onClick.AddListener(action);
            subscribed.Add(button);
        }

        void LateUpdate()
        {
            if (!valid)
                return;
            switch (course.State)
            {
                case KD1CourseState.Running:
                    SetScreen(BoardScreen.Practice);
                    UpdatePractice();
                    break;
                case KD1CourseState.Completed:
                    SetScreen(BoardScreen.Completion);
                    var summary = course.Summary;
                    Show(countText, summary != null ? TrainingSubtitles.Progress(summary.AreasDone, summary.AreaCount) : string.Empty);
                    retryButton.interactable = course.CanReset;
                    break;
                default:
                    SetScreen(BoardScreen.Start);
                    UpdateStart();
                    break;
            }
        }

        void UpdateStart()
        {
            string line;
            var canStart = false;
            switch (course.State)
            {
                case KD1CourseState.Faulted:
                    line = course.Equipment != null && course.Equipment.IsFaulted ? TrainingSubtitles.ResetFailed : TrainingSubtitles.SetupInvalid;
                    break;
                case KD1CourseState.Resetting:
                    line = TrainingSubtitles.Resetting;
                    break;
                default:
                    course.TryCheckReady(out var block, out _);
                    canStart = block == KD1StartBlock.None;
                    line = block switch
                    {
                        KD1StartBlock.None => TrainingSubtitles.ReadyNow,
                        KD1StartBlock.Held => TrainingSubtitles.ReadyHeld,
                        KD1StartBlock.Waiting => TrainingSubtitles.ReadyWaiting,
                        KD1StartBlock.Focus => TrainingSubtitles.ReadyFocus,
                        KD1StartBlock.NotInitial => TrainingSubtitles.ReadyNotInitial,
                        _ => TrainingSubtitles.SetupInvalid
                    };
                    break;
            }
            Show(readyText, line);
            startButton.interactable = canStart;
        }

        void UpdatePractice()
        {
            var step = course.CurrentStep;
            if (step != shownStep)
            {
                shownStep = step;
                for (var i = 0; i < stepBoxes.Length; i++)
                    stepBoxes[i].color = i < step ? stepDone : i == step ? stepCurrent : stepAhead;
                var id = step >= 0 ? KD1TrainingIds.Steps[step].step : null;
                Show(stepText, id != null ? TrainingSubtitles.StepLine(step, id) : string.Empty);
                Show(purposeText, id != null ? TrainingSubtitles.PurposeOf(id) : string.Empty);
            }
            Show(actionText, KD1GuideLines.Action(course.Equipment, course.Practice));
            Show(hintText, KD1GuideLines.Hint(course.Equipment, course.Practice));
            Show(progressText, TrainingSubtitles.Progress(course.Practice.DoneCount, course.Practice.ZoneCount));
            restartButton.interactable = course.CanReset;
        }

        void SetScreen(BoardScreen next)
        {
            if (screen == next)
                return;
            screen = next;
            startPanel.SetActive(next == BoardScreen.Start);
            practicePanel.SetActive(next == BoardScreen.Practice);
            completionPanel.SetActive(next == BoardScreen.Completion);
            shownStep = -2;
        }

        void Show(TMP_Text text, string line)
        {
            if (text == null || shown.TryGetValue(text, out var current) && current == line)
                return;
            shown[text] = line;
            text.font = koreanFont;
            text.text = line;
        }

        void OnStartClicked()
        {
            if (!valid || !startButton.interactable)
                return;
            startButton.interactable = false;
            course.TryStart(out _);
        }

        void OnResetClicked()
        {
            if (!valid || !course.CanReset)
                return;
            restartButton.interactable = false;
            retryButton.interactable = false;
            course.TryReset();
        }

        public bool TryValidate(out string error)
        {
            var missing = new List<string>();
            if (course == null)
                missing.Add(nameof(course));
            if (koreanFont == null)
                missing.Add(nameof(koreanFont));
            if (startPanel == null || practicePanel == null || completionPanel == null)
                missing.Add("a panel");
            if (startButton == null || restartButton == null || retryButton == null)
                missing.Add("a button");
            foreach (var t in new[] { titleText, descriptionText, noteText, readyText, startButtonText, stepText, purposeText, actionText, hintText, progressText,
                         restartButtonText, completionTitleText, summaryText, countText, completionNoteText, retryButtonText })
            {
                if (t == null)
                {
                    missing.Add("a text");
                    break;
                }
            }
            if (stepBoxes == null || stepBoxTexts == null || stepBoxes.Length != KD1TrainingIds.StepCount || stepBoxTexts.Length != KD1TrainingIds.StepCount ||
                System.Array.Exists(stepBoxes, b => b == null) || System.Array.Exists(stepBoxTexts, t => t == null))
                missing.Add($"{KD1TrainingIds.StepCount} step boxes with texts");
            error = missing.Count > 0 ? $"KD1TrainingBoard '{name}' missing references: {string.Join(", ", missing)}." : null;
            if (error == null && (!startButtonText.transform.IsChildOf(startButton.transform) || !restartButtonText.transform.IsChildOf(restartButton.transform) ||
                                  !retryButtonText.transform.IsChildOf(retryButton.transform)))
                error = $"KD1TrainingBoard '{name}': each button label must be inside its button.";
            if (error == null && (!startButton.transform.IsChildOf(startPanel.transform) || !restartButton.transform.IsChildOf(practicePanel.transform) ||
                                  !retryButton.transform.IsChildOf(completionPanel.transform)))
                error = $"KD1TrainingBoard '{name}': start, restart and retry buttons must be on their panels.";
            return error == null;
        }
    }
}
