using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

namespace MilTrainSimVR.UrbanCombat
{
    // 플레이어가 맞았을 때 몸으로 느끼게 하는 것 (C 범위 문서 4번, 10/26):
    //   · 이동 속도 — 부상 70%, 중상 30%, 전사 0. 스틱 이동(ContinuousMoveProvider)의 속도를 바꾼다
    //   · 시야 가장자리가 붉게 — 부상은 옅게, 중상은 심장 뛰듯 조였다 풀리며 전사가 가까울수록 좁아지고, 전사는 시야 전체가 검붉게
    //   · 맞는 순간 두 손 진동 + 시야가 한 번 확 좁아짐
    // 붉은 가장자리는 XRI 멀미 비네트와 같은 반구 메시·셰이더(VR/TunnelingVignette)를 빌려 따로 하나 더 띄운다 —
    // 그 셰이더는 두 눈을 따로 맞게 그리고 깊이 검사를 꺼서 벽에 바짝 붙어도 가려지지 않는다.
    // (처음엔 카메라 앞에 판을 붙였는데 두 눈이 판을 다르게 보고, URP 기본 셰이더로는 벽에 가려졌다)
    [RequireComponent(typeof(XROrigin), typeof(CombatantHealth))]
    public class PlayerWoundEffects : MonoBehaviour
    {
        [Tooltip("붉은 비네트 재질 (C_WoundVignette — 셰이더 VR/TunnelingVignette). 비우면 시야 효과 없이 속도·진동만")]
        [SerializeField] Material m_VignetteMaterial;

        [Tooltip("비네트 반구 메시 (XRI Starter Assets 의 TunnelingVignetteHemisphere)")]
        [SerializeField] Mesh m_VignetteMesh;

        [SerializeField] Color m_WoundColor = new Color(0.75f, 0.02f, 0.02f);

        [Header("시야 — 앞에서 몇 도부터 붉어져서 몇 도에서 가장 진한지 (헤드셋 시야 끝 약 50°)")]
        [SerializeField] Vector2 m_WoundedAngles = new Vector2(35f, 58f);
        [SerializeField] float m_WoundedAlpha = 0.45f;
        [Tooltip("중상 — 막 맞았을 때 → 전사 직전")]
        [SerializeField] Vector2 m_SevereAnglesStart = new Vector2(27f, 50f);
        [SerializeField] Vector2 m_SevereAnglesEnd = new Vector2(14f, 38f);
        [SerializeField] float m_SevereAlpha = 0.8f;

        [Tooltip("맞았을 때 두 손 진동 세기(0~1)·길이(초)")]
        [SerializeField] float m_HitHapticAmplitude = 0.9f;
        [SerializeField] float m_HitHapticSeconds = 0.25f;

        static readonly int k_Aperture = Shader.PropertyToID("_ApertureSize");
        static readonly int k_Feather = Shader.PropertyToID("_FeatheringEffect");
        static readonly int k_Color = Shader.PropertyToID("_VignetteColor");
        static readonly int k_ColorBlend = Shader.PropertyToID("_VignetteColorBlend");

        CombatantHealth m_Health;
        ContinuousMoveProvider m_Move;
        float m_BaseSpeed;
        HapticImpulsePlayer[] m_Haptics;
        MeshRenderer m_Vignette;
        Material m_VignetteInstance;
        float m_Flash;
        float m_StartAngle;
        float m_Alpha;

        void Awake()
        {
            m_Health = GetComponent<CombatantHealth>();
            m_Move = GetComponentInChildren<ContinuousMoveProvider>(true);
            if (m_Move != null)
                m_BaseSpeed = m_Move.moveSpeed;
            m_Haptics = GetComponentsInChildren<HapticImpulsePlayer>(true);
            m_Health.StateChanged += OnStateChanged;
            m_Health.Hit += OnHit;
        }

        void Start()
        {
            Camera cam = GetComponent<XROrigin>().Camera;
            if (m_VignetteMaterial == null || m_VignetteMesh == null || cam == null)
                return;
            // 카메라 자식 — 고개와 같이 움직인다. 반구가 카메라 앞을 덮는다(멀미 비네트와 같은 자리·크기)
            var go = new GameObject("C_WoundVignette");
            go.transform.SetParent(cam.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = m_VignetteMesh;
            m_Vignette = go.AddComponent<MeshRenderer>();
            m_Vignette.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m_Vignette.receiveShadows = false;
            m_VignetteInstance = new Material(m_VignetteMaterial);
            m_Vignette.sharedMaterial = m_VignetteInstance;
            m_Vignette.enabled = false;
        }

        void OnDestroy()
        {
            if (m_Health != null)
            {
                m_Health.StateChanged -= OnStateChanged;
                m_Health.Hit -= OnHit;
            }
            if (m_VignetteInstance != null)
                Destroy(m_VignetteInstance);
        }

        void OnStateChanged(CombatantHealth who, CombatantHealth.State state, HitZone.Part? part)
        {
            if (m_Move != null)
                m_Move.moveSpeed = m_BaseSpeed * m_Health.SpeedMultiplier;
        }

        void OnHit(CombatantHealth who, HitZone.Part part, Vector3 point)
        {
            m_Flash = 1f;
            foreach (HapticImpulsePlayer haptics in m_Haptics)
                haptics.SendHapticImpulse(m_HitHapticAmplitude, m_HitHapticSeconds);
        }

        void Update()
        {
            if (m_Vignette == null)
                return;
            Vector2 angles;
            Color color = m_WoundColor;
            switch (m_Health.Current)
            {
                case CombatantHealth.State.Wounded:
                    angles = m_WoundedAngles;
                    color.a = m_WoundedAlpha;
                    break;
                case CombatantHealth.State.Severe:
                {
                    // 전사가 가까울수록 좁아지고, 1초에 1.3번 심장 뛰듯 3° 씩 조였다 풀린다
                    float spent = m_Health.BleedOutSeconds > 0f ? 1f - m_Health.BleedOutRemaining / m_Health.BleedOutSeconds : 1f;
                    angles = Vector2.Lerp(m_SevereAnglesStart, m_SevereAnglesEnd, spent);
                    angles -= Vector2.one * (3f * (0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f * 1.3f)));
                    color.a = m_SevereAlpha;
                    break;
                }
                case CombatantHealth.State.Dead:
                    // 한가운데까지 다 덮는다 — 끝 각도를 1° 로 두면 가운데 1° 가 점처럼 비어 보였다.
                    // 결과판을 볼 땐 가운데를 비우고 가장자리만 검붉게 (MatchDisplay 가 켠다)
                    angles = ResultsShown ? new Vector2(22f, 48f) : new Vector2(0f, 0.1f);
                    color = Color.Lerp(m_WoundColor, Color.black, 0.65f);
                    color.a = ResultsShown ? 0.85f : 0.93f;
                    break;
                default:
                    angles = new Vector2(90f, 90f);
                    color.a = 0f;
                    break;
            }
            // 맞은 순간 — 0.35초 동안 시야가 15° 더 좁고 진하다
            m_Flash = Mathf.Max(0f, m_Flash - Time.deltaTime / 0.35f);
            if (m_Flash > 0f)
            {
                if (color.a <= 0f)
                {
                    angles = m_WoundedAngles;
                    color.a = 0f;
                }
                angles -= Vector2.one * (15f * m_Flash);
                color.a = Mathf.Max(color.a, 0.75f * m_Flash);
            }

            m_StartAngle = Mathf.Max(0f, angles.x);
            m_Alpha = color.a;
            m_Vignette.enabled = color.a > 0.01f;
            if (!m_Vignette.enabled)
                return;
            // 셰이더는 앞에서 벌어진 각도를 반구 UV 0(앞)~0.5(옆 90°)로 본다:
            //   붉어지기 시작하는 UV = 0.5 - √(0.25 - 조리개²/4) → 조리개 = √(1 - (1 - 2·시작UV)²)
            //   가장 진해지는 UV = 시작UV + 번짐²  → 번짐 = √(끝UV - 시작UV)
            float s = Mathf.Clamp01(m_StartAngle / 180f);
            float e = Mathf.Max(s + 0.0005f, Mathf.Max(0f, angles.y) / 180f);
            m_VignetteInstance.SetFloat(k_Aperture, Mathf.Sqrt(Mathf.Max(0f, 1f - (1f - 2f * s) * (1f - 2f * s))));
            m_VignetteInstance.SetFloat(k_Feather, Mathf.Sqrt(e - s));
            m_VignetteInstance.SetColor(k_Color, color);
            m_VignetteInstance.SetColor(k_ColorBlend, color);
        }

        // 경기가 끝나 결과판을 보는 중 — 전사했어도 시야 가운데를 비운다 (MatchDisplay)
        public bool ResultsShown { get; set; }

        // 시험용 — 지금 붉어지기 시작하는 각도와 진하기 (안 보이면 진하기 0)
        public float VignetteStartAngle => m_StartAngle;
        public float VignetteAlpha => m_Vignette != null && m_Vignette.enabled ? m_Alpha : 0f;
    }
}
