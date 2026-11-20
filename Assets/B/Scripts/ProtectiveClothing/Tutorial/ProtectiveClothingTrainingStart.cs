using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Object = UnityEngine.Object;

namespace MilTrainSimVR.B.ProtectiveClothing.Tutorial
{
    public enum ProtectiveClothingStartState
    {
        WaitingForStart = 0,
        Started = 1,
        Faulted = 2,
        // Retry in progress: locked like WaitingForStart, but no start is accepted.
        Resetting = 3
    }

    // Why the course cannot start right now (start screen line); None when it can.
    public enum ProtectiveClothingStartBlock
    {
        None = 0,
        // References, the equipment, the user body or the course data are invalid, a reset failed, or the equipment guide's
        // development reset is active in this scene: the trainer must look at it.
        Setup = 1,
        // The equipment is not in its initial state (a part worn or not back).
        NotInitial = 2,
        // A part is selected: put it down first.
        Held = 3,
        // The equipment or the user body is not running yet (paused, starting).
        Waiting = 4,
        // The application lost input focus.
        Focus = 5
    }

    // Start gate and course lock of the protective clothing equipment: the equipment's wear gate (ProtectiveClothingEquipment
    // asks it for every new grab and for every wear judgement; the equipment never references this assembly). The same
    // allowance answers both: only while the course this gate started runs, only the part of the current step that is not
    // worn yet (ProtectiveClothingTrainingLink.AllowsPart). Nothing is allowed before the start, while resetting, after a fault,
    // after a stop or after completion. A selection in progress is never ended by this gate (the equipment's filter lets it
    // pass); a held part the course does not allow is only kept from wearing (the equipment disarms it). The equipment's own
    // rules (one hand one part, glove in the other hand, tracking, focus, arming outside the zone) stay as they are.
    //
    // The start is accepted once, only when references, the equipment's initial state and the user body are ready and the
    // runner really runs this course. An interactor whose select input is still pressed when the start is accepted cannot
    // select until it lets go, so the start press never becomes a grab.
    [DisallowMultipleComponent]
    public sealed class ProtectiveClothingTrainingStart : MonoBehaviour, IProtectiveClothingWearGate
    {
        [SerializeField] ProtectiveClothingTrainingLink link;
        [Tooltip("Training field only: the station that approves the start (ITrainingStartGate; one course at a time). Empty in the tutorial scene.")]
        [SerializeField] Object startGate;

        static readonly Predicate<IXRSelectInteractor> Released = i => i as Object == null || !IsSelectPressed(i);

        readonly HashSet<IXRSelectInteractor> waitingForRelease = new HashSet<IXRSelectInteractor>();
        readonly List<IXRInteractor> registered = new List<IXRInteractor>();
        ProtectiveClothingGuide[] equipmentGuides = Array.Empty<ProtectiveClothingGuide>();

        public ProtectiveClothingStartState State { get; private set; } = ProtectiveClothingStartState.WaitingForStart;
        public ProtectiveClothingTrainingLink Link => link;
        public ProtectiveClothingEquipment Equipment => link != null ? link.Equipment : null;

        void Awake()
        {
            // Equipment guides of the loaded scenes, also inactive ones (the course scene keeps PC_Board saved inactive); checked
            // again on every readiness check, so one switched on later still blocks the start.
            equipmentGuides = FindObjectsByType<ProtectiveClothingGuide>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (!TryValidate(out var error))
            {
                State = ProtectiveClothingStartState.Faulted;
                Debug.LogError($"[ProtectiveClothingTraining] {error} Equipment input stays locked.", this);
            }
        }

        void Update()
        {
            if (waitingForRelease.Count > 0)
                waitingForRelease.RemoveWhere(Released);
        }

        // ---------- Wear gate (asked by the equipment) ----------

        // Also locked while the runner is held (training field stop confirm): no new grab and no wearing.
        bool Allows(ProtectiveClothingPart part) => State == ProtectiveClothingStartState.Started && link.AllowsPart(part) && !link.Runner.Held;

        public bool AllowsSelect(IXRSelectInteractor interactor, ProtectiveClothingPart part) =>
            interactor != null && !waitingForRelease.Contains(interactor) && Allows(part);

        public bool AllowsWear(ProtectiveClothingPart part) => Allows(part);

        // ---------- Start, reset and fault ----------

        public bool TryStart(out string error)
        {
            if (State != ProtectiveClothingStartState.WaitingForStart)
            {
                error = $"Start ignored in state {State}.";
                return false;
            }
            if (!TryCheckReady(out var block, out error))
            {
                // Not ready yet (a part in hand, paused body, focus): refused, still waiting. A setup problem faults.
                if (block == ProtectiveClothingStartBlock.Setup)
                {
                    State = ProtectiveClothingStartState.Faulted;
                    Debug.LogWarning($"[ProtectiveClothingTraining] Training start refused: {error} Equipment input stays locked.", this);
                }
                return false;
            }
            // Training field: another course owns the field; nothing has changed here.
            if (!MilTrainSimVR.B.Training.TrainingStartGate.TryClaim(startGate))
            {
                error = "another course of the training field is in use.";
                return false;
            }
            if (!link.TryBeginRun(out error))
            {
                MilTrainSimVR.B.Training.TrainingStartGate.Cancel(startGate);
                State = ProtectiveClothingStartState.Faulted;
                Debug.LogError($"[ProtectiveClothingTraining] Training start refused: {error} Equipment input stays locked.", this);
                return false;
            }

            State = ProtectiveClothingStartState.Started;
            HoldPressedInteractors();
            return true;
        }

        public void LockForReset()
        {
            State = ProtectiveClothingStartState.Resetting;
            waitingForRelease.Clear();
        }

        public bool TryReturnToWaiting(out string error)
        {
            if (State != ProtectiveClothingStartState.Resetting)
            {
                error = $"the start gate is {State}, not resetting.";
                return false;
            }
            waitingForRelease.Clear();
            if (!TryCheckReady(out var block, out error) && block != ProtectiveClothingStartBlock.Waiting && block != ProtectiveClothingStartBlock.Focus)
            {
                State = ProtectiveClothingStartState.Faulted;
                return false;
            }
            error = null;
            State = ProtectiveClothingStartState.WaitingForStart;
            return true;
        }

        public void FaultReset()
        {
            State = ProtectiveClothingStartState.Faulted;
            waitingForRelease.Clear();
        }

        // Ready for a new run: valid references and course data, a valid, running and unfaulted equipment and user body, focus,
        // nothing worn, every pickup back (no worn mesh shown), nothing selected, every grab registered with a manager, and no
        // active equipment guide in this scene (its development reset button would bypass the course).
        public bool TryCheckReady(out ProtectiveClothingStartBlock block, out string error)
        {
            block = ProtectiveClothingStartBlock.Setup;
            if (!TryValidate(out error) || !link.TryValidate(out error))
                return false;
            var equipment = link.Equipment;
            if (!equipment.IsValid || !equipment.Body.IsValid)
            {
                error = "the equipment or the user body setup is invalid (see its error).";
                return false;
            }
            if (equipment.IsFaulted)
            {
                error = "the last equipment reset failed: " + equipment.FaultReason;
                return false;
            }
            if (!ReferenceEquals(equipment.WearGate, this))
            {
                error = "the equipment's wear gate is not this start gate.";
                return false;
            }
            foreach (var guide in equipmentGuides)
            {
                if (guide != null && guide.isActiveAndEnabled && guide.Equipment == equipment)
                {
                    error = $"the equipment guide '{guide.name}' (development reset button) is active in the course scene.";
                    return false;
                }
            }
            if (equipment.WornCount > 0)
            {
                block = ProtectiveClothingStartBlock.NotInitial;
                error = $"{equipment.WornCount} part(s) already worn.";
                return false;
            }
            foreach (var piece in equipment.Pieces)
            {
                if (piece.Worn.activeSelf || !piece.gameObject.activeSelf)
                {
                    block = ProtectiveClothingStartBlock.NotInitial;
                    error = $"{piece.Part} is not back on the workbench (pickup hidden or worn mesh shown).";
                    return false;
                }
                if (piece.Grab.isSelected)
                {
                    block = ProtectiveClothingStartBlock.Held;
                    error = $"{piece.Part} is selected.";
                    return false;
                }
                if (piece.Grab.isActiveAndEnabled && piece.Grab.interactionManager == null)
                {
                    error = $"'{piece.Grab.name}' has no interaction manager.";
                    return false;
                }
            }
            if (!equipment.isActiveAndEnabled || equipment.IsResetting || !equipment.Body.IsRunning)
            {
                block = ProtectiveClothingStartBlock.Waiting;
                error = "the equipment or the user body is not running now.";
                return false;
            }
            if (!Application.isFocused)
            {
                block = ProtectiveClothingStartBlock.Focus;
                error = "the application has no input focus.";
                return false;
            }
            block = ProtectiveClothingStartBlock.None;
            error = null;
            return true;
        }

        void HoldPressedInteractors()
        {
            waitingForRelease.Clear();
            var managers = new HashSet<XRInteractionManager>();
            foreach (var piece in link.Equipment.Pieces)
            {
                var manager = piece.Grab.interactionManager;
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

        public bool TryValidate(out string error)
        {
            var problems = new List<string>();
            if (link == null)
                problems.Add(nameof(link) + " missing");
            else if (link.Equipment == null)
                problems.Add("the link has no equipment");
            else if (!ReferenceEquals(link.Equipment.WearGate, this))
                problems.Add("the equipment's wear gate must be this start gate");
            error = problems.Count > 0 ? $"ProtectiveClothingTrainingStart '{name}': {string.Join(", ", problems)}." : null;
            return error == null;
        }
    }
}
