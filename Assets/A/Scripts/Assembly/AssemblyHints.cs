using System.Collections.Generic;
using UnityEngine;

namespace MilTrainSimVR.A.Assembly
{
    // 학습 모드 힌트 — 지금 단계의 부품을 반투명으로 강조하고, 움직일 방향 화살표와 (결합 때) 끼울 자리를 보여 준다.
    // 흐름의 구간·모드와 순서의 단계 변경을 듣기만 한다 — 판정에는 끼어들지 않는다.
    // 강조는 부품 Renderer 에 반투명 재질을 하나 더 얹는 방식이다 (같은 메시를 한 번 더 그린다). 외곽선 셰이더는 쓰지 않는다 (10/27 결정).
    public class AssemblyHints : MonoBehaviour
    {
        [SerializeField] AssemblyTrainingFlow m_Flow;
        [SerializeField] AssemblySequence m_Sequence;

        [Header("재질 (A/만들기/힌트 재질)")]
        [SerializeField] Material m_HighlightMaterial;
        [SerializeField] Material m_GhostMaterial;
        [SerializeField] Material m_ArrowMaterial;

        [Header("모양")]
        [Tooltip("강조가 깜박이는 빠르기")]
        [SerializeField] float m_PulseSpeed = 3f;

        [Tooltip("강조 투명도 (최소, 최대)")]
        [SerializeField] Vector2 m_PulseAlpha = new Vector2(0.15f, 0.45f);

        [Tooltip("부품 끝에서 화살표까지 거리(m)")]
        [SerializeField] float m_ArrowGap = 0.02f;

        [Tooltip("화살표가 앞뒤로 오가는 거리(m)")]
        [SerializeField] float m_ArrowTravel = 0.02f;

        Material m_Highlight;   // 실행 중 복사본 — 깜박임으로 색을 바꿔도 에셋은 그대로
        Color m_HighlightColor;
        readonly Dictionary<Renderer, Material[]> m_Originals = new Dictionary<Renderer, Material[]>();
        readonly List<Renderer> m_Targets = new List<Renderer>();
        readonly List<Renderer> m_GhostRenderers = new List<Renderer>();
        Transform m_Arrow;
        Transform m_Ghost;
        AssemblyStep m_Step;
        Bounds m_HingeLocal;   // 열기·닫기 단계: 상부 본체 메시를 본체 좌표계에서 감싸는 상자

        void Awake()
        {
            m_Highlight = new Material(m_HighlightMaterial) { name = "Hint_Highlight (실행 중)" };
            m_HighlightColor = m_Highlight.color;
            m_Arrow = CreateArrow(m_ArrowMaterial).transform;
            m_Arrow.SetParent(transform, false);
            m_Arrow.gameObject.SetActive(false);
            m_Ghost = new GameObject("Hint_Ghost").transform;
            m_Ghost.SetParent(transform, false);
            m_Ghost.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            m_Flow.StageChanged += OnStageChanged;
            m_Sequence.StepChanged += OnStepChanged;
        }

        void OnDisable()
        {
            m_Flow.StageChanged -= OnStageChanged;
            m_Sequence.StepChanged -= OnStepChanged;
            Clear();
        }

        void OnDestroy()
        {
            if (m_Highlight != null)
                Destroy(m_Highlight);
        }

        void OnStageChanged(TrainingStage stage) => ShowCurrent();

        void OnStepChanged(int index, int count) => ShowCurrent();

        void ShowCurrent()
        {
            Clear();
            if (m_Flow.Mode != TrainingMode.Learning || m_Flow.Stage != TrainingStage.Assembly)
                return;
            var step = m_Sequence.Current;
            if (step == null)
                return;

            m_Step = step;
            Component owner = step.hinge != null ? step.hinge : step.part;
            CollectOwnRenderers(owner, m_Targets);
            foreach (var r in m_Targets)
            {
                var original = r.sharedMaterials;
                m_Originals[r] = original;
                var withHint = new Material[original.Length + 1];
                original.CopyTo(withHint, 0);
                withHint[original.Length] = m_Highlight;   // 마지막 재질은 마지막 서브메시를 한 번 더 그린다 — 부품은 서브메시 1개
                r.sharedMaterials = withHint;
            }
            if (step.kind == StepKind.Insert)
                BuildGhost(step.part);
            if (step.hinge != null)
                m_HingeLocal = LocalBounds(m_Targets, step.hinge.transform);
            m_Arrow.gameObject.SetActive(true);
        }

        void Clear()
        {
            foreach (var pair in m_Originals)
            {
                if (pair.Key != null)
                    pair.Key.sharedMaterials = pair.Value;
            }
            m_Originals.Clear();
            m_Targets.Clear();
            m_Step = null;
            if (m_Arrow != null)
                m_Arrow.gameObject.SetActive(false);
            if (m_Ghost != null)
            {
                for (int i = m_Ghost.childCount - 1; i >= 0; i--)
                    Destroy(m_Ghost.GetChild(i).gameObject);
                m_Ghost.gameObject.SetActive(false);
            }
            m_GhostRenderers.Clear();
        }

        void LateUpdate()
        {
            if (m_Step == null)
                return;

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * m_PulseSpeed);
            var c = m_HighlightColor;
            c.a = Mathf.Lerp(m_PulseAlpha.x, m_PulseAlpha.y, pulse);
            m_Highlight.color = c;

            // 끼울 자리: 부품이 아직 손에 들려 있거나 매트 위에 있을 때만 (축에 물리면 감춘다)
            bool showGhost = m_Step.kind == StepKind.Insert && m_Step.part.State == PartState.Free;
            m_Ghost.gameObject.SetActive(showGhost);
            if (showGhost)
            {
                Pose entry = m_Step.part.EntryPose;
                m_Ghost.SetPositionAndRotation(entry.position, entry.rotation);
            }

            PlaceArrow(showGhost);
        }

        // 화살표 — 빼기: 부품 앞쪽에서 빠지는 방향, 끼우기: 부품(또는 끼울 자리) 뒤쪽에서 미는 방향, 열기·닫기: 도는 방향
        void PlaceArrow(bool atGhost)
        {
            if (m_Step.hinge != null)
            {
                PlaceHingeArrow();
                return;
            }
            var bounds = Bounds(atGhost ? m_GhostRenderers : m_Targets);
            Vector3 dir;
            bool leading;
            switch (m_Step.kind)
            {
                case StepKind.Remove:
                case StepKind.PullToDetent:
                    dir = m_Step.part.SlideAxisWorld;
                    leading = true;
                    break;
                default:   // Insert
                    dir = -m_Step.part.SlideAxisWorld;
                    leading = false;
                    break;
            }
            float extent = ExtentAlong(bounds, dir);
            float wave = Wave();
            // 빼기는 앞쪽에서 밖으로, 끼우기는 뒤쪽(화살표 길이만큼 더 뒤)에서 안으로
            Vector3 position = leading
                ? bounds.center + dir * (extent + m_ArrowGap + wave)
                : bounds.center - dir * (extent + m_ArrowGap + k_ArrowLength) + dir * wave;
            m_Arrow.SetPositionAndRotation(position, Quaternion.LookRotation(dir));
        }

        // 열기·닫기: 상부 본체 뒤쪽 끝 가까이(손으로 잡고 여는 곳) 바깥에, 그 점이 도는 방향으로.
        // 열린 본체는 기울어져 있어 월드 상자로 재면 어긋난다 → 같이 도는 본체 자신의 좌표계(m_HingeLocal)로 잰다. 뒤 = 본체 -Z
        void PlaceHingeArrow()
        {
            var hinge = m_Step.hinge;
            Transform t = hinge.transform;
            Vector3 c = m_HingeLocal.center;
            Vector3 anchor = t.TransformPoint(new Vector3(c.x, c.y, m_HingeLocal.min.z + k_HingeRearInset * m_HingeLocal.size.z));
            Vector3 dir = hinge.OpenDirectionAt(anchor);
            Vector3 local = t.InverseTransformDirection(dir);
            float extent = Mathf.Abs(local.x) * m_HingeLocal.extents.x + Mathf.Abs(local.y) * m_HingeLocal.extents.y;
            // 열기: 본체 바깥에서 열리는 쪽으로.
            // 닫기: 뒤쪽 끝 바로 위(월드 위)에 띄워 닫히는 쪽으로 — 닫히는 쪽 앞은 하부 본체라 묻히고, 바깥쪽은 사용자 쪽에서 본체에 가린다
            Vector3 position;
            if (m_Step.kind == StepKind.Close)
            {
                dir = -dir;
                Vector3 up = t.InverseTransformDirection(Vector3.up);
                float height = Mathf.Abs(up.x) * m_HingeLocal.extents.x + Mathf.Abs(up.y) * m_HingeLocal.extents.y + Mathf.Abs(up.z) * m_HingeLocal.extents.z;
                position = anchor + Vector3.up * (height + m_ArrowGap) - dir * (0.5f * k_ArrowLength) + dir * Wave();
            }
            else
            {
                position = anchor + dir * (extent + m_ArrowGap + Wave());
            }
            m_Arrow.SetPositionAndRotation(position, Quaternion.LookRotation(dir));
        }

        const float k_HingeRearInset = 0.1f;   // 뒤쪽 끝에서 본체 길이의 10% 안쪽

        // 메시들을 space 의 로컬 공간에서 감싸는 상자
        static Bounds LocalBounds(List<Renderer> renderers, Transform space)
        {
            var b = new Bounds();
            bool any = false;
            foreach (var r in renderers)
            {
                var filter = r.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                var mb = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = space.InverseTransformPoint(r.transform.TransformPoint(corner));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                    else b.Encapsulate(p);
                }
            }
            return b;
        }

        float Wave() => m_ArrowTravel * (0.5f + 0.5f * Mathf.Sin(Time.time * m_PulseSpeed * 1.5f));

        static float ExtentAlong(Bounds b, Vector3 dir) =>
            Mathf.Abs(dir.x) * b.extents.x + Mathf.Abs(dir.y) * b.extents.y + Mathf.Abs(dir.z) * b.extents.z;

        static Bounds Bounds(List<Renderer> renderers)
        {
            var b = new Bounds();
            bool any = false;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        // 부품의 끼울 자리 시작 위치에 같은 모양의 반투명 형상
        void BuildGhost(AssemblyPart part)
        {
            var own = new List<Renderer>();
            CollectOwnRenderers(part, own);
            foreach (var r in own)
            {
                var filter = r.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                // 부품 기준 메시 자리를 그대로 옮긴다
                Matrix4x4 local = part.transform.worldToLocalMatrix * r.transform.localToWorldMatrix;
                var go = new GameObject("Ghost_" + r.name, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(m_Ghost, false);
                go.transform.localPosition = local.GetColumn(3);
                go.transform.localRotation = local.rotation;
                go.transform.localScale = local.lossyScale;
                go.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = m_GhostMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                m_GhostRenderers.Add(renderer);
            }
        }

        // owner(부품이나 힌지)에 속한 Renderer — 자식 부품(예: 상부 안의 내부 부품, 가스 활대 안의 노리쇠 뭉치)의 것은 뺀다
        static void CollectOwnRenderers(Component owner, List<Renderer> result)
        {
            foreach (var r in owner.GetComponentsInChildren<Renderer>())
            {
                if (Owner(r.transform) == owner)
                    result.Add(r);
            }
        }

        static Component Owner(Transform t)
        {
            for (; t != null; t = t.parent)
            {
                if (t.TryGetComponent(out AssemblyPart part)) return part;
                if (t.TryGetComponent(out AssemblyHinge hinge)) return hinge;
            }
            return null;
        }

        // --- 화살표 메시: +Z 를 가리키는 막대 + 사각뿔 ---

        const float k_ArrowLength = 0.07f;
        const float k_ShaftHalf = 0.004f;
        const float k_HeadHalf = 0.012f;
        const float k_HeadLength = 0.025f;

        static GameObject CreateArrow(Material material)
        {
            var go = new GameObject("Hint_Arrow", typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = ArrowMesh();
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        static Mesh ArrowMesh()
        {
            float s = k_ShaftHalf, h = k_HeadHalf, shaftEnd = k_ArrowLength - k_HeadLength;
            var v = new List<Vector3>();
            var tri = new List<int>();
            // 막대: 상자 (z 0 ~ shaftEnd)
            AddBox(v, tri, new Vector3(-s, -s, 0f), new Vector3(s, s, shaftEnd));
            // 머리: 사각뿔 (밑면 z = shaftEnd, 꼭짓점 z = k_ArrowLength)
            int b = v.Count;
            v.Add(new Vector3(-h, -h, shaftEnd)); v.Add(new Vector3(h, -h, shaftEnd));
            v.Add(new Vector3(h, h, shaftEnd)); v.Add(new Vector3(-h, h, shaftEnd));
            v.Add(new Vector3(0f, 0f, k_ArrowLength));
            tri.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });   // 밑면
            tri.AddRange(new[] { b, b + 1, b + 4, b + 1, b + 2, b + 4, b + 2, b + 3, b + 4, b + 3, b, b + 4 });
            var mesh = new Mesh { name = "Hint_Arrow" };
            mesh.SetVertices(v);
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static void AddBox(List<Vector3> v, List<int> tri, Vector3 min, Vector3 max)
        {
            int b = v.Count;
            for (int i = 0; i < 8; i++)
                v.Add(new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z));
            int[] faces =
            {
                0, 2, 3, 0, 3, 1,   // -Z
                4, 5, 7, 4, 7, 6,   // +Z
                0, 1, 5, 0, 5, 4,   // -Y
                2, 6, 7, 2, 7, 3,   // +Y
                0, 4, 6, 0, 6, 2,   // -X
                1, 3, 7, 1, 7, 5,   // +X
            };
            foreach (int f in faces)
                tri.Add(b + f);
        }
    }
}
