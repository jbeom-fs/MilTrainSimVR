using System;
using System.Collections.Generic;
using MilTrainSimVR.B.Interaction;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MilTrainSimVR.B.BleedingControl.Tourniquet
{
    public enum TourniquetState
    {
        Unattached = 0,
        HeldBlocked = 1,
        HeldPlaceable = 2,
        Attached = 3
    }

    // Prototype tourniquet, chapter 1: body grab with either hand (one hand at a time), placement candidate while held, and
    // attachment to the designated limb only on a valid, tracked user release. Not a reproduction of a specific product.
    // Release reference pose: XRI ends the selection in its Update before it moves interactables, so on the release frame the
    // transform still holds the previous frame's pose, and the NearFar interactor resets its attach offset (OnSelectExiting)
    // before selectExited arrives. Earlier in the same Update, NearFarInteractor.PreprocessInteractor runs its
    // InteractionAttachController.DoUpdate (stabilisation, distance-based velocity scaling, push/pull and rotate input) and the
    // controller raises its public attachUpdated event. While held, this component listens to that event on the holding
    // interactor and samples, per frame, the updated interactor attach pose and the device root in the frame of its dynamic grab
    // attach. On release it uses only a sample of this frame and this interactor: root = sampled attach pose x root-in-grab-attach,
    // which is what the default single-grab transformer (position and rotation tracking) would have produced from that attach.
    // One writer at a time: XRI moves the transform while selected; this component writes it once, in LateUpdate of the release
    // frame, after XRInteractionManager's Late phase (Drop and Detach done): to the candidate when the release pose is valid,
    // otherwise to the release pose itself, so the judged pose and the resting pose agree.
    // Far grab: the B rig attaches a ray grab near the hand and XRI eases the body to the hand with its rotation kept (dynamic
    // attach, attach ease-in). While it is still being brought (GrabBring, read in LateUpdate) no placement candidate is shown and a
    // release meanwhile never attaches; the body rests where XRI left it.
    // Band, windlass, clip, securing strap and time label are driven after attaching by TourniquetOperation (chapter 2); this
    // component only judges their volumes, including the current shape of deformed part visuals.
    // The preview and the status board read this state; this component never calls training or UI code.
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class TourniquetDevice : MonoBehaviour
    {
        [Serializable]
        public struct Parts
        {
            [Tooltip("Fixed body (base plate) the band and windlass are mounted on.")]
            public Transform body;
            public Transform band;
            [Tooltip("Pivot at the free band end; forward is the pull direction.")]
            public Transform bandEnd;
            [Tooltip("Windlass rotation pivot; up is the rotation axis.")]
            public Transform windlassAxis;
            public Transform windlass;
            [Tooltip("Clip that holds the windlass once turned.")]
            public Transform windlassClip;
            [Tooltip("Securing strap (finish part).")]
            public Transform securingStrap;
            [Tooltip("Time label writing surface; up is the surface normal.")]
            public Transform timeLabelSurface;
        }

        [Serializable]
        public struct JudgeBox
        {
            [Tooltip("Part transform the box is defined in; its position, rotation and scale apply.")]
            public Transform frame;
            public Vector3 center;
            public Vector3 size;
        }

        const int BandSegments = 24;

        [SerializeField] XRGrabInteractable grab;
        [SerializeField] Rigidbody body;
        [Tooltip("Body grab reference, the XRGrabInteractable attach transform.")]
        [SerializeField] Transform grabPoint;
        [Tooltip("Limb attach reference at the band loop centre. Forward runs through the loop (along the limb toward its distal end " +
                 "when placed); up points out of the body face.")]
        [SerializeField] Transform limbAttachPoint;
        [Tooltip("Designated limb in this scene.")]
        [SerializeField] TourniquetLimbTarget target;
        [Header("Zone check volumes (geometry only, no colliders)")]
        [Tooltip("Inner radius in metres of the band loop around parts.band (+Z axis). Prototype shape, not a product size.")]
        [SerializeField, Min(0.01f)] float bandLoopRadius = 0.06f;
        [SerializeField, Min(0.0005f)] float bandThickness = 0.003f;
        [Tooltip("Band width in metres along the loop axis.")]
        [SerializeField, Min(0.001f)] float bandWidth = 0.038f;
        [Tooltip("Boxes of the solid parts (body, band end, windlass, clip, securing strap, time label). The band loop is added as " +
                 "segment boxes from the values above, so the loop opening stays free.")]
        [SerializeField] JudgeBox[] judgeBoxes = Array.Empty<JudgeBox>();
        [Tooltip("Named part pivots of the model, operated after attaching by TourniquetOperation.")]
        [SerializeField] Parts parts;

        XRGrabInteractable subscribedGrab;
        IXRSelectInteractor holding;
        TrackedPoseDriver holdingDriver;
        // Attach controller of the holding NearFar interactor, subscribed while held.
        InteractionAttachController holdingAttach;
        // Sample taken when XRI finished updating the holding interactor's attach this frame (before selection processing).
        IXRSelectInteractor sampleInteractor;
        int sampleFrame = -1;
        Pose sampleAttachPose;
        Pose sampleRootInGrabAttach;
        bool sampleTracked;
        bool releasePending;
        // Still being brought to the hand after a far grab, as of this holder's last LateUpdate.
        bool bringing;
        TourniquetPlacementCheck releaseCheck;
        bool hasReleasePose;
        Pose releasePose;
        // Scene placement captured on Awake, restored by ResetPlacement.
        Transform initialParent;
        Vector3 initialLocalPosition;
        Quaternion initialLocalRotation;
        InteractionLayerMask initialLayers;
        RigidbodyInterpolation initialInterpolation;
        // Per judge box: deformer of its part visual (null for rigid parts) and the source vertices inside the box.
        TourniquetMeshDeform[] boxDeforms;
        int[][] boxVertices;

        public TourniquetState State { get; private set; }
        // Check of the held pose, updated every frame while held; None otherwise.
        public TourniquetPlacementCheck CurrentCheck { get; private set; }
        // Reason of the last release that did not attach; None before any failure and after attaching.
        public TourniquetPlacementCheck LastFailure { get; private set; }
        public bool IsHeld => holding != null;
        public bool IsAttached => State == TourniquetState.Attached;
        public TourniquetLimbTarget Target => target;
        public TourniquetLimbTarget AttachedTarget { get; private set; }
        public InteractorHandedness GrabbingHand => holding != null ? holding.handedness : InteractorHandedness.None;
        // Root pose the tourniquet would take on the limb; valid only while HasCandidate.
        public bool HasCandidate { get; private set; }
        public Pose CandidateRootPose { get; private set; }
        // Set by a training field while it asks whether to stop the course (its confirm dialog). A held device stays held; no
        // placement candidate is shown and a release meanwhile never attaches (it is handled like a canceled selection: it rests
        // where XRI left it). Not saved; false by default.
        public bool Suspended { get; set; }
        public Parts PartTransforms => parts;
        public Transform GrabPoint => grabPoint;
        public Transform LimbAttachPoint => limbAttachPoint;
        public float BandLoopRadius => bandLoopRadius;
        public float BandThickness => bandThickness;
        public float BandWidth => bandWidth;
        public IReadOnlyList<JudgeBox> JudgeBoxes => judgeBoxes;
        // Limb attach point in the root frame (root scale is kept at 1).
        public Pose AttachLocalPose => new Pose(Quaternion.Inverse(transform.rotation) * (limbAttachPoint.position - transform.position),
                                                Quaternion.Inverse(transform.rotation) * limbAttachPoint.rotation);
        // Raised on state changes and when LastFailure changes after a release.
        public event Action<TourniquetDevice> StateChanged;
        // Raised once, when the tourniquet attaches to AttachedTarget.
        public event Action<TourniquetDevice> Attached;

        void Awake()
        {
            initialParent = transform.parent;
            initialLocalPosition = transform.localPosition;
            initialLocalRotation = transform.localRotation;
            if (grab != null)
                initialLayers = grab.interactionLayers;
            if (body != null)
                initialInterpolation = body.interpolation;
        }

        void OnEnable()
        {
            if (subscribedGrab == null && grab != null)
            {
                subscribedGrab = grab;
                subscribedGrab.selectEntered.AddListener(OnSelectEntered);
                subscribedGrab.selectExited.AddListener(OnSelectExited);
            }

            // Resync with a selection that survived while only this component was disabled.
            if (holding == null && !IsAttached && grab != null && grab.isSelected)
                BeginHold(grab.firstInteractorSelecting);
        }

        void OnDisable()
        {
            // Drop the real XR selection too, so internal and XRI state cannot drift apart. That release is canceled: no attach.
            if (grab != null && grab.isSelected && grab.interactionManager != null)
                grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
            if (subscribedGrab != null)
            {
                subscribedGrab.selectEntered.RemoveListener(OnSelectEntered);
                subscribedGrab.selectExited.RemoveListener(OnSelectExited);
            }

            subscribedGrab = null;
            UnwatchAttach();
            ClearSample();
            holding = null;
            holdingDriver = null;
            releasePending = false;
            hasReleasePose = false;
            ClearCandidate();
        }

        void LateUpdate()
        {
            if (releasePending)
            {
                releasePending = false;
                ResolveRelease();
            }
            else if (holding != null)
            {
                // After XRI's update of this frame (GrabBring).
                bringing = GrabBring.IsBringing(grab);
                UpdateCandidate();
            }
        }

        void OnSelectEntered(SelectEnterEventArgs args) => BeginHold(args.interactorObject);

        void BeginHold(IXRSelectInteractor interactor)
        {
            if (IsAttached)
                return;

            // A hand-over (the other hand grabbing in the same frame) replaces the pending release: nothing to judge.
            releasePending = false;
            hasReleasePose = false;
            holding = interactor;
            holdingDriver = interactor != null && interactor.transform != null ? interactor.transform.GetComponentInParent<TrackedPoseDriver>() : null;
            // Samples of a previous hand or grab are never reused. The first sample of this grab comes from the next frame's
            // attach update; a release always happens on a later frame than the grab.
            ClearSample();
            WatchAttach(interactor);
            // Until this grab's first LateUpdate has measured it, the body counts as still being brought.
            bringing = true;
            UpdateCandidate();
        }

        void OnSelectExited(SelectExitEventArgs args)
        {
            if (holding == null || args.interactorObject != holding)
                return;

            var tracked = IsTracked(holdingDriver);
            // Only this frame's sample of this interactor counts. The interactor's attach transform is not read here: NearFar
            // has already reset its offset, and the dynamic grab attach is released right after this event.
            var sampled = sampleInteractor == holding && sampleFrame == Time.frameCount;
            // Let go while still being brought from a distance: not a placement (no failure either); it rests where XRI left it.
            hasReleasePose = !args.isCanceled && !Suspended && tracked && sampled && sampleTracked && !bringing;
            if (hasReleasePose)
                releasePose = sampleRootInGrabAttach.GetTransformedBy(sampleAttachPose);
            // A canceled selection (tracking loss handling, disable, unload), a release while suspended, an untracked hand or a
            // missing sample never attaches.
            releaseCheck = args.isCanceled || Suspended ? TourniquetPlacementCheck.SelectionCanceled
                : !tracked || (sampled && !sampleTracked) ? TourniquetPlacementCheck.TrackingLost
                : !sampled ? TourniquetPlacementCheck.ReleasePoseUnavailable
                : TourniquetPlacementCheck.None;
            UnwatchAttach();
            ClearSample();
            holding = null;
            holdingDriver = null;
            ClearCandidate();
            if (IsAttached)
                return;

            // Judged and applied in LateUpdate, after XRI finished the drop of this frame.
            releasePending = true;
            CurrentCheck = TourniquetPlacementCheck.None;
            SetState(TourniquetState.Unattached);
        }

        void ResolveRelease()
        {
            if (IsAttached || grab.isSelected)
                return;

            if (hasReleasePose)
            {
                // The release pose is the only pose judged; the previous frame's candidate and transform are not used.
                hasReleasePose = false;
                var placement = target.Evaluate(releasePose, this);
                if (placement.Check == TourniquetPlacementCheck.Valid)
                {
                    Attach(placement.RootPose);
                    return;
                }

                // Not attached: rest at the judged pose (XRI stopped moving the device one frame earlier).
                SetPose(releasePose);
                releaseCheck = placement.Check;
            }

            // The device keeps the prefab's physics (XRI restored it on drop) and can be grabbed again.
            LastFailure = releaseCheck;
            StateChanged?.Invoke(this);
        }

        void Attach(Pose rootPose)
        {
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            body.isKinematic = true;
            // Interpolation would pull the transform back toward the pre-snap physics pose.
            body.interpolation = RigidbodyInterpolation.None;
            SetPose(rootPose);
            // Body move grab locked from now on: no throw, no reselect. Later chapters add their own part interactables.
            grab.throwOnDetach = false;
            grab.interactionLayers = 0;
            // Kept as a local placement in the limb frame.
            transform.SetParent(target.transform, true);

            AttachedTarget = target;
            LastFailure = TourniquetPlacementCheck.None;
            CurrentCheck = TourniquetPlacementCheck.None;
            SetState(TourniquetState.Attached);
            Attached?.Invoke(this);
        }

        // Back to the scene's initial placement: unattached, body grab restored, no failure shown. Part shapes and progress are
        // reset by TourniquetOperation.ResetToInitial, which calls this; no retry UI is connected in this chapter.
        public void ResetPlacement()
        {
            // The canceled selection never attaches; its pending release is dropped below.
            if (grab.isSelected && grab.interactionManager != null)
                grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
            UnwatchAttach();
            ClearSample();
            holding = null;
            holdingDriver = null;
            releasePending = false;
            hasReleasePose = false;
            ClearCandidate();

            transform.SetParent(initialParent, false);
            transform.SetLocalPositionAndRotation(initialLocalPosition, initialLocalRotation);
            body.position = transform.position;
            body.rotation = transform.rotation;
            body.interpolation = initialInterpolation;
            grab.interactionLayers = initialLayers;
            AttachedTarget = null;
            LastFailure = TourniquetPlacementCheck.None;
            CurrentCheck = TourniquetPlacementCheck.None;
            if (State != TourniquetState.Unattached)
                SetState(TourniquetState.Unattached);
            else
                StateChanged?.Invoke(this);
        }

        // Transform and Rigidbody pose together; physics settings are left as they are (the prefab's after a drop).
        void SetPose(Pose pose)
        {
            transform.SetPositionAndRotation(pose.position, pose.rotation);
            body.position = pose.position;
            body.rotation = pose.rotation;
        }

        void UpdateCandidate()
        {
            if (Suspended || bringing)
            {
                CurrentCheck = TourniquetPlacementCheck.None;
                ClearCandidate();
                SetState(TourniquetState.HeldBlocked);
                return;
            }
            if (!IsTracked(holdingDriver))
            {
                CurrentCheck = TourniquetPlacementCheck.TrackingLost;
                ClearCandidate();
                SetState(TourniquetState.HeldBlocked);
                return;
            }

            var placement = EvaluateCurrent();
            CurrentCheck = placement.Check;
            HasCandidate = placement.HasCandidate;
            CandidateRootPose = placement.HasCandidate ? placement.RootPose : default;
            SetState(placement.Check == TourniquetPlacementCheck.Valid ? TourniquetState.HeldPlaceable : TourniquetState.HeldBlocked);
        }

        TourniquetPlacement EvaluateCurrent() => target.Evaluate(new Pose(transform.position, transform.rotation), this);

        void WatchAttach(IXRSelectInteractor interactor)
        {
            UnwatchAttach();
            holdingAttach = interactor is NearFarInteractor nearFar ? nearFar.interactionAttachController as InteractionAttachController : null;
            if (holdingAttach != null)
                holdingAttach.attachUpdated += OnAttachUpdated;
        }

        void UnwatchAttach()
        {
            if (holdingAttach != null)
                holdingAttach.attachUpdated -= OnAttachUpdated;
            holdingAttach = null;
        }

        // Raised at the end of InteractionAttachController.DoUpdate in NearFarInteractor.PreprocessInteractor (Dynamic), before
        // XRI evaluates selections in the same Update. Reads poses only; nothing is moved or updated again.
        void OnAttachUpdated()
        {
            if (holding == null || !holding.IsSelecting(grab))
                return;

            var attach = holding.GetAttachTransform(grab);
            var grabAttach = grab.GetAttachTransform(holding);
            var inverse = Quaternion.Inverse(grabAttach.rotation);
            sampleInteractor = holding;
            sampleFrame = Time.frameCount;
            sampleAttachPose = new Pose(attach.position, attach.rotation);
            // The dynamic grab attach is a child of this root and fixed during the grab, so this relation is the transformer's
            // grab offset (root scale 1).
            sampleRootInGrabAttach = new Pose(inverse * (transform.position - grabAttach.position), inverse * transform.rotation);
            sampleTracked = IsTracked(holdingDriver);
        }

        void ClearSample()
        {
            sampleInteractor = null;
            sampleFrame = -1;
        }

        // Root pose that puts the limb attach point on attachPose (data only).
        public Pose RootPoseFor(Pose attachPose)
        {
            var local = AttachLocalPose;
            var rotation = attachPose.rotation * Quaternion.Inverse(local.rotation);
            return new Pose(attachPose.position - rotation * local.position, rotation);
        }

        // World boxes of the device parts as if its root were at rootPose; the device itself is not moved.
        internal void CollectVolumes(Pose rootPose, List<TourniquetBox> into)
        {
            into.Clear();
            var toPose = Matrix4x4.TRS(rootPose.position, rootPose.rotation, Vector3.one) *
                         Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one).inverse;
            for (var i = 0; i < judgeBoxes.Length; i++)
            {
                var frame = judgeBoxes[i].frame;
                if (frame == null)
                    continue;
                CurrentBox(i, out var partCenter, out var partSize);
                into.Add(TourniquetBox.FromMatrix(toPose * frame.localToWorldMatrix, partCenter, partSize));
            }

            if (parts.band == null)
                return;
            // Band loop as segment boxes, each covering one annular sector of the band (width and thickness); loop opening free.
            var band = toPose * Matrix4x4.TRS(parts.band.position, parts.band.rotation, Vector3.one);
            var half = Mathf.PI / BandSegments;
            var inner = bandLoopRadius * Mathf.Cos(half);
            var outer = bandLoopRadius + bandThickness;
            var bandSegmentSize = new Vector3(outer - inner, 2f * outer * Mathf.Sin(half), bandWidth);
            for (var i = 0; i < BandSegments; i++)
            {
                var segment = Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, (2f * i + 1f) * half * Mathf.Rad2Deg));
                into.Add(TourniquetBox.FromMatrix(band * segment, new Vector3((inner + outer) * 0.5f, 0f, 0f), bandSegmentSize));
            }
        }

        // Judge box i as serialized, or, while its part visual is deformed, the box around the deformed vertices that were inside
        // the serialized box in the source shape (the box follows a pulled band end or a folded strap).
        void CurrentBox(int i, out Vector3 center, out Vector3 size)
        {
            var box = judgeBoxes[i];
            center = box.center;
            size = box.size;
            if (boxDeforms == null || boxDeforms.Length != judgeBoxes.Length)
            {
                boxDeforms = new TourniquetMeshDeform[judgeBoxes.Length];
                boxVertices = new int[judgeBoxes.Length][];
                for (var k = 0; k < judgeBoxes.Length; k++)
                    boxDeforms[k] = judgeBoxes[k].frame != null ? judgeBoxes[k].frame.GetComponent<TourniquetMeshDeform>() : null;
            }

            var deform = boxDeforms[i];
            if (deform == null || !deform.IsDeformed)
                return;
            boxVertices[i] ??= VerticesInside(deform.BaseVertices, box);
            var indices = boxVertices[i];
            if (indices.Length == 0)
                return;

            var current = deform.Vertices;
            var min = current[indices[0]];
            var max = min;
            foreach (var index in indices)
            {
                min = Vector3.Min(min, current[index]);
                max = Vector3.Max(max, current[index]);
            }
            center = (min + max) * 0.5f;
            size = Vector3.Max(max - min, Vector3.one * 0.0005f);
        }

        static int[] VerticesInside(IReadOnlyList<Vector3> vertices, JudgeBox box)
        {
            var half = box.size * 0.5f + Vector3.one * 0.0005f;
            var inside = new List<int>();
            for (var i = 0; i < vertices.Count; i++)
            {
                var d = vertices[i] - box.center;
                if (Mathf.Abs(d.x) <= half.x && Mathf.Abs(d.y) <= half.y && Mathf.Abs(d.z) <= half.z)
                    inside.Add(i);
            }
            return inside.ToArray();
        }

        void ClearCandidate()
        {
            HasCandidate = false;
            CandidateRootPose = default;
        }

        void SetState(TourniquetState state)
        {
            if (State == state)
                return;
            State = state;
            StateChanged?.Invoke(this);
        }

        // Position and rotation both tracked by a live pose driver; a transform alone is not tracking. Shared with part handling.
        internal static bool IsTracked(TrackedPoseDriver driver)
        {
            if (driver == null || !driver.isActiveAndEnabled)
                return false;

            var tracking = driver.trackingStateInput.action;
            if (tracking == null || !tracking.enabled || tracking.controls.Count == 0)
                return false;
            const InputTrackingState required = InputTrackingState.Position | InputTrackingState.Rotation;
            if (((InputTrackingState)tracking.ReadValue<int>() & required) != required)
                return false;

            var position = driver.positionInput.action;
            return position != null && position.enabled && position.controls.Count > 0;
        }

        // requireTarget false for the prefab asset: the limb target is a scene reference set on the instance.
        public bool TryValidate(out string error, bool requireTarget = true)
        {
            var missing = new List<string>();
            if (grab == null)
                missing.Add(nameof(grab));
            if (body == null)
                missing.Add(nameof(body));
            if (grabPoint == null)
                missing.Add(nameof(grabPoint));
            if (limbAttachPoint == null)
                missing.Add(nameof(limbAttachPoint));
            if (requireTarget && target == null)
                missing.Add(nameof(target));
            if (parts.body == null)
                missing.Add("parts.body");
            if (parts.band == null)
                missing.Add("parts.band");
            if (parts.bandEnd == null)
                missing.Add("parts.bandEnd");
            if (parts.windlassAxis == null)
                missing.Add("parts.windlassAxis");
            if (parts.windlass == null)
                missing.Add("parts.windlass");
            if (parts.windlassClip == null)
                missing.Add("parts.windlassClip");
            if (parts.securingStrap == null)
                missing.Add("parts.securingStrap");
            if (parts.timeLabelSurface == null)
                missing.Add("parts.timeLabelSurface");
            if (judgeBoxes == null || judgeBoxes.Length == 0 ||
                Array.Exists(judgeBoxes, b => b.frame == null || b.size.x <= 0f || b.size.y <= 0f || b.size.z <= 0f))
                missing.Add(nameof(judgeBoxes));

            error = missing.Count > 0 ? $"TourniquetDevice '{name}' missing references: {string.Join(", ", missing)}." : null;
            return error == null;
        }
    }
}
