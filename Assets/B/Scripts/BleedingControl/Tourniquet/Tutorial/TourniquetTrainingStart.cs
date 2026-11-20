using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Object = UnityEngine.Object;

namespace MilTrainSimVR.B.BleedingControl.Tourniquet.Tutorial
{
    public enum TourniquetTrainingStartState
    {
        WaitingForStart = 0,
        Started = 1,
        Faulted = 2,
        // Retry in progress (TourniquetTrainingReset): locked like WaitingForStart, but a start is not accepted.
        Resetting = 3
    }

    // Training start gate of the tourniquet course. Until the start is accepted, new selections of the device body and its part
    // handles are refused by this extra select filter. The device's own conditions (part handles opened by step, body grab locked
    // after attaching) stay as they are and XRI applies both: nothing is deactivated and no enabled flag or interaction layer is
    // written. The start is accepted once, only when the references and the device are ready and the training runner really
    // runs (started through TourniquetTrainingLink while the equipment is still locked); otherwise the runner is left not
    // running and the lock stays. New selections are allowed only while that run is running or completed; a stopped run locks
    // them again. An interactor whose select input is still pressed when the start is accepted cannot select the equipment until
    // it lets go, so the start input never becomes a grab. The time record keeps its attach-time clock. After a completed run,
    // the retry (TourniquetTrainingReset) locks the gate first (Resetting) and returns it to WaitingForStart only after the
    // device reset checked out; a failed retry faults it, so the equipment stays locked.
    public sealed class TourniquetTrainingStart : MonoBehaviour, IXRSelectFilter
    {
        [SerializeField] TourniquetDevice device;
        [SerializeField] TourniquetOperation operation;
        [Tooltip("Starts and reports the training run for this device.")]
        [SerializeField] TourniquetTrainingLink link;
        [Tooltip("Every interactable of the device: the body grab and the four part handles.")]
        [SerializeField] XRBaseInteractable[] lockedInteractables = Array.Empty<XRBaseInteractable>();
        [Tooltip("Training field only: the station that approves the start (ITrainingStartGate; one course at a time). Empty in the tutorial scene.")]
        [SerializeField] Object startGate;

        static readonly Predicate<IXRSelectInteractor> Released = i => i as Object == null || !IsSelectPressed(i);

        readonly HashSet<IXRSelectInteractor> waitingForRelease = new HashSet<IXRSelectInteractor>();
        readonly List<IXRInteractor> registered = new List<IXRInteractor>();
        bool subscribed;

        public TourniquetTrainingStartState State { get; private set; } = TourniquetTrainingStartState.WaitingForStart;
        // Also locked while the runner is held (training field stop confirm).
        public bool InputAllowed => State == TourniquetTrainingStartState.Started && link.RunActive && !link.Runner.Held;
        public TourniquetDevice Device => device;
        public TourniquetTrainingLink Link => link;
        public IReadOnlyList<XRBaseInteractable> LockedInteractables => lockedInteractables;
        public event Action<TourniquetTrainingStart> StartAccepted;

        public bool canProcess => isActiveAndEnabled;

        void Awake()
        {
            if (!TryValidate(out var error))
            {
                State = TourniquetTrainingStartState.Faulted;
                Debug.LogError($"[Tourniquet] {error} Equipment input stays locked.", this);
            }
        }

        void OnEnable()
        {
            if (subscribed)
                return;
            foreach (var interactable in lockedInteractables)
            {
                if (interactable != null)
                    interactable.selectFilters.Add(this);
            }
            subscribed = true;
        }

        void OnDisable()
        {
            if (!subscribed)
                return;
            foreach (var interactable in lockedInteractables)
            {
                if (interactable != null)
                    interactable.selectFilters.Remove(this);
            }
            subscribed = false;
        }

        void Update()
        {
            if (waitingForRelease.Count > 0)
                waitingForRelease.RemoveWhere(Released);
        }

        // Gates new selections only; a hand already holding a point keeps it.
        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable) =>
            interactor.IsSelecting(interactable) || (InputAllowed && !waitingForRelease.Contains(interactor));

        // Accepts the start once, in this order: references, device still in its initial, unselected state with part handling
        // ready, then the training run (the runner must be running at the first step). Only then is the equipment unlocked. On
        // any failure the gate faults, no run is left running and the equipment stays locked.
        public bool TryStart(out string error)
        {
            if (State != TourniquetTrainingStartState.WaitingForStart)
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
                State = TourniquetTrainingStartState.Faulted;
                Debug.LogError($"[Tourniquet] Training start refused: {error} Equipment input stays locked.", this);
                return false;
            }

            State = TourniquetTrainingStartState.Started;
            HoldPressedInteractors();
            StartAccepted?.Invoke(this);
            return true;
        }

        // Retry entry point, first step: new selections locked, no start accepted, the release wait list cleared.
        public void LockForReset()
        {
            State = TourniquetTrainingStartState.Resetting;
            waitingForRelease.Clear();
        }

        // Retry entry point, after the device reset: back to waiting for the start only when the references and the device are
        // ready again (initial, unselected); otherwise faulted and locked.
        public bool TryReturnToWaiting(out string error)
        {
            if (State != TourniquetTrainingStartState.Resetting)
            {
                error = $"the start gate is {State}, not resetting.";
                return false;
            }
            waitingForRelease.Clear();
            if (!TryValidate(out error) || !TryCheckReady(out error))
            {
                State = TourniquetTrainingStartState.Faulted;
                return false;
            }
            State = TourniquetTrainingStartState.WaitingForStart;
            return true;
        }

        public void FaultReset()
        {
            State = TourniquetTrainingStartState.Faulted;
            waitingForRelease.Clear();
        }

        bool TryCheckReady(out string error)
        {
            error = !operation.IsReady ? "part handling is not ready (see the TourniquetOperation error)."
                : device.IsAttached || device.IsHeld ? $"device is not in its initial state ({device.State})."
                : null;
            foreach (var interactable in lockedInteractables)
            {
                if (error != null)
                    break;
                // Part handles stay disabled (unregistered) until their step opens; only enabled interactables need a manager now.
                if (interactable.isActiveAndEnabled && interactable.interactionManager == null)
                    error = $"'{interactable.name}' has no interaction manager.";
                else if (interactable.isSelected)
                    error = $"'{interactable.name}' is already selected.";
            }
            return error == null;
        }

        // Interactors whose select input is pressed right now (for example still holding after the start press) wait for a release.
        void HoldPressedInteractors()
        {
            waitingForRelease.Clear();
            var managers = new HashSet<XRInteractionManager>();
            foreach (var interactable in lockedInteractables)
            {
                if (interactable.interactionManager == null || !managers.Add(interactable.interactionManager))
                    continue;
                interactable.interactionManager.GetRegisteredInteractors(registered);
                foreach (var interactor in registered)
                {
                    if (interactor is IXRSelectInteractor select && IsSelectPressed(select))
                        waitingForRelease.Add(select);
                }
            }
        }

        static bool IsSelectPressed(IXRSelectInteractor interactor) =>
            interactor is XRBaseInputInteractor input ? input.isSelectActive || input.selectInput.ReadIsPerformed() : interactor.isSelectActive;

        public bool TryValidate(out string error)
        {
            var problems = new List<string>();
            if (device == null || operation == null)
                problems.Add("device or operation missing");
            else if (operation.Device != device)
                problems.Add("operation is not on this device");
            if (link == null)
                problems.Add(nameof(link) + " missing");
            else if (link.Device != device || link.Operation != operation)
                problems.Add("link is not on this device");
            if (lockedInteractables == null || lockedInteractables.Length == 0 || Array.IndexOf(lockedInteractables, null) >= 0)
                problems.Add(nameof(lockedInteractables) + " empty or missing entries");
            else if (device != null)
            {
                var all = device.GetComponentsInChildren<XRBaseInteractable>(true);
                var missing = Array.FindAll(all, i => Array.IndexOf(lockedInteractables, i) < 0);
                if (missing.Length > 0)
                    problems.Add("device interactables not locked: " + string.Join(", ", Array.ConvertAll(missing, i => i.name)));
                if (Array.Exists(lockedInteractables, i => Array.IndexOf(all, i) < 0))
                    problems.Add(nameof(lockedInteractables) + " has entries outside the device");
            }

            error = problems.Count > 0 ? $"TourniquetTrainingStart '{name}': {string.Join(", ", problems)}." : null;
            return error == null;
        }
    }
}
