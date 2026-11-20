using UnityEngine;

namespace MilTrainSimVR.B.BleedingControl.Tourniquet
{
    public enum TourniquetZoneKind
    {
        Forbidden = 0,
        OtherTarget = 1
    }

    // Development box where the tourniquet must not be placed: a forbidden area on the designated limb, or another body part.
    // Pure geometry: no collider or physics, so it never blocks rays, grabs or the equipment. Not a medical boundary.
    // Keep this transform's scale at 1; size is in metres along its own axes.
    [DisallowMultipleComponent]
    public sealed class TourniquetBlockZone : MonoBehaviour
    {
        [SerializeField] TourniquetZoneKind kind;
        [Tooltip("Box size in metres along this transform's axes. Development placement setting, not a medical boundary.")]
        [SerializeField] Vector3 size = new Vector3(0.16f, 0.16f, 0.1f);
        [Tooltip("Optional collider-free box visual (child, unit cube), scaled to size on enable.")]
        [SerializeField] Transform visual;

        public TourniquetZoneKind Kind => kind;
        public Vector3 Size => size;

        void OnEnable() => ApplyVisual();

        // World box from position, rotation and size; the transform scale is ignored.
        internal TourniquetBox Box => TourniquetBox.FromMatrix(Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one), Vector3.zero, size);

        public void ApplyVisual()
        {
            if (visual == null)
                return;
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = size;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = kind == TourniquetZoneKind.Forbidden ? Color.red : new Color(1f, 0.6f, 0f);
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, size);
        }
    }
}
