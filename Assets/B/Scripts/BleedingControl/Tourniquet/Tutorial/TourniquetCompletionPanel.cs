using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MilTrainSimVR.B.BleedingControl.Tourniquet.Tutorial
{
    // Completion panel of the status board: a summary of the virtual handling performed in the completed run, from the summary
    // TourniquetTrainingLink kept when the runner reported Completed (never from the click itself). The time is
    // TourniquetOperation's recorded value (attach-time clock, fixed once recorded), so it does not grow while the panel is shown.
    // Writes only this panel's texts, once per completed run; which panel is shown is decided by TourniquetStartScreen. Owns the
    // retry button, the only retry in this course: usable only while the link holds the summary of a completed run, it asks
    // TourniquetTrainingReset to restore the device and the training and show the start screen (a new run starts only from the
    // start button). The button is subscribed in code only; keep its Inspector OnClick list empty. No course selection or
    // saving. Not a treatment result, protection effect or medical pass.
    [DefaultExecutionOrder(110)]
    [DisallowMultipleComponent]
    public sealed class TourniquetCompletionPanel : MonoBehaviour
    {
        [SerializeField] TourniquetTrainingLink link;
        [SerializeField] TMP_FontAsset koreanFont;
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text titleText;
        [Tooltip("Steps 1-4.")]
        [SerializeField] TMP_Text summaryLeftText;
        [Tooltip("Steps 5-6 and the final check.")]
        [SerializeField] TMP_Text summaryRightText;
        [SerializeField] TMP_Text timeText;
        [SerializeField] TMP_Text noteText;
        [SerializeField] Button retryButton;
        [SerializeField] TMP_Text retryButtonText;
        [SerializeField] TourniquetTrainingReset trainingReset;

        Button subscribedButton;
        bool valid;
        TourniquetTrainingSummary shown;

        public TourniquetTrainingLink Link => link;
        public GameObject Panel => panel;
        public Button RetryButton => retryButton;
        public TourniquetTrainingReset TrainingReset => trainingReset;
        public IEnumerable<TMP_Text> Texts => new[] { titleText, summaryLeftText, summaryRightText, timeText, noteText, retryButtonText };

        void Awake()
        {
            valid = TryValidate(out var error);
            if (!valid)
            {
                Debug.LogError($"[TourniquetTraining] {error}", this);
                if (retryButton != null)
                    retryButton.interactable = false;
                return;
            }
            ApplyText(retryButtonText, TrainingSubtitles.RetryButton);
            retryButton.interactable = false;
        }

        void OnEnable()
        {
            if (subscribedButton == null && retryButton != null)
            {
                subscribedButton = retryButton;
                subscribedButton.onClick.AddListener(OnRetryClicked);
            }
        }

        void OnDisable()
        {
            if (subscribedButton != null)
                subscribedButton.onClick.RemoveListener(OnRetryClicked);
            subscribedButton = null;
        }

        // Retry entry point (TourniquetTrainingReset): button off and the summary cache cleared.
        public void ClearForReset()
        {
            if (retryButton != null)
                retryButton.interactable = false;
            shown = null;
        }

        // Only reachable through the button of the shown completion panel. The button goes off at once, so a second click or
        // hand in the same frame is ignored; the reset itself also runs once per completed run.
        void OnRetryClicked()
        {
            if (!valid || !retryButton.interactable)
                return;
            retryButton.interactable = false;
            trainingReset.TryRetry();
        }

        void LateUpdate()
        {
            if (!valid)
                return;
            var summary = link.Summary;
            retryButton.interactable = summary != null && !trainingReset.IsResetting;
            if (summary == null || summary == shown)
                return;
            shown = summary;
            Write(summary);
        }

        void Write(TourniquetTrainingSummary summary)
        {
            var (left, right) = SummaryColumns(summary.StateAt, summary.FinalCheckConfirmed);
            var total = Mathf.FloorToInt(summary.RecordedElapsedSeconds);
            ApplyText(titleText, TrainingSubtitles.CompletionTitle);
            ApplyText(summaryLeftText, left);
            ApplyText(summaryRightText, right);
            ApplyText(timeText, summary.TimeRecorded ? string.Format(TrainingSubtitles.TimeRecordedFormat, total / 60, total % 60)
                                                     : TrainingSubtitles.TimeNotRecorded);
            ApplyText(noteText, TrainingSubtitles.CompletionNote);
        }

        // Rows of the summary columns: the six practice steps then the final check (seven lines), steps 1-4 left and the rest right.
        public const int SummaryLeftRows = 4;

        public static (string left, string right) SummaryColumns(Func<int, bool> stepDone, bool finalConfirmed)
        {
            var lines = new List<string>();
            var steps = TourniquetTrainingIds.Steps;
            for (var i = 0; i < steps.Length; i++)
                lines.Add(Item(TourniquetTrainingGuide.StepName(steps[i].step), stepDone(i)));
            lines.Add(Item(TrainingSubtitles.FinalCheckTitle, finalConfirmed));
            return (string.Join("\n", lines.GetRange(0, SummaryLeftRows)), string.Join("\n", lines.GetRange(SummaryLeftRows, lines.Count - SummaryLeftRows)));
        }

        static string Item(string name, bool done) =>
            string.Format(done ? TrainingSubtitles.CompletionItemDoneFormat : TrainingSubtitles.CompletionItemNotDoneFormat, name);

        void ApplyText(TMP_Text target, string value)
        {
            target.font = koreanFont;
            target.text = value;
        }

        public bool TryValidate(out string error)
        {
            var missing = new List<string>();
            if (link == null)
                missing.Add(nameof(link));
            if (koreanFont == null)
                missing.Add(nameof(koreanFont));
            if (panel == null)
                missing.Add(nameof(panel));
            if (titleText == null)
                missing.Add(nameof(titleText));
            if (summaryLeftText == null)
                missing.Add(nameof(summaryLeftText));
            if (summaryRightText == null)
                missing.Add(nameof(summaryRightText));
            if (timeText == null)
                missing.Add(nameof(timeText));
            if (noteText == null)
                missing.Add(nameof(noteText));
            if (retryButton == null)
                missing.Add(nameof(retryButton));
            if (retryButtonText == null)
                missing.Add(nameof(retryButtonText));
            if (trainingReset == null)
                missing.Add(nameof(trainingReset));

            error = missing.Count > 0 ? $"TourniquetCompletionPanel '{name}' missing references: {string.Join(", ", missing)}." : null;
            if (error == null)
            {
                var texts = new List<TMP_Text>(Texts);
                if (new HashSet<TMP_Text>(texts).Count != texts.Count)
                    error = $"TourniquetCompletionPanel '{name}': each text must be a different object.";
                else if (!retryButton.transform.IsChildOf(panel.transform) || texts.Exists(t => !t.transform.IsChildOf(panel.transform)))
                    error = $"TourniquetCompletionPanel '{name}': texts and the retry button must be inside its panel.";
                else if (trainingReset.Link != link)
                    error = $"TourniquetCompletionPanel '{name}': the training reset must use the same training link.";
            }
            return error == null;
        }
    }
}
