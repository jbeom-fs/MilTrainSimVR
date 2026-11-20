using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MilTrainSimVR.UrbanCombat.EditorTools
{
    // 청운백화점(N05) 세라믹 외벽 (사용자 11/06 — 목업 C 안 '아이보리 + 청록', 목업 렌더를 보고 정함).
    // 벽 · 콜라이더 · 길찾기는 그대로 두고 바깥에 얇은 겉면을 한 겹 입힌다 — 실내 칸막이 · 벽 안쪽 면은 원래 재질 그대로.
    //   · 1층: 진회색 화강석 판(Textures/Map/GraniteDark) + 기둥 자리의 줄눈 석재 기둥(1층 창·문과 겹치면 뺀다, 콜라이더 있음) + 청동 창틀
    //   · 2층 ~ 꼭대기: 아이보리 유약 세라믹 블록(Textures/Map/Ceramic1) + 창 테두리 · 쐐기돌(아이보리) · 창턱(청록)
    //   · 창 사이 세로 기둥 띠(아이보리, 세로 홈 다섯) + 청록 머리 — 기둥 자리는 쇼핑몰 창 간격(약 6 m)과 같은 계산(MallWindows)
    //   · 기단 위 굵은 띠 · 층마다 얇은 띠 · 꼭대기 처마 + 톱니 — 건물 둘레를 한 바퀴(모서리는 45° 맞춤), 청록
    //   · 창 없는 꼭대기 층: 칸마다 청록 장식판 + 금색 해돋이(Models/Facade/Sunburst). 앞면 가운데 두 칸을 합친 큰 판과
    //     출입구가 있는 옆면 가운데 칸에는 금색 '청운백화점'(Models/Facade/StoreName)
    //   · 간판 띠 · 막힌 층 창 띠는 붙이지 않는다 (BuildBuilding)
    // 금속 장식 모델은 소품 씌우기(UrbanGreyboxBuilder.Props.cs)로 놓는다 — 자리 상자를 모델 비율대로 만들어 늘어나지 않게
    public static partial class UrbanGreyboxBuilder
    {
        const string k_FacadeModelFolder = "Assets/C/Models/Facade";
        const float k_FacadeGraniteT = 0.05f;    // 화강석 판 두께 (벽 겉면에서)
        const float k_FacadeCeramicT = 0.03f;    // 세라믹 블록 두께
        const float k_FacadeBaseBelt = 0.35f;    // 기단 위 굵은 띠 높이 (2층 바닥에서 위로)
        const float k_FacadeDentil = 0.13f;      // 처마 밑 톱니 높이
        const float k_FacadeCapital = 0.72f;     // 기둥 머리 두 단 높이
        const float k_PilasterHalf = 0.4f;       // 기둥 띠 폭의 절반
        const float k_PilasterOut = 0.2f;        // 기둥 띠가 세라믹 겉면에서 튀어나온 만큼
        const float k_FacadePanelMargin = 0.9f;  // 기둥 가운데에서 장식판 안쪽 끝까지 (기둥 반폭 0.4 + 여백 0.36 + 테두리 0.14)

        // 세라믹 블록 텍스처 아래 끝 = 기단 위 띠 윗면 — 블록 줄이 띠에서 시작한다
        static float FacadeBlockStart => k_Curb + k_FloorHeight + k_FacadeBaseBelt;

        // 바깥벽 한 면 — 건물 좌표(앞면 왼쪽 아래가 원점, x 앞면을 따라, z 안쪽)에서 벽 겉면 위의 자리.
        // s = 벽을 따라(앞·뒤는 x, 옆은 z), d = 겉면에서 바깥쪽으로
        struct FacadeSide
        {
            public Vector3 origin, along, outward;
            public float length;

            public Vector3 P(float s, float y, float d)
            {
                return origin + along * s + outward * d + Vector3.up * y;
            }
        }

        static FacadeSide[] FacadeSides(float W, float D)
        {
            return new[]
            {
                new FacadeSide { origin = Vector3.zero, along = Vector3.right, outward = Vector3.back, length = W },                   // 0 앞 (-z)
                new FacadeSide { origin = new Vector3(0f, 0f, D), along = Vector3.right, outward = Vector3.forward, length = W },      // 1 뒤 (+z)
                new FacadeSide { origin = Vector3.zero, along = Vector3.forward, outward = Vector3.left, length = D },                 // 2 왼 (-x)
                new FacadeSide { origin = new Vector3(W, 0f, 0f), along = Vector3.forward, outward = Vector3.right, length = D },      // 3 오른 (+x)
            };
        }

        static GameObject SideBox(string name, Transform root, FacadeSide side, float s0, float s1, float y0, float y1, float d0, float d1,
            Material mat, bool collide = false)
        {
            Vector3 a = side.P(s0, y0, d0), b = side.P(s1, y1, d1);
            return Box(name, root, Vector3.Min(a, b), Vector3.Max(a, b), mat, collide);
        }

        // openings: BuildMall 이 돌려준 층마다 · 면마다 구멍 (벽 좌표) — 여기서 건물 좌표(s, 높이)로 옮겨 쓴다
        static void BuildCeramicFacade(Transform root, GreyboxBuildingSpec spec, float W, float D, int floors, int enterable,
            List<Vector4>[][] openings, Materials m)
        {
            EnsureFacadeModelMaterials(m);
            var f = new MallFrame { longIsX = W >= D, L = Mathf.Max(W, D), S = Mathf.Min(W, D) };
            bool[] door = OuterDoors(spec, root.rotation);
            FacadeSide[] sides = FacadeSides(W, D);
            float top = k_Curb + floors * k_FloorHeight;
            float base1 = k_Curb + k_FloorHeight;                  // 1층 천장 = 기단 위 띠 아래
            float blockStart = FacadeBlockStart;
            float capitalBottom = top - k_FacadeDentil - k_FacadeCapital;
            bool blindTop = floors > enterable;                     // 꼭대기 층이 막혀 있으면 장식판
            float panelY0 = k_Curb + (floors - 1) * k_FloorHeight + 0.4f, panelY1 = top - 0.5f;

            for (int w = 0; w < 4; w++)
            {
                FacadeSide side = sides[w];
                float startOffset = w >= 2 ? k_Wall : 0f;
                var holes1F = new List<Rect>();
                var upperWindows = new List<Rect>();
                for (int fl = 0; fl < enterable; fl++)
                {
                    float y = k_Curb + fl * k_FloorHeight;
                    foreach (Vector4 h in openings[fl][w])
                    {
                        var r = Rect.MinMaxRect(h.x + startOffset, y + h.z, h.y + startOffset, y + h.w);
                        if (fl == 0)
                            holes1F.Add(r);
                        else
                            upperWindows.Add(r);
                    }
                }

                // 기둥 자리: 모서리 둘 + 쇼핑몰 창 칸 경계 (창은 칸 가운데)
                List<float> pilasters = PilasterLine(f, w, side.length);
                // 앞면 가운데 기둥 양옆 두 칸은 이름판 하나로 — 그 기둥은 꼭대기 층 띠에서 멈춘다
                int nameCenter = -1;
                if (w == 0 && blindTop)
                {
                    for (int i = 1; i < pilasters.Count - 1; i++)
                    {
                        if (Mathf.Abs(pilasters[i] - side.length * 0.5f) < 0.6f)
                            nameCenter = i;
                    }
                }

                // ---- 1층 화강석 판 · 청동 창틀 · 석재 기둥
                var graniteHoles = new List<Rect>();
                foreach (Rect r in holes1F)
                    graniteHoles.Add(Rect.MinMaxRect(r.xMin - 0.08f, Mathf.Max(k_Curb, r.yMin - 0.08f), r.xMax + 0.08f, r.yMax + 0.08f));
                float e0 = w < 2 ? -k_FacadeGraniteT : 0f, e1 = w < 2 ? side.length + k_FacadeGraniteT : side.length;
                foreach (Rect p in RectMinus(Rect.MinMaxRect(e0, k_Curb, e1, base1), graniteHoles))
                    SideBox("Granite", root, side, p.xMin, p.xMax, p.yMin, p.yMax, 0f, k_FacadeGraniteT, m.facadeGranite);
                foreach (Rect r in holes1F)
                {
                    bool isDoor = r.yMin < k_Curb + 0.05f;
                    SideBox("Frame1F", root, side, r.xMin - 0.08f, r.xMin, r.yMin, r.yMax + 0.08f, k_FacadeGraniteT, k_FacadeGraniteT + 0.02f, m.facadeBronze);
                    SideBox("Frame1F", root, side, r.xMax, r.xMax + 0.08f, r.yMin, r.yMax + 0.08f, k_FacadeGraniteT, k_FacadeGraniteT + 0.02f, m.facadeBronze);
                    SideBox("Frame1F", root, side, r.xMin, r.xMax, r.yMax, r.yMax + 0.08f, k_FacadeGraniteT, k_FacadeGraniteT + 0.02f, m.facadeBronze);
                    if (!isDoor)
                        SideBox("Frame1F", root, side, r.xMin - 0.08f, r.xMax + 0.08f, r.yMin - 0.08f, r.yMin, k_FacadeGraniteT, k_FacadeGraniteT + 0.02f, m.facadeBronze);
                    Reveal(root, side, r, k_FacadeGraniteT, m.facadeBronze, !isDoor);
                }
                foreach (float s in pilasters)
                {
                    float c = Mathf.Clamp(s, 0.55f, side.length - 0.55f);
                    bool blocked = false;
                    foreach (Rect r in holes1F)
                        blocked |= c + 0.5f > r.xMin - 0.1f && c - 0.5f < r.xMax + 0.1f;
                    if (blocked)
                        continue;
                    for (float y = k_Curb; y < base1 - 0.05f; y += 0.6f)
                        SideBox("GranitePier", root, side, c - 0.5f, c + 0.5f, y, Mathf.Min(y + 0.58f, base1), 0f, 0.3f, m.facadeGranite, true);
                }

                // ---- 위층 세라믹 블록 — 창 둘레 · 창턱 · 쐐기돌 · 장식판 자리는 비운다
                var ceramicHoles = new List<Rect>();
                foreach (Rect r in upperWindows)
                {
                    float cx = r.center.x;
                    ceramicHoles.Add(Rect.MinMaxRect(r.xMin - 0.12f, r.yMin, r.xMax + 0.12f, r.yMax + 0.12f));
                    ceramicHoles.Add(Rect.MinMaxRect(r.xMin - 0.2f, r.yMin - 0.14f, r.xMax + 0.2f, r.yMin));
                    ceramicHoles.Add(Rect.MinMaxRect(cx - 0.16f, r.yMax, cx + 0.16f, r.yMax + 0.3f));
                }
                var panels = new List<(float s0, float s1, string ornament)>();
                if (blindTop)
                {
                    for (int i = 0; i + 1 < pilasters.Count; i++)
                    {
                        if (nameCenter >= 0 && (i == nameCenter - 1 || i == nameCenter))
                        {
                            if (i == nameCenter - 1)
                                panels.Add((pilasters[i] + k_FacadePanelMargin, pilasters[i + 2] - k_FacadePanelMargin, "StoreName"));
                            continue;
                        }
                        float s0 = pilasters[i] + k_FacadePanelMargin, s1 = pilasters[i + 1] - k_FacadePanelMargin;
                        if (s1 - s0 < 1.8f)
                            continue;
                        // 출입구 쪽 옆면은 가운데 칸에 이름
                        bool centerBay = pilasters[i] < side.length * 0.5f && pilasters[i + 1] > side.length * 0.5f;
                        panels.Add((s0, s1, w >= 2 && door[w] && centerBay ? "StoreName" : "Sunburst"));
                    }
                    foreach (var p in panels)
                        ceramicHoles.Add(Rect.MinMaxRect(p.s0 - 0.14f, panelY0 - 0.14f, p.s1 + 0.14f, panelY1 + 0.14f));
                }
                float c0 = w < 2 ? -k_FacadeCeramicT : 0f, c1 = w < 2 ? side.length + k_FacadeCeramicT : side.length;
                foreach (Rect p in RectMinus(Rect.MinMaxRect(c0, blockStart, c1, top - k_FacadeDentil), ceramicHoles))
                    SideBox("Ceramic", root, side, p.xMin, p.xMax, p.yMin, p.yMax, 0f, k_FacadeCeramicT, m.facadeCeramic);

                // ---- 위층 창: 테두리 · 쐐기돌(아이보리) · 창턱(청록) · 안쪽 옆면 · 청동 안쪽 틀
                float face = k_FacadeCeramicT;
                foreach (Rect r in upperWindows)
                {
                    float cx = r.center.x;
                    SideBox("Surround", root, side, r.xMin - 0.12f, r.xMin, r.yMin, r.yMax + 0.12f, face, face + 0.04f, m.facadeIvory);
                    SideBox("Surround", root, side, r.xMax, r.xMax + 0.12f, r.yMin, r.yMax + 0.12f, face, face + 0.04f, m.facadeIvory);
                    SideBox("Surround", root, side, r.xMin, r.xMax, r.yMax, r.yMax + 0.12f, face, face + 0.04f, m.facadeIvory);
                    SideBox("Keystone", root, side, cx - 0.16f, cx + 0.16f, r.yMax, r.yMax + 0.3f, face, face + 0.08f, m.facadeIvory);
                    SideBox("Sill", root, side, r.xMin - 0.2f, r.xMax + 0.2f, r.yMin - 0.14f, r.yMin, face, face + 0.1f, m.facadeTeal);
                    Reveal(root, side, r, face, m.facadeIvory, true);
                    SideBox("InnerFrame", root, side, r.xMin + 0.02f, r.xMin + 0.07f, r.yMin + 0.02f, r.yMax - 0.02f, -0.14f, -0.08f, m.facadeBronze);
                    SideBox("InnerFrame", root, side, r.xMax - 0.07f, r.xMax - 0.02f, r.yMin + 0.02f, r.yMax - 0.02f, -0.14f, -0.08f, m.facadeBronze);
                    SideBox("InnerFrame", root, side, r.xMin + 0.07f, r.xMax - 0.07f, r.yMax - 0.07f, r.yMax - 0.02f, -0.14f, -0.08f, m.facadeBronze);
                }

                // ---- 기둥 띠: 받침 · 몸(아이보리) · 세로 홈 다섯 · 두 단 머리(청록)
                float pf = face + k_PilasterOut;
                var reeds = new List<Vector3[]>();
                var reedNormals = new List<Vector3>();
                for (int i = 0; i < pilasters.Count; i++)
                {
                    float s = pilasters[i];
                    bool stopsUnderName = i == nameCenter;
                    float shaftTop = stopsUnderName ? panelY0 - 0.5f : capitalBottom;
                    SideBox("Plinth", root, side, s - 0.45f, s + 0.45f, blockStart, blockStart + 0.25f, face, pf + 0.05f, m.facadeIvory);
                    SideBox("Pilaster", root, side, s - k_PilasterHalf, s + k_PilasterHalf, blockStart + 0.25f, shaftTop, face, pf, m.facadeIvory);
                    for (int k = -2; k <= 2; k++)
                        ReedQuads(side, s + k * 0.14f, blockStart + 0.55f, shaftTop - 0.3f, pf, 0.035f, reeds, reedNormals);
                    if (stopsUnderName)
                        continue;
                    SideBox("Capital", root, side, s - 0.47f, s + 0.47f, capitalBottom, capitalBottom + 0.35f, face, pf + 0.05f, m.facadeTeal);
                    SideBox("Capital", root, side, s - 0.53f, s + 0.53f, capitalBottom + 0.35f, top - k_FacadeDentil, face, pf + 0.1f, m.facadeTeal);
                }
                QuadsMesh("Reeds", root, reeds, reedNormals, m.facadeIvory);

                // ---- 처마 밑 톱니 (기둥 머리 자리는 비운다 — 이름판 가운데 기둥은 머리가 없으니 이어서)
                for (float s = 0.6f; s < side.length - 0.65f; s += 0.2f)
                {
                    bool nearCapital = false;
                    for (int i = 0; i < pilasters.Count; i++)
                        nearCapital |= i != nameCenter && Mathf.Abs(s + 0.045f - pilasters[i]) < 0.62f;
                    if (!nearCapital)
                        SideBox("Dentil", root, side, s, s + 0.09f, top - k_FacadeDentil, top, face, face + 0.12f, m.facadeTeal);
                }

                // ---- 꼭대기 층 장식판: 청록 테두리 · 짙은 청록 판 · 금색 해돋이 또는 이름
                foreach (var p in panels)
                {
                    SideBox("PanelFrame", root, side, p.s0 - 0.14f, p.s1 + 0.14f, panelY0 - 0.14f, panelY0, face, face + 0.06f, m.facadeTeal);
                    SideBox("PanelFrame", root, side, p.s0 - 0.14f, p.s1 + 0.14f, panelY1, panelY1 + 0.14f, face, face + 0.06f, m.facadeTeal);
                    SideBox("PanelFrame", root, side, p.s0 - 0.14f, p.s0, panelY0, panelY1, face, face + 0.06f, m.facadeTeal);
                    SideBox("PanelFrame", root, side, p.s1, p.s1 + 0.14f, panelY0, panelY1, face, face + 0.06f, m.facadeTeal);
                    SideBox("PanelField", root, side, p.s0, p.s1, panelY0, panelY1, 0f, 0.01f, m.facadePanel);
                    if (p.ornament == "StoreName")
                        FacadeOrnament(root, side, "StoreName", p.s0 + 0.4f, p.s1 - 0.4f, panelY0 + 0.3f, panelY1 - 0.3f, 1.35f, false, m);
                    else
                        FacadeOrnament(root, side, "Sunburst", p.s0 + 0.15f, p.s1 - 0.15f, panelY0 + 0.1f, panelY1 - 0.15f, 2.25f, true, m);
                }
            }

            // ---- 옥상: 처마 고리 안쪽에 어두운 방수층 — 원래 벽 재질(격자)이 높은 건물에서 내려다보인다
            Box("RoofMembrane", root, new Vector3(0f, top + k_Parapet, 0f), new Vector3(W, top + k_Parapet + 0.02f, D), m.asphalt, false);

            // ---- 둘레 띠: 기단 위 굵은 띠(벽 겉면에서) · 층마다 얇은 띠 · 처마 (세라믹 겉면에서), 모두 청록
            PerimeterProfile("BaseBelt", root, W, D, 0f, new[]
            {
                new Vector2(0f, base1), new Vector2(0.32f, base1), new Vector2(0.32f, base1 + 0.07f), new Vector2(0.27f, base1 + 0.11f),
                new Vector2(0.27f, base1 + 0.25f), new Vector2(0.21f, base1 + 0.29f), new Vector2(0.21f, blockStart), new Vector2(0f, blockStart),
            }, m.facadeTeal);
            for (int fl = 2; fl < floors; fl++)
            {
                float y = k_Curb + fl * k_FloorHeight, y0 = y - 0.1f, y1 = y + 0.1f;
                PerimeterProfile("Belt", root, W, D, k_FacadeCeramicT, new[]
                {
                    new Vector2(0f, y0), new Vector2(0.10f, y0), new Vector2(0.13f, y0 + 0.04f), new Vector2(0.13f, y1 - 0.04f),
                    new Vector2(0.10f, y1), new Vector2(0f, y1),
                }, m.facadeTeal);
            }
            float z = top;
            PerimeterProfile("Cornice", root, W, D, k_FacadeCeramicT, new[]
            {
                new Vector2(0f, z), new Vector2(0.16f, z), new Vector2(0.16f, z + 0.08f), new Vector2(0.22f, z + 0.11f), new Vector2(0.26f, z + 0.20f),
                new Vector2(0.32f, z + 0.27f), new Vector2(0.40f, z + 0.35f), new Vector2(0.48f, z + 0.41f), new Vector2(0.55f, z + 0.45f),
                new Vector2(0.58f, z + 0.49f), new Vector2(0.58f, z + 0.67f), new Vector2(0.52f, z + 0.67f), new Vector2(0.52f, z + 0.75f),
                new Vector2(0f, z + 0.75f),
            }, m.facadeTeal);
        }

        // 기둥 띠 자리 — 양 끝 모서리 + 쇼핑몰 위층 창 칸 경계 (MallWindows 와 같은 계산: 계단실 모서리 + 0.4 m 사이를 약 6 m 로 나눈다)
        static List<float> PilasterLine(MallFrame f, int w, float length)
        {
            bool shortWall = f.longIsX ? w >= 2 : w < 2;
            float corner = shortWall ? f.B(10) : f.A(4);
            float s0 = corner + 0.4f, s1 = length - corner - 0.4f;
            int n = Mathf.Max(1, Mathf.RoundToInt((s1 - s0) / 6f));
            float len = (s1 - s0) / n;
            var line = new List<float> { k_PilasterHalf + 0.05f };
            for (int k = 0; k <= n; k++)
                line.Add(s0 + k * len);
            line.Add(length - k_PilasterHalf - 0.05f);
            return line;
        }

        // 구멍 안쪽 옆면을 덮는 얇은 판 (벽 두께 + 겉면까지) — 벽 재질이 구멍 안에서 보이지 않게
        static void Reveal(Transform root, FacadeSide side, Rect r, float face, Material mat, bool bottom)
        {
            SideBox("Reveal", root, side, r.xMin, r.xMin + 0.02f, r.yMin, r.yMax, -k_Wall, face, mat);
            SideBox("Reveal", root, side, r.xMax - 0.02f, r.xMax, r.yMin, r.yMax, -k_Wall, face, mat);
            SideBox("Reveal", root, side, r.xMin, r.xMax, r.yMax - 0.02f, r.yMax, -k_Wall, face, mat);
            if (bottom)
                SideBox("Reveal", root, side, r.xMin, r.xMax, r.yMin, r.yMin + 0.02f, -k_Wall, face, mat);
        }

        // 기둥 띠 앞면에 반쯤 묻힌 둥근 세로 홈 (바깥 반쪽만, 8 조각)
        static void ReedQuads(FacadeSide side, float s, float y0, float y1, float d, float radius, List<Vector3[]> quads, List<Vector3> normals)
        {
            const int segments = 8;
            for (int i = 0; i < segments; i++)
            {
                float a0 = Mathf.PI * i / segments, a1 = Mathf.PI * (i + 1) / segments;
                Vector3 p0 = side.P(s + radius * Mathf.Cos(a0), y0, d + radius * Mathf.Sin(a0));
                Vector3 p1 = side.P(s + radius * Mathf.Cos(a1), y0, d + radius * Mathf.Sin(a1));
                float am = (a0 + a1) * 0.5f;
                quads.Add(new[] { p0, p1, p1 + Vector3.up * (y1 - y0), p0 + Vector3.up * (y1 - y0) });
                normals.Add((side.along * Mathf.Cos(am) + side.outward * Mathf.Sin(am)).normalized);
            }
        }

        // 건물 둘레를 한 바퀴 도는 띠 — 단면(d = 겉면에서 바깥쪽, y = 높이)을 아래 → 바깥 → 위 → 벽 순서(반시계)로 준다.
        // 단면 점마다 바깥으로 baseD + d 만큼 넓힌 사각형의 네 모서리를 이어 면을 만든다 → 모서리가 저절로 45° 로 맞는다
        static void PerimeterProfile(string name, Transform root, float W, float D, float baseD, Vector2[] profile, Material mat)
        {
            var quads = new List<Vector3[]>();
            var normals = new List<Vector3>();
            Vector3[] outward = { Vector3.back, Vector3.right, Vector3.forward, Vector3.left };
            for (int i = 0; i + 1 < profile.Length; i++)
            {
                Vector2 a = profile[i], b = profile[i + 1];
                Vector2 dir = b - a;
                if (dir.sqrMagnitude < 1e-8f)
                    continue;
                // 반시계 단면의 바깥 법선 = (dy, -dx)
                Vector2 n2 = new Vector2(dir.y, -dir.x).normalized;
                Vector3[] ca = PerimeterCorners(W, D, baseD + a.x, a.y), cb = PerimeterCorners(W, D, baseD + b.x, b.y);
                for (int k = 0; k < 4; k++)
                {
                    int k1 = (k + 1) % 4;
                    quads.Add(new[] { ca[k], ca[k1], cb[k1], cb[k] });
                    normals.Add((outward[k] * n2.x + Vector3.up * n2.y).normalized);
                }
            }
            QuadsMesh(name, root, quads, normals, mat);
        }

        static Vector3[] PerimeterCorners(float W, float D, float e, float y)
        {
            return new[] { new Vector3(-e, y, -e), new Vector3(W + e, y, -e), new Vector3(W + e, y, D + e), new Vector3(-e, y, D + e) };
        }

        // 사각형 면 묶음을 메시 하나로 — CombineVisuals 가 꼭짓점 4개씩 읽어 (0,2,1)(0,3,2)로 감으니 BoxMesh 처럼
        // (v3 - v0) × (v1 - v0) 이 법선 쪽이 되게 차례를 맞춘다. UV 는 BoxMesh 와 같은 월드 좌표 두 축
        static void QuadsMesh(string name, Transform root, List<Vector3[]> quads, List<Vector3> normals, Material mat)
        {
            if (quads.Count == 0)
                return;
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int q = 0; q < quads.Count; q++)
            {
                Vector3[] v = quads[q];
                Vector3 n = normals[q];
                if (Vector3.Dot(Vector3.Cross(v[3] - v[0], v[1] - v[0]), n) < 0f)
                    v = new[] { v[0], v[3], v[2], v[1] };
                Vector3 wn = root.TransformDirection(n);
                int start = verts.Count;
                foreach (Vector3 p in v)
                {
                    verts.Add(p);
                    norms.Add(n);
                    Vector3 wp = root.TransformPoint(p);
                    if (Mathf.Abs(wn.x) > Mathf.Abs(wn.y) && Mathf.Abs(wn.x) > Mathf.Abs(wn.z))
                        uvs.Add(new Vector2(wp.z, wp.y));
                    else if (Mathf.Abs(wn.y) > Mathf.Abs(wn.z))
                        uvs.Add(new Vector2(wp.x, wp.z));
                    else
                        uvs.Add(new Vector2(wp.x, wp.y));
                }
                tris.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }
            var mesh = new Mesh { name = name };
            mesh.indexFormat = verts.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        // 금속 장식 모델 자리: 모델 비율 그대로 [s0, s1] × [y0, y1] 안에 가장 크게(높이는 maxHeight 까지).
        // bottom = 아래에 붙인다(해돋이 — 지평선이 판 아래쪽), 아니면 가운데. 판 앞면(d 0.01)에 뒷면을 붙인다
        static void FacadeOrnament(Transform root, FacadeSide side, string kind, float s0, float s1, float y0, float y1, float maxHeight,
            bool bottom, Materials m)
        {
            GameObject model = PropModel(kind);
            if (model == null)
                return;
            Bounds mb = ModelBounds(model);
            float mw = Mathf.Max(mb.size.x, mb.size.z), mh = mb.size.y, mt = Mathf.Min(mb.size.x, mb.size.z);
            float k = Mathf.Min((s1 - s0) / mw, Mathf.Min(y1 - y0, maxHeight) / mh);
            float cs = (s0 + s1) * 0.5f, h = mh * k;
            float yb = bottom ? y0 : (y0 + y1 - h) * 0.5f;
            GameObject box = SideBox("Ornament_" + kind, root, side, cs - mw * k * 0.5f, cs + mw * k * 0.5f, yb, yb + h, 0.01f, 0.01f + mt * k, m.facadeGold);
            Skin(box, kind, side.outward);
        }

        // 블렌더에서 뽑은 장식 모델의 재질(FacadeGold)을 맵 재질 GB_Facade_Gold 로 바꿔 끼운다 (NeonSignBuilder 와 같은 방식)
        static void EnsureFacadeModelMaterials(Materials m)
        {
            foreach (string kind in new[] { "Sunburst", "StoreName" })
            {
                string folder = $"{k_FacadeModelFolder}/{kind}";
                if (!AssetDatabase.IsValidFolder(folder))
                    continue;
                foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
                {
                    var mi = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as ModelImporter;
                    if (mi == null)
                        continue;
                    var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), "FacadeGold");
                    if (mi.GetExternalObjectMap().TryGetValue(id, out Object current) && current == m.facadeGold)
                        continue;
                    mi.animationType = ModelImporterAnimationType.None;
                    mi.AddRemap(id, m.facadeGold);
                    mi.SaveAndReimport();
                }
            }
        }

        // 겉면 재질 — 유약 세라믹 장식은 매끄럽게, 금 · 청동은 금속
        static Material FacadeMat(string name, Color color, float smoothness, float metallic = 0f)
        {
            string path = $"{k_MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
                return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
