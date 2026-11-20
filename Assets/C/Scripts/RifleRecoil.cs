using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace MilTrainSimVR.UrbanCombat
{
    // 반동 — 쏠 때마다 총을 손에서 뒤로 밀고 총구를 들었다가 천천히 제자리로 돌린다.
    // XRI 의 '잡기 변환기(grab transformer)' 라서, 손이 정한 총 위치 위에 반동만 더한다
    // (총의 Transform 을 직접 움직이면 XRI 가 다음 프레임에 손 위치로 덮어쓴다).
    // XRGrabInteractable 의 변환기 목록에서 XRGeneralGrabTransformer '뒤'에 와야 한다 — 손 위치가 먼저 정해지고 그 위에 더한다.
    // 한 손 · 두 손 (11/12 — 11/11 헤드셋: "한 손으로 쏘면 쏘기 힘들 만큼 반동이 세야 하지 않나"): 전에는 한 손이 두 손의 2배일 뿐이라
    // (한 발에 3° · 쌓여도 12° · 금방 돌아옴) 한 손 연발도 맞힐 수 있었다. 이제 값을 따로 둔다 — 두 손은 전과 같고, 한 손은 크게 튀고
    // 느리게 돌아와서 단발은 기다렸다 쏘면 맞지만 연발은 총구가 20° 가까이 들린 채로 남는다. 총만 움직이고 시야(카메라)는 건드리지 않는다
    // 숨 참기 (11/17): 같은 총에 RifleBreath 가 있으면 그 배율을 곱한다 — 참는 동안 한 발 반동 0.6배 · 1.5배 빨리 돌아옴, 숨 한계 땐 1.3배 · 0.8배
    public class RifleRecoil : XRBaseGrabTransformer
    {
        // 한 발의 반동 · 쌓이는 한계 · 돌아오는 빠르기
        [System.Serializable]
        public struct Settings
        {
            [Tooltip("한 발에 뒤로 밀리는 거리(m)")]
            public float back;

            [Tooltip("한 발에 총구가 들리는 각도(도)")]
            public float up;

            [Tooltip("한 발에 좌우로 틀어지는 최대 각도(도) — 매번 무작위")]
            public float side;

            [Tooltip("연발로 쌓일 수 있는 최대치 — 뒤로(m)")]
            public float maxBack;

            [Tooltip("연발로 쌓일 수 있는 최대치 — 위로(도)")]
            public float maxUp;

            [Tooltip("제자리로 돌아오는 빠르기 — 클수록 빨리 돌아온다 (12 = 0.2초에 9%까지, 5 = 0.2초에 37%까지)")]
            public float recovery;
        }

        [Tooltip("두 손 — 앞손이 총열덮개를 받치고 있을 때")]
        [SerializeField] Settings m_TwoHands = new Settings { back = 0.0125f, up = 1.5f, side = 0.4f, maxBack = 0.06f, maxUp = 12f, recovery = 12f };

        [Tooltip("한 손 — 방아쇠 손만으로 들었을 때. 단발은 돌아오기를 기다리면 쏠 만하고, 연발은 못 맞히게")]
        [SerializeField] Settings m_OneHand = new Settings { back = 0.03f, up = 6.5f, side = 2.5f, maxBack = 0.08f, maxUp = 25f, recovery = 5f };

        RifleHands m_Hands;
        RifleBreath m_Breath;
        float m_Back;   // 지금 뒤로 밀려 있는 거리
        float m_Up;     // 지금 들려 있는 각도
        float m_Side;   // 지금 옆으로 틀어진 각도

        public float CurrentBack => m_Back;
        public float CurrentUp => m_Up;

        protected override RegistrationMode registrationMode => RegistrationMode.SingleAndMultiple;

        void Awake()
        {
            m_Hands = GetComponent<RifleHands>();
            m_Breath = GetComponent<RifleBreath>();
        }

        // 한 발의 반동을 더한다. amount = 한 발 대비 크기 — 장전 손잡이를 놓아 노리쇠가 닫힐 때는 작게(RifleChargingHandle)
        public void Kick(bool twoHanded, float amount = 1f)
        {
            Settings kick = twoHanded ? m_TwoHands : m_OneHand;
            if (m_Breath != null)
                amount *= m_Breath.RecoilScale;
            // 한 손으로 쏘다 앞손을 대면 한계가 낮아진다 — 그때 이미 그보다 더 들려 있으면 끌어내리지 않고 그대로 둔다 (돌아오기가 내린다)
            m_Back = Mathf.Max(m_Back, Mathf.Min(m_Back + kick.back * amount, kick.maxBack));
            m_Up = Mathf.Max(m_Up, Mathf.Min(m_Up + kick.up * amount, kick.maxUp));
            m_Side += Random.Range(-kick.side, kick.side) * amount;
        }

        void Update()
        {
            // 시간에 따라 0 으로 돌아온다 (프레임 수와 상관없이 같은 빠르기) — 지금 앞손이 받치고 있으면 빨리, 한 손이면 느리게
            bool twoHanded = m_Hands != null && m_Hands.IsSupported;
            float recovery = (twoHanded ? m_TwoHands.recovery : m_OneHand.recovery) * (m_Breath != null ? m_Breath.RecoveryScale : 1f);
            float t = 1f - Mathf.Exp(-recovery * Time.deltaTime);
            m_Back = Mathf.Lerp(m_Back, 0f, t);
            m_Up = Mathf.Lerp(m_Up, 0f, t);
            m_Side = Mathf.Lerp(m_Side, 0f, t);
        }

        public override void Process(XRGrabInteractable grabInteractable, XRInteractionUpdateOrder.UpdatePhase updatePhase, ref Pose targetPose, ref Vector3 localScale)
        {
            // XRGeneralGrabTransformer 가 손 위치를 정하는 두 단계에서만 더한다
            if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic && updatePhase != XRInteractionUpdateOrder.UpdatePhase.OnBeforeRender)
                return;

            // 총 기준으로 뒤(-Z)로 밀고, 총구를 위로(-X 회전) 들고, 옆으로(Y 회전) 튼다
            targetPose.position += targetPose.rotation * new Vector3(0f, 0f, -m_Back);
            targetPose.rotation *= Quaternion.Euler(-m_Up, m_Side, 0f);
        }
    }
}
