using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MilTrainSimVR.A.Assembly
{
    public enum PartState { Seated, Moving, Free }

    // 분해·결합 부품 하나.
    // 끼워진 동안은 끼울 자리의 축을 따라서만 움직이고, 정해진 거리 이상 빠지면 자유롭게 들 수 있다.
    // 얼마나 움직일 수 있는지, 끼울 수 있는지는 AssemblySequence 가 정한다.
    // 씬 시작 때 끼워진 상태로, 끼울 자리(SeatParent)의 자식으로 놓여 있어야 한다.
    [RequireComponent(typeof(XRGrabInteractable))]
    public class AssemblyPart : MonoBehaviour
    {
        [Tooltip("끼울 자리의 기준. 끼워진 동안 이 오브젝트의 자식이 된다")]
        [SerializeField] Transform m_SeatParent;

        [Tooltip("빠지는 방향 — SeatParent 기준 로컬 축")]
        [SerializeField] Vector3 m_SlideAxis = Vector3.up;

        [Tooltip("이만큼(m) 빠지면 분리된다")]
        [SerializeField] float m_RemoveDistance = 0.05f;

        [Tooltip("중간 걸림 위치(m). 0이면 없음")]
        [SerializeField] float m_DetentDistance;

        [Tooltip("떨어뜨렸을 때 되돌아갈 자리")]
        [SerializeField] Transform m_RestSpot;

        [Header("결합")]
        [Tooltip("끼워 넣는 길(빠지는 거리만큼의 축 위 선분)과 이 거리(m) 안이면 축에 물린다 — 자석처럼 (11/03 사용자: 근처면 붙게)")]
        [SerializeField] float m_SnapDistance = 0.10f;

        [Tooltip("부품의 끼우는 축이 끼울 자리 축과 이 각도(°) 안이면 물린다 — 축을 중심으로 비튼 것(roll)은 보지 않는다")]
        [SerializeField] float m_SnapAngle = 60f;

        [Tooltip("붙으면 밀어 넣지 않아도 바로 끼워지고 손에서 놓인다. 끼울 자리 쪽 부품을 들고 놓여 있는 이 부품에 가져다 대도 붙는다 — " +
                 "두 손에 하나씩 들고 합치는 부품 (노리쇠 뭉치 → 가스 활대, 11/03 헤드셋: 합치기가 잘 안 됨)")]
        [SerializeField] bool m_AutoSeat;

        const float k_SeatEpsilon = 0.002f;
        const float k_BlockedSlack = 0.015f;   // 막힌 방향으로 이만큼 더 당기면 막힘으로 본다
        const float k_DetachSlack = 0.01f;

        XRGrabInteractable m_Grab;
        Rigidbody m_Body;
        AssemblySequence m_Sequence;
        Vector3 m_SeatLocalPos;
        Quaternion m_SeatLocalRot;
        bool m_Inserting;
        bool m_PendingDrop;
        bool m_ErrorCounted;   // 이번 잡기에서 오류를 이미 셌는지
        bool m_ActiveAtGrab;   // 잡을 때 지금 단계의 부품이었는지
        bool m_SnapArmed;      // 분리한 뒤 끼울 자리에서 한 번 멀어졌는지 — 빼자마자 다시 물리지 않게
        IXRSelectInteractor m_Hand;
        float m_GrabOffset;
        Vector3 m_GrabHandLocal;

        public float Offset { get; private set; }
        public float RemoveDistance => m_RemoveDistance;
        public float DetentDistance => m_DetentDistance;
        public PartState State { get; private set; } = PartState.Seated;
        public bool AtDetent => m_DetentDistance > 0f && Offset >= m_DetentDistance - 0.003f;
        public bool IsHeld => m_Grab != null && m_Grab.isSelected;

        // 힌트(화살표·끼울 자리 표시)가 읽는다
        public Vector3 SlideAxisWorld => m_SeatParent.TransformDirection(m_SlideAxis);
        public Pose SeatedPose => new Pose(m_SeatParent.TransformPoint(m_SeatLocalPos), m_SeatParent.rotation * m_SeatLocalRot);

        // 끼울 자리의 시작 위치 — 분리된 부품을 여기 가져오면 축에 물린다
        public Pose EntryPose => new Pose(m_SeatParent.TransformPoint(m_SeatLocalPos + m_SlideAxis * m_RemoveDistance), m_SeatParent.rotation * m_SeatLocalRot);

        void Awake()
        {
            m_Grab = GetComponent<XRGrabInteractable>();
            m_Body = GetComponent<Rigidbody>();
            m_SlideAxis = m_SlideAxis.normalized;
            m_SeatLocalPos = transform.localPosition;
            m_SeatLocalRot = transform.localRotation;

            m_Grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            m_Grab.trackPosition = false;
            m_Grab.trackRotation = false;
            m_Grab.throwOnDetach = false;
            m_Grab.retainTransformParent = false;
            m_Grab.useDynamicAttach = true;   // 분리된 뒤에도 잡은 자세 그대로 든다
            m_Body.isKinematic = true;
            m_Body.interpolation = RigidbodyInterpolation.None;   // SyncInterpolation 참고
            AssemblyGrabPriority.Apply(m_Grab, () => m_Sequence != null && m_Sequence.IsActive(this));
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

        // 끼워진 처음 상태로 되돌린다 — 잡고 있으면 손에서 놓게 하고, 분리됐으면 끼울 자리로 옮긴다
        public void ResetToSeat()
        {
            // 먼저 놓게 한다 — 놓을 때 XRGrabInteractable 이 물리 상태를 되돌리므로 그 뒤에 다시 정한다
            if (m_Grab.isSelected && m_Grab.interactionManager != null)
                m_Grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)m_Grab);
            m_Hand = null;
            m_PendingDrop = false;
            m_Inserting = false;
            m_SnapArmed = false;
            if (!m_Body.isKinematic)
            {
                m_Body.linearVelocity = Vector3.zero;
                m_Body.angularVelocity = Vector3.zero;
            }
            m_Body.isKinematic = true;
            m_Body.interpolation = RigidbodyInterpolation.None;
            m_Grab.trackPosition = false;
            m_Grab.trackRotation = false;
            transform.SetParent(m_SeatParent, false);
            SetOffset(0f);
        }

        void OnGrab(SelectEnterEventArgs args)
        {
            m_Hand = args.interactorObject;
            m_PendingDrop = false;
            m_ErrorCounted = false;
            m_ActiveAtGrab = m_Sequence != null && m_Sequence.IsActive(this);
            if (State != PartState.Free)
            {
                // XRGrabInteractable 은 잡는 순간(Grab) 부모를 떼어 씬 루트로 옮긴다 → 끼워진 부품은 끼울 자리 기준 로컬 좌표로
                // 움직이므로 다시 붙인다. 안 붙이면 로컬 좌표가 월드 좌표로 쓰여 부품이 월드 원점 쪽으로 날아간다 (10/27 헤드셋)
                if (transform.parent != m_SeatParent)
                    transform.SetParent(m_SeatParent, true);
                BeginConstrained();
            }
        }

        void OnRelease(SelectExitEventArgs args)
        {
            m_Hand = null;
            KeepSeatedKinematic();
            // XRGrabInteractable 이 잡기 전 물리 상태로 되돌린 뒤라 다음 프레임에 떨어뜨린다
            if (State == PartState.Free)
                m_PendingDrop = true;
        }

        void Update()
        {
            KeepSeatedKinematic();
            SyncInterpolation();
            if (!IsHeld)
            {
                if (m_PendingDrop)
                {
                    m_PendingDrop = false;
                    m_Body.isKinematic = false;
                }
                if (State == PartState.Free)
                {
                    if (m_AutoSeat)
                        TrySnapFromSeat();
                    CheckFallen();
                }
                return;
            }

            if (m_Sequence == null)
                return;

            if (State == PartState.Free)
            {
                TrySnap();
                return;
            }

            float desired = m_GrabOffset + Vector3.Dot(HandLocal() - m_GrabHandLocal, m_SlideAxis);
            float max = m_Inserting ? m_RemoveDistance : m_Sequence.MaxOffset(this);
            bool canDetach = m_Inserting || max >= m_RemoveDistance;

            if (canDetach && desired > m_RemoveDistance + k_DetachSlack)
            {
                Detach();
                return;
            }

            // 지금 단계(또는 잡을 때 단계)의 부품을 정해진 범위보다 더 당긴 건 오류가 아니다
            // 걸림 위치 전이라 막힌 경우(max = 0)는 오류
            bool overPull = !m_Inserting && (m_ActiveAtGrab || m_Sequence.IsActive(this)) && max > 0f;
            if (desired > max + k_BlockedSlack && !overPull)
            {
                m_Sequence.ReportBlocked(m_Hand, !m_ErrorCounted);
                m_ErrorCounted = true;
            }

            SetOffset(Mathf.Clamp(desired, 0f, Mathf.Min(max, m_RemoveDistance)));

            if (m_Inserting && Offset <= k_SeatEpsilon)
            {
                m_Inserting = false;
                SetOffset(0f);
                m_Sequence.ReportSeated(m_Hand);
            }
        }

        // 끼워진(움직이지 않는) 부품은 Rigidbody 보간을 끈다 — 보간이 켜진 kinematic 자식은 부모(상부 본체)가 Transform 으로
        // 움직여도 물리 쪽 이전 자세로 Transform 을 덮어써서 뒤처진다 (10/27 헤드셋: 본체를 열면 안의 부품이 따라오지 않음,
        // 처음으로 눌러도 제자리로 안 돌아감). 떨어지는 부품만 켠다. XRI 는 놓을 때 잡을 때의 값으로 되돌리므로 매 프레임 맞춘다
        void SyncInterpolation()
        {
            var wanted = State == PartState.Free && !m_Body.isKinematic ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
            if (m_Body.interpolation != wanted)
                m_Body.interpolation = wanted;
        }

        // 끼워진(또는 끼우는 중인) 부품은 항상 kinematic — XRGrabInteractable 은 놓을 때 isKinematic 을 잡을 때의 값으로 되돌린다.
        // 매트 위(물리로 떨어진 상태)에서 집어 끼운 부품은 놓는 순간 물리가 켜져, 끼워진 채로 중력에 쓰러지거나(장전 손잡이)
        // 부모(가스 활대)를 들 때 튕겨 나갔다 (10/27 헤드셋)
        void KeepSeatedKinematic()
        {
            if (State == PartState.Free || m_Body.isKinematic)
                return;
            m_Body.linearVelocity = Vector3.zero;
            m_Body.angularVelocity = Vector3.zero;
            m_Body.isKinematic = true;
        }

        void BeginConstrained()
        {
            m_GrabOffset = Offset;
            m_GrabHandLocal = HandLocal();
        }

        Vector3 HandLocal() => m_SeatParent.InverseTransformPoint(m_Hand.transform.position);

        void SetOffset(float offset)
        {
            Offset = offset;
            transform.localPosition = m_SeatLocalPos + m_SlideAxis * offset;
            transform.localRotation = m_SeatLocalRot;
            if (!m_Inserting)
                State = offset > k_SeatEpsilon ? PartState.Moving : PartState.Seated;
        }

        void Detach()
        {
            State = PartState.Free;
            m_Inserting = false;
            m_SnapArmed = false;
            Offset = m_RemoveDistance;
            transform.SetParent(null, true);
            m_Grab.trackPosition = true;
            m_Grab.trackRotation = true;
            m_Sequence.ReportDetached(m_Hand);
        }

        // 자석 결합 (11/03 사용자: 각도가 조금 틀려도 근처에 가져다 대면 붙게).
        // 거리: 끼우는 길 — 끼워진 자리에서 빠지는 방향으로 m_RemoveDistance 까지의 선분 — 의 가장 가까운 점까지. 붙으면 그 깊이부터 축을 따라 밀어 넣는다.
        // 각도: 부품의 끼우는 축 방향만 본다 (막대를 길이 방향으로 비틀어 쥐어도 붙는다). 붙는 순간 자리의 회전으로 맞춰진다
        // 붙을 거리·각도 안인가. along = 끼우는 길 위 깊이(0 = 끼워진 자리). 분리한 뒤 한 번 멀어져야(m_SnapArmed) 붙는다 — 빼자마자 다시 물리지 않게
        bool InSnapRange(out float along)
        {
            Vector3 seat = m_SeatParent.TransformPoint(m_SeatLocalPos);
            Vector3 axis = m_SeatParent.TransformDirection(m_SlideAxis).normalized;
            along = Mathf.Clamp(Vector3.Dot(transform.position - seat, axis), 0f, m_RemoveDistance);
            float distance = Vector3.Distance(transform.position, seat + axis * along);
            if (!m_SnapArmed)
            {
                m_SnapArmed = distance > m_SnapDistance * 1.5f;
                return false;
            }
            Vector3 partAxis = Quaternion.Inverse(m_SeatLocalRot) * m_SlideAxis;   // 부품 자신 기준의 끼우는 축
            float angle = Vector3.Angle(transform.rotation * partAxis, axis);
            return distance <= m_SnapDistance && angle <= m_SnapAngle;
        }

        void TrySnap()
        {
            if (!InSnapRange(out float along))
                return;

            if (!m_Sequence.CanInsert(this))
            {
                // 순서가 아닌 부품: 근처를 지나가기만 한 건 세지 않고, 정말 끼우려 할 때(예전 기준 — 끼우는 시작점 5 cm · 축 45° 안)만 실수로 센다 (11/03)
                if (TryingToInsert())
                {
                    m_Sequence.ReportWrongInsert(m_Hand, !m_ErrorCounted);
                    m_ErrorCounted = true;
                }
                return;
            }

            m_Grab.trackPosition = false;
            m_Grab.trackRotation = false;
            transform.SetParent(m_SeatParent, true);
            m_Inserting = true;
            State = PartState.Moving;
            SetOffset(Mathf.Max(along, k_SeatEpsilon * 2f));   // 대어 놓은 깊이에서 시작 (바로 끼워진 걸로 치지 않게 조금 빼 둔다)
            BeginConstrained();
            m_Sequence.ReportSnapped(m_Hand);
            if (m_AutoSeat)
            {
                // 바로 끼우고 손에서 놓는다 — 들고 있으면 끼워진 부품을 당기는 걸로 보여 막힘 실수가 된다
                m_Inserting = false;
                SetOffset(0f);
                m_Sequence.ReportSeated(m_Hand);
                if (m_Grab.isSelected && m_Grab.interactionManager != null)
                    m_Grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)m_Grab);
            }
        }

        const float k_WrongInsertDistance = 0.05f;
        const float k_WrongInsertAngle = 45f;

        bool TryingToInsert()
        {
            Pose entry = EntryPose;
            Vector3 axis = m_SeatParent.TransformDirection(m_SlideAxis).normalized;
            Vector3 partAxis = Quaternion.Inverse(m_SeatLocalRot) * m_SlideAxis;
            return Vector3.Distance(transform.position, entry.position) <= k_WrongInsertDistance
                && Vector3.Angle(transform.rotation * partAxis, axis) <= k_WrongInsertAngle;
        }

        // 놓여 있는 이 부품에, 끼울 자리 쪽 부품(SeatParent 의 AssemblyPart)을 들고 가져다 댄 경우 (m_AutoSeat 만).
        // 순서가 아니면 붙지 않고 실수로도 세지 않는다 (손에 든 부품이 아니므로)
        void TrySnapFromSeat()
        {
            var seatPart = m_SeatParent.GetComponentInParent<AssemblyPart>();
            bool seatHeld = seatPart != null && seatPart != this && seatPart.IsHeld;
            if (!InSnapRange(out _) || !seatHeld || m_Sequence == null || !m_Sequence.CanInsert(this))
                return;
            if (!m_Body.isKinematic)
            {
                m_Body.linearVelocity = Vector3.zero;
                m_Body.angularVelocity = Vector3.zero;
            }
            m_Body.isKinematic = true;
            m_PendingDrop = false;
            m_Grab.trackPosition = false;
            m_Grab.trackRotation = false;
            transform.SetParent(m_SeatParent, true);
            m_Inserting = false;
            SetOffset(0f);
            m_Sequence.ReportSnapped(seatPart.m_Hand);
            m_Sequence.ReportSeated(seatPart.m_Hand);
        }

        void CheckFallen()
        {
            if (m_RestSpot == null || transform.position.y > m_RestSpot.position.y - 0.4f)
                return;
            if (!m_Body.isKinematic)
            {
                m_Body.linearVelocity = Vector3.zero;
                m_Body.angularVelocity = Vector3.zero;
            }
            transform.SetPositionAndRotation(m_RestSpot.position, m_RestSpot.rotation);
        }

        // --- 시험용 (AssemblySequence.TestCompleteCurrent) ---

        internal void TestDetach() => Detach();

        internal void TestSetOffset(float offset) => SetOffset(offset);
    }
}
