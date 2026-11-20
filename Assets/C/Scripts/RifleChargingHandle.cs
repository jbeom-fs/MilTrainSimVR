using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MilTrainSimVR.UrbanCombat
{
    // 장전 손잡이 (10/28) — 총을 든 채 다른 손을 장전 손잡이(총 오른쪽 위)에 대고 그립을 쥔 다음 뒤로 당겼다 놓는다.
    // 손이 총 뒤쪽(-Z)으로 간 만큼 따라가고(최대 m_Stroke), 놓으면 스프링처럼 앞으로 돌아온다.
    // 끝까지 당기면 PulledBack, 그 뒤 앞으로 다 돌아오면 ReturnedForward — 약실에 탄을 넣고 빼는 건 총(TempRifle)이 정한다.
    // A 의 ChargingHandleSlide(Assets/A/Scripts/Rifle) 와 같은 거리·돌아오는 빠르기다. 다만 잡기를 XRI 로 하지 않고 손 자리(HandReach)로 본다
    // — 손잡이에 충돌체를 붙이면 총몸 충돌체와 겹쳐 서로 밀어낸다.
    // 무게감 (10/29, 사용자가 고른 추천안) — 전엔 손이 간 만큼 1:1 로 따라가고 끝에서 진동 한 번뿐이라 가벼운 플라스틱 같았다:
    //   · 당기는 동안 보이는 손 모델을 손잡이에 붙인다 (RifleHands 의 받치는 손과 같은 방식) — 주먹 가운데가 손잡이에 온다
    //   · 처음 m_Breakaway 는 손잡이가 버티다가 툭 풀린다 (공이치기 스프링을 이기는 느낌) — 풀리는 순간 딸깍 진동
    //   · 풀린 뒤엔 끝으로 갈수록 손보다 덜 움직인다 (손 이동의 100% → m_EndRatio). 실제 손과 보이는 손은 끝에서 3cm 안으로 벌어진다
    //   · 당길수록 커지는 약한 떨림, 끝까지 당기면 강하고 짧은 진동 한 번. 끝까지 당긴 뒤로 손을 6cm 넘게 더 빼면 손잡이를 놓친다
    //   · 놓아서 노리쇠가 닫히면 두 손에 진동 + 총이 두 손 사격 반동의 m_CloseKick 만큼 튄다 (덜 당겼다 놓으면 당긴 만큼 약하게)
    //   · 소리(철컥)는 음향 작업 때 넣는다
    // 쏠 때 노리쇠 왕복 (11/17): 한 발마다 총(TempRifle)이 CycleBolt 를 부르면 장전 손잡이가 끝(m_Stroke)까지 빠르게 갔다가 앞으로 닫힌다.
    //   K2 는 장전 손잡이가 노리쇠 뭉치 오른쪽에 붙어 있어 사격 중에 같이 오간다. 보이는 움직임만 — 약실 · 탄창은 총이 이미 정했고,
    //   Offset(손으로 당긴 거리)과 PulledBack · ReturnedForward 는 건드리지 않는다.
    //   모델 안의 가스 활대 · 노리쇠 뭉치(Part_GasRod · 자식 Part_BoltAssembly)는 m_CycleParts 로 1 cm 만 같이 오간다 — 상부 본체 뒤끝까지
    //   1 cm 뿐이라 9 cm 밀면 본체 뒤로 튀어나와 조준하는 눈앞을 가린다 (A 10/27 헤드셋). 오른쪽 홈 안에서 움직이는 게 보이게 하는 모습뿐이다.
    //   손으로 당길 때는 전처럼 장전 손잡이만 움직인다 (사용자는 쏠 때만 말함)
    [RequireComponent(typeof(XRGrabInteractable))]
    public class RifleChargingHandle : MonoBehaviour, IXRSelectFilter
    {
        [Tooltip("움직일 장전 손잡이 — K2C1 의 Part_ChargingHandle")]
        [SerializeField] Transform m_Handle;

        [Tooltip("끝까지 당기는 거리(m) — A 와 같은 값")]
        [SerializeField] float m_Stroke = 0.09f;

        [Tooltip("놓았을 때 앞으로 돌아오는 빠르기(m/s) — 스프링에 밀려 빠르게 닫힌다")]
        [SerializeField] float m_ReturnSpeed = 2.5f;

        [Tooltip("손의 잡기 점이 장전 손잡이에서 이 거리(m) 안이면 잡힌다")]
        [SerializeField] float m_GrabRadius = 0.055f;

        [Tooltip("처음에 손잡이가 버티는 손 이동(m) — 이만큼 당겨야 풀린다")]
        [SerializeField] float m_Breakaway = 0.008f;

        [Tooltip("풀리는 순간 손잡이가 툭 따라붙는 거리(m)")]
        [SerializeField] float m_Pop = 0.004f;

        [Tooltip("끝까지 당겼을 때 손잡이가 손 이동을 따라가는 비율 — 처음엔 1(같이 감), 끝으로 갈수록 이 값으로 줄어든다(스프링이 세진다).\n" +
                 "0.65 면 끝까지 당겼을 때 실제 손이 보이는 손(손잡이)보다 약 2.5cm 더 뒤에 있다")]
        [Range(0.3f, 1f)]
        [SerializeField] float m_EndRatio = 0.65f;

        [Tooltip("노리쇠가 닫힐 때 총이 튀는 크기 — 두 손 사격 반동 한 발 대비 (한 손으로 들고 있어도 같은 크기)")]
        [Range(0f, 1f)]
        [SerializeField] float m_CloseKick = 0.5f;

        [Header("쏠 때 노리쇠 왕복")]
        [Tooltip("한 발 쏠 때 노리쇠(와 장전 손잡이)가 뒤 끝까지 가는 시간(s) — 가스에 밀려 빠르게 간다")]
        [SerializeField] float m_CycleBackTime = 0.025f;

        [Tooltip("뒤 끝에서 스프링에 밀려 앞으로 닫히는 시간(s). 뒤로 + 앞으로가 다음 탄까지의 간격보다 길면 그 안에 끝나게 줄인다")]
        [SerializeField] float m_CycleForwardTime = 0.045f;

        [Tooltip("쏠 때 장전 손잡이와 함께 오가는 안쪽 부품 — K2C1 의 Part_GasRod (노리쇠 뭉치가 자식이라 같이 간다). 비우면 장전 손잡이만")]
        [SerializeField] Transform[] m_CycleParts;

        [Tooltip("안쪽 부품이 뒤로 가는 거리(m) — 장전 손잡이가 끝까지 갈 때 이만큼. 상부 본체 뒤끝까지 여유가 1 cm 라 그 안에서만")]
        [SerializeField] float m_CyclePartStroke = 0.01f;

        const float k_FullSlack = 0.004f;    // 끝까지 당겼다고 볼 여유 (A 와 같은 값)
        const float k_HomeSlack = 0.0005f;   // 다 돌아왔다고 볼 여유
        const float k_CloseFrom = 0.3f;      // 이만큼(행정 대비) 넘게 당겼다 놓아야 닫힐 때 진동·반동이 난다
        const float k_Overpull = 0.06f;      // 끝까지 당긴 뒤로 손을 이만큼(m) 더 빼면 손잡이를 놓친다

        XRGrabInteractable m_Rifle;
        RifleRecoil m_Recoil;
        RifleHands m_Hands;
        XROrigin m_HolderRig;
        NearFarInteractor[] m_HolderHands = new NearFarInteractor[0];
        NearFarInteractor m_Hand;        // 지금 당기고 있는 손
        Transform m_HandPose;            // 당기는 손의 손 모델이 붙는 자리 — 손잡이를 따라 움직인다
        RifleHands.Snap m_Snap;
        Vector3 m_RestLocal;
        Vector3 m_BackLocal;             // 총 뒤쪽 1m 가 손잡이 부모 기준으로 얼마인지
        float m_StartOffset;
        float m_StartHandZ;
        bool m_BrokeAway;                // 처음 버티는 구간을 넘었는지
        float m_MaxOffset;               // 이번에 놓기 전까지 가장 많이 당긴 거리 — 닫힐 때 세기
        bool m_ReachedBack;
        float m_CycleStart = -1f;        // 쏴서 노리쇠가 왕복을 시작한 시각 — 음수면 왕복 중이 아니다
        float m_CycleFrom;               // 왕복을 시작할 때 이미 뒤로 가 있던 거리 (연발로 앞 왕복이 덜 끝났을 때)
        float m_CycleScale = 1f;         // 다음 탄까지의 간격에 맞춘 시간 배율
        float m_CycleOffset;             // 지금 왕복이 뒤로 민 거리 (m)
        Vector3[] m_PartRestLocal;       // 안쪽 부품의 처음 자리
        Vector3[] m_PartBackLocal;       // 총 뒤쪽 1m 가 각 부품의 부모 기준으로 얼마인지

        // 손으로 당긴 거리 — 쏠 때의 왕복은 들어가지 않는다 (총이 '당기고 있으면 격발 안 됨'을 이 값으로 본다)
        public float Offset { get; private set; }
        public float Stroke => m_Stroke;
        public bool IsHeld => m_Hand != null;
        public NearFarInteractor Hand => m_Hand;
        public Vector3 HandlePoint => m_Handle.position;

        public event System.Action PulledBack;
        public event System.Action ReturnedForward;

        bool IXRSelectFilter.canProcess => isActiveAndEnabled;

        void Awake()
        {
            m_Rifle = GetComponent<XRGrabInteractable>();
            m_Recoil = GetComponent<RifleRecoil>();
            m_Hands = GetComponent<RifleHands>();
            m_RestLocal = m_Handle.localPosition;
            m_BackLocal = m_Handle.parent.InverseTransformVector(transform.TransformDirection(Vector3.back));
            int parts = m_CycleParts != null ? m_CycleParts.Length : 0;
            m_PartRestLocal = new Vector3[parts];
            m_PartBackLocal = new Vector3[parts];
            for (int i = 0; i < parts; i++)
            {
                if (m_CycleParts[i] == null)
                    continue;
                m_PartRestLocal[i] = m_CycleParts[i].localPosition;
                m_PartBackLocal[i] = m_CycleParts[i].parent.InverseTransformVector(transform.TransformDirection(Vector3.back));
            }
            m_HandPose = new GameObject("Pose_ChargingHand").transform;
            m_HandPose.SetParent(m_Handle, false);
        }

        void OnEnable()
        {
            m_Rifle.selectFilters.Add(this);
        }

        void OnDisable()
        {
            m_Rifle.selectFilters.Remove(this);
            Release();
            m_ReachedBack = false;
            m_MaxOffset = 0f;
            m_CycleStart = -1f;
            m_CycleOffset = 0f;
            SetOffset(0f);
        }

        void Update()
        {
            UpdateHolder();

            if (m_Hand != null)
            {
                // 그립을 놓았거나, 총을 놓았거나, 그 손이 다른 걸 잡았으면 손을 뗀 것으로 본다
                if (m_HolderRig == null || !m_Hand.selectInput.ReadIsPerformed() || m_Hand.hasSelection)
                    Release();
                else
                    Pull(m_StartHandZ - HandZ(m_Hand));
            }

            if (m_Hand == null)
            {
                if (Offset > 0f)
                {
                    SetOffset(Mathf.Max(0f, Offset - m_ReturnSpeed * Time.deltaTime));
                    if (Offset <= 0f)
                        Closed();
                }
                if (m_HolderRig != null)
                {
                    // 장전 손잡이 자리가 가장 먼저다 — 레일(RifleHands)·탄창(MagazineWell)은 이 자리를 빼고 본다
                    foreach (NearFarInteractor hand in m_HolderHands)
                    {
                        if (!HandReach.IsFreshGrip(hand) || !InGrabZone(hand))
                            continue;
                        Grab(hand);
                        break;
                    }
                }
            }

            if (!m_ReachedBack && Offset >= m_Stroke - k_FullSlack)
            {
                m_ReachedBack = true;
                if (m_Hand != null)
                    m_Hand.SendHapticImpulse(0.8f, 0.05f);
                PulledBack?.Invoke();
            }
            else if (m_ReachedBack && m_Hand == null && Offset <= k_HomeSlack)
            {
                m_ReachedBack = false;
                ReturnedForward?.Invoke();
            }
        }

        // 쏠 때 노리쇠 왕복 — 모든 Update(손으로 당기기, 총의 격발) 뒤에 이번 프레임 자리를 정한다
        void LateUpdate()
        {
            if (m_CycleStart < 0f)
                return;
            float t = Time.time - m_CycleStart;
            float back = m_CycleBackTime * m_CycleScale;
            float forward = m_CycleForwardTime * m_CycleScale;
            if (t < back)
            {
                // 뒤로: 가스에 밀려 튀어나가다 뒤 끝에서 멎는다 (끝으로 갈수록 느리게)
                float u = 1f - t / back;
                m_CycleOffset = Mathf.Lerp(m_CycleFrom, m_Stroke, 1f - u * u);
            }
            else if (t < back + forward)
            {
                // 앞으로: 스프링에 밀려 점점 빨라지다 탁 닫힌다
                float u = (t - back) / forward;
                m_CycleOffset = m_Stroke * (1f - u * u);
            }
            else
            {
                m_CycleOffset = 0f;
                m_CycleStart = -1f;
            }
            ApplyPose();
        }

        // 한 발 쐈다 — 노리쇠와 함께 장전 손잡이가 뒤 끝까지 갔다가 앞으로 닫힌다 (보이는 움직임만).
        // maxDuration = 다음 탄까지의 간격(s) — 왕복이 그 안에 끝나게 줄인다. 앞 왕복이 덜 끝났으면 지금 자리에서 다시 뒤로 간다
        public void CycleBolt(float maxDuration)
        {
            float total = m_CycleBackTime + m_CycleForwardTime;
            m_CycleScale = total > 0f && maxDuration > 0f ? Mathf.Min(1f, maxDuration * 0.9f / total) : 1f;
            m_CycleFrom = m_CycleOffset;
            m_CycleStart = Time.time;
        }

        // 손의 잡기 점이 장전 손잡이 가까이(m_GrabRadius) 있는지 — 레일·탄창 자리보다 먼저 본다
        public bool InGrabZone(NearFarInteractor hand)
        {
            return HandReach.IsNear(hand, HandlePoint, m_GrabRadius);
        }

        void Grab(NearFarInteractor hand)
        {
            m_Hand = hand;
            m_StartOffset = Offset;
            m_StartHandZ = HandZ(hand);
            m_BrokeAway = false;
            if (Offset <= 0f)
                m_MaxOffset = 0f;
            // 보이는 손을 손잡이에 붙인다 — 손 방향은 그대로, 주먹 가운데(잡기 점)가 손잡이에 오게.
            // 손 모델이 이미 다른 데(레일)에 붙어 있으면 건드리지 않는다
            ControllerHandPose visual = HandReach.HandVisual(hand);
            if (visual == null || visual.HandModel == null || visual.HandModel.parent != visual.transform)
                return;
            Transform root = visual.transform;
            m_HandPose.SetPositionAndRotation(root.position + (HandlePoint - HandReach.GrabPoint(hand)), root.rotation);
            m_Snap = RifleHands.Attach(visual, m_HandPose);
            m_Snap.hand.GripOverride = 1f;
            m_Snap.hand.IndexOverride = 0.8f;
            m_Snap.hand.ThumbOverride = 0.8f;
        }

        void Release()
        {
            RifleHands.Detach(ref m_Snap);
            m_Hand = null;
        }

        // 손이 잡은 때부터 총 뒤쪽으로 간 거리(travel) → 손잡이 위치. 처음엔 버티다가 풀리고, 풀린 뒤엔 끝으로 갈수록 덜 따라간다
        void Pull(float travel)
        {
            float effective;
            if (m_StartOffset > 0f)
            {
                // 돌아오는 중에 다시 잡았다 — 버티는 구간 없이 그 자리부터 이어서
                m_BrokeAway = true;
                effective = travel + Travel(m_StartOffset);
            }
            else if (!m_BrokeAway && travel < m_Breakaway)
            {
                SetOffset(0f);
                return;
            }
            else
            {
                if (!m_BrokeAway)
                {
                    m_BrokeAway = true;
                    m_Hand.SendHapticImpulse(0.35f, 0.02f);   // 딸깍 — 스프링을 이겼다
                }
                effective = travel - m_Breakaway + m_Pop;
            }
            // 끝까지 당긴 뒤로도 손을 한참 더 빼면 놓친 것으로 본다 — 손잡이에 붙은 보이는 손이 실제 손과 너무 벌어지지 않게
            if (effective > Travel(m_Stroke) + k_Overpull)
            {
                Release();
                return;
            }
            float before = Offset;
            SetOffset(Mathf.Clamp(Follow(effective), 0f, m_Stroke));
            m_MaxOffset = Mathf.Max(m_MaxOffset, Offset);

            // 움직이는 동안 떨림 — 빨리 당길수록, 많이 당겼을수록 세다
            float speed = Mathf.Abs(Offset - before) / Mathf.Max(Time.deltaTime, 0.0001f);
            float amplitude = Mathf.Clamp01(speed / 0.4f) * Mathf.Lerp(0.08f, 0.3f, Offset / m_Stroke);
            if (amplitude > 0.02f)
                m_Hand.SendHapticImpulse(amplitude, Time.deltaTime * 2f);
        }

        // 풀린 뒤의 손 이동 → 손잡이 위치. 따라가는 비율이 손잡이 위치에 따라 1 에서 m_EndRatio 로 고르게 줄어든다
        // (비율 = 1 − k·위치 → 위치 = (1 − e^(−k·이동)) / k)
        float Follow(float travel)
        {
            float k = (1f - m_EndRatio) / m_Stroke;
            return k > 0f ? (1f - Mathf.Exp(-k * travel)) / k : travel;
        }

        // Follow 의 거꾸로 — 손잡이 위치 → 그만큼 가려면 필요한 손 이동
        float Travel(float offset)
        {
            float k = (1f - m_EndRatio) / m_Stroke;
            return k > 0f ? -Mathf.Log(Mathf.Max(0.0001f, 1f - k * offset)) / k : offset;
        }

        // 노리쇠가 앞으로 닫혔다 — 두 손에 진동, 총이 조금 튄다. 많이 당겼다 놓을수록 세게
        void Closed()
        {
            float strength = m_MaxOffset / m_Stroke;
            m_MaxOffset = 0f;
            if (strength < k_CloseFrom)
                return;
            foreach (NearFarInteractor hand in m_HolderHands)
            {
                if (hand != null && hand.isActiveAndEnabled)
                    hand.SendHapticImpulse(0.7f * strength, 0.06f);
            }
            if (m_Recoil != null)
                m_Recoil.Kick(true, m_CloseKick * strength);   // 사격 반동이 아니라 노리쇠 충격 — 한 손 반동(11/12 부터 훨씬 크다)을 따라가지 않게 두 손 값 기준
        }

        // 손의 잡기 점이 총 앞뒤 어디쯤인지 (총 기준 Z, m) — 이 값이 줄어든 만큼 당긴 것이다
        float HandZ(NearFarInteractor hand)
        {
            return transform.InverseTransformPoint(HandReach.GrabPoint(hand)).z;
        }

        void SetOffset(float offset)
        {
            Offset = offset;
            ApplyPose();
        }

        // 손잡이 자리 = 손으로 당긴 거리와 쏠 때 왕복 중 더 뒤쪽. 안쪽 부품은 쏠 때 왕복만 따라 m_CyclePartStroke 비율로
        void ApplyPose()
        {
            m_Handle.localPosition = m_RestLocal + m_BackLocal * Mathf.Max(Offset, m_CycleOffset);
            if (m_PartRestLocal == null)
                return;
            float part = m_Stroke > 0f ? m_CycleOffset / m_Stroke * m_CyclePartStroke : 0f;
            for (int i = 0; i < m_PartRestLocal.Length; i++)
            {
                if (m_CycleParts[i] != null)
                    m_CycleParts[i].localPosition = m_PartRestLocal[i] + m_PartBackLocal[i] * part;
            }
        }

        // 총을 든 사람(먼저 잡은 손의 XR Origin)과 그 사람의 두 손
        void UpdateHolder()
        {
            XROrigin rig = m_Rifle.interactorsSelecting.Count > 0 ? m_Rifle.interactorsSelecting[0].transform.GetComponentInParent<XROrigin>() : null;
            if (rig == m_HolderRig)
                return;
            m_HolderRig = rig;
            m_HolderHands = rig != null ? rig.GetComponentsInChildren<NearFarInteractor>(true) : new NearFarInteractor[0];
        }

        // 총을 이미 든 사람의 다른 손이 장전 손잡이 자리에서 총을 새로 잡지 못하게 한다 (MagazineWell 과 같은 까닭).
        // 이미 잡고 있는 손(앞손)은 그대로 둔다
        bool IXRSelectFilter.Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            if (interactable.interactorsSelecting.Count == 0 || interactor.IsSelecting(interactable))
                return true;
            if (ReferenceEquals(interactor, m_Hand))
                return false;
            return !(interactor is NearFarInteractor hand && HandReach.IsNear(hand, HandlePoint, m_GrabRadius));
        }
    }
}
