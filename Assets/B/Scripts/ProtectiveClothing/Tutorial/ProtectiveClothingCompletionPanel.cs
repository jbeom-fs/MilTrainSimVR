using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MilTrainSimVR.B.ProtectiveClothing.Tutorial
{
    // Completion panel: the summary of the completed run (course completed and each part's virtual wear state), written once
    // from the summary ProtectiveClothingTrainingLink kept for this run when the runner completed by itself on the last part
    // (never from a click; there is no final check). Owns the retry button, the only retry of the course: usable only while that
    // summary exists; it asks ProtectiveClothingTrainingReset to restore the equipment and the training (a new run starts only
    // from the start button). Writes only this panel's texts; ProtectiveClothingStartScreen decides which panel shows. The
    // button is subscribed in code only; keep its Inspector OnClick list empty. No saving, score or protection result.
    [DefaultExecutionOrder(110)]
    [DisallowMultipleComponent]
    public sealed class ProtectiveClothingCompletionPanel : MonoBehaviour
    {
        [SerializeField] ProtectiveClothingTrainingLink link;
        [SerializeField] ProtectiveClothingTrainingReset trainingReset;
        [SerializeField] TMP_FontAsset koreanFont;
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text summaryText;
        [SerializeField] TMP_Text noteText;
        [SerializeField] Button retryButton;
        [SerializeField] TMP_Text retryButtonText;

        Button subscribedButton;
        bool valid;
        ProtectiveClothingTrainingSummary shown;

        public ProtectiveClothingTrainingLink Link => link;
        public ProtectiveClothingTrainingReset TrainingReset => trainingReset;
        public GameObject Panel => panel;
        public Button RetryButton => retryButton;
        public IEnumerable<TMP_Text> Texts => new[] { titleText, summaryText, noteText, retryButtonText };

        void Awake()
        {
            valid = TryValidate(out var error);
            if (!valid)
            {
                Debug.LogError($"[ProtectiveClothingTraining] {error}", this);
                if (retryButton != null)
                    retryButton.interactable = false;
                return;
            }
            Apply(retryButtonText, TrainingSubtitles.RetryButton);
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

        public void ClearForReset()
        {
            if (retryButton != null)
                retryButton.interactable = false;
            shown = null;
        }

        void OnRetryClicked()
        {
            if (!valid || !retryButton.interactable)
                return;
            retryButton.interactable = false;
            trainingReset.TryRetry();
        }

        // After the link (55) and the guide (105): this frame's summary; the start screen (112) shows the panel after it.
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

        void Write(ProtectiveClothingTrainingSummary summary)
        {
            Apply(titleText, TrainingSubtitles.CompletionTitle);
            Apply(summaryText, SummaryText(summary.Worn));
            Apply(noteText, TrainingSubtitles.CompletionNote);
        }

        // Summary text: the course line, then one line per part in course order.
        public static string SummaryText(System.Func<ProtectiveClothingPart, bool> worn)
        {
            var builder = new StringBuilder(string.Format(TrainingSubtitles.CompletionCourseFormat, ProtectiveClothingTrainingIds.StepCount));
            foreach (var part in ProtectiveClothingTrainingIds.Order)
            {
                builder.Append('\n');
                builder.Append(string.Format(worn(part) ? TrainingSubtitles.CompletionItemDoneFormat : TrainingSubtitles.CompletionItemNotDoneFormat,
                    TrainingSubtitles.Name(part)));
            }
            return builder.ToString();
        }

        void Apply(TMP_Text target, string value)
        {
            target.font = koreanFont;
            target.text = value;
        }

        public bool TryValidate(out string error)
        {
            var missing = new List<string>();
            if (link == null)
                missing.Add(nameof(link));
            if (trainingReset == null)
                missing.Add(nameof(trainingReset));
            if (koreanFont == null)
                missing.Add(nameof(koreanFont));
            if (panel == null)
                missing.Add(nameof(panel));
            if (titleText == null || summaryText == null || noteText == null || retryButtonText == null)
                missing.Add("a text");
            if (retryButton == null)
                missing.Add(nameof(retryButton));
            error = missing.Count > 0 ? $"ProtectiveClothingCompletionPanel '{name}' missing references: {string.Join(", ", missing)}." : null;
            if (error == null)
            {
                var texts = new List<TMP_Text>(Texts);
                if (new HashSet<TMP_Text>(texts).Count != texts.Count)
                    error = $"ProtectiveClothingCompletionPanel '{name}': each text must be a different object.";
                else if (!retryButton.transform.IsChildOf(panel.transform) || texts.Exists(t => !t.transform.IsChildOf(panel.transform)))
                    error = $"ProtectiveClothingCompletionPanel '{name}': texts and the retry button must be inside its panel.";
                else if (trainingReset.Link != link)
                    error = $"ProtectiveClothingCompletionPanel '{name}': the training reset must use the same training link.";
            }
            return error == null;
        }
    }
}
