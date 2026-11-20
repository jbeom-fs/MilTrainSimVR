using UnityEngine;

namespace MilTrainSimVR.B.BleedingControl.PressureBandage.Tutorial
{
    // One guide ring of the course: drawn with its own LineRenderer around the handling point the course guide names (dressing
    // top, roll, bandage end or tape), turned toward the viewer and moved a little toward them so the part does not hide it. Lives
    // outside the equipment hierarchy and only follows a point: no collider, rigidbody, interactable or UI graphic, so it never
    // takes rays or grabs. PressureBandageTrainingGuide alone sets its point and turns it on and off.
    [DefaultExecutionOrder(120)]
    [RequireComponent(typeof(LineRenderer))]
    [DisallowMultipleComponent]
    public sealed class PressureBandageCourseMarker : MonoBehaviour
    {
        const int RingSegments = 32;

        [Tooltip("Ring radius (m).")]
        [SerializeField, Min(0.005f)] float size = 0.035f;
        [Tooltip("Shift toward the viewer (m).")]
        [SerializeField, Min(0f)] float towardViewer = 0.02f;

        LineRenderer line;
        Transform viewer;
        Transform target;
        bool following;

        public Transform Target => target;

        void Awake()
        {
            line = GetComponent<LineRenderer>();
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

        // Shows the ring around point (null hides it).
        public void Show(Transform point)
        {
            target = point;
            var on = point != null;
            if (gameObject.activeSelf != on)
                gameObject.SetActive(on);
            if (on)
                Follow();
        }

        void OnEnable()
        {
            if (!following)
            {
                Application.onBeforeRender += Follow;
                following = true;
            }
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
            var position = target.position;
            var toViewer = viewer.position - position;
            if (toViewer.sqrMagnitude < 1e-6f)
                return;
            var facing = toViewer.normalized;
            transform.SetPositionAndRotation(position + facing * towardViewer, Quaternion.LookRotation(-facing, Vector3.up));
        }
    }
}
