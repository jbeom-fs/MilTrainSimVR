using System;
using System.Collections.Generic;
using MilTrainSimVR.B.Training;
using UnityEngine;

namespace MilTrainSimVR.B.BleedingControl.PressureBandage.Tutorial
{
    public enum PressureCheckKind
    {
        None = 0,
        // Situation and tools (one item).
        Situation = 1,
        // Before wrapping: hand-side colour, warmth, sensation (virtual casualty information).
        PreWrap = 2,
        // After fixing: re-bleeding, then hand-side colour, warmth, sensation (virtual casualty information).
        PostWrap = 3
    }

    public enum PressureInputResult
    {
        // Not this check / step of this run, or an input is pending: nothing changes.
        Ignored = 0,
        // Refused: a condition is not met right now. Nothing is kept.
        Refused = 2,
        // Check input taken at input time: recorded or dropped in this frame's LateUpdate. Not an acceptance.
        Pending = 3
    }

    // In-memory summary of one completed run (virtual handling and check inputs performed; no medical result). Kept until the
    // retry or a new run.
    public sealed class PressureBandageTrainingSummary
    {
        readonly bool[] requirements;

        public int RunId { get; }

        public PressureBandageTrainingSummary(int runId, bool[] requirements)
        {
            RunId = runId;
            this.requirements = (bool[])requirements.Clone();
        }

        public bool Met(PressureRequirement requirement) => requirements[(int)requirement];
    }

    // Connects the pressure bandage equipment to the common training runner and keeps this run's records. Reports facts only:
    // it never advances training, moves equipment or completes an equipment state.
    //
    // Facts (TrainingTarget states, written every frame after the equipment's LateUpdate, order 104 > 102, while a run runs; the
    // runner reads them in its own LateUpdate, so all conditions are States and nothing hinges on an action in a step's first frame):
    // - Current equipment states: dressing placed (PressureDressing.IsPlacementValid), wrap complete (application stage past
    //   wrapping), application fixed (stage Fixed). They turn false again when the equipment does.
    // - Records of this run: situation confirmed (button); contact practiced (PressureDressing's practice record seen true after
    //   the situation input; the start requires it false and equipment is locked before, so it is this run's practice; the contact
    //   time stays counted by PressureDressing only); pre-wrap check and post-wrap check (one input per item, each taken only
    //   while its conditions hold both at the input and on this frame's final equipment state; see TryConfirmCheck).
    // - The two checks belong to the current application attempt (PressureBandageApplication CancelCount and ResetCount, not the
    //   display Version): a cancelled application withdraws both (redo the check before the new wrap); the post-wrap check also
    //   needs the application to be Fixed now. Normal wrapping progress keeps them.
    // Completion: the last step (post-wrap check) needs every requirement at once (PressureBandageTrainingIds.Steps), so the runner
    // completes on its own once the last post-wrap item is recorded (after its input-time and frame-end checks) and all the
    // other states still hold; there is no separate final input. A missing requirement keeps the run going (the course guide
    // shows the recovery). When the runner reports Completed for this run, a summary of the requirements is kept once in
    // memory; nothing is saved.
    [DefaultExecutionOrder(104)]
    [DisallowMultipleComponent]
    public sealed class PressureBandageTrainingLink : MonoBehaviour
    {
        [SerializeField] TrainingRunner runner;
        [SerializeField] PressureDressing dressing;
        [SerializeField] PressureBandageApplication application;
        [Tooltip("TrainingTarget '" + PressureBandageTrainingIds.EquipmentTarget + "' with the current equipment states.")]
        [SerializeField] TrainingTarget equipmentTarget;
        [Tooltip("TrainingTarget '" + PressureBandageTrainingIds.TraineeTarget + "' with the records and inputs of this run.")]
        [SerializeField] TrainingTarget traineeTarget;

        readonly bool[] current = new bool[PressureBandageTrainingIds.AllRequirements.Length];
        readonly bool[] preItems = new bool[PressureBandageTrainingIds.PreCheckItems];
        readonly bool[] postItems = new bool[PressureBandageTrainingIds.PostCheckItems];
        bool valid;
        int observedRunId;
        int startedRunId;
        bool situationConfirmed;
        bool contactPracticed;
        (int cancel, int reset) preKey;
        (int cancel, int reset) postKey;

        // A check input waiting for this frame's LateUpdate (see TryConfirmCheck); never kept past its frame.
        bool checkPending;
        PressureCheckKind checkKind;
        TrainingStepToken checkToken;
        (int cancel, int reset) checkKey;
        int checkItem;
        int checkFrame;

        PressureBandageTrainingSummary summary;

        public TrainingRunner Runner => runner;
        public PressureDressing Dressing => dressing;
        public PressureBandageApplication BandageApplication => application;
        public TrainingTarget EquipmentTarget => equipmentTarget;
        public TrainingTarget TraineeTarget => traineeTarget;
        public bool IsCurrentRun => valid && startedRunId != 0 && runner.CurrentToken.RunId == startedRunId;
        public bool IsRunning => IsCurrentRun && runner.Status == TrainingStatus.Running;
        public PressureBandageTrainingSummary Summary => summary != null && IsCurrentRun && runner.Status == TrainingStatus.Completed ? summary : null;
        // The pre-wrap check was given in this run and withdrawn by a cancelled application (it must be given again).
        public bool PreCheckWithdrawn { get; private set; }

        // Equipment selection allowed by the course (the equipment's own filters still apply on top): the dressing after the
        // situation input; roll, end and tape after the contact practice and the pre-wrap check of the current attempt, so the
        // bandage cannot be applied before the practice it must follow. Nothing before the start, after a stop or after completion.
        public bool AllowsDressing => IsRunning && situationConfirmed;
        public bool AllowsBandageParts => AllowsDressing && contactPracticed && IsMet(PressureRequirement.PreCheck);

        void Awake()
        {
            valid = TryValidate(out var error);
            if (!valid)
                Debug.LogError($"[PressureBandageTraining] {error} Training cannot start.", this);
        }

        // After the runner (0), the dressing (100) and the application (102): facts for the next judgement, then a check input of
        // this frame is recorded or dropped, then the summary of a run the runner reported Completed.
        void LateUpdate()
        {
            Observe();
            ResolveCheck();
            CaptureSummary();
        }

        void OnDisable() => DropCheck();

        // ---------- Current requirements ----------

        public bool IsMet(PressureRequirement requirement) => current[(int)requirement];

        public bool AllMet()
        {
            foreach (var value in current)
            {
                if (!value)
                    return false;
            }
            return true;
        }

        // First requirement in course order that does not hold now; null when all hold.
        public PressureRequirement? FirstMissing()
        {
            foreach (var requirement in PressureBandageTrainingIds.AllRequirements)
            {
                if (!IsMet(requirement))
                    return requirement;
            }
            return null;
        }

        (int, int) AttemptKey => (application.CancelCount, application.ResetCount);

        bool Compute(PressureRequirement requirement) => requirement switch
        {
            PressureRequirement.Situation => situationConfirmed,
            PressureRequirement.Dressing => dressing.IsPlacementValid,
            PressureRequirement.Contact => contactPracticed,
            PressureRequirement.PreCheck => AllTrue(preItems) && preKey == AttemptKey,
            PressureRequirement.Wrap => application.Stage >= PressureBandageStage.EndPlacement,
            PressureRequirement.Fix => application.Stage == PressureBandageStage.Fixed,
            _ => AllTrue(postItems) && postKey == AttemptKey && application.Stage == PressureBandageStage.Fixed
        };

        // ---------- Checks (situation, pre-wrap, post-wrap) ----------

        // The check the trainee should do now, if any (shown in the practice screen by the course guide).
        public PressureCheckKind ActiveCheck
        {
            get
            {
                // Also in a later step: if a requirement broke there, its check must stay reachable for recovery.
                if (!IsRunning)
                    return PressureCheckKind.None;
                if (!situationConfirmed)
                    return PressureCheckKind.Situation;
                if (contactPracticed && !IsMet(PressureRequirement.PreCheck) && application.Stage == PressureBandageStage.Ready)
                    return PressureCheckKind.PreWrap;
                if (IsMet(PressureRequirement.Fix) && !IsMet(PressureRequirement.PostCheck))
                    return PressureCheckKind.PostWrap;
                return PressureCheckKind.None;
            }
        }

        public static int ItemCount(PressureCheckKind kind) => kind switch
        {
            PressureCheckKind.Situation => PressureBandageTrainingIds.SituationItems,
            PressureCheckKind.PreWrap => PressureBandageTrainingIds.PreCheckItems,
            PressureCheckKind.PostWrap => PressureBandageTrainingIds.PostCheckItems,
            _ => 0
        };

        public bool ItemDone(PressureCheckKind kind, int index) => kind switch
        {
            PressureCheckKind.Situation => situationConfirmed,
            PressureCheckKind.PreWrap => preItems[index] && preKey == AttemptKey,
            PressureCheckKind.PostWrap => postItems[index] && postKey == AttemptKey,
            _ => false
        };

        public int ItemsDone(PressureCheckKind kind)
        {
            var done = 0;
            for (var i = 0; i < ItemCount(kind); i++)
            {
                if (ItemDone(kind, i))
                    done++;
            }
            return done;
        }

        // Whether an input for this check would be taken right now (display aid for the button; TryConfirmCheck checks again);
        // refusal is a TrainingSubtitles line (null when taken). False while an input of this frame waits.
        public bool CanConfirmCheck(PressureCheckKind kind, out string refusal)
        {
            refusal = null;
            if (kind == PressureCheckKind.None || ActiveCheck != kind || CheckWaiting)
                return false;
            return ConditionsHoldNow(kind, out refusal);
        }

        bool CheckWaiting => checkPending && checkFrame == Time.frameCount;

        // The check's own conditions on current data (the dressing's input-time reads, not its last LateUpdate states).
        // Pre-wrap: dressing placed and held by one hand (IsHoldValidNow includes the placement), before wrapping;
        // post-wrap: application fixed.
        bool ConditionsHoldNow(PressureCheckKind kind, out string refusal)
        {
            refusal = !dressing.IsInputFocusedNow ? TrainingSubtitles.CheckRefusedFocus
                : kind == PressureCheckKind.PreWrap && !(dressing.IsHoldValidNow && application.Stage == PressureBandageStage.Ready) ? TrainingSubtitles.CheckRefusedHold
                : kind == PressureCheckKind.PostWrap && application.Stage != PressureBandageStage.Fixed ? TrainingSubtitles.CheckRefusedFixed
                : null;
            return refusal == null;
        }

        // One check input (the check button), judged twice so that it is never recorded on an earlier frame's state:
        // - At the input (Update): ignored unless this check is active and no input of this frame waits; refused (nothing kept)
        //   unless its conditions hold now. Otherwise it waits, bound to the run's step entry, the application attempt, the
        //   check and its next item.
        // - In this frame's LateUpdate, after the dressing (100) and the application (102) updated, so after XRI's selection,
        //   release and pose processing of this frame (the UI input module runs before them, at -200): recorded only if all of
        //   that is unchanged and the conditions still hold, else dropped.
        // A dropped input is never applied later and a new input is required; nothing is recorded before that second judgement.
        public PressureInputResult TryConfirmCheck(PressureCheckKind kind)
        {
            if (checkPending && checkFrame != Time.frameCount)
                DropCheck();
            if (!valid || kind == PressureCheckKind.None || ActiveCheck != kind || checkPending)
                return PressureInputResult.Ignored;
            if (!ConditionsHoldNow(kind, out _))
                return PressureInputResult.Refused;

            checkPending = true;
            checkKind = kind;
            checkToken = runner.CurrentToken;
            checkKey = AttemptKey;
            checkItem = ItemsDone(kind);
            checkFrame = Time.frameCount;
            return PressureInputResult.Pending;
        }

        void ResolveCheck()
        {
            if (!checkPending)
                return;
            var kind = checkKind;
            var reason = checkFrame != Time.frameCount ? "not judged in its frame"
                : !IsRunning || !SameToken(checkToken, runner.CurrentToken) ? "the run or step entry changed"
                : checkKey != AttemptKey ? "the bandage application attempt changed"
                : ActiveCheck != kind || ItemsDone(kind) != checkItem ? "the active check or its item changed"
                : !ConditionsHoldNow(kind, out _) ? "conditions not met on this frame's final equipment state"
                : null;
            if (reason != null)
            {
                DropCheck();
                return;
            }
            checkPending = false;
            Record(kind);
        }

        void DropCheck()
        {
            if (!checkPending)
                return;
            checkPending = false;
        }

        // Records the next item of the check (one per input).
        void Record(PressureCheckKind kind)
        {
            switch (kind)
            {
                case PressureCheckKind.Situation:
                    situationConfirmed = true;
                    break;
                case PressureCheckKind.PreWrap:
                    MarkNext(preItems, ref preKey);
                    PreCheckWithdrawn = false;
                    break;
                default:
                    MarkNext(postItems, ref postKey);
                    break;
            }
            Observe();
        }

        void MarkNext(bool[] items, ref (int cancel, int reset) key)
        {
            if (key != AttemptKey)
            {
                Array.Clear(items, 0, items.Length);
                key = AttemptKey;
            }
            var next = Array.IndexOf(items, false);
            if (next >= 0)
                items[next] = true;
        }

        // ---------- Run ----------

        // Starts a new run (equipment still locked): clears the records, starts the runner and checks that it really runs this
        // course from its first step. On failure no run is left running and the records stay clear.
        public bool TryBeginRun(out string error)
        {
            if (!valid)
            {
                error = "PressureBandageTrainingLink references are invalid (see its earlier error).";
                return false;
            }
            if (runner.Status == TrainingStatus.Running)
            {
                error = "the training runner is already running a run this course did not start.";
                return false;
            }

            ClearFacts();
            runner.StartTraining();
            var first = PressureBandageTrainingIds.Steps[0].step;
            if (runner.Status != TrainingStatus.Running || runner.CurrentStepId != first)
            {
                error = $"the training runner did not start at '{first}' (status {runner.Status}, step '{runner.CurrentStepId}'; see the [Training] error).";
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

        // Retry step (PressureBandageTrainingReset), before the equipment reset: the runner stops and the link forgets the run,
        // its records, inputs and summary, so nothing of the reset or the old run reaches the next run.
        public void EndRunForReset()
        {
            if (runner != null && (runner.Status == TrainingStatus.Running || runner.Status == TrainingStatus.Completed))
                runner.StopTraining();
            startedRunId = 0;
            if (valid)
                ClearFacts();
        }

        // Equipment in its initial state for a new run: nothing selected, application Ready, the dressing on the tray (not on the
        // wound) and without a practice record, and the last equipment reset (if any) succeeded.
        public bool TryCheckEquipmentReady(out string error)
        {
            error = !valid ? "PressureBandageTrainingLink references are invalid."
                : !dressing.isActiveAndEnabled || !application.isActiveAndEnabled ? "PressureDressing or PressureBandageApplication is not running (see its error)."
                : !application.LastResetSucceeded ? "the last equipment reset failed."
                : application.Stage != PressureBandageStage.Ready ? $"the bandage application is not Ready ({application.Stage})."
                : dressing.IsHeld || application.IsRollHeld || application.IsEndHeld || application.IsTapeHeld ? "a kit part is selected."
                : dressing.IsPlacementValid ? "the dressing already lies on the wound (not its tray position)."
                : dressing.HasCompletedContactPractice ? "the dressing already has a contact practice record."
                : null;
            return error == null;
        }

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

            // The checks belong to the current application attempt: a cancelled application withdraws them.
            if (AnyTrue(preItems) && preKey != AttemptKey)
            {
                Array.Clear(preItems, 0, preItems.Length);
                PreCheckWithdrawn = true;
            }
            if (AnyTrue(postItems) && (postKey != AttemptKey || application.Stage != PressureBandageStage.Fixed))
                Array.Clear(postItems, 0, postItems.Length);
            if (situationConfirmed && dressing.HasCompletedContactPractice)
                contactPracticed = true;

            foreach (var requirement in PressureBandageTrainingIds.AllRequirements)
            {
                var value = Compute(requirement);
                current[(int)requirement] = value;
                var (target, signal) = PressureBandageTrainingIds.SignalOf(requirement);
                var t = target == PressureBandageTrainingIds.EquipmentTarget ? equipmentTarget : traineeTarget;
                if (value)
                    t.SetStateTrue(signal);
                else
                    t.SetStateFalse(signal);
            }

        }

        void CaptureSummary()
        {
            if (summary != null || !valid || !IsCurrentRun || runner.Status != TrainingStatus.Completed)
                return;
            var values = new bool[current.Length];
            foreach (var requirement in PressureBandageTrainingIds.AllRequirements)
                values[(int)requirement] = Compute(requirement);
            summary = new PressureBandageTrainingSummary(startedRunId, values);
        }

        void ClearFacts()
        {
            Array.Clear(current, 0, current.Length);
            Array.Clear(preItems, 0, preItems.Length);
            Array.Clear(postItems, 0, postItems.Length);
            situationConfirmed = false;
            contactPracticed = false;
            preKey = postKey = default;
            PreCheckWithdrawn = false;
            DropCheck();
            foreach (var signal in PressureBandageTrainingIds.EquipmentSignals)
                equipmentTarget.SetStateFalse(signal);
            foreach (var signal in PressureBandageTrainingIds.TraineeSignals)
                traineeTarget.SetStateFalse(signal);
            summary = null;
        }

        static bool AllTrue(bool[] values) => Array.TrueForAll(values, v => v);
        static bool AnyTrue(bool[] values) => Array.Exists(values, v => v);
        static bool SameToken(TrainingStepToken a, TrainingStepToken b) => a.RunId == b.RunId && a.StepEntry == b.StepEntry;

        public bool TryValidate(out string error)
        {
            var problems = new List<string>();
            if (runner == null)
                problems.Add(nameof(runner) + " missing");
            if (dressing == null || application == null)
                problems.Add("dressing or application missing");
            else if (application.Dressing != dressing)
                problems.Add("application does not use this dressing");
            if (equipmentTarget == null || equipmentTarget.TargetId != PressureBandageTrainingIds.EquipmentTarget)
                problems.Add($"{nameof(equipmentTarget)} must be '{PressureBandageTrainingIds.EquipmentTarget}'");
            else
            {
                foreach (var signal in PressureBandageTrainingIds.EquipmentSignals)
                {
                    if (!equipmentTarget.HasState(signal))
                        problems.Add($"{nameof(equipmentTarget)} has no state '{signal}'");
                }
            }
            if (traineeTarget == null || traineeTarget == equipmentTarget || traineeTarget.TargetId != PressureBandageTrainingIds.TraineeTarget)
                problems.Add($"{nameof(traineeTarget)} must be '{PressureBandageTrainingIds.TraineeTarget}'");
            else
            {
                foreach (var signal in PressureBandageTrainingIds.TraineeSignals)
                {
                    if (!traineeTarget.HasState(signal))
                        problems.Add($"{nameof(traineeTarget)} has no state '{signal}'");
                }
            }
            error = problems.Count > 0 ? $"PressureBandageTrainingLink '{name}': {string.Join(", ", problems)}." : null;
            return error == null;
        }
    }
}
