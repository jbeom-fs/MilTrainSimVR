using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MilTrainSimVR.B.K1GasMask
{
    public enum K1MaskWearState
    {
        Stored = 0,          // in the carrier, including extraction (K1MaskExtraction)
        Extracted = 1,       // fully out, harness still folded, free grab
        Unfolding = 2,       // harness partly pulled open toward Loose_Front by a strap hand, body still free
        Ready = 3,           // harness at Loose_Front, free grab (face snap is possible from Extracted on)
        FaceHeld = 4,        // body held on the face reference, harness opening to or at Loose_Front; not worn
        Strapping = 5,       // body on the face, harness moving along the donning route
        Worn = 6,            // harness seated behind the head; body and harness follow the head
        StrapsReturning = 7, // released from the face, harness running back to Loose_Front
    }

    // K1 mask after extraction, simplified: bring the fully extracted mask near the face and it snaps and stays there, held or
    // not; the harness opens to Loose_Front by itself on the snap; then one hand takes either strap roughly behind the head and
    // the straps seat themselves, which locks the mask as worn. Pulling a strap to unfold before the snap stays optional.
    // The face snap stands in for one hand holding the mask on the face, so the other hand is free for the strap.
    // Body pose has one writer per state: XRI (via K1MaskExtraction) while free; this component while face controlled, with
    // XRGrabInteractable position/rotation tracking switched off so XRI's Dynamic/OnBeforeRender updates never write it.
    // Harness bones are written only through K1MaskHarnessPose; strap hands only produce unfold and route progress.
    // Either strap point and either hand can drive; the most recent strap selection is the single controlling hand.
    // Strapping is judged by one wide zone behind the eyes in the tracked head's frame, not by a path (DriveStraps).
    // Activate (new press) on the held body releases face control (FaceHeld/Strapping/Worn); plain release never does.
    // Snap, zone and speed values are engineering settings, not fit, seal or training pass criteria.
    // Reports state only; no tightening, scoring, audio or training progression here.
    [DisallowMultipleComponent]
    public sealed class K1MaskWear : MonoBehaviour, IXRSelectFilter
    {
        // Route stage the straps follow the hand up to before the automatic seat (K1MaskHarnessPose: 4 Behind_Head, 5 Seated).
        const float BehindHead = 4f;

        [SerializeField] K1MaskExtraction extraction;
        [SerializeField] XRGrabInteractable grab;
        [SerializeField] K1MaskHarnessPose harness;
        [Tooltip("Anchor_FaceFit of this mask; aligned with the face reference.")]
        [SerializeField] Transform faceFit;
        [Tooltip("Select point on Anchor_HarnessGrab_L (follows RearGrab_L).")]
        [SerializeField] XRSimpleInteractable strapLeft;
        [Tooltip("Select point on Anchor_HarnessGrab_R (follows RearGrab_R).")]
        [SerializeField] XRSimpleInteractable strapRight;
        [Tooltip("Anchor_HarnessGrab_L at each harness route sample, in this object's local space. Written by the K1 editor setup.")]
        [SerializeField] Vector3[] routeLeftPath = Array.Empty<Vector3>();
        [Tooltip("Anchor_HarnessGrab_R at each harness route sample, in this object's local space. Written by the K1 editor setup.")]
        [SerializeField] Vector3[] routeRightPath = Array.Empty<Vector3>();
        [Tooltip("Anchor_HarnessGrab_L from folded (first) to Loose_Front (last) at evenly spaced unfold fractions. Written by the K1 editor setup.")]
        [SerializeField] Vector3[] unfoldLeftPath = Array.Empty<Vector3>();
        [Tooltip("Anchor_HarnessGrab_R from folded (first) to Loose_Front (last) at evenly spaced unfold fractions. Written by the K1 editor setup.")]
        [SerializeField] Vector3[] unfoldRightPath = Array.Empty<Vector3>();
        [Tooltip("Render part bounds at Loose_Front in this object's local space, for the unfold clearance check. Written by the K1 editor setup.")]
        [SerializeField] K1MaskExtraction.PartBounds[] looseOutline = Array.Empty<K1MaskExtraction.PartBounds>();

        [Header("Scene links (set by the K1 editor setup)")]
        [Tooltip("Face reference under the XR Origin camera: camera pose plus an adjustable local offset. Anchor_FaceFit is aligned to it.")]
        [SerializeField] Transform faceReference;
        [Tooltip("TrackedPoseDriver of the XR Origin camera; its position and rotation tracking gate all face control.")]
        [SerializeField] TrackedPoseDriver head;

        [Header("Engineering settings (not fit or training criteria)")]
        [Tooltip("Snap starts when this mask's Anchor_FaceFit is within this distance (m) of the face reference and not behind the eyes. " +
                 "No angle condition: the alignment turns the body to the face reference.")]
        [SerializeField] float snapDistance = 0.18f;
        [Tooltip("After an explicit release, snapping is allowed again only once Anchor_FaceFit has been farther than this (m) from the face reference. Must exceed snapDistance.")]
        [SerializeField] float releaseDistance = 0.28f;
        [Tooltip("Seconds to blend from the snap pose (or the pose at head-tracking return) to the face reference, and to open a folded harness " +
                 "to Loose_Front on the snap. Strap input is held meanwhile.")]
        [SerializeField] float alignDuration = 0.3f;
        [Tooltip("Unfolding stops while the Loose_Front bounds come closer than this (m) to the carrier's bounds.")]
        [SerializeField] float unfoldClearance = 0.02f;
        [Tooltip("Unfold progress moves only while the strap hand's target stays within this distance (m) of the strap point's unfold path.")]
        [SerializeField] float unfoldPathTolerance = 0.1f;
        [Tooltip("Largest unfold progress change per frame (0..1 is folded..Loose_Front).")]
        [SerializeField, Range(0.005f, 0.5f)] float unfoldMaxStepPerFrame = 0.05f;
        [Tooltip("A new strap grab needs the tracked controller within this distance (m) of the strap point.")]
        [SerializeField] float strapHandDistance = 0.25f;
        [Tooltip("Strapping zone in the head frame (XR camera: origin between the eyes, +z view direction): the strap hand at least this far (m) " +
                 "behind the eye plane, at any height or side, confirms the strapping. The straps then seat themselves.")]
        [SerializeField] float strapBehindDepth = 0.05f;
        [Tooltip("Route units per second the straps move toward the hand's target (forward only).")]
        [SerializeField] float strapFollowSpeed = 4f;
        [Tooltip("Route units per second for the automatic seat from Behind_Head to Seated after the completion zone.")]
        [SerializeField] float seatSnapSpeed = 1.5f;
        [Tooltip("Route units per second for returning the straps to Loose_Front after release.")]
        [SerializeField] float strapReturnSpeed = 2f;

        bool ready;
        bool subscribed;
        // This object's pose in the face reference's space when Anchor_FaceFit coincides with the face reference.
        Pose faceTargetLocal;
        bool faceControlled;
        Pose alignStartLocal;
        float alignElapsed;
        bool headLost;
        float[] unfoldParameters = Array.Empty<float>();
        float unfold;
        bool foldedCaptured;
        bool unfoldBlockedLogged;
        // The single controlling strap hand: the most recent strap selection.
        XRSimpleInteractable controlStrap;
        IXRSelectInteractor controlHand;
        // Unfolding baseline: the hand (object space) and unfold progress.
        bool strapBaseline;
        Vector3 strapBaselineHand;
        float strapBaselineValue;
        // The strap hand was seen behind the head since the face snap; kept over release, re-grab and tracking loss.
        bool strapSeating;
        // Set by an explicit release; cleared only by a tracked observation outside the face range (UpdateSnapBlock).
        bool snapBlocked;
        bool suspended;
        bool snapRearm;
        bool strapRearm;
        bool initialTrackPosition = true;
        bool initialTrackRotation = true;

        public K1MaskWearState State { get; private set; }
        // True while this component, not XRI, writes the body pose.
        public bool ControlsBody => faceControlled;
        // 0 folded .. 1 Loose_Front.
        public float UnfoldProgress => unfold;
        public float StrapProgress => harness != null ? harness.Progress : 0f;
        public bool IsWorn => State == K1MaskWearState.Worn;
        // True after an explicit release until the body has clearly left the face range.
        public bool IsSnapBlocked => snapBlocked;
        // Set by a training field while it asks whether to stop the course (its confirm dialog). Nothing of the wearing changes:
        // no face snap, unfolding, strapping, seating or strap return, and the activate press does not release the face. A mask
        // on the face keeps following the head; selections are kept and strap baselines restart. Back from it (RearmAfterHold),
        // a pose made meanwhile is never taken as done: a free mask already in snap range snaps only after it has left the face
        // range once (the same block as after an explicit release), and a strap hand already behind the head confirms the
        // strapping only after it has been seen in front of that depth again or after a new strap grab. A snap or a strapping
        // confirmed before the hold stays. Not saved; false by default (a course's own scene never sets it).
        public bool Suspended
        {
            get => suspended;
            set
            {
                if (suspended && !value)
                    RearmAfterHold();
                suspended = value;
            }
        }
        // Field hold return only: the snap waits for the mask to leave the face range; the strapping waits for a new move.
        public bool IsSnapRearming => snapRearm && snapBlocked;
        public bool IsStrapRearming => strapRearm && faceControlled && !strapSeating;
        // On the face, aligning or opening the harness to Loose_Front; strap input is not taken yet (DriveStraps). Read only.
        public bool IsPreparing => faceControlled && (Aligning || unfold < 1f);
        // Snap or strapping is waiting for tracking: no valid head pose, or the controlling strap hand holds its strap untracked.
        // Read only; the states that act on it are the ones with selectable straps.
        public bool IsWaitingForTracking => ready && StrapsSelectable(State)
            && (!IsPoseTracked(head) || (faceControlled && controlStrap != null && controlStrap.isSelected
                                         && !K1CarrierLid.IsTracked(K1CarrierLid.DriverOf(controlHand))));
        // Raised only on actual state changes.
        public event Action<K1MaskWear, K1MaskWearState> StateChanged;
        // Raised once per transition into Worn.
        public event Action<K1MaskWear> Worn;

        public bool canProcess => isActiveAndEnabled;

        bool Aligning => faceControlled && alignElapsed < alignDuration;

        void Awake()
        {
            if (!TryValidate(out var error))
            {
                Debug.LogError(error + " Unfolding and face snap stay disabled.", this);
                return;
            }

            var fitPosition = transform.InverseTransformPoint(faceFit.position);
            var fitRotation = Quaternion.Inverse(transform.rotation) * faceFit.rotation;
            var inverse = Quaternion.Inverse(fitRotation);
            faceTargetLocal = new Pose(-(inverse * fitPosition), inverse);
            unfoldParameters = new float[unfoldLeftPath.Length];
            for (var i = 0; i < unfoldParameters.Length; i++)
                unfoldParameters[i] = (float)i / (unfoldParameters.Length - 1);
            strapLeft.gameObject.SetActive(false);
            strapRight.gameObject.SetActive(false);
            // Free-grab tracking as saved; restored by the new-run reset.
            initialTrackPosition = grab.trackPosition;
            initialTrackRotation = grab.trackRotation;
            ready = true;
        }

        void OnEnable()
        {
            if (!ready || subscribed)
                return;

            grab.activated.AddListener(OnActivated);
            foreach (var strap in new[] { strapLeft, strapRight })
            {
                strap.selectFilters.Add(this);
                strap.selectEntered.AddListener(OnStrapSelected);
                strap.selectExited.AddListener(OnStrapReleased);
            }
            Application.onBeforeRender += ApplyFacePose;
            subscribed = true;
        }

        void OnDisable()
        {
            if (!subscribed)
                return;

            grab.activated.RemoveListener(OnActivated);
            foreach (var strap in new[] { strapLeft, strapRight })
            {
                strap.selectFilters.Remove(this);
                strap.selectEntered.RemoveListener(OnStrapSelected);
                strap.selectExited.RemoveListener(OnStrapReleased);
            }
            Application.onBeforeRender -= ApplyFacePose;
            subscribed = false;
            ClearStrapControl();
            // Never leave the body without a writer: hand it back to XRI.
            if (faceControlled)
                EndFaceControl();
        }

        void Update()
        {
            if (!ready)
                return;
            if (Suspended)
            {
                strapBaseline = false;
                return;
            }

            if (State == K1MaskWearState.Stored && extraction.IsExtracted)
                SetState(K1MaskWearState.Extracted);
            if (faceControlled)
                alignElapsed += Time.deltaTime;
            else if (snapBlocked)
                UpdateSnapBlock();

            switch (State)
            {
                case K1MaskWearState.Extracted:
                case K1MaskWearState.Unfolding:
                    // Optional manual unfold; the face snap does not wait for it.
                    DriveUnfold();
                    TrySnapToFace();
                    break;
                case K1MaskWearState.Ready:
                    // A strap may stay held from unfolding; it only starts moving the route after the face snap.
                    strapBaseline = false;
                    TrySnapToFace();
                    break;
                case K1MaskWearState.FaceHeld:
                case K1MaskWearState.Strapping:
                    if (unfold < 1f)
                        PrepareHarness();
                    else
                        DriveStraps();
                    break;
                case K1MaskWearState.StrapsReturning:
                    harness.ApplyRoute(Mathf.MoveTowards(harness.Progress, 0f, strapReturnSpeed * Time.deltaTime));
                    if (harness.Progress <= 0f)
                        SetState(K1MaskWearState.Ready);
                    break;
            }

            var strapsActive = StrapsSelectable(State);
            if (strapLeft.gameObject.activeSelf != strapsActive)
                strapLeft.gameObject.SetActive(strapsActive);
            if (strapRight.gameObject.activeSelf != strapsActive)
                strapRight.gameObject.SetActive(strapsActive);
        }

        void LateUpdate() => ApplyFacePose();

        // Gates new strap grabs only; a hand already holding a strap keeps it. Unfolding starts only with the body in a hand.
        bool IXRSelectFilter.Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            if (interactor.IsSelecting(interactable))
                return true;
            if (!StrapsSelectable(State))
                return false;
            if ((State == K1MaskWearState.Extracted || State == K1MaskWearState.Unfolding) && !grab.isSelected)
                return false;

            var driver = K1CarrierLid.DriverOf(interactor);
            return K1CarrierLid.IsTracked(driver) && Vector3.Distance(driver.transform.position, interactable.transform.position) <= strapHandDistance;
        }

        public bool TryValidate(out string error)
        {
            var problems = new List<string>();
            if (extraction == null || grab == null || harness == null || faceFit == null || strapLeft == null || strapRight == null)
                problems.Add("prefab references missing");
            else if (!harness.TryValidate(out var harnessError))
                problems.Add(harnessError);
            else if (routeLeftPath == null || routeRightPath == null || routeLeftPath.Length != harness.Samples.Count || routeRightPath.Length != harness.Samples.Count)
                problems.Add("strap route paths do not match the harness samples");
            if (unfoldLeftPath == null || unfoldRightPath == null || unfoldLeftPath.Length < 2 || unfoldRightPath.Length != unfoldLeftPath.Length)
                problems.Add("strap unfold paths missing");
            if (looseOutline == null || looseOutline.Length == 0)
                problems.Add(nameof(looseOutline) + " empty");
            if (faceReference == null || head == null)
                problems.Add("face reference or head not linked");
            if (releaseDistance <= snapDistance)
                problems.Add("release range must be wider than the snap range (releaseDistance > snapDistance)");
            if (snapDistance <= 0f || alignDuration < 0f || unfoldPathTolerance <= 0f || strapHandDistance <= 0f || strapBehindDepth <= 0f
                || strapFollowSpeed <= 0f || seatSnapSpeed <= 0f || strapReturnSpeed <= 0f)
                problems.Add("engineering settings out of range");

            error = problems.Count > 0 ? $"K1MaskWear '{name}': {string.Join(", ", problems)}." : null;
            return error == null;
        }

        // New-run reset (training retry), not the explicit release: ends face control, alignment, preparing, seating and any
        // strap return at once, restores the saved XRI tracking, clears strap hand, baselines, head-loss and the snap block,
        // and puts the harness back in the folded storage pose with unfold and route progress 0. State becomes Stored; no Worn
        // event. The folded pose is the one cached before the first unfold step (never re-captured here); if no unfold step
        // ever ran, the bones are still in it. The body and both strap points must already be released (the caller ends XRI
        // selections first); otherwise nothing changes. The body pose itself is restored by K1MaskExtraction.
        public bool ResetForNewRun(out string error)
        {
            if (!TryValidate(out error))
                return false;
            if (!ready)
            {
                error = $"K1MaskWear '{name}': not initialized.";
                return false;
            }
            if (grab.isSelected || strapLeft.isSelected || strapRight.isSelected)
            {
                error = $"K1MaskWear '{name}': body or strap still selected; end the selections before the reset.";
                return false;
            }

            faceControlled = false;
            grab.trackPosition = initialTrackPosition;
            grab.trackRotation = initialTrackRotation;
            ClearStrapControl();
            strapSeating = false;
            snapBlocked = false;
            snapRearm = false;
            strapRearm = false;
            headLost = false;
            alignElapsed = 0f;
            alignStartLocal = Pose.identity;
            unfoldBlockedLogged = false;
            unfold = 0f;
            if (foldedCaptured)
                harness.ApplyCapturedToRouteStart(0f);
            strapLeft.gameObject.SetActive(false);
            strapRight.gameObject.SetActive(false);
            SetState(K1MaskWearState.Stored);
            Debug.Log("[K1MaskWear] Reset for a new run: harness folded, not on the face.", this);
            return true;
        }

        static bool StrapsSelectable(K1MaskWearState state) =>
            state == K1MaskWearState.Extracted || state == K1MaskWearState.Unfolding || state == K1MaskWearState.Ready
            || state == K1MaskWearState.FaceHeld || state == K1MaskWearState.Strapping;

        void OnActivated(ActivateEventArgs args)
        {
            if (Suspended)
                return;
            switch (State)
            {
                case K1MaskWearState.FaceHeld:
                case K1MaskWearState.Strapping:
                case K1MaskWearState.Worn:
                    ReleaseFromFace();
                    break;
            }
        }

        // The newest strap selection (either point, either hand) takes control and starts from the current pose.
        void OnStrapSelected(SelectEnterEventArgs args)
        {
            // A strap grab after a field hold is a new move: its position counts again, as for any grab.
            strapRearm = false;
            controlStrap = ReferenceEquals(args.interactableObject, strapLeft) ? strapLeft : strapRight;
            controlHand = args.interactorObject;
            strapBaseline = false;
        }

        // When the controlling hand lets go, a hand still holding the other strap point takes over; otherwise nothing drives.
        void OnStrapReleased(SelectExitEventArgs args)
        {
            if (!ReferenceEquals(args.interactableObject, controlStrap) || args.interactorObject != controlHand)
                return;

            var other = controlStrap == strapLeft ? strapRight : strapLeft;
            if (other.isSelected)
            {
                controlStrap = other;
                controlHand = other.firstInteractorSelecting;
            }
            else
            {
                controlStrap = null;
                controlHand = null;
            }
            strapBaseline = false;
        }

        void ClearStrapControl()
        {
            controlStrap = null;
            controlHand = null;
            strapBaseline = false;
        }

        // The controlling hand's world position, if it still holds its strap and is tracked.
        bool TryControlHand(out Vector3 hand)
        {
            hand = default;
            if (controlStrap == null || !controlStrap.isSelected)
                return false;

            var driver = K1CarrierLid.DriverOf(controlHand);
            if (!K1CarrierLid.IsTracked(driver))
                return false;

            hand = driver.transform.position;
            return true;
        }

        // Pull-to-unfold: the held strap point's folded-to-Loose_Front path, driven by the hand's motion relative to the body
        // since the baseline (first tracked frame after a grab, hand change or block). Nothing advances with time alone.
        void DriveUnfold()
        {
            if (!TryControlHand(out var world))
            {
                strapBaseline = false;
                return;
            }
            var hand = transform.InverseTransformPoint(world);
            if (!strapBaseline)
            {
                strapBaselineHand = hand;
                strapBaselineValue = unfold;
                strapBaseline = true;
                return;
            }

            var path = controlStrap == strapLeft ? unfoldLeftPath : unfoldRightPath;
            var target = PathPoint(path, unfoldParameters, strapBaselineValue) + (hand - strapBaselineHand);
            if (!TryNearestOnPath(path, unfoldParameters, target, unfold, unfoldMaxStepPerFrame, unfoldPathTolerance, out var value))
                return;

            if (value > unfold && !LooseOutlineClearOfCarrier())
            {
                // Hold here; re-anchor so the hand motion while blocked is not applied once there is room.
                strapBaseline = false;
                if (!unfoldBlockedLogged)
                    Debug.Log("[K1MaskWear] Unfolding paused: the opened straps would reach into the carrier. Move the mask away.", this);
                unfoldBlockedLogged = true;
                return;
            }

            unfoldBlockedLogged = false;
            SetUnfold(value);
        }

        void SetUnfold(float value)
        {
            ApplyUnfold(value);
            SetState(FreeUnfoldState());
        }

        // Harness pose for an unfold progress; at 1 it is exactly the route start (Loose_Front, route progress 0).
        void ApplyUnfold(float value)
        {
            if (!foldedCaptured)
            {
                // The folded storage pose, captured once before the first unfold step.
                harness.CaptureCurrent();
                foldedCaptured = true;
            }

            unfold = Mathf.Clamp01(value);
            harness.ApplyCapturedToRouteStart(unfold);
            if (unfold >= 1f)
                harness.ApplyRoute(0f);
        }

        K1MaskWearState FreeUnfoldState() =>
            unfold >= 1f ? K1MaskWearState.Ready : unfold > 0f ? K1MaskWearState.Unfolding : K1MaskWearState.Extracted;

        // On the face with a folded or partly opened harness: open it to Loose_Front over alignDuration, in step with the snap
        // alignment. DriveUnfold does not run in the face states, so this is the only bone writer meanwhile, and strap input
        // waits until it is done (no carrier check: the body is on the face).
        void PrepareHarness()
        {
            ApplyUnfold(alignDuration > 0f ? Mathf.MoveTowards(unfold, 1f, Time.deltaTime / alignDuration) : 1f);
        }

        // Conservative: world bounds of the Loose_Front part bounds at the current body pose vs the carrier renderers' bounds.
        bool LooseOutlineClearOfCarrier()
        {
            var carrier = extraction.Carrier;
            if (carrier == null)
                return true;

            var carrierBounds = new Bounds();
            var any = false;
            foreach (var r in carrier.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled)
                    continue;
                if (!any) { carrierBounds = r.bounds; any = true; }
                else carrierBounds.Encapsulate(r.bounds);
            }
            if (!any)
                return true;

            carrierBounds.Expand(2f * unfoldClearance);
            var maskBounds = new Bounds(transform.position, Vector3.zero);
            foreach (var part in looseOutline)
            {
                var b = part.localBounds;
                for (var i = 0; i < 8; i++)
                    maskBounds.Encapsulate(transform.TransformPoint(b.center + Vector3.Scale(b.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f))));
            }
            return !carrierBounds.Intersects(maskBounds);
        }

        // Wide proximity snap from full extraction on (Extracted, Unfolding, Ready), held or not: Anchor_FaceFit within
        // snapDistance of the face reference and not behind the eye plane, with valid head tracking. No angle condition and no
        // hand condition; the alignment brings the body to the face reference pose, and a folded harness opens meanwhile.
        void TrySnapToFace()
        {
            if (snapBlocked || !IsPoseTracked(head))
                return;
            if (Vector3.Distance(faceFit.position, faceReference.position) > snapDistance || ToHeadFrame(faceFit.position).z < 0f)
                return;

            // From here XRI stops writing the body; a selection, if any, is kept. A held strap stays held.
            grab.trackPosition = false;
            grab.trackRotation = false;
            faceControlled = true;
            alignStartLocal = ToFaceLocal(transform.position, transform.rotation);
            alignElapsed = 0f;
            headLost = false;
            strapBaseline = false;
            strapSeating = false;
            SetState(K1MaskWearState.FaceHeld);
        }

        // One strap hand, judged in the tracked head's frame (XR camera: origin between the eyes, +z view direction), never in
        // the mask's axes and not along a path: the hand at least strapBehindDepth behind the eye plane, at any height or side
        // (over the top or straight back past the side), confirms the strapping. Its current position counts as it is, also
        // right after a grab, hand change or tracking return. Before that the straps follow the hand back from the held strap's
        // Loose_Front point toward Behind_Head; after it they seat themselves to Seated and the mask is Worn. The route progress
        // only moves forward, at a per-second speed, and is the visual only. Any lost input (no strap hand, aligning, lost head)
        // pauses it; the confirmation is kept, so a re-grab or tracking return continues the seat without another move.
        void DriveStraps()
        {
            if (!TryControlHand(out var world) || Aligning || headLost || !IsPoseTracked(head))
                return;

            var hand = ToHeadFrame(world);
            if (strapRearm)
            {
                // Back from a field hold with the hand behind the head: wait until it is in front of that depth again.
                if (hand.z > -strapBehindDepth)
                    strapRearm = false;
                else
                    return;
            }
            if (hand.z <= -strapBehindDepth)
                strapSeating = true;

            float target;
            if (strapSeating)
            {
                target = K1MaskHarnessPose.RouteEnd;
            }
            else
            {
                var path = controlStrap == strapLeft ? routeLeftPath : routeRightPath;
                var start = ToHeadFrame(transform.TransformPoint(path[0]));
                target = BehindHead * Mathf.InverseLerp(start.z, -strapBehindDepth, hand.z);
            }

            if (target > harness.Progress)
            {
                var speed = harness.Progress >= BehindHead ? seatSnapSpeed : strapFollowSpeed;
                harness.ApplyRoute(Mathf.MoveTowards(harness.Progress, target, speed * Time.deltaTime));
            }

            // Worn needs the hand seen behind the head and Seated applied, in a frame with the head and the controlling hand
            // tracked and holding its strap (checked above). The other hand and the body grab play no part.
            if (strapSeating && harness.Progress >= K1MaskHarnessPose.RouteEnd)
            {
                ClearStrapControl();
                SetState(K1MaskWearState.Worn);
                Debug.Log("[K1MaskWear] Worn: straps seated behind the head.", this);
                Worn?.Invoke(this);
                return;
            }

            SetState(harness.Progress > 0f ? K1MaskWearState.Strapping : K1MaskWearState.FaceHeld);
        }

        // Head frame from the tracked head pose, rotation and position only (meters, no scale).
        Vector3 ToHeadFrame(Vector3 world) => Quaternion.Inverse(head.transform.rotation) * (world - head.transform.position);

        static Vector3 PathPoint(Vector3[] path, float[] parameters, float value)
        {
            var i = 1;
            while (i < parameters.Length - 1 && parameters[i] < value)
                i++;
            return Vector3.Lerp(path[i - 1], path[i], Mathf.InverseLerp(parameters[i - 1], parameters[i], value));
        }

        // Closest path point to the target within maxStep of the current value; false if it is farther than tolerance.
        static bool TryNearestOnPath(Vector3[] path, float[] parameters, Vector3 target, float current, float maxStep, float tolerance, out float value)
        {
            var lo = Mathf.Max(parameters[0], current - maxStep);
            var hi = Mathf.Min(parameters[parameters.Length - 1], current + maxStep);
            value = current;
            var bestDistance = float.PositiveInfinity;
            for (var i = 0; i < parameters.Length - 1; i++)
            {
                var p0 = parameters[i];
                var p1 = parameters[i + 1];
                if (p1 < lo || p0 > hi)
                    continue;

                var a = path[i];
                var ab = path[i + 1] - a;
                var t = ab.sqrMagnitude > 1e-12f ? Vector3.Dot(target - a, ab) / ab.sqrMagnitude : 0f;
                t = Mathf.Clamp(t, Mathf.InverseLerp(p0, p1, lo), Mathf.InverseLerp(p0, p1, hi));
                var distance = (a + ab * t - target).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    value = Mathf.Lerp(p0, p1, t);
                }
            }
            return bestDistance <= tolerance * tolerance;
        }

        // Explicit release only (Activate on the held body): the next XRI update captures the hand-to-mask offset from this
        // exact pose, so nothing jumps. Strap control is dropped and the straps run back along the route to Loose_Front.
        // Released while the harness was still opening on the face: it stays where it is, in the matching free unfold state.
        void ReleaseFromFace()
        {
            EndFaceControl();
            ClearStrapControl();
            strapSeating = false;
            // The body is still at the face; without this block TrySnapToFace would re-attach it on the next free frame.
            snapBlocked = true;
            Debug.Log("[K1MaskWear] Released from the face. Snap blocked until the mask leaves the face range.", this);
            SetState(unfold < 1f ? FreeUnfoldState() : harness.Progress > 0f ? K1MaskWearState.StrapsReturning : K1MaskWearState.Ready);
        }

        // Clears the post-release block only on a trustworthy observation that Anchor_FaceFit is outside the release range
        // (wider than the snap range, so small jitter at the snap boundary does not count). Needs valid head tracking and,
        // while the body is held, a valid hand pose; lost tracking never clears it. Checked in every free state, including
        // StrapsReturning. Not time based, and releasing or re-grabbing the body does not clear it.
        void UpdateSnapBlock()
        {
            if (!IsPoseTracked(head) || (grab.isSelected && !IsPoseTracked(K1CarrierLid.DriverOf(grab.firstInteractorSelecting))))
                return;
            if (Vector3.Distance(faceFit.position, faceReference.position) <= releaseDistance)
                return;

            snapBlocked = false;
            snapRearm = false;
            Debug.Log("[K1MaskWear] Face snap allowed again: the mask left the face range.", this);
        }

        // Field hold just ended (see Suspended). Free states only for the snap, face states only for the strapping.
        void RearmAfterHold()
        {
            if (!ready)
                return;
            var free = State == K1MaskWearState.Extracted || State == K1MaskWearState.Unfolding || State == K1MaskWearState.Ready;
            if (!faceControlled && free && !snapBlocked && Vector3.Distance(faceFit.position, faceReference.position) <= snapDistance)
            {
                snapBlocked = true;
                snapRearm = true;
                Debug.Log("[K1MaskWear] Back from the field hold with the mask at the face: it snaps after it has left the face range.", this);
            }
            strapRearm = faceControlled && !strapSeating;
        }

        void EndFaceControl()
        {
            faceControlled = false;
            grab.trackPosition = true;
            grab.trackRotation = true;
        }

        // Only writer of the body pose while face controlled. Holds still without valid head tracking; on return it blends
        // from where the body is to the face reference instead of jumping.
        void ApplyFacePose()
        {
            if (!faceControlled)
                return;
            if (!IsPoseTracked(head))
            {
                headLost = true;
                return;
            }
            if (headLost)
            {
                alignStartLocal = ToFaceLocal(transform.position, transform.rotation);
                alignElapsed = 0f;
                headLost = false;
            }

            var s = alignDuration > 0f ? Mathf.SmoothStep(0f, 1f, alignElapsed / alignDuration) : 1f;
            var position = Vector3.Lerp(alignStartLocal.position, faceTargetLocal.position, s);
            var rotation = Quaternion.Slerp(alignStartLocal.rotation, faceTargetLocal.rotation, s);
            transform.SetPositionAndRotation(faceReference.TransformPoint(position), faceReference.rotation * rotation);
        }

        Pose ToFaceLocal(Vector3 position, Quaternion rotation) =>
            new Pose(faceReference.InverseTransformPoint(position), Quaternion.Inverse(faceReference.rotation) * rotation);

        void SetState(K1MaskWearState state)
        {
            if (State == state)
                return;

            State = state;
            Debug.Log($"[K1MaskWear] State: {state}", this);
            StateChanged?.Invoke(this, state);
        }

        // Both position and rotation must be tracked where both drive a pose (head for face control, hand for the snap).
        static bool IsPoseTracked(TrackedPoseDriver driver)
        {
            if (!K1CarrierLid.IsTracked(driver))
                return false;

            var tracking = driver.trackingStateInput.action;
            if (((InputTrackingState)tracking.ReadValue<int>() & InputTrackingState.Rotation) == 0)
                return false;

            var rotation = driver.rotationInput.action;
            return rotation != null && rotation.enabled && rotation.controls.Count > 0;
        }
    }
}
