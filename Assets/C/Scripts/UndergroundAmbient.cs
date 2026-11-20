using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Rendering;

namespace MilTrainSimVR.UrbanCombat
{
    // 지하보도를 어둡게 (사용자 11/18: "지하보도 자체를 좀 어둡게 하고 전등을 … 조금만 써서 어둡게 유지해 색적은 조금은 힘들면 좋겠음").
    // 지하는 해가 안 들게 해 두었지만(렌더링 레이어) 하늘빛 환경광은 그대로 받아 어디나 고르게 밝았다. 환경광은 씬 전체에 하나라서
    // 지하만 줄이려면 렌더러마다 따로 줘야 한다 — 'Light Probes = Custom Provided' 로 두고 줄인 환경광을 MaterialPropertyBlock 으로 넣는다.
    //   · 지하 구조물 · 소품(이 오브젝트 아래, 덮인 방의 렌더링 레이어): 켜질 때 한 번. 편집 중에도 같아 보이게 ExecuteAlways
    //   · 움직이는 것(적 · 플레이어 몸과 손 · 총): Play 중 0.2초마다 자리를 보고, 덮인 방(m_Rooms) 안이면 줄인 환경광, 밖이면 원래대로.
    //     방 밖 m_Fade 까지는 서서히 — 계단을 내려오며 어두워진다. 이걸 안 하면 어두운 지하에서 사람만 환해서 오히려 눈에 띈다
    // 전등(스포트라이트)은 UrbanGreyboxBuilder 가 따로 단다. 방 목록과 'Custom Provided' 표시는 지도 생성기가 넣는다
    [ExecuteAlways]
    public class UndergroundAmbient : MonoBehaviour
    {
        [Tooltip("지하의 환경광 — 바깥의 몇 배인지 (0 = 전등 빛만, 1 = 바깥과 같음)")]
        [Range(0f, 1f)]
        [SerializeField] float m_Ambient = 0.12f;

        [Tooltip("덮인 방(홀 · 통로) — 월드 좌표 상자. 이 안에 든 적 · 플레이어 · 총도 줄인 환경광을 받는다")]
        [SerializeField] Bounds[] m_Rooms = new Bounds[0];

        [Tooltip("방 밖 이 거리(m)까지는 서서히 밝아진다 — 계단 아래 끝")]
        [SerializeField] float m_Fade = 2f;

        [Tooltip("이 렌더링 레이어만 가진 렌더러가 지하 구조물 (지도 생성기의 k_UgRenderingLayer)")]
        [SerializeField] uint m_StaticLayer = 1u << 1;

        static readonly int[] k_SHA = { Shader.PropertyToID("unity_SHAr"), Shader.PropertyToID("unity_SHAg"), Shader.PropertyToID("unity_SHAb") };
        static readonly int[] k_SHB = { Shader.PropertyToID("unity_SHBr"), Shader.PropertyToID("unity_SHBg"), Shader.PropertyToID("unity_SHBb") };
        static readonly int k_SHC = Shader.PropertyToID("unity_SHC");

        class Subject
        {
            public Transform root;
            public Renderer[] renderers;
            public LightProbeUsage[] usage;   // 원래 값 — 밖으로 나오면 되돌린다
            public float scale = 1f;
        }

        readonly List<Subject> m_Subjects = new List<Subject>();

        // 이 오브젝트 밖인데 덮인 방 안에 놓인 것 — 바리케이드 · 엄폐물 · 간판 (손으로 놓은 것 포함). Play 를 시작할 때 찾아 같은 환경광을 준다.
        // 안 하면 어두운 지하에서 이것들만 바깥 하늘빛으로 밝아, 그 앞에 선 적이 검은 실루엣으로 드러난다 (11/19 사용자: "색적이 좀 잘되는게 문제").
        // 씬에 저장되는 값(Light Probes)을 건드리므로 편집 중에는 하지 않는다 — 씬 뷰에서는 이것들이 Play 때보다 밝게 보인다
        readonly List<Renderer> m_Placed = new List<Renderer>();

        MaterialPropertyBlock m_Block;
        float m_NextCheck;

        void OnEnable() => ApplyStatic();

        void OnValidate()
        {
            if (isActiveAndEnabled)
                ApplyStatic();
        }

        void Start()
        {
            if (!Application.isPlaying)
                return;
            // 움직이는 것 — 씬에 처음부터 있는 것만 (적은 새로 생기지 않는다)
            foreach (EnemySoldier enemy in FindObjectsByType<EnemySoldier>(FindObjectsSortMode.None))
                Track(enemy.transform);
            foreach (TempRifle rifle in FindObjectsByType<TempRifle>(FindObjectsSortMode.None))
                Track(rifle.transform);
            foreach (XROrigin rig in FindObjectsByType<XROrigin>(FindObjectsSortMode.None))
                Track(rig.transform);

            // 방 안에 놓인 것 — 움직이는 것과 지하 구조물(이 아래)은 뺀다
            var moving = new HashSet<Renderer>();
            foreach (Subject s in m_Subjects)
                moving.UnionWith(s.renderers);
            foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r is ParticleSystemRenderer || moving.Contains(r) || r.transform.IsChildOf(transform) || Outside(r.bounds.center) > 0f)
                    continue;
                r.lightProbeUsage = LightProbeUsage.CustomProvided;
                m_Placed.Add(r);
            }
            ApplyStatic();
        }

        void Track(Transform root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            var usage = new LightProbeUsage[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                usage[i] = renderers[i].lightProbeUsage;
            m_Subjects.Add(new Subject { root = root, renderers = renderers, usage = usage });
        }

        // 지하 구조물 — 이 아래 렌더러 가운데 덮인 방의 레이어인 것에 줄인 환경광을 넣는다
        public void ApplyStatic()
        {
            m_Block ??= new MaterialPropertyBlock();
            SphericalHarmonicsL2 probe = RenderSettings.ambientProbe * m_Ambient;
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                if (r.renderingLayerMask != m_StaticLayer || r.lightProbeUsage != LightProbeUsage.CustomProvided)
                    continue;
                r.GetPropertyBlock(m_Block);
                SetProbe(m_Block, probe);
                r.SetPropertyBlock(m_Block);
            }
            foreach (Renderer r in m_Placed)
            {
                if (r == null)
                    continue;
                r.GetPropertyBlock(m_Block);
                SetProbe(m_Block, probe);
                r.SetPropertyBlock(m_Block);
            }
        }

        void Update()
        {
            if (!Application.isPlaying || Time.time < m_NextCheck)
                return;
            m_NextCheck = Time.time + 0.2f;
            foreach (Subject s in m_Subjects)
            {
                if (s.root == null)
                    continue;
                float scale = Mathf.Lerp(m_Ambient, 1f, Outside(s.root.position + Vector3.up));
                if (Mathf.Abs(scale - s.scale) < 0.02f)
                    continue;
                s.scale = scale;
                bool dim = scale < 0.99f;
                SphericalHarmonicsL2 probe = RenderSettings.ambientProbe * scale;
                for (int i = 0; i < s.renderers.Length; i++)
                {
                    Renderer r = s.renderers[i];
                    if (r == null)
                        continue;
                    r.lightProbeUsage = dim ? LightProbeUsage.CustomProvided : s.usage[i];
                    if (!dim)
                        continue;
                    r.GetPropertyBlock(m_Block);
                    SetProbe(m_Block, probe);
                    r.SetPropertyBlock(m_Block);
                }
            }
        }

        // 그 점이 덮인 방에서 얼마나 밖인가 — 0 = 방 안, 1 = m_Fade 넘게 밖
        float Outside(Vector3 point)
        {
            float nearest = float.MaxValue;
            foreach (Bounds room in m_Rooms)
                nearest = Mathf.Min(nearest, room.SqrDistance(point));
            return m_Fade <= 0f ? (nearest > 0f ? 1f : 0f) : Mathf.Clamp01(Mathf.Sqrt(nearest) / m_Fade);
        }

        // 환경광(구면 조화 2차) → 셰이더 값 일곱 개 (Unity 가 렌더러마다 넣는 것과 같은 식)
        static void SetProbe(MaterialPropertyBlock block, SphericalHarmonicsL2 sh)
        {
            for (int c = 0; c < 3; c++)
            {
                block.SetVector(k_SHA[c], new Vector4(sh[c, 3], sh[c, 1], sh[c, 2], sh[c, 0] - sh[c, 6]));
                block.SetVector(k_SHB[c], new Vector4(sh[c, 4], sh[c, 5], sh[c, 6] * 3f, sh[c, 7]));
            }
            block.SetVector(k_SHC, new Vector4(sh[0, 8], sh[1, 8], sh[2, 8], 1f));
        }
    }
}
