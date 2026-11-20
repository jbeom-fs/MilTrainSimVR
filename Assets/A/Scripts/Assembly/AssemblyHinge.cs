using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MilTrainSimVR.A.Assembly
{
    // 총기 본체 열기·닫기. 잡은 손의 움직임을 힌지 축 회전으로만 바꾼다.
    // 이 오브젝트(상부)는 Pivot 의 자식이고, Pivot 의 로컬 회전을 바꿔 연다. 0° = 닫힘.
    [RequireComponent(typeof(XRGrabInteractable))]
    public class AssemblyHinge : MonoBehaviour
    {
        [Tooltip("회전 중심. 이 오브젝트의 부모")]
        [SerializeField] Transform m_Pivot;

        [Tooltip("회전축 — Pivot 부모 기준 로컬 축. +각도가 열리는 방향")]
        [SerializeField] Vector3 m_LocalAxis = Vector3.back;

        [Tooltip("열림 각도(°)")]
        [SerializeField] float m_OpenAngle = 25f;

        const float k_BlockedSlack = 4f;

        XRGrabInteractable m_Grab;
        Rigidbody m_Body;
        AssemblySequence m_Sequence;
        Quaternion m_ClosedLocalRot;
        IXRSelectInteractor m_Hand;
        bool m_ErrorCounted;
        bool m_MovableAtGrab;   // 잡을 때 열기·닫기 단계였는지
        float m_GrabAngle;
        Vector3 m_GrabDir;

        public float Angle { get; private set; }
        public float OpenAngle => m_OpenAngle;
        public bool IsOpen => Angle >= m_OpenAngle - 1f;
        public bool IsClosed => Angle <= 0.5f;

        void Awake()
        {
            m_Grab = GetComponent<XRGrabInteractable>();
            m_Grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            m_Grab.trackPosition = false;
            m_Grab.trackRotation = false;
            m_Grab.throwOnDetach = false;
            m_Body = GetComponent<Rigidbody>();
            m_Body.isKinematic = true;
            m_Body.interpolation = RigidbodyInterpolation.None;   // Update 참고
            AssemblyGrabPriority.Apply(m_Grab, () => m_Sequence != null && m_Sequence.IsActive(this));
            m_ClosedLocalRot = m_Pivot.localRotation;
            m_LocalAxis = m_LocalAxis.normalized;
        }

        void OnEnable()
        {
            m_Grab.selectEntered.AddListener(OnGrab);
            m_Grab.selectExited.AddListener(OnRelease);
        }

        void OnDisable()
        {
            m_Grab.selectEntered.RemoveListener(OnGrab);
            m_Grab.selectExited.RemoveListener(OnRelease);
        }

        public void Bind(AssemblySequence sequence) => m_Sequence = sequence;

        // 닫힌 처음 상태로 되돌린다 — 잡고 있으면 손에서 놓게 한다
        public void ResetClosed()
        {
            if (m_Grab.isSelected && m_Grab.interactionManager != null)
                m_Grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)m_Grab);
            m_Hand = null;
            SetAngle(0f);
        }

        void OnGrab(SelectEnterEventArgs args)
        {
            // XRGrabInteractable 은 잡는 순간(Grab) 부모를 떼어 씬 루트로 옮긴다 → 회전 중심(m_Pivot)에 다시 붙인다.
            // 안 붙이면 회전 중심을 돌려도 본체가 따라 돌지 않는다 (10/27 헤드셋: 본체가 안 열림)
            if (transform.parent != m_Pivot)
                transform.SetParent(m_Pivot, true);
            m_Hand = args.interactorObject;
            m_ErrorCounted = false;
            if (m_Sequence != null)
            {
                Vector2 range = m_Sequence.HingeRange(this);
                m_MovableAtGrab = !Mathf.Approximately(range.x, range.y);
            }
            m_GrabAngle = Angle;
            m_GrabDir = HandDir(AxisWorld());
        }

        void OnRelease(SelectExitEventArgs args) => m_Hand = null;

        void Update()
        {
            // 상부 본체는 회전 중심(부모)을 돌려 움직인다 — 보간이 켜진 kinematic Rigidbody 는 부모를 따라가지 못하고 뒤처진다.
            // XRI 가 놓을 때 잡을 때의 보간 값으로 되돌릴 수 있어 매 프레임 끈다
            if (m_Body.interpolation != RigidbodyInterpolation.None)
                m_Body.interpolation = RigidbodyInterpolation.None;
            if (m_Hand == null || m_Sequence == null)
                return;

            Vector3 axis = AxisWorld();
            Vector3 dir = HandDir(axis);
            if (dir.sqrMagnitude < 1e-6f || m_GrabDir.sqrMagnitude < 1e-6f)
                return;

            float desired = m_GrabAngle + Vector3.SignedAngle(m_GrabDir, dir, axis);
            // 열기·닫기 단계가 아니면 잠겨 있다 (범위 최소 = 최대).
            // 단계 중에 끝까지 더 돌리거나, 단계가 끝난 뒤 그대로 잡고 있는 건 오류가 아니다
            Vector2 range = m_Sequence.HingeRange(this);
            bool locked = Mathf.Approximately(range.x, range.y);
            if (locked && !m_MovableAtGrab && Mathf.Abs(desired - range.x) > k_BlockedSlack)
            {
                m_Sequence.ReportBlocked(m_Hand, !m_ErrorCounted);
                m_ErrorCounted = true;
            }

            bool wasClosed = IsClosed;
            SetAngle(Mathf.Clamp(desired, range.x, range.y));
            if (!wasClosed && IsClosed)
                m_Sequence.ReportSeated(m_Hand);
        }

        Vector3 AxisWorld() => m_Pivot.parent.TransformDirection(m_LocalAxis).normalized;

        // 열리는 쪽(+각도)으로 돌 때 worldPoint 가 움직이는 방향 — 힌트 화살표가 쓴다.
        // 회전 부호를 따지지 않도록 실제로 1° 돌려 본 차이로 구한다
        public Vector3 OpenDirectionAt(Vector3 worldPoint)
        {
            Vector3 r = worldPoint - m_Pivot.position;
            Vector3 moved = Quaternion.AngleAxis(1f, AxisWorld()) * r;
            return (moved - r).normalized;
        }

        Vector3 HandDir(Vector3 axis) => Vector3.ProjectOnPlane(m_Hand.transform.position - m_Pivot.position, axis);

        void SetAngle(float angle)
        {
            Angle = angle;
            m_Pivot.localRotation = Quaternion.AngleAxis(angle, m_LocalAxis) * m_ClosedLocalRot;
        }

        // 시험용 (AssemblySequence.TestCompleteCurrent)
        internal void TestSetAngle(float angle) => SetAngle(angle);
    }
}
