using System;
using System.Collections.Generic;
using MilTrainSimVR.B.Training;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Object = UnityEngine.Object;

namespace MilTrainSimVR.B.Decontamination.KD1.Tutorial
{
    public enum KD1CourseState
    {
        WaitingForStart = 0,
        Running = 1,
        Completed = 2,
        // Restart / retry in progress: locked, no start accepted.
        Resetting = 3,
        // References invalid, a reset failed or the run ended without its summary: locked until the trainer looks at it.
        Faulted = 4
    }

    // Why the course cannot start right now (start panel line); None when it can.
    public enum KD1StartBlock
    {
        None = 0,
        Setup = 1,
        NotInitial = 2,
        Held = 3,
        Waiting = 4,
        Focus = 5
    }

    // In-memory result of one completed run (its run ID and the areas recorded when it completed). Kept until the restart/retry;
    // never saved, no score.
    public sealed class KD1TrainingSummary
    {
        public int RunId { get; }
        public int AreasDone { get; }
        public int AreaCount { get; }

        public KD1TrainingSummary(int runId, int areasDone, int areaCount)
        {
            RunId = runId;
            AreasDone = areasDone;
            AreaCount = areaCount;
        }
    }

    // The KD-1 course: start gate of the equipment (KD1Equipment.courseGate), the facts the common TrainingRunner reads, the
    // completion summary and the one restart/retry path. KD1Equipment and KD1SurfacePractice stay the only owners of the equipment
    // state and the area records; this component reads them and never opens, pulls, fits, removes or records anything.
    //
    // Input: the equipment asks AllowsInput for new grabs, the automatic opening, pull completion, fitting/removal presses and area
    // records, and AllowsSelect for each new grab. Both allow only while the run this course started runs. Before the start, while
    // resetting, after a fault and after completion nothing new happens on the equipment; UI stays usable. A selection in progress
    // is never ended by the gate (an item held at completion can still be moved and put down; the retry ends it through the
    // equipment reset). Hands whose select input is still pressed when the start is accepted cannot grab until they let go, and a
    // trigger press needs a valid release after the start (the equipment's trigger validity includes the gate), so the press that
    // started the course never becomes a grab, an opening or a fit. The other hand never has to do anything.
    //
    // Facts (TrainingTarget 'kd1-course', written in this LateUpdate, order 58: after the equipment at 50 and the practice at 55;
    // the runner at 0 reads them in its next LateUpdate): outer-opened, pad-out and pad-fitted are kept once seen in this run
    // (KD1TrainingIds), areas-done is the practice's current IsComplete (kept until the reset). Nothing is written outside the run.
    //
    // Completion: when the runner reports Completed for this run and every area is recorded at that moment, the summary is kept once
    // and the course is Completed (locked, the scene left as it is: no reset, no pad taken off). Otherwise the course faults.
    //
    // Restart (during the run) and retry (after completion) are the same TryReset: ① state Resetting (no new input, no start, a
    // second request ignored), ② runner stopped, the run, its facts and summary forgotten (late events of the old run cannot reach
    // the next one: the runner checks its run token and the facts are cleared), ③ KD1Equipment.ResetEquipment (selections ended
    // through XRI, pads off the hands, all pieces back; it raises EquipmentReset, which clears the practice records and hides the
    // markers), ④ the exact restore checked (KD1Equipment.CheckRestored, pose included, and the practice), ⑤ WaitingForStart (the
    // board shows the start panel). No run starts by itself. On any failure the course faults and stays locked; the board shows
    // the failure line.
    //
    // The start screen and the start press use the ready check instead (KD1Equipment.CheckReadyToStart: the same logical start
    // state without the resting outer pack's exact pose, plus no recorded area, shown marker or kept summary), so the kit settling
    // with gravity on the workbench while waiting never blocks the start.
    [DefaultExecutionOrder(58)]
    [DisallowMultipleComponent]
    public sealed class KD1TrainingCourse : MonoBehaviour, IKD1CourseGate
    {
        [SerializeField] TrainingRunner runner;
        [Tooltip("TrainingTarget '" + KD1TrainingIds.CourseTarget + "' with the course's states.")]
        [SerializeField] TrainingTarget courseTarget;
        [SerializeField] KD1Equipment equipment;
        [SerializeField] KD1SurfacePractice practice;
        [Tooltip("Training field only: the station that approves the start (ITrainingStartGate; one course at a time). Empty in the tutorial scene.")]
        [SerializeField] Object startGate;

        static readonly Predicate<IXRSelectInteractor> Released = i => i as Object == null || !IsSelectPressed(i);

        readonly HashSet<IXRSelectInteractor> waitingForRelease = new HashSet<IXRSelectInteractor>();
        readonly List<IXRInteractor> registered = new List<IXRInteractor>();
        KD1Guide[] equipmentGuides = Array.Empty<KD1Guide>();
        bool valid;
        int startedRunId;
        bool opened;
        bool padOut;
        bool fitted;

        public KD1CourseState State { get; private set; } = KD1CourseState.WaitingForStart;
        public TrainingRunner Runner => runner;
        public TrainingTarget CourseTarget => courseTarget;
        public KD1Equipment Equipment => equipment;
        public KD1SurfacePractice Practice => practice;
        public string FaultReason { get; private set; }
        public KD1TrainingSummary Summary { get; private set; }
        public bool IsCurrentRun => valid && startedRunId != 0 && runner.CurrentToken.RunId == startedRunId;
        public bool IsRunning => IsCurrentRun && runner.Status == TrainingStatus.Running && State == KD1CourseState.Running;
        // 0-based current step while this run runs, -1 otherwise.
        public int CurrentStep => IsRunning ? KD1TrainingIds.StepIndex(runner.CurrentStepId) : -1;

        // Also nothing new while the runner is held (training field stop confirm): no grab, opening, fit or area record.
        public bool AllowsInput => IsRunning && !runner.Held;
        public bool AllowsSelect(IXRSelectInteractor interactor) => interactor != null && !waitingForRelease.Contains(interactor);

        void Awake()
        {
            // Equipment boards of the loaded scenes, inactive ones included: one switched on would add a second screen owner and a
            // reset button outside the course, so it blocks the start.
            equipmentGuides = FindObjectsByType<KD1Guide>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            valid = TryValidate(out var error);
            if (!valid)
                Fault(error);
        }

        void Update()
        {
            if (waitingForRelease.Count > 0)
                waitingForRelease.RemoveWhere(Released);
        }

        void LateUpdate()
        {
            if (!valid || State != KD1CourseState.Running)
                return;
            if (!IsCurrentRun || runner.Status == TrainingStatus.Stopped || runner.Status == TrainingStatus.Idle)
            {
                Fault("the training runner stopped this run from outside.");
                return;
            }
            if (runner.Status == TrainingStatus.Completed)
            {
                CaptureSummary();
                return;
            }
            Observe();
        }

        void Observe()
        {
            opened |= equipment.OuterPack.IsOpen;
            foreach (var pad in equipment.Pads)
            {
                if (pad.State == KD1ItemState.Free || pad.State == KD1ItemState.Worn)
                    padOut = true;
            }
            fitted |= equipment.FittedOn(KD1Hand.Left) != null || equipment.FittedOn(KD1Hand.Right) != null;
            Write(KD1TrainingIds.OuterOpened, opened);
            Write(KD1TrainingIds.PadOut, padOut);
            Write(KD1TrainingIds.PadFitted, fitted);
            Write(KD1TrainingIds.AreasDone, practice.IsComplete);
        }

        void Write(string signal, bool value)
        {
            if (value)
                courseTarget.SetStateTrue(signal);
            else
                courseTarget.SetStateFalse(signal);
        }

        void CaptureSummary()
        {
            if (!practice.IsComplete)
            {
                Fault($"run {startedRunId} completed but only {practice.DoneCount}/{practice.ZoneCount} areas are recorded now.");
                return;
            }
            Summary = new KD1TrainingSummary(startedRunId, practice.DoneCount, practice.ZoneCount);
            State = KD1CourseState.Completed;
        }

        // ---------- Start ----------

        public bool TryStart(out string error)
        {
            if (State != KD1CourseState.WaitingForStart)
            {
                error = $"start ignored in state {State}.";
                return false;
            }
            if (!TryCheckReady(out var block, out error))
            {
                if (block == KD1StartBlock.Setup)
                    Fault(error);
                return false;
            }
            if (runner.Status == TrainingStatus.Running)
            {
                Fault("the training runner is already running a run this course did not start.");
                error = FaultReason;
                return false;
            }
            // Training field: another course owns the field; nothing has changed here.
            if (!TrainingStartGate.TryClaim(startGate))
            {
                error = "another course of the training field is in use.";
                return false;
            }

            ClearFacts();
            runner.StartTraining();
            var first = KD1TrainingIds.Steps[0].step;
            if (runner.Status != TrainingStatus.Running || runner.CurrentStepId != first)
            {
                if (runner.Status == TrainingStatus.Running)
                    runner.StopTraining();
                ClearFacts();
                TrainingStartGate.Cancel(startGate);
                Fault($"the training runner did not start at '{first}' (status {runner.Status}, step '{runner.CurrentStepId}'; see the [Training] error).");
                error = FaultReason;
                return false;
            }
            startedRunId = runner.CurrentToken.RunId;
            State = KD1CourseState.Running;
            HoldPressedInteractors();
            Observe();
            error = null;
            return true;
        }

        // Ready for a new run: valid references and data, no equipment board active here, the equipment and the practice in their
        // start state, nothing selected, focus. The other hand's tracking or input is never asked for.
        public bool TryCheckReady(out KD1StartBlock block, out string error)
        {
            block = KD1StartBlock.Setup;
            if (!valid || !TryValidate(out error))
            {
                error = FaultReason ?? "KD1TrainingCourse references are invalid.";
                return false;
            }
            if (!equipment.IsValid || !practice.IsValid)
            {
                error = "the equipment or the surface practice setup is invalid (see its error).";
                return false;
            }
            foreach (var guide in equipmentGuides)
            {
                if (guide != null && guide.isActiveAndEnabled && guide.Equipment == equipment)
                {
                    error = $"the equipment board '{guide.name}' is active in the course scene.";
                    return false;
                }
            }
            if (equipment.IsFaulted)
            {
                error = "the last equipment reset failed: " + equipment.FaultReason;
                return false;
            }
            if (!equipment.isActiveAndEnabled || equipment.IsResetting || !practice.isActiveAndEnabled)
            {
                block = KD1StartBlock.Waiting;
                error = $"the equipment or the practice is not running now (equipment enabled {equipment.isActiveAndEnabled}, resetting {equipment.IsResetting}, " +
                        $"practice enabled {practice.isActiveAndEnabled}).";
                return false;
            }
            foreach (var item in equipment.Items)
            {
                if (item.Grab.isSelected)
                {
                    block = KD1StartBlock.Held;
                    error = $"{item.name} ({item.Kind} {item.Number + 1}) is selected.";
                    return false;
                }
            }
            // Ready check (logical start state, not the outer pack's exact resting pose: it may settle on the workbench while waiting).
            // Each course condition is reported on its own.
            var notInitial = equipment.CheckReadyToStart();
            if (notInitial != null)
            {
                block = KD1StartBlock.NotInitial;
                error = "equipment " + notInitial;
                return false;
            }
            if (practice.DoneCount != 0)
            {
                block = KD1StartBlock.NotInitial;
                error = $"[areas-recorded] expected 0 recorded areas, DoneCount {practice.DoneCount} of ZoneCount {practice.ZoneCount}";
                return false;
            }
            if (practice.MarkersShown)
            {
                block = KD1StartBlock.NotInitial;
                error = "[markers-shown] expected the area markers hidden (no pad fitted), MarkersShown True";
                return false;
            }
            if (Summary != null)
            {
                block = KD1StartBlock.NotInitial;
                error = $"[summary-kept] expected no summary, summary of run {Summary.RunId} kept";
                return false;
            }
            if (!Application.isFocused)
            {
                block = KD1StartBlock.Focus;
                error = "the application has no input focus.";
                return false;
            }
            block = KD1StartBlock.None;
            error = null;
            return true;
        }

        void HoldPressedInteractors()
        {
            waitingForRelease.Clear();
            var managers = new HashSet<XRInteractionManager>();
            foreach (var item in equipment.Items)
            {
                var manager = item.Grab.interactionManager;
                if (manager == null || !managers.Add(manager))
                    continue;
                manager.GetRegisteredInteractors(registered);
                foreach (var interactor in registered)
                {
                    if (interactor is IXRSelectInteractor select && IsSelectPressed(select))
                        waitingForRelease.Add(select);
                }
            }
        }

        static bool IsSelectPressed(IXRSelectInteractor interactor) =>
            interactor is XRBaseInputInteractor input ? input.isSelectActive || input.selectInput.ReadIsPerformed() : interactor.isSelectActive;

        // ---------- Restart / retry ----------

        public bool IsResetting => State == KD1CourseState.Resetting;
        public bool CanReset => valid && (State == KD1CourseState.Running || State == KD1CourseState.Completed);

        public bool TryReset()
        {
            if (!CanReset)
            {
                Debug.LogWarning($"[KD1Training] Restart ignored in state {State}.", this);
                return false;
            }
            State = KD1CourseState.Resetting;
            waitingForRelease.Clear();
            try
            {
                if (runner.Status == TrainingStatus.Running || runner.Status == TrainingStatus.Completed)
                    runner.StopTraining();
                startedRunId = 0;
                ClearFacts();
                if (!equipment.ResetEquipment())
                {
                    Fault("KD1Equipment.ResetEquipment failed or was ignored" + (equipment.IsFaulted ? ": " + equipment.FaultReason : " (see its log)."));
                    return false;
                }
                // Exact restore check of the reset just done (this frame: no physics step since, so the start pose is exact).
                var notInitial = equipment.CheckRestored();
                if (notInitial != null || practice.DoneCount != 0 || practice.MarkersShown)
                {
                    Fault("not back in the start state after the reset: " + (notInitial ?? $"{practice.DoneCount} area(s) still recorded or markers shown."));
                    return false;
                }
                State = KD1CourseState.WaitingForStart;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                Fault(e.Message);
                return false;
            }
        }

        void ClearFacts()
        {
            opened = false;
            padOut = false;
            fitted = false;
            Summary = null;
            if (courseTarget == null)
                return;
            foreach (var signal in KD1TrainingIds.Signals)
            {
                if (courseTarget.HasState(signal))
                    courseTarget.SetStateFalse(signal);
            }
        }

        void Fault(string reason)
        {
            State = KD1CourseState.Faulted;
            FaultReason = reason;
            waitingForRelease.Clear();
            Debug.LogError($"[KD1Training] {reason} Equipment input stays locked.", this);
        }

        public bool TryValidate(out string error)
        {
            var problems = new List<string>();
            if (runner == null)
                problems.Add(nameof(runner) + " missing");
            if (equipment == null)
                problems.Add(nameof(equipment) + " missing");
            else if (!ReferenceEquals(equipment.CourseGate, this))
                problems.Add("the equipment's course gate must be this course");
            if (practice == null || equipment != null && practice.Equipment != equipment)
                problems.Add(nameof(practice) + " missing or not the equipment's practice");
            if (courseTarget == null || courseTarget.TargetId != KD1TrainingIds.CourseTarget)
                problems.Add($"{nameof(courseTarget)} must be '{KD1TrainingIds.CourseTarget}'");
            else
            {
                foreach (var signal in KD1TrainingIds.Signals)
                {
                    if (!courseTarget.HasState(signal))
                        problems.Add($"{nameof(courseTarget)} has no state '{signal}'");
                }
            }
            error = problems.Count > 0 ? $"KD1TrainingCourse '{name}': {string.Join(", ", problems)}." : null;
            return error == null;
        }
    }
}
