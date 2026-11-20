using System;
using System.Collections.Generic;
using MilTrainSimVR.B.Training;
using UnityEngine;

namespace MilTrainSimVR.B.BleedingControl.Tourniquet.Tutorial
{
    public enum TourniquetConfirmResult
    {
        // Not the final check step of this run, or an input is already pending: nothing changes.
        Ignored = 0,
        Accepted = 1,
        // Refused: a device state is not valid now. Nothing is kept.
        Unmet = 2
    }

    // In-memory summary of one completed run (virtual handling performed, not a medical result). Kept until a new run starts.
    public sealed class TourniquetTrainingSummary
    {
        readonly bool[] states;

        public int RunId { get; }
        public bool FinalCheckConfirmed { get; }
        public bool TimeRecorded { get; }
        // TourniquetOperation's recorded value (seconds from attaching), fixed when the time was recorded.
        public float RecordedElapsedSeconds { get; }

        public TourniquetTrainingSummary(int runId, bool[] states, bool finalCheckConfirmed, bool timeRecorded, float recordedElapsedSeconds)
        {
            RunId = runId;
            this.states = (bool[])states.Clone();
            FinalCheckConfirmed = finalCheckConfirmed;
            TimeRecorded = timeRecorded;
            RecordedElapsedSeconds = recordedElapsedSeconds;
        }

        // Device state i of TourniquetTrainingIds.Steps when the run completed.
        public bool StateAt(int index) => states[index];
    }

    // Connects the tourniquet device to the common training runner. Reports facts only: it never advances training, moves the
    // device or completes a device state. Each of the six device states (TourniquetTrainingIds.Steps) is read from the device's
    // current values and written as a TrainingTarget state, every frame after TourniquetOperation has applied that frame's input
    // and releases (execution order 106 > 105) and on the device events. The runner reads them one step per frame, so a fast
    // sequence is caught up step by step and nothing is lost. Values are current, not latched: a state that stops being valid
    // (for example the windlass turned back below the required amount before it is seated) turns false again, and the training
    // data's cumulative conditions then hold every later step, including the last, until the device is valid again. Whether a
    // state was reached earlier in this run is kept apart (WasReached) for the recovery guidance only; it never completes a step.
    // A new run (new RunId) clears both. Starts the runner on request from the start gate and reports whether it really runs.
    //
    // Final check: after the six practice steps the runner waits on the final check step, whose conditions are the six current
    // device states plus the trainee state FinalConfirmed (a State, not an Action: an accepted Action stays accepted for the whole
    // step and cannot be withdrawn, so an input taken while the device was not valid would complete the step later on its own).
    // TryConfirm takes the input only in the final check step of the run this link started, re-reads the device right then and
    // sets FinalConfirmed only when all six states are valid; a refused input is not kept. The accepted input belongs to that
    // run and step entry (token). It is withdrawn again (and a new input is required) when the token changes, when a device state
    // is seen invalid before the runner completes, or when the runner has judged it without completing. When the runner reports
    // Completed for this run, a summary (device states, the input and the recorded time) is kept in memory for the completion
    // panel; nothing is saved.
    [DefaultExecutionOrder(106)]
    [DisallowMultipleComponent]
    public sealed class TourniquetTrainingLink : MonoBehaviour
    {
        [SerializeField] TrainingRunner runner;
        [SerializeField] TourniquetDevice device;
        [SerializeField] TourniquetOperation operation;
        [Tooltip("TrainingTarget '" + TourniquetTrainingIds.DeviceTarget + "' with the six device states.")]
        [SerializeField] TrainingTarget deviceTarget;
        [Tooltip("TrainingTarget '" + TourniquetTrainingIds.TraineeTarget + "' with the final check input state.")]
        [SerializeField] TrainingTarget traineeTarget;

        readonly bool[] current = new bool[TourniquetTrainingIds.Steps.Length];
        readonly bool[] reached = new bool[TourniquetTrainingIds.Steps.Length];
        bool valid;
        bool subscribed;
        int observedRunId;
        int startedRunId;

        bool confirmPending;
        TrainingStepToken confirmToken;
        int confirmFrame;
        bool confirmCancelled;
        TrainingStepToken cancelledToken;

        TourniquetTrainingSummary summary;

        public TrainingRunner Runner => runner;
        public TourniquetDevice Device => device;
        public TourniquetOperation Operation => operation;
        public TrainingTarget DeviceTarget => deviceTarget;
        public TrainingTarget TraineeTarget => traineeTarget;
        // The runner's current run is the one this link started.
        public bool IsCurrentRun => valid && startedRunId != 0 && runner.CurrentToken.RunId == startedRunId;
        public bool HasStartedRun => startedRunId != 0;
        // Equipment input stays allowed after completion so the last handling and its release are not cut off; a stopped run
        // (not completed) locks new selections again.
        public bool RunActive => IsCurrentRun && (runner.Status == TrainingStatus.Running || runner.Status == TrainingStatus.Completed);
        // The runner waits on the final check step of the run this link started.
        public bool IsFinalCheckStep => IsCurrentRun && runner.Status == TrainingStatus.Running &&
                                        runner.CurrentStepId == TourniquetTrainingIds.FinalCheckStep;
        // An accepted final check input of this step entry that the runner has not judged yet.
        public bool ConfirmPending => confirmPending && IsFinalCheckStep && SameToken(confirmToken, runner.CurrentToken);
        // An accepted input of this step entry was withdrawn before completion; a new input is required.
        public bool ConfirmCancelled => confirmCancelled && IsFinalCheckStep && SameToken(cancelledToken, runner.CurrentToken);
        // Summary kept when the runner completed the run this link started; null before.
        public TourniquetTrainingSummary Summary => summary != null && IsCurrentRun && runner.Status == TrainingStatus.Completed ? summary : null;

        // Device state i of TourniquetTrainingIds.Steps as last observed in this run, and whether it was valid at any time in it.
        public bool IsValidNow(int index) => current[index];
        public bool WasReached(int index) => reached[index];

        public bool AllValidNow()
        {
            foreach (var value in current)
            {
                if (!value)
                    return false;
            }
            return true;
        }

        void Awake()
        {
            valid = TryValidate(out var error);
            if (!valid)
                Debug.LogError($"[TourniquetTraining] {error} Training cannot start.", this);
        }

        void OnEnable()
        {
            if (subscribed || !valid)
                return;
            device.StateChanged += OnDeviceChanged;
            device.Attached += OnDeviceChanged;
            operation.Changed += OnOperationChanged;
            operation.BandPrepared += OnOperationChanged;
            operation.WindlassSecured += OnOperationChanged;
            operation.TimeRecorded += OnOperationChanged;
            operation.StrapFastened += OnOperationChanged;
            subscribed = true;
        }

        void OnDisable()
        {
            if (!subscribed)
                return;
            device.StateChanged -= OnDeviceChanged;
            device.Attached -= OnDeviceChanged;
            operation.Changed -= OnOperationChanged;
            operation.BandPrepared -= OnOperationChanged;
            operation.WindlassSecured -= OnOperationChanged;
            operation.TimeRecorded -= OnOperationChanged;
            operation.StrapFastened -= OnOperationChanged;
            subscribed = false;
        }

        // After the runner's LateUpdate (order 0): device states for the next judgement, then the accepted input the runner has
        // judged without completing (it had its tick in the frame of the input or the next one) is withdrawn, then the summary.
        void LateUpdate()
        {
            Observe();
            if (confirmPending && runner.Status == TrainingStatus.Running && Time.frameCount > confirmFrame)
                CancelConfirm("the runner did not complete on it");
            CaptureSummary();
        }

        void OnDeviceChanged(TourniquetDevice _) => Observe();
        void OnOperationChanged(TourniquetOperation _) => Observe();

        // Starts a new run while the equipment is still locked: clears the facts, starts the runner and checks that it really runs
        // this course from its first step. On failure the runner is left not running and the facts cleared, so the caller keeps
        // the equipment locked.
        public bool TryBeginRun(out string error)
        {
            if (!valid)
            {
                error = "TourniquetTrainingLink references are invalid (see its earlier error).";
                return false;
            }
            if (runner.Status == TrainingStatus.Running)
            {
                error = "the training runner is already running a run this course did not start.";
                return false;
            }

            ClearFacts();
            runner.StartTraining();
            var firstStep = TourniquetTrainingIds.Steps[0].step;
            if (runner.Status != TrainingStatus.Running || runner.CurrentStepId != firstStep)
            {
                error = $"the training runner did not start at '{firstStep}' (status {runner.Status}, step '{runner.CurrentStepId}'; see the [Training] error).";
                if (runner.Status == TrainingStatus.Running)
                    runner.StopTraining();
                ClearFacts();
                return false;
            }

            startedRunId = observedRunId = runner.CurrentToken.RunId;
            Observe();
            error = null;
            return true;
        }

        // Retry entry point (TourniquetTrainingReset), before the device reset: the runner stops (no step runs; the next
        // StartTraining makes a new run id), and this link forgets the run it started, the device and trainee states, the pending,
        // cancelled or refused-input record and the summary. Device events during the device reset are then ignored (Observe only
        // writes while a run runs), so nothing of the reset or the old run reaches the next run.
        public void EndRunForReset()
        {
            if (runner != null && (runner.Status == TrainingStatus.Running || runner.Status == TrainingStatus.Completed))
                runner.StopTraining();
            startedRunId = 0;
            if (valid)
                ClearFacts();
        }

        // Final check input (the trainee's button). Taken only in the final check step of this run with no input pending; the
        // device is read again now (its own results, as in Observe) and the input is accepted only when all six states are valid.
        // A refused input is dropped, so it can never complete the step later; the trainee must give a new one.
        public TourniquetConfirmResult TryConfirm()
        {
            if (!valid || !IsFinalCheckStep || ConfirmPending)
                return TourniquetConfirmResult.Ignored;

            Observe();
            if (!AllValidNow())
                return TourniquetConfirmResult.Unmet;

            confirmPending = true;
            confirmToken = runner.CurrentToken;
            confirmFrame = Time.frameCount;
            confirmCancelled = false;
            traineeTarget.SetStateTrue(TourniquetTrainingIds.FinalConfirmed);
            return TourniquetConfirmResult.Accepted;
        }

        void CancelConfirm(string reason)
        {
            confirmPending = false;
            confirmCancelled = true;
            cancelledToken = confirmToken;
            traineeTarget.SetStateFalse(TourniquetTrainingIds.FinalConfirmed);
            Debug.LogWarning($"[TourniquetTraining] Final check input withdrawn (run {startedRunId}): {reason}. A new input is required.", this);
        }

        void ClearConfirm()
        {
            confirmPending = false;
            confirmCancelled = false;
            traineeTarget.SetStateFalse(TourniquetTrainingIds.FinalConfirmed);
        }

        // Once per completed run, in the frame the runner completed: the device states read again now, whether the completing
        // input was this run's, and the time recorded by TourniquetOperation (attach-time clock, fixed once recorded).
        void CaptureSummary()
        {
            if (summary != null || !valid || !IsCurrentRun || runner.Status != TrainingStatus.Completed)
                return;

            var steps = TourniquetTrainingIds.Steps;
            var states = new bool[steps.Length];
            for (var i = 0; i < steps.Length; i++)
                states[i] = operation.IsReady && Read(steps[i].signal);
            var confirmed = confirmPending && confirmToken.RunId == startedRunId;
            summary = new TourniquetTrainingSummary(startedRunId, states, confirmed, operation.IsTimeRecorded, operation.RecordedElapsedSeconds);
        }

        string Missing()
        {
            var missing = new List<string>();
            for (var i = 0; i < current.Length; i++)
            {
                if (!current[i])
                    missing.Add(TourniquetTrainingIds.Steps[i].signal);
            }
            return string.Join(", ", missing);
        }

        static bool SameToken(TrainingStepToken a, TrainingStepToken b) => a.RunId == b.RunId && a.StepEntry == b.StepEntry;

        // Only writes states while a run is running; the runner evaluates them in its own LateUpdate, never inside a device event.
        void Observe()
        {
            if (!valid || runner.Status != TrainingStatus.Running)
                return;

            var runId = runner.CurrentToken.RunId;
            if (runId != observedRunId)
            {
                observedRunId = runId;
                ClearFacts();
            }

            var steps = TourniquetTrainingIds.Steps;
            for (var i = 0; i < steps.Length; i++)
            {
                var value = operation.IsReady && Read(steps[i].signal);
                current[i] = value;
                reached[i] |= value;
                if (value)
                    deviceTarget.SetStateTrue(steps[i].signal);
                else
                    deviceTarget.SetStateFalse(steps[i].signal);
            }

            // An accepted input never outlives its step entry or a device state seen invalid before completion.
            if (confirmPending && !SameToken(confirmToken, runner.CurrentToken))
                CancelConfirm("the step entry changed");
            else if (confirmPending && !AllValidNow())
                CancelConfirm($"device states became invalid before completion ({Missing()})");
        }

        // The device's own results; no pull distance, turn amount or seat check is computed here.
        bool Read(string signal) => signal switch
        {
            TourniquetTrainingIds.Attached => device.IsAttached && device.AttachedTarget == device.Target,
            TourniquetTrainingIds.BandPrepared => operation.IsBandPrepared,
            TourniquetTrainingIds.TurnReached => operation.WindlassTurnDegrees >= operation.RequiredTurnDegrees,
            TourniquetTrainingIds.WindlassSecured => operation.IsWindlassSecured,
            TourniquetTrainingIds.TimeRecorded => operation.IsTimeRecorded,
            TourniquetTrainingIds.StrapFastened => operation.IsStrapFastened,
            _ => false
        };

        void ClearFacts()
        {
            Array.Clear(current, 0, current.Length);
            Array.Clear(reached, 0, reached.Length);
            foreach (var (_, signal) in TourniquetTrainingIds.Steps)
                deviceTarget.SetStateFalse(signal);
            ClearConfirm();
            summary = null;
        }

        public bool TryValidate(out string error)
        {
            var problems = new List<string>();
            if (runner == null)
                problems.Add(nameof(runner) + " missing");
            if (device == null || operation == null)
                problems.Add("device or operation missing");
            else if (operation.Device != device)
                problems.Add("operation is not on this device");
            if (deviceTarget == null || deviceTarget.TargetId != TourniquetTrainingIds.DeviceTarget)
                problems.Add($"{nameof(deviceTarget)} must be '{TourniquetTrainingIds.DeviceTarget}'");
            else
            {
                foreach (var (_, signal) in TourniquetTrainingIds.Steps)
                {
                    if (!deviceTarget.HasState(signal))
                        problems.Add($"{nameof(deviceTarget)} has no state '{signal}'");
                }
            }
            if (traineeTarget == null || traineeTarget == deviceTarget || traineeTarget.TargetId != TourniquetTrainingIds.TraineeTarget ||
                !traineeTarget.HasState(TourniquetTrainingIds.FinalConfirmed))
                problems.Add($"{nameof(traineeTarget)} must be '{TourniquetTrainingIds.TraineeTarget}' with state '{TourniquetTrainingIds.FinalConfirmed}'");

            error = problems.Count > 0 ? $"TourniquetTrainingLink '{name}': {string.Join(", ", problems)}." : null;
            return error == null;
        }
    }
}
