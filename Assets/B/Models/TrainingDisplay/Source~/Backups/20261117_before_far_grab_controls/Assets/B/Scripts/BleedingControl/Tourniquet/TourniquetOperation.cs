using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MilTrainSimVR.B.BleedingControl.Tourniquet
{
    // Current handling target after attaching. Order of this virtual prototype flow (not an approved medical procedure):
    // PullBand -> TurnWindlass -> SeatWindlass -> RecordTime -> CoverStrap -> Done. Values are kept stable; RecordTime was
    // added later with its own value.
    public enum TourniquetStep
    {
        NotAttached = 0,
        PullBand = 1,
        TurnWindlass = 2,
        SeatWindlass = 3,
        CoverStrap = 4,
        Done = 5,
        RecordTime = 6
    }

    public enum TourniquetWindlassPhase
    {
        // At the turning height: free rotation around the axis.
        Turning = 0,
        // On the entry path beside the clip, moving along the axis only.
        Lowering = 1,
        // At the clip height: rotation only toward the clip's open side.
        Lowered = 2,
        // Fully turned into the clip.
        Secured = 3
    }

    public enum TourniquetPart
    {
        None = 0,
        BandEnd = 1,
        Windlass = 2,
        SecuringStrap = 3,
        TimeLabel = 4
    }

    public enum TourniquetOperationIssue
    {
        None = 0,
        TrackingLost = 1,
        SelectionCanceled = 2,
        PullIncomplete = 3,
        TurnIncomplete = 4,
        SeatIncomplete = 5,
        CoverIncomplete = 6,
        PartsUnavailable = 7,
        // Normal release without an attach sample of that frame and grab (for example an interactor without an attach controller).
        ReleaseInputUnavailable = 8
    }

    // Tourniquet chapter 2: part handling after the device is attached (band end pull, windlass turn and seat, securing strap
    // cover, virtual time record), with state and events for the later training connection. No training step, score or UI flow.
    // Input: each part has its own XRSimpleInteractable handle, so XRI never moves a transform; this component alone writes the
    // part poses and work-copy meshes, in LateUpdate after XRI's selection update. One hand per part (single select); a new
    // grab, a hand swap and a tracking gap restart the hand reference without counting the step. A canceled selection or an
    // untracked release never completes anything. The body grab is locked by TourniquetDevice once attached.
    // Hand input per frame: XRI updates the holding NearFar interactor's attach (InteractionAttachController.DoUpdate, raising
    // its public attachUpdated) before it processes selections in the same Update; the release resets the attach offset before
    // selectExited arrives. So each handle samples the attach position at attachUpdated, tagged with the frame and the grab
    // generation. A held part applies that sample in LateUpdate; a released part applies the sample of its release frame once,
    // before its completion is judged. A frame's sample is consumed once, so the release frame is never counted twice.
    // All amounts (pull travel, turn degrees, fold) are game handling settings, not pressure, bleeding control or medical counts.
    [DefaultExecutionOrder(105)]
    [DisallowMultipleComponent]
    public sealed class TourniquetOperation : MonoBehaviour
    {
        sealed class PartHandle
        {
            public readonly TourniquetPart Part;
            public readonly XRSimpleInteractable Interactable;
            public IXRSelectInteractor Holder;
            public TrackedPoseDriver Driver;
            public bool HasPosition;
            public Vector3 RefPosition;
            public bool HasDirection;
            public Vector3 RefDirection;
            public bool ReleasePending;
            public TourniquetOperationIssue ReleaseIssue;
            // Attach controller of the holding NearFar interactor, subscribed while held, and this handle's listener.
            public InteractionAttachController Attach;
            public Action AttachHandler;
            // Incremented by every grab, so a sample of an earlier grab or hand never counts.
            public int Generation;
            public int SampleFrame = -1;
            public int SampleGeneration;
            public Vector3 SamplePosition;
            public bool SampleTracked;
            // Frame whose hand input was applied (held or release).
            public int ConsumedFrame = -1;
            public bool ReleaseHasInput;
            public Vector3 ReleasePosition;

            public PartHandle(TourniquetPart part, XRSimpleInteractable interactable)
            {
                Part = part;
                Interactable = interactable;
            }

            public void ResetReference()
            {
                HasPosition = false;
                HasDirection = false;
            }
        }

        [SerializeField] TourniquetDevice device;
        [Header("Part handles (XRSimpleInteractable; saved disabled, enabled only when their step is open)")]
        [SerializeField] XRSimpleInteractable bandEndHandle;
        [SerializeField] XRSimpleInteractable windlassHandle;
        [SerializeField] XRSimpleInteractable strapHandle;
        [SerializeField] XRSimpleInteractable timeLabelHandle;
        [Header("Deformed visuals (runtime work copies; the imported meshes are not changed)")]
        [SerializeField] TourniquetMeshDeform bandEndVisual;
        [SerializeField] TourniquetMeshDeform bodyVisual;
        [Tooltip("Sub-mesh of the body visual that is the internal band (woven material).")]
        [SerializeField, Min(0)] int internalBandSubMesh = 1;
        [SerializeField] TourniquetMeshDeform strapVisual;
        [SerializeField] TourniquetMeshDeform timeLabelVisual;
        [Tooltip("Text on Surface_TimeLabel, beside the printed TIME mark; filled from TrainingSubtitles.TimeLabelFormat.")]
        [SerializeField] TMP_Text timeRecordText;

        [Header("Hand input filter (game handling settings)")]
        [Tooltip("Hand travel in metres smaller than this is held back as tremor (band end, strap, windlass lowering).")]
        [SerializeField, Min(0f)] float jitterTravel = 0.002f;
        [Tooltip("Hand step in metres larger than this restarts the reference without counting (tracking jump).")]
        [SerializeField, Min(0.01f)] float maxStepTravel = 0.15f;
        [Tooltip("Angle change in degrees smaller than this is held back as tremor (windlass).")]
        [SerializeField, Range(0f, 10f)] float jitterDegrees = 1.5f;
        [Tooltip("Angle step in degrees larger than this restarts the reference without counting.")]
        [SerializeField, Range(10f, 180f)] float maxStepDegrees = 60f;

        [Header("Band end pull (game handling settings)")]
        [Tooltip("Hand travel in metres along Pivot_BandEnd +Z for full virtual pull progress.")]
        [SerializeField, Min(0.01f)] float bandPullHandTravel = 0.12f;
        [Tooltip("Visual travel in metres of the free end at full progress (the model's limited pull sample is 0.02 m).")]
        [SerializeField, Range(0f, 0.02f)] float bandVisualPull = 0.02f;
        [Tooltip("Band end local Z in metres (tip at 0, buckle side negative): fixed up to this value...")]
        [SerializeField] float bandFixedUntilZ = -0.08f;
        [Tooltip("...and moving fully from this value toward the tip.")]
        [SerializeField] float bandFreeFromZ = -0.05f;
        [Tooltip("Progress needed when the band end is let go to confirm the band as prepared.")]
        [SerializeField, Range(0.5f, 1f)] float bandPreparedProgress = 0.95f;

        [Header("Windlass turn (game handling settings, not a medical turn count)")]
        [SerializeField, Min(30f)] float requiredTurnDegrees = 360f;
        [Tooltip("Hand distance in metres from the windlass axis below which the angle is unstable and not counted.")]
        [SerializeField, Min(0.005f)] float minAxisDistance = 0.015f;
        [Header("Windlass seat (model entry geometry: drop 0.019 m, -30 deg into the clip)")]
        [SerializeField, Min(0.001f)] float seatDrop = 0.019f;
        [Tooltip("Turn from the entry angle (rod beside the clip, 0 or 180 deg) into the clip; the sign is the clip's open side.")]
        [SerializeField] float seatEntryDegrees = -30f;
        [Tooltip("Max angle in degrees from an entry angle at which lowering can start.")]
        [SerializeField, Range(1f, 20f)] float entryWindowDegrees = 8f;
        [Tooltip("Downward hand travel in metres at an entry angle before lowering starts.")]
        [SerializeField, Min(0f)] float lowerStartTravel = 0.005f;
        [Header("Internal band (body visual) following the windlass")]
        [Tooltip("Depth in metres below the windlass centre where the internal band stays fixed to the body.")]
        [SerializeField, Min(0.005f)] float internalBandFixedDepth = 0.029f;
        [Tooltip("Depth in metres below the windlass centre from which the internal band turns fully with the rod (in its slot).")]
        [SerializeField, Min(0f)] float internalBandFollowDepth = 0.0063f;

        [Header("Securing strap (bent over the clip, time label follows)")]
        [SerializeField, Range(30f, 200f)] float strapCoverDegrees = 180f;
        [SerializeField, Range(1f, 30f)] float strapCoverTolerance = 6f;
        [Tooltip("Bend radius in metres at the strap hinge; the folded part lies 2 x radius above the hinge.")]
        [SerializeField, Min(0.001f)] float strapBendRadius = 0.003f;
        [Tooltip("Strap length in metres from the hinge along its +Z, used to turn hand travel into fold angle.")]
        [SerializeField, Min(0.01f)] float strapLength = 0.044f;
        [Tooltip("Fold angle in degrees up to which the time label still faces up and can be written on.")]
        [SerializeField, Range(0f, 180f)] float timeLabelMaxFold = 90f;

        readonly List<PartHandle> handles = new List<PartHandle>();
        PartHandle band, windlass, strap, timeLabel;
        bool subscribed;
        bool partsReady;
        bool wasAttached;
        float sessionStart;

        float bandPull;
        bool bandPrepared;
        float rodAngle;
        int tightenSign;
        float turnDegrees;
        TourniquetWindlassPhase phase;
        float rodDrop;
        float seatAngle;
        float entryAngle;
        float lowerStartAngle;
        float lowerTravel;
        float strapFold;
        bool strapFastened;
        bool timeRecorded;
        float recordedSeconds;

        // Rest poses and frames captured once the parts are prepared.
        Vector3 windlassRestPosition;
        Quaternion windlassRestRotation;
        Vector3 bandHandleRest;
        Pose strapHandleRest;
        Pose surfaceRestLocal;
        Pose surfaceInStrap;
        Matrix4x4 strapVisualToStrap, strapToStrapVisual, labelVisualToStrap, strapToLabelVisual;
        Quaternion strapVisualToStrapRotation, labelToStrapRotation;
        int[] internalBand;
        Vector3 axisInBody;
        Vector3 axisDirectionInBody;
        float foldRadians;
        TourniquetMeshDeform.VertexMap mapBand, mapInternalBand, mapStrap, mapLabel;

        public TourniquetDevice Device => device;
        public bool IsReady => partsReady;
        public TourniquetStep CurrentStep =>
            device == null || !device.IsAttached ? TourniquetStep.NotAttached
            : !bandPrepared ? TourniquetStep.PullBand
            : phase == TourniquetWindlassPhase.Turning && turnDegrees < requiredTurnDegrees ? TourniquetStep.TurnWindlass
            : phase != TourniquetWindlassPhase.Secured ? TourniquetStep.SeatWindlass
            : !timeRecorded ? TourniquetStep.RecordTime
            : !strapFastened ? TourniquetStep.CoverStrap
            : TourniquetStep.Done;
        public TourniquetPart HeldPart
        {
            get
            {
                foreach (var h in handles)
                {
                    if (h.Holder != null)
                        return h.Part;
                }
                return TourniquetPart.None;
            }
        }
        // Last reason a step did not advance; cleared by the next grab.
        public TourniquetOperationIssue LastIssue { get; private set; }
        // Set by a training field while it asks whether to stop the course (its confirm dialog). A held part stays held but does
        // not move (its hand reference restarts, so motion during the dialog is never counted), a release meanwhile completes
        // nothing and the attach-time clock of the time record does not count the dialog. Not saved; false by default.
        public bool Suspended
        {
            get => suspended;
            set
            {
                if (!suspended && value)
                    suspendedAt = Time.time;
                else if (suspended && !value && device != null && device.IsAttached)
                    sessionStart += Time.time - suspendedAt;
                suspended = value;
            }
        }
        bool suspended;
        float suspendedAt;

        // Virtual band pull progress 0..1, and the band prepared state confirmed by a valid release at enough progress.
        public float BandPullProgress => bandPull;
        public bool IsBandPrepared => bandPrepared;
        // Virtual turn progress in degrees in the tightening direction (set by the first counted turn: +1 or -1, 0 before).
        public float WindlassTurnDegrees => turnDegrees;
        public float RequiredTurnDegrees => requiredTurnDegrees;
        public int TighteningDirection => tightenSign;
        public TourniquetWindlassPhase WindlassPhase => phase;
        public float WindlassDrop => rodDrop;
        public float WindlassSeatDegrees => seatAngle;
        public float WindlassSeatTargetDegrees => Mathf.Abs(seatEntryDegrees);
        public bool IsWindlassSecured => phase == TourniquetWindlassPhase.Secured;
        public float StrapFoldDegrees => strapFold;
        public float StrapCoverDegrees => strapCoverDegrees;
        // Covered: folded fully over the clip now. Fastened: let go validly while covered (final, locked).
        public bool IsStrapCovered => strapFold >= strapCoverDegrees - strapCoverTolerance;
        public bool IsStrapFastened => strapFastened;
        public bool CanRecordTime => timeLabel != null && timeLabel.Interactable != null && timeLabel.Interactable.enabled;
        public bool IsTimeRecorded => timeRecorded;
        // Virtual training record in seconds of the session clock, not a real treatment time.
        public float RecordedElapsedSeconds => recordedSeconds;
        // Session clock for the time record. Until the training runner provides one (chapter 3), the clock starts when the
        // device attaches.
        public Func<float> ElapsedSecondsSource { get; set; }
        public float ElapsedSeconds => ElapsedSecondsSource != null ? ElapsedSecondsSource()
            : device != null && device.IsAttached ? Time.time - sessionStart : 0f;

        // Raised on any state or progress change.
        public event Action<TourniquetOperation> Changed;
        public event Action<TourniquetOperation> BandPrepared;
        public event Action<TourniquetOperation> WindlassSecured;
        public event Action<TourniquetOperation> StrapFastened;
        public event Action<TourniquetOperation> TimeRecorded;

        void Awake()
        {
            band = new PartHandle(TourniquetPart.BandEnd, bandEndHandle);
            windlass = new PartHandle(TourniquetPart.Windlass, windlassHandle);
            strap = new PartHandle(TourniquetPart.SecuringStrap, strapHandle);
            timeLabel = new PartHandle(TourniquetPart.TimeLabel, timeLabelHandle);
            handles.AddRange(new[] { band, windlass, strap, timeLabel });
            foreach (var h in handles)
            {
                var handle = h;
                handle.AttachHandler = () => OnAttachUpdated(handle);
            }
            mapBand = MapBand;
            mapInternalBand = MapInternalBand;
            mapStrap = MapStrap;
            mapLabel = MapLabel;
        }

        void Start()
        {
            partsReady = TryValidate(out var error) && TryPrepareParts(out error);
            if (!partsReady)
            {
                Debug.LogError("[Tourniquet] Part handling disabled: " + error, this);
                LastIssue = TourniquetOperationIssue.PartsUnavailable;
            }
            if (timeRecordText != null)
                timeRecordText.text = string.Empty;
            UpdateGates();
        }

        void OnEnable()
        {
            if (subscribed)
                return;
            foreach (var h in handles)
            {
                if (h.Interactable == null)
                    continue;
                h.Interactable.selectEntered.AddListener(OnPartSelectEntered);
                h.Interactable.selectExited.AddListener(OnPartSelectExited);
            }
            subscribed = true;
        }

        void OnDisable()
        {
            CancelSelections();
            if (subscribed)
            {
                foreach (var h in handles)
                {
                    if (h.Interactable == null)
                        continue;
                    h.Interactable.selectEntered.RemoveListener(OnPartSelectEntered);
                    h.Interactable.selectExited.RemoveListener(OnPartSelectExited);
                }
            }
            subscribed = false;
            foreach (var h in handles)
            {
                UnwatchAttach(h);
                ClearHolder(h);
                h.ReleasePending = false;
            }
        }

        void LateUpdate()
        {
            if (!partsReady)
                return;

            var attached = device.IsAttached;
            if (attached != wasAttached)
            {
                wasAttached = attached;
                // Temporary session clock start until the training runner provides ElapsedSecondsSource.
                if (attached)
                    sessionStart = Time.time;
                Changed?.Invoke(this);
            }

            ResolveRelease(band);
            ResolveRelease(windlass);
            ResolveRelease(strap);
            foreach (var h in handles)
            {
                if (h.Holder != null && h != timeLabel && TryHand(h, out var hand))
                    ApplyHand(h, hand);
            }
            UpdateGates();
        }

        // ---------- Selection ----------

        void OnPartSelectEntered(SelectEnterEventArgs args)
        {
            var h = HandleOf(args.interactableObject);
            if (h == null)
                return;

            // A hand swap arrives as the old hand's exit followed by this enter in the same frame: the pending release of the
            // old hand is dropped, and the new hand starts its own reference (its first frame is never counted).
            UnwatchAttach(h);
            h.Holder = args.interactorObject;
            h.Driver = args.interactorObject.transform != null ? args.interactorObject.transform.GetComponentInParent<TrackedPoseDriver>() : null;
            h.Generation++;
            h.ResetReference();
            h.ReleasePending = false;
            h.ReleaseHasInput = false;
            WatchAttach(h);
            if (h == windlass)
                lowerTravel = 0f;
            SetIssue(TourniquetOperationIssue.None);
            if (h == timeLabel)
                RecordTime(h);
            Changed?.Invoke(this);
        }

        void OnPartSelectExited(SelectExitEventArgs args)
        {
            var h = HandleOf(args.interactableObject);
            if (h == null || args.interactorObject != h.Holder)
                return;

            // Only this frame's attach sample of this grab counts. The attach transform is not read here: NearFar has already reset
            // its offset for the release.
            var frame = Time.frameCount;
            var sampled = h.Attach != null && h.SampleFrame == frame && h.SampleGeneration == h.Generation && h.ConsumedFrame != frame;
            var tracked = TourniquetDevice.IsTracked(h.Driver);
            // A release while suspended (training field confirm) is handled like a canceled selection: nothing completes.
            h.ReleaseIssue = args.isCanceled || Suspended ? TourniquetOperationIssue.SelectionCanceled
                : !tracked || (sampled && !h.SampleTracked) ? TourniquetOperationIssue.TrackingLost
                : h != timeLabel && !sampled ? TourniquetOperationIssue.ReleaseInputUnavailable
                : TourniquetOperationIssue.None;
            h.ReleaseHasInput = h.ReleaseIssue == TourniquetOperationIssue.None && sampled;
            h.ReleasePosition = h.SamplePosition;
            // Judged in LateUpdate, so a same-frame hand swap replaces the release (and drops its input).
            h.ReleasePending = h != timeLabel;
            UnwatchAttach(h);
            ClearHolder(h);
            Changed?.Invoke(this);
        }

        void ResolveRelease(PartHandle h)
        {
            if (!h.ReleasePending)
                return;
            h.ReleasePending = false;
            if (h.Holder != null)
                return;

            // The release frame's last valid motion first (applied once; this handle has no holder, so no held update follows),
            // then the completion check on the updated progress.
            if (h.ReleaseHasInput)
            {
                h.ReleaseHasInput = false;
                h.ConsumedFrame = Time.frameCount;
                ApplyHand(h, h.ReleasePosition);
            }
            h.ResetReference();

            var issue = h.ReleaseIssue;
            if (h == band)
            {
                if (bandPrepared)
                    return;
                if (issue == TourniquetOperationIssue.None)
                {
                    if (bandPull >= bandPreparedProgress)
                    {
                        bandPrepared = true;
                        Changed?.Invoke(this);
                        BandPrepared?.Invoke(this);
                        return;
                    }
                    issue = TourniquetOperationIssue.PullIncomplete;
                }
            }
            else if (h == windlass)
            {
                // Letting go keeps the last pose; only turning fully into the clip secures the rod.
                if (phase == TourniquetWindlassPhase.Secured)
                    return;
                if (issue == TourniquetOperationIssue.None && phase != TourniquetWindlassPhase.Turning)
                    issue = TourniquetOperationIssue.SeatIncomplete;
            }
            else if (h == strap)
            {
                if (strapFastened)
                    return;
                if (issue == TourniquetOperationIssue.None)
                {
                    if (IsStrapCovered)
                    {
                        strapFastened = true;
                        strapFold = strapCoverDegrees;
                        ApplyStrap();
                        Changed?.Invoke(this);
                        StrapFastened?.Invoke(this);
                        return;
                    }
                    issue = TourniquetOperationIssue.CoverIncomplete;
                }
            }

            SetIssue(issue);
            Changed?.Invoke(this);
        }

        void RecordTime(PartHandle h)
        {
            // Recorded once per attempt, only after the windlass is secured and before the strap is covered.
            if (timeRecorded || !device.IsAttached || phase != TourniquetWindlassPhase.Secured || strapFastened)
                return;
            if (!TourniquetDevice.IsTracked(h.Driver))
            {
                SetIssue(TourniquetOperationIssue.TrackingLost);
                return;
            }

            recordedSeconds = ElapsedSeconds;
            timeRecorded = true;
            var total = Mathf.FloorToInt(recordedSeconds);
            timeRecordText.text = string.Format(TrainingSubtitles.TimeLabelFormat, total / 60, total % 60);
            TimeRecorded?.Invoke(this);
        }

        // Each handle takes input only while its step is open: band -> windlass turn and seat -> time record -> securing strap.
        // The time label is open only after the windlass is secured (strap still open, label facing up); the strap only after the
        // time is recorded. Disabling a selected handle ends that selection (canceled).
        void UpdateGates()
        {
            var attached = partsReady && device.IsAttached;
            var secured = phase == TourniquetWindlassPhase.Secured;
            SetHandleEnabled(band, attached && !bandPrepared);
            SetHandleEnabled(windlass, attached && bandPrepared && !secured);
            SetHandleEnabled(timeLabel, attached && secured && !timeRecorded && strapFold < timeLabelMaxFold);
            SetHandleEnabled(strap, attached && secured && timeRecorded && !strapFastened);
        }

        static void SetHandleEnabled(PartHandle h, bool on)
        {
            if (h.Interactable == null || h.Interactable.enabled == on)
                return;
            h.Interactable.enabled = on;
            foreach (var c in h.Interactable.colliders)
            {
                if (c != null)
                    c.enabled = on;
            }
        }

        void WatchAttach(PartHandle h)
        {
            h.Attach = h.Holder is NearFarInteractor nearFar ? nearFar.interactionAttachController as InteractionAttachController : null;
            if (h.Attach != null)
                h.Attach.attachUpdated += h.AttachHandler;
        }

        static void UnwatchAttach(PartHandle h)
        {
            if (h.Attach != null)
                h.Attach.attachUpdated -= h.AttachHandler;
            h.Attach = null;
            h.SampleFrame = -1;
        }

        // Raised at the end of the holder's InteractionAttachController.DoUpdate, before XRI processes selections in the same
        // Update. Reads the attach pose only; nothing is moved and XRI's update is not run again.
        void OnAttachUpdated(PartHandle h)
        {
            if (h.Holder == null || !h.Holder.IsSelecting(h.Interactable))
                return;
            h.SamplePosition = h.Holder.GetAttachTransform(h.Interactable).position;
            h.SampleFrame = Time.frameCount;
            h.SampleGeneration = h.Generation;
            h.SampleTracked = TourniquetDevice.IsTracked(h.Driver);
        }

        // This frame's hand position of the holder, once per frame: the attach sample of this frame and grab, or, for an
        // interactor without an attach controller, its attach transform now. False without input this frame (for example the
        // grab frame itself) or while the hand is not tracked (the gap is never counted).
        bool TryHand(PartHandle h, out Vector3 hand)
        {
            hand = default;
            var frame = Time.frameCount;
            if (h.ConsumedFrame == frame)
                return false;
            // Suspended (training field confirm): the part holds still and its hand reference restarts, as after a tracking gap,
            // so the hand's motion during the dialog is never counted.
            if (Suspended)
            {
                h.ResetReference();
                return false;
            }

            bool tracked;
            Vector3 position;
            if (h.Attach != null)
            {
                if (h.SampleFrame != frame || h.SampleGeneration != h.Generation)
                    return false;
                tracked = h.SampleTracked;
                position = h.SamplePosition;
            }
            else
            {
                tracked = TourniquetDevice.IsTracked(h.Driver);
                position = tracked ? h.Holder.GetAttachTransform(h.Interactable).position : default;
            }
            h.ConsumedFrame = frame;

            if (!tracked)
            {
                h.ResetReference();
                SetIssue(TourniquetOperationIssue.TrackingLost);
                return false;
            }
            if (LastIssue == TourniquetOperationIssue.TrackingLost)
                SetIssue(TourniquetOperationIssue.None);
            hand = position;
            return true;
        }

        // One hand position into the part's progress (held frame or release frame).
        void ApplyHand(PartHandle h, Vector3 hand)
        {
            if (h == band)
                UpdateBand(hand);
            else if (h == windlass)
                UpdateWindlass(hand);
            else if (h == strap)
                UpdateStrap(hand);
        }

        // Hand travel along direction since the last counted sample. Tremor is held back; a jump restarts the reference.
        bool TryTravel(PartHandle h, Vector3 hand, Vector3 direction, out float travel)
        {
            travel = 0f;
            if (!h.HasPosition)
            {
                h.RefPosition = hand;
                h.HasPosition = true;
                return false;
            }

            var step = hand - h.RefPosition;
            if (step.magnitude > maxStepTravel)
            {
                h.RefPosition = hand;
                return false;
            }
            if (step.magnitude < jitterTravel)
                return false;
            travel = Vector3.Dot(step, direction);
            h.RefPosition = hand;
            return true;
        }

        // Signed hand rotation in degrees around the windlass axis since the last counted sample. Continuous across +-180
        // (only the small step between samples is measured). Near the axis the angle is unstable: not counted, reference dropped.
        bool TryTurn(PartHandle h, Vector3 hand, out float degrees)
        {
            degrees = 0f;
            var axis = device.PartTransforms.windlassAxis;
            var around = Vector3.ProjectOnPlane(hand - axis.position, axis.up);
            if (around.magnitude < minAxisDistance)
            {
                h.HasDirection = false;
                return false;
            }
            if (!h.HasDirection)
            {
                h.RefDirection = around;
                h.HasDirection = true;
                return false;
            }

            var step = Vector3.SignedAngle(h.RefDirection, around, axis.up);
            if (Mathf.Abs(step) > maxStepDegrees)
            {
                h.RefDirection = around;
                return false;
            }
            if (Mathf.Abs(step) < jitterDegrees)
                return false;
            degrees = step;
            h.RefDirection = around;
            return true;
        }

        // ---------- Band end ----------

        void UpdateBand(Vector3 hand)
        {
            if (bandPrepared)
                return;
            if (!TryTravel(band, hand, device.PartTransforms.bandEnd.forward, out var travel))
                return;

            // Pulling back toward the buckle reduces the progress; it never goes below 0 or above 1.
            var pull = Mathf.Clamp01(bandPull + travel / bandPullHandTravel);
            if (pull == bandPull)
                return;
            bandPull = pull;
            ApplyBand();
            Changed?.Invoke(this);
        }

        void ApplyBand()
        {
            bandEndHandle.transform.localPosition = bandHandleRest + Vector3.forward * (bandPull * bandVisualPull);
            if (bandPull <= 0f)
                bandEndVisual.Restore();
            else
                bandEndVisual.Apply(mapBand);
        }

        // Band end visual space = Pivot_BandEnd space (+Z pull, tip at 0). The buckle side stays, the free end moves along +Z,
        // with a smooth blend between, so the band stays connected at the buckle.
        void MapBand(Vector3 p, out Vector3 position, out Quaternion rotation)
        {
            var weight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(bandFixedUntilZ, bandFreeFromZ, p.z));
            position = p + Vector3.forward * (bandPull * bandVisualPull * weight);
            rotation = Quaternion.identity;
        }

        // ---------- Windlass ----------

        void UpdateWindlass(Vector3 hand)
        {
            if (phase == TourniquetWindlassPhase.Secured)
                return;

            var turned = TryTurn(windlass, hand, out var degrees);
            var moved = TryTravel(windlass, hand, device.PartTransforms.windlassAxis.up, out var rise);
            var changed = false;
            switch (phase)
            {
                case TourniquetWindlassPhase.Turning:
                    if (turned)
                        changed |= Turn(degrees);
                    if (moved)
                        changed |= TryStartLowering(rise);
                    break;
                case TourniquetWindlassPhase.Lowering:
                    if (moved)
                        changed |= Lower(rise);
                    break;
                case TourniquetWindlassPhase.Lowered:
                    if (turned)
                        changed |= Enter(degrees);
                    // Back up the entry path only from the entry angle, never through the clip roof.
                    if (moved && seatAngle <= 0f && rise > 0f)
                        changed |= Lower(rise);
                    break;
            }
            if (!changed)
                return;

            ApplyWindlass();
            Changed?.Invoke(this);
            if (phase == TourniquetWindlassPhase.Secured)
                WindlassSecured?.Invoke(this);
        }

        bool Turn(float degrees)
        {
            // The first counted turn sets the virtual tightening direction; the reverse direction reduces the progress.
            if (tightenSign == 0)
                tightenSign = degrees > 0f ? 1 : -1;
            var progress = Mathf.Clamp(turnDegrees + degrees * tightenSign, 0f, requiredTurnDegrees * 2f);
            if (progress == turnDegrees)
                return false;
            turnDegrees = progress;
            rodAngle = progress * tightenSign;
            return true;
        }

        bool TryStartLowering(float rise)
        {
            var nearEntry = Mathf.Abs(Mathf.DeltaAngle(EntryAngle(rodAngle), rodAngle)) <= entryWindowDegrees;
            if (!nearEntry || rise >= 0f)
            {
                lowerTravel = 0f;
                return false;
            }

            lowerTravel -= rise;
            if (lowerTravel < lowerStartTravel)
                return false;
            if (turnDegrees < requiredTurnDegrees)
            {
                SetIssue(TourniquetOperationIssue.TurnIncomplete);
                lowerTravel = 0f;
                return false;
            }

            // Restricted entry path: beside the clip, then straight down along the axis (clear of the clip roof). The rod
            // aligns to the exact entry angle over the first millimetres, above the roof, instead of jumping to it.
            entryAngle = EntryAngle(rodAngle);
            lowerStartAngle = rodAngle;
            phase = TourniquetWindlassPhase.Lowering;
            Lower(-lowerTravel);
            lowerTravel = 0f;
            return true;
        }

        bool Lower(float rise)
        {
            var drop = Mathf.Clamp(rodDrop - rise, 0f, seatDrop);
            if (drop == rodDrop)
                return false;
            rodDrop = drop;
            phase = drop <= 0f ? TourniquetWindlassPhase.Turning
                : drop >= seatDrop ? TourniquetWindlassPhase.Lowered
                : TourniquetWindlassPhase.Lowering;
            // Back at the turning height the rod returns to its turned angle; the turn progress was never changed.
            rodAngle = Mathf.Lerp(lowerStartAngle, entryAngle, rodDrop / Mathf.Min(AlignDrop, seatDrop));
            return true;
        }

        bool Enter(float degrees)
        {
            var target = Mathf.Abs(seatEntryDegrees);
            var side = Mathf.Sign(seatEntryDegrees);
            var angle = Mathf.Clamp(seatAngle + degrees * side, 0f, target);
            if (angle >= target - 0.5f)
                angle = target;
            if (angle == seatAngle)
                return false;
            seatAngle = angle;
            rodAngle = entryAngle + seatAngle * side;
            if (seatAngle >= target)
                phase = TourniquetWindlassPhase.Secured;
            return true;
        }

        // Drop in metres over which the rod aligns to the entry angle; the turning height clears the clip roof by about 2 mm.
        const float AlignDrop = 0.002f;

        // Rod angles beside the clip from which the entry path starts (the rod is symmetric: every 180 degrees).
        static float EntryAngle(float angle) => Mathf.Round(angle / 180f) * 180f;

        void ApplyWindlass()
        {
            var windlassPart = device.PartTransforms.windlass;
            windlassPart.localRotation = Quaternion.AngleAxis(rodAngle, Vector3.up) * windlassRestRotation;
            windlassPart.localPosition = windlassRestPosition - Vector3.up * rodDrop;
            if (rodAngle == 0f && rodDrop <= 0f)
                bodyVisual.Restore();
            else
                bodyVisual.Apply(mapInternalBand, internalBand);
        }

        // Body visual space. Below the fixed depth the internal band stays on the body; toward the rod it turns with the rod
        // angle (fully inside the slot), and lowering compresses it between its fixed base and the rod, so the band in the
        // slot keeps the rod's pose instead of passing through it.
        void MapInternalBand(Vector3 p, out Vector3 position, out Quaternion rotation)
        {
            var relative = p - axisInBody;
            var height = Vector3.Dot(relative, axisDirectionInBody);
            var fixedHeight = -internalBandFixedDepth;
            if (height <= fixedHeight)
            {
                position = p;
                rotation = Quaternion.identity;
                return;
            }

            var weight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fixedHeight, -internalBandFollowDepth, height));
            var compress = (internalBandFixedDepth - rodDrop) / internalBandFixedDepth;
            relative += axisDirectionInBody * ((fixedHeight + (height - fixedHeight) * compress) - height);
            rotation = Quaternion.AngleAxis(rodAngle * weight, axisDirectionInBody);
            position = axisInBody + rotation * relative;
        }

        // ---------- Securing strap and time label ----------

        void UpdateStrap(Vector3 hand)
        {
            if (strapFastened)
                return;

            // Direction the free end moves for a growing fold (up and back over the hinge), at the strap length.
            var radians = strapFold * Mathf.Deg2Rad;
            var direction = device.PartTransforms.securingStrap.TransformDirection(new Vector3(0f, Mathf.Cos(radians), -Mathf.Sin(radians)));
            if (!TryTravel(strap, hand, direction, out var travel))
                return;
            var fold = Mathf.Clamp(strapFold + travel / strapLength * Mathf.Rad2Deg, 0f, strapCoverDegrees);
            if (fold == strapFold)
                return;
            strapFold = fold;
            ApplyStrap();
            Changed?.Invoke(this);
        }

        void ApplyStrap()
        {
            var strapFrame = device.PartTransforms.securingStrap;
            var surface = device.PartTransforms.timeLabelSurface;
            if (strapFold <= 0f)
            {
                strapVisual.Restore();
                timeLabelVisual.Restore();
                surface.SetLocalPositionAndRotation(surfaceRestLocal.position, surfaceRestLocal.rotation);
                strapHandle.transform.SetLocalPositionAndRotation(strapHandleRest.position, strapHandleRest.rotation);
                return;
            }

            foldRadians = strapFold * Mathf.Deg2Rad;
            strapVisual.Apply(mapStrap);
            timeLabelVisual.Apply(mapLabel);
            // The writing surface (and the record text on it) and the strap handle take the bent pose of their point.
            var surfacePosition = Bend(surfaceInStrap.position, out var turn);
            surface.SetPositionAndRotation(strapFrame.TransformPoint(surfacePosition), strapFrame.rotation * (turn * surfaceInStrap.rotation));
            var handlePosition = Bend(strapHandleRest.position, out turn);
            strapHandle.transform.SetLocalPositionAndRotation(handlePosition, turn * strapHandleRest.rotation);
        }

        // Strap space (Part_SecuringStrap: hinge at 0, +Z along the strap, +Y out of its face). Points run around a circle of
        // strapBendRadius above the hinge for the fold angle and continue straight; the face offset follows the turned
        // normal. The strap is bent, not turned as a rigid plate, so it rises over the hinge instead of sweeping through the clip.
        Vector3 Bend(Vector3 p, out Quaternion turn)
        {
            if (p.z <= 0f)
            {
                turn = Quaternion.identity;
                return p;
            }

            var radius = strapBendRadius;
            var theta = Mathf.Min(p.z / radius, foldRadians);
            var sin = Mathf.Sin(theta);
            var cos = Mathf.Cos(theta);
            var y = radius - radius * cos;
            var z = radius * sin;
            var straight = p.z - radius * foldRadians;
            if (straight > 0f)
            {
                y += straight * sin;
                z += straight * cos;
            }

            turn = Quaternion.AngleAxis(-theta * Mathf.Rad2Deg, Vector3.right);
            return new Vector3(p.x, y + p.y * cos, z - p.y * sin);
        }

        void MapStrap(Vector3 p, out Vector3 position, out Quaternion rotation)
        {
            position = strapToStrapVisual.MultiplyPoint3x4(Bend(strapVisualToStrap.MultiplyPoint3x4(p), out var turn));
            rotation = Quaternion.Inverse(strapVisualToStrapRotation) * turn * strapVisualToStrapRotation;
        }

        // The time patch is a sibling of the strap: bent with the same strap-space mapping, so both move as one.
        void MapLabel(Vector3 p, out Vector3 position, out Quaternion rotation)
        {
            position = strapToLabelVisual.MultiplyPoint3x4(Bend(labelVisualToStrap.MultiplyPoint3x4(p), out var turn));
            rotation = Quaternion.Inverse(labelToStrapRotation) * turn * labelToStrapRotation;
        }

        // ---------- Reset ----------

        // Restores the device placement (TourniquetDevice.ResetPlacement), every part pose and work-copy mesh, the progress,
        // the time record and the session clock to the scene start. Entry point for a later retry; no UI calls it yet.
        public void ResetToInitial()
        {
            CancelSelections();
            foreach (var h in handles)
            {
                UnwatchAttach(h);
                ClearHolder(h);
                h.ResetReference();
                h.ReleasePending = false;
                h.ReleaseHasInput = false;
            }

            if (device != null)
                device.ResetPlacement();
            wasAttached = false;
            sessionStart = 0f;
            bandPull = 0f;
            bandPrepared = false;
            rodAngle = 0f;
            tightenSign = 0;
            turnDegrees = 0f;
            phase = TourniquetWindlassPhase.Turning;
            rodDrop = 0f;
            seatAngle = 0f;
            entryAngle = 0f;
            lowerStartAngle = 0f;
            lowerTravel = 0f;
            strapFold = 0f;
            strapFastened = false;
            timeRecorded = false;
            recordedSeconds = 0f;
            if (timeRecordText != null)
                timeRecordText.text = string.Empty;
            if (partsReady)
            {
                LastIssue = TourniquetOperationIssue.None;
                ApplyBand();
                ApplyWindlass();
                ApplyStrap();
            }
            UpdateGates();
            Changed?.Invoke(this);
        }

        // ---------- Helpers ----------

        void CancelSelections()
        {
            foreach (var h in handles)
            {
                var interactable = h.Interactable;
                if (interactable != null && interactable.isSelected && interactable.interactionManager != null)
                    interactable.interactionManager.CancelInteractableSelection((IXRSelectInteractable)interactable);
            }
        }

        static void ClearHolder(PartHandle h)
        {
            h.Holder = null;
            h.Driver = null;
        }

        PartHandle HandleOf(IXRInteractable interactable)
        {
            foreach (var h in handles)
            {
                if (h.Interactable != null && (IXRInteractable)h.Interactable == interactable)
                    return h;
            }
            return null;
        }

        void SetIssue(TourniquetOperationIssue issue)
        {
            if (LastIssue == TourniquetOperationIssue.PartsUnavailable || LastIssue == issue)
                return;
            LastIssue = issue;
            Changed?.Invoke(this);
        }

        bool TryPrepareParts(out string error)
        {
            foreach (var deform in new[] { bandEndVisual, bodyVisual, strapVisual, timeLabelVisual })
            {
                if (!deform.TryPrepare(out error))
                    return false;
            }

            internalBand = bodyVisual.SubMeshVertices(internalBandSubMesh);
            if (internalBand.Length == 0)
            {
                error = $"TourniquetOperation '{name}': body visual has no vertices in internal band sub-mesh {internalBandSubMesh}.";
                return false;
            }

            var parts = device.PartTransforms;
            windlassRestPosition = parts.windlass.localPosition;
            windlassRestRotation = parts.windlass.localRotation;
            bandHandleRest = bandEndHandle.transform.localPosition;
            strapHandleRest = new Pose(strapHandle.transform.localPosition, strapHandle.transform.localRotation);
            var surface = parts.timeLabelSurface;
            surfaceRestLocal = new Pose(surface.localPosition, surface.localRotation);
            var strapFrame = parts.securingStrap;
            surfaceInStrap = new Pose(strapFrame.InverseTransformPoint(surface.position), Quaternion.Inverse(strapFrame.rotation) * surface.rotation);
            strapVisualToStrap = strapFrame.worldToLocalMatrix * strapVisual.transform.localToWorldMatrix;
            strapToStrapVisual = strapVisualToStrap.inverse;
            labelVisualToStrap = strapFrame.worldToLocalMatrix * timeLabelVisual.transform.localToWorldMatrix;
            strapToLabelVisual = labelVisualToStrap.inverse;
            strapVisualToStrapRotation = Quaternion.Inverse(strapFrame.rotation) * strapVisual.transform.rotation;
            labelToStrapRotation = Quaternion.Inverse(strapFrame.rotation) * timeLabelVisual.transform.rotation;
            var body = bodyVisual.transform;
            axisInBody = body.InverseTransformPoint(parts.windlassAxis.position);
            axisDirectionInBody = body.InverseTransformDirection(parts.windlassAxis.up).normalized;
            error = null;
            return true;
        }

        public bool TryValidate(out string error)
        {
            var missing = new List<string>();
            if (device == null)
                missing.Add(nameof(device));
            if (bandEndHandle == null)
                missing.Add(nameof(bandEndHandle));
            if (windlassHandle == null)
                missing.Add(nameof(windlassHandle));
            if (strapHandle == null)
                missing.Add(nameof(strapHandle));
            if (timeLabelHandle == null)
                missing.Add(nameof(timeLabelHandle));
            if (bandEndVisual == null)
                missing.Add(nameof(bandEndVisual));
            if (bodyVisual == null)
                missing.Add(nameof(bodyVisual));
            if (strapVisual == null)
                missing.Add(nameof(strapVisual));
            if (timeLabelVisual == null)
                missing.Add(nameof(timeLabelVisual));
            if (timeRecordText == null)
                missing.Add(nameof(timeRecordText));
            if (device != null)
            {
                var parts = device.PartTransforms;
                if (parts.bandEnd == null || parts.windlassAxis == null || parts.windlass == null || parts.securingStrap == null || parts.timeLabelSurface == null)
                    missing.Add("device parts");
            }

            error = missing.Count > 0 ? $"TourniquetOperation '{name}' missing references: {string.Join(", ", missing)}." : null;
            if (error == null)
            {
                foreach (var handle in new[] { bandEndHandle, windlassHandle, strapHandle, timeLabelHandle })
                {
                    if (handle.colliders.Count == 0 || handle.colliders.Exists(c => c == null))
                        error = $"TourniquetOperation '{name}': handle '{handle.name}' needs its own collider in its colliders list.";
                }
            }
            return error == null;
        }
    }
}
