using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MilTrainSimVR.A.Rifle
{
    // 장전 손잡이 — 잡은 손을 총 뒤쪽(-Z)으로만 따라가고, 놓으면 앞으로 돌아온다.
    // m_MoveWith 에 넣은 것도 같이 민다. K2C1_Rifle 은 비워 둔다 — 가스 활대를 같이 밀면 모델에서 본체 뒤로 튀어나온다 (10/27 헤드셋).
    // 끝까지 당기면 PulledBack, 끝까지 당겼다가 앞으로 다 돌아오면 ReturnedForward — 공이치기·약실은 총(K2C1Rifle)이 정한다.
    [RequireComponent(typeof(XRGrabInteractable))]
    public class ChargingHandleSlide : MonoBehaviour
    {
        [Tooltip("움직임 기준 — 총 루트. 이 오브젝트의 뒤(-Z)로 당긴다")]
        [SerializeField] Transform m_Frame;

        [Tooltip("장전 손잡이와 함께 뒤로 움직이는 것. 비우면 장전 손잡이만 움직인다")]
        [SerializeField] Transform[] m_MoveWith;

        [Tooltip("끝까지 당기는 거리(m)")]
        [SerializeField] float m_Stroke = 0.09f;

        [Tooltip("놓았을 때 앞으로 돌아오는 빠르기(m/s) — 스프링에 밀려 빠르게 닫힌다")]
        [SerializeField] float m_ReturnSpeed = 2.5f;

        const float k_FullSlack = 0.004f;   // 끝까지 당겼다고 볼 여유
        const float k_HomeSlack = 0.0005f;

        XRGrabInteractable m_Grab;
        Transform m_Parent;
        IXRSelectInteractor m_Hand;
        Vector3[] m_RestLocal;     // 0 = 자신, 1.. = m_MoveWith
        Vector3[] m_BackLocal;     // 각 부모 기준 뒤 방향
        Transform[] m_Moving;
        float m_GrabOffset;
        float m_GrabHandZ;
        bool m_ReachedBack;

        public event System.Action PulledBack;
        public event System.Action ReturnedForward;

        public float Offset { get; private set; }
        public float Stroke => m_Stroke;
        public bool IsHeld => m_Hand != null;

        void Awake()
        {
            m_Grab = GetComponent<XRGrabInteractable>();
            m_Grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            m_Grab.trackPosition = false;
            m_Grab.trackRotation = false;
            m_Grab.throwOnDetach = false;
            m_Grab.retainTransformParent = true;
            var body = GetComponent<Rigidbody>();
            if (body != null) body.isKinematic = true;

            m_Parent = transform.parent;
            m_Moving = new Transform[1 + m_MoveWith.Length];
            m_Moving[0] = transform;
            m_MoveWith.CopyTo(m_Moving, 1);
            m_RestLocal = new Vector3[m_Moving.Length];
            m_BackLocal = new Vector3[m_Moving.Length];
            Vector3 backWorld = m_Frame.TransformDirection(Vector3.back);
            for (int i = 0; i < m_Moving.Length; i++)
            {
                m_RestLocal[i] = m_Moving[i].localPosition;
                m_BackLocal[i] = m_Moving[i].parent.InverseTransformDirection(backWorld);
            }
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
            m_Hand = null;
        }

        void OnGrab(SelectEnterEventArgs args)
        {
            // 잡는 동안에도 총에 붙어 있어야 축 계산이 맞는다
            if (transform.parent != m_Parent)
                transform.SetParent(m_Parent, true);
            m_Hand = args.interactorObject;
            m_GrabOffset = Offset;
            m_GrabHandZ = HandZ();
        }

        void OnRelease(SelectExitEventArgs args) => m_Hand = null;

        void Update()
        {
            float target;
            if (m_Hand != null)
                target = m_GrabOffset + (m_GrabHandZ - HandZ());   // 손이 뒤(-Z)로 간 만큼
            else if (Offset > 0f)
                target = Mathf.MoveTowards(Offset, 0f, m_ReturnSpeed * Time.deltaTime);
            else
                return;
            SetOffset(target);
        }

        float HandZ() => m_Frame.InverseTransformPoint(m_Hand.transform.position).z;

        // 시험·초기화용으로도 쓴다 — 끝까지/처음 위치 판정도 여기서 한다
        public void SetOffset(float offset)
        {
            Offset = Mathf.Clamp(offset, 0f, m_Stroke);
            for (int i = 0; i < m_Moving.Length; i++)
                m_Moving[i].localPosition = m_RestLocal[i] + m_BackLocal[i] * Offset;

            if (!m_ReachedBack && Offset >= m_Stroke - k_FullSlack)
            {
                m_ReachedBack = true;
                Buzz(0.4f, 0.05f);
                PulledBack?.Invoke();
            }
            else if (m_ReachedBack && Offset <= k_HomeSlack)
            {
                m_ReachedBack = false;
                ReturnedForward?.Invoke();
            }
        }

        // 앞으로 닫힌 처음 상태로 — 잡고 있으면 손에서 놓게 한다
        public void ResetForward()
        {
            if (m_Grab.isSelected && m_Grab.interactionManager != null)
                m_Grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)m_Grab);
            m_Hand = null;
            m_ReachedBack = false;
            SetOffset(0f);
        }

        void Buzz(float amplitude, float duration)
        {
            if (m_Hand is XRBaseInputInteractor input)
                input.SendHapticImpulse(amplitude, duration);
        }
    }
}
