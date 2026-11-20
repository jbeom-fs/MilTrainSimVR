using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Object = UnityEngine.Object;

namespace MilTrainSimVR.B.BleedingControl.PressureBandage.Tutorial
{
    public enum PressureTrainingStartState
    {
        WaitingForStart = 0,
        Started = 1,
        Faulted = 2,
        // Retry in progress: locked like WaitingForStart, but no start is accepted.
        Resetting = 3
    }

    // Training start gate and course lock of the pressure bandage equipment. One extra select filter on the four kit grabs
    // (dressing, roll, end, tape) refuses new selections unless the course allows that part now (PressureBandageTrainingLink:
    // dressing after the situation input, bandage parts after the practice and the pre-wrap check, nothing before the start,
    // after a stop or after completion). The equipment's own filters (one hand one part, near grab, stage, lock after fixing) stay
    // as they are and XRI applies all of them; no enabled flag or interaction layer is written. A selection in progress always
    // passes (XRI re-checks filters while selected), so this gate never drops a part; ending selections belongs to the equipment
    // reset (PressureBandageApplication.ResetEquipment). The same order holds back the start of a wrap by a roll already in hand
    // (PressureBandageApplication.WrapStartLocked, written every Update; a wrap under way is never stopped). The start is accepted once, only when references and the equipment are
    // ready and the runner really runs this course. An interactor whose select input is still pressed when the start is accepted
    // cannot select until it lets go, so the start press never becomes a grab.
    public sealed class PressureBandageTrainingStart : MonoBehaviour, IXRSelectFilter
    {
        [SerializeField] PressureBandageTrainingLink link;
        [Tooltip("Dressing grab (the course allows it after the situation input).")]
        [SerializeField] XRGrabInteractable dressingGrab;
        [Tooltip("Roll, end and tape grabs (the course allows them after the practice and the pre-wrap check).")]
        [SerializeField] XRGrabInteractable[] bandageGrabs = Array.Empty<XRGrabInteractable>();
        [Tooltip("Training field only: the station that approves the start (ITrainingStartGate; one course at a time). Empty in the tutorial scene.")]
        [SerializeField] Object startGate;

        static readonly Predicate<IXRSelectInteractor> Released = i => i as Object == null || !IsSelectPressed(i);

        readonly HashSet<IXRSelectInteractor> waitingForRelease = new HashSet<IXRSelectInteractor>();
        readonly List<IXRInteractor> registered = new List<IXRInteractor>();
        bool subscribed;

        public PressureTrainingStartState State { get; private set; } = PressureTrainingStartState.WaitingForStart;
        public PressureBandageTrainingLink Link => link;
        public bool canProcess => isActiveAndEnabled;

        void Awake()
        {
            if (!TryValidate(out var error))
            {
                State = PressureTrainingStartState.Faulted;
                Debug.LogError($"[PressureBandageTraining] {error} Equipment input stays locked.", this);
            }
        }

        void OnEnable()
        {
            if (subscribed)
                return;
            foreach (var grab in AllGrabs())
            {
                if (grab != null)
                    grab.selectFilters.Add(this);
            }
            subscribed = true;
        }

        void OnDisable()
        {
            if (!subscribed)
                return;
            foreach (var grab in AllGrabs())
            {
                if (grab != null)
                    grab.selectFilters.Remove(this);
            }
            subscribed = false;
            // Without the gate the course lock is off for the grabs, so also for the wrap start.
            if (link != null && link.BandageApplication != null)
                link.BandageApplication.WrapStartLocked = false;
        }

        void Update()
        {
            if (waitingForRelease.Count > 0)
                waitingForRelease.RemoveWhere(Released);
            // The same course order for the start of a wrap: a roll already in hand passes the select filter (a selection in
            // progress always does), so the application is told not to start a wrap until the bandage parts are allowed. Read by
            // its LateUpdate this frame; it never stops a wrap under way.
            if (link != null && link.BandageApplication != null)
                link.BandageApplication.WrapStartLocked = !(State == PressureTrainingStartState.Started && link.AllowsBandageParts);
        }

        bool Held => link != null && link.Runner != null && link.Runner.Held;

        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            if (interactor == null)
                return false;
            if (interactor.IsSelecting(interactable))
                return true;
            if (State != PressureTrainingStartState.Started || waitingForRelease.Contains(interactor) || Held)
                return false;
            return ReferenceEquals(interactable, dressingGrab) ? link.AllowsDressing : link.AllowsBandageParts;
        }

        public bool TryStart(out string error)
        {
            if (State != PressureTrainingStartState.WaitingForStart)
            {
                error = $"Start ignored in state {State}.";
                return false;
            }
            // Training field: another course owns the field; nothing has changed here.
            if (!MilTrainSimVR.B.Training.TrainingStartGate.TryClaim(startGate))
            {
                error = "another course of the training field is in use.";
                return false;
            }
            if (!TryValidate(out error) || !TryCheckReady(out error) || !link.TryBeginRun(out error))
            {
                MilTrainSimVR.B.Training.TrainingStartGate.Cancel(startGate);
                State = PressureTrainingStartState.Faulted;
                Debug.LogError($"[PressureBandageTraining] Training start refused: {error} Equipment input stays locked.", this);
                return false;
            }

            State = PressureTrainingStartState.Started;
            HoldPressedInteractors();
            return true;
        }

        public void LockForReset()
        {
            State = PressureTrainingStartState.Resetting;
            waitingForRelease.Clear();
        }

        public bool TryReturnToWaiting(out string error)
        {
            if (State != PressureTrainingStartState.Resetting)
            {
                error = $"the start gate is {State}, not resetting.";
                return false;
            }
            waitingForRelease.Clear();
            if (!TryValidate(out error) || !TryCheckReady(out error))
            {
                State = PressureTrainingStartState.Faulted;
                return false;
            }
            State = PressureTrainingStartState.WaitingForStart;
            return true;
        }

        public void FaultReset()
        {
            State = PressureTrainingStartState.Faulted;
            waitingForRelease.Clear();
        }

        // Equipment initial and unselected, every grab registered with a manager (the end grab may be inactive until detached).
        public bool TryCheckReady(out string error)
        {
            if (!link.TryCheckEquipmentReady(out error))
                return false;
            foreach (var grab in AllGrabs())
            {
                if (grab.isActiveAndEnabled && grab.interactionManager == null)
                    error = $"'{grab.name}' has no interaction manager.";
                else if (grab.isSelected)
                    error = $"'{grab.name}' is already selected.";
                if (error != null)
                    return false;
            }
            return true;
        }

        void HoldPressedInteractors()
        {
            waitingForRelease.Clear();
            var managers = new HashSet<XRInteractionManager>();
            foreach (var grab in AllGrabs())
            {
                if (grab.interactionManager == null || !managers.Add(grab.interactionManager))
                    continue;
                grab.interactionManager.GetRegisteredInteractors(registered);
                foreach (var interactor in registered)
                {
                    if (interactor is IXRSelectInteractor select && IsSelectPressed(select))
                        waitingForRelease.Add(select);
                }
            }
        }

        static bool IsSelectPressed(IXRSelectInteractor interactor) =>
            interactor is XRBaseInputInteractor input ? input.isSelectActive || input.selectInput.ReadIsPerformed() : interactor.isSelectActive;

        IEnumerable<XRGrabInteractable> AllGrabs()
        {
            yield return dressingGrab;
            if (bandageGrabs == null)
                yield break;
            foreach (var grab in bandageGrabs)
                yield return grab;
        }

        public bool TryValidate(out string error)
        {
            var problems = new List<string>();
            if (link == null)
                problems.Add(nameof(link) + " missing");
            if (dressingGrab == null || bandageGrabs == null || bandageGrabs.Length != 3 || Array.IndexOf(bandageGrabs, null) >= 0)
                problems.Add("dressing grab and the three bandage grabs (roll, end, tape) required");
            else if (link != null && link.BandageApplication != null && link.Dressing != null)
            {
                var a = link.BandageApplication;
                if (dressingGrab != link.Dressing.Grab)
                    problems.Add("dressing grab is not the course dressing's grab");
                if (Array.IndexOf(bandageGrabs, a.RollGrab) < 0 || Array.IndexOf(bandageGrabs, a.EndGrab) < 0 || Array.IndexOf(bandageGrabs, a.TapeGrab) < 0)
                    problems.Add("bandage grabs are not the application's roll, end and tape grabs");
            }
            error = problems.Count > 0 ? $"PressureBandageTrainingStart '{name}': {string.Join(", ", problems)}." : null;
            return error == null;
        }
    }
}
