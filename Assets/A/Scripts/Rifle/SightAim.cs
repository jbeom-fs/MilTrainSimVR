using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MilTrainSimVR.A.Rifle
{
    public enum BreathState { Normal, Holding, Exhausted }   // 보통 · 숨 참기 · 숨 한계

    // 조준 확대 모드 (Docs/A/1028_조준확대모드_설계.md).
    //   · 들어가기·나오기: 총을 든 채 눈(머리 카메라)을 가늠자 뒤로 가져가면 자동으로. 경계에서 깜박이지 않게 기준을 둘로 둔다
    //   · 화면: 가늠자(점 1)에 둔 카메라가 조준선 방향을 좁은 시야로 찍고, 눈에서 조준선 방향 m_WindowDistance 앞의 창에 띄운다.
    //     창 위에 가늠자·가늠쇠 그림(바깥은 어둡게)을 겹친다. 둘 다 깊이 검사 없이 맨 위에 그린다 (AimOverlay 셰이더)
    //   · 조준선: 손 방향을 부드럽게 따라가고 숨 상태에 따른 흔들림을 더한다. 화면과 판정이 같은 선을 쓴다 — 가늠쇠 끝 = 탄이 가는 곳
    //   · 판정(IShotLine): 확대 모드일 때만 조준선 + 방아쇠 흔들림으로 직선을 준다. 아니면 총은 원래대로 총구 방향
    //   · 숨 참기: 앞손 트리거를 누르고 있는 동안. 한계를 넘으면 흔들림이 커지고 쉬어야 회복
    //     앞손 = 레일을 받친 손(IRifleSupport — 두 손 파지, 11/17). 두 손 파지가 없는 총이면 전처럼 두 번째로 잡은 손(XRI Activate)
    // 값은 모두 임시 — 헤드셋으로 조정한다. 그림(가늠쇠 크기)은 창 크기·배율 기준으로 만들어져 있다 (Editor/AimOverlayBuilder).
    [RequireComponent(typeof(XRGrabInteractable))]
    public class SightAim : MonoBehaviour, IShotLine
    {
        [Header("조준선")]
        [Tooltip("점 1 — 가늠자 구멍 가운데 (Anchor_RearSight)")]
        [SerializeField] Transform m_RearSight;

        [Tooltip("점 2 — 가늠쇠 끝 (Anchor_FrontSight)")]
        [SerializeField] Transform m_FrontSight;

        [Tooltip("머리 — 비우면 Main Camera")]
        [SerializeField] Transform m_Head;

        [Header("들어가기 · 나오기")]
        [SerializeField] float m_EnterDistance = 0.25f;
        [SerializeField] float m_ExitDistance = 0.32f;
        [Tooltip("조준선과 '눈 → 가늠쇠' 방향의 각도(°)")]
        [SerializeField] float m_EnterAngle = 10f;
        [SerializeField] float m_ExitAngle = 15f;

        [Header("화면 — 그림(AimOverlayBuilder)과 같은 값이어야 한다")]
        [Tooltip("눈에서 창까지 (m)")]
        [SerializeField] float m_WindowDistance = 0.6f;
        [Tooltip("창이 보이는 크기 (°)")]
        [SerializeField] float m_WindowDegrees = 30f;
        [SerializeField] float m_Magnification = 4f;
        [Tooltip("가늠자 그림(어두운 바깥 포함) 크기 = 창 크기 × 이 값")]
        [SerializeField] float m_OverlayScale = 2.4f;
        [SerializeField] Material m_OverlayMaterial;
        [SerializeField] int m_TextureSize = 1024;
        [Tooltip("확대 카메라 가까운 면 (m) — 총·손이 찍히지 않게")]
        [SerializeField] float m_CameraNear = 1f;

        [Header("흔들림 (25 m 기준 1 mrad = 2.5 cm)")]
        [SerializeField] float m_SwayNormal = 2f;
        [SerializeField] float m_SwayHolding = 0.15f;
        [SerializeField] float m_SwayExhausted = 4f;
        [Tooltip("흔들림 빠르기 (Hz)")]
        [SerializeField] float m_SwayFrequency = 0.35f;
        [Tooltip("손을 따라가는 시간 상수 (초) — 보통")]
        [SerializeField] float m_FollowNormal = 0.06f;
        [Tooltip("손을 따라가는 시간 상수 (초) — 숨 참기 (거의 멈춘 듯)")]
        [SerializeField] float m_FollowHolding = 0.4f;
        [Tooltip("방아쇠 흔들림 — 쏘는 순간 무작위 (mrad, 표준편차)")]
        [SerializeField] float m_TriggerJerk = 0.6f;

        [Header("숨 참기")]
        [SerializeField] float m_HoldSeconds = 6f;
        [Tooltip("숨 한계 뒤 트리거를 놓고 이만큼 쉬어야 다시 참을 수 있다 (초)")]
        [SerializeField] float m_RecoverSeconds = 2.5f;

        [Header("숨 → 반동 (11/17 총기 통합 — C RifleBreath 와 같은 값). RifleRecoil 이 읽는다")]
        [Tooltip("숨 참기 — 한 발 반동 배율")]
        [SerializeField] float m_RecoilHolding = 0.6f;
        [Tooltip("숨 참기 — 제자리로 돌아오는 빠르기 배율")]
        [SerializeField] float m_RecoveryHolding = 1.5f;
        [Tooltip("숨 한계 — 한 발 반동 배율")]
        [SerializeField] float m_RecoilExhausted = 1.3f;
        [Tooltip("숨 한계 — 제자리로 돌아오는 빠르기 배율")]
        [SerializeField] float m_RecoveryExhausted = 0.8f;

        XRGrabInteractable m_Grab;
        Camera m_Camera;
        RenderTexture m_Texture;
        Transform m_Lens;
        Transform m_Overlay;
        Transform m_BreathBar;
        Material m_BarMaterial;

        bool m_Aiming;
        Quaternion m_Smoothed;
        Quaternion m_AimRotation;
        bool m_HoldInput;
        IRifleSupport m_Support;   // 두 손 파지 — 있으면 받친 손의 트리거가 숨 참기
        float m_HoldTime;
        float m_RestTime;
        float m_NoiseTime;
        float m_SightRadius;

        public bool IsAiming => m_Aiming;
        public BreathState Breath { get; private set; }

        // 숨 상태에 따른 반동 배율 — 참는 동안 덜 튀고 빨리 돌아오며, 숨 한계 땐 더 튀고 늦게 돌아온다 (C RifleBreath 와 같은 뜻 · 값)
        public float RecoilScale => Breath switch
        {
            BreathState.Holding => m_RecoilHolding,
            BreathState.Exhausted => m_RecoilExhausted,
            _ => 1f,
        };

        public float RecoveryScale => Breath switch
        {
            BreathState.Holding => m_RecoveryHolding,
            BreathState.Exhausted => m_RecoveryExhausted,
            _ => 1f,
        };
        // 숨을 더 참을 수 있는 비율 (1 = 가득)
        public float BreathLeft => Mathf.Clamp01(1f - m_HoldTime / m_HoldSeconds);
        public Vector3 AimDirection => m_AimRotation * Vector3.forward;

        public event System.Action<bool> AimChanged;
        public event System.Action<BreathState> BreathChanged;

        // 시험용 (에디터) — 손·머리 없이
        public bool TestHeld { get; set; }
        public Transform TestEye { get; set; }
        public bool TestBreath { get; set; }
        public bool TestNoDispersion { get; set; }   // 흔들림·방아쇠 흔들림 끔 — 조준선 판정만 확인

        void Awake()
        {
            m_Grab = GetComponent<XRGrabInteractable>();
            m_Support = GetComponent<IRifleSupport>();
            m_SightRadius = Vector3.Distance(m_RearSight.position, m_FrontSight.position);
            BuildView();
            SetViewVisible(false);
            m_NoiseTime = Random.value * 100f;
        }

        void OnEnable()
        {
            m_Grab.activated.AddListener(OnActivated);
            m_Grab.deactivated.AddListener(OnDeactivated);
            Application.onBeforeRender += PlaceView;
        }

        void OnDisable()
        {
            m_Grab.activated.RemoveListener(OnActivated);
            m_Grab.deactivated.RemoveListener(OnDeactivated);
            Application.onBeforeRender -= PlaceView;
            m_HoldInput = false;
            SetAiming(false);
        }

        void OnDestroy()
        {
            if (m_Texture != null)
                m_Texture.Release();
        }

        // 앞손 트리거 = 숨 참기. 손잡이 쪽(먼저 잡은 손) 트리거는 격발이라 K2C1Rifle 이 받는다
        void OnActivated(ActivateEventArgs args)
        {
            if (!IsGripHand(args.interactorObject))
                m_HoldInput = true;
        }

        void OnDeactivated(DeactivateEventArgs args)
        {
            if (!IsGripHand(args.interactorObject))
                m_HoldInput = false;
        }

        bool IsGripHand(object interactor) =>
            m_Grab.interactorsSelecting.Count > 0 && ReferenceEquals(interactor, m_Grab.interactorsSelecting[0]);

        Transform Eye => TestEye != null ? TestEye : m_Head != null ? m_Head : Camera.main != null ? Camera.main.transform : null;

        Quaternion RawRotation => Quaternion.LookRotation(m_FrontSight.position - m_RearSight.position, transform.up);

        void Update()
        {
            float dt = Time.deltaTime;
            if (m_Support != null)
            {
                var support = m_Support.SupportHand;
                m_HoldInput = support != null && support.activateInput.ReadIsPerformed();
            }
            UpdateAiming();
            UpdateBreath(dt);
            if (!m_Aiming)
                return;

            // 손 방향을 부드럽게 따라간다 — 숨 참기 중엔 아주 천천히 (멈춘 듯, 천천히 고칠 수는 있다)
            float tau = Breath == BreathState.Holding ? m_FollowHolding : m_FollowNormal;
            m_Smoothed = Quaternion.Slerp(m_Smoothed, RawRotation, 1f - Mathf.Exp(-dt / Mathf.Max(tau, 1e-4f)));

            // 숨 상태에 따른 흔들림 (천천히 떠도는 잡음, mrad)
            float amp = TestNoDispersion ? 0f : Breath switch
            {
                BreathState.Holding => m_SwayHolding,
                BreathState.Exhausted => m_SwayExhausted,
                _ => m_SwayNormal,
            };
            m_NoiseTime += dt * m_SwayFrequency;
            float yaw = (Mathf.PerlinNoise(m_NoiseTime, 0.3f) - 0.5f) * 2f * amp;
            float pitch = (Mathf.PerlinNoise(0.7f, m_NoiseTime) - 0.5f) * 2f * amp;
            m_AimRotation = m_Smoothed * Quaternion.Euler(pitch * Mathf.Rad2Deg * 0.001f, yaw * Mathf.Rad2Deg * 0.001f, 0f);
        }

        void UpdateAiming()
        {
            Transform eye = Eye;
            bool held = m_Grab.isSelected || TestHeld;
            if (!held || eye == null)
            {
                SetAiming(false);
                return;
            }
            float distance = Vector3.Distance(eye.position, m_RearSight.position);
            Vector3 lineDir = (m_FrontSight.position - m_RearSight.position).normalized;
            float angle = Vector3.Angle(lineDir, m_FrontSight.position - eye.position);
            if (!m_Aiming && distance <= m_EnterDistance && angle <= m_EnterAngle)
                SetAiming(true);
            else if (m_Aiming && (distance > m_ExitDistance || angle > m_ExitAngle))
                SetAiming(false);
        }

        void UpdateBreath(float dt)
        {
            bool holding = (m_HoldInput || TestBreath) && m_Aiming;
            BreathState next = Breath;
            switch (Breath)
            {
                case BreathState.Normal:
                    m_HoldTime = Mathf.Max(0f, m_HoldTime - dt * (m_HoldSeconds / m_RecoverSeconds));
                    if (holding && m_HoldTime < m_HoldSeconds)
                        next = BreathState.Holding;
                    break;
                case BreathState.Holding:
                    m_HoldTime += dt;
                    if (!holding)
                        next = BreathState.Normal;
                    else if (m_HoldTime >= m_HoldSeconds)
                        next = BreathState.Exhausted;
                    break;
                case BreathState.Exhausted:
                    // 트리거를 놓고 쉬어야 회복
                    m_RestTime = holding ? 0f : m_RestTime + dt;
                    if (m_RestTime >= m_RecoverSeconds)
                    {
                        m_HoldTime = 0f;
                        next = BreathState.Normal;
                    }
                    break;
            }
            if (next == Breath)
                return;
            if (next == BreathState.Exhausted)
                m_RestTime = 0f;
            Breath = next;
            Debug.Log($"[Aim] 숨: {Breath}");
            BreathChanged?.Invoke(Breath);
        }

        void SetAiming(bool aiming)
        {
            if (m_Aiming == aiming)
                return;
            m_Aiming = aiming;
            if (aiming)
                m_Smoothed = m_AimRotation = RawRotation;
            SetViewVisible(aiming);
            Debug.Log($"[Aim] 확대 모드 {(aiming ? "켜짐" : "꺼짐")}");
            AimChanged?.Invoke(aiming);
        }

        // --- 판정 ---

        public bool TryGetShotLine(out Vector3 origin, out Vector3 direction)
        {
            origin = default;
            direction = default;
            if (!m_Aiming)
                return false;
            Quaternion rotation = m_AimRotation;
            if (!TestNoDispersion && m_TriggerJerk > 0f)
                rotation *= Quaternion.Euler(Gaussian() * m_TriggerJerk * Mathf.Rad2Deg * 0.001f, Gaussian() * m_TriggerJerk * Mathf.Rad2Deg * 0.001f, 0f);
            direction = rotation * Vector3.forward;
            // 가늠쇠 자리에서 시작 — 같은 직선이고, 총 자신의 가늠자에 걸리지 않는다
            origin = m_RearSight.position + direction * m_SightRadius;
            return true;
        }

        static float Gaussian()
        {
            float u1 = 1f - Random.value, u2 = Random.value;
            return Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
        }

        // --- 화면 ---

        void BuildView()
        {
            m_Texture = new RenderTexture(m_TextureSize, m_TextureSize, 24) { name = "AimView" };
            var cameraGo = new GameObject("AimCamera");
            cameraGo.transform.SetParent(transform, false);
            m_Camera = cameraGo.AddComponent<Camera>();
            m_Camera.targetTexture = m_Texture;
            // 텍스처에 한 장 — URP 에선 targetTexture 카메라가 그대로 한 장이라 stereoTargetEye 는 효과 없이 경고만 남겨 뺐다 (C 부탁 11/17)
            m_Camera.nearClipPlane = m_CameraNear;
            m_Camera.farClipPlane = 200f;
            m_Camera.fieldOfView = m_WindowDegrees / m_Magnification;

            Shader shader = m_OverlayMaterial != null ? m_OverlayMaterial.shader : Shader.Find("MilTrainSimVR/A/AimOverlay");
            var lensMaterial = new Material(shader) { name = "AimLens", renderQueue = (int)RenderQueue.Overlay };
            lensMaterial.SetTexture("_MainTex", m_Texture);
            m_Lens = MakeQuad("AimLens", lensMaterial);
            if (m_OverlayMaterial != null)
            {
                var overlay = new Material(m_OverlayMaterial) { renderQueue = (int)RenderQueue.Overlay + 1 };
                m_Overlay = MakeQuad("AimSightPicture", overlay);
            }
            m_BarMaterial = new Material(shader) { name = "AimBreathBar", renderQueue = (int)RenderQueue.Overlay + 2 };
            m_BarMaterial.SetTexture("_MainTex", Texture2D.whiteTexture);
            m_BreathBar = MakeQuad("AimBreathBar", m_BarMaterial);
        }

        Transform MakeQuad(string name, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            DestroyImmediate(go.GetComponent<Collider>());   // 총의 잡기 충돌체 목록에 끼지 않게 바로 없앤다
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        void SetViewVisible(bool visible)
        {
            if (m_Camera != null) m_Camera.enabled = visible;
            if (m_Lens != null) m_Lens.gameObject.SetActive(visible);
            if (m_Overlay != null) m_Overlay.gameObject.SetActive(visible);
            if (m_BreathBar != null) m_BreathBar.gameObject.SetActive(visible);
        }

        // 그리기 직전 — 머리 위치가 이번 프레임 값으로 바뀐 뒤라 창이 머리에 한 프레임 늦지 않는다
        void PlaceView()
        {
            if (!m_Aiming || Eye == null)
                return;
            Quaternion rotation = m_AimRotation;
            Vector3 forward = rotation * Vector3.forward;
            m_Camera.transform.SetPositionAndRotation(m_RearSight.position, rotation);
            m_Camera.fieldOfView = m_WindowDegrees / m_Magnification;

            // 창: 눈에서 조준선 방향으로 m_WindowDistance 앞 — 세상에 고정된 조준선을 따르고 머리를 따라 돌지 않는다
            float size = 2f * m_WindowDistance * Mathf.Tan(m_WindowDegrees * 0.5f * Mathf.Deg2Rad);
            Vector3 center = Eye.position + forward * m_WindowDistance;
            Place(m_Lens, center, rotation, size);
            if (m_Overlay != null)
                Place(m_Overlay, center, rotation, size * m_OverlayScale);

            // 숨 막대 — 창 아래, 남은 숨만큼. 숨 참기 중 흰색, 한계면 빨강, 보통이면 흐리게
            float bar = size * 0.5f * BreathLeft;
            Vector3 up = rotation * Vector3.up;
            m_BreathBar.SetPositionAndRotation(center - up * size * 0.58f, rotation);
            m_BreathBar.localScale = new Vector3(Mathf.Max(bar, 0.001f), size * 0.012f, 1f);
            m_BarMaterial.SetColor("_Color", Breath switch
            {
                BreathState.Holding => new Color(1f, 1f, 1f, 0.9f),
                BreathState.Exhausted => new Color(1f, 0.3f, 0.3f, 0.9f),
                _ => new Color(1f, 1f, 1f, 0.35f),
            });
        }

        static void Place(Transform quad, Vector3 position, Quaternion rotation, float size)
        {
            quad.SetPositionAndRotation(position, rotation);
            quad.localScale = new Vector3(size, size, 1f);
        }
    }
}
