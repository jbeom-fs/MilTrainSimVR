using UnityEngine;

namespace MilTrainSimVR.B.BleedingControl.PressureBandage
{
    // Result of judging a dressing pose against the designated wound, in check order.
    public enum PressureDressingCheck
    {
        None = 0,
        // Contact centre farther than the candidate range from the wound: not this wound (another site, the tray, the air).
        Away = 1,
        // The top face, not the contact face, points at the wound.
        WrongFace = 2,
        // Contact face toward the wound but tilted beyond the tolerance.
        Tilted = 3,
        // Contact face above the skin surface beyond the allowed gap.
        Lifted = 4,
        // Contact face sunk below the skin surface beyond the allowed depth (a position check only, never a pressure value).
        TooDeep = 5,
        // The wound disc is not inside the dressing face.
        NotCovering = 6,
        Valid = 7
    }

    // Designated wound of the training arm: a judge frame only (no renderer, no collider). Position = wound centre on the skin
    // surface, up = outward skin normal, forward = along the forearm. The wound marker is a separate display object. Tolerances
    // are development VR handling values, not medical criteria. Evaluate takes the pose as data and moves nothing.
    [DisallowMultipleComponent]
    public sealed class PressureBandageWound : MonoBehaviour
    {
        [Header("VR handling tolerances (development values, not medical criteria)")]
        [Tooltip("Radius (m) of the wound area the dressing face must cover.")]
        [SerializeField, Min(0.001f)] float woundRadius = 0.015f;
        [Tooltip("Contact centre within this distance (m) of the wound centre is judged against this wound; farther is Away.")]
        [SerializeField, Min(0.01f)] float candidateRange = 0.15f;
        [Tooltip("Largest angle (deg) between the contact direction and the inward skin normal.")]
        [SerializeField, Range(1f, 89f)] float tiltToleranceDegrees = 35f;
        [Tooltip("Largest gap (m) of the contact centre above the skin surface plane.")]
        [SerializeField, Min(0f)] float maxGap = 0.02f;
        [Tooltip("Largest depth (m) of the contact centre below the skin surface plane.")]
        [SerializeField, Min(0f)] float maxDepth = 0.02f;

        public Vector3 Center => transform.position;
        public Vector3 Normal => transform.up;
        public float WoundRadius => woundRadius;
        public float CandidateRange => candidateRange;
        public float TiltToleranceDegrees => tiltToleranceDegrees;
        public float MaxGap => maxGap;
        public float MaxDepth => maxDepth;

        // contactPoint: contact face centre; contactDirection: unit direction the contact face points to; faceU/faceV: unit
        // in-plane axes of the dressing face with halfSize the face half extents along them.
        public PressureDressingCheck Evaluate(Vector3 contactPoint, Vector3 contactDirection, Vector3 faceU, Vector3 faceV, Vector2 halfSize)
        {
            var center = Center;
            var normal = Normal;
            if (Vector3.Distance(contactPoint, center) > candidateRange)
                return PressureDressingCheck.Away;

            var angle = Vector3.Angle(contactDirection, -normal);
            if (angle >= 90f)
                return PressureDressingCheck.WrongFace;
            if (angle > tiltToleranceDegrees)
                return PressureDressingCheck.Tilted;

            var height = Vector3.Dot(contactPoint - center, normal);
            if (height > maxGap)
                return PressureDressingCheck.Lifted;
            if (height < -maxDepth)
                return PressureDressingCheck.TooDeep;

            // Wound centre in the dressing face, measured from the contact centre along the face axes.
            var offset = center - contactPoint;
            var u = Mathf.Abs(Vector3.Dot(offset, faceU));
            var v = Mathf.Abs(Vector3.Dot(offset, faceV));
            if (u + woundRadius > halfSize.x || v + woundRadius > halfSize.y)
                return PressureDressingCheck.NotCovering;
            return PressureDressingCheck.Valid;
        }

        public bool TryValidate(out string error)
        {
            error = null;
            if (!IsFinitePositive(woundRadius) || !IsFinitePositive(candidateRange) || !IsFinitePositive(tiltToleranceDegrees) ||
                !IsFinite(maxGap) || maxGap < 0f || !IsFinite(maxDepth) || maxDepth < 0f)
                error = $"PressureBandageWound '{name}' has a non-finite or out-of-range tolerance.";
            else if (candidateRange <= woundRadius)
                error = $"PressureBandageWound '{name}': candidate range must exceed the wound radius.";
            return error == null;
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool IsFinitePositive(float value) => IsFinite(value) && value > 0f;
    }
}
