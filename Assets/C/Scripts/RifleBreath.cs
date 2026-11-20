using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace MilTrainSimVR.UrbanCombat
{
    // 숨 참기 (11/17 — A 의 SightAim 숨 참기를 C 총에 넣음. A 사로가 C 총을 쓰게).
    // 두 손으로 받친 채 앞손 트리거를 누르고 있으면 숨을 참는다 — A 와 같은 입력 · 같은 규칙 · 같은 값:
    //   · 보통: 총이 숨 따라 아주 조금 흔들린다 (2 mrad = 25 m 에서 ±5 cm)
    //   · 숨 참기: 흔들림이 거의 없어지고(0.15 mrad) 총이 손 떨림을 천천히 따라간다(시간 상수 m_SteadyHolding) — 거의 멈춘 듯 겨눌 수 있다
    //   · 6초를 넘기면 숨 한계: 흔들림이 커지고(4 mrad), 트리거를 놓고 2.5초 쉬어야 다시 참을 수 있다
    // A 는 확대 창의 조준선만 흔들었지만 C 엔 확대 창이 없어 총 자체를 움직인다 — 가늠자 · 가늠쇠 · 총구가 같이 움직여 보이는 곳 = 맞는 곳.
    // 반동 (사용자 11/17 "숨참기로 반동을 더 잡을 수 있게"): 반동은 C 것(RifleRecoil)을 쓰고, 참는 동안은 한 발 반동이 작아지고 빨리 돌아온다.
    //   숨 한계 땐 반대로 커지고 느리게 돌아온다 — RifleRecoil 이 RecoilScale · RecoveryScale 을 읽는다.
    // XRI 잡기 변환기 — 목록에서 RifleHands(두 손 조준) 뒤, RifleRecoil(반동) 앞. 반동은 이 거름 위에 더해져서 참는 동안에도 날카롭게 튄다.
    // 손 모델은 총에 붙어 있어(RifleHands) 총과 같이 움직인다. 한 손으로 들었을 땐 숨을 참지 못한다(앞손 트리거가 없다)
    public class RifleBreath : XRBaseGrabTransformer
    {
        public enum State { Normal, Holding, Exhausted }   // 보통 · 숨 참기 · 숨 한계 — A 의 BreathState 와 같은 순서

        [Header("숨 (A SightAim 과 같은 값)")]
        [Tooltip("숨을 참을 수 있는 시간(초) — 넘기면 숨 한계")]
        [SerializeField] float m_HoldSeconds = 6f;

        [Tooltip("숨 한계 뒤 트리거를 놓고 쉬어야 하는 시간(초). 보통 상태에선 참은 시간이 이 비율로 줄어든다")]
        [SerializeField] float m_RecoverSeconds = 2.5f;

        [Tooltip("앞손 트리거를 이만큼(0~1) 누르면 숨 참기 시작")]
        [SerializeField] float m_HoldAt = 0.6f;

        [Tooltip("이만큼 아래로 풀면 숨을 내쉰다")]
        [SerializeField] float m_ReleaseAt = 0.4f;

        [Header("흔들림 (mrad — 1 mrad = 25 m 에서 2.5 cm)")]
        [SerializeField] float m_SwayNormal = 2f;
        [SerializeField] float m_SwayHolding = 0.15f;
        [SerializeField] float m_SwayExhausted = 4f;

        [Tooltip("흔들림 빠르기(Hz)")]
        [SerializeField] float m_SwayFrequency = 0.35f;

        [Header("손 떨림 거르기 (초 — 총이 손을 따라가는 시간 상수)")]
        [Tooltip("보통 — 0 = 손 그대로 (시가지 전투 감각을 바꾸지 않게. A 확대 창은 0.06)")]
        [SerializeField] float m_SteadyNormal = 0f;

        [Tooltip("숨 참기 — 클수록 거의 멈춘 듯 (A 확대 창은 0.4. C 는 총 자체가 늦게 따라오므로 조금 짧게)")]
        [SerializeField] float m_SteadyHolding = 0.3f;

        [Header("반동 배율 (RifleRecoil 이 읽는다)")]
        [Tooltip("숨 참기 — 한 발 반동(뒤로 · 위로 · 옆으로) 배율")]
        [SerializeField] float m_RecoilHolding = 0.6f;

        [Tooltip("숨 참기 — 반동이 돌아오는 빠르기 배율")]
        [SerializeField] float m_RecoveryHolding = 1.5f;

        [Tooltip("숨 한계 — 한 발 반동 배율")]
        [SerializeField] float m_RecoilExhausted = 1.3f;

        [Tooltip("숨 한계 — 돌아오는 빠르기 배율")]
        [SerializeField] float m_RecoveryExhausted = 0.8f;

        XRGrabInteractable m_Grab;
        RifleHands m_Hands;
        Vector3 m_PivotLocal;      // 총이 도는 중심 = 손잡이 잡기 점 (총 기준) — 손잡이는 손에 남고 총구 쪽이 움직인다
        bool m_Input;              // 앞손 트리거를 누르고 있는지 (경계에서 떨지 않게 두 값 사이)
        float m_HoldTime;          // 참은 시간 — 보통 상태에서 천천히 줄어든다
        float m_RestTime;          // 숨 한계 뒤 쉰 시간
        float m_Steady;            // 지금 거르기 시간 상수 — 상태가 바뀌면 0.25초에 걸쳐 바뀐다 (툭 끊기지 않게)
        float m_Sway;              // 지금 흔들림 크기(mrad) — 상태가 바뀌면 부드럽게
        float m_NoiseTime;
        float m_NextTick;          // 숨이 얼마 안 남았을 때 앞손에 톡톡 — 다음 시각
        Quaternion m_SwayRotation = Quaternion.identity;
        Quaternion m_Base;         // 이 프레임 거르기 시작점 = 지난 프레임 결과
        Quaternion m_Output;       // 거른 총 방향 (흔들림 빼고)
        int m_FilterFrame = -10;

        public State Breath { get; private set; }

        // 남은 숨 (1 = 가득, 0 = 한계) — A 의 BreathLeft 와 같다. 숨 막대를 그릴 때
        public float BreathLeft => Mathf.Clamp01(1f - m_HoldTime / m_HoldSeconds);

        public float RecoilScale => Breath switch
        {
            State.Holding => m_RecoilHolding,
            State.Exhausted => m_RecoilExhausted,
            _ => 1f,
        };

        public float RecoveryScale => Breath switch
        {
            State.Holding => m_RecoveryHolding,
            State.Exhausted => m_RecoveryExhausted,
            _ => 1f,
        };

        public event System.Action<State> BreathChanged;

        // 시험용 (손 없이) — 앞손 트리거를 누른 것으로 친다 (두 손 받침이 없어도)
        public bool TestBreath { get; set; }

        protected override RegistrationMode registrationMode => RegistrationMode.SingleAndMultiple;

        void Awake()
        {
            m_Grab = GetComponent<XRGrabInteractable>();
            m_Hands = GetComponent<RifleHands>();
            if (m_Grab != null && m_Grab.attachTransform != null)
                m_PivotLocal = transform.InverseTransformPoint(m_Grab.attachTransform.position);
            m_Sway = m_SwayNormal;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            NearFarInteractor support = m_Hands != null ? m_Hands.SupportHand : null;
            if (support != null)
            {
                float pull = support.activateInput.ReadValue();
                if (!m_Input && pull >= m_HoldAt)
                    m_Input = true;
                else if (m_Input && pull <= m_ReleaseAt)
                    m_Input = false;
            }
            else
            {
                m_Input = false;   // 앞손을 떼면 숨도 내쉰다
            }

            UpdateBreath(dt, m_Input || TestBreath);

            // 거르기 · 흔들림 크기를 새 상태 쪽으로 부드럽게
            float steadyTarget = Breath == State.Holding ? m_SteadyHolding : m_SteadyNormal;
            m_Steady = Mathf.MoveTowards(m_Steady, steadyTarget, dt * Mathf.Max(m_SteadyHolding, 0.01f) / 0.25f);
            float swayTarget = Breath switch
            {
                State.Holding => m_SwayHolding,
                State.Exhausted => m_SwayExhausted,
                _ => m_SwayNormal,
            };
            m_Sway = Mathf.Lerp(m_Sway, swayTarget, 1f - Mathf.Exp(-dt / 0.3f));

            // 흔들림 — 두 겹 펄린 잡음 (A 와 같은 식). mrad → 도
            m_NoiseTime += dt * m_SwayFrequency;
            float amp = m_Sway * 0.001f * Mathf.Rad2Deg;
            float yaw = (Mathf.PerlinNoise(m_NoiseTime, 0.3f) - 0.5f) * 2f * amp;
            float pitch = (Mathf.PerlinNoise(0.7f, m_NoiseTime) - 0.5f) * 2f * amp;
            m_SwayRotation = Quaternion.Euler(pitch, yaw, 0f);

            // 숨이 얼마 안 남았다 — 앞손에 0.5초마다 톡 (C 엔 숨 막대가 없다)
            if (Breath == State.Holding && BreathLeft < 0.3f && Time.time >= m_NextTick)
            {
                m_NextTick = Time.time + 0.5f;
                Buzz(support, 0.12f, 0.03f);
            }
        }

        // A SightAim.UpdateBreath 와 같은 규칙
        void UpdateBreath(float dt, bool holding)
        {
            State next = Breath;
            switch (Breath)
            {
                case State.Normal:
                    m_HoldTime = Mathf.Max(0f, m_HoldTime - dt * m_HoldSeconds / Mathf.Max(m_RecoverSeconds, 0.01f));
                    if (holding && m_HoldTime < m_HoldSeconds)
                        next = State.Holding;
                    break;
                case State.Holding:
                    m_HoldTime += dt;
                    if (!holding)
                        next = State.Normal;
                    else if (m_HoldTime >= m_HoldSeconds)
                        next = State.Exhausted;
                    break;
                case State.Exhausted:
                    m_RestTime = holding ? 0f : m_RestTime + dt;
                    if (m_RestTime >= m_RecoverSeconds)
                    {
                        m_HoldTime = 0f;
                        next = State.Normal;
                    }
                    break;
            }
            if (next == Breath)
                return;
            if (next == State.Exhausted)
                m_RestTime = 0f;
            Breath = next;
            NearFarInteractor support = m_Hands != null ? m_Hands.SupportHand : null;
            if (next == State.Holding)
                Buzz(support, 0.08f, 0.05f);
            else if (next == State.Exhausted)
            {
                // 숨 한계 — 두 손에 크게 한 번
                Buzz(support, 0.4f, 0.2f);
                if (m_Grab != null && m_Grab.interactorsSelecting.Count > 0 && m_Grab.interactorsSelecting[0] is XRBaseInputInteractor grip)
                    grip.SendHapticImpulse(0.4f, 0.2f);
            }
            Debug.Log($"[C] 숨: {Breath}");
            BreathChanged?.Invoke(Breath);
        }

        static void Buzz(XRBaseInputInteractor hand, float amplitude, float duration)
        {
            if (hand != null && hand.isActiveAndEnabled)
                hand.SendHapticImpulse(amplitude, duration);
        }

        public override void Process(XRGrabInteractable grabInteractable, XRInteractionUpdateOrder.UpdatePhase updatePhase, ref Pose targetPose, ref Vector3 localScale)
        {
            // 손 위치가 정해지는 두 단계에서만
            if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic && updatePhase != XRInteractionUpdateOrder.UpdatePhase.OnBeforeRender)
                return;

            Quaternion raw = targetPose.rotation;
            int frame = Time.frameCount;
            if (frame != m_FilterFrame)
            {
                // 새 프레임: 지난 프레임 결과에서 시작한다. 바로 전 프레임에 안 들고 있었으면(막 잡음) 손 방향에서 새로 시작
                m_Base = frame - m_FilterFrame == 1 ? m_Output : raw;
                m_FilterFrame = frame;
            }
            // 한 프레임에 두 번(Dynamic · 그리기 직전) 불려도 같은 시작점에서 최신 손 방향으로 — 두 번 거르지 않게
            float k = m_Steady > 1e-4f ? 1f - Mathf.Exp(-Time.deltaTime / m_Steady) : 1f;
            m_Output = Quaternion.Slerp(m_Base, raw, k);

            // 손잡이 잡기 점을 중심으로 돌린다 — 손잡이는 손 자리에 남는다
            Quaternion final = m_Output * m_SwayRotation;
            Vector3 pivot = targetPose.position + raw * m_PivotLocal;
            targetPose.position = pivot - final * m_PivotLocal;
            targetPose.rotation = final;
        }
    }
}
