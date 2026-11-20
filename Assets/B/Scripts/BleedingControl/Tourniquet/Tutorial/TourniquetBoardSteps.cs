using System.Collections.Generic;
using MilTrainSimVR.B.Training;
using MilTrainSimVR.B.TrainingUI;
using UnityEngine;

namespace MilTrainSimVR.B.BleedingControl.Tourniquet.Tutorial
{
    // Step row of the tourniquet course board (KD-1 board layout): six display-only boxes for the six practice steps, shown with
    // the operation and final check panels only. It reads which panel TourniquetStartScreen shows this frame (the only panel switcher)
    // and the guide target TourniquetTrainingGuide decided this frame (on the same board object, order 110: the step line, purpose,
    // action and markers name the same target); it never changes the runner, the link, the guide or a panel and never judges a
    // recovery itself. Current box: the guide target (the runner's practice step, or the earlier step being restored while the step
    // line reads "보완 k/6"); on the final check every box is done; no guided step (stopped run) shows no current box. The row object
    // is switched here only.
    [DefaultExecutionOrder(115)]
    [DisallowMultipleComponent]
    public sealed class TourniquetBoardSteps : MonoBehaviour
    {
        [SerializeField] TrainingRunner runner;
        [SerializeField] TourniquetTrainingLink link;
        [SerializeField] TrainingStepRow stepRow;
        [SerializeField] GameObject operationPanel;
        [SerializeField] GameObject finalCheckPanel;

        bool valid;
        // The board's guide, found on this object (no serialized reference).
        TourniquetTrainingGuide guide;

        public TrainingStepRow StepRow => stepRow;

        void Awake()
        {
            valid = TryValidate(out var error);
            if (!valid)
                Debug.LogError($"[TourniquetTraining] {error}", this);
        }

        // After the guide (110) decided this frame's target and the start screen (112) switched this frame's panel.
        void LateUpdate()
        {
            if (!valid)
                return;
            var shown = operationPanel.activeSelf || finalCheckPanel.activeSelf;
            if (stepRow.gameObject.activeSelf != shown)
                stepRow.gameObject.SetActive(shown);
            if (!shown)
                return;
            var current = link.IsFinalCheckStep ? TourniquetTrainingIds.Steps.Length : guide.GuideStepIndex;
            stepRow.Show(current);
        }

        public bool TryValidate(out string error)
        {
            guide = GetComponent<TourniquetTrainingGuide>();
            var missing = new List<string>();
            if (runner == null)
                missing.Add(nameof(runner));
            if (link == null)
                missing.Add(nameof(link));
            if (stepRow == null)
                missing.Add(nameof(stepRow));
            if (operationPanel == null || finalCheckPanel == null)
                missing.Add("operation or final check panel");
            if (guide == null)
                missing.Add("TourniquetTrainingGuide on this object");
            error = missing.Count > 0 ? $"TourniquetBoardSteps '{name}' missing references: {string.Join(", ", missing)}." : null;
            if (error == null && link.Runner != runner)
                error = $"TourniquetBoardSteps '{name}': runner is not the link's runner.";
            if (error == null && (guide.Runner != runner || guide.Link != link))
                error = $"TourniquetBoardSteps '{name}': the guide on this object must use the same runner and link.";
            if (error == null && !stepRow.TryValidate(out error))
                return false;
            if (error == null && stepRow.Count != TourniquetTrainingIds.Steps.Length)
                error = $"TourniquetBoardSteps '{name}': the step row needs {TourniquetTrainingIds.Steps.Length} boxes, not {stepRow.Count}.";
            if (error == null && (operationPanel.transform.IsChildOf(stepRow.transform) || finalCheckPanel.transform.IsChildOf(stepRow.transform) ||
                                  stepRow.transform.IsChildOf(operationPanel.transform) || stepRow.transform.IsChildOf(finalCheckPanel.transform)))
                error = $"TourniquetBoardSteps '{name}': the step row must be apart from the operation and final check panels.";
            return error == null;
        }
    }
}
