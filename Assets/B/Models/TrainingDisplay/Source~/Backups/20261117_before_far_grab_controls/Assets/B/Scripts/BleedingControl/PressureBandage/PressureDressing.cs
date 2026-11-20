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
    // Why the two-hand contact stopped counting (the last break; kept until the next break).
    public enum PressureContactBreak
    {
        None = 0,
        // A hand left the press zone above the dressing.
        HandLeft = 1,
        // A hand's controller stopped being tracked, or its pose driver is missing.
        TrackingLost = 2,
        // The application lost input focus.
        FocusLost = 3,
        // The dressing no longer covers the wound validly.
        PlacementLost = 4,
        // This component was disabled.
        Disabled = 5
    }

    // Pressure bandage chapter 1, dressing handling and contact state; the only owner of the continuous contact time. Not a
    // pressure, blood flow or bleeding control measurement: hands are judged only by tracked controller positions in zones.
    //
    // Grab: XRGrabInteractable on the dressing part (single select, so one moving owner; the other hand grabbing takes over).
    // This component is a select filter of that interactable: a new selection needs a tracked controller within grabReach of the
    // dressing grab collider (near grab only, no far ray pick-up); a selection in progress is never refused, so the dressing is
    // never dropped by this component. Released dressing rests where it is (kinematic, no gravity) and can be grabbed again by
    // either hand. After a normal release by a tracked hand whose resting pose is a valid placement, it is seated once: turned
    // about its contact centre so the contact face lies on the skin plane (lateral position kept). A release that is not valid is
    // never seated or counted; a near pass while held never fixes the dressing.
    //
    // States, recomputed every LateUpdate from current data only (after XRI's Late phase):
    // - Placement valid: the current dressing pose covers the designated wound with its contact face (PressureBandageWound).
    // - Hold valid (one hand keeping the dressing, for the pre-wrap check): placement valid, the dressing selected, the
    //   selecting hand tracked and its controller within holdReach of the press anchor, and input focus. Letting go ends it;
    //   a same-frame hand-over keeps it.
    // - Two-hand contact valid (direct pressure practice, VR shorthand for stacked hands): placement valid, both controllers
    //   tracked and inside the press zone above the dressing, and input focus. No hand has to select the dressing.
    // Continuous contact: counts up only while two-hand contact stays valid; any break resets it to zero (breaks are never
    // summed). The first valid frame after a break counts nothing, a long frame counts at most MaxCountedStep, so a return from
    // tracking loss, focus loss or disable never jumps to completion. Reaching contactCheckSeconds once records the practice
    // in this run's history; the history never stands in for the current state.
    //
    // Input-time reads (IsInputFocusedNow, IsHoldValidNow) apply the same rules to current data without touching any state, for
    // inputs handled in Update before this LateUpdate; the contact time and practice history are updated in Refresh only.
    //
    // Chapter 2 (PressureBandageApplication) only reads these states (its wrap, end and tape need the placement, not the hold), adds its
    // own select filter to the dressing grab (one hand, one kit part; no new grab after fixing) and, for the equipment reset
    // only, calls ResetEquipmentState. It never counts the contact time.
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class PressureDressing : MonoBehaviour, IXRSelectFilter
    {
        const float MaxCountedStep = 0.1f;

        [SerializeField] XRGrabInteractable grab;
        [SerializeField] Rigidbody body;
        [Tooltip("Grab collider on its own child of the dressing part (not the visual mesh).")]
        [SerializeField] Collider grabCollider;
        [SerializeField] Transform contactAnchor;
        [SerializeField] Transform pressAnchor;
        [Tooltip("Half extents (m) of the dressing face along the dressing part's local X and Y, from the model mesh.")]
        [SerializeField] Vector2 faceHalfSize = new Vector2(0.05f, 0.05f);
        [Tooltip("Designated wound in this scene.")]
        [SerializeField] PressureBandageWound wound;
        [Tooltip("Pose drivers of the left and right controllers in this scene (hand positions and tracking).")]
        [SerializeField] TrackedPoseDriver leftHand;
        [SerializeField] TrackedPoseDriver rightHand;
        [Header("VR handling values (development, not medical criteria)")]
        [Tooltip("Largest distance (m) from the controller to the dressing grab collider to start a grab.")]
        [SerializeField, Min(0.01f)] float grabReach = 0.1f;
        [Tooltip("Largest distance (m) from the holding controller to the press anchor for the hold to count.")]
        [SerializeField, Min(0.01f)] float holdReach = 0.12f;
        [Tooltip("Press zone radius (m) around the press anchor, measured across the dressing face.")]
        [SerializeField, Min(0.01f)] float pressZoneRadius = 0.12f;
        [Tooltip("Press zone extent (m) below the top face, so hands slightly inside the arm still count.")]
        [SerializeField, Min(0f)] float pressZoneBelow = 0.03f;
        [Tooltip("Press zone height (m) above the top face; room for two stacked controllers.")]
        [SerializeField, Min(0.01f)] float pressZoneHeight = 0.22f;
        [Tooltip("Continuous two-hand contact needed to record the practice (s). A development handling check, not a medical " +
                 "compression time. Finite and above zero.")]
        [SerializeField] float contactCheckSeconds = 5f;

        XRGrabInteractable subscribedGrab;
        bool filterAdded;
        bool releasePending;
        bool wasTwoHand;
        float contactSeconds;
        readonly Dictionary<IXRSelectInteractor, TrackedPoseDriver> drivers = new Dictionary<IXRSelectInteractor, TrackedPoseDriver>();

        public PressureBandageWound Wound => wound;
        public XRGrabInteractable Grab => grab;
        public Rigidbody Body => body;
        public TrackedPoseDriver LeftHandDriver => leftHand;
        public TrackedPoseDriver RightHandDriver => rightHand;
        public Transform ContactAnchor => contactAnchor;
        public Transform PressAnchor => pressAnchor;
        public Vector2 FaceHalfSize => faceHalfSize;
        public float ContactCheckSeconds => contactCheckSeconds;
        public bool ContactCheckSecondsValid => IsFinite(contactCheckSeconds) && contactCheckSeconds > 0f;

        // Current state (this frame).
        public PressureDressingCheck CurrentCheck { get; private set; }
        public bool IsPlacementValid => CurrentCheck == PressureDressingCheck.Valid;
        public bool IsHeld { get; private set; }
        public InteractorHandedness HoldingHand { get; private set; }
        public PressureHandTracking HoldingHandTracking { get; private set; }
        public bool IsHoldValid { get; private set; }
        public PressureHandTracking LeftTracking { get; private set; }
        public PressureHandTracking RightTracking { get; private set; }
        public bool LeftInPressZone { get; private set; }
        public bool RightInPressZone { get; private set; }
        public bool InputFocused { get; private set; }
        // Set by a training field while it asks whether to stop the course (its confirm dialog): handled like lost input focus, so
        // no contact time is counted and a contact under way ends (it starts again from zero after the dialog); a release meanwhile
        // is never seated. The selection itself is kept. Not saved; false by default.
        public bool InputSuspended { get; set; }
        public bool IsTwoHandContactValid { get; private set; }
        public float ContactSeconds => contactSeconds;
        public float ContactProgress => ContactCheckSecondsValid ? Mathf.Clamp01(contactSeconds / contactCheckSeconds) : 0f;
        public PressureContactBreak LastContactBreak { get; private set; }
        // Time.time of LastContactBreak; negative before any break.
        public float LastContactBreakTime { get; private set; } = -1f;

        // History of this run: the continuous two-hand contact reached contactCheckSeconds at least once.
        public bool HasCompletedContactPractice { get; private set; }
        // Raised once, when the practice is first recorded.
        public event Action<PressureDressing> ContactPracticeCompleted;

        bool IXRSelectFilter.canProcess => isActiveAndEnabled;

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
            if (subscribedGrab == null && grab != null)
            {
                subscribedGrab = grab;
                subscribedGrab.selectEntered.AddListener(OnSelectEntered);
                subscribedGrab.selectExited.AddListener(OnSelectExited);
            }
            if (!filterAdded && grab != null)
            {
                grab.selectFilters.Add(this);
                filterAdded = true;
            }
            // Starts from no contact; an existing selection is read again from the interactable on the next LateUpdate.
            wasTwoHand = false;
            contactSeconds = 0f;
        }

        void OnDisable()
        {
            if (subscribedGrab != null)
            {
                subscribedGrab.selectEntered.RemoveListener(OnSelectEntered);
                subscribedGrab.selectExited.RemoveListener(OnSelectExited);
            }
            subscribedGrab = null;
            if (filterAdded && grab != null)
                grab.selectFilters.Remove(this);
            filterAdded = false;

            // The XR selection itself is left to XRI; only this component's derived state is cleared.
            if (wasTwoHand)
                RecordBreak(PressureContactBreak.Disabled);
            releasePending = false;
            wasTwoHand = false;
            contactSeconds = 0f;
            IsTwoHandContactValid = false;
            IsHoldValid = false;
            drivers.Clear();
        }

        void OnSelectEntered(SelectEnterEventArgs args) => releasePending = false;

        void OnSelectExited(SelectExitEventArgs args)
        {
            // Seated later this frame only after a normal release by a tracked hand; a canceled selection (tracking loss
            // handling, disable, unload) or an untracked hand leaves the dressing where XRI left it.
            releasePending = !args.isCanceled && !InputSuspended && PressureBandageTracking.Of(DriverOf(args.interactorObject)) == PressureHandTracking.Tracked;
        }

        void LateUpdate()
        {
            if (releasePending)
            {
                releasePending = false;
                if (!grab.isSelected)
                    SeatIfValid();
            }
            Refresh();
        }

        void Refresh()
        {
            InputFocused = Application.isFocused && !InputSuspended;
            LeftTracking = PressureBandageTracking.Of(leftHand);
            RightTracking = PressureBandageTracking.Of(rightHand);
            CurrentCheck = EvaluateCurrent();

            var holder = Holder;
            IsHeld = holder != null;
            HoldingHand = holder != null ? holder.handedness : InteractorHandedness.None;
            var holderDriver = holder != null ? DriverOf(holder) : null;
            HoldingHandTracking = holder != null ? PressureBandageTracking.Of(holderDriver) : PressureHandTracking.Missing;
            IsHoldValid = HoldValid(InputFocused, IsPlacementValid, holderDriver, HoldingHandTracking);

            LeftInPressZone = LeftTracking == PressureHandTracking.Tracked && InPressZone(leftHand.transform.position);
            RightInPressZone = RightTracking == PressureHandTracking.Tracked && InPressZone(rightHand.transform.position);
            var twoHand = InputFocused && IsPlacementValid && LeftInPressZone && RightInPressZone;

            if (twoHand)
            {
                // A new run of contact starts at zero; only frames after the first one add time.
                contactSeconds = wasTwoHand ? contactSeconds + Mathf.Min(Time.deltaTime, MaxCountedStep) : 0f;
                if (!HasCompletedContactPractice && ContactCheckSecondsValid && contactSeconds >= contactCheckSeconds)
                {
                    HasCompletedContactPractice = true;
                    ContactPracticeCompleted?.Invoke(this);
                }
            }
            else
            {
                if (wasTwoHand)
                    RecordBreak(BreakReason());
                contactSeconds = 0f;
            }
            wasTwoHand = twoHand;
            IsTwoHandContactValid = twoHand;
        }

        IXRSelectInteractor Holder => grab.isSelected ? grab.firstInteractorSelecting : null;

        // One-hand hold rule, shared by the frame state (Refresh) and the input-time read (IsHoldValidNow).
        bool HoldValid(bool focused, bool placementValid, TrackedPoseDriver holderDriver, PressureHandTracking holderTracking) =>
            focused && placementValid && holderTracking == PressureHandTracking.Tracked &&
            Vector3.Distance(holderDriver.transform.position, pressAnchor.position) <= holdReach;

        // Input-time reads, for an input handled before this frame's LateUpdate (a UI button): the same data and rules as the
        // frame state, read now. Nothing is changed: no contact time, practice record, event, pose or selection.
        public bool IsInputFocusedNow => Application.isFocused;

        // False while this component is not running (as the frame state after OnDisable).
        public bool IsHoldValidNow
        {
            get
            {
                if (!isActiveAndEnabled)
                    return false;
                var holder = Holder;
                var holderDriver = holder != null ? DriverOf(holder) : null;
                var tracking = holder != null ? PressureBandageTracking.Of(holderDriver) : PressureHandTracking.Missing;
                return HoldValid(Application.isFocused, EvaluateCurrent() == PressureDressingCheck.Valid, holderDriver, tracking);
            }
        }

        // Equipment reset step, called only by PressureBandageApplication.ResetEquipment after it has cancelled the dressing
        // selection and put the dressing back to its tray pose: clears this run's contact state, contact time and practice
        // history, then recomputes the current state from that pose. XR selection and the pose are not touched here.
        public void ResetEquipmentState()
        {
            releasePending = false;
            wasTwoHand = false;
            contactSeconds = 0f;
            IsTwoHandContactValid = false;
            IsHoldValid = false;
            HasCompletedContactPractice = false;
            LastContactBreak = PressureContactBreak.None;
            LastContactBreakTime = -1f;
            if (isActiveAndEnabled)
                Refresh();
        }

        PressureContactBreak BreakReason()
        {
            if (!InputFocused)
                return PressureContactBreak.FocusLost;
            if (LeftTracking != PressureHandTracking.Tracked || RightTracking != PressureHandTracking.Tracked)
                return PressureContactBreak.TrackingLost;
            if (!IsPlacementValid)
                return PressureContactBreak.PlacementLost;
            return PressureContactBreak.HandLeft;
        }

        void RecordBreak(PressureContactBreak reason)
        {
            LastContactBreak = reason;
            LastContactBreakTime = Time.time;
        }

        // Inside a cylinder over the press anchor along the outward normal of the top face.
        bool InPressZone(Vector3 hand)
        {
            var outward = (pressAnchor.position - contactAnchor.position).normalized;
            var offset = hand - pressAnchor.position;
            var height = Vector3.Dot(offset, outward);
            if (height < -pressZoneBelow || height > pressZoneHeight)
                return false;
            return (offset - outward * height).magnitude <= pressZoneRadius;
        }

        PressureDressingCheck EvaluateCurrent()
        {
            if (wound == null)
                return PressureDressingCheck.None;
            var part = grab.transform;
            var direction = (contactAnchor.position - pressAnchor.position).normalized;
            return wound.Evaluate(contactAnchor.position, direction, part.right, part.up, faceHalfSize);
        }

        // Turn about the contact centre so the contact face points into the skin, then move along the skin normal onto the
        // surface plane. Applied only when the resting pose is valid and the seated pose stays valid.
        void SeatIfValid()
        {
            if (wound == null || EvaluateCurrent() != PressureDressingCheck.Valid)
                return;

            var part = grab.transform;
            var pivot = contactAnchor.position;
            var direction = (pivot - pressAnchor.position).normalized;
            var turn = Quaternion.FromToRotation(direction, -wound.Normal);
            var seatedContact = pivot - wound.Normal * Vector3.Dot(pivot - wound.Center, wound.Normal);
            var check = wound.Evaluate(seatedContact, -wound.Normal, turn * part.right, turn * part.up, faceHalfSize);
            if (check != PressureDressingCheck.Valid)
                return;

            var rotation = turn * part.rotation;
            var position = pivot + turn * (part.position - pivot) + (seatedContact - pivot);
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            part.SetPositionAndRotation(position, rotation);
            body.position = position;
            body.rotation = rotation;
        }

        // Near grab only: a tracked controller close to the grab collider. A selection in progress always passes, so this filter
        // never ends a grab (XRI re-checks filters while selected).
        bool IXRSelectFilter.Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            if (interactor == null)
                return false;
            if (interactor.IsSelecting(interactable))
                return true;
            var driver = DriverOf(interactor);
            if (PressureBandageTracking.Of(driver) != PressureHandTracking.Tracked || grabCollider == null || !grabCollider.enabled)
                return false;
            var hand = driver.transform.position;
            return Vector3.Distance(grabCollider.ClosestPoint(hand), hand) <= grabReach;
        }

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

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        // requireScene false for the prefab asset: the wound and the hand drivers are scene references set on the instance.
        public bool TryValidate(out string error, bool requireScene = true)
        {
            var missing = new List<string>();
            if (grab == null)
                missing.Add(nameof(grab));
            if (body == null)
                missing.Add(nameof(body));
            if (grabCollider == null)
                missing.Add(nameof(grabCollider));
            if (contactAnchor == null)
                missing.Add(nameof(contactAnchor));
            if (pressAnchor == null)
                missing.Add(nameof(pressAnchor));
            if (grab != null && body != null && body.gameObject != grab.gameObject)
                missing.Add("body on the grab object");
            if (requireScene && wound == null)
                missing.Add(nameof(wound));
            else if (requireScene && !wound.TryValidate(out var woundError))
                missing.Add(woundError);
            if (requireScene && leftHand == null)
                missing.Add(nameof(leftHand));
            if (requireScene && rightHand == null)
                missing.Add(nameof(rightHand));
            if (!ContactCheckSecondsValid)
                missing.Add(nameof(contactCheckSeconds) + " (finite, above zero)");
            if (!(faceHalfSize.x > 0f) || !(faceHalfSize.y > 0f))
                missing.Add(nameof(faceHalfSize));
            error = missing.Count > 0 ? $"PressureDressing '{name}' missing or invalid: {string.Join(", ", missing)}." : null;
            return error == null;
        }
    }
}
