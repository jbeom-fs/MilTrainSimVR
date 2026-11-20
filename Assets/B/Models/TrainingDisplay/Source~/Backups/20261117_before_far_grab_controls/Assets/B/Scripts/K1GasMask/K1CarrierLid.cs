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
    // K1 carrier closure and flap. Selecting the closure point unlocks it (no re-lock); after that selection ends the flap
    // handle becomes selectable, and the holding hand's movement is turned into an openness value along the flap handle path.
    // Hands only produce openness; K1CarrierFlapPose is the single writer of the flap bones. Nothing grabs or moves the bag.
    // Reports state only (unlocked, openness, fully open). Mask grab/extraction and training progression are not here.
    // The closure mechanism itself is unconfirmed, so unlocking is a plain select with a colour change, not a modelled fastener.
    public sealed class K1CarrierLid : MonoBehaviour, IXRSelectFilter
    {
        const int PathSegments = 40;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] K1CarrierFlapPose flapPose;
        [Tooltip("Select point on Anchor_ClosureGrab. Active until the closure is unlocked and that selection ends.")]
        [SerializeField] XRSimpleInteractable closureInteractable;
        [Tooltip("Select point on Anchor_FlapGrab. Active only after unlocking.")]
        [SerializeField] XRSimpleInteractable flapInteractable;
        [Tooltip("Point that rides on the flap (Anchor_FlapGrab); its path defines how hand motion maps to openness.")]
        [SerializeField] Transform flapHandle;
        [SerializeField] Renderer closureIndicator;
        [SerializeField] Color lockedColor = new Color(1f, 0.6f, 0.1f, 1f);
        [SerializeField] Color unlockedColor = new Color(0.25f, 0.95f, 0.35f, 1f);
        [Tooltip("Engineering setting: a new selection needs the tracked controller within this distance (m) of the point. Blocks far ray selection.")]
        [SerializeField] float maxHandDistance = 0.25f;
        [Tooltip("Engineering setting, not a training criterion: IsFullyOpen turns on at this openness.")]
        [SerializeField, Range(0f, 1f)] float fullyOpenEnter = 0.97f;
        [Tooltip("Engineering setting: IsFullyOpen turns off again only below this openness (hysteresis).")]
        [SerializeField, Range(0f, 1f)] float fullyOpenExit = 0.93f;
        [Tooltip("Largest openness change per frame, so the flap follows the hand without jumping to a distant part of its path.")]
        [SerializeField, Range(0.01f, 0.5f)] float maxStepPerFrame = 0.1f;

        // Flap handle positions in carrier space at openness i / PathSegments.
        readonly Vector3[] handlePath = new Vector3[PathSegments + 1];
        MaterialPropertyBlock indicatorBlock;
        IXRSelectInteractor flapHand;
        TrackedPoseDriver flapDriver;
        bool hasBaseline;
        Vector3 baselineHand;
        float baselineOpenness;
        bool subscribed;
        // Lowest openness allowed while the stored mask is partly pulled out (0 = no hold). Set by K1MaskExtraction.
        float openHold;

        public bool IsUnlocked { get; private set; }
        public float Openness => flapPose != null ? flapPose.Openness : 0f;
        public bool IsFullyOpen { get; private set; }
        // Set by a training field while it asks whether to stop the course (its confirm dialog). A held flap stays held but does
        // not move (its baseline restarts, as after a tracking gap). Not saved; false by default.
        public bool Suspended { get; set; }
        // A hand holds the flap handle but its controller is not tracked, so the flap holds still. Read only.
        public bool IsFlapHandUntracked => flapHand != null && !IsTracked(flapDriver);
        public event Action<K1CarrierLid> Unlocked;
        // Raised only when IsFullyOpen actually changes.
        public event Action<K1CarrierLid, bool> FullyOpenChanged;

        public bool canProcess => isActiveAndEnabled;

        void Awake()
        {
            if (!TryValidate(out var error))
            {
                Debug.LogError(error, this);
                enabled = false;
                return;
            }

            // Sample the handle path through the same interpolation that drives the bones, then start closed and locked.
            for (var i = 0; i <= PathSegments; i++)
            {
                flapPose.Apply((float)i / PathSegments);
                handlePath[i] = transform.InverseTransformPoint(flapHandle.position);
            }

            flapPose.Apply(0f);
            closureInteractable.gameObject.SetActive(true);
            flapInteractable.gameObject.SetActive(false);
            ApplyIndicator();
        }

        void OnEnable()
        {
            if (subscribed)
                return;

            closureInteractable.selectEntered.AddListener(OnClosureSelected);
            flapInteractable.selectEntered.AddListener(OnFlapSelected);
            flapInteractable.selectExited.AddListener(OnFlapReleased);
            closureInteractable.selectFilters.Add(this);
            flapInteractable.selectFilters.Add(this);
            subscribed = true;

            // Resync with a flap selection that survived while only this component was disabled.
            if (flapInteractable.isSelected)
                BeginFlapControl(flapInteractable.firstInteractorSelecting);
        }

        void OnDisable()
        {
            ReleaseFlap();
            if (!subscribed)
                return;

            closureInteractable.selectEntered.RemoveListener(OnClosureSelected);
            flapInteractable.selectEntered.RemoveListener(OnFlapSelected);
            flapInteractable.selectExited.RemoveListener(OnFlapReleased);
            closureInteractable.selectFilters.Remove(this);
            flapInteractable.selectFilters.Remove(this);
            subscribed = false;
        }

        void Update()
        {
            if (Suspended)
            {
                hasBaseline = false;
                return;
            }

            // Hand over to the flap handle once the unlocking selection has ended, so opening always needs a new select.
            if (IsUnlocked && closureInteractable.gameObject.activeSelf && !closureInteractable.isSelected)
            {
                closureInteractable.gameObject.SetActive(false);
                flapInteractable.gameObject.SetActive(true);
            }

            if (flapHand != null)
                DriveFlap();
        }

        // Gates new selections only: a hand that already holds the point keeps it (the manager re-checks filters every frame).
        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            if (interactor.IsSelecting(interactable))
                return true;
            if (ReferenceEquals(interactable, closureInteractable))
                return !IsUnlocked && HandWithin(interactor, closureInteractable.transform);
            if (ReferenceEquals(interactable, flapInteractable))
                return IsUnlocked && HandWithin(interactor, flapHandle);
            return true;
        }

        // The flap may still open further but cannot close below this openness until the hold is released.
        public void HoldOpenAtLeast(float openness) => openHold = Mathf.Clamp01(openness);

        public void ReleaseOpenHold() => openHold = 0f;

        // New-run reset (training retry): locked, closed flap, closure point back, flap handle off, locked colour. Raises no
        // Unlocked or FullyOpenChanged event. Both points must already be released (the caller ends XRI selections first);
        // otherwise nothing changes. Repeated calls give the same state.
        public bool ResetForNewRun(out string error)
        {
            if (!TryValidate(out error))
                return false;
            if (closureInteractable.isSelected || flapInteractable.isSelected)
            {
                error = $"K1CarrierLid '{name}': closure or flap still selected; end the selections before the reset.";
                return false;
            }

            ReleaseFlap();
            openHold = 0f;
            IsUnlocked = false;
            IsFullyOpen = false;
            flapPose.Apply(0f);
            closureInteractable.gameObject.SetActive(true);
            flapInteractable.gameObject.SetActive(false);
            ApplyIndicator();
            return true;
        }

        public bool TryValidate(out string error)
        {
            var problems = new List<string>();
            if (flapPose == null)
                problems.Add(nameof(flapPose) + " missing");
            else if (!flapPose.TryValidate(out var poseError))
                problems.Add(poseError);
            if (closureInteractable == null)
                problems.Add(nameof(closureInteractable) + " missing");
            if (flapInteractable == null)
                problems.Add(nameof(flapInteractable) + " missing");
            if (flapHandle == null)
                problems.Add(nameof(flapHandle) + " missing");
            if (closureIndicator == null)
                problems.Add(nameof(closureIndicator) + " missing");
            if (maxHandDistance <= 0f)
                problems.Add(nameof(maxHandDistance) + " must be positive");
            if (fullyOpenExit >= fullyOpenEnter)
                problems.Add(nameof(fullyOpenExit) + " must be below " + nameof(fullyOpenEnter));

            error = problems.Count > 0 ? $"K1CarrierLid '{name}': {string.Join(", ", problems)}." : null;
            return error == null;
        }

        void OnClosureSelected(SelectEnterEventArgs args)
        {
            if (IsUnlocked)
                return;

            IsUnlocked = true;
            ApplyIndicator();
            Unlocked?.Invoke(this);
        }

        // Single select mode: another hand selecting takes over; it continues from the current openness.
        void OnFlapSelected(SelectEnterEventArgs args) => BeginFlapControl(args.interactorObject);

        void OnFlapReleased(SelectExitEventArgs args)
        {
            if (args.interactorObject == flapHand)
                ReleaseFlap();
        }

        void BeginFlapControl(IXRSelectInteractor interactor)
        {
            flapHand = interactor;
            flapDriver = DriverOf(interactor);
            hasBaseline = false;
        }

        void ReleaseFlap()
        {
            flapHand = null;
            flapDriver = null;
            hasBaseline = false;
        }

        // Relative control: the handle moves by the hand's displacement since the baseline, projected onto the handle path.
        // The baseline is the first tracked frame after a grab or after tracking returns, so the flap never jumps to the hand.
        void DriveFlap()
        {
            if (!IsTracked(flapDriver))
            {
                hasBaseline = false;
                return;
            }

            var hand = transform.InverseTransformPoint(flapDriver.transform.position);
            if (!hasBaseline)
            {
                baselineHand = hand;
                baselineOpenness = Openness;
                hasBaseline = true;
                return;
            }

            SetOpenness(NearestOpenness(PathPoint(baselineOpenness) + (hand - baselineHand), Openness));
        }

        void SetOpenness(float openness)
        {
            flapPose.Apply(Mathf.Max(openness, openHold));
            var fullyOpen = IsFullyOpen ? Openness >= fullyOpenExit : Openness >= fullyOpenEnter;
            if (fullyOpen == IsFullyOpen)
                return;

            IsFullyOpen = fullyOpen;
            FullyOpenChanged?.Invoke(this, fullyOpen);
        }

        Vector3 PathPoint(float openness)
        {
            var x = Mathf.Clamp01(openness) * PathSegments;
            var i = Mathf.Min((int)x, PathSegments - 1);
            return Vector3.Lerp(handlePath[i], handlePath[i + 1], x - i);
        }

        // Closest point on the path within maxStepPerFrame of the current openness.
        float NearestOpenness(Vector3 target, float current)
        {
            var lo = Mathf.Max(0f, current - maxStepPerFrame);
            var hi = Mathf.Min(1f, current + maxStepPerFrame);
            var first = Mathf.Clamp(Mathf.FloorToInt(lo * PathSegments), 0, PathSegments - 1);
            var last = Mathf.Clamp(Mathf.CeilToInt(hi * PathSegments) - 1, first, PathSegments - 1);
            var best = current;
            var bestDistance = float.PositiveInfinity;
            for (var i = first; i <= last; i++)
            {
                var a = handlePath[i];
                var ab = handlePath[i + 1] - a;
                var t = ab.sqrMagnitude > 1e-12f ? Mathf.Clamp01(Vector3.Dot(target - a, ab) / ab.sqrMagnitude) : 0f;
                var distance = (a + ab * t - target).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = (i + t) / PathSegments;
                }
            }

            return Mathf.Clamp(best, lo, hi);
        }

        bool HandWithin(IXRSelectInteractor interactor, Transform point)
        {
            var driver = DriverOf(interactor);
            return IsTracked(driver) && Vector3.Distance(driver.transform.position, point.position) <= maxHandDistance;
        }

        // Shared with K1MaskExtraction so both devices judge the controlling hand the same way.
        internal static TrackedPoseDriver DriverOf(IXRSelectInteractor interactor) =>
            interactor != null && interactor.transform != null ? interactor.transform.GetComponentInParent<TrackedPoseDriver>() : null;

        // A transform alone is not tracking: the controller pose driver must be live and report a tracked position.
        internal static bool IsTracked(TrackedPoseDriver driver)
        {
            if (driver == null || !driver.isActiveAndEnabled)
                return false;

            var tracking = driver.trackingStateInput.action;
            if (tracking == null || !tracking.enabled || tracking.controls.Count == 0)
                return false;
            if (((InputTrackingState)tracking.ReadValue<int>() & InputTrackingState.Position) == 0)
                return false;

            var position = driver.positionInput.action;
            return position != null && position.enabled && position.controls.Count > 0;
        }

        void ApplyIndicator()
        {
            indicatorBlock ??= new MaterialPropertyBlock();
            closureIndicator.GetPropertyBlock(indicatorBlock);
            indicatorBlock.SetColor(BaseColorId, IsUnlocked ? unlockedColor : lockedColor);
            closureIndicator.SetPropertyBlock(indicatorBlock);
        }
    }
}
