using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MilTrainSimVR.UrbanCombat
{
    // 깨지는 유리 판 (11/18 사용자: "카스2 처럼 유리창에 맞은 부위부터 부서지게" · "뚫리면 바로 부서지는 게 좋다, 유리창이 남아 있는 건 별로") —
    // 백화점 에스컬레이터 통유리 난간을 약 1.2 m 판으로 나눈 한 장 (실제 에스컬레이터처럼 판 사이에 틈 · 옆면이 보인다 — BreakableGlassBuilder).
    // 같은 날 창 유리도 (사용자: "모든 창틀에 유리창") — 건물 창 · 백화점 창(무늬 유리) · 주차장 부스 창마다 통유리 한 장 (UrbanGreyboxBuilder.WindowGlass.cs).
    //   창 유리는 총 개머리판으로 쳐도 깨진다 (m_StockBreaks, TempRifle.StockStrike). 처음엔 몸이 지나가면 깨지게 했는데 사용자가 바꿨다:
    //   "점프로 깨는 게 아닌, 총기 개머리판으로 깰 수 있게 — 그래야 유리창이 갑작스럽게 깨지지 않을 것 같아서".
    //   안 깨진 창 유리는 몸을 막는다 (m_BodyBlock, 사용자 11/18 "네, 막아 줘") — 창으로 들어가려면 먼저 쏘거나 개머리판으로 깬다.
    //   그 판정은 'Ignore Raycast' 층이라 총알 · 적 시선(Gunfire.ShotMask)은 그대로 지나간다
    //   · 처음엔 원래 유리 메시(지도 생성기가 만든 면을 판으로 자른 것)를 그대로 그린다 — 안 맞은 유리는 예전과 같다
    //   · 시작 뒤 몇 프레임에 걸쳐(판마다 한 프레임) 유리 면을 작은 세모 칸으로 나눠 둔다: 5 cm 격자의 꼭짓점을 흔들고 칸마다 대각선을 마구 그어
    //     깨진 유리처럼 모난 조각이 되게. 칸은 유리 윤곽(앞면 삼각형)으로 잘라 경사 · 반원 끝이 맞는다
    //   · 총알 길이 판을 지나면(BulletPath — 플레이어 총 · 적 총이 부른다) 그 판이 통째로 부서진다: 맞은 자리가 바로 뚫리고,
    //     거기서부터 하얀 금(m_CrackedMaterial)이 m_ShatterSpeed 로 번지며 칸이 차례로 떨어진다 (판 하나에 0.1 ~ 0.25 초).
    //     떨어지는 칸은 파편으로 쏟아지고, 맞은 순간 쨍그랑 (GlassShards). 이웃 판은 그대로
    //   · 몸을 막는 난간 판정(보이지 않는 상자, Ignore Raycast)은 그대로 — 깨진 자리로 떨어지지 않는다. 판엔 총알이 맞는 콜라이더가 없다 (판정은 면과 총알 길의 교점으로)
    //   · 다시하기는 씬을 새로 불러오니 유리도 처음대로
    // 로컬 축: x = 유리를 따라 (0 ~ m_Length), y = 위 (0 ~ m_Height), z = 두께 방향 (가운데 0)
    public class BreakableGlass : MonoBehaviour
    {
        [SerializeField] Vector2[] m_Outline = new Vector2[0];   // 앞면 삼각형 (로컬 x · y) — 3개씩
        [SerializeField] float m_Length = 1f;
        [SerializeField] float m_Height = 1f;
        [SerializeField] float m_Thickness = 0.016f;
        [Tooltip("격자 크기(m) — 칸은 이 격자를 흔들어 둘로 나눈 세모")]
        [SerializeField] float m_CellSize = 0.05f;
        [Tooltip("맞은 순간 바로 뚫리는 반지름(m)")]
        [SerializeField] float m_FirstHole = 0.07f;
        [Tooltip("부서짐이 번지는 빠르기(m/s)")]
        [SerializeField] float m_ShatterSpeed = 5f;
        [Tooltip("번지는 앞쪽의 하얀 금 띠 너비(m)")]
        [SerializeField] float m_CrackBand = 0.18f;
        [SerializeField] Material m_CrackedMaterial;
        [SerializeField] GlassShards m_ShardsPrefab;
        [Tooltip("총 개머리판으로 쳐도 깨진다 — 창 유리. 에스컬레이터 난간 유리는 꺼 둔다")]
        [SerializeField] bool m_StockBreaks;
        [Tooltip("안 깨진 동안 몸을 막는 판정 — 'Ignore Raycast' 층의 상자라 총알 · 적 시선은 지나간다. 깨지는 순간 끈다. 창 유리만 (에스컬레이터는 난간 상자가 따로 막는다)")]
        [SerializeField] Collider m_BodyBlock;
        [Tooltip("무늬 유리(백화점 창)의 테두리 무늬 폭(m). 0 이면 UV = 미터 그대로. 0 보다 크면 그림의 네 귀퉁이(m_UvBorderShare 만큼)가 이 폭에 맞고 가운데는 늘어난다 — 창 크기가 달라도 테두리 무늬 크기는 같다")]
        [SerializeField] float m_UvBorder;
        [SerializeField] float m_UvBorderShare = 0.25f;

        static readonly List<BreakableGlass> s_All = new List<BreakableGlass>();
        static int s_NextBuildFrame;
        static GlassShards s_Shards;

        // 판을 감싸는 공 — 총알 길 · 개머리판이 먼 판은 좌표 변환 없이 건너뛴다 (창 유리까지 판이 160장 남짓)
        Vector3 m_Center;
        float m_Radius;

        // 칸 = 세모. 번호 u = (j * m_Nu + i) * 2 + k
        int m_Nu, m_Nv;
        Vector2[] m_Corner;      // 흔든 격자 꼭짓점 (m_Nu + 1) × (m_Nv + 1)
        int[] m_Tri;             // 칸마다 꼭짓점 번호 셋 (반시계)
        int[] m_Neighbor;        // 칸마다 변 셋의 이웃 칸 (없으면 -1) — 변 e 는 꼭짓점 e → e+1
        byte[] m_State;          // 0 = 유리 밖, 1 = 있음, 2 = 빠짐
        bool[] m_Full;           // 칸 전체가 유리 안 (가장자리 칸이 아님)
        bool[] m_Anchor;         // 받침 · 손잡이 · 이웃 판에 붙은 칸 (지금은 안 쓴다)
        bool[] m_Cracked;        // 금 간 칸 (하얗게)
        bool[] m_Reached;
        float[] m_Reach;         // 부서짐이 닿는 거리 (맞은 자리에서, 칸마다 들쭉날쭉)
        int[] m_PolyFirst, m_PolyEnd;
        readonly List<int> m_PolyStart = new List<int>();
        readonly List<int> m_PolyCount = new List<int>();
        readonly List<Vector2> m_PolyVerts = new List<Vector2>();
        int m_InitialAlive, m_Alive;
        bool m_Built, m_Shattering, m_Done;
        float m_ShatterStart, m_ShardKeep;
        Vector3 m_ShotDir;
        Vector2 m_HitPoint;
        Mesh m_CellMesh;
        MeshFilter m_Filter;
        MeshRenderer m_Renderer;

        static readonly List<Vector3> s_V = new List<Vector3>();
        static readonly List<Vector3> s_N = new List<Vector3>();
        static readonly List<Vector2> s_U = new List<Vector2>();
        static readonly List<int> s_T0 = new List<int>();
        static readonly List<int> s_T1 = new List<int>();

        void OnEnable()
        {
            s_All.Add(this);
            Vector3 a = transform.position, b = transform.TransformPoint(new Vector3(m_Length, m_Height, 0f));
            m_Center = (a + b) * 0.5f;
            m_Radius = (b - a).magnitude * 0.5f + 0.05f;
        }

        void OnDisable() => s_All.Remove(this);

        void OnDestroy()
        {
            if (m_CellMesh != null)
                Destroy(m_CellMesh);
        }

        IEnumerator Start()
        {
            m_Filter = GetComponent<MeshFilter>();
            m_Renderer = GetComponent<MeshRenderer>();
            // 파편 · 소리는 미리 하나 — 처음 맞을 때 만들면 그 프레임이 멈칫한다
            if (s_Shards == null && m_ShardsPrefab != null)
                s_Shards = Instantiate(m_ShardsPrefab);
            // 칸 나누기는 한 프레임에 한 판씩 — 큰 창은 한 장에 5 ms 까지 든다 (11/18 잼). 그 전에 맞으면 Hit 이 그 자리에서 나눈다
            if (s_NextBuildFrame > Time.frameCount + 2000)
                s_NextBuildFrame = 0;   // 에디터에서 지난 Play 의 값이 남았을 때
            int frame = Mathf.Max(Time.frameCount + 10, s_NextBuildFrame + 1);
            s_NextBuildFrame = frame;
            while (Time.frameCount < frame)
                yield return null;
            if (!m_Built)
                BuildCells();
        }

        // 총알 길(from → to)이 지나는 유리 판을 모두 깬다 — 플레이어 총(TempRifle) · 적 총(EnemyShooter)이 부른다
        public static void BulletPath(Vector3 from, Vector3 to) => Path(from, to, false);

        // 개머리판의 한 점이 한 프레임에 움직인 길 — 개머리판으로 깨지는 판(창 유리)만 깬다. 하나라도 깼으면 true. TempRifle 이 부른다
        public static bool StockPath(Vector3 from, Vector3 to) => Path(from, to, true);

        static bool Path(Vector3 from, Vector3 to, bool stock)
        {
            Vector3 dir = to - from;
            float sq = dir.sqrMagnitude;
            bool broke = false;
            if (sq < 1e-8f)
                return false;
            for (int i = s_All.Count - 1; i >= 0; i--)
            {
                BreakableGlass glass = s_All[i];
                if (glass == null || (stock && !glass.m_StockBreaks))
                    continue;
                // 길에서 판 가운데까지 가장 가까운 거리가 판 반지름보다 멀면 안 지나간다
                Vector3 toCenter = glass.m_Center - from;
                float t = Mathf.Clamp01(Vector3.Dot(toCenter, dir) / sq);
                if ((toCenter - dir * t).sqrMagnitude > glass.m_Radius * glass.m_Radius)
                    continue;
                if (glass.Crosses(from, to, out Vector3 point))
                {
                    glass.Hit(point, dir.normalized);
                    broke = true;
                }
            }
            return broke;
        }

        // 무늬 유리의 UV 한 축 — 양 끝 border(m)는 그림의 share 만큼, 가운데는 나머지에 늘려 맞춘다. 생성기(처음 메시)와 같은 식
        public static float SliceUv(float x, float size, float border, float share)
        {
            float b = Mathf.Min(border, size * 0.5f);
            if (x <= b)
                return x / b * share;
            if (x >= size - b)
                return 1f - (size - x) / b * share;
            return Mathf.Lerp(share, 1f - share, (x - b) / Mathf.Max(1e-4f, size - 2f * b));
        }

        Vector2 Uv(Vector2 p) => m_UvBorder > 0f
            ? new Vector2(SliceUv(p.x, m_Length, m_UvBorder, m_UvBorderShare), SliceUv(p.y, m_Height, m_UvBorder, m_UvBorderShare))
            : p;

        bool Crosses(Vector3 from, Vector3 to, out Vector3 point)
        {
            point = default;
            if (m_Shattering || m_Done)
                return false;
            Vector3 a = transform.InverseTransformPoint(from), b = transform.InverseTransformPoint(to);
            if ((a.z > 0f) == (b.z > 0f) || Mathf.Approximately(a.z, b.z))
                return false;
            Vector3 p = Vector3.Lerp(a, b, a.z / (a.z - b.z));
            if (p.x < 0f || p.x > m_Length || p.y < 0f || p.y > m_Height)
                return false;
            point = transform.TransformPoint(new Vector3(p.x, p.y, 0f));
            return true;
        }

        // ---------------------------------------------------------------- 맞음 → 판 전체가 부서진다

        void Hit(Vector3 worldPoint, Vector3 dir)
        {
            if (!m_Built)
                BuildCells();
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            var hit = new Vector2(local.x, local.y);
            int cell = FindCell(hit);
            if (cell < 0 || m_State[cell] != 1)
                return;   // 유리 윤곽 밖 (격자 귀퉁이)

            m_Shattering = true;
            // 깨진 창은 지나갈 수 있다 — 몸을 막던 판정을 끈다
            if (m_BodyBlock != null)
                m_BodyBlock.enabled = false;
            m_ShatterStart = Time.time;
            m_ShotDir = dir;
            m_HitPoint = hit;
            m_ShardKeep = Mathf.Min(1f, 240f / Mathf.Max(1, m_InitialAlive));
            m_Reach = new float[m_State.Length];
            for (int u = 0; u < m_State.Length; u++)
                if (m_State[u] == 1)
                    m_Reach[u] = (Centroid(u) - hit).magnitude * Random.Range(0.8f, 1.25f);
            if (s_Shards == null && m_ShardsPrefab != null)
                s_Shards = Instantiate(m_ShardsPrefab);
            if (s_Shards != null)
                s_Shards.Sound(worldPoint, 1f, 1f);
            Step(force: cell);
        }

        void Update()
        {
            if (m_Shattering)
                Step(-1);
        }

        // 부서짐 한 걸음: 맞은 자리에서 r 안의 칸은 떨어지고, 그 바깥 띠는 하얗게 금 간다
        void Step(int force)
        {
            float r = m_FirstHole + m_ShatterSpeed * (Time.time - m_ShatterStart);
            for (int u = 0; u < m_State.Length; u++)
            {
                if (m_State[u] != 1)
                    continue;
                if (u == force || m_Reach[u] < r)
                {
                    m_State[u] = 2;
                    m_Alive--;
                    Shard(u, m_Reach[u] < m_FirstHole * 1.5f);
                }
                else if (m_Reach[u] < r + m_CrackBand)
                    m_Cracked[u] = true;
            }
            Rebuild();
            if (m_Alive <= 0)
            {
                m_Shattering = false;
                m_Done = true;
            }
        }

        // 떨어지는 칸 하나의 파편 — 맞은 자리 칸은 총알 쪽으로 튀고, 나머지는 바깥으로 살짝 벌어지며 쏟아진다
        void Shard(int u, bool atHole)
        {
            if (s_Shards == null || Random.value > m_ShardKeep)
                return;
            float r1 = Random.value, r2 = Random.value;
            if (r1 + r2 > 1f) { r1 = 1f - r1; r2 = 1f - r2; }
            Vector2 a = m_Corner[m_Tri[u * 3]], b = m_Corner[m_Tri[u * 3 + 1]], c = m_Corner[m_Tri[u * 3 + 2]];
            Vector2 q = a + (b - a) * r1 + (c - a) * r2;
            Vector3 pos = transform.TransformPoint(new Vector3(q.x, q.y, 0f));
            Vector2 out2 = (q - m_HitPoint).normalized;
            Vector3 outward = transform.TransformDirection(new Vector3(out2.x, out2.y * 0.3f, 0f));
            Vector3 v = atHole ? m_ShotDir * Random.Range(1f, 3f) + Random.insideUnitSphere * 0.6f
                               : m_ShotDir * Random.Range(0.2f, 0.9f) + outward * Random.Range(0.1f, 0.5f) + Random.insideUnitSphere * 0.3f;
            s_Shards.Shard(pos, v, Random.Range(0.015f, 0.045f));
        }

        Vector2 Centroid(int u) => (m_Corner[m_Tri[u * 3]] + m_Corner[m_Tri[u * 3 + 1]] + m_Corner[m_Tri[u * 3 + 2]]) / 3f;

        // 점이 든 칸 — 흔든 격자라 둘레 칸까지 본다
        int FindCell(Vector2 p)
        {
            int ci = Mathf.FloorToInt(p.x / m_CellSize), cj = Mathf.FloorToInt(p.y / m_CellSize);
            for (int j = cj - 1; j <= cj + 1; j++)
                for (int i = ci - 1; i <= ci + 1; i++)
                {
                    if (i < 0 || j < 0 || i >= m_Nu || j >= m_Nv)
                        continue;
                    for (int k = 0; k < 2; k++)
                    {
                        int u = (j * m_Nu + i) * 2 + k;
                        Vector2 a = m_Corner[m_Tri[u * 3]], b = m_Corner[m_Tri[u * 3 + 1]], c = m_Corner[m_Tri[u * 3 + 2]];
                        if (Side(a, b, p) >= 0f && Side(b, c, p) >= 0f && Side(c, a, p) >= 0f)
                            return u;
                    }
                }
            return -1;
        }

        // ---------------------------------------------------------------- 칸 나누기

        void BuildCells()
        {
            m_Built = true;
            float s = m_CellSize;
            m_Nu = Mathf.Max(1, Mathf.CeilToInt(m_Length / s - 1e-4f));
            m_Nv = Mathf.Max(1, Mathf.CeilToInt(m_Height / s - 1e-4f));
            int cw = m_Nu + 1;
            m_Corner = new Vector2[cw * (m_Nv + 1)];
            var rng = new System.Random(GetInstanceID());
            for (int j = 0; j <= m_Nv; j++)
                for (int i = 0; i <= m_Nu; i++)
                {
                    float x = Mathf.Min(m_Length, i * s), y = Mathf.Min(m_Height, j * s);
                    // 안쪽 꼭짓점만 흔든다 — 조각 끝(이웃 조각과 맞닿는 곳)과 위아래 끝은 곧게
                    if (i > 0 && i < m_Nu)
                        x = Mathf.Clamp(x + ((float)rng.NextDouble() - 0.5f) * 0.7f * s, 0f, m_Length);
                    if (j > 0 && j < m_Nv)
                        y = Mathf.Clamp(y + ((float)rng.NextDouble() - 0.5f) * 0.7f * s, 0f, m_Height);
                    m_Corner[j * cw + i] = new Vector2(x, y);
                }

            int units = m_Nu * m_Nv * 2;
            m_Tri = new int[units * 3];
            for (int j = 0; j < m_Nv; j++)
                for (int i = 0; i < m_Nu; i++)
                {
                    int c00 = j * cw + i, c10 = c00 + 1, c01 = c00 + cw, c11 = c01 + 1;
                    int u = (j * m_Nu + i) * 2;
                    if (rng.Next(2) == 0)
                    {
                        Set(u, c00, c10, c11);
                        Set(u + 1, c00, c11, c01);
                    }
                    else
                    {
                        Set(u, c00, c10, c01);
                        Set(u + 1, c10, c11, c01);
                    }
                }

            // 이웃: 같은 변(꼭짓점 둘)을 쓰는 칸
            m_Neighbor = new int[units * 3];
            var edges = new Dictionary<long, int>(units * 2);
            for (int u = 0; u < units; u++)
                for (int e = 0; e < 3; e++)
                {
                    int a = m_Tri[u * 3 + e], b = m_Tri[u * 3 + (e + 1) % 3];
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (edges.TryGetValue(key, out int other))
                    {
                        m_Neighbor[u * 3 + e] = other / 3;
                        m_Neighbor[other] = u;
                        edges.Remove(key);
                    }
                    else
                    {
                        m_Neighbor[u * 3 + e] = -1;
                        edges[key] = u * 3 + e;
                    }
                }

            // 칸마다 유리 윤곽과 겹친 부분 (볼록 다각형들)
            m_State = new byte[units];
            m_Full = new bool[units];
            m_Anchor = new bool[units];
            m_Cracked = new bool[units];
            m_Reached = new bool[units];
            var pieces = new List<int>[units];
            var area = new float[units];
            var subject = new List<Vector2>(8);
            var clipped = new List<Vector2>(12);
            var tmp = new List<Vector2>(12);
            var poolStart = new List<int>();
            var poolCount = new List<int>();
            var pool = new List<Vector2>();
            for (int t = 0; t + 2 < m_Outline.Length; t += 3)
            {
                Vector2 a = m_Outline[t], b = m_Outline[t + 1], c = m_Outline[t + 2];
                float signed = (b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y);
                if (Mathf.Abs(signed) < 1e-8f)
                    continue;   // 반원 부채꼴의 납작한 삼각형
                if (signed < 0f)
                    (b, c) = (c, b);
                int i0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)) / s) - 1, 0, m_Nu - 1);
                int i1 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)) / s) + 1, 0, m_Nu - 1);
                int j0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y)) / s) - 1, 0, m_Nv - 1);
                int j1 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y)) / s) + 1, 0, m_Nv - 1);
                for (int j = j0; j <= j1; j++)
                    for (int i = i0; i <= i1; i++)
                        for (int k = 0; k < 2; k++)
                        {
                            int u = (j * m_Nu + i) * 2 + k;
                            subject.Clear();
                            subject.Add(m_Corner[m_Tri[u * 3]]);
                            subject.Add(m_Corner[m_Tri[u * 3 + 1]]);
                            subject.Add(m_Corner[m_Tri[u * 3 + 2]]);
                            ClipConvex(subject, a, b, clipped, tmp);
                            ClipConvex(clipped, b, c, subject, tmp);
                            ClipConvex(subject, c, a, clipped, tmp);
                            float pa = PolygonArea(clipped);
                            if (clipped.Count < 3 || pa < 1e-8f)
                                continue;
                            (pieces[u] ??= new List<int>()).Add(poolStart.Count);
                            poolStart.Add(pool.Count);
                            poolCount.Add(clipped.Count);
                            pool.AddRange(clipped);
                            area[u] += pa;
                        }
            }

            m_PolyFirst = new int[units];
            m_PolyEnd = new int[units];
            m_PolyStart.Clear();
            m_PolyCount.Clear();
            m_PolyVerts.Clear();
            m_InitialAlive = 0;
            for (int u = 0; u < units; u++)
            {
                m_PolyFirst[u] = m_PolyStart.Count;
                if (pieces[u] != null)
                {
                    foreach (int p in pieces[u])
                    {
                        m_PolyStart.Add(m_PolyVerts.Count);
                        m_PolyCount.Add(poolCount[p]);
                        for (int q = 0; q < poolCount[p]; q++)
                            m_PolyVerts.Add(pool[poolStart[p] + q]);
                    }
                    Vector2 a = m_Corner[m_Tri[u * 3]], b = m_Corner[m_Tri[u * 3 + 1]], c = m_Corner[m_Tri[u * 3 + 2]];
                    float full = Mathf.Abs((b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y)) * 0.5f;
                    m_State[u] = 1;
                    m_Full[u] = area[u] >= full * 0.985f;
                    // 테두리: 윤곽에 잘린 칸(받침 · 손잡이 · 반원 끝) 또는 조각 양 끝(이웃 조각)에 닿은 칸
                    bool atEnd = a.x <= 1e-4f || b.x <= 1e-4f || c.x <= 1e-4f || a.x >= m_Length - 1e-4f || b.x >= m_Length - 1e-4f || c.x >= m_Length - 1e-4f;
                    m_Anchor[u] = !m_Full[u] || atEnd;
                    m_InitialAlive++;
                }
                m_PolyEnd[u] = m_PolyStart.Count;
            }
            m_Alive = m_InitialAlive;

            void Set(int u, int a, int b, int c)
            {
                m_Tri[u * 3] = a;
                m_Tri[u * 3 + 1] = b;
                m_Tri[u * 3 + 2] = c;
            }
        }

        // 볼록 다각형 src 를 선분 a→b 의 왼쪽(반시계 삼각형의 안쪽)으로 자른다 (Sutherland–Hodgman 한 변)
        static void ClipConvex(List<Vector2> src, Vector2 a, Vector2 b, List<Vector2> dst, List<Vector2> tmp)
        {
            tmp.Clear();
            int n = src.Count;
            for (int k = 0; k < n; k++)
            {
                Vector2 p = src[k], q = src[(k + 1) % n];
                float dp = Side(a, b, p), dq = Side(a, b, q);
                if (dp >= 0f)
                    tmp.Add(p);
                if ((dp >= 0f) != (dq >= 0f))
                    tmp.Add(Vector2.Lerp(p, q, dp / (dp - dq)));
            }
            dst.Clear();
            dst.AddRange(tmp);
        }

        static float Side(Vector2 a, Vector2 b, Vector2 p) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);

        static float PolygonArea(List<Vector2> p)
        {
            float s = 0f;
            for (int k = 0; k < p.Count; k++)
            {
                Vector2 a = p[k], b = p[(k + 1) % p.Count];
                s += a.x * b.y - b.x * a.y;
            }
            return Mathf.Abs(s) * 0.5f;
        }

        // ---------------------------------------------------------------- 칸 메시 (재질 둘: 0 = 유리, 1 = 금 간 유리)

        void Rebuild()
        {
            if (m_CellMesh == null)
            {
                m_CellMesh = new Mesh { name = name + " (깨진 유리)" };
                m_CellMesh.MarkDynamic();
            }
            s_V.Clear();
            s_N.Clear();
            s_U.Clear();
            s_T0.Clear();
            s_T1.Clear();
            float h = m_Thickness * 0.5f;
            for (int u = 0; u < m_State.Length; u++)
            {
                if (m_State[u] != 1)
                    continue;
                List<int> tris = m_Cracked[u] && m_CrackedMaterial != null ? s_T1 : s_T0;
                for (int p = m_PolyFirst[u]; p < m_PolyEnd[u]; p++)
                {
                    int start = m_PolyStart[p], count = m_PolyCount[p];
                    int front = s_V.Count;
                    for (int k = 0; k < count; k++)
                        Add(m_PolyVerts[start + k], h, Vector3.forward);
                    int back = s_V.Count;
                    for (int k = 0; k < count; k++)
                        Add(m_PolyVerts[start + k], -h, Vector3.back);
                    for (int k = 1; k + 1 < count; k++)
                    {
                        tris.Add(front); tris.Add(front + k); tris.Add(front + k + 1);
                        tris.Add(back); tris.Add(back + k + 1); tris.Add(back + k);
                    }
                }
                // 구멍 둘레의 유리 두께 면 — 꽉 찬 칸이 빠진 칸과 맞닿은 변에만
                if (!m_Full[u])
                    continue;
                for (int e = 0; e < 3; e++)
                {
                    int n = m_Neighbor[u * 3 + e];
                    if (n >= 0 && m_State[n] == 2)
                        Rim(m_Corner[m_Tri[u * 3 + e]], m_Corner[m_Tri[u * 3 + (e + 1) % 3]], h, tris);
                }
            }
            m_CellMesh.Clear();
            m_CellMesh.indexFormat = s_V.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            m_CellMesh.SetVertices(s_V);
            m_CellMesh.SetNormals(s_N);
            m_CellMesh.SetUVs(0, s_U);
            bool cracks = m_CrackedMaterial != null;
            m_CellMesh.subMeshCount = cracks ? 2 : 1;
            m_CellMesh.SetTriangles(s_T0, 0);
            if (cracks)
                m_CellMesh.SetTriangles(s_T1, 1);
            m_CellMesh.RecalculateBounds();
            if (m_Filter != null && m_Filter.sharedMesh != m_CellMesh)
            {
                m_Filter.sharedMesh = m_CellMesh;
                if (cracks && m_Renderer != null)
                    m_Renderer.sharedMaterials = new[] { m_Renderer.sharedMaterial, m_CrackedMaterial };
            }
            if (m_Alive <= 0 && m_Renderer != null)
                m_Renderer.enabled = false;
        }

        void Add(Vector2 p, float z, Vector3 normal)
        {
            s_V.Add(new Vector3(p.x, p.y, z));
            s_N.Add(normal);
            s_U.Add(Uv(p));
        }

        // 변 p→q (반시계 칸의 변 — 바깥은 오른쪽)의 두께 면
        void Rim(Vector2 p, Vector2 q, float h, List<int> tris)
        {
            Vector2 d = q - p;
            var outward = new Vector3(d.y, -d.x, 0f).normalized;
            int b = s_V.Count;
            s_V.Add(new Vector3(p.x, p.y, h)); s_V.Add(new Vector3(q.x, q.y, h));
            s_V.Add(new Vector3(q.x, q.y, -h)); s_V.Add(new Vector3(p.x, p.y, -h));
            for (int k = 0; k < 4; k++)
                s_N.Add(outward);
            Vector2 up = Uv(p), uq = Uv(q);
            s_U.Add(up); s_U.Add(uq); s_U.Add(uq); s_U.Add(up);
            // 면의 앞쪽(Cross(b-a, c-a))이 바깥을 보게
            Vector3 n = Vector3.Cross(s_V[b + 1] - s_V[b], s_V[b + 2] - s_V[b]);
            if (Vector3.Dot(n, outward) >= 0f)
            {
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
            }
            else
            {
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                tris.Add(b); tris.Add(b + 3); tris.Add(b + 2);
            }
        }
    }
}
