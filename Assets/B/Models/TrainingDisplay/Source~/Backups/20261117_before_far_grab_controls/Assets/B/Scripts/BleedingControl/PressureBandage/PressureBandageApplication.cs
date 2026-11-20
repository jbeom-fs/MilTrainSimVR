using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MilTrainSimVR.B.BleedingControl.PressureBandage
{
    // Device operation stage of one bandage application (current state, not a training step or history).
    public enum PressureBandageStage
    {
        // No wrap yet: grab the roll and bring it into the wrap zone.
        Ready = 0,
        // Wrapping: the roll held in the wrap zone adds time; WrapProgress below 1.
        Wrapping = 1,
        // Wrap completed, the roll detached; the bandage end must be brought near its mark (it attaches by distance).
        EndPlacement = 2,
        // End laid; the tape must be applied over it.
        Taping = 3,
        // Taped. The application stays without any hand; its parts are locked (no grab, no release in this chapter).
        Fixed = 4
    }

    // First missing condition for the current stage to proceed right now. No dressing grab is needed: the dressing only has to
    // cover the wound validly.
    public enum PressureWrapBlock
    {
        None = 0,
        FocusLost = 1,
        DressingNotPlaced = 2,
        RollNotHeld = 5,
        RollHandNotTracked = 7,
        // The roll centre is not in the wrap zone (or lies inside the arm, dressing or bandage layers).
        OutOfZone = 8,
        // A training course does not allow a new wrap yet (WrapStartLocked; only before the wrap has started).
        StartLocked = 9
    }

    // Last input that was not counted or not accepted, kept with its time for short notices.
    public enum PressureWrapIssue
    {
        None = 0,
        // The bandage end was released farther than the attach distance from its mark (the distance is the only end check).
        EndRejected = 6,
        // The tape was released away from its mark (beyond tapeSnapRadius).
        TapeRejected = 7,
        // End or tape released while input was not focused or the dressing did not cover the wound validly.
        PlacementRequired = 8,
        // The dressing left the wound: the wrap, end and tape of this application were cancelled.
        DressingDisplaced = 9,
        // The dressing slid or turned under the wrap beyond the limits: the wrap, end and tape were cancelled.
        DressingShifted = 11
    }

    // Distance check of the tape against its mark (like the bandage end: distance only, no face, tilt or direction).
    public enum PressureFixCheck
    {
        None = 0,
        Away = 1,
        Valid = 5
    }

    // Pressure bandage chapter 2: roll handling, hold-to-wrap wrapping, end placement, taping and the equipment reset; the only owner
    // of the bandage application state. The dressing state stays owned by PressureDressing (read here; its contact time is never
    // counted here). Device handling only: no tension, pressure, blood flow or bleeding control is judged.
    //
    // Hands: either hand may hold the dressing or work the roll / end / tape. Select filters added to the four grabs allow one
    // kit part per hand, near grab only (tracked controller within grabReach of the part's grab collider), and never refuse a
    // selection in progress (XRI re-checks filters while selected), so they never drop a part. Released parts stay where they
    // are (kinematic, no gravity, no throw). From the wrap to the tape the dressing need not be held: it only has to cover the
    // wound validly, so it can be let go and both hands are free for the roll, end and tape.
    //
    // Wrapping (hold to wrap, LateUpdate after PressureDressing): this component alone owns the wrap time and progress. Progress
    // is added only while input is focused, the dressing placement is valid, the roll is selected by a tracked hand, its centre
    // (Anchor_RollAxis) is in the wrap zone of the path (PressureBandageWrapPath.InWrapZone) and, before the start, a training
    // course does not lock the start (WrapStartLocked). The first such frame starts the wrap; each later one adds its frame
    // time, at most MaxCountedStep, over wrapSeconds. The first valid frame after any pause (zone left, roll released, re-grab or
    // hand-over, tracking or focus lost) adds nothing, so a pause is never counted and a return never completes at once; the
    // reached progress is kept through the pause. The trainee never moves the roll round the arm: the wrapped band grows along
    // the path by the same progress (WrappedAngle), drawn by PressureBandageRibbons; the held roll is never moved here.
    //
    // Interruptions: the pauses above stop the progress and keep it; C (dressing placement invalid for placementLossSeconds, or
    // the dressing slid / turned under the wrap beyond the limits) cancels the wrap, end and tape of this application (the next
    // attempt starts at 0), so the bandage part can be redone without restarting anything else. Smaller dressing moves rebuild
    // the wrap profile so cloth, marks and clearance checks follow it (FollowDressing).
    //
    // End: at full progress the roll is detached (its selection cancelled, hidden) and the bandage end appears where the strip
    // met it. It attaches by distance only (UpdateEndSnap, this LateUpdate): while it is selected by a tracked hand, input is
    // focused and the dressing placement is valid, its fix point (Anchor_EndFix) within endSnapRadius of its mark (EndMark)
    // attaches it at once, without a release. Face, tilt and direction are never compared; the attach seats it in the laid
    // pose. The selection is ended as a cancel (XRI's normal cancel path), so OnEndExited records no release and nothing is
    // confirmed twice; a normal release of a tracked hand uses the same distance check (one frame where it was let go in range);
    // an untracked or cancelled release is never an attach input. A freshly made or untouched end never attaches by itself.
    // Dressing under the finished wrap: on the frame the wrap completes (EndPlacement) the dressing's pose, valid then (the wrap
    // only counts with a valid placement), is fixed in the wound's frame. A hand still holding it lets go as a cancel (no release,
    // so the dressing never seats or moves from it), it cannot be grabbed again, and every frame it is kept at that pose (whatever
    // XRI's drop or a later update did), so the hands are free for the end and the tape and the placement stays valid. The fixing
    // belongs to the application state: cleared with it (cancel, equipment reset), kept through a training field hold.
    // Tape (Taping only, the same rule as the end): while it is selected by a tracked hand, input is focused and the dressing
    // placement is valid, its contact centre (Anchor_TapeContact) within tapeSnapRadius of its mark attaches it at once, seated
    // in the laid pose (face, tilt and direction are set by the attach, never compared); the selection is ended as a cancel. A
    // normal release of a tracked hand uses the same distance check. Taping fixes the application: a dressing selection (if
    // any) is ended and no part can be grabbed again until the equipment reset.
    [DefaultExecutionOrder(102)]
    [DisallowMultipleComponent]
    public sealed class PressureBandageApplication : MonoBehaviour
    {
        enum Part
        {
            Dressing,
            Roll,
            End,
            Tape
        }

        struct PartPose
        {
            public Transform transform;
            public Transform parent;
            public Vector3 position;
            public Quaternion rotation;
            public bool active;
            public Rigidbody body;
        }

        sealed class PartFilter : IXRSelectFilter
        {
            readonly PressureBandageApplication owner;
            readonly Part part;

            public PartFilter(PressureBandageApplication owner, Part part)
            {
                this.owner = owner;
                this.part = part;
            }

            public bool canProcess => owner != null && owner.isActiveAndEnabled;

            public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable) => owner.AllowSelect(part, interactor, interactable);
        }

        [SerializeField] PressureDressing dressing;
        [SerializeField] PressureBandageKit kit;
        [SerializeField] PressureBandageRibbons ribbons;
        [SerializeField] XRGrabInteractable rollGrab;
        [SerializeField] Collider rollGrabCollider;
        [SerializeField] XRGrabInteractable endGrab;
        [SerializeField] Collider endGrabCollider;
        [SerializeField] XRGrabInteractable tapeGrab;
        [SerializeField] Collider tapeGrabCollider;
        [Tooltip("Wrap path of this scene (a child of the designated wound judge frame).")]
        [SerializeField] PressureBandageWrapPath path;
        [Header("VR handling values (development, not medical criteria)")]
        [Tooltip("Largest distance (m) from the controller to a part's grab collider to start a grab.")]
        [SerializeField, Min(0.01f)] float grabReach = 0.1f;
        [Tooltip("Dressing placement invalid for this long (s) cancels the wrap, end and tape of this application.")]
        [SerializeField, Min(0f)] float placementLossSeconds = 0.3f;
        [Tooltip("Attach distance (m): the end's fix point (Anchor_EndFix) within this distance of its mark attaches the held end. " +
                 "Distance only; face, tilt and direction are set by the attach.")]
        [SerializeField, Min(0.005f)] float endSnapRadius = 0.08f;
        [Tooltip("Attach distance (m): the tape's contact centre (Anchor_TapeContact) within this distance of its mark attaches the " +
                 "held tape. Distance only; face, tilt and direction are set by the attach. Default 0.08 m, the end's attach distance at " +
                 "the same mark, so both parts of the fixing behave alike.")]
        [SerializeField, Min(0.005f)] float tapeSnapRadius = 0.08f;
        [Tooltip("Time (s) the roll must be held in the wrap zone for the whole wrap: a development handling value, separate from " +
                 "the dressing's contact time, not a taught wrapping time. Finite and above zero.")]
        [SerializeField] float wrapSeconds = 5f;
        [Tooltip("Height (m) of the seated end above the bandage surface (the tape lies twice as high).")]
        [SerializeField, Min(0f)] float surfaceLift = 0.0008f;
        [Tooltip("Margin (m) between the roll body (model radius and width) and the outer boundary (wrap profile plus layers).")]
        [SerializeField, Min(0f)] float rollMargin = 0.003f;
        [Tooltip("Slide (m) of the dressing along the skin under the wrap, from its pose when the wrap started, beyond which the " +
                 "application is cancelled.")]
        [SerializeField, Min(0.001f)] float dressingSlideLimit = 0.02f;
        [Tooltip("Turn (deg) of the dressing about the skin normal under the wrap, from its pose when the wrap started, beyond " +
                 "which the application is cancelled.")]
        [SerializeField, Range(1f, 90f)] float dressingTwistLimit = 20f;
        [Tooltip("Change (m) of the wrap profile from the dressing's current pose that rebuilds the bandage geometry.")]
        [SerializeField, Min(0.0005f)] float profileTolerance = 0.002f;

        // Largest frame time counted at once (s), so a long frame never jumps the progress.
        const float MaxCountedStep = 0.1f;

        readonly List<PartPose> initialPoses = new List<PartPose>();
        readonly Dictionary<IXRSelectInteractor, TrackedPoseDriver> drivers = new Dictionary<IXRSelectInteractor, TrackedPoseDriver>();
        readonly List<Vector3> corners = new List<Vector3>(8);
        PartFilter dressingFilter;
        PartFilter rollFilter;
        PartFilter endFilter;
        PartFilter tapeFilter;
        bool subscribed;
        bool captured;
        bool resetting;
        // Wrap progress of this application (0..1) and whether the last frame was a valid hold (the next one may add time).
        float wrapAmount;
        bool counting;
        IXRSelectInteractor lastRollHolder;
        float placementInvalidSince = -1f;
        bool pendingEndRelease;
        bool pendingTapeRelease;
        float lengthSign = 1f;
        float endLength;
        float endFixDistance;
        float rollRadius;
        float rollHalfWidth;
        Bounds dressingBounds;
        // Dressing pose in the wound frame when the wrap started (slide and turn reference).
        Vector3 referenceContact;
        Vector3 referenceSide;

        public PressureDressing Dressing => dressing;
        public PressureBandageKit Kit => kit;
        public PressureBandageWrapPath Path => path;
        public XRGrabInteractable RollGrab => rollGrab;
        public XRGrabInteractable EndGrab => endGrab;
        public XRGrabInteractable TapeGrab => tapeGrab;
        public float SurfaceLift => surfaceLift;
        // End part: its length axis is local X times this sign (from the model anchors), its outer face local +Z.
        public float EndLengthSign => lengthSign;
        public float EndLength => endLength;
        public float EndFixDistance => endFixDistance;
        public float RollRadius => rollRadius;
        public float RollHalfWidth => rollHalfWidth;
        public float RollMargin => rollMargin;
        public float WrapSeconds => wrapSeconds;
        // The roll's bounding sphere plus margin: a roll centre this far outside the outer boundary clears it in any orientation.
        public static float RollBoundRadius(float radius, float halfWidth, float margin) => Mathf.Sqrt(radius * radius + halfWidth * halfWidth) + margin;

        // Current state.
        bool suspended;
        float suspendedAt;
        // Dressing fixed under the finished wrap (see the class notes): its pose in the wound's frame.
        bool dressingFixed;
        Vector3 fixedDressingPosition;
        Quaternion fixedDressingRotation = Quaternion.identity;
        bool endRearm;
        bool tapeRearm;

        public PressureBandageStage Stage { get; private set; }
        public PressureWrapBlock Block { get; private set; }
        // Roll held and its centre in the wrap zone now (Ready and Wrapping; false otherwise).
        public bool IsRollInZone { get; private set; }
        // Set by a training course while its order does not allow a new wrap (for a roll already in hand, whose selection the
        // course lock lets pass). It only holds back the start; a wrap under way is not affected. Without a course it stays false.
        public bool WrapStartLocked { get; set; }
        // Set by a training field while it asks whether to stop the course (its confirm dialog), together with the dressing's
        // InputSuspended. Parts in hand stay in hand; nothing of the application changes: no wrap time (the next valid frame after
        // the dialog adds nothing), no end or tape attach, no placement watch or cancel, and a release meanwhile is never a
        // confirm input. Back from it, an end that is already within its attach distance is re-armed: it attaches (held or by a
        // release) only after its fix point has been seen beyond that distance once, so a position made during the dialog is
        // never an attach. Not saved; false by default (a course's own scene never sets it).
        public bool Suspended
        {
            get => suspended;
            set
            {
                if (!suspended && value)
                    suspendedAt = Time.time;
                if (suspended && !value)
                {
                    // The placement watch never counts the dialog's time.
                    if (placementInvalidSince >= 0f)
                        placementInvalidSince += Time.time - suspendedAt;
                    if (Stage == PressureBandageStage.EndPlacement && WithinEndSnap(kit.AnchorTransforms.endFix.position, EndMarkPoint()))
                        endRearm = true;
                    if (Stage == PressureBandageStage.Taping && TapeDistance() <= tapeSnapRadius)
                        tapeRearm = true;
                }
                suspended = value;
            }
        }
        // The dressing is fixed under the finished wrap: it stays at its pose and cannot be grabbed (EndPlacement until the reset).
        public bool IsDressingFixed => dressingFixed;
        // The end / tape waits to leave its attach distance once (field hold return only; see Suspended).
        public bool IsEndRearming => endRearm;
        public bool IsTapeRearming => tapeRearm;
        public float TapeSnapRadius => tapeSnapRadius;
        public float EndSnapRadius => endSnapRadius;
        // EndPlacement: distance (m) of the end's fix point from its mark now, and whether it is within the attach distance
        // (display; the attach itself also needs the held end, its tracked hand, focus and the placement). -1 / false otherwise.
        public float EndDistance { get; private set; } = -1f;
        public bool IsEndInSnapRange => EndDistance >= 0f && EndDistance <= endSnapRadius;
        public PressureFixCheck TapeCheck { get; private set; }
        public bool IsRollHeld => rollGrab != null && rollGrab.isSelected;
        public bool IsEndHeld => endGrab != null && endGrab.isSelected;
        public bool IsTapeHeld => tapeGrab != null && tapeGrab.isSelected;
        public bool IsRollDetached => Stage >= PressureBandageStage.EndPlacement;
        public InteractorHandedness RollHand { get; private set; }
        public PressureHandTracking RollHandTracking { get; private set; }
        // Tracking of the hand selecting the end / tape right now (read live, Missing when not selected): the same judgement as
        // the tracked release that the end placement and the tape fixing require. Display only; nothing here changes the parts.
        public PressureHandTracking EndHandTracking => SelectingHandTracking(endGrab);
        public PressureHandTracking TapeHandTracking => SelectingHandTracking(tapeGrab);
        // Wrap progress (0..1): hold time in the zone over wrapSeconds, 1 once the wrap is complete. The one value behind the
        // wrapped band, the loose strip start, the end position and every progress display.
        public float WrapProgress => Stage >= PressureBandageStage.EndPlacement ? 1f : wrapAmount;
        // Path angle reached by the wrap (WrapProgress of the path's full angle).
        public float WrappedAngle => path != null ? WrapProgress * path.AngleOf(path.LastWaypoint) : 0f;
        public PressureWrapIssue LastIssue { get; private set; }
        // Time.time of LastIssue; negative when none.
        public float LastIssueTime { get; private set; } = -1f;
        // Raised when applied geometry changes (stage, wrap progress, end or tape seated, cancel, reset); views rebuild on change.
        public int Version { get; private set; }
        public int ResetCount { get; private set; }
        // Applications cancelled so far (dressing off the wound, or slid / turned under the wrap). With ResetCount it names the
        // current application attempt, so checks tied to an attempt can be withdrawn when it is cancelled; display-only geometry
        // changes (Version) do not change it.
        public int CancelCount { get; private set; }
        public bool IsResetting => resetting;
        public bool LastResetSucceeded { get; private set; } = true;
        // Roll body clear of the outer boundary in its current pose (Ready and Wrapping; false otherwise). Display only.
        public bool IsRollBodyClear { get; private set; }

        // Loose strip connection of this frame (Wrapping: to the roll exit; EndPlacement: to the loose end), computed once here
        // and drawn by PressureBandageRibbons as is: it leaves the surface at LooseTangentAngle and runs straight to LooseFarPoint.
        // LooseValid: the roll body is clear (Wrapping), the far point is visible from the tangent point and the straight run is
        // clear of the wrap profile. Display only (an invalid connection is never drawn); the wrap progress does not use it.
        public bool LooseValid { get; private set; }
        public float LooseStartAngle { get; private set; }
        public float LooseTangentAngle { get; private set; }
        public Vector3 LooseFarPoint { get; private set; }
        public Vector3 LooseFarWidth { get; private set; }

        public Vector3 RollCentre => kit.AnchorTransforms.rollAxis.position;
        public Vector3 RollAxisDirection => kit.PartTransforms.bandageRoll.up;

        void Awake() => Capture();

        void Start()
        {
            if (!TryValidate(out var error))
            {
                Debug.LogError("[PressureBandage] " + error, this);
                enabled = false;
            }
        }

        void OnEnable()
        {
            if (!subscribed && rollGrab != null && endGrab != null && tapeGrab != null && dressing != null && dressing.Grab != null)
            {
                dressingFilter ??= new PartFilter(this, Part.Dressing);
                rollFilter ??= new PartFilter(this, Part.Roll);
                endFilter ??= new PartFilter(this, Part.End);
                tapeFilter ??= new PartFilter(this, Part.Tape);
                dressing.Grab.selectFilters.Add(dressingFilter);
                rollGrab.selectFilters.Add(rollFilter);
                endGrab.selectFilters.Add(endFilter);
                tapeGrab.selectFilters.Add(tapeFilter);
                rollGrab.selectExited.AddListener(OnRollExited);
                endGrab.selectEntered.AddListener(OnEndEntered);
                endGrab.selectExited.AddListener(OnEndExited);
                tapeGrab.selectEntered.AddListener(OnTapeEntered);
                tapeGrab.selectExited.AddListener(OnTapeExited);
                subscribed = true;
            }
            counting = false;
        }

        void OnDisable()
        {
            if (subscribed)
            {
                if (dressing != null && dressing.Grab != null)
                    dressing.Grab.selectFilters.Remove(dressingFilter);
                if (rollGrab != null)
                {
                    rollGrab.selectFilters.Remove(rollFilter);
                    rollGrab.selectExited.RemoveListener(OnRollExited);
                }
                if (endGrab != null)
                {
                    endGrab.selectFilters.Remove(endFilter);
                    endGrab.selectEntered.RemoveListener(OnEndEntered);
                    endGrab.selectExited.RemoveListener(OnEndExited);
                }
                if (tapeGrab != null)
                {
                    tapeGrab.selectFilters.Remove(tapeFilter);
                    tapeGrab.selectEntered.RemoveListener(OnTapeEntered);
                    tapeGrab.selectExited.RemoveListener(OnTapeExited);
                }
            }
            subscribed = false;
            // Selections are left to XRI; only derived input state is dropped (the reached progress is kept).
            counting = false;
            pendingEndRelease = false;
            pendingTapeRelease = false;
            drivers.Clear();
        }

        // The user's initial tray layout (scene as saved), visibility and model dimensions, once, before any interaction.
        void Capture()
        {
            if (captured || kit == null || !kit.TryValidate(out _))
                return;
            var p = kit.PartTransforms;
            initialPoses.Clear();
            foreach (var t in new[] { p.dressingPad, p.bandageRoll, p.bandageEnd, p.fixingTape, p.bandageStrip, p.wrapStrip })
            {
                t.TryGetComponent<Rigidbody>(out var body);
                initialPoses.Add(new PartPose
                {
                    transform = t, parent = t.parent, position = t.localPosition, rotation = t.localRotation, active = t.gameObject.activeSelf, body = body
                });
            }

            var a = kit.AnchorTransforms;
            lengthSign = a.endFix.localPosition.x < 0f ? -1f : 1f;
            endFixDistance = Mathf.Abs(a.endFix.localPosition.x);
            endLength = p.bandageEnd.TryGetComponent<MeshFilter>(out var endFilterMesh) && endFilterMesh.sharedMesh != null
                ? endFilterMesh.sharedMesh.bounds.size.x
                : 0f;
            rollRadius = (a.rollExit.localPosition - a.rollAxis.localPosition).magnitude;
            // Roll width along its local Y (rotation axis), from the model mesh bounds.
            rollHalfWidth = p.bandageRoll.TryGetComponent<MeshFilter>(out var rollMesh) && rollMesh.sharedMesh != null
                ? rollMesh.sharedMesh.bounds.extents.y
                : 0f;
            dressingBounds = p.dressingPad.TryGetComponent<MeshFilter>(out var padFilter) && padFilter.sharedMesh != null
                ? padFilter.sharedMesh.bounds
                : new Bounds(Vector3.zero, Vector3.zero);
            captured = true;
        }

        // ---------- Input ----------

        bool AllowSelect(Part part, IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            if (interactor == null)
                return false;
            // A selection in progress always passes: these filters never end a grab.
            if (interactor.IsSelecting(interactable))
                return true;
            if (resetting || SelectsOtherPart(interactor, part))
                return false;
            switch (part)
            {
                case Part.Dressing:
                    // Reach and tracking are PressureDressing's own filter; once the wrap is complete the dressing stays under it.
                    return !dressingFixed && Stage != PressureBandageStage.Fixed;
                case Part.Roll:
                    return Stage <= PressureBandageStage.Wrapping && NearGrab(interactor, rollGrabCollider);
                case Part.End:
                    return Stage == PressureBandageStage.EndPlacement && NearGrab(interactor, endGrabCollider);
                default:
                    return Stage != PressureBandageStage.Fixed && NearGrab(interactor, tapeGrabCollider);
            }
        }

        // One hand, one kit part: a hand holding the dressing cannot take the roll, end or tape, and the other way round.
        bool SelectsOtherPart(IXRSelectInteractor interactor, Part part) =>
            (part != Part.Dressing && interactor.IsSelecting(dressing.Grab)) ||
            (part != Part.Roll && interactor.IsSelecting(rollGrab)) ||
            (part != Part.End && interactor.IsSelecting(endGrab)) ||
            (part != Part.Tape && interactor.IsSelecting(tapeGrab));

        bool NearGrab(IXRSelectInteractor interactor, Collider grabCollider)
        {
            var driver = DriverOf(interactor);
            if (PressureBandageTracking.Of(driver) != PressureHandTracking.Tracked || grabCollider == null || !grabCollider.enabled ||
                !grabCollider.gameObject.activeInHierarchy)
                return false;
            var hand = driver.transform.position;
            return Vector3.Distance(grabCollider.ClosestPoint(hand), hand) <= grabReach;
        }

        void OnRollExited(SelectExitEventArgs args)
        {
            if (!resetting)
                counting = false;
        }

        void OnEndEntered(SelectEnterEventArgs args) => pendingEndRelease = false;
        void OnTapeEntered(SelectEnterEventArgs args) => pendingTapeRelease = false;

        // Confirmed later this frame only for a normal release of a tracked hand; cancels (reset, detach, disable) never count.
        void OnEndExited(SelectExitEventArgs args) => pendingEndRelease = !resetting && !Suspended && IsTrackedRelease(args);
        void OnTapeExited(SelectExitEventArgs args) => pendingTapeRelease = !resetting && !Suspended && IsTrackedRelease(args);

        bool IsTrackedRelease(SelectExitEventArgs args) =>
            !args.isCanceled && PressureBandageTracking.Of(DriverOf(args.interactorObject)) == PressureHandTracking.Tracked;

        PressureHandTracking SelectingHandTracking(XRGrabInteractable grab) =>
            grab != null && grab.isSelected ? PressureBandageTracking.Of(DriverOf(grab.firstInteractorSelecting)) : PressureHandTracking.Missing;

        TrackedPoseDriver DriverOf(IXRSelectInteractor interactor)
        {
            if (interactor == null || interactor.transform == null)
                return null;
            if (!drivers.TryGetValue(interactor, out var driver) || driver == null)
            {
                driver = interactor.transform.GetComponentInParent<TrackedPoseDriver>();
                drivers[interactor] = driver;
            }
            return driver;
        }

        // ---------- Per frame ----------

        void LateUpdate()
        {
            if (resetting)
                return;
            var focused = dressing.InputFocused;
            var rollHolder = rollGrab.isSelected ? rollGrab.firstInteractorSelecting : null;
            RollHand = rollHolder != null ? rollHolder.handedness : InteractorHandedness.None;
            RollHandTracking = rollHolder != null ? PressureBandageTracking.Of(DriverOf(rollHolder)) : PressureHandTracking.Missing;
            IsRollInZone = rollHolder != null && Stage <= PressureBandageStage.Wrapping && path.InWrapZone(RollCentre);
            Block = CurrentBlock(focused, rollHolder);
            KeepFixedDressing();
            if (endRearm && (Stage != PressureBandageStage.EndPlacement || EndMarkDistance() > endSnapRadius))
                endRearm = false;
            if (tapeRearm && (Stage != PressureBandageStage.Taping || TapeDistance() > tapeSnapRadius))
                tapeRearm = false;
            if (Suspended)
            {
                // The displays keep this frame's read-only state; nothing is judged or counted.
                pendingEndRelease = false;
                pendingTapeRelease = false;
                counting = false;
                return;
            }

            if (!WatchPlacement() && !FollowDressing())
            {
                HandleReleases(focused);
                UpdateWrap(rollHolder);
                UpdateEndSnap(focused);
                UpdateTapeSnap(focused);
            }
            // After the progress of this frame, so the strip starts where the wrapped band ends.
            UpdateConnection();
            EndDistance = Stage == PressureBandageStage.EndPlacement && kit.PartTransforms.bandageEnd.gameObject.activeInHierarchy ? EndMarkDistance() : -1f;
            TapeCheck = Stage == PressureBandageStage.Taping ? JudgeTape() : PressureFixCheck.None;
        }

        // No dressing grab or dressing hand tracking here: only the placement counts for the dressing.
        PressureWrapBlock CurrentBlock(bool focused, IXRSelectInteractor rollHolder)
        {
            if (Stage == PressureBandageStage.Fixed)
                return PressureWrapBlock.None;
            if (!focused)
                return PressureWrapBlock.FocusLost;
            if (!dressing.IsPlacementValid)
                return PressureWrapBlock.DressingNotPlaced;
            if (Stage > PressureBandageStage.Wrapping)
                return PressureWrapBlock.None;
            if (Stage == PressureBandageStage.Ready && WrapStartLocked)
                return PressureWrapBlock.StartLocked;
            if (rollHolder == null)
                return PressureWrapBlock.RollNotHeld;
            if (RollHandTracking != PressureHandTracking.Tracked)
                return PressureWrapBlock.RollHandNotTracked;
            return IsRollInZone ? PressureWrapBlock.None : PressureWrapBlock.OutOfZone;
        }

        // C: the dressing no longer covers the wound for placementLossSeconds -> this application cannot be trusted. A short
        // invalid moment (hand jitter) only pauses the operation through the placement condition.
        bool WatchPlacement()
        {
            if (Stage == PressureBandageStage.Ready || dressing.IsPlacementValid)
            {
                placementInvalidSince = -1f;
                return false;
            }
            if (placementInvalidSince < 0f)
                placementInvalidSince = Time.time;
            if (Time.time - placementInvalidSince < placementLossSeconds)
                return false;
            CancelApplication(PressureWrapIssue.DressingDisplaced);
            return true;
        }

        // Wrap geometry follows the dressing as it is now (development policy):
        // - Ready: the wrap profile includes the placed dressing (plain profile when it is not placed), so the start gate and
        //   the roll clearance already account for it.
        // - Wrapping, end placement, taping: a slide along the skin or a turn about the skin normal from the dressing's pose when
        //   the wrap started beyond dressingSlideLimit / dressingTwistLimit means the wrapped bandage could not have held it: the
        //   application is cancelled (redo the bandage part). Smaller moves within the placement tolerance rebuild the profile
        //   when it changes by more than profileTolerance; Version rises so the wrap, strip, laid end, marks, gates and the roll
        //   clearance all use the new geometry. Lifting and pressing are left to the placement check.
        // - A placement invalid for a moment in a later stage keeps the geometry (WatchPlacement decides). Fixed: the dressing
        //   cannot be grabbed, nothing to follow.
        // Returns true when the application was cancelled this frame.
        bool FollowDressing()
        {
            if (Stage == PressureBandageStage.Fixed)
                return false;
            if (!dressing.IsPlacementValid)
            {
                if (Stage == PressureBandageStage.Ready && path.IsWidenedForDressing)
                {
                    path.PrepareProfile(null);
                    Version++;
                }
                return false;
            }
            if (Stage != PressureBandageStage.Ready && DressingShiftedUnderWrap())
            {
                CancelApplication(PressureWrapIssue.DressingShifted);
                return true;
            }
            DressingCorners(corners);
            if (path.UpdateProfile(corners, profileTolerance))
            {
                if (Stage == PressureBandageStage.Taping)
                    SeatEnd();
                Version++;
            }
            return false;
        }

        void CaptureDressingReference()
        {
            var w = dressing.Wound.transform;
            referenceContact = w.InverseTransformPoint(dressing.ContactAnchor.position);
            referenceSide = w.InverseTransformDirection(kit.PartTransforms.dressingPad.right);
        }

        // Slide along the skin plane and turn about the skin normal (wound frame, local up = normal) since the wrap started.
        bool DressingShiftedUnderWrap()
        {
            var w = dressing.Wound.transform;
            var slide = w.InverseTransformPoint(dressing.ContactAnchor.position) - referenceContact;
            slide.y = 0f;
            if (slide.magnitude > dressingSlideLimit)
                return true;
            var before = new Vector3(referenceSide.x, 0f, referenceSide.z);
            var now = w.InverseTransformDirection(kit.PartTransforms.dressingPad.right);
            now.y = 0f;
            // Side axis near the normal: a tilt, which the placement check covers.
            if (before.magnitude < 0.3f || now.magnitude < 0.3f)
                return false;
            return Vector3.Angle(before, now) > dressingTwistLimit;
        }

        // Loose strip connection for this frame (see LooseValid). Wrapping: the tangent point is found toward the roll centre,
        // then twice toward the exit it gives (RollExitToward), ending with a tangent toward the final exit, so the drawn
        // straight run is the one checked.
        void UpdateConnection()
        {
            LooseValid = false;
            var roll = kit.PartTransforms.bandageRoll;
            IsRollBodyClear = Stage <= PressureBandageStage.Wrapping && roll.gameObject.activeInHierarchy &&
                              path.RollClear(RollCentre, RollAxisDirection, rollRadius, rollHalfWidth, rollMargin);
            if (Stage != PressureBandageStage.Wrapping && Stage != PressureBandageStage.EndPlacement)
                return;

            var theta0 = WrappedAngle;
            var lift = ribbons.StripLift;
            var axial = path.AxialAt(theta0);
            bool found;
            float thetaT;
            Vector3 far;
            Vector3 width;
            if (Stage == PressureBandageStage.Wrapping)
            {
                found = path.TryTangent(theta0, RollCentre, lift, out thetaT);
                far = RollExitToward(path.FreePoint(theta0, thetaT, axial, lift));
                for (var i = 0; i < 2 && found; i++)
                {
                    found = path.TryTangent(theta0, far, lift, out thetaT);
                    if (i == 0 && found)
                        far = RollExitToward(path.FreePoint(theta0, thetaT, axial, lift));
                }
                width = RollAxisDirection;
                found &= IsRollBodyClear;
            }
            else
            {
                var end = kit.PartTransforms.bandageEnd;
                far = end.position;
                width = EndWidth(end);
                found = path.TryTangent(theta0, far, lift, out thetaT);
            }

            LooseStartAngle = theta0;
            LooseTangentAngle = thetaT;
            LooseFarPoint = far;
            LooseFarWidth = width;
            LooseValid = found && path.FreeVisible(theta0, thetaT, far, axial, lift) &&
                         path.SegmentClearOfWrap(path.FreePoint(theta0, thetaT, axial, lift), far, 0.0005f);
        }

        // Hold to wrap (see the class notes). Nothing about the dressing hand: letting the dressing go changes nothing here.
        void UpdateWrap(IXRSelectInteractor rollHolder)
        {
            if (rollHolder != lastRollHolder)
            {
                // Release, re-grab or hand-over of the roll: the new hold carries no time of the earlier one.
                counting = false;
                lastRollHolder = rollHolder;
            }
            if (Stage > PressureBandageStage.Wrapping || Block != PressureWrapBlock.None)
            {
                counting = false;
                return;
            }
            if (Stage == PressureBandageStage.Ready)
                Begin();
            if (!counting)
            {
                // First valid frame of a hold (start, return from a pause, new grab): it adds nothing.
                counting = true;
                return;
            }
            wrapAmount = Mathf.Min(1f, wrapAmount + Mathf.Min(Time.deltaTime, MaxCountedStep) / wrapSeconds);
            Version++;
            if (wrapAmount >= 1f)
                CompleteWrap();
        }

        void Begin()
        {
            DressingCorners(corners);
            path.PrepareProfile(corners);
            CaptureDressingReference();
            Stage = PressureBandageStage.Wrapping;
            wrapAmount = 0f;
            counting = false;
            Version++;
        }

        // Wrap complete (full progress, once): the roll is detached (selection cancelled, hidden) and the bandage end appears
        // where the strip met the roll, its length along the strip, outer face outward. The strip from the wrap end follows it.
        void CompleteWrap()
        {
            var theta = path.AngleOf(path.LastWaypoint);
            path.FrameAt(theta, 0f, out var wrapEnd, out var tangent, out var normal);
            var exit = RollExitToward(wrapEnd);
            var along = exit - wrapEnd;
            var direction = along.sqrMagnitude > 1e-4f ? along.normalized : tangent;
            var face = Vector3.ProjectOnPlane(normal, direction);
            if (face.sqrMagnitude < 1e-6f)
                face = Vector3.ProjectOnPlane(path.Up, direction);

            CancelSelection(rollGrab);
            FixDressing();
            kit.PartTransforms.bandageRoll.gameObject.SetActive(false);
            var end = kit.PartTransforms.bandageEnd;
            end.gameObject.SetActive(true);
            SetPose(end, exit, EndRotation(direction, face.normalized));
            SetCollider(endGrabCollider, true);
            Stage = PressureBandageStage.EndPlacement;
            counting = false;
            Version++;
        }

        void HandleReleases(bool focused)
        {
            if (pendingEndRelease)
            {
                pendingEndRelease = false;
                if (Stage == PressureBandageStage.EndPlacement && !endGrab.isSelected)
                    TryPlaceEnd(focused);
            }
            if (pendingTapeRelease)
            {
                pendingTapeRelease = false;
                if (Stage == PressureBandageStage.Taping && !tapeGrab.isSelected)
                    TryFixTape(focused);
            }
        }

        // Normal release of a tracked hand (pendingEndRelease): the same distance check as the held attach, no pose check.
        void TryPlaceEnd(bool focused)
        {
            // Released at the mark it was brought to during a field hold: not an attach (see Suspended).
            if (endRearm)
                return;
            if (!WithinEndSnap(kit.AnchorTransforms.endFix.position, EndMarkPoint()))
            {
                SetIssue(PressureWrapIssue.EndRejected);
                return;
            }
            if (!focused || !dressing.IsPlacementValid)
            {
                SetIssue(PressureWrapIssue.PlacementRequired);
                return;
            }
            AttachEnd();
        }

        // Held attach (see the class notes): only while the end is selected and the selecting hand is tracked now, input is
        // focused and the dressing is placed; then distance only. Not on the frame the end appears (nobody holds it yet).
        void UpdateEndSnap(bool focused)
        {
            if (Stage != PressureBandageStage.EndPlacement || !endGrab.isSelected || !focused || !dressing.IsPlacementValid || endRearm)
                return;
            if (EndHandTracking != PressureHandTracking.Tracked)
                return;
            if (WithinEndSnap(kit.AnchorTransforms.endFix.position, EndMarkPoint()))
                AttachEnd();
        }

        // The one attach of the end: the selection ends as a cancel (no release is recorded for it), the end is seated in the
        // laid pose on its mark, its grab is locked and the stage moves to Taping once (Version once; the cloth and the tape
        // frame follow from it).
        void AttachEnd()
        {
            if (Stage != PressureBandageStage.EndPlacement)
                return;
            CancelSelection(endGrab);
            pendingEndRelease = false;
            SeatEnd();
            SetCollider(endGrabCollider, false);
            Stage = PressureBandageStage.Taping;
            Version++;
        }

        // Wrap complete: the dressing's pose now (valid: the wrap counted only with a valid placement) is fixed in the wound's frame,
        // then a hand still holding it lets go as a cancel (OnSelectExited of the dressing records no release, so it never seats).
        void FixDressing()
        {
            var part = dressing.Grab.transform;
            var wound = dressing.Wound.transform;
            fixedDressingPosition = wound.InverseTransformPoint(part.position);
            fixedDressingRotation = Quaternion.Inverse(wound.rotation) * part.rotation;
            dressingFixed = true;
            CancelSelection(dressing.Grab);
        }

        // The fixed dressing stays exactly at its pose in the wound's frame (also through a training field hold).
        void KeepFixedDressing()
        {
            if (!dressingFixed)
                return;
            var part = dressing.Grab.transform;
            var wound = dressing.Wound.transform;
            var position = wound.TransformPoint(fixedDressingPosition);
            var rotation = wound.rotation * fixedDressingRotation;
            if ((part.position - position).sqrMagnitude > 1e-10f || Quaternion.Angle(part.rotation, rotation) > 0.01f)
                SetPose(part, position, rotation);
        }

        // Distance-only attach rule: the end's fix point within endSnapRadius of the mark, whatever the end's face, tilt or turn.
        public bool WithinEndSnap(Vector3 fixPoint, Vector3 mark) => Vector3.Distance(fixPoint, mark) <= endSnapRadius;

        // Normal release of a tracked hand (pendingTapeRelease): the same distance check as the held attach, no pose check.
        void TryFixTape(bool focused)
        {
            // Released at the mark it was brought to during a field hold: not an attach (see Suspended).
            if (tapeRearm)
                return;
            if (JudgeTape() != PressureFixCheck.Valid)
            {
                SetIssue(PressureWrapIssue.TapeRejected);
                return;
            }
            if (!focused || !dressing.IsPlacementValid)
            {
                SetIssue(PressureWrapIssue.PlacementRequired);
                return;
            }
            FixTape();
        }

        // Held attach: only while the tape is selected by a tracked hand, input is focused and the dressing is placed; then
        // distance only (see the class notes).
        void UpdateTapeSnap(bool focused)
        {
            if (Stage != PressureBandageStage.Taping || !tapeGrab.isSelected || !focused || !dressing.IsPlacementValid || tapeRearm)
                return;
            if (TapeHandTracking != PressureHandTracking.Tracked)
                return;
            if (JudgeTape() == PressureFixCheck.Valid)
                FixTape();
        }

        // The one fixing of the tape: its selection ends as a cancel (no release is recorded for it), it is seated on its mark in
        // the laid pose (contact face into the bandage, long side across it, the side kept from how it was held), its grab is
        // locked and the stage moves to Fixed once.
        void FixTape()
        {
            if (Stage != PressureBandageStage.Taping)
                return;
            TapeMark(out var mark, out _, out var normal, out var across);
            CancelSelection(tapeGrab);
            pendingTapeRelease = false;
            var tape = kit.PartTransforms.fixingTape;
            var side = Vector3.Dot(tape.right, across) >= 0f ? across : -across;
            var rotation = Quaternion.LookRotation(normal, Vector3.Cross(normal, side));
            var position = mark - rotation * kit.AnchorTransforms.tapeContact.localPosition;
            SetPose(tape, position, rotation);
            SetCollider(tapeGrabCollider, false);
            Stage = PressureBandageStage.Fixed;
            // The bandage now keeps the dressing: a hand still holding it lets go and no part can be grabbed again.
            CancelSelection(dressing.Grab);
            Version++;
        }

        // Laid end on the wrap continuation after the last waypoint (also after a profile rebuild while taping).
        void SeatEnd()
        {
            path.FrameAt(path.AngleOf(path.LastWaypoint), surfaceLift, out var position, out var tangent, out var normal);
            SetPose(kit.PartTransforms.bandageEnd, position, EndRotation(tangent, normal));
        }

        // ---------- Marks ----------

        // Mark of the end's fix point on the wrap continuation after the last waypoint: position, wrap direction, outer normal.
        public void EndMark(out Vector3 position, out Vector3 tangent, out Vector3 normal)
        {
            var theta = path.AngleAfter(path.AngleOf(path.LastWaypoint), endFixDistance);
            path.FrameAt(theta, surfaceLift, out position, out tangent, out normal);
        }

        // Mark of the tape's contact centre: over the end's fix point; across = bandage width direction.
        public void TapeMark(out Vector3 position, out Vector3 tangent, out Vector3 normal, out Vector3 across)
        {
            var theta = path.AngleAfter(path.AngleOf(path.LastWaypoint), endFixDistance);
            path.FrameAt(theta, surfaceLift * 2f, out position, out tangent, out normal);
            across = Vector3.Cross(tangent, normal).normalized;
        }

        Vector3 EndMarkPoint()
        {
            EndMark(out var mark, out _, out _);
            return mark;
        }

        float EndMarkDistance() => Vector3.Distance(kit.AnchorTransforms.endFix.position, EndMarkPoint());

        // Distance of the tape's contact centre from its mark now.
        float TapeDistance()
        {
            TapeMark(out var mark, out _, out _, out _);
            return Vector3.Distance(kit.AnchorTransforms.tapeContact.position, mark);
        }

        // Distance-only attach rule of the tape, as for the end.
        PressureFixCheck JudgeTape() => TapeDistance() <= tapeSnapRadius ? PressureFixCheck.Valid : PressureFixCheck.Away;

        // ---------- Cancel (C) and equipment reset ----------

        // The dressing left the wound, or slid / turned under the wrap: wrap, end and tape of this application are cancelled and
        // their parts put back so the bandage part can be done again. The dressing, a tape in hand or left elsewhere, and the
        // contact history are kept.
        void CancelApplication(PressureWrapIssue reason)
        {
            var detached = Stage >= PressureBandageStage.EndPlacement;
            var taped = Stage == PressureBandageStage.Fixed;
            CancelSelection(endGrab);
            foreach (var pose in initialPoses)
            {
                var p = kit.PartTransforms;
                if (pose.transform == p.bandageEnd || (detached && pose.transform == p.bandageRoll) || (taped && pose.transform == p.fixingTape))
                    RestorePose(pose);
            }
            SetCollider(rollGrabCollider, true);
            SetCollider(endGrabCollider, true);
            SetCollider(tapeGrabCollider, true);
            ClearApplicationState();
            SetIssue(reason);
            CancelCount++;
            Version++;
        }

        void ClearApplicationState()
        {
            Stage = PressureBandageStage.Ready;
            wrapAmount = 0f;
            counting = false;
            IsRollInZone = false;
            path.PrepareProfile(null);
            pendingEndRelease = false;
            pendingTapeRelease = false;
            endRearm = false;
            tapeRearm = false;
            dressingFixed = false;
            placementInvalidSince = -1f;
            EndDistance = -1f;
            TapeCheck = PressureFixCheck.None;
            LooseValid = false;
        }

        // Equipment reset entry for the later training flow (chapter 3 restart); not a trainee button. This component
        // coordinates it, in order:
        // 1. Block new selections (filters refuse while resetting) and end every kit selection as a cancel, so no release is
        //    handled as a placement, seat, end placement or taping.
        // 2. Put the dressing, roll, end, tape and the strip parts back to the scene's initial layout, visibility and rest.
        // 3. Clear the application state, wrap progress, pending releases, notices and the wrap profile.
        // 4. Clear the dressing's contact state, contact time and practice history (PressureDressing.ResetEquipmentState).
        // 5. Put the strip, wrap and end meshes back to the model originals (PressureBandageRibbons.ResetDisplay).
        // 6. Check the result; views (guide, board) rebuild from Version / ResetCount.
        // The user's position and view, the board and the wound layout are not touched; no scene reload; nothing starts by itself.
        // Returns false (and logs) when the reset is refused or a check fails; repeated calls are safe.
        public bool ResetEquipment()
        {
            if (resetting)
            {
                Debug.LogWarning("[PressureBandage] Equipment reset already running; this call was ignored.", this);
                return false;
            }
            if (!captured)
                Capture();
            if (!captured)
            {
                Debug.LogError("[PressureBandage] Equipment reset refused: the initial tray layout was not captured (kit missing or " +
                               "incomplete).", this);
                LastResetSucceeded = false;
                return false;
            }
            if (!TryValidate(out var error))
            {
                Debug.LogError("[PressureBandage] Equipment reset refused: " + error, this);
                LastResetSucceeded = false;
                return false;
            }

            resetting = true;
            string problem;
            try
            {
                CancelSelection(dressing.Grab);
                CancelSelection(rollGrab);
                CancelSelection(endGrab);
                CancelSelection(tapeGrab);
                foreach (var pose in initialPoses)
                    RestorePose(pose);
                SetCollider(rollGrabCollider, true);
                SetCollider(endGrabCollider, true);
                SetCollider(tapeGrabCollider, true);
                ClearApplicationState();
                LastIssue = PressureWrapIssue.None;
                LastIssueTime = -1f;
                lastRollHolder = null;
                RollHand = InteractorHandedness.None;
                RollHandTracking = PressureHandTracking.Missing;
                Block = PressureWrapBlock.None;
                dressing.ResetEquipmentState();
                ribbons.ResetDisplay();
                ResetCount++;
                Version++;
                problem = VerifyReset();
            }
            catch (Exception e)
            {
                problem = "exception " + e.GetType().Name + ": " + e.Message;
                Debug.LogException(e, this);
            }
            finally
            {
                resetting = false;
            }

            LastResetSucceeded = problem == null;
            if (problem != null)
                Debug.LogError("[PressureBandage] Equipment reset failed: " + problem, this);
            return LastResetSucceeded;
        }

        // Development entry for the user's Play check of the reset (Inspector context menu); not a trainee control.
        [ContextMenu("Reset Equipment (development check)")]
        void ResetEquipmentFromMenu()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[PressureBandage] Equipment reset is a Play mode operation.", this);
                return;
            }
            ResetEquipment();
        }

        string VerifyReset()
        {
            if (dressing.Grab.isSelected || rollGrab.isSelected || endGrab.isSelected || tapeGrab.isSelected)
                return "a kit part is still selected";
            foreach (var pose in initialPoses)
            {
                var t = pose.transform;
                if (t.parent != pose.parent || (t.localPosition - pose.position).sqrMagnitude > 1e-8f ||
                    Quaternion.Angle(t.localRotation, pose.rotation) > 0.01f || t.gameObject.activeSelf != pose.active)
                    return t.name + " is not at its initial layout";
            }
            if (Stage != PressureBandageStage.Ready || wrapAmount != 0f || path.IsWidenedForDressing)
                return "bandage application state not cleared";
            if (dressing.HasCompletedContactPractice || dressing.ContactSeconds != 0f)
                return "dressing contact state not cleared";
            if (!ribbons.ShowsModelMeshes)
                return "strip meshes not restored";
            return null;
        }

        // ---------- Helpers ----------

        void SetIssue(PressureWrapIssue issue)
        {
            LastIssue = issue;
            LastIssueTime = Time.time;
        }

        public bool IsRecent(PressureWrapIssue issue, float seconds) =>
            LastIssue == issue && LastIssueTime >= 0f && Time.time - LastIssueTime <= seconds;

        // Where the strip leaves the roll: on the roll surface, at its axial centre, toward the given point.
        public Vector3 RollExitToward(Vector3 target)
        {
            var centre = RollCentre;
            var toward = Vector3.ProjectOnPlane(target - centre, RollAxisDirection);
            return toward.sqrMagnitude > 1e-8f ? centre + toward.normalized * rollRadius : centre;
        }

        // End part rotation: its length axis (local X times lengthSign) along length, its outer face (local +Z) along face.
        public Quaternion EndRotation(Vector3 length, Vector3 face)
        {
            var x = length * lengthSign;
            return Quaternion.LookRotation(face, Vector3.Cross(face, x));
        }

        // Width direction of the end in world space, matching the strip's width = cross(length, face).
        public Vector3 EndWidth(Transform end) => Vector3.Cross(end.TransformDirection(new Vector3(lengthSign, 0f, 0f)), end.forward).normalized;

        void DressingCorners(List<Vector3> output)
        {
            output.Clear();
            var pad = kit.PartTransforms.dressingPad;
            for (var i = 0; i < 8; i++)
            {
                var sign = new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f);
                output.Add(pad.TransformPoint(dressingBounds.center + Vector3.Scale(dressingBounds.extents, sign)));
            }
        }

        static void CancelSelection(XRGrabInteractable grab)
        {
            if (grab == null || !grab.isSelected)
                return;
            var manager = grab.interactionManager;
            if (manager != null)
                manager.CancelInteractableSelection((IXRSelectInteractable)grab);
        }

        static void SetCollider(Collider c, bool on)
        {
            if (c != null)
                c.enabled = on;
        }

        static void SetPose(Transform part, Vector3 position, Quaternion rotation)
        {
            part.SetPositionAndRotation(position, rotation);
            if (part.TryGetComponent<Rigidbody>(out var body))
            {
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.position = position;
                body.rotation = rotation;
            }
        }

        static void RestorePose(PartPose pose)
        {
            var t = pose.transform;
            if (t.gameObject.activeSelf != pose.active)
                t.gameObject.SetActive(pose.active);
            if (t.parent != pose.parent)
                t.SetParent(pose.parent, false);
            t.SetLocalPositionAndRotation(pose.position, pose.rotation);
            if (pose.body != null && t.gameObject.activeInHierarchy)
            {
                if (!pose.body.isKinematic)
                {
                    pose.body.linearVelocity = Vector3.zero;
                    pose.body.angularVelocity = Vector3.zero;
                }
                pose.body.position = t.position;
                pose.body.rotation = t.rotation;
            }
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        // requireScene false for the prefab asset: the wrap path is a scene reference set on the instance.
        public bool TryValidate(out string error, bool requireScene = true)
        {
            var missing = new List<string>();
            if (dressing == null)
                missing.Add(nameof(dressing));
            else if (dressing.Grab == null)
                missing.Add("dressing grab");
            if (kit == null)
                missing.Add(nameof(kit));
            else if (!kit.TryValidate(out var kitError))
                missing.Add(kitError);
            if (ribbons == null)
                missing.Add(nameof(ribbons));
            if (rollGrab == null || rollGrabCollider == null)
                missing.Add("roll grab or collider");
            if (endGrab == null || endGrabCollider == null)
                missing.Add("end grab or collider");
            if (tapeGrab == null || tapeGrabCollider == null)
                missing.Add("tape grab or collider");
            if (kit != null && kit.TryValidate(out _))
            {
                var p = kit.PartTransforms;
                if (rollGrab != null && rollGrab.transform != p.bandageRoll)
                    missing.Add("roll grab on PB_BandageRoll");
                if (endGrab != null && endGrab.transform != p.bandageEnd)
                    missing.Add("end grab on PB_BandageEnd");
                if (tapeGrab != null && tapeGrab.transform != p.fixingTape)
                    missing.Add("tape grab on PB_FixingTape");
                if (rollGrabCollider != null && !rollGrabCollider.transform.IsChildOf(p.bandageRoll))
                    missing.Add("roll grab collider under the roll");
                if (endGrabCollider != null && !endGrabCollider.transform.IsChildOf(p.bandageEnd))
                    missing.Add("end grab collider under the end");
                if (tapeGrabCollider != null && !tapeGrabCollider.transform.IsChildOf(p.fixingTape))
                    missing.Add("tape grab collider under the tape");
                if (!p.bandageEnd.TryGetComponent<MeshFilter>(out var f) || f.sharedMesh == null)
                    missing.Add("end mesh");
            }
            if (requireScene && path == null)
                missing.Add(nameof(path));
            else if (requireScene && !path.TryValidate(out var pathError))
                missing.Add(pathError);
            if (!IsFinite(grabReach) || grabReach <= 0f || !IsFinite(placementLossSeconds) || placementLossSeconds < 0f ||
                !IsFinite(endSnapRadius) || endSnapRadius <= 0f || !IsFinite(tapeSnapRadius) || tapeSnapRadius <= 0f ||
                !IsFinite(surfaceLift) || surfaceLift < 0f || !IsFinite(rollMargin) || rollMargin < 0f ||
                !IsFinite(dressingSlideLimit) || dressingSlideLimit <= 0f || !IsFinite(profileTolerance) || profileTolerance <= 0f)
                missing.Add("handling values");
            if (!IsFinite(wrapSeconds) || wrapSeconds <= 0f)
                missing.Add(nameof(wrapSeconds) + " (finite, above zero)");
            if (captured && (rollRadius <= 0f || rollHalfWidth <= 0f))
                missing.Add("roll radius or width from the model");
            error = missing.Count > 0 ? $"PressureBandageApplication '{name}' missing or invalid: {string.Join(", ", missing)}." : null;
            return error == null;
        }
    }
}
