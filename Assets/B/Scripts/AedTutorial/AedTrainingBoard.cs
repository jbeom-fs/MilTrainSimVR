using System.Collections.Generic;
using MilTrainSimVR.B.TrainingUI;
using TMPro;
using UnityEngine;

namespace MilTrainSimVR.B.AedTutorial
{
    // Course board adapter of the AED tutorial in the KD-1 board layout. AedTutorialStartController keeps the whole tutorial (start,
    // power, pads, contact check, rhythm analysis, charge, shock, retry) and its own texts, panels and buttons; this component only
    // reads its State and writes the board parts the controller has none of:
    // - start panel: the ready line (the start button can be used now, or the tutorial is blocked);
    // - guide panel: the step row (four display steps grouping the existing states: power, pads, analysis, charge and shock), the
    //   step line and the step's purpose line above the controller's instruction. An interruption that needs a scene reload keeps the
    //   last step shown;
    // - completion panel: shown over the guide panel only after the shock (ShockDeliveredAwaitingNext, the only state with the retry
    //   button), with the completion title and the follow-up note; the controller's retry button stays above it.
    // Never changes the controller, the devices or the pads; no button of its own. Lives on the board root, outside the panels.
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class AedTrainingBoard : MonoBehaviour
    {
        const int StepCount = 4;
        const int Keep = -2;

        [SerializeField] AedTutorialStartController controller;
        [SerializeField] TMP_FontAsset koreanFont;
        [Header("Start panel")]
        [SerializeField] TMP_Text readyText;
        [Header("Guide panel")]
        [SerializeField] TrainingStepRow stepRow;
        [SerializeField] TMP_Text stepText;
        [Tooltip("Purpose line of the board step, above the controller's instruction.")]
        [SerializeField] TMP_Text purposeText;
        [Header("Completion panel (board child after the guide panel, before the retry button)")]
        [SerializeField] GameObject completionPanel;
        [SerializeField] TMP_Text completionTitleText;
        [SerializeField] TMP_Text completionNoteText;

        static readonly string[] StepNames =
        {
            TrainingSubtitles.StepPower, TrainingSubtitles.StepPads, TrainingSubtitles.StepAnalysis, TrainingSubtitles.StepShock
        };

        public static readonly string[] StepPurposes =
        {
            TrainingSubtitles.PurposePower, TrainingSubtitles.PurposePads, TrainingSubtitles.PurposeAnalysis, TrainingSubtitles.PurposeShock
        };

        bool valid;
        int shownStep = int.MinValue;
        string shownReady;

        public AedTutorialStartController Controller => controller;
        public TrainingStepRow StepRow => stepRow;
        public GameObject CompletionPanel => completionPanel;
        public IEnumerable<TMP_Text> Texts => new[] { readyText, stepText, purposeText, completionTitleText, completionNoteText };

        void Awake()
        {
            valid = TryValidate(out var error);
            if (!valid)
            {
                Debug.LogError("[AedTutorial] " + error, this);
                return;
            }
            Apply(completionTitleText, TrainingSubtitles.CompletionTitle);
            Apply(completionNoteText, TrainingSubtitles.CompletionFollowUp);
            completionPanel.SetActive(false);
        }

        // After the controller's Update and its button and device events of this frame.
        void LateUpdate()
        {
            if (!valid)
                return;
            var state = controller.State;
            var ready = state == AedTutorialStartState.WaitingForStart ? TrainingSubtitles.StartReady
                : state == AedTutorialStartState.Faulted ? TrainingSubtitles.StartUnavailable
                : string.Empty;
            if (ready != shownReady)
            {
                shownReady = ready;
                Apply(readyText, ready);
            }

            var completed = state == AedTutorialStartState.ShockDeliveredAwaitingNext;
            if (completionPanel.activeSelf != completed)
                completionPanel.SetActive(completed);

            var step = StepOf(state);
            if (step == Keep || step == shownStep)
                return;
            shownStep = step;
            stepRow.Show(step);
            var shownStepNow = step >= 0 && step < StepCount;
            Apply(stepText, shownStepNow ? string.Format(TrainingSubtitles.StepLineFormat, step + 1, StepCount, StepNames[step]) : string.Empty);
            Apply(purposeText, shownStepNow ? StepPurposes[step] : string.Empty);
        }

        // Display step of a tutorial state; StepCount after the shock (every box done), Keep for the reload-required interruption.
        static int StepOf(AedTutorialStartState state) => state switch
        {
            AedTutorialStartState.AwaitingPowerButton => 0,
            AedTutorialStartState.AwaitingPadPickup => 1,
            AedTutorialStartState.GuidingPadPlacement => 1,
            AedTutorialStartState.AwaitingHandsOff => 2,
            AedTutorialStartState.AnalyzingRhythm => 2,
            AedTutorialStartState.AnalysisCompleteAwaitingNext => 3,
            AedTutorialStartState.ChargingShock => 3,
            AedTutorialStartState.AwaitingShockButton => 3,
            AedTutorialStartState.ShockDeliveredAwaitingNext => StepCount,
            AedTutorialStartState.InterruptedReloadRequired => Keep,
            _ => -1
        };

        void Apply(TMP_Text target, string value)
        {
            target.font = koreanFont;
            target.text = value;
        }

        public bool TryValidate(out string error)
        {
            var missing = new List<string>();
            if (controller == null)
                missing.Add(nameof(controller));
            if (koreanFont == null)
                missing.Add(nameof(koreanFont));
            if (stepRow == null)
                missing.Add(nameof(stepRow));
            if (completionPanel == null)
                missing.Add(nameof(completionPanel));
            if (readyText == null || stepText == null || purposeText == null || completionTitleText == null || completionNoteText == null)
                missing.Add("a text");
            error = missing.Count > 0 ? $"AedTrainingBoard '{name}' missing references: {string.Join(", ", missing)}." : null;
            if (error == null && !stepRow.TryValidate(out error))
                return false;
            if (error == null && stepRow.Count != StepCount)
                error = $"AedTrainingBoard '{name}': the step row needs {StepCount} boxes, not {stepRow.Count}.";
            if (error == null && (!completionTitleText.transform.IsChildOf(completionPanel.transform) || !completionNoteText.transform.IsChildOf(completionPanel.transform)))
                error = $"AedTrainingBoard '{name}': the completion texts must be inside the completion panel.";
            if (error == null && new HashSet<TMP_Text>(Texts).Count != 5)
                error = $"AedTrainingBoard '{name}': each text must be a different object.";
            return error == null;
        }
    }
}
