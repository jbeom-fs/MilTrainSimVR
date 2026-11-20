// 원본: Assets/C/Scripts/RifleRecoil.cs (커밋 a1a51ac, C 이주석). A 가 복사해 namespace 만 바꿨다 — Docs/A/1027_C사격코드_재사용검토.md
// 11/17 총기 통합: 숨 → 반동 배율 (C 7f4d63a 와 같은 방식) — Docs/A/1117_총기통합_4_숨반동.md
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace MilTrainSimVR.A.Rifle
{
    // 반동 — 쏠 때마다 총을 손에서 뒤로 밀고 총구를 들었다가 천천히 제자리로 돌린다.
    // XRI 의 '잡기 변환기(grab transformer)' 라서, 손이 정한 총 위치 위에 반동만 더한다
    // (총의 Transform 을 직접 움직이면 XRI 가 다음 프레임에 손 위치로 덮어쓴다).
    // XRGrabInteractable 의 변환기 목록에서 XRGeneralGrabTransformer '뒤'에 와야 한다 — 손 위치가 먼저 정해지고 그 위에 더한다.
    // 숨 참기 (11/17): 같은 총의 조준 확대(SightAim) 숨 상태 배율을 곱한다 — 참는 동안 한 발 반동 0.6배 · 1.5배 빨리 돌아옴, 숨 한계 땐 1.3배 · 0.8배
    public class RifleRecoil : XRBaseGrabTransformer
    {
        [Tooltip("한 발에 뒤로 밀리는 거리(m)")]
        [SerializeField] float m_KickBack = 0.025f;

        [Tooltip("한 발에 총구가 들리는 각도(도)")]
        [SerializeField] float m_KickUp = 3f;

        [Tooltip("한 발에 좌우로 틀어지는 최대 각도(도) — 매번 무작위")]
        [SerializeField] float m_KickSide = 0.8f;

        [Tooltip("연발로 쌓일 수 있는 최대치 — x: 뒤로(m), y: 위로(도)")]
        [SerializeField] Vector2 m_Max = new Vector2(0.06f, 12f);

        [Tooltip("제자리로 돌아오는 빠르기 — 클수록 빨리 돌아온다")]
        [SerializeField] float m_Recovery = 12f;

        [Tooltip("두 손으로 잡았을 때의 반동 비율 — 앞손이 받쳐 줘서 덜 튄다")]
        [Range(0f, 1f)]
        [SerializeField] float m_TwoHandFactor = 0.5f;

        float m_Back;   // 지금 뒤로 밀려 있는 거리
        float m_Up;     // 지금 들려 있는 각도
        float m_Side;   // 지금 옆으로 틀어진 각도
        SightAim m_Breath;

        public float CurrentBack => m_Back;
        public float CurrentUp => m_Up;

        protected override RegistrationMode registrationMode => RegistrationMode.SingleAndMultiple;

        void Awake()
        {
            m_Breath = GetComponent<SightAim>();
        }

        public void Kick(bool twoHanded)
        {
            float scale = (twoHanded ? m_TwoHandFactor : 1f) * (m_Breath != null ? m_Breath.RecoilScale : 1f);
            m_Back = Mathf.Min(m_Back + m_KickBack * scale, m_Max.x);
            m_Up = Mathf.Min(m_Up + m_KickUp * scale, m_Max.y);
            m_Side += Random.Range(-m_KickSide, m_KickSide) * scale;
        }

        void Update()
        {
            // 시간에 따라 0 으로 돌아온다 (프레임 수와 상관없이 같은 빠르기)
            float recovery = m_Recovery * (m_Breath != null ? m_Breath.RecoveryScale : 1f);
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
