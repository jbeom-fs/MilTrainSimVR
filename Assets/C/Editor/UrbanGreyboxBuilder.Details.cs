using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MilTrainSimVR.UrbanCombat.EditorTools
{
    // 바깥벽 입체 디테일 (사용자 11/06 — 4단계 A 목업 → 옥상 출입구 · 철제 난간 · 콘크리트 파라펫 목업 → "같이 적용").
    // 바깥벽 마감(finish)을 고른 일반 건물(세라믹 외벽 빼고)에 벽 겉면에서 튀어나온 상자들을 붙인다. 합칠 때(CombineVisuals) 재질별로 묶이고,
    // 파라펫 · 옥탑 몸통만 부딪힌다 — 나머지는 보이기만 한다(길찾기 · 총알 판정은 그대로):
    //   · 층 띠 — 층 바닥 높이마다 한 바퀴. 타일 · 벽돌 · 미장 = 밝은 인조석 20 cm · 4 cm 돌출 / 밝은 화강석 = 어두운 줄눈 3 cm /
    //     패널 = 어두운 알루미늄 4 cm / 노출 콘크리트 = 없음. 가게 간판(C_Signs) 뒤에서는 끊는다
    //   · 옥상 파라펫(벽 난간) — 막힌 위층 상자를 지붕 높이에서 끊고(BuildBuilding) 그 위에 두께 0.2 m 벽: 바깥은 그 건물 마감, 옥상 쪽은 콘크리트.
    //     10층 이상 1.0 m(옥상 설비를 가리는 높은 벽) · 철제 난간을 얹는 옥상 0.3 m · 나머지 0.5 m
    //   · 덮개(파라펫 위 + 바깥 4 cm 돌출 띠) · 네 모서리 마감(타일 · 미장 · 패널) · 창턱 · 창틀 · 문틀(유리 없음) · 1층 앞면 가게 창 · 문 머리 셔터 박스
    //   · 옥탑 계단실(장식) — 10층 이상은 모두(엘리베이터 기계실 겸 크게), 나머지는 반쯤. 철문 · 문틀 · 작은 차양 · 환기창
    //   · 철제 난간 — 옥탑이 붙는 중 · 저층 옥상. 덮개 위, 바깥면에서 12 cm 안쪽, 가로대 둘, 기둥 1.8 m 간격 이내. 녹색 · 회색 · 흰색 중 하나
    // 옥탑 · 난간이 어느 건물에 붙고 크기 · 자리 · 색이 어떤지는 건물 이름으로 정한다 — 목업과 같은 씨앗 · 같은 차례라 목업 그대로이고, 다시 지어도 같다
    public static partial class UrbanGreyboxBuilder
    {
        const int k_TowerFloors = 10;          // 이 층수부터 높은 건물 — 파라펫 1 m · 큰 옥탑(엘리베이터 기계실 겸), 철제 난간 없음
        const float k_ParapetSkin = 0.02f;     // 파라펫 옥상 쪽 콘크리트 판 두께 — 뒤 면과 2 cm 떨어져 멀리서도 깜빡이지 않게
        const float k_CopeIn = 0.24f;          // 덮개가 옥상 쪽으로 덮는 폭 (파라펫 0.2 m + 콘크리트 판 + 2 cm)

        // 가게 간판 자리(월드) — 맵을 만들 때 C_Signs 에서 모은다. 층 띠를 간판 뒤에서 끊는 데 쓴다
        static readonly List<Bounds> s_SignBounds = new List<Bounds>();

        // 마감마다 층 띠(재질 · 높이 · 돌출 — 재질이 없으면 띠 없음) · 덮개 · 모서리 · 창턱 재질
        struct DetailStyle
        {
            public Material belt, cope, corner, sill;
            public float beltHeight, beltProud;
        }

        static DetailStyle DetailStyleFor(GreyboxBuildingSpec.Finish finish, Material face, Materials m)
        {
            switch (finish)
            {
                case GreyboxBuildingSpec.Finish.Brick:
                    // 벽돌은 모서리 마감 없이 — 벽돌 모서리가 그대로 보인다
                    return new DetailStyle { belt = m.extStone, beltHeight = 0.2f, beltProud = 0.04f, cope = m.extStone, sill = m.extStone };
                case GreyboxBuildingSpec.Finish.GraniteLight:
                    // 화강석 판 사이 어두운 줄눈 — 덮개 · 창턱은 같은 화강석
                    return new DetailStyle { belt = m.extGroove, beltHeight = 0.03f, beltProud = 0.002f, cope = face, sill = face };
                case GreyboxBuildingSpec.Finish.PanelSilver:
                case GreyboxBuildingSpec.Finish.PanelWhite:
                case GreyboxBuildingSpec.Finish.PanelDark:
                    return new DetailStyle { belt = m.extAluDark, beltHeight = 0.04f, beltProud = 0.003f, cope = m.extAlu, corner = m.extAlu, sill = m.extAlu };
                case GreyboxBuildingSpec.Finish.Concrete:
                    return new DetailStyle { cope = m.extConcrete, sill = m.extConcrete };
                default:
                    // 타일 · 미장
                    return new DetailStyle { belt = m.extStone, beltHeight = 0.2f, beltProud = 0.04f, cope = m.extStone, corner = m.extStone, sill = m.extStone };
            }
        }

        // 마감을 고른 건물의 바깥 겉면 재질 — 마감마다 3벌 중 건물 이름으로 하나. 마감 없음(Grid) · 세라믹 외벽은 null
        static Material FaceMaterialFor(GreyboxBuildingSpec spec, Materials m)
        {
            if (spec.finish == GreyboxBuildingSpec.Finish.Grid || spec.facade == GreyboxBuildingSpec.Facade.Ceramic)
                return null;
            if (m.finishes == null || !m.finishes.TryGetValue(spec.finish, out Material[] variants))
                return null;
            return variants[(StableHash(spec.name) & 0x7fffffff) % variants.Length];
        }

        static void CollectSignBounds(Scene scene)
        {
            s_SignBounds.Clear();
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.name != "C_Signs")
                    continue;
                foreach (Transform sign in go.transform)
                {
                    bool any = false;
                    var b = new Bounds();
                    foreach (Renderer r in sign.GetComponentsInChildren<Renderer>())
                    {
                        if (any)
                            b.Encapsulate(r.bounds);
                        else
                            b = r.bounds;
                        any = true;
                    }
                    if (any)
                        s_SignBounds.Add(b);
                }
            }
        }

        // 이 건물 벽에 납작하게 붙은 간판 자리 (면, u0, u1, y0, y1) — 둘레 5 cm · 위아래 2 cm 여유. 면 번호 · u 는 FacadeSides 와 같다
        static List<(int side, float u0, float u1, float y0, float y1)> SignCuts(Transform root, float W, float D)
        {
            var cuts = new List<(int side, float u0, float u1, float y0, float y1)>();
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
                int sd = hi.z <= 0.05f && lo.z > -0.3f && lo.x < W && hi.x > 0f ? 0
                    : lo.z >= D - 0.05f && hi.z < D + 0.3f && lo.x < W && hi.x > 0f ? 1
                    : hi.x <= 0.05f && lo.x > -0.3f && lo.z < D && hi.z > 0f ? 2
                    : lo.x >= W - 0.05f && hi.x < W + 0.3f && lo.z < D && hi.z > 0f ? 3
                    : -1;
                if (sd >= 0)
                    cuts.Add((sd, (sd < 2 ? lo.x : lo.z) - 0.05f, (sd < 2 ? hi.x : hi.z) + 0.05f, lo.y - 0.02f, hi.y + 0.02f));
            }
            return cuts;
        }

        // top = 맨 위층 천장 높이(지붕 윗면). massRoof = 막힌 위층 상자가 지붕까지(파라펫을 여기서 세운다) — 아니면 RoofWithParapet 의 0.6 m 벽이 있다.
        // openings = BuildInterior · BuildMall 이 돌려준 층마다 · 면마다 바깥벽 구멍 (u0, u1, v0, v1) — u 는 벽 좌표(옆벽은 앞벽 두께만큼 밀림), v 는 그 층 바닥에서
        static void BuildExteriorDetails(Transform root, GreyboxBuildingSpec spec, Material face, int floors, float W, float D, float top, bool massRoof,
            List<Vector4>[][] openings, Materials m)
        {
            DetailStyle st = DetailStyleFor(spec.finish, face, m);
            FacadeSide[] sides = FacadeSides(W, D);
            int hash = StableHash(spec.name) & 0x7fffffff;
            bool tower = floors >= k_TowerFloors;
            bool railed = !tower && hash % 2 == 0;

            // ---- 층 띠 (간판 뒤는 끊는다)
            if (st.belt != null)
            {
                var cuts = SignCuts(root, W, D);
                for (int f = 1; f < floors; f++)
                {
                    float yc = k_Curb + f * k_FloorHeight;
                    // 굵은 인조석 띠는 층 바닥 15 cm 아래부터, 얇은 줄눈 · 알루미늄 띠는 층 바닥 높이가 가운데
                    float y0 = st.beltHeight < 0.1f ? yc - st.beltHeight * 0.5f : yc - 0.15f, y1 = y0 + st.beltHeight;
                    for (int sd = 0; sd < 4; sd++)
                    {
                        var segs = new List<Vector2> { sd < 2 ? new Vector2(-st.beltProud, W + st.beltProud) : new Vector2(0f, D) };
                        foreach (var c in cuts)
                        {
                            if (c.side != sd || c.y1 < y0 || c.y0 > y1)
                                continue;
                            var next = new List<Vector2>();
                            foreach (Vector2 g in segs)
                            {
                                if (c.u1 <= g.x || c.u0 >= g.y)
                                {
                                    next.Add(g);
                                    continue;
                                }
                                if (c.u0 > g.x)
                                    next.Add(new Vector2(g.x, c.u0));
                                if (c.u1 < g.y)
                                    next.Add(new Vector2(c.u1, g.y));
                            }
                            segs = next;
                        }
                        foreach (Vector2 g in segs)
                            DetailSide("Belt", root, sides[sd], g.x, g.y, y0, y1, 0f, st.beltProud, st.belt);
                    }
                }
            }

            // ---- 옥상 파라펫 — 바깥은 그 건물 마감, 옥상 쪽은 콘크리트 판. 옆벽 것은 앞 · 뒤 벽 사이에만(모서리가 겹치면 같은 면이 두 번 그려진다)
            float parH = !massRoof ? k_Parapet : tower ? 1.0f : railed ? 0.3f : 0.5f;
            if (massRoof)
            {
                float py = top + parH;
                const float skin = k_ParapetSkin;
                DetailSide("Parapet", root, sides[0], 0f, W, top, py, -k_Wall, 0f, face, true);
                DetailSide("Parapet", root, sides[1], 0f, W, top, py, -k_Wall, 0f, face, true);
                DetailSide("Parapet", root, sides[2], k_Wall, D - k_Wall, top, py, -k_Wall, 0f, face, true);
                DetailSide("Parapet", root, sides[3], k_Wall, D - k_Wall, top, py, -k_Wall, 0f, face, true);
                DetailSide("ParapetSkin", root, sides[0], k_Wall, W - k_Wall, top, py, -k_Wall - skin, -k_Wall, m.extConcrete);
                DetailSide("ParapetSkin", root, sides[1], k_Wall, W - k_Wall, top, py, -k_Wall - skin, -k_Wall, m.extConcrete);
                DetailSide("ParapetSkin", root, sides[2], k_Wall + skin, D - k_Wall - skin, top, py, -k_Wall - skin, -k_Wall, m.extConcrete);
                DetailSide("ParapetSkin", root, sides[3], k_Wall + skin, D - k_Wall - skin, top, py, -k_Wall - skin, -k_Wall, m.extConcrete);
            }
            float copeY = top + parH;

            // ---- 덮개: 바깥 4 cm 돌출 띠(덮개 윗면 8 cm 아래부터) + 파라펫 위를 덮는 판
            if (st.cope != null)
            {
                for (int sd = 0; sd < 4; sd++)
                {
                    float a0 = sd < 2 ? -0.04f : 0f, a1 = sd < 2 ? W + 0.04f : D;
                    DetailSide("Coping", root, sides[sd], a0, a1, copeY - 0.08f, copeY + 0.025f, 0f, 0.04f, st.cope);
                    float c0 = sd < 2 ? 0f : k_CopeIn, c1 = sd < 2 ? W : D - k_CopeIn;
                    DetailSide("Coping", root, sides[sd], c0, c1, copeY, copeY + 0.025f, -k_CopeIn, 0f, st.cope);
                }
            }

            // ---- 모서리 마감 (네 모서리, 양쪽 면에 폭 8 cm · 두께 1.2 cm — 인도 높이부터 덮개 띠 밑까지)
            if (st.corner != null)
            {
                const float t = 0.012f, w = 0.08f;
                float ya = k_Curb, yb = copeY - 0.08f;
                DetailSide("Corner", root, sides[0], -t, w, ya, yb, 0f, t, st.corner);
                DetailSide("Corner", root, sides[2], 0f, w, ya, yb, 0f, t, st.corner);
                DetailSide("Corner", root, sides[0], W - w, W + t, ya, yb, 0f, t, st.corner);
                DetailSide("Corner", root, sides[3], 0f, w, ya, yb, 0f, t, st.corner);
                DetailSide("Corner", root, sides[1], -t, w, ya, yb, 0f, t, st.corner);
                DetailSide("Corner", root, sides[2], D - w, D, ya, yb, 0f, t, st.corner);
                DetailSide("Corner", root, sides[1], W - w, W + t, ya, yb, 0f, t, st.corner);
                DetailSide("Corner", root, sides[3], D - w, D, ya, yb, 0f, t, st.corner);
            }

            // ---- 창턱 · 창틀 · 문틀(유리 없음) · 셔터 박스 — 들어갈 수 있는 층의 바깥벽 구멍마다. 바닥부터 뚫린 구멍(v0 = 0)은 문
            if (openings != null)
            {
                const float fw = 0.045f, fd0 = -0.11f, fd1 = -0.06f;   // 창틀 폭 · 겉면에서 들어간 깊이
                for (int f = 0; f < openings.Length; f++)
                {
                    if (openings[f] == null)
                        continue;
                    float fy = k_Curb + f * k_FloorHeight;
                    for (int sd = 0; sd < 4; sd++)
                    {
                        float off = sd >= 2 ? k_Wall : 0f;
                        FacadeSide side = sides[sd];
                        foreach (Vector4 h in openings[f][sd])
                        {
                            float u0 = h.x + off, u1 = h.y + off, y0 = fy + h.z, y1 = fy + h.w;
                            bool door = h.z < 0.01f;
                            if (!door)
                                DetailSide("Sill", root, side, u0 - 0.05f, u1 + 0.05f, y0 - 0.035f, y0 + 0.015f, fd1, 0.05f, st.sill);
                            DetailSide("Frame", root, side, u0, u0 + fw, y0, y1, fd0, fd1, m.extFrame);
                            DetailSide("Frame", root, side, u1 - fw, u1, y0, y1, fd0, fd1, m.extFrame);
                            DetailSide("Frame", root, side, u0, u1, y1 - fw, y1, fd0, fd1, m.extFrame);
                            if (!door)
                                DetailSide("Frame", root, side, u0, u1, y0, y0 + fw, fd0, fd1, m.extFrame);
                            // 1층 앞면 가게 창 · 정문: 머리 25 cm 안에 셔터 박스 (간판과 안 겹치게 구멍 안쪽에)
                            if (sd == 0 && f == 0 && h.w >= 2.3f)
                                DetailSide("Shutter", root, side, u0, u1, y1 - 0.25f, y1, -k_Wall, -0.02f, m.extShutter);
                        }
                    }
                }
            }

            // ---- 옥탑 계단실 (장식) — 10층 이상은 모두, 나머지는 반쯤. 크기 · 자리는 건물 이름 씨앗의 난수 (PenthouseSpot)
            if (PenthouseSpot(hash, tower, W, D, out float pw, out float pd, out float ph, out bool pentRight))
                Penthouse(root, top, W, D, pw, pd, ph, pentRight, face, st.cope != null ? st.cope : m.extStone, m);

            // ---- 옥상 철제 난간 — 덮개 위. 꼭대기가 옥상 바닥에서 약 1.1 m
            if (railed && m.extRails != null)
                Railing(root, W, D, copeY + 0.025f, Mathf.Max(0.5f, 1.1f - parH), m.extRails[hash / 7 % m.extRails.Length]);
        }

        // 옥탑 크기 · 높이 · 자리 쪽 — 10층 이상은 모두, 나머지는 이름 씨앗이 짝수인 건물. 옥상이 작아 안 들어가면 false.
        // 목업과 같은 차례로 난수를 뽑는다(가로 · 깊이 · 높이 → 오른쪽?). 설비(UrbanGreyboxBuilder.Utilities.cs)도 이걸로 옥탑을 피한다
        static bool PenthouseSpot(int hash, bool tower, float W, float D, out float pw, out float pd, out float ph, out bool right)
        {
            pw = pd = ph = 0f;
            right = false;
            if (!tower && hash % 2 != 0)
                return false;
            var rng = new System.Random(hash);
            pw = tower ? 4.2f + 0.8f * (float)rng.NextDouble() : 2.6f + 0.8f * (float)rng.NextDouble();
            pd = tower ? 5.0f + 1.0f * (float)rng.NextDouble() : 3.4f + 1.2f * (float)rng.NextDouble();
            ph = tower ? 3.2f : 2.5f + 0.3f * (float)rng.NextDouble();
            if (W < pw + 2f || D < pd + 2f)
                return false;
            right = rng.NextDouble() < 0.35;
            return true;
        }

        // 옥탑 바닥 자리 (x, z) — Penthouse 와 같은 식
        static Rect PenthouseRect(float W, float D, float pw, float pd, bool right)
        {
            float x0 = Mathf.Clamp(right ? W * 0.5f + 0.4f : W * 0.5f - pw - 0.4f, 0.9f, W - 0.9f - pw);
            float z1 = D - 0.9f;
            return Rect.MinMaxRect(x0, z1 - pd, x0 + pw, z1);
        }

        // 옥탑 계단실 — 계단이 오는 쪽(가운데 복도 왼쪽 · 뒤쪽)에, 가끔은 오른쪽. 앞(지붕 가운데 쪽)에 철문 · 문틀 · 작은 차양, 옆에 환기창
        static void Penthouse(Transform root, float y0, float W, float D, float pw, float pd, float ph, bool right, Material face, Material cope, Materials m)
        {
            float x0 = Mathf.Clamp(right ? W * 0.5f + 0.4f : W * 0.5f - pw - 0.4f, 0.9f, W - 0.9f - pw);
            float z1 = D - 0.9f, z0 = z1 - pd, y1 = y0 + ph;
            DetailBox("Penthouse", root, new Vector3(x0, y0, z0), new Vector3(x0 + pw, y1, z1), face, true);
            // 지붕 둘레 덮개 (건물 덮개와 같은 재질)
            DetailBox("PenthouseCoping", root, new Vector3(x0 - 0.04f, y1 - 0.08f, z0 - 0.04f), new Vector3(x0 + pw + 0.04f, y1 + 0.03f, z0), cope);
            DetailBox("PenthouseCoping", root, new Vector3(x0 - 0.04f, y1 - 0.08f, z1), new Vector3(x0 + pw + 0.04f, y1 + 0.03f, z1 + 0.04f), cope);
            DetailBox("PenthouseCoping", root, new Vector3(x0 - 0.04f, y1 - 0.08f, z0), new Vector3(x0, y1 + 0.03f, z1), cope);
            DetailBox("PenthouseCoping", root, new Vector3(x0 + pw, y1 - 0.08f, z0), new Vector3(x0 + pw + 0.04f, y1 + 0.03f, z1), cope);
            float dx = x0 + pw * 0.5f - 0.45f;
            DetailBox("PenthouseDoorFrame", root, new Vector3(dx - 0.06f, y0, z0 - 0.02f), new Vector3(dx + 0.96f, y0 + 2.16f, z0), m.extFrame);
            DetailBox("PenthouseDoor", root, new Vector3(dx, y0 + 0.02f, z0 - 0.035f), new Vector3(dx + 0.9f, y0 + 2.1f, z0 - 0.02f), m.extDoor);
            DetailBox("PenthouseCanopy", root, new Vector3(dx - 0.25f, y0 + 2.28f, z0 - 0.65f), new Vector3(dx + 1.15f, y0 + 2.38f, z0), m.extStone);
            float vz = z0 + pd * 0.5f;
            DetailBox("PenthouseVent", root, new Vector3(right ? x0 + pw : x0 - 0.02f, y0 + 1.5f, vz - 0.3f), new Vector3(right ? x0 + pw + 0.02f : x0, y0 + 1.95f, vz + 0.3f), m.extVent);
        }

        // 옥상 철제 난간 — 바깥면에서 12 cm 안쪽 네 변. 기둥 5 cm 각(1.8 m 간격 이내), 가로대 둘(맨 위 · 바닥에서 50 cm)
        static void Railing(Transform root, float W, float D, float y0, float h, Material mat)
        {
            const float inset = 0.12f, ps = 0.05f;
            var corners = new[] { new Vector2(inset, inset), new Vector2(W - inset, inset), new Vector2(W - inset, D - inset), new Vector2(inset, D - inset) };
            for (int k = 0; k < 4; k++)
            {
                Vector2 a = corners[k], b = corners[(k + 1) % 4];
                int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 1.8f));
                for (int i = 0; i < n; i++)
                {
                    Vector2 p = Vector2.Lerp(a, b, (float)i / n);
                    DetailBox("RailPost", root, new Vector3(p.x - ps * 0.5f, y0, p.y - ps * 0.5f), new Vector3(p.x + ps * 0.5f, y0 + h, p.y + ps * 0.5f), mat);
                }
                Vector2 lo = Vector2.Min(a, b) - Vector2.one * (ps * 0.5f), hi = Vector2.Max(a, b) + Vector2.one * (ps * 0.5f);
                DetailBox("Rail", root, new Vector3(lo.x, y0 + h - ps, lo.y), new Vector3(hi.x, y0 + h, hi.y), mat);
                if (h >= 0.7f)
                    DetailBox("Rail", root, new Vector3(lo.x, y0 + 0.5f, lo.y), new Vector3(hi.x, y0 + 0.5f + ps, hi.y), mat);
            }
        }

        // 상자 — 재질이 없거나 한 변이 1 mm 도 안 되면 안 만든다
        static void DetailBox(string name, Transform root, Vector3 a, Vector3 b, Material mat, bool collide = false)
        {
            if (mat == null)
                return;
            Vector3 lo = Vector3.Min(a, b), hi = Vector3.Max(a, b);
            if (hi.x - lo.x < 0.001f || hi.y - lo.y < 0.001f || hi.z - lo.z < 0.001f)
                return;
            Box(name, root, lo, hi, mat, collide);
        }

        // 바깥벽 한 면 기준 상자 — s 는 면을 따라, d 는 겉면에서 바깥쪽(음수 = 벽 속 · 옥상 쪽)
        static void DetailSide(string name, Transform root, FacadeSide side, float s0, float s1, float y0, float y1, float d0, float d1, Material mat,
            bool collide = false)
        {
            DetailBox(name, root, side.P(s0, y0, d0), side.P(s1, y1, d1), mat, collide);
        }
    }
}
