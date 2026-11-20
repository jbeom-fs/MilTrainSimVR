using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MilTrainSimVR.UrbanCombat.EditorTools
{
    // 4단계 B 설비 (사용자 11/06 — "옥상의 콜라이더 없는 대형 공업용 에어컨 실외기도 있어야 하지 않을까? 옥상 출입구처럼" → B 목업 → "이대로 적용").
    // 건물마다 옥상 · 벽에 설비를 붙인다. 모두 보이기만 한다(콜라이더 없음 — 길찾기 · 부딪힘 · 총알 판정은 그대로):
    //   옥상: 대형 실외기 줄 — 윗면 토출 시스템 에어컨(2팬 1.30 / 1팬 0.94 × 0.76 × 1.69 m), 뒤 · 앞 위쪽 · 줄 끝 옆 열교환기 그릴, 윗면 팬 보호망,
    //         아연도금 받침 레일 · 앞 배관(검은 보온재) · 배관 덕트 · 배관 후드. 연면적(층 × 바닥) 600 m² 에 1대(1 ~ 6대), 10층 이상 · 백화점은 두 줄 10대까지.
    //         중 · 저층 다섯에 하나는 없다(벽걸이 실외기를 쓰는 건물). 옥탑과 그 문 앞은 비운다
    //       물탱크 — 중 · 저층 60 % = 파란(70 %) · 흰 FRP 원통(둥근 지붕 · 띠) + 철 받침, 10층 이상 · 백화점 = 1 m 판 조립 사각 탱크 + 콘크리트 받침 · 사다리
    //       안테나 — 10층 이상 = 기지국 기둥(판 안테나 4 · 접시), 나머지 55 % = TV 안테나(옥탑 지붕 또는 옥상 받침)
    //   벽(청운백화점 빼고): 배수관 — 뒤 모서리 쪽 1 ~ 2줄, 옥상 높이 집수기 → 땅, 층마다 이음 · 고정쇠
    //       노란 가스관 — 8층 이하, 옆면 앞 모서리에서 0.62 m: 세로관 + 층마다 계량기함, 1층 출입구 위 가로관 → 가게 계량기
    //       벽걸이 실외기 — 골목 쪽 벽에만(사용자 11/06: 실외기는 길에서 잘 안 보이는 건물 사이에 — 벽에서 밖으로 나가다 옆 건물 · 샛길을
    //       먼저 만나는 벽. 차도를 먼저 만나면 길 쪽, 아무것도 없으면 공터라 안 붙인다), 2층 이상(1층 길에는 안 둔다): 들어갈 수 있는 층은 창 아래,
    //       막힌 층은 창 띠 아래. 받침대 · 배관 테이프. 사무용 건물(패널 · 화강석)은 적게. 창 · 문 · 간판 자리는 피한다
    // 자리 · 개수 · 색은 건물 이름 씨앗 — 목업과 같은 식이라 다시 지어도 같다. 창 · 문 자리는 BuildInterior · BuildMall 이 돌려준 구멍 목록.
    // 원통 · 원뿔대 · 팬 판(UV 0..1)이 있어 상자 면만 다루는 CombineVisuals 를 거치지 않고, 건물을 합친 뒤 재질별 메시를 하나씩 따로 만든다.
    // 가는 관 · 고정쇠 · 안테나 막대 · 테이프는 그림자를 끈다. 텍스처는 Textures/Map/Source~/make_utility_textures.py
    public static partial class UrbanGreyboxBuilder
    {
        const float k_GasMaxFloors = 8;        // 이 층수까지만 노란 가스관 (주거 · 상가 겸용 중 · 저층)
        const float k_GasRiserU = 0.62f;       // 가스 세로관 — 옆면 앞 모서리에서
        const int k_WallAcMax = 16;            // 건물 하나의 벽걸이 실외기 최대

        // 건물마다 설비 자리 — BuildBuilding 이 남기고, 합친 뒤 BuildUtilities 가 쓴다
        class UtilitySite
        {
            public Transform layout;   // 배치의 이 건물 상자 — 골목 판별 때 자기 자신은 뺀다
            public float W, D, roofY;
            public int floors, enterable, hash;
            public bool ceramic, office, metalPipe, hasPent;
            public Rect pent;
            public float pentH;
            public readonly List<(int sd, float u0, float u1, float y0, float y1)> holes = new List<(int sd, float u0, float u1, float y0, float y1)>();
        }

        static readonly Dictionary<Transform, UtilitySite> s_UtilitySites = new Dictionary<Transform, UtilitySite>();
        static int s_UtilAc, s_UtilAcRoofs, s_UtilTanks, s_UtilMasts, s_UtilTv, s_UtilDownpipes, s_UtilGas, s_UtilWallAc, s_UtilVerts, s_UtilAlleyWalls, s_UtilWalls;
        // 벽 바깥이 골목인지 보는 데 쓰는 배치 — 건물 상자 · 바닥(샛길 · 차도 · 인도 · 대지)
        static readonly List<Transform> s_ExposureBuildings = new List<Transform>();
        static readonly List<(GreyboxGroundSpec.Kind kind, Transform t)> s_ExposureGrounds = new List<(GreyboxGroundSpec.Kind kind, Transform t)>();

        static void ResetUtilities(GreyboxBuildingSpec[] buildings, GreyboxGroundSpec[] grounds)
        {
            s_UtilitySites.Clear();
            s_UtilAc = s_UtilAcRoofs = s_UtilTanks = s_UtilMasts = s_UtilTv = s_UtilDownpipes = s_UtilGas = s_UtilWallAc = s_UtilVerts = s_UtilAlleyWalls = s_UtilWalls = 0;
            s_ExposureBuildings.Clear();
            foreach (GreyboxBuildingSpec b in buildings)
                s_ExposureBuildings.Add(b.transform);
            s_ExposureGrounds.Clear();
            foreach (GreyboxGroundSpec g in grounds)
                s_ExposureGrounds.Add((g.kind, g.transform));
        }

        static string UtilitySummary()
        {
            return $"설비 — 옥상 실외기 {s_UtilAc}대({s_UtilAcRoofs}곳) · 물탱크 {s_UtilTanks} · 기지국 {s_UtilMasts} · TV 안테나 {s_UtilTv} · " +
                $"배수관 {s_UtilDownpipes} · 가스관 {s_UtilGas}동 · 벽걸이 실외기 {s_UtilWallAc}(골목 쪽 벽 {s_UtilAlleyWalls}/{s_UtilWalls}면에만) · 꼭짓점 {s_UtilVerts:N0}";
        }

        // 배치 상자(위치 · 가로 · 깊이 · y 회전) 안인가 — 높이는 안 본다
        static bool InLayoutBox(Transform t, Vector3 p)
        {
            Quaternion r = Quaternion.Euler(0f, t.eulerAngles.y, 0f);
            Vector3 l = Quaternion.Inverse(r) * (new Vector3(p.x, 0f, p.z) - new Vector3(t.position.x, 0f, t.position.z));
            return Mathf.Abs(l.x) < Mathf.Abs(t.lossyScale.x) * 0.5f && Mathf.Abs(l.z) < Mathf.Abs(t.lossyScale.z) * 0.5f;
        }

        // 이 자리 바닥 — 겹치면 샛길 > 차도 > 인도 > 대지 (없으면 null)
        static GreyboxGroundSpec.Kind? GroundKindAt(Vector3 p)
        {
            GreyboxGroundSpec.Kind? best = null;
            int rank = 0;
            foreach (var g in s_ExposureGrounds)
            {
                if (!InLayoutBox(g.t, p))
                    continue;
                int rk = g.kind == GreyboxGroundSpec.Kind.Alley ? 4 : g.kind == GreyboxGroundSpec.Kind.Road ? 3 : g.kind == GreyboxGroundSpec.Kind.Sidewalk ? 2 : 1;
                if (rk > rank)
                {
                    rank = rk;
                    best = g.kind;
                }
            }
            return best;
        }

        // openings = BuildInterior · BuildMall 의 층마다 · 면마다 구멍 (u0, u1, v0, v1) — 건물 좌표 (면, u, 높이)로 옮겨 둔다
        static void RegisterUtilitySite(Transform root, GreyboxBuildingSpec spec, int floors, int enterable, float W, float D, float roofY, bool ceramic,
            bool detailed, List<Vector4>[][] openings)
        {
            GreyboxBuildingSpec.Finish f = spec.finish;
            var site = new UtilitySite
            {
                layout = spec.transform,
                W = W,
                D = D,
                roofY = roofY,
                floors = floors,
                enterable = enterable,
                hash = StableHash(spec.name) & 0x7fffffff,
                ceramic = ceramic,
                office = f == GreyboxBuildingSpec.Finish.PanelSilver || f == GreyboxBuildingSpec.Finish.PanelWhite || f == GreyboxBuildingSpec.Finish.PanelDark
                    || f == GreyboxBuildingSpec.Finish.GraniteLight,
            };
            site.metalPipe = site.office || f == GreyboxBuildingSpec.Finish.Concrete;
            // 옥탑은 바깥벽 입체 디테일을 붙이는 건물에만 (UrbanGreyboxBuilder.Details.cs 와 같은 자리)
            if (detailed && PenthouseSpot(site.hash, floors >= k_TowerFloors, W, D, out float pw, out float pd, out float ph, out bool right))
            {
                site.hasPent = true;
                site.pent = PenthouseRect(W, D, pw, pd, right);
                site.pentH = ph;
            }
            if (openings != null)
            {
                for (int fl = 0; fl < openings.Length; fl++)
                {
                    if (openings[fl] == null)
                        continue;
                    float fy = k_Curb + fl * k_FloorHeight;
                    for (int sd = 0; sd < 4; sd++)
                    {
                        float off = sd >= 2 ? k_Wall : 0f;
                        foreach (Vector4 h in openings[fl][sd])
                            site.holes.Add((sd, h.x + off, h.y + off, fy + h.z, fy + h.w));
                    }
                }
            }
            s_UtilitySites[root] = site;
        }

        // 재질 하나에 모으는 메시 (위치 · 법선 · UV · 삼각형)
        class UtilMesh
        {
            public readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> t = new List<int>();
        }

        static void BuildUtilities(Transform root, Materials m)
        {
            if (!s_UtilitySites.TryGetValue(root, out UtilitySite site) || m.utilCasing == null)
                return;
            float W = site.W, D = site.D, roofY = site.roofY;
            int floors = site.floors, hash = site.hash;
            bool cer = site.ceramic, tower = floors >= k_TowerFloors, yard = tower || cer;
            float mgn = cer ? 1.5f : 1.0f;   // 옥상 설비를 바깥면에서 이만큼 안쪽에
            var meshes = new Dictionary<Material, UtilMesh>();

            // ================================================================ 모양 만들기
            UtilMesh L(Material mat)
            {
                if (!meshes.TryGetValue(mat, out UtilMesh um))
                    meshes[mat] = um = new UtilMesh();
                return um;
            }
            // 축에 나란한 상자 — UV 는 건물 좌표 (uvo 를 원점으로)
            void AddBox(Material mat, Vector3 a, Vector3 b, Vector3 uvo = default)
            {
                Vector3 mn = Vector3.Min(a, b), mx = Vector3.Max(a, b);
                if (mx.x - mn.x < 0.001f || mx.y - mn.y < 0.001f || mx.z - mn.z < 0.001f)
                    return;
                UtilMesh um = L(mat);
                Vector3[] ns = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
                foreach (Vector3 nn in ns)
                {
                    Vector3 ax = nn.x != 0 ? Vector3.up : nn.y != 0 ? Vector3.forward : Vector3.right;
                    Vector3 bx = Vector3.Cross(nn, ax);
                    if (Vector3.Dot(Vector3.Cross(ax, bx), nn) < 0)
                        bx = -bx;
                    Vector3 c = (mn + mx) * 0.5f, h = (mx - mn) * 0.5f;
                    Vector3 cc = c + Vector3.Scale(nn, h), aa = Vector3.Scale(ax, h), bb = Vector3.Scale(bx, h);
                    Vector3[] q = { cc - aa - bb, cc - aa + bb, cc + aa + bb, cc + aa - bb };
                    int s0 = um.v.Count;
                    foreach (Vector3 p in q)
                    {
                        um.v.Add(p);
                        um.n.Add(nn);
                        Vector3 pu = p - uvo;
                        um.uv.Add(Mathf.Abs(nn.x) > 0.5f ? new Vector2(pu.z, pu.y) : Mathf.Abs(nn.y) > 0.5f ? new Vector2(pu.x, pu.z) : new Vector2(pu.x, pu.y));
                    }
                    um.t.AddRange(new[] { s0, s0 + 2, s0 + 1, s0, s0 + 3, s0 + 2 });
                }
            }
            // 판 하나 (UV 0..1) — 앞면이 nrm 쪽
            void AddQuad(Material mat, Vector3 c, Vector3 au, Vector3 av, float half, Vector3 nrm)
            {
                UtilMesh um = L(mat);
                int s0 = um.v.Count;
                Vector3[] q = { c - au * half - av * half, c - au * half + av * half, c + au * half + av * half, c + au * half - av * half };
                Vector2[] uq = { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
                for (int k = 0; k < 4; k++)
                {
                    um.v.Add(q[k]);
                    um.n.Add(nrm);
                    um.uv.Add(uq[k]);
                }
                if (Vector3.Dot(Vector3.Cross(q[1] - q[0], q[2] - q[0]), nrm) > 0f)
                    um.t.AddRange(new[] { s0, s0 + 1, s0 + 2, s0, s0 + 2, s0 + 3 });
                else
                    um.t.AddRange(new[] { s0, s0 + 2, s0 + 1, s0, s0 + 3, s0 + 2 });
            }
            // 원뿔대(r0 == r1 이면 기둥 · 관) — p0 → p1, 옆면은 부드러운 법선. caps: 0 없음 · 1 위만 · 2 양쪽
            void AddFrustum(Material mat, Vector3 p0, Vector3 p1, float r0, float r1, int sides, int caps)
            {
                Vector3 axv = p1 - p0;
                float len = axv.magnitude;
                if (len < 0.001f)
                    return;
                Vector3 axn = axv / len;
                Vector3 e1 = Vector3.Cross(axn, Mathf.Abs(axn.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                Vector3 e2 = Vector3.Cross(axn, e1);
                UtilMesh um = L(mat);
                float dr = r1 - r0;
                for (int i = 0; i < sides; i++)
                {
                    float a0 = 2f * Mathf.PI * i / sides, a1 = 2f * Mathf.PI * (i + 1) / sides;
                    Vector3 n0 = e1 * Mathf.Cos(a0) + e2 * Mathf.Sin(a0), n1 = e1 * Mathf.Cos(a1) + e2 * Mathf.Sin(a1);
                    Vector3 s0n = (n0 * len - axn * dr).normalized, s1n = (n1 * len - axn * dr).normalized;
                    Vector3[] q = { p0 + n0 * r0, p0 + n1 * r0, p1 + n1 * r1, p1 + n0 * r1 };
                    Vector3[] nq = { s0n, s1n, s1n, s0n };
                    Vector2[] uq = { new Vector2(a0 * r0, 0f), new Vector2(a1 * r0, 0f), new Vector2(a1 * r1, len), new Vector2(a0 * r1, len) };
                    int s0 = um.v.Count;
                    for (int k = 0; k < 4; k++)
                    {
                        um.v.Add(q[k]);
                        um.n.Add(nq[k]);
                        um.uv.Add(uq[k]);
                    }
                    Vector3 fn = (s0n + s1n).normalized;
                    if (Vector3.Dot(Vector3.Cross(q[1] - q[0], q[2] - q[0]), fn) > 0f)
                        um.t.AddRange(new[] { s0, s0 + 1, s0 + 2, s0, s0 + 2, s0 + 3 });
                    else
                        um.t.AddRange(new[] { s0, s0 + 2, s0 + 1, s0, s0 + 3, s0 + 2 });
                }
                for (int cap = 0; cap < 2; cap++)
                {
                    bool top = cap == 1;
                    if (caps == 0 || (caps == 1 && !top))
                        continue;
                    Vector3 pc = top ? p1 : p0, nc = top ? axn : -axn;
                    float rc = top ? r1 : r0;
                    if (rc < 0.001f)
                        continue;
                    int c0 = um.v.Count;
                    um.v.Add(pc);
                    um.n.Add(nc);
                    um.uv.Add(new Vector2(0.5f, 0.5f));
                    for (int i = 0; i < sides; i++)
                    {
                        float a0 = 2f * Mathf.PI * i / sides;
                        um.v.Add(pc + (e1 * Mathf.Cos(a0) + e2 * Mathf.Sin(a0)) * rc);
                        um.n.Add(nc);
                        um.uv.Add(new Vector2(0.5f + 0.5f * Mathf.Cos(a0), 0.5f + 0.5f * Mathf.Sin(a0)));
                    }
                    for (int i = 0; i < sides; i++)
                    {
                        int ia = c0 + 1 + i, ib = c0 + 1 + (i + 1) % sides;
                        if (Vector3.Dot(Vector3.Cross(um.v[ia] - pc, um.v[ib] - pc), nc) > 0f)
                            um.t.AddRange(new[] { c0, ia, ib });
                        else
                            um.t.AddRange(new[] { c0, ib, ia });
                    }
                }
            }
            void AddPrism(Material mat, Vector3 p0, Vector3 p1, float rad, int sides, bool caps) => AddFrustum(mat, p0, p1, rad, rad, sides, caps ? 2 : 0);
            // 면 좌표: 0 앞(-z) · 1 뒤(+z) · 2 왼(-x) · 3 오른(+x), u = 면을 따라(앞 · 뒤 x, 옆 z), d = 겉면에서 바깥으로
            Vector3 FP(int sd, float u, float y, float d) => sd == 0 ? new Vector3(u, y, -d) : sd == 1 ? new Vector3(u, y, D + d)
                : sd == 2 ? new Vector3(-d, y, u) : new Vector3(W + d, y, u);
            void FBox(Material mat, int sd, float u0, float u1, float y0, float y1, float d0, float d1) => AddBox(mat, FP(sd, u0, y0, d0), FP(sd, u1, y1, d1));
            float Len(int sd) => sd < 2 ? W : D;
            Vector3 OutDir(int sd) => sd == 0 ? Vector3.back : sd == 1 ? Vector3.forward : sd == 2 ? Vector3.left : Vector3.right;
            Vector3 AlongDir(int sd) => sd < 2 ? Vector3.right : Vector3.forward;

            // 간판 자리 (납작한 정면 간판 · 돌출 간판 모두) — 면, u0, u1, y0, y1
            var signs = new List<(int sd, float u0, float u1, float y0, float y1)>();
            if (!cer)
            {
                foreach (Bounds sb in s_SignBounds)
                {
                    var lo = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                    Vector3 hi = -lo;
                    for (int k = 0; k < 8; k++)
                    {
                        var wp = new Vector3((k & 1) == 0 ? sb.min.x : sb.max.x, (k & 2) == 0 ? sb.min.y : sb.max.y, (k & 4) == 0 ? sb.min.z : sb.max.z);
                        Vector3 lp = root.InverseTransformPoint(wp);
                        lo = Vector3.Min(lo, lp);
                        hi = Vector3.Max(hi, lp);
                    }
                    int sds = hi.z <= 0.05f && lo.z > -2.2f && lo.x < W && hi.x > 0f ? 0
                        : lo.z >= D - 0.05f && hi.z < D + 2.2f && lo.x < W && hi.x > 0f ? 1
                        : hi.x <= 0.05f && lo.x > -2.2f && lo.z < D && hi.z > 0f ? 2
                        : lo.x >= W - 0.05f && hi.x < W + 2.2f && lo.z < D && hi.z > 0f ? 3
                        : -1;
                    if (sds >= 0)
                        signs.Add((sds, sds < 2 ? lo.x : lo.z, sds < 2 ? hi.x : hi.z, lo.y, hi.y));
                }
            }
            // 이 면의 이 자리가 창 · 문 · 간판과 겹치나 (mq 만큼 여유)
            bool Blocked(int sdq, float u0q, float u1q, float y0q, float y1q, float mq)
            {
                foreach (var hq in site.holes)
                {
                    if (hq.sd == sdq && hq.u1 + mq > u0q && hq.u0 - mq < u1q && hq.y1 + mq > y0q && hq.y0 - mq < y1q)
                        return true;
                }
                foreach (var sq in signs)
                {
                    if (sq.sd == sdq && sq.u1 + mq > u0q && sq.u0 - mq < u1q && sq.y1 + mq > y0q && sq.y0 - mq < y1q)
                        return true;
                }
                return false;
            }

            // ================================================================ 옥상
            var occ = new List<Rect>();   // 차지한 자리 (x, z)
            if (site.hasPent)
                occ.Add(Rect.MinMaxRect(site.pent.xMin - 0.6f, site.pent.yMin - 1.6f, site.pent.xMax + 0.6f, site.pent.yMax + 0.6f));   // 옥탑 둘레 · 문 앞
            bool Free(Rect rc)
            {
                var big = new Rect(rc.x - 0.6f, rc.y - 0.6f, rc.width + 1.2f, rc.height + 1.2f);
                foreach (Rect o in occ)
                {
                    if (o.Overlaps(big))
                        return false;
                }
                return true;
            }
            // 빈자리 — 네 모서리 · 네 변 가운데를 씨앗 차례로 보고, 없으면 0.5 m 씩 훑는다
            Rect? Place(float fw, float fd, System.Random rq)
            {
                float xa = mgn + 0.3f, xb = W - mgn - 0.3f - fw, za = mgn + 0.3f, zb = D - mgn - 0.3f - fd;
                if (xb < xa || zb < za)
                    return null;
                var cands = new List<Vector2>
                {
                    new Vector2(xa, za), new Vector2(xb, za), new Vector2(xa, zb), new Vector2(xb, zb),
                    new Vector2((xa + xb) * 0.5f, za), new Vector2((xa + xb) * 0.5f, zb), new Vector2(xa, (za + zb) * 0.5f), new Vector2(xb, (za + zb) * 0.5f),
                };
                for (int i = cands.Count - 1; i > 0; i--)
                {
                    int j = rq.Next(i + 1);
                    (cands[i], cands[j]) = (cands[j], cands[i]);
                }
                foreach (Vector2 cq in cands)
                {
                    var rc = new Rect(cq.x, cq.y, fw, fd);
                    if (Free(rc))
                        return rc;
                }
                for (float zz = za; zz <= zb; zz += 0.5f)
                {
                    for (float xx = xa; xx <= xb; xx += 0.5f)
                    {
                        var rc = new Rect(xx, zz, fw, fd);
                        if (Free(rc))
                            return rc;
                    }
                }
                return null;
            }

            // ---- 대형 실외기 줄 — 띠 0 뒤(x 를 따라, 앞이 -z) · 1 왼(z 를 따라, 앞이 +x) · 2 오른(z, 앞이 -x) 중 들어가는 곳 (뒤를 먼저 볼 때가 많게)
            var arng = new System.Random(hash ^ 0x2C1B3C6D);
            bool acSkip = !yard && arng.NextDouble() < 0.2;
            bool bigU = yard || arng.NextDouble() < 0.5;
            float uw = bigU ? 1.30f : 0.94f, ud = 0.76f, uh = 1.69f, gap = 0.06f, rail = 0.15f, aisle = 1.0f;
            int nUnits = Mathf.Clamp(Mathf.RoundToInt(floors * W * D / 600f), 1, yard ? 10 : 6);
            int rowsN = yard && nUnits >= 4 ? 2 : 1;
            int perRow = Mathf.CeilToInt(nUnits / (float)rowsN);
            float bandDepth = rowsN * ud + (rowsN - 1) * aisle + 0.8f;   // 앞 배관 덕트까지
            int strip = -1;
            Vector2 seg = default;
            if (!acSkip)
            {
                var order = new List<int> { 0, 1, 2 };
                for (int i = order.Count - 1; i > 0; i--)
                {
                    int j = arng.Next(i + 1);
                    (order[i], order[j]) = (order[j], order[i]);
                }
                if (arng.NextDouble() < 0.6)
                {
                    order.Remove(0);
                    order.Insert(0, 0);
                }
                for (int tries = perRow; tries >= 1 && strip < 0; tries--)
                {
                    foreach (int stp in order)
                    {
                        float s0 = mgn, s1 = stp == 0 ? W - mgn : D - mgn;
                        Rect band = stp == 0 ? Rect.MinMaxRect(mgn, D - mgn - bandDepth, W - mgn, D - mgn)
                            : stp == 1 ? Rect.MinMaxRect(mgn, mgn, mgn + bandDepth, D - mgn)
                            : Rect.MinMaxRect(W - mgn - bandDepth, mgn, W - mgn, D - mgn);
                        if (band.width <= 0f || band.height <= 0f || (stp == 0 ? D : W) < 2f * mgn + bandDepth + 1f)
                            continue;
                        var segs = new List<Vector2> { new Vector2(s0, s1) };
                        foreach (Rect blk in occ)
                        {
                            if (!blk.Overlaps(band))
                                continue;
                            float b0 = stp == 0 ? blk.xMin : blk.yMin, b1 = stp == 0 ? blk.xMax : blk.yMax;
                            var next = new List<Vector2>();
                            foreach (Vector2 g in segs)
                            {
                                if (b1 <= g.x || b0 >= g.y)
                                {
                                    next.Add(g);
                                    continue;
                                }
                                if (b0 > g.x)
                                    next.Add(new Vector2(g.x, b0));
                                if (b1 < g.y)
                                    next.Add(new Vector2(b1, g.y));
                            }
                            segs = next;
                        }
                        float need = tries * uw + (tries - 1) * gap + 0.4f + 1.0f;   // 레일 끝 + 배관 후드
                        foreach (Vector2 g in segs)
                        {
                            if (g.y - g.x >= need && (strip < 0 || g.y - g.x > seg.y - seg.x))
                            {
                                strip = stp;
                                seg = g;
                            }
                        }
                        if (strip >= 0)
                        {
                            perRow = tries;
                            break;
                        }
                    }
                }
            }
            if (strip >= 0)
            {
                // 띠 좌표 → 건물 좌표: a = 띠를 따라, b = 그 줄 뒷면에서 앞으로, 줄 r 은 뒷면이 (ud + aisle) 씩 앞
                Vector3 P(int r, float a, float y, float b)
                {
                    float o = mgn + r * (ud + aisle) + b;
                    return strip == 0 ? new Vector3(a, y, D - o) : strip == 1 ? new Vector3(o, y, a) : new Vector3(W - o, y, a);
                }
                float rowLen = perRow * uw + (perRow - 1) * gap;
                float slack = (seg.y - seg.x) - rowLen - 0.4f - 1.0f;
                float tpos = new[] { 0f, 0.5f, 1f }[arng.Next(3)];
                bool hoodAtEnd = arng.NextDouble() < 0.5;
                float start = seg.x + 0.2f + (hoodAtEnd ? 0f : 1.0f) + slack * tpos, end = start + rowLen;
                float yb = roofY + rail;
                int made = 0;
                for (int r = 0; r < rowsN && made < nUnits; r++)
                {
                    int inRow = Mathf.Min(perRow, nUnits - made);
                    float rowEnd = start + inRow * uw + (inRow - 1) * gap;
                    AddBox(m.utilSteel, P(r, start - 0.2f, roofY, 0.06f), P(r, rowEnd + 0.2f, yb, 0.16f));            // 받침 레일 두 줄
                    AddBox(m.utilSteel, P(r, start - 0.2f, roofY, ud - 0.16f), P(r, rowEnd + 0.2f, yb, ud - 0.06f));
                    for (int i = 0; i < inRow; i++)
                    {
                        float ua = start + i * (uw + gap);
                        AddBox(m.utilCasing, P(r, ua, yb, 0f), P(r, ua + uw, yb + uh, ud));
                        // 열교환기 그릴: 뒤 전부 · 앞 위쪽 · 줄 끝 옆 (네 면 열교환기형) — 앞 아래는 서비스 판(이음 줄 · 이름판)
                        AddBox(m.utilGrille, P(r, ua + 0.04f, yb + 0.2f, -0.008f), P(r, ua + uw - 0.04f, yb + uh - 0.22f, 0f));
                        AddBox(m.utilGrille, P(r, ua + 0.04f, yb + uh * 0.45f, ud), P(r, ua + uw - 0.04f, yb + uh - 0.22f, ud + 0.008f));
                        if (i == 0)
                            AddBox(m.utilGrille, P(r, ua - 0.008f, yb + 0.2f, 0.04f), P(r, ua, yb + uh - 0.22f, ud - 0.06f));
                        if (i == inRow - 1)
                            AddBox(m.utilGrille, P(r, ua + uw, yb + 0.2f, 0.04f), P(r, ua + uw + 0.008f, yb + uh - 0.22f, ud - 0.06f));
                        AddBox(m.utilSeam, P(r, ua + 0.02f, yb + 0.4f, ud), P(r, ua + uw - 0.02f, yb + 0.412f, ud + 0.004f));
                        AddBox(m.utilSeam, P(r, ua + uw * 0.5f - 0.1f, yb + 0.55f, ud), P(r, ua + uw * 0.5f + 0.1f, yb + 0.6f, ud + 0.006f));
                        // 윗면 팬 보호망 (2팬 · 1팬)
                        if (bigU)
                        {
                            AddQuad(m.utilFan, P(r, ua + uw * 0.25f, yb + uh + 0.003f, ud * 0.5f), Vector3.right, Vector3.forward, 0.3f, Vector3.up);
                            AddQuad(m.utilFan, P(r, ua + uw * 0.75f, yb + uh + 0.003f, ud * 0.5f), Vector3.right, Vector3.forward, 0.3f, Vector3.up);
                        }
                        else
                        {
                            AddQuad(m.utilFan, P(r, ua + uw * 0.5f, yb + uh + 0.003f, ud * 0.5f), Vector3.right, Vector3.forward, 0.34f, Vector3.up);
                        }
                        AddBox(m.utilInsulation, P(r, ua + uw * 0.5f - 0.07f, roofY + 0.03f, ud), P(r, ua + uw * 0.5f + 0.07f, roofY + 0.13f, ud + 0.35f));   // 배관(보온재)
                        made++;
                    }
                    // 배관 덕트 (줄 앞) + 한쪽 끝 배관 후드(지붕 속으로)
                    AddBox(m.utilSteel, P(r, start - 0.1f, roofY, ud + 0.35f), P(r, rowEnd + 0.1f, roofY + 0.13f, ud + 0.65f));
                    float h0 = hoodAtEnd ? rowEnd + 0.35f : start - 0.85f;
                    if (hoodAtEnd)
                        AddBox(m.utilSteel, P(r, rowEnd + 0.1f, roofY, ud + 0.35f), P(r, h0, roofY + 0.13f, ud + 0.65f));
                    else
                        AddBox(m.utilSteel, P(r, h0 + 0.5f, roofY, ud + 0.35f), P(r, start - 0.1f, roofY + 0.13f, ud + 0.65f));
                    AddBox(m.utilSteel, P(r, h0, roofY, ud + 0.25f), P(r, h0 + 0.5f, roofY + 0.45f, ud + 0.75f));
                    AddBox(m.utilSteel, P(r, h0 - 0.05f, roofY + 0.45f, ud + 0.2f), P(r, h0 + 0.55f, roofY + 0.5f, ud + 0.8f));
                }
                s_UtilAc += made;
                s_UtilAcRoofs++;
                Vector3 pa = P(0, start - 1.1f, roofY, -0.1f), pb = P(rowsN - 1, end + 1.1f, roofY, ud + 0.85f);
                occ.Add(Rect.MinMaxRect(Mathf.Min(pa.x, pb.x), Mathf.Min(pa.z, pb.z), Mathf.Max(pa.x, pb.x), Mathf.Max(pa.z, pb.z)));
            }

            // ---- 물탱크
            var rngT = new System.Random(hash ^ 0x51ED270B);
            if (yard)
            {
                // 1 m 판 조립 사각 탱크 — 판 이음이 탱크 모서리에 맞게 자리를 1 m 에 맞추고, UV 원점을 탱크 모서리로
                float tw = cer ? 4f : 3f, tdp = cer ? 3f : 2f, th = cer ? 2.5f : 2f;
                Rect? rc = Place(tw + 0.6f, tdp + 0.8f, rngT);
                if (rc.HasValue)
                {
                    float x0 = Mathf.Round(rc.Value.x + 0.3f), z0 = Mathf.Round(rc.Value.y + 0.5f);
                    foreach (float bxq in new[] { x0 + 0.25f, x0 + tw * 0.5f, x0 + tw - 0.25f })
                        AddBox(m.utilBeam, new Vector3(bxq - 0.15f, roofY, z0 - 0.1f), new Vector3(bxq + 0.15f, roofY + 0.3f, z0 + tdp + 0.1f));
                    float tb0 = roofY + 0.3f;
                    AddBox(m.utilPanelTank, new Vector3(x0, tb0, z0), new Vector3(x0 + tw, tb0 + th, z0 + tdp), new Vector3(x0, tb0, z0));
                    AddBox(m.utilSteel, new Vector3(x0 + 0.3f, tb0 + th, z0 + 0.3f), new Vector3(x0 + 0.9f, tb0 + th + 0.06f, z0 + 0.9f));   // 맨홀
                    // 사다리 (앞 = -z 쪽)
                    float lx = x0 + tw - 0.6f;
                    AddBox(m.utilRod, new Vector3(lx - 0.24f, roofY, z0 - 0.3f), new Vector3(lx - 0.2f, tb0 + th + 0.9f, z0 - 0.26f));
                    AddBox(m.utilRod, new Vector3(lx + 0.2f, roofY, z0 - 0.3f), new Vector3(lx + 0.24f, tb0 + th + 0.9f, z0 - 0.26f));
                    for (float ry = roofY + 0.3f; ry < tb0 + th + 0.8f; ry += 0.3f)
                        AddBox(m.utilRod, new Vector3(lx - 0.2f, ry, z0 - 0.29f), new Vector3(lx + 0.2f, ry + 0.025f, z0 - 0.265f));
                    AddBox(m.utilRod, new Vector3(lx - 0.24f, tb0 + th - 0.1f, z0 - 0.26f), new Vector3(lx - 0.2f, tb0 + th - 0.06f, z0));
                    AddBox(m.utilRod, new Vector3(lx + 0.2f, tb0 + th - 0.1f, z0 - 0.26f), new Vector3(lx + 0.24f, tb0 + th - 0.06f, z0));
                    // 통기관 · 나가는 관
                    AddPrism(m.utilSteel, new Vector3(x0 + tw - 0.4f, tb0 + th, z0 + tdp - 0.4f), new Vector3(x0 + tw - 0.4f, tb0 + th + 0.45f, z0 + tdp - 0.4f), 0.05f, 8, true);
                    AddPrism(m.utilSteel, new Vector3(x0 + 0.5f, tb0 + 0.25f, z0 + tdp), new Vector3(x0 + 0.5f, tb0 + 0.25f, z0 + tdp + 0.35f), 0.05f, 8, false);
                    AddPrism(m.utilSteel, new Vector3(x0 + 0.5f, tb0 + 0.25f, z0 + tdp + 0.35f), new Vector3(x0 + 0.5f, roofY + 0.05f, z0 + tdp + 0.35f), 0.05f, 8, false);
                    occ.Add(Rect.MinMaxRect(x0 - 0.3f, z0 - 0.4f, x0 + tw + 0.3f, z0 + tdp + 0.5f));
                    s_UtilTanks++;
                }
            }
            else if (rngT.NextDouble() < 0.6)
            {
                // FRP 원통 탱크 + 철 받침 (다리 넷 · 위아래 틀 · 받침판)
                float R = 0.55f + 0.25f * (float)rngT.NextDouble(), Ht = 1.3f + 0.6f * (float)rngT.NextDouble(), Hs = 0.55f + 0.5f * (float)rngT.NextDouble();
                Material frp = rngT.NextDouble() < 0.7 ? m.utilFrpBlue : m.utilFrpWhite;
                Rect? rc = Place(2f * R + 0.3f, 2f * R + 0.3f, rngT);
                if (rc.HasValue)
                {
                    float cx = rc.Value.center.x, cz = rc.Value.center.y, leg = R - 0.08f;
                    foreach (var (lxq, lzq) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
                        AddBox(m.utilSteel, new Vector3(cx + lxq * leg - 0.035f, roofY, cz + lzq * leg - 0.035f), new Vector3(cx + lxq * leg + 0.035f, roofY + Hs, cz + lzq * leg + 0.035f));
                    foreach (float fyq in new[] { roofY + 0.25f, roofY + Hs - 0.07f })
                    {
                        AddBox(m.utilSteel, new Vector3(cx - leg - 0.035f, fyq, cz - leg - 0.035f), new Vector3(cx + leg + 0.035f, fyq + 0.06f, cz - leg + 0.035f));
                        AddBox(m.utilSteel, new Vector3(cx - leg - 0.035f, fyq, cz + leg - 0.035f), new Vector3(cx + leg + 0.035f, fyq + 0.06f, cz + leg + 0.035f));
                        AddBox(m.utilSteel, new Vector3(cx - leg - 0.035f, fyq, cz - leg + 0.035f), new Vector3(cx - leg + 0.035f, fyq + 0.06f, cz + leg - 0.035f));
                        AddBox(m.utilSteel, new Vector3(cx + leg - 0.035f, fyq, cz - leg + 0.035f), new Vector3(cx + leg + 0.035f, fyq + 0.06f, cz + leg - 0.035f));
                    }
                    AddBox(m.utilSteel, new Vector3(cx - R, roofY + Hs, cz - R), new Vector3(cx + R, roofY + Hs + 0.04f, cz + R));
                    float tb = roofY + Hs + 0.04f;
                    AddPrism(frp, new Vector3(cx, tb, cz), new Vector3(cx, tb + Ht, cz), R, 20, true);
                    for (float ry = tb + 0.3f; ry < tb + Ht - 0.2f; ry += 0.38f)
                        AddPrism(frp, new Vector3(cx, ry, cz), new Vector3(cx, ry + 0.05f, cz), R + 0.018f, 20, false);   // 띠
                    // 둥근 지붕 (원뿔대 넷) + 맨홀 뚜껑
                    float dy = tb + Ht;
                    AddFrustum(frp, new Vector3(cx, dy, cz), new Vector3(cx, dy + 0.07f, cz), R, R * 0.93f, 20, 0);
                    AddFrustum(frp, new Vector3(cx, dy + 0.07f, cz), new Vector3(cx, dy + 0.19f, cz), R * 0.93f, R * 0.74f, 20, 0);
                    AddFrustum(frp, new Vector3(cx, dy + 0.19f, cz), new Vector3(cx, dy + 0.29f, cz), R * 0.74f, R * 0.45f, 20, 0);
                    AddFrustum(frp, new Vector3(cx, dy + 0.29f, cz), new Vector3(cx, dy + 0.33f, cz), R * 0.45f, R * 0.32f, 20, 1);
                    AddPrism(frp, new Vector3(cx, dy + 0.33f, cz), new Vector3(cx, dy + 0.4f, cz), R * 0.28f, 16, true);
                    // 나가는 관: 바닥 가운데에서 아래로 → 옆으로
                    AddPrism(m.utilSteel, new Vector3(cx, tb, cz), new Vector3(cx, roofY + 0.12f, cz), 0.035f, 8, false);
                    AddPrism(m.utilSteel, new Vector3(cx, roofY + 0.12f, cz), new Vector3(cx + R + 0.6f, roofY + 0.12f, cz), 0.035f, 8, true);
                    occ.Add(Rect.MinMaxRect(cx - R - 0.2f, cz - R - 0.2f, cx + R + 0.8f, cz + R + 0.2f));
                    s_UtilTanks++;
                }
            }

            // ---- 안테나
            var rngA = new System.Random(hash ^ 0x2545F491);
            if (tower)
            {
                // 기지국 기둥 — 받침 틀 · 기둥 · 판 안테나 4(팔 둘씩) · 마이크로웨이브 접시 · 케이블 판
                Rect? rc = Place(1.6f, 1.6f, rngA);
                if (rc.HasValue)
                {
                    float mx = rc.Value.center.x, mz = rc.Value.center.y;
                    AddBox(m.utilSteel, new Vector3(mx - 0.7f, roofY, mz - 0.7f), new Vector3(mx + 0.7f, roofY + 0.14f, mz - 0.56f));
                    AddBox(m.utilSteel, new Vector3(mx - 0.7f, roofY, mz + 0.56f), new Vector3(mx + 0.7f, roofY + 0.14f, mz + 0.7f));
                    AddBox(m.utilSteel, new Vector3(mx - 0.7f, roofY, mz - 0.56f), new Vector3(mx - 0.56f, roofY + 0.14f, mz + 0.56f));
                    AddBox(m.utilSteel, new Vector3(mx + 0.56f, roofY, mz - 0.56f), new Vector3(mx + 0.7f, roofY + 0.14f, mz + 0.56f));
                    AddBox(m.utilSteel, new Vector3(mx - 0.56f, roofY, mz - 0.06f), new Vector3(mx + 0.56f, roofY + 0.14f, mz + 0.06f));
                    AddPrism(m.utilSteel, new Vector3(mx, roofY + 0.14f, mz), new Vector3(mx, roofY + 5.2f, mz), 0.08f, 12, true);
                    foreach (Vector3 dq in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                    {
                        Vector3 pcq = new Vector3(mx, 0f, mz) + dq * 0.48f;
                        float hx = Mathf.Abs(dq.x) > 0.5f ? 0.035f : 0.15f, hz = Mathf.Abs(dq.z) > 0.5f ? 0.035f : 0.15f;
                        AddBox(m.utilAntenna, new Vector3(pcq.x - hx, roofY + 3.6f, pcq.z - hz), new Vector3(pcq.x + hx, roofY + 4.95f, pcq.z + hz));
                        foreach (float ay in new[] { roofY + 3.85f, roofY + 4.7f })
                            AddBox(m.utilRod, new Vector3(Mathf.Min(mx, pcq.x) - 0.02f, ay, Mathf.Min(mz, pcq.z) - 0.02f), new Vector3(Mathf.Max(mx, pcq.x) + 0.02f, ay + 0.04f, Mathf.Max(mz, pcq.z) + 0.02f));
                    }
                    AddPrism(m.utilAntenna, new Vector3(mx + 0.12f, roofY + 2.7f, mz), new Vector3(mx + 0.42f, roofY + 2.7f, mz), 0.32f, 16, true);
                    AddBox(m.utilMetalDark, new Vector3(mx - 0.11f, roofY + 0.14f, mz + 0.08f), new Vector3(mx - 0.05f, roofY + 4.9f, mz + 0.11f));
                    occ.Add(rc.Value);
                    s_UtilMasts++;
                }
            }
            else if (rngA.NextDouble() < 0.55)
            {
                // TV 안테나 — 옥탑 지붕 뒤 모서리(60 %) 또는 옥상 빈자리 받침 위. 기둥 + 위아래 야기 안테나 둘
                Vector3 baseP = default;
                bool okA = false;
                if (site.hasPent && rngA.NextDouble() < 0.6)
                {
                    baseP = new Vector3(site.pent.xMax - 0.35f, roofY + site.pentH, site.pent.yMax - 0.35f);
                    okA = true;
                }
                else
                {
                    Rect? rc = Place(0.8f, 0.8f, rngA);
                    if (rc.HasValue)
                    {
                        baseP = new Vector3(rc.Value.center.x, roofY, rc.Value.center.y);
                        AddBox(m.utilBeam, baseP + new Vector3(-0.2f, 0f, -0.2f), baseP + new Vector3(0.2f, 0.12f, 0.2f));
                        baseP.y += 0.12f;
                        occ.Add(rc.Value);
                        okA = true;
                    }
                }
                if (okA)
                {
                    float yTop = baseP.y + 2.4f + 0.6f * (float)rngA.NextDouble();
                    AddPrism(m.utilRod, baseP, new Vector3(baseP.x, yTop, baseP.z), 0.025f, 8, true);
                    foreach (float yy in new[] { yTop - 0.12f, yTop - 0.7f })
                    {
                        float scl = yy < yTop - 0.3f ? 0.75f : 1f;
                        AddBox(m.utilRod, new Vector3(baseP.x - 0.75f * scl, yy, baseP.z - 0.012f), new Vector3(baseP.x + 0.55f * scl, yy + 0.024f, baseP.z + 0.012f));
                        for (int el = 0; el < 8; el++)
                        {
                            float ex = baseP.x + (-0.7f + el * 0.16f) * scl, elen = (0.95f - el * 0.06f) * scl;
                            AddBox(m.utilRod, new Vector3(ex - 0.008f, yy + 0.024f, baseP.z - elen * 0.5f), new Vector3(ex + 0.008f, yy + 0.04f, baseP.z + elen * 0.5f));
                        }
                    }
                    s_UtilTv++;
                }
            }

            // ================================================================ 벽 (청운백화점 빼고)
            if (!cer)
            {
                // ---- 배수관: 뒤 모서리 쪽(뒷면 양 끝 · 옆면 뒤 끝) 1 ~ 2줄, 같은 모서리 둘은 안 둔다
                var dps = new List<(int sd, float u)>();
                var rngD = new System.Random(hash ^ 0x1B873593);
                var dc = new List<(int sd, float u, int corner)> { (1, 0.35f, 0), (1, W - 0.35f, 1), (2, D - 0.35f, 0), (3, D - 0.35f, 1) };
                for (int i = dc.Count - 1; i > 0; i--)
                {
                    int j = rngD.Next(i + 1);
                    (dc[i], dc[j]) = (dc[j], dc[i]);
                }
                int want = W * D > 250f ? 2 : 1;
                var usedCorner = new HashSet<int>();
                Material dpMat = site.metalPipe ? m.utilMetalDark : rngD.NextDouble() < 0.5 ? m.utilPvcGrey : m.utilPvcWhite;
                foreach (var dcq in dc)
                {
                    if (dps.Count >= want)
                        break;
                    if (usedCorner.Contains(dcq.corner) || Blocked(dcq.sd, dcq.u - 0.15f, dcq.u + 0.15f, 0f, roofY + 0.1f, 0.05f))
                        continue;
                    usedCorner.Add(dcq.corner);
                    dps.Add((dcq.sd, dcq.u));
                    int sdp = dcq.sd;
                    float up = dcq.u;
                    const float dpd = 0.10f, dpr = 0.05f;   // 벽에서 관 가운데 · 관 반지름 (층 띠 4 cm 보다 바깥)
                    AddPrism(dpMat, FP(sdp, up, 0f, dpd), FP(sdp, up, roofY - 0.25f, dpd), dpr, 8, false);
                    FBox(dpMat, sdp, up - 0.14f, up + 0.14f, roofY - 0.25f, roofY + 0.05f, 0f, 0.24f);     // 집수기
                    FBox(dpMat, sdp, up - 0.16f, up + 0.16f, roofY + 0.05f, roofY + 0.08f, 0f, 0.26f);
                    for (int fl = 1; fl < floors; fl++)
                        AddPrism(dpMat, FP(sdp, up, k_Curb + fl * k_FloorHeight + 0.25f, dpd), FP(sdp, up, k_Curb + fl * k_FloorHeight + 0.33f, dpd), dpr + 0.008f, 8, true);   // 이음
                    for (int fl = 0; fl < floors; fl++)
                        FBox(m.utilBracket, sdp, up - 0.07f, up + 0.07f, k_Curb + fl * k_FloorHeight + 1.7f, k_Curb + fl * k_FloorHeight + 1.73f, 0f, dpd + dpr + 0.012f);   // 고정쇠
                    s_UtilDownpipes++;
                }

                // ---- 노란 가스관 (8층 이하): 옆면 앞 모서리 — 세로관 + 층마다 계량기함(모서리 쪽), 1층 출입구 위 가로관 → 가게 계량기
                var rngG = new System.Random(hash ^ 0x68E31DA4);
                if (floors <= k_GasMaxFloors && rngG.NextDouble() < 0.85)
                {
                    var gfs = new List<int> { 2, 3 };
                    if (rngG.NextDouble() < 0.5)
                        gfs.Reverse();
                    foreach (int sdg in gfs)
                    {
                        const float ur = k_GasRiserU;
                        bool clash = false;
                        foreach (var dpq in dps)
                            clash |= dpq.sd == sdg && Mathf.Abs(dpq.u - ur) < 0.6f;
                        float yTopG = k_Curb + (floors - 1) * k_FloorHeight + 1.45f;
                        if (clash || Blocked(sdg, 0.15f, ur + 0.12f, 0f, yTopG + 0.3f, 0.05f))
                            continue;
                        AddPrism(m.utilGas, FP(sdg, ur, 0f, 0.1f), FP(sdg, ur, yTopG + 0.05f, 0.1f), 0.025f, 8, true);
                        FBox(m.utilGas, sdg, ur - 0.05f, ur + 0.05f, k_Curb + 0.9f, k_Curb + 1.02f, 0.04f, 0.17f);   // 밸브
                        for (int fl = 0; fl < floors; fl++)
                        {
                            float my = k_Curb + fl * k_FloorHeight + 1.25f;
                            FBox(m.utilMeter, sdg, ur - 0.44f, ur - 0.1f, my, my + 0.4f, 0.03f, 0.19f);
                            AddPrism(m.utilGas, FP(sdg, ur, my + 0.2f, 0.1f), FP(sdg, ur - 0.1f, my + 0.2f, 0.1f), 0.02f, 8, false);
                            FBox(m.utilBracket, sdg, ur - 0.04f, ur + 0.04f, k_Curb + fl * k_FloorHeight + 2.6f, k_Curb + fl * k_FloorHeight + 2.63f, 0f, 0.13f);
                        }
                        float yRun = k_Curb + 2.6f;   // 1층 출입구 · 창(2.4 m)보다 위, 2층 바닥 띠보다 아래
                        foreach (float runL in new[] { 3.5f, 2.5f, 4.5f, 2.0f })
                        {
                            float ue = ur + runL;
                            if (ue > Len(sdg) - 1.0f || Blocked(sdg, ur, ue + 0.25f, yRun - 0.06f, yRun + 0.06f, 0.02f) || Blocked(sdg, ue - 0.25f, ue + 0.25f, k_Curb + 1.4f, yRun, 0.05f))
                                continue;
                            AddPrism(m.utilGas, FP(sdg, ur, yRun, 0.1f), FP(sdg, ue, yRun, 0.1f), 0.025f, 8, true);
                            AddPrism(m.utilGas, FP(sdg, ue, yRun, 0.1f), FP(sdg, ue, k_Curb + 1.95f, 0.1f), 0.02f, 8, false);
                            FBox(m.utilMeter, sdg, ue - 0.17f, ue + 0.17f, k_Curb + 1.55f, k_Curb + 1.95f, 0.03f, 0.19f);
                            for (float uq = ur + 1.2f; uq < ue - 0.3f; uq += 1.2f)
                                FBox(m.utilBracket, sdg, uq - 0.02f, uq + 0.02f, yRun - 0.04f, yRun + 0.04f, 0f, 0.13f);
                            break;
                        }
                        s_UtilGas++;
                        break;
                    }
                }

                // ---- 벽걸이 실외기 (2층 이상만, 골목 쪽 벽에만): 들어갈 수 있는 층은 창 아래, 막힌 층은 창 띠 아래. 사무용 건물은 적게.
                //      골목 쪽 벽 = 벽을 따라 여섯 곳에서 밖으로 0.5 m 씩 12 m 까지 나가며 옆 건물 · 샛길을 먼저 만나는 곳이 차도를 먼저 만나는 곳 ·
                //      아무것도 안 만나는 곳보다 적지 않은 벽
                bool FacesAlley(int sd)
                {
                    int nAlley = 0, nStreet = 0, nOpen = 0;
                    for (int k = 0; k < 6; k++)
                    {
                        float u = Len(sd) * (k + 0.5f) / 6f;
                        int hit = 0;   // 0 공터 · 1 골목 · 2 길
                        for (float dd = 0.5f; dd <= 12f && hit == 0; dd += 0.5f)
                        {
                            Vector3 wp = root.TransformPoint(FP(sd, u, 0f, dd));
                            foreach (Transform b in s_ExposureBuildings)
                            {
                                if (b != site.layout && InLayoutBox(b, wp))
                                {
                                    hit = 1;
                                    break;
                                }
                            }
                            if (hit != 0)
                                break;
                            GreyboxGroundSpec.Kind? kind = GroundKindAt(wp);
                            if (kind == GreyboxGroundSpec.Kind.Alley)
                                hit = 1;
                            else if (kind == GreyboxGroundSpec.Kind.Road)
                                hit = 2;
                        }
                        if (hit == 1)
                            nAlley++;
                        else if (hit == 2)
                            nStreet++;
                        else
                            nOpen++;
                    }
                    return nAlley >= nStreet && nAlley >= nOpen;
                }
                bool[] alley = { FacesAlley(0), FacesAlley(1), FacesAlley(2), FacesAlley(3) };
                foreach (bool a in alley)
                {
                    s_UtilWalls++;
                    if (a)
                        s_UtilAlleyWalls++;
                }
                var rngW = new System.Random(hash ^ 0x7FEB352D);
                float pWin = site.office ? 0.12f : 0.35f, pClosed = site.office ? 0.12f : 0.45f;
                var placedW = new List<(int sd, int f, float u)>();
                void WallAc(int sdw, float uc, float fy)
                {
                    float ub = fy + 0.15f, ut = ub + 0.55f, d0 = 0.06f, d1 = 0.36f, u0 = uc - 0.4f, u1 = uc + 0.4f;
                    FBox(m.utilCasing, sdw, u0, u1, ub, ut, d0, d1);
                    AddQuad(m.utilFan, FP(sdw, uc - 0.1f, ub + 0.275f, d1 + 0.003f), AlongDir(sdw), Vector3.up, 0.22f, OutDir(sdw));   // 앞 팬
                    FBox(m.utilSeam, sdw, uc + 0.24f, uc + 0.36f, ub + 0.08f, ub + 0.2f, d1, d1 + 0.004f);                            // 밸브 덮개
                    FBox(m.utilGrille, sdw, u0 - 0.006f, u0, ub + 0.05f, ut - 0.05f, d0 + 0.03f, d1 - 0.04f);                       // 옆 열교환기
                    foreach (float uaq in new[] { u0 + 0.1f, u1 - 0.1f })
                    {
                        FBox(m.utilBracket, sdw, uaq - 0.02f, uaq + 0.02f, ub - 0.04f, ub, 0f, d1 + 0.02f);      // 받침 가로 팔
                        FBox(m.utilBracket, sdw, uaq - 0.02f, uaq + 0.02f, ub - 0.42f, ub - 0.04f, 0f, 0.04f);   // 벽 세로
                    }
                    FBox(m.utilTape, sdw, u1, u1 + 0.12f, ub + 0.1f, ub + 0.16f, 0.15f, 0.21f);   // 배관 테이프 → 벽 속으로
                    FBox(m.utilTape, sdw, u1 + 0.06f, u1 + 0.12f, ub + 0.1f, ub + 0.16f, 0f, 0.15f);
                }
                foreach (int sdw in new[] { 1, 2, 3, 0 })
                {
                    float lenW = Len(sdw);
                    if (!alley[sdw])
                        continue;
                    for (int fl = 1; fl < floors && placedW.Count < k_WallAcMax; fl++)
                    {
                        float fy = k_Curb + fl * k_FloorHeight;
                        bool closedF = fl >= site.enterable;
                        var cands = new List<float>();
                        if (!closedF)
                        {
                            foreach (var hq in site.holes)
                            {
                                if (hq.sd == sdw && hq.y0 > fy + 0.5f && hq.y0 < fy + 1.3f && rngW.NextDouble() < pWin)
                                    cands.Add((hq.u0 + hq.u1) * 0.5f);
                            }
                        }
                        else if (lenW > 3.2f)
                        {
                            if (rngW.NextDouble() < pClosed)
                                cands.Add(1.3f + (float)rngW.NextDouble() * (lenW - 2.6f));
                            if (rngW.NextDouble() < pClosed * 0.4f)
                                cands.Add(1.3f + (float)rngW.NextDouble() * (lenW - 2.6f));
                        }
                        foreach (float uc in cands)
                        {
                            if (uc - 0.5f < 0.9f || uc + 0.65f > lenW - 0.9f || Blocked(sdw, uc - 0.5f, uc + 0.65f, fy + 0.05f, fy + 0.75f, 0.05f))
                                continue;
                            bool near = false;
                            foreach (var pq in placedW)
                                near |= pq.sd == sdw && pq.f == fl && Mathf.Abs(pq.u - uc) < 1.4f;
                            foreach (var dpq in dps)
                                near |= dpq.sd == sdw && Mathf.Abs(dpq.u - uc) < 1.0f;
                            if (near)
                                continue;
                            WallAc(sdw, uc, fy);
                            placedW.Add((sdw, fl, uc));
                        }
                    }
                }
                s_UtilWallAc += placedW.Count;
            }

            // ================================================================ 재질별 메시
            var thin = new HashSet<Material> { m.utilTape, m.utilPvcGrey, m.utilPvcWhite, m.utilMetalDark, m.utilGas, m.utilBracket, m.utilRod };
            var textured = new HashSet<Material> { m.utilGrille, m.utilFan, m.utilPanelTank };
            foreach (KeyValuePair<Material, UtilMesh> kv in meshes)
            {
                UtilMesh um = kv.Value;
                var mesh = new Mesh { name = root.name + "_Util_" + kv.Key.name };
                mesh.indexFormat = um.v.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.SetVertices(um.v);
                mesh.SetNormals(um.n);
                // 텍스처 없는 재질은 UV 를 안 저장한다 — 씬에 들어가는 메시가 덜 커진다
                if (textured.Contains(kv.Key))
                    mesh.SetUVs(0, um.uv);
                mesh.SetTriangles(um.t, 0);
                mesh.RecalculateBounds();
                var go = new GameObject("Util_" + kv.Key.name);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = kv.Key;
                if (thin.Contains(kv.Key))
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
                s_UtilVerts += um.v.Count;
            }
        }

        // ---------------------------------------------------------------- 재질

        static void UtilityMaterials(Materials m, Texture2D grid)
        {
            m.utilCasing = FacadeMat("GB_Util_Casing", new Color(206 / 255f, 206 / 255f, 198 / 255f), 0.45f, 0.25f);   // 팬 텍스처 바깥 색과 같게
            m.utilGrille = UtilityTexMat("GB_Util_Grille", "ACGrille1", 0.2f, 0.3f, 0.5f, grid, new Color(0.2f, 0.21f, 0.22f));
            m.utilFan = UtilityTexMat("GB_Util_Fan", "ACFan1", 1f, 0.35f, 0.3f, grid, new Color(0.3f, 0.3f, 0.3f));
            m.utilSteel = FacadeMat("GB_Util_Steel", new Color(0.6f, 0.61f, 0.62f), 0.45f, 0.6f);
            m.utilSeam = FacadeMat("GB_Util_Seam", new Color(0.36f, 0.37f, 0.37f), 0.3f, 0.2f);
            m.utilInsulation = FacadeMat("GB_Util_Insulation", new Color(0.07f, 0.07f, 0.075f), 0.25f);
            m.utilTape = FacadeMat("GB_Util_Tape", new Color(0.86f, 0.84f, 0.78f), 0.3f);
            m.utilPvcGrey = FacadeMat("GB_Util_PvcGrey", new Color(0.62f, 0.63f, 0.62f), 0.35f);
            m.utilPvcWhite = FacadeMat("GB_Util_PvcWhite", new Color(0.88f, 0.88f, 0.86f), 0.35f);
            m.utilMetalDark = FacadeMat("GB_Util_MetalDark", new Color(0.2f, 0.21f, 0.22f), 0.4f, 0.5f);
            m.utilGas = FacadeMat("GB_Util_Gas", new Color(0.93f, 0.72f, 0.05f), 0.45f, 0.1f);
            m.utilMeter = FacadeMat("GB_Util_Meter", new Color(0.8f, 0.8f, 0.78f), 0.35f, 0.2f);
            m.utilBracket = FacadeMat("GB_Util_Bracket", new Color(0.42f, 0.43f, 0.44f), 0.4f, 0.6f);
            m.utilFrpBlue = FacadeMat("GB_Util_FrpBlue", new Color(0.1f, 0.3f, 0.62f), 0.55f);
            m.utilFrpWhite = FacadeMat("GB_Util_FrpWhite", new Color(0.86f, 0.87f, 0.86f), 0.5f);
            m.utilPanelTank = UtilityTexMat("GB_Util_PanelTank", "PanelTank1", 1f, 0.4f, 0f, grid, new Color(0.86f, 0.85f, 0.8f));
            m.utilBeam = FacadeMat("GB_Util_Beam", new Color(0.6f, 0.6f, 0.58f), 0.2f);
            m.utilAntenna = FacadeMat("GB_Util_Antenna", new Color(0.88f, 0.89f, 0.9f), 0.4f, 0.1f);
            m.utilRod = FacadeMat("GB_Util_Rod", new Color(0.72f, 0.73f, 0.74f), 0.6f, 0.8f);
            AssetDatabase.SaveAssets();
        }

        // 텍스처 재질 (MapMat — 한 장 meters m) — 처음 만들 때만 금속을 넣는다. 텍스처는 삼선형 · 비등방 8 (목업과 같게, 비스듬히 봐도 그릴이 뭉개지지 않게)
        static Material UtilityTexMat(string name, string folder, float meters, float smooth, float metal, Texture2D grid, Color fallback)
        {
            Texture2D tex = MapTexture($"{k_FloorTextureFolder}/{folder}", "_Color", false, false);
            if (tex != null && AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) is TextureImporter importer
                && (importer.filterMode != FilterMode.Trilinear || importer.anisoLevel != 8))
            {
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 8;
                importer.SaveAndReimport();
            }
            bool exists = AssetDatabase.LoadAssetAtPath<Material>($"{k_MaterialFolder}/{name}.mat") != null;
            Material mat = MapMat(name, folder, Vector2.one * meters, 0f, smooth, grid, fallback);
            if (exists)
                return mat;
            mat.SetFloat("_Metallic", metal);
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
