using UnityEngine;

namespace MilTrainSimVR.B.K1GasMask.Tutorial
{
    // Small guide outline drawn with its own LineRenderer and B material: a ring around a select point or an up chevron.
    // Lives outside the equipment hierarchy and only follows its target, so no equipment renderer, material or bounds change.
    // Turned toward the viewer (the chevron about the world up axis only); no collider, so it never takes rays or grabs.
    // Follows in LateUpdate and again before rendering, after the XRI and face-pose updates that subscribed earlier.
    [RequireComponent(typeof(LineRenderer))]
    public sealed class K1GuideMarker : MonoBehaviour
    {
        public enum Shape
        {
            Ring = 0,
            UpChevron = 1
        }

        const int RingSegments = 32;

        [SerializeField] Transform target;
        [SerializeField] Shape shape;
        [Tooltip("Ring radius or chevron width (m).")]
        [SerializeField] float size = 0.03f;
        [Tooltip("World-space offset from the target (m), e.g. above the mask for the chevron.")]
        [SerializeField] Vector3 worldOffset;
        [Tooltip("Shift toward the viewer (m) so the outline is not hidden inside the model.")]
        [SerializeField] float towardViewer = 0.02f;

        LineRenderer line;
        Transform viewer;

        public Transform Target => target;

        void Awake()
        {
            line = GetComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = shape == Shape.Ring;
            if (shape == Shape.Ring)
            {
                line.positionCount = RingSegments;
                for (var i = 0; i < RingSegments; i++)
                {
                    var angle = 2f * Mathf.PI * i / RingSegments;
                    line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * size);
                }
            }
            else
            {
                var half = size * 0.5f;
                line.positionCount = 3;
                line.SetPosition(0, new Vector3(-half, -half * 0.5f, 0f));
                line.SetPosition(1, new Vector3(0f, half * 0.5f, 0f));
                line.SetPosition(2, new Vector3(half, -half * 0.5f, 0f));
            }
        }

        void OnEnable()
        {
            Application.onBeforeRender += Follow;
            Follow();
        }

        void OnDisable() => Application.onBeforeRender -= Follow;

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

            var position = target.position + worldOffset;
            var toViewer = viewer.position - position;
            if (shape == Shape.UpChevron)
                toViewer = Vector3.ProjectOnPlane(toViewer, Vector3.up);
            if (toViewer.sqrMagnitude < 1e-6f)
                return;

            var facing = toViewer.normalized;
            transform.SetPositionAndRotation(position + facing * towardViewer, Quaternion.LookRotation(-facing, Vector3.up));
        }
    }
}
