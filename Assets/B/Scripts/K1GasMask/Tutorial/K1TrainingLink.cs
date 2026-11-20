using System;
using System.Collections.Generic;
using MilTrainSimVR.B.Training;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MilTrainSimVR.B.K1GasMask.Tutorial
{
    // Connects the K1 equipment to the common training runner. Reports facts only; never advances training or moves equipment.
    // Facts (closure released, lid fully open, mask extracted, mask on the face, mask worn) are latched as TrainingTarget states
    // the first time they are seen in the current run and kept for that run, so a fast sequence never loses a fact whose step
    // has not been shown yet: the runner reads them one step per frame. Each fact is read from the device's current state both
    // on its events and every frame, so a change missed by an event is still seen. A new run (new RunId) clears the facts.
    // Until the runner is running (or completed), new selections of the listed equipment points are refused by this extra
    // select filter; the devices' own filters and conditions stay as they are and nothing is deactivated.
    public sealed class K1TrainingLink : MonoBehaviour, IXRSelectFilter
    {
        [SerializeField] TrainingRunner runner;
        [SerializeField] K1CarrierLid carrier;
        [SerializeField] K1MaskExtraction extraction;
        [SerializeField] K1MaskWear wear;
        [Tooltip("TrainingTarget '" + K1TrainingIds.CarrierTarget + "': closure released and lid fully opened.")]
        [SerializeField] TrainingTarget carrierTarget;
        [Tooltip("TrainingTarget '" + K1TrainingIds.MaskTarget + "': mask extracted, on the face and worn.")]
        [SerializeField] TrainingTarget maskTarget;
        [Tooltip("Every select point of the carrier and the mask. New selections are locked until the training starts.")]
        [SerializeField] XRBaseInteractable[] lockedInteractables = Array.Empty<XRBaseInteractable>();

        bool valid;
        bool subscribed;
        int observedRunId;
        // Set for the whole retry reset; stays set if the reset fails, so the equipment stays locked.
        bool resetting;

        public K1CarrierLid Carrier => carrier;
        public K1MaskExtraction Extraction => extraction;
        public K1MaskWear Wear => wear;
        // Equipment input is allowed once the runner started; kept after completion so the explicit face release still works.
        // A retry stops the runner (Stopped), so the start screen after it is locked again until the next start.
        // Also locked while the runner is held (training field stop confirm).
        public bool InputAllowed => valid && !resetting && runner != null && !runner.Held &&
            (runner.Status == TrainingStatus.Running || runner.Status == TrainingStatus.Completed);

        public bool canProcess => isActiveAndEnabled;

        void Awake()
        {
            valid = TryValidate(out var error);
            if (!valid)
                Debug.LogError($"[K1Training] {error} Equipment input stays locked.", this);
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

            if (valid)
            {
                carrier.Unlocked += OnCarrierUnlocked;
                carrier.FullyOpenChanged += OnFullyOpenChanged;
                extraction.Extracted += OnExtracted;
                // Covers both the face snap (FaceHeld) and Worn: the state changes right after the device state is set.
                wear.StateChanged += OnWearStateChanged;
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

            if (valid)
            {
                carrier.Unlocked -= OnCarrierUnlocked;
                carrier.FullyOpenChanged -= OnFullyOpenChanged;
                extraction.Extracted -= OnExtracted;
                wear.StateChanged -= OnWearStateChanged;
            }

            subscribed = false;
        }

        void Update() => Observe();

        // Gates new selections only; a hand already holding a point keeps it.
        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable) =>
            interactor.IsSelecting(interactable) || InputAllowed;

        public bool TryValidate(out string error)
        {
            var problems = new List<string>();
            if (runner == null)
                problems.Add(nameof(runner) + " missing");
            if (carrier == null || extraction == null || wear == null)
                problems.Add("carrier, extraction or wear missing");
            else if (extraction.Carrier != carrier)
                problems.Add("extraction is not linked to this carrier");
            if (carrierTarget == null || carrierTarget.TargetId != K1TrainingIds.CarrierTarget
                || !carrierTarget.HasState(K1TrainingIds.ClosureReleased) || !carrierTarget.HasState(K1TrainingIds.LidFullyOpened))
                problems.Add($"{nameof(carrierTarget)} must be '{K1TrainingIds.CarrierTarget}' with its states");
            if (maskTarget == null || maskTarget.TargetId != K1TrainingIds.MaskTarget || !maskTarget.HasState(K1TrainingIds.MaskExtracted)
                || !maskTarget.HasState(K1TrainingIds.MaskOnFace) || !maskTarget.HasState(K1TrainingIds.MaskWorn))
                problems.Add($"{nameof(maskTarget)} must be '{K1TrainingIds.MaskTarget}' with its states");
            if (lockedInteractables == null || lockedInteractables.Length == 0 || Array.IndexOf(lockedInteractables, null) >= 0)
                problems.Add(nameof(lockedInteractables) + " empty or missing entries");

            error = problems.Count > 0 ? $"K1TrainingLink '{name}': {string.Join(", ", problems)}." : null;
            return error == null;
        }

        // Retry from the completion screen, in this order: (1) lock new selections, (2) stop the runner so no fact is collected,
        // (3) end every XRI selection still held on the carrier and mask, (4) reset wear, extraction and carrier through their
        // own reset APIs (no success events), (5) clear the five facts of the finished run at once. Everything that can fail
        // is checked before anything moves. On failure the equipment stays locked and the caller must not show the start
        // screen. Does not start a new run; the start button does. Subscriptions and filters are left as they are.
        public bool TryResetForNewRun(out string error)
        {
            resetting = true;
            if (!valid)
            {
                error = "K1TrainingLink is not valid; equipment not reset.";
                return false;
            }
            if (!carrier.TryValidate(out error) || !extraction.TryValidate(out error) || !wear.TryValidate(out error))
                return false;
            foreach (var interactable in lockedInteractables)
            {
                if (interactable.isSelected && interactable.interactionManager == null)
                {
                    error = $"'{interactable.name}' is selected but has no interaction manager; equipment not reset.";
                    return false;
                }
            }

            runner.StopTraining();

            // Copy first: SelectExit changes interactorsSelecting. The release callbacks (strap/flap hand handover, XRI drop)
            // find the runner stopped and every new selection refused, so they record nothing and select nothing.
            foreach (var interactable in lockedInteractables)
            {
                var selecting = new List<IXRSelectInteractor>(interactable.interactorsSelecting);
                foreach (var interactor in selecting)
                    interactable.interactionManager.SelectExit(interactor, interactable);
                if (interactable.isSelected)
                {
                    error = $"'{interactable.name}' is still selected after ending its selections; equipment not reset.";
                    return false;
                }
            }

            // Wear first (ends face control so nothing else writes the body), then the body pose, then the carrier.
            if (!wear.ResetForNewRun(out error) || !extraction.ResetForNewRun(out error) || !carrier.ResetForNewRun(out error))
                return false;

            ClearFacts();
            resetting = false;
            return true;
        }

        void ClearFacts()
        {
            carrierTarget.SetStateFalse(K1TrainingIds.ClosureReleased);
            carrierTarget.SetStateFalse(K1TrainingIds.LidFullyOpened);
            maskTarget.SetStateFalse(K1TrainingIds.MaskExtracted);
            maskTarget.SetStateFalse(K1TrainingIds.MaskOnFace);
            maskTarget.SetStateFalse(K1TrainingIds.MaskWorn);
        }

        void OnCarrierUnlocked(K1CarrierLid _) => Observe();
        void OnFullyOpenChanged(K1CarrierLid _, bool __) => Observe();
        void OnExtracted(K1MaskExtraction _) => Observe();
        void OnWearStateChanged(K1MaskWear _, K1MaskWearState __) => Observe();

        // Only latches states; the runner evaluates them in its own LateUpdate, so nothing advances inside a device event.
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

            if (carrier.IsUnlocked)
                carrierTarget.SetStateTrue(K1TrainingIds.ClosureReleased);
            if (carrier.IsFullyOpen)
                carrierTarget.SetStateTrue(K1TrainingIds.LidFullyOpened);
            if (extraction.IsExtracted)
                maskTarget.SetStateTrue(K1TrainingIds.MaskExtracted);
            if (wear.ControlsBody || wear.IsWorn)
                maskTarget.SetStateTrue(K1TrainingIds.MaskOnFace);
            if (wear.IsWorn)
                maskTarget.SetStateTrue(K1TrainingIds.MaskWorn);
        }
    }
}
