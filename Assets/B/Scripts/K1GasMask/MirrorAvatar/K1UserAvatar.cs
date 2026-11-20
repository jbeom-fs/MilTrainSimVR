using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace MilTrainSimVR.B.K1GasMask.MirrorAvatar
{
    // Observer body of the user on the mannequin rig, for the mirror and the rear view. Only the head (HMD camera) and both
    // hands (controllers) are tracked inputs. Everything else is estimated, not measured: the body turns toward the head's
    // heading, the hips shift so the avatar's eye point lands on the HMD camera (lean and crouch within limits), elbows and
    // knees come from a two-bone solve with fixed bend hints, and the feet stay where the standing pose puts them on the floor.
    // Single writer of the rig: every solve first resets the driven bones to the rest pose and then sets them, in LateUpdate
    // and again right before rendering. The model's Animator is disabled; no other component writes them.
    // Reads the XR Origin only and never moves the camera, controllers or origin. A lost input holds its last valid pose and
    // blends back when tracking returns, so nothing jumps to the origin. Hidden from the HMD camera only, so it never covers
    // the first-person view or the equipment; other cameras (mirror, rear view, scene view) see it. No colliders or physics.
    [DisallowMultipleComponent]
    public sealed class K1UserAvatar : MonoBehaviour
    {
        [Serializable]
        public struct Limb
        {
            public Transform upper;
            public Transform lower;
            public Transform end;
            [Tooltip("Arms: middle finger root (aim of the hand). Legs: toe base.")]
            public Transform aim;
            [Tooltip("Arms: index finger root (thumb side of the aim). Legs: unused.")]
            public Transform side;
        }

        // Kept with its last valid pose; blends from it back to the live pose after a loss.
        sealed class PoseSource
        {
            Pose held;
            float blend = 1f;
            bool lost;

            public bool HasPose { get; private set; }
            public Pose Output { get; private set; }

            public void Sample(Transform source, TrackedPoseDriver driver, float blendTime, float deltaTime)
            {
                if (source == null || !IsTracked(driver))
                {
                    if (HasPose && !lost)
                    {
                        lost = true;
                        held = Output;
                    }
                    return;
                }

                var live = new Pose(source.position, source.rotation);
                if (!HasPose)
                {
                    Output = live;
                    HasPose = true;
                    return;
                }

                if (lost)
                {
                    lost = false;
                    blend = 0f;
                }

                if (blend < 1f && blendTime > 0f)
                {
                    blend = Mathf.Min(1f, blend + deltaTime / blendTime);
                    Output = new Pose(Vector3.Lerp(held.position, live.position, blend), Quaternion.Slerp(held.rotation, live.rotation, blend));
                }
                else
                {
                    Output = live;
                }
            }
        }

        // Fixed bend hints in the body frame (m): elbows down, out and back; knees forward. Not tracked data.
        static readonly Vector3 ElbowHint = new Vector3(0.3f, -0.6f, -0.3f);
        static readonly Vector3 KneeHint = new Vector3(0f, 0f, 0.6f);
        const float MaxLift = 0.15f;

        [Header("Tracked inputs (XR Origin, read only)")]
        [SerializeField] Transform head;
        [SerializeField] TrackedPoseDriver headDriver;
        [SerializeField] Transform leftController;
        [SerializeField] TrackedPoseDriver leftDriver;
        [SerializeField] Transform rightController;
        [SerializeField] TrackedPoseDriver rightDriver;

        [Header("Rig (mannequin bones)")]
        [Tooltip("Mannequin model instance under this object; scaled at start so its eye point reaches the standing eye height.")]
        [SerializeField] Transform model;
        [SerializeField] Transform hips;
        [SerializeField] Transform headBone;
        [SerializeField] Limb leftArm;
        [SerializeField] Limb rightArm;
        [SerializeField] Limb leftLeg;
        [SerializeField] Limb rightLeg;

        [Header("Alignment (adjustable)")]
        [Tooltip("Point between the eyes in the head bone's local space; it is placed on the HMD camera.")]
        [SerializeField] Vector3 eyeOffset;
        [Tooltip("User's standing eye height above the floor (m). The mannequin is scaled uniformly to it at start.")]
        [SerializeField] float standingEyeHeight = 1.6f;
        [Tooltip("Floor height in world space (m); the feet stand on it. Used only while no floor reference is set (a course's own scene).")]
        [SerializeField] float floorHeight;
        [Tooltip("Training field: the station's floor anchor. When set, its world height is the floor (floorHeight is not used), so a " +
                 "station moved to another floor height keeps the body on its own floor. Empty in a course's own scene.")]
        [SerializeField] Transform floorReference;
        [Tooltip("Upper and lower arm length factor after scaling (1 = mannequin proportions).")]
        [SerializeField, Range(0.8f, 1.25f)] float armLengthScale = 1f;
        [Tooltip("Right wrist in the right controller's local space (m); mirrored in x for the left hand.")]
        [SerializeField] Vector3 wristOffset = new Vector3(0f, -0.02f, -0.08f);
        [Tooltip("Extra right hand rotation on the controller frame (Euler, deg); mirrored for the left hand.")]
        [SerializeField] Vector3 handRotationOffset;
        [Tooltip("Seconds for the body to turn toward the head's heading.")]
        [SerializeField] float bodyTurnTime = 0.3f;
        [Tooltip("Seconds to blend from a held pose back to live tracking after a loss.")]
        [SerializeField] float trackingBlendTime = 0.25f;
        [Tooltip("Largest hip drop for crouching (m).")]
        [SerializeField] float maxCrouch = 0.6f;
        [Tooltip("Largest horizontal hip shift for leaning (m).")]
        [SerializeField] float maxLean = 0.3f;

        readonly PoseSource headPose = new PoseSource();
        readonly PoseSource leftPose = new PoseSource();
        readonly PoseSource rightPose = new PoseSource();
        bool ready;
        bool subscribed;
        Camera headCamera;
        Renderer[] renderers = Array.Empty<Renderer>();
        Transform[] driven = Array.Empty<Transform>();
        Quaternion[] restRotations = Array.Empty<Quaternion>();
        Vector3 hipsRestPosition;
        Vector3[] armRestPositions = Array.Empty<Vector3>();
        Quaternion headRelative;
        Vector3 eyeRest;
        Quaternion leftHandRelative;
        Quaternion rightHandRelative;
        Vector3 leftFootRest;
        Vector3 rightFootRest;
        Quaternion leftFootRelative;
        Quaternion rightFootRelative;
        float bodyYaw;
        bool hasYaw;

        public Transform Model => model;
        public Transform Head => head;
        // World height of the floor the body stands on this frame: the floor reference's height when set, else floorHeight. Set
        // absolutely every solve (never accumulated).
        public float FloorY => floorReference != null ? floorReference.position.y : floorHeight;
        public Transform FloorReference => floorReference;

        void Awake()
        {
            ready = TryValidate(out var error);
            if (!ready)
            {
                Debug.LogError(error + " Avatar stays in its rest pose.", this);
                return;
            }

            headCamera = head.GetComponent<Camera>();
            renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                // The HMD camera never draws it, so skinning must not depend on that camera's visibility.
                if (r is SkinnedMeshRenderer skinned)
                    skinned.updateWhenOffscreen = true;
            }

            // Uniform scale so the rest eye point is standingEyeHeight above this object (the floor point).
            var eyeHeight = transform.InverseTransformPoint(headBone.TransformPoint(eyeOffset)).y;
            if (eyeHeight > 0.1f)
                model.localScale *= standingEyeHeight / eyeHeight;
            else
                Debug.LogError($"[K1UserAvatar] Rest eye height {eyeHeight:F3} m is not usable; scale unchanged.", this);

            CaptureRest();
        }

        void OnEnable()
        {
            if (!ready || subscribed)
                return;

            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            Application.onBeforeRender += OnBeforeRender;
            subscribed = true;
        }

        void OnDisable()
        {
            if (!subscribed)
                return;

            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            Application.onBeforeRender -= OnBeforeRender;
            subscribed = false;
            SetHidden(false);
        }

        void LateUpdate() => Solve(Time.deltaTime);

        // Latest XR poses for this frame's cameras; time-based smoothing advances only in LateUpdate.
        void OnBeforeRender() => Solve(0f);

        void OnBeginCamera(ScriptableRenderContext _, Camera renderingCamera) => SetHidden(renderingCamera == headCamera);

        void SetHidden(bool hidden)
        {
            foreach (var r in renderers)
            {
                if (r != null)
                    r.forceRenderingOff = hidden;
            }
        }

        public bool TryValidate(out string error)
        {
            var problems = new List<string>();
            if (head == null || headDriver == null || leftController == null || leftDriver == null || rightController == null || rightDriver == null)
                problems.Add("head or controller inputs missing");
            if (model == null || hips == null || headBone == null || !LimbValid(leftArm, true) || !LimbValid(rightArm, true)
                || !LimbValid(leftLeg, false) || !LimbValid(rightLeg, false))
                problems.Add("rig bones missing");
            else if (!model.IsChildOf(transform) || !hips.IsChildOf(model) || !headBone.IsChildOf(hips))
                problems.Add("rig bones not under this avatar's model");
            if (head != null && model != null && (head.IsChildOf(transform) || transform.IsChildOf(head)))
                problems.Add("avatar must not contain or sit under the HMD camera");
            if (standingEyeHeight <= 0.5f || bodyTurnTime < 0f || trackingBlendTime < 0f || maxCrouch < 0f || maxLean < 0f)
                problems.Add("alignment settings out of range");

            error = problems.Count > 0 ? $"K1UserAvatar '{name}': {string.Join(", ", problems)}." : null;
            return error == null;
        }

        static bool LimbValid(Limb limb, bool arm) =>
            limb.upper != null && limb.lower != null && limb.end != null && limb.aim != null && (!arm || limb.side != null);

        void CaptureRest()
        {
            driven = new[]
            {
                hips, headBone, leftArm.upper, leftArm.lower, leftArm.end, rightArm.upper, rightArm.lower, rightArm.end,
                leftLeg.upper, leftLeg.lower, leftLeg.end, rightLeg.upper, rightLeg.lower, rightLeg.end
            };
            restRotations = new Quaternion[driven.Length];
            for (var i = 0; i < driven.Length; i++)
                restRotations[i] = driven[i].localRotation;
            hipsRestPosition = hips.localPosition;
            armRestPositions = new[] { leftArm.lower.localPosition, leftArm.end.localPosition, rightArm.lower.localPosition, rightArm.end.localPosition };

            var inverseRoot = Quaternion.Inverse(transform.rotation);
            headRelative = inverseRoot * headBone.rotation;
            eyeRest = transform.InverseTransformPoint(headBone.TransformPoint(eyeOffset));
            leftHandRelative = HandRelative(leftArm);
            rightHandRelative = HandRelative(rightArm);
            leftFootRest = transform.InverseTransformPoint(leftLeg.end.position);
            rightFootRest = transform.InverseTransformPoint(rightLeg.end.position);
            leftFootRelative = inverseRoot * leftLeg.end.rotation;
            rightFootRelative = inverseRoot * rightLeg.end.rotation;
        }

        // Hand rotation relative to its aim frame (aim = toward the middle finger root, up = toward the index side).
        // The controller frame plays that aim frame: +z forward along the grip, +y up on the thumb side.
        static Quaternion HandRelative(Limb arm) =>
            Quaternion.Inverse(Quaternion.LookRotation(arm.aim.position - arm.end.position, arm.side.position - arm.aim.position)) * arm.end.rotation;

        void Solve(float deltaTime)
        {
            if (!ready)
                return;

            headPose.Sample(head, headDriver, trackingBlendTime, deltaTime);
            leftPose.Sample(leftController, leftDriver, trackingBlendTime, deltaTime);
            rightPose.Sample(rightController, rightDriver, trackingBlendTime, deltaTime);
            if (!headPose.HasPose)
                return;

            for (var i = 0; i < driven.Length; i++)
                driven[i].localRotation = restRotations[i];
            hips.localPosition = hipsRestPosition;
            leftArm.lower.localPosition = armRestPositions[0] * armLengthScale;
            leftArm.end.localPosition = armRestPositions[1] * armLengthScale;
            rightArm.lower.localPosition = armRestPositions[2] * armLengthScale;
            rightArm.end.localPosition = armRestPositions[3] * armLengthScale;

            // Body heading follows the head's heading; looking straight up or down keeps the last heading.
            var eye = headPose.Output;
            var heading = Vector3.ProjectOnPlane(eye.rotation * Vector3.forward, Vector3.up);
            if (heading.sqrMagnitude > 1e-4f)
            {
                var targetYaw = Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg;
                if (!hasYaw || bodyTurnTime <= 0f)
                    bodyYaw = targetYaw;
                else if (deltaTime > 0f)
                    bodyYaw = Mathf.LerpAngle(bodyYaw, targetYaw, 1f - Mathf.Exp(-deltaTime / bodyTurnTime));
                hasYaw = true;
            }

            // Stand on the floor under the eye point, then turn the head and shift the hips so the eye point meets the HMD.
            var bodyRotation = Quaternion.Euler(0f, bodyYaw, 0f);
            var eyeFlat = bodyRotation * new Vector3(eyeRest.x, 0f, eyeRest.z);
            transform.SetPositionAndRotation(new Vector3(eye.position.x - eyeFlat.x, FloorY, eye.position.z - eyeFlat.z), bodyRotation);
            headBone.rotation = eye.rotation * headRelative;
            var offset = eye.position - headBone.TransformPoint(eyeOffset);
            var lean = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(offset, Vector3.up), maxLean);
            hips.position += lean + Vector3.up * Mathf.Clamp(offset.y, -maxCrouch, MaxLift);

            SolveArm(leftArm, leftPose, leftHandRelative, -1f);
            SolveArm(rightArm, rightPose, rightHandRelative, 1f);
            SolveLeg(leftLeg, leftFootRest, leftFootRelative);
            SolveLeg(rightLeg, rightFootRest, rightFootRelative);
        }

        // Without any valid pose of that controller yet, the arm keeps its rest pose.
        void SolveArm(Limb arm, PoseSource source, Quaternion handRelative, float side)
        {
            if (!source.HasPose)
                return;

            var controller = source.Output;
            var wrist = controller.position + controller.rotation * new Vector3(wristOffset.x * side, wristOffset.y, wristOffset.z);
            var hint = arm.upper.position + transform.rotation * new Vector3(ElbowHint.x * side, ElbowHint.y, ElbowHint.z);
            SolveTwoBone(arm.upper, arm.lower, arm.end, wrist, hint);
            var extra = side > 0f ? Quaternion.Euler(handRotationOffset)
                : Quaternion.Euler(handRotationOffset.x, -handRotationOffset.y, -handRotationOffset.z);
            arm.end.rotation = controller.rotation * extra * handRelative;
        }

        void SolveLeg(Limb leg, Vector3 footRest, Quaternion footRelative)
        {
            SolveTwoBone(leg.upper, leg.lower, leg.end, transform.TransformPoint(footRest), leg.upper.position + transform.rotation * KneeHint);
            leg.end.rotation = transform.rotation * footRelative;
        }

        // Analytic two-bone chain: a (shoulder/hip), b (elbow/knee), c (wrist/ankle); bends toward the hint, clamps the reach.
        static void SolveTwoBone(Transform a, Transform b, Transform c, Vector3 target, Vector3 hint)
        {
            var root = a.position;
            var upperLength = Vector3.Distance(root, b.position);
            var lowerLength = Vector3.Distance(b.position, c.position);
            var toTarget = target - root;
            var distance = toTarget.magnitude;
            if (upperLength < 1e-5f || lowerLength < 1e-5f || distance < 1e-5f)
                return;

            var direction = toTarget / distance;
            distance = Mathf.Clamp(distance, Mathf.Abs(upperLength - lowerLength) + 1e-4f, upperLength + lowerLength - 1e-4f);
            var bend = Vector3.ProjectOnPlane(hint - root, direction);
            if (bend.sqrMagnitude < 1e-8f)
                bend = Vector3.ProjectOnPlane(b.position - root, direction);
            if (bend.sqrMagnitude < 1e-8f)
                return;

            bend.Normalize();
            var cos = Mathf.Clamp((upperLength * upperLength + distance * distance - lowerLength * lowerLength) / (2f * upperLength * distance), -1f, 1f);
            var elbow = root + direction * (upperLength * cos) + bend * (upperLength * Mathf.Sqrt(1f - cos * cos));
            a.rotation = Quaternion.FromToRotation(b.position - root, elbow - root) * a.rotation;
            b.rotation = Quaternion.FromToRotation(c.position - b.position, root + direction * distance - b.position) * b.rotation;
        }

        // Both position and rotation tracked by a live pose driver; a transform alone is not tracking.
        static bool IsTracked(TrackedPoseDriver driver)
        {
            if (driver == null || !driver.isActiveAndEnabled)
                return false;

            var tracking = driver.trackingStateInput.action;
            if (tracking == null || !tracking.enabled || tracking.controls.Count == 0)
                return false;

            const InputTrackingState pose = InputTrackingState.Position | InputTrackingState.Rotation;
            return ((InputTrackingState)tracking.ReadValue<int>() & pose) == pose;
        }
    }
}
