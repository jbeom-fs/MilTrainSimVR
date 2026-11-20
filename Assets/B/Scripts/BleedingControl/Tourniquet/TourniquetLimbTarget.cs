using System;
using System.Collections.Generic;
using UnityEngine;

namespace MilTrainSimVR.B.BleedingControl.Tourniquet
{
    public enum TourniquetPlacementCheck
    {
        None = 0,
        Valid = 1,
        // Too far from the marked zone for a candidate; no preview.
        OutOfRange = 2,
        OutsideAllowedZone = 3,
        OffLimbCenter = 4,
        WrongAxis = 5,
        WrongRoll = 6,
        ForbiddenZone = 7,
        OtherTarget = 8,
        TrackingLost = 9,
        SelectionCanceled = 10,
        // Release without an attach sample of that frame and hand (e.g. an interactor without an attach controller).
        ReleasePoseUnavailable = 11
    }

    public readonly struct TourniquetPlacement
    {
        public readonly TourniquetPlacementCheck Check;
        public readonly bool HasCandidate;
        // Candidate root pose of the tourniquet: its limb attach point on the limb axis, aligned with the limb frame.
        public readonly Pose RootPose;

        public TourniquetPlacement(TourniquetPlacementCheck check, bool hasCandidate, Pose rootPose)
        {
            Check = check;
            HasCandidate = hasCandidate;
            RootPose = rootPose;
        }
    }

    // Designated limb frame for the prototype: origin at the centre of the allowed segment on the limb axis, forward along the
    // limb toward its distal end, up toward the side the tourniquet body should face. Judges a tourniquet root pose given as
    // data: its limb attach point (band loop centre) against this frame, and its part volumes against the block zones, both at
    // that pose and at the aligned candidate pose. It never moves, grabs or attaches anything.
    // All tolerances are VR handling settings of this technical prototype, not a medical position, distance or criterion.
    [DisallowMultipleComponent]
    public sealed class TourniquetLimbTarget : MonoBehaviour
    {
        // Floating-point slack in metres for the segment and radial limits: a pose aligned exactly on a limit (for example the
        // segment ends) loses about 1e-7 m in the transform round trip. 0.01 mm, far below any handling
        // value; it separates rounding from a real overshoot and does not widen the development zone.
        public const float NumericSlack = 1e-5f;

        [Header("VR handling tolerances (prototype engineering values, not medical criteria)")]
        [Tooltip("Half length in metres of the allowed segment along the limb axis, measured from this origin. Development zone only.")]
        [SerializeField, Min(0f)] float allowedHalfLength = 0.06f;
        [Tooltip("Max distance in metres between the band loop centre and the limb axis.")]
        [SerializeField, Min(0f)] float radialTolerance = 0.05f;
        [Tooltip("Max angle in degrees between the loop axis and the limb axis (forward). The loop direction is signed: a reversed loop fails.")]
        [SerializeField, Range(0f, 90f)] float axisToleranceDegrees = 35f;
        [Tooltip("Max angle in degrees between the tourniquet body face (up) and this frame's up, around the limb axis.")]
        [SerializeField, Range(0f, 180f)] float rollToleranceDegrees = 60f;
        [Tooltip("Distance in metres around the allowed segment within which a placement candidate is computed and previewed.")]
        [SerializeField, Min(0f)] float candidateRange = 0.15f;

        [Header("Zones")]
        [SerializeField] TourniquetBlockZone[] blockZones = Array.Empty<TourniquetBlockZone>();
        [Tooltip("Optional collider-free cylinder visual of the allowed segment (child, Unity cylinder primitive), sized on enable.")]
        [SerializeField] Transform allowedZoneVisual;
        [SerializeField, Min(0f)] float allowedZoneVisualRadius = 0.07f;

        readonly List<TourniquetBox> volumes = new List<TourniquetBox>();

        public float AllowedHalfLength => allowedHalfLength;
        public float RadialTolerance => radialTolerance;
        public float AxisToleranceDegrees => axisToleranceDegrees;
        public float RollToleranceDegrees => rollToleranceDegrees;
        public float CandidateRange => candidateRange;
        public IReadOnlyList<TourniquetBlockZone> BlockZones => blockZones;

        void OnEnable() => ApplyVisual();

        // rootPose: tourniquet root pose to judge (data only; the device is not moved).
        public TourniquetPlacement Evaluate(Pose rootPose, TourniquetDevice device)
        {
            var attachPose = device.AttachLocalPose.GetTransformedBy(rootPose);
            var axis = transform.forward;
            Measure(rootPose, device, out var along, out var radial);
            var inRange = radial <= candidateRange && Mathf.Abs(along) <= allowedHalfLength + candidateRange;
            var candidate = device.RootPoseFor(new Pose(transform.position + axis * Mathf.Clamp(along, -allowedHalfLength, allowedHalfLength), transform.rotation));

            // Zones first: touching another body part or a forbidden area is the reason even when the pose is also off. The same
            // volume rule runs on the judged pose and, when there is one, on the candidate the device would be aligned to.
            var zone = HitZone(rootPose, device);
            if (zone == null && inRange)
                zone = HitZone(candidate, device);
            if (zone != null)
                return Result(zone.Kind == TourniquetZoneKind.OtherTarget ? TourniquetPlacementCheck.OtherTarget : TourniquetPlacementCheck.ForbiddenZone);

            if (!inRange)
                return new TourniquetPlacement(TourniquetPlacementCheck.OutOfRange, false, default);
            if (Mathf.Abs(along) > allowedHalfLength + NumericSlack)
                return Result(TourniquetPlacementCheck.OutsideAllowedZone);
            if (radial > radialTolerance + NumericSlack)
                return Result(TourniquetPlacementCheck.OffLimbCenter);
            if (Vector3.Angle(attachPose.forward, axis) > axisToleranceDegrees)
                return Result(TourniquetPlacementCheck.WrongAxis);
            var face = Vector3.ProjectOnPlane(attachPose.up, axis);
            if (face.sqrMagnitude < 1e-6f || Vector3.Angle(face, transform.up) > rollToleranceDegrees)
                return Result(TourniquetPlacementCheck.WrongRoll);
            return Result(TourniquetPlacementCheck.Valid);

            TourniquetPlacement Result(TourniquetPlacementCheck check) => new TourniquetPlacement(check, inRange, candidate);
        }

        // Band loop centre of the device at rootPose in this frame: signed distance along the limb axis from the segment centre and
        // distance from the axis, in metres (data only).
        public void Measure(Pose rootPose, TourniquetDevice device, out float along, out float radial)
        {
            var offset = device.AttachLocalPose.GetTransformedBy(rootPose).position - transform.position;
            along = Vector3.Dot(offset, transform.forward);
            radial = (offset - transform.forward * along).magnitude;
        }

        TourniquetBlockZone HitZone(Pose rootPose, TourniquetDevice device)
        {
            device.CollectVolumes(rootPose, volumes);
            foreach (var zone in blockZones)
            {
                // enabled + activeInHierarchy rather than isActiveAndEnabled, so the zones also count outside Play mode.
                if (zone == null || !zone.enabled || !zone.gameObject.activeInHierarchy)
                    continue;
                var box = zone.Box;
                foreach (var volume in volumes)
                {
                    if (volume.Overlaps(box))
                        return zone;
                }
            }
            return null;
        }

        public void ApplyVisual()
        {
            if (allowedZoneVisual == null)
                return;
            // Unity cylinder: axis along its Y, height 2, diameter 1.
            allowedZoneVisual.localPosition = Vector3.zero;
            allowedZoneVisual.localRotation = Quaternion.Euler(90f, 0f, 0f);
            allowedZoneVisual.localScale = new Vector3(allowedZoneVisualRadius * 2f, allowedHalfLength, allowedZoneVisualRadius * 2f);
        }

        // Shows or hides only the renderers of the allowed zone visual (no object is deactivated; size, tolerances, zones and the
        // judgement are unchanged). Without a caller the visual keeps its scene state, so a standalone device check still shows it.
        public void SetAllowedZoneVisible(bool visible)
        {
            if (allowedZoneVisual == null)
                return;
            foreach (var renderer in allowedZoneVisual.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer != null)
                    renderer.enabled = visible;
            }
        }

        public bool TryValidate(out string error)
        {
            error = null;
            if (blockZones == null || blockZones.Length == 0 || Array.Exists(blockZones, z => z == null))
                error = $"TourniquetLimbTarget '{name}': blockZones must be set and contain no empty entry.";
            else if (allowedHalfLength <= 0f || radialTolerance <= 0f || candidateRange <= 0f)
                error = $"TourniquetLimbTarget '{name}': allowedHalfLength, radialTolerance and candidateRange must be above 0.";
            return error == null;
        }

        void OnDrawGizmosSelected()
        {
            var a = transform.position - transform.forward * allowedHalfLength;
            var b = transform.position + transform.forward * allowedHalfLength;
            Gizmos.color = Color.green;
            Gizmos.DrawLine(a, b);
            Gizmos.DrawWireSphere(a, radialTolerance);
            Gizmos.DrawWireSphere(b, radialTolerance);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, transform.position + transform.up * 0.1f);
        }
    }
}
