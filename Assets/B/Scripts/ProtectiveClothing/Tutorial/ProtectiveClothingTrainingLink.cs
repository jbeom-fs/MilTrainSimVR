using System;
using System.Collections.Generic;
using MilTrainSimVR.B.Training;
using UnityEngine;

namespace MilTrainSimVR.B.ProtectiveClothing.Tutorial
{
    // In-memory summary of one completed run: the virtual wear state of each part when the runner completed it (no fit, closure
    // or protection result). Kept until the retry or a new run; never saved.
    public sealed class ProtectiveClothingTrainingSummary
    {
        readonly bool[] worn;

        public int RunId { get; }

        public ProtectiveClothingTrainingSummary(int runId, bool[] worn)
        {
            RunId = runId;
            this.worn = (bool[])worn.Clone();
        }

        public bool Worn(ProtectiveClothingPart part) => worn[(int)part];
    }

    // Connects ProtectiveClothingEquipment (the only owner of the wear state, grabs and reset) to the common training runner.
    // Reports facts only: it never advances training, wears, hides or resets a part.
    //
    // Facts: one TrainingTarget ('pc-equipment') with one state per part, the equipment's current IsWorn, written every frame in
    // this LateUpdate (order 55: after the equipment's wear judgement at 50) while the run this course started is running.
    // The runner (default order 0) reads them in its next LateUpdate, so a step advances one frame after the wear; the course's
    // allowance (CurrentPart) follows the runner's step, so the next part opens only after that advance and a frame never skips
    // a step. Every condition is a State (ProtectiveClothingTrainingIds.Steps: cumulative, the last step needs all six), so
    // nothing hinges on an action in a step's first frame.
    //
    // Allowance: CurrentPart is the first part of the course order that is not worn now, but never beyond the runner's current
    // step (no part ahead of the course; an earlier part, if ever not worn, comes first). Nothing before the start, after a
    // stop or after completion.
    //
    // Completion: when the runner reports Completed for this run, the summary is kept once, only if all six parts are worn at
    // that moment (the same frame's equipment state); otherwise no summary is made and the run is reported as stopped.
    [DefaultExecutionOrder(55)]
    [DisallowMultipleComponent]
    public sealed class ProtectiveClothingTrainingLink : MonoBehaviour
    {
        [SerializeField] TrainingRunner runner;
        [SerializeField] ProtectiveClothingEquipment equipment;
        [Tooltip("TrainingTarget '" + ProtectiveClothingTrainingIds.EquipmentTarget + "' with one worn state per part.")]
        [SerializeField] TrainingTarget equipmentTarget;

        bool valid;
        int startedRunId;
        ProtectiveClothingTrainingSummary summary;
        bool summaryRefused;

        public TrainingRunner Runner => runner;
        public ProtectiveClothingEquipment Equipment => equipment;
        public TrainingTarget EquipmentTarget => equipmentTarget;
        public bool IsCurrentRun => valid && startedRunId != 0 && runner.CurrentToken.RunId == startedRunId;
        public bool IsRunning => IsCurrentRun && runner.Status == TrainingStatus.Running;
        public ProtectiveClothingTrainingSummary Summary =>
            summary != null && IsCurrentRun && runner.Status == TrainingStatus.Completed && summary.RunId == startedRunId ? summary : null;
        // The run this course started ended without a summary of it (stopped from outside, or completed without all six worn).
        public bool RunEndedWithoutSummary => IsCurrentRun && !IsRunning && Summary == null;

        // 0-based step of the runner while this run runs, -1 otherwise.
        public int CurrentStep => IsRunning ? ProtectiveClothingTrainingIds.StepIndex(runner.CurrentStepId) : -1;

        public ProtectiveClothingPart? CurrentPart
        {
            get
            {
                var step = CurrentStep;
                if (step < 0)
                    return null;
                var order = ProtectiveClothingTrainingIds.Order;
                for (var k = 0; k <= step && k < order.Length; k++)
                {
                    if (!equipment.IsWorn(order[k]))
                        return order[k];
                }
                return null;
            }
        }

        // The course allows a new grab and wearing of this part now (the equipment's own rules still apply on top).
        public bool AllowsPart(ProtectiveClothingPart part) => CurrentPart == part;

        void Awake()
        {
            valid = TryValidate(out var error);
            if (!valid)
                Debug.LogError($"[ProtectiveClothingTraining] {error} Training cannot start.", this);
        }

        // After the runner (0), the user body (40) and the equipment (50): this frame's wear state for the runner's next
        // judgement, then the summary of a run the runner reported Completed this frame.
        void LateUpdate()
        {
            Observe();
            CaptureSummary();
        }

        void Observe()
        {
            if (!valid || !IsRunning)
                return;
            foreach (var part in ProtectiveClothingTrainingIds.AllParts)
            {
                var signal = ProtectiveClothingTrainingIds.SignalOf(part);
                if (equipment.IsWorn(part))
                    equipmentTarget.SetStateTrue(signal);
                else
                    equipmentTarget.SetStateFalse(signal);
            }
        }

        void CaptureSummary()
        {
            if (summary != null || summaryRefused || !IsCurrentRun || runner.Status != TrainingStatus.Completed)
                return;
            if (!equipment.AllWorn)
            {
                summaryRefused = true;
                Debug.LogError($"[ProtectiveClothingTraining] Run {startedRunId} completed but not all six parts are worn now " +
                               $"({equipment.WornCount}/{ProtectiveClothingTrainingIds.AllParts.Length}); no summary. Equipment stays locked.", this);
                return;
            }
            var worn = new bool[ProtectiveClothingTrainingIds.AllParts.Length];
            foreach (var part in ProtectiveClothingTrainingIds.AllParts)
                worn[(int)part] = equipment.IsWorn(part);
            summary = new ProtectiveClothingTrainingSummary(startedRunId, worn);
        }

        // Starts a new run (equipment still locked by the start gate): clears the facts, starts the runner and checks that it
        // really runs this course from its first step. On failure no run is left running and the facts stay clear.
        public bool TryBeginRun(out string error)
        {
            if (!valid)
            {
                error = "ProtectiveClothingTrainingLink references are invalid (see its earlier error).";
                return false;
            }
            if (runner.Status == TrainingStatus.Running)
            {
                error = "the training runner is already running a run this course did not start.";
                return false;
            }

            ClearFacts();
            runner.StartTraining();
            var first = ProtectiveClothingTrainingIds.Steps[0].step;
            if (runner.Status != TrainingStatus.Running || runner.CurrentStepId != first)
            {
                error = $"the training runner did not start at '{first}' (status {runner.Status}, step '{runner.CurrentStepId}'; see the [Training] error).";
                if (runner.Status == TrainingStatus.Running)
                    runner.StopTraining();
                ClearFacts();
                return false;
            }

            startedRunId = runner.CurrentToken.RunId;
            Observe();
            error = null;
            return true;
        }

        // Retry step (ProtectiveClothingTrainingReset), before the equipment reset: the runner stops and the link forgets the
        // run, its states and summary, so nothing of the reset or the old run reaches the next run.
        public void EndRunForReset()
        {
            if (runner != null && (runner.Status == TrainingStatus.Running || runner.Status == TrainingStatus.Completed))
                runner.StopTraining();
            startedRunId = 0;
            if (valid)
                ClearFacts();
        }

        void ClearFacts()
        {
            foreach (var signal in ProtectiveClothingTrainingIds.Signals)
                equipmentTarget.SetStateFalse(signal);
            summary = null;
            summaryRefused = false;
        }

        public bool TryValidate(out string error)
        {
            var problems = new List<string>();
            if (!ProtectiveClothingTrainingIds.TryValidateOrder(out var orderError))
                problems.Add(orderError);
            if (runner == null)
                problems.Add(nameof(runner) + " missing");
            if (equipment == null)
                problems.Add(nameof(equipment) + " missing");
            if (equipmentTarget == null || equipmentTarget.TargetId != ProtectiveClothingTrainingIds.EquipmentTarget)
                problems.Add($"{nameof(equipmentTarget)} must be '{ProtectiveClothingTrainingIds.EquipmentTarget}'");
            else
            {
                foreach (var signal in ProtectiveClothingTrainingIds.Signals)
                {
                    if (!equipmentTarget.HasState(signal))
                        problems.Add($"{nameof(equipmentTarget)} has no state '{signal}'");
                }
            }
            error = problems.Count > 0 ? $"ProtectiveClothingTrainingLink '{name}': {string.Join(", ", problems)}." : null;
            return error == null;
        }
    }
}
