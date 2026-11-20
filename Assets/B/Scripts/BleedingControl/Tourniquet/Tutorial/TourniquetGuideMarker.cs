using UnityEngine;

namespace MilTrainSimVR.B.BleedingControl.Tourniquet.Tutorial
{
    // Small guide ring drawn with its own LineRenderer and B material around one handling point of the tourniquet (same idea as
    // the K1 course's guide outline, kept in this course's assembly). Lives outside the device hierarchy and only follows its
    // point: no device renderer, material, mesh or judge volume changes, and no collider, rigidbody or interactable, so it never
    // takes rays or grabs. The point is a local position in the target's space (for example a part handle's collider centre or
    // the windlass end), so it follows the handle as the operation moves and bends the part. Turned toward the viewer and moved a
    // little toward them. Drawn with MAT_TQ_GuideMarker (B/Tourniquet/GuideMarker: two-sided, depth pulled a few centimetres
    // toward the camera) so nearby device surfaces do not cut the ring into a C. Follows after the operation's part update
    // (order 105) and again before rendering. TourniquetTrainingGuide alone turns markers on and off.
    [DefaultExecutionOrder(120)]
    [RequireComponent(typeof(LineRenderer))]
    public sealed class TourniquetGuideMarker : MonoBehaviour
    {
        const int RingSegments = 32;

        [SerializeField] Transform target;
        [Tooltip("Point in the target's local space that the ring surrounds (m).")]
        [SerializeField] Vector3 localPoint;
        [Tooltip("Ring radius (m).")]
        [SerializeField] float size = 0.015f;
        [Tooltip("Shift toward the viewer (m) so the ring is not hidden inside the model.")]
        [SerializeField] float towardViewer = 0.015f;

        LineRenderer line;
        Transform viewer;
        bool following;

        public Transform Target => target;
        public Vector3 LocalPoint => localPoint;
        public Vector3 Point => target != null ? target.TransformPoint(localPoint) : transform.position;

        void Awake()
        {
            line = GetComponent<LineRenderer>();
            // A closed ring in this object's XY plane, which Follow turns to face the viewer (two-sided marker material).
            line.useWorldSpace = false;
            line.alignment = LineAlignment.TransformZ;
            line.loop = true;
            line.positionCount = RingSegments;
            for (var i = 0; i < RingSegments; i++)
            {
                var angle = 2f * Mathf.PI * i / RingSegments;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * size);
            }
        }

        void OnEnable()
        {
            if (!following)
            {
                Application.onBeforeRender += Follow;
                following = true;
            }
            Follow();
        }

        void OnDisable()
        {
            if (following)
                Application.onBeforeRender -= Follow;
            following = false;
        }

        void LateUpdate() => Follow();

        void Follow()
        {
            if (target == null)
                return;
            if (viewer == null)
            {
                var camera = Camera.main;
                if (camera == null)
                    return;
                viewer = camera.transform;
            }

            var position = Point;
            var toViewer = viewer.position - position;
            if (toViewer.sqrMagnitude < 1e-6f)
                return;

            var facing = toViewer.normalized;
            transform.SetPositionAndRotation(position + facing * towardViewer, Quaternion.LookRotation(-facing, Vector3.up));
        }
    }
}
