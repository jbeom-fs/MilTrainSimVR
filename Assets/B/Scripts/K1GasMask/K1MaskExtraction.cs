using System;
using System.Collections.Generic;
using MilTrainSimVR.B.Interaction;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Transformers;
using UnityEngine.XR.Interaction.Toolkit.Utilities;

namespace MilTrainSimVR.B.K1GasMask
{
    // Stored K1 mask: grab it from an unlocked, fully open carrier, pull it straight up out of the mouth, then hold it freely.
    // Grab near or far by ray (a tracked controller; no distance limit). A far grab never skips the carrier: the stored mask
    // still needs the unlocked, fully open carrier and the pull out of the mouth.
    // Single pose path while grabbed: XRGrabInteractable (Instantaneous) with this component as its only grab transformer.
    //   Stored/extracting: target = stored pose moved along the carrier's up axis; rotation and lateral position stay stored.
    //     The pull is a RelativeHand point (starts at the pickup point, moves only by the controller's motion in the tracking
    //     space), so near and far pulls give the same amount and locomotion never pulls.
    //   Extracted: a free item (FreeHold) with the same grab: it keeps its rotation and turns only with the hand; a far grab
    //     brings it to the hand smoothly (from the moment it comes out, or from a new far grab), a near one holds it where it is.
    //     A new grab, a hand swap, tracking return and the end of a hold re-take the reference from the current pose, so nothing
    //     jumps or turns by itself. IsBringing is true while it is being brought (the face snap waits).
    // Untracked hands hold the mask still and their motion meanwhile is never applied.
    // Completion: every corner of the folded parts' conservative bounds must clear the mouth plane. Happens once.
    // Released masks stay where they are (kinematic, no throw). No re-storing, scoring or training flow here.
    // Face snap, strapping and worn states belong to K1MaskWear, which writes the body itself while ControlsBody is true.
    [DisallowMultipleComponent]
    public sealed class K1MaskExtraction : XRBaseGrabTransformer, IXRSelectFilter
    {
        // Extraction (m) above which the flap is held open; at or below it the mask counts as back in place.
        const float HoldThreshold = 0.001f;

        [Serializable]
        public struct PartBounds
        {
            public string part;
            public Bounds localBounds;
        }

        [SerializeField] XRGrabInteractable grab;
        [Tooltip("Grab reference on the mask; matches the carrier's Anchor_MaskPickup in the stored state.")]
        [SerializeField] Transform pickupPoint;
        [Tooltip("Scene link to the carrier holding this mask.")]
        [SerializeField] K1CarrierLid carrierLid;
        [Tooltip("Carrier Anchor_ExitPlane: mouth centre. The carrier's up axis is the extraction direction.")]
        [SerializeField] Transform exitPlane;
        [Tooltip("Bounds of each render part in the folded storage pose, in this object's local space (deformed harness included).")]
        [SerializeField] PartBounds[] storedOutline = Array.Empty<PartBounds>();
        [Tooltip("Engineering setting: every outline corner must be at least this far (m) above the mouth plane for completion.")]
        [SerializeField] float exitMargin = 0.005f;
        [Tooltip("Engineering setting: extraction may run this far (m) past the computed exit distance before it stops.")]
        [SerializeField] float extractionOvershoot = 0.03f;

        bool ready;
        Pose storedInCarrier;
        float maxExtraction;
        IXRSelectInteractor controlHand;
        TrackedPoseDriver controlDriver;
        bool hasBaseline;
        Vector3 baselineHand;
        float baselineExtraction;
        // Pull input point while stored or extracting, and the free hold once out (see the class notes).
        readonly RelativeHand pullPoint = new RelativeHand();
        readonly FreeHold free = new FreeHold();
        // The current grab has not set up its free hold yet: it starts with a bring (if far) instead of a plain re-take.
        bool freshGrab;
        bool holdingLid;
        bool subscribed;
        // Takes over the body pose while it is face controlled (snap, temporary hold, worn).
        K1MaskWear wear;

        // Extraction along the carrier's up axis in metres (0 = stored). Frozen once extracted.
        public float Extraction { get; private set; }
        public bool IsExtracted { get; private set; }
        // Set by a training field while it asks whether to stop the course (its confirm dialog). A held mask stays held but holds
        // still, as with an untracked hand (its baseline is dropped, so the hand's motion meanwhile is never applied and nothing
        // jumps after it); no extraction progresses. Not saved; false by default.
        public bool Suspended { get; set; }
        public K1CarrierLid Carrier => carrierLid;
        // Current grab permission apart from hand distance: extracted, or still stored with the carrier unlocked and fully open.
        public bool CanGrab => ready && (IsExtracted || (carrierLid.IsUnlocked && carrierLid.IsFullyOpen));
        // The mask is held by XRI (not face controlled) but the holding controller is not tracked, so it holds still. Read only.
        public bool IsHoldingHandUntracked => grab != null && grab.isSelected && (wear == null || !wear.ControlsBody)
                                              && !K1CarrierLid.IsTracked(K1CarrierLid.DriverOf(grab.firstInteractorSelecting));
        // The extracted mask is still being brought to the hand after a far grab (nothing is completed with it meanwhile).
        public bool IsBringing => grab != null && grab.isSelected && IsExtracted && (wear == null || !wear.ControlsBody) && free.IsBringing;
        // Raised once, on the frame the whole outline clears the mouth.
        public event Action<K1MaskExtraction> Extracted;

        void Awake()
        {
            TryGetComponent(out wear);
            if (!TryValidate(out var error))
            {
                Debug.LogError(error + " Grabbing stays blocked.", this);
                return;
            }

            // The saved scene pose is the stored pose; extraction is measured from it in carrier space.
            var carrier = carrierLid.transform;
            storedInCarrier = new Pose(carrier.InverseTransformPoint(transform.position), Quaternion.Inverse(carrier.rotation) * transform.rotation);
            maxExtraction = Mathf.Max(0f, exitMargin - LowestCornerHeight(transform.position, transform.rotation)) + extractionOvershoot;
            ready = true;
        }

        void OnEnable()
        {
            if (subscribed || grab == null)
                return;

            grab.selectFilters.Add(this);
            grab.selectEntered.AddListener(OnGrabbed);
            subscribed = true;
        }

        void OnDisable()
        {
            if (subscribed)
            {
                grab.selectFilters.Remove(this);
                grab.selectEntered.RemoveListener(OnGrabbed);
            }
            subscribed = false;
            SetLidHold(false);
            controlHand = null;
            hasBaseline = false;
            pullPoint.End();
            free.End();
        }

        // Gates new grabs only; a hand already holding the mask keeps it (the manager re-checks filters every frame).
        bool IXRSelectFilter.Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            if (interactor.IsSelecting(interactable))
                return true;
            if (!CanGrab)
                return false;
            return K1CarrierLid.IsTracked(K1CarrierLid.DriverOf(interactor));
        }

        public override void Process(XRGrabInteractable grabInteractable, XRInteractionUpdateOrder.UpdatePhase updatePhase,
                                     ref Pose targetPose, ref Vector3 localScale)
        {
            localScale = transform.localScale;
            var hand = grabInteractable.firstInteractorSelecting;
            if (hand != controlHand)
            {
                // New grab, re-grab or another hand took over: continue from the current pose with this hand.
                controlHand = hand;
                controlDriver = K1CarrierLid.DriverOf(hand);
                hasBaseline = false;
                freshGrab = true;
                pullPoint.Begin(hand != null ? hand.transform : null, pickupPoint.position);
            }

            // Face controlled: K1MaskWear writes the body and XRI tracking is off; keep no hand offset so that a release
            // continues from the exact face pose.
            if (wear != null && wear.ControlsBody)
            {
                hasBaseline = false;
                pullPoint.Step(false);
                targetPose = new Pose(transform.position, transform.rotation);
                return;
            }

            if (!ready || Suspended || !K1CarrierLid.IsTracked(controlDriver))
            {
                hasBaseline = false;
                pullPoint.Step(false);
                targetPose = new Pose(transform.position, transform.rotation);
                return;
            }

            var handPose = new Pose(controlDriver.transform.position, controlDriver.transform.rotation);
            if (IsExtracted)
            {
                if (!hasBaseline)
                    BeginFree(handPose, new Pose(transform.position, transform.rotation));
                targetPose = free.Evaluate(handPose);
                return;
            }

            pullPoint.Step(true);
            if (!hasBaseline)
            {
                baselineHand = pullPoint.Position;
                baselineExtraction = Extraction;
                hasBaseline = true;
            }

            var up = carrierLid.transform.up;
            var extraction = Mathf.Clamp(baselineExtraction + Vector3.Dot(pullPoint.Position - baselineHand, up), 0f, maxExtraction);

            // Leaving the stored position re-checks the carrier now, not only at grab time: the flap may have been closed
            // while the mask was held. If it is not unlocked and fully open, keep the grab but do not move, and drop the
            // baseline so hand motion meanwhile is not applied later. If it is, take the flap hold before moving.
            if (!holdingLid && extraction > HoldThreshold)
            {
                if (!carrierLid.IsUnlocked || !carrierLid.IsFullyOpen)
                {
                    hasBaseline = false;
                    targetPose = ExtractionPose(Extraction);
                    return;
                }

                SetLidHold(true);
            }

            Extraction = extraction;
            targetPose = ExtractionPose(Extraction);
            // Back in the stored position: release the hold; the next start checks the carrier again.
            if (Extraction <= HoldThreshold)
                SetLidHold(false);

            if (LowestCornerHeight(targetPose.position, targetPose.rotation) < exitMargin)
                return;

            // Whole outline is out: drop the up-axis constraint and keep the same hand, from this exact pose (the same grab goes
            // on as a free item; a far one is brought to the hand from here).
            IsExtracted = true;
            SetLidHold(false);
            pullPoint.End();
            BeginFree(handPose, targetPose);
            Extracted?.Invoke(this);
        }

        // New-run reset (training retry): back to the stored pose from Awake in the carrier's current frame, Extraction 0,
        // not extracted, no hand, baseline or free offset, flap hold released. Transform and Rigidbody get the same pose.
        // Raises no Extracted event. The body must already be released (the caller ends XRI selections first, so no XRI
        // target pose is applied afterwards); otherwise nothing changes. The stored pose itself is never re-captured.
        public bool ResetForNewRun(out string error)
        {
            if (!TryValidate(out error))
                return false;
            if (!ready)
            {
                error = $"K1MaskExtraction '{name}': not initialized; the stored pose is unknown.";
                return false;
            }
            if (grab.isSelected)
            {
                error = $"K1MaskExtraction '{name}': body still selected; end the selection before the reset.";
                return false;
            }

            controlHand = null;
            controlDriver = null;
            hasBaseline = false;
            baselineHand = default;
            baselineExtraction = 0f;
            freshGrab = false;
            pullPoint.End();
            free.End();
            SetLidHold(false);
            Extraction = 0f;
            IsExtracted = false;

            var stored = ExtractionPose(0f);
            transform.SetPositionAndRotation(stored.position, stored.rotation);
            if (TryGetComponent<Rigidbody>(out var body))
            {
                body.position = stored.position;
                body.rotation = stored.rotation;
            }
            return true;
        }

        public bool TryValidate(out string error)
        {
            var problems = new List<string>();
            if (grab == null || grab.transform != transform)
                problems.Add(nameof(grab) + " missing or not on this object");
            if (pickupPoint == null)
                problems.Add(nameof(pickupPoint) + " missing");
            if (carrierLid == null)
                problems.Add(nameof(carrierLid) + " not linked");
            if (exitPlane == null)
                problems.Add(nameof(exitPlane) + " not linked");
            if (storedOutline == null || storedOutline.Length == 0)
                problems.Add(nameof(storedOutline) + " empty");
            if (exitMargin < 0f || extractionOvershoot <= 0f)
                problems.Add(nameof(exitMargin) + " must be >= 0 and " + nameof(extractionOvershoot) + " > 0");

            error = problems.Count > 0 ? $"K1MaskExtraction '{name}': {string.Join(", ", problems)}." : null;
            return error == null;
        }

        // Every new selection, including a re-grab by the same hand, starts from the current pose.
        void OnGrabbed(SelectEnterEventArgs args)
        {
            controlHand = null;
            hasBaseline = false;
        }

        Pose ExtractionPose(float extraction)
        {
            var carrier = carrierLid.transform;
            return new Pose(carrier.TransformPoint(storedInCarrier.position) + carrier.up * extraction, carrier.rotation * storedInCarrier.rotation);
        }

        // Height of the lowest outline corner above the mouth plane, along the carrier's up axis, for this object at the given pose.
        float LowestCornerHeight(Vector3 position, Quaternion rotation)
        {
            var up = carrierLid.transform.up;
            var mouth = exitPlane.position;
            var lowest = float.PositiveInfinity;
            foreach (var part in storedOutline)
            {
                var b = part.localBounds;
                for (var i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    lowest = Mathf.Min(lowest, Vector3.Dot(position + rotation * corner - mouth, up));
                }
            }
            return lowest;
        }

        // A fresh grab (or the moment the grab's mask comes out) starts the free hold with a bring of the mask point nearest to the
        // hand; anything else (tracking return, hold over, release from the face) re-takes the reference from the current pose.
        void BeginFree(Pose hand, Pose mask)
        {
            if (freshGrab)
            {
                var grip = XRInteractableUtility.TryGetClosestPointOnCollider(grab, hand.position, out var nearest) ? nearest.point : mask.position;
                free.Begin(hand, mask, grip);
                freshGrab = false;
            }
            else
            {
                free.Rebase(hand, mask);
            }
            hasBaseline = true;
        }

        // Keeps the flap from closing onto a partly extracted mask; taken at the current (fully open) openness before the
        // first upward move, kept while released part way, released when the mask is back in place or fully out.
        void SetLidHold(bool hold)
        {
            if (hold == holdingLid || carrierLid == null)
                return;

            holdingLid = hold;
            if (hold)
                carrierLid.HoldOpenAtLeast(carrierLid.Openness);
            else
                carrierLid.ReleaseOpenHold();
        }
    }
}
