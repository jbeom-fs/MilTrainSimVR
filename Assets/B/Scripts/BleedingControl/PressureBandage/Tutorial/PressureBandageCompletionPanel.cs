using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MilTrainSimVR.B.BleedingControl.PressureBandage.Tutorial
{
    // Completion panel: the summary of the completed run (the seven course steps' results: situation, dressing, contact practice,
    // pre-wrap check, wrap, end and tape fixing, post-wrap check), written once from the summary PressureBandageTrainingLink kept
    // when the runner reported Completed on its own (never from a click; there is no final input to list). Owns the retry button, the only retry of the course: usable only while that summary
    // exists; it asks PressureBandageTrainingReset to restore the equipment and the training (a new run starts only from the start
    // button). Writes only this panel's texts; PressureBandageStartScreen decides which panel shows. The button is subscribed in
    // code only; keep its Inspector OnClick list empty. No saving, scoring or medical result.
    [DefaultExecutionOrder(110)]
    [DisallowMultipleComponent]
    public sealed class PressureBandageCompletionPanel : MonoBehaviour
    {
        [SerializeField] PressureBandageTrainingLink link;
        [SerializeField] PressureBandageTrainingReset trainingReset;
        [SerializeField] TMP_FontAsset koreanFont;
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text summaryText;
        [SerializeField] TMP_Text noteText;
        [SerializeField] Button retryButton;
        [SerializeField] TMP_Text retryButtonText;

        Button subscribedButton;
        bool valid;
        PressureBandageTrainingSummary shown;

        public PressureBandageTrainingLink Link => link;
        public PressureBandageTrainingReset TrainingReset => trainingReset;
        public GameObject Panel => panel;
        public Button RetryButton => retryButton;
        public IEnumerable<TMP_Text> Texts => new[] { titleText, summaryText, noteText, retryButtonText };

        void Awake()
        {
            valid = TryValidate(out var error);
            if (!valid)
            {
                Debug.LogError($"[PressureBandageTraining] {error}", this);
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

        void Write(PressureBandageTrainingSummary summary)
        {
            Apply(titleText, TrainingSubtitles.CompletionTitle);
            Apply(summaryText, SummaryText(summary.Met));
            Apply(noteText, TrainingSubtitles.CompletionNote);
        }

        // Summary text: one line per requirement in course order.
        public static string SummaryText(System.Func<PressureRequirement, bool> met)
        {
            var builder = new StringBuilder();
            foreach (var requirement in PressureBandageTrainingIds.AllRequirements)
            {
                if (builder.Length > 0)
                    builder.Append('\n');
                builder.Append(Item(PressureBandageTrainingIds.RequirementName(requirement), met(requirement)));
            }
            return builder.ToString();
        }

        static string Item(string name, bool done) =>
            string.Format(done ? TrainingSubtitles.CompletionItemDoneFormat : TrainingSubtitles.CompletionItemNotDoneFormat, name);

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
            error = missing.Count > 0 ? $"PressureBandageCompletionPanel '{name}' missing references: {string.Join(", ", missing)}." : null;
            if (error == null)
            {
                var texts = new List<TMP_Text>(Texts);
                if (new HashSet<TMP_Text>(texts).Count != texts.Count)
                    error = $"PressureBandageCompletionPanel '{name}': each text must be a different object.";
                else if (!retryButton.transform.IsChildOf(panel.transform) || texts.Exists(t => !t.transform.IsChildOf(panel.transform)))
                    error = $"PressureBandageCompletionPanel '{name}': texts and the retry button must be inside its panel.";
                else if (trainingReset.Link != link)
                    error = $"PressureBandageCompletionPanel '{name}': the training reset must use the same training link.";
            }
            return error == null;
        }
    }
}
