using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MilTrainSimVR.UrbanCombat.EditorTools
{
    // 청운백화점(N05) 실내 (사용자 11/06 — 목업 'B 원목' + 에스컬레이터는 'C 모던' + 계단 대신 레일형 무빙워크).
    // 쇼핑몰(BuildMall)에 덧입힌다 — 벽 · 바닥 · 난간 · 에스컬레이터의 콜라이더와 걷는 경사로는 그대로라 싸움 · 길찾기는 전과 같다:
    //   · 벽: 한 층 높이 텍스처 한 장(Textures/Map/DeptWall1 — 검정 대리석 걸레받이 · 호두나무 판벽 · 황동 띠 · 크림 대리석)을
    //     맵 좌표로 층마다 맞춰 모든 실내 벽에 깐다 (BuildBuilding 이 쇼핑몰 벽 재질로 넘긴다)
    //   · 천장: 크림색 + 원목 들보(짧은 변 방향, 2.4 m 마다) + 둥근 매입등(황동 테두리, 2.4 m 격자). 꼭대기 층 뚫린 곳 자리는 빛나는 천창 + 원목 격자
    //   · 바닥: 뚫린 곳 둘레 검정 대리석 테두리 0.6 m + 황동 줄, 위층 뚫린 곳 가장자리는 원목 띠
    //   · 뚫린 곳 · 계단 끝 난간: 콜라이더 상자는 안 보이게 두고 원목 손잡이 · 황동 아래띠 · 황동 살 14 cm
    //   · 에스컬레이터 → 경사형 무빙워크: 계단 모양은 숨기고 알루미늄 판(Textures/Map/Walkway1, 홈이 진행 방향) · 스테인리스 발판 ·
    //     노란 빗살판과 가장자리 줄 · 스테인리스 옆판 · 흰 대리석 받침 · 통유리 난간 + 검은 손잡이 · 흰 대리석 몸통
    //   · 실내 조명: 층마다 그림자 없는 따뜻한 점광원 (PC 렌더러가 Forward+ 라 큰 합친 메시에도 다 닿는다)
    //   · 층마다 반사 기준점을 구워 대리석 · 오닉스 바닥에 하늘 대신 실내가 비친다 (맵 만들기 끝에 BakeDeptProbes —
    //     합친 메시는 Reflection Probe Static 이라 구울 때 그려진다)
    public static partial class UrbanGreyboxBuilder
    {
        const float k_DeptBeamSpacing = 2.4f;
        static readonly List<ReflectionProbe> s_DeptProbes = new List<ReflectionProbe>();

        static float DeptFloorTop(int fl) => fl == 0 ? k_Curb + k_FloorFinish : k_Curb + fl * k_FloorHeight;

        // 그 층 천장 높이 — 위층 바닥판 밑, 꼭대기 들어갈 층은 막힌 위층(Mass) 밑 또는 지붕판 밑
        static float DeptCeiling(int fl, int enterable, int floors)
        {
            if (fl < enterable - 1)
                return k_Curb + (fl + 1) * k_FloorHeight - k_Slab;
            return floors > enterable ? k_Curb + enterable * k_FloorHeight : k_Curb + floors * k_FloorHeight - k_Slab;
        }

        // 건물 좌표 사각형(x, z) → 쇼핑몰 평면(a = 긴 변, b = 짧은 변)
        static Rect ToPlan(MallFrame f, Rect r) => f.longIsX ? r : Rect.MinMaxRect(r.yMin, r.xMin, r.yMax, r.xMax);

        // 1층 로비 안내데스크 (사용자 11/16 "그 로비에 청운 백화점 간판의 안내데스크를 간단하게") — 큰 정문(grandEntrance) 백화점만.
        //   로비 가운데, 정문에서 7.6 m 안쪽에 정문을 보고 선다. 앞판 4.2 m + 양 끝에서 뒤로 꺾인 날개(ㄷ자, 안쪽이 직원 자리)
        //   높이 1.08 m — 서면 너머로 쏘고, 무릎 쏴 자세(머리 1.05 m)면 숨는 높이라 적 · 플레이어 모두 엄폐물로 쓴다.
        //   호두나무 몸 · 검정 대리석 걸레받이 · 흰 대리석 상판 · 금색 띠, 앞면 가운데 청록 판에 금색 '청운백화점'(외벽 이름판과 같은 모델)
        static void DeptInfoDesk(Transform root, MallFrame f, float y, Materials m)
        {
            const float half = 2.1f, depth = 0.7f, wing = 0.9f, h = 1.08f, top = 0.04f, over = 0.03f, plinth = 0.12f, inset = 0.025f;
            float ac = f.L * 0.5f, b0 = 7.6f, b1 = b0 + depth, b2 = b1 + wing;
            float a0 = ac - half, a1 = ac + half;
            // 몸통 — 앞판 + 왼 · 오른 날개 (부딪힘 있음)
            var bodies = new[] { (a0, a1, b0, b1), (a0, a0 + depth, b1, b2), (a1 - depth, a1, b1, b2) };
            foreach (var (x0, x1, z0, z1) in bodies)
            {
                MBox("DeskBody", root, f, x0, x1, y + plinth, y + h - top, z0, z1, m.deptWood, true);
                MBox("DeskPlinth", root, f, x0 + inset, x1 - inset, y, y + plinth, z0 + inset, z1 - inset, m.deptBlack, true);
            }
            // 상판 — 바깥쪽으로 3 cm 내민 흰 대리석 (앞판 · 날개)
            MBox("DeskTop", root, f, a0 - over, a1 + over, y + h - top, y + h, b0 - over, b1, m.deptWhite, true);
            MBox("DeskTop", root, f, a0 - over, a0 + depth, y + h - top, y + h, b1, b2 + over, m.deptWhite, true);
            MBox("DeskTop", root, f, a1 - depth, a1 + over, y + h - top, y + h, b1, b2 + over, m.deptWhite, true);
            // 금색 띠 — 상판 밑 앞면 · 바깥 옆면
            float g0 = y + h - top - 0.07f, g1 = g0 + 0.025f;
            MBox("DeskBand", root, f, a0, a1, g0, g1, b0 - 0.006f, b0, m.facadeGold, false);
            MBox("DeskBand", root, f, a0 - 0.006f, a0, g0, g1, b0, b2, m.facadeGold, false);
            MBox("DeskBand", root, f, a1, a1 + 0.006f, g0, g1, b0, b2, m.facadeGold, false);
            // 앞면 청록 판 + 금색 테두리
            float p0 = ac - 1.35f, p1 = ac + 1.35f, py0 = y + 0.3f, py1 = y + 0.84f, pf = b0 - 0.012f;
            MBox("DeskPanel", root, f, p0, p1, py0, py1, pf, b0, m.facadeTeal, false);
            MBox("DeskPanelFrame", root, f, p0 - 0.02f, p1 + 0.02f, py1, py1 + 0.02f, pf - 0.004f, b0, m.facadeGold, false);
            MBox("DeskPanelFrame", root, f, p0 - 0.02f, p1 + 0.02f, py0 - 0.02f, py0, pf - 0.004f, b0, m.facadeGold, false);
            MBox("DeskPanelFrame", root, f, p0 - 0.02f, p0, py0, py1, pf - 0.004f, b0, m.facadeGold, false);
            MBox("DeskPanelFrame", root, f, p1, p1 + 0.02f, py0, py1, pf - 0.004f, b0, m.facadeGold, false);
            // 금색 '청운백화점' — 판 안쪽 여백을 두고 모델 비율 그대로 가장 크게, 판 앞면에 붙인다 (FacadeOrnament 와 같은 방식)
            GameObject model = PropModel("StoreName");
            if (model == null)
                return;
            Bounds mb = ModelBounds(model);
            float mw = Mathf.Max(mb.size.x, mb.size.z), mh = mb.size.y, mt = Mathf.Min(mb.size.x, mb.size.z);
            float k = Mathf.Min((p1 - p0 - 0.3f) / mw, (py1 - py0 - 0.14f) / mh);
            float lw = mw * k, lh = mh * k, ly = (py0 + py1 - lh) * 0.5f;
            GameObject letters = MBox("DeskName", root, f, ac - lw * 0.5f, ac + lw * 0.5f, ly, ly + lh, pf - 0.002f - mt * k, pf - 0.002f, m.facadeGold, false);
            Skin(letters, "StoreName", f.longIsX ? Vector3.back : Vector3.left);   // 정문 쪽(b 작은 쪽)을 본다
        }

        // holes: 위층 바닥판 구멍(건물 좌표 — 계단실 넷 + 뚫린 곳), va · vb: 뚫린 곳(평면 좌표)
        static void DeptInterior(Transform root, MallFrame f, List<Rect> holes, int enterable, int floors, float W, float D,
            float va0, float va1, float vb0, float vb1, Materials m)
        {
            var planHoles = new List<Rect>();
            foreach (Rect h in holes)
                planHoles.Add(ToPlan(f, h));
            var voidRect = Rect.MinMaxRect(va0, vb0, va1, vb1);
            Transform lights = Group("DeptLights", root);
            for (int fl = 0; fl < enterable; fl++)
            {
                float y = DeptFloorTop(fl), yc = DeptCeiling(fl, enterable, floors);
                bool top = fl == enterable - 1;
                // 바닥: 뚫린 곳 둘레 검정 대리석 테두리 + 황동 줄
                foreach (Rect r in RectMinus(Rect.MinMaxRect(va0 - 0.6f, vb0 - 0.6f, va1 + 0.6f, vb1 + 0.6f), new List<Rect> { voidRect }))
                    MBox("DeptBorder", root, f, r.xMin, r.xMax, y, y + 0.004f, r.yMin, r.yMax, m.deptBlack, false);
                foreach (Rect r in RectMinus(Rect.MinMaxRect(va0 - 0.48f, vb0 - 0.48f, va1 + 0.48f, vb1 + 0.48f),
                             new List<Rect> { Rect.MinMaxRect(va0 - 0.45f, vb0 - 0.45f, va1 + 0.45f, vb1 + 0.45f) }))
                    MBox("DeptInlay", root, f, r.xMin, r.xMax, y + 0.004f, y + 0.006f, r.yMin, r.yMax, m.facadeGold, false);
                // 위층 뚫린 곳 가장자리 원목 띠 (바닥판 옆면보다 1 cm 안쪽으로 나와 같은 면이 겹치지 않게)
                if (fl >= 1)
                {
                    foreach (Rect r in RectMinus(Rect.MinMaxRect(va0 - 0.05f, vb0 - 0.05f, va1 + 0.05f, vb1 + 0.05f),
                                 new List<Rect> { Rect.MinMaxRect(va0 + 0.01f, vb0 + 0.01f, va1 - 0.01f, vb1 - 0.01f) }))
                        MBox("DeptSlabEdge", root, f, r.xMin, r.xMax, y - 0.65f, y - 0.02f, r.yMin, r.yMax, m.deptWood, false);
                }

                // 천장: 원목 들보(b 방향) + 들보 사이 2.4 m 격자로 둥근 매입등(황동 테두리 + 빛나는 판). 위층 바닥판에 뚫린 곳(계단 · 에스컬레이터)은
                // 피한다 — 꼭대기는 뚫린 곳(천창)만
                List<Rect> ceilingHoles = top ? new List<Rect> { voidRect } : planHoles;
                var trims = new List<Vector3[]>();
                var trimNormals = new List<Vector3>();
                var lamps = new List<Vector3[]>();
                var lampNormals = new List<Vector3>();
                for (float a = k_Wall + k_DeptBeamSpacing * 0.5f; a < f.L - k_Wall - 0.3f; a += k_DeptBeamSpacing)
                {
                    foreach (Vector2 span in Spans(k_Wall, f.S - k_Wall, a, 0.16f, ceilingHoles))
                        MBox("DeptBeam", root, f, a - 0.14f, a + 0.14f, yc - 0.3f, yc, span.x, span.y, m.deptWood, false);
                    float am = a + k_DeptBeamSpacing * 0.5f;
                    if (am > f.L - k_Wall - 0.5f)
                        continue;
                    for (float b = k_Wall + 1.2f; b < f.S - k_Wall - 0.6f; b += 2.4f)
                    {
                        if (InAny(ceilingHoles, am, b, 0.3f))
                            continue;
                        DownDisc(trims, trimNormals, f.P(am, yc - 0.005f, b), 0.1f);
                        DownDisc(lamps, lampNormals, f.P(am, yc - 0.01f, b), 0.075f);
                    }
                }
                QuadsMesh("DeptDownlightTrim", root, trims, trimNormals, m.facadeGold);
                QuadsMesh("DeptDownlight", root, lamps, lampNormals, m.deptLight);
                if (top)
                {
                    // 천창: 빛나는 판 + 원목 격자 + 둘레 틀
                    MBox("DeptLaylight", root, f, va0, va1, yc - 0.02f, yc, vb0, vb1, m.deptLaylight, false);
                    for (int k = 1; k < 10; k++)
                    {
                        float a = Mathf.Lerp(va0, va1, k / 10f);
                        MBox("DeptLayGrid", root, f, a - 0.06f, a + 0.06f, yc - 0.14f, yc - 0.02f, vb0, vb1, m.deptWood, false);
                    }
                    for (int k = 1; k < 4; k++)
                    {
                        float b = Mathf.Lerp(vb0, vb1, k / 4f);
                        MBox("DeptLayGrid", root, f, va0, va1, yc - 0.14f, yc - 0.02f, b - 0.06f, b + 0.06f, m.deptWood, false);
                    }
                    foreach (Rect r in RectMinus(Rect.MinMaxRect(va0 - 0.25f, vb0 - 0.25f, va1 + 0.25f, vb1 + 0.25f), new List<Rect> { voidRect }))
                        MBox("DeptLayFrame", root, f, r.xMin, r.xMax, yc - 0.25f, yc, r.yMin, r.yMax, m.deptWood, false);
                }

                // 따뜻한 실내 조명 — 그림자 없이 약 7 × 8 m 격자, 바깥벽에서 4.5 m 안쪽 (벽 너머로 새는 빛을 줄이게)
                int na = Mathf.Max(1, Mathf.RoundToInt((f.L - 9f) / 7.5f)), nb = Mathf.Max(1, Mathf.RoundToInt((f.S - 9f) / 7f));
                for (int i = 0; i <= na; i++)
                {
                    for (int j = 0; j <= nb; j++)
                    {
                        float a = Mathf.Lerp(4.5f, f.L - 4.5f, (float)i / na), b = Mathf.Lerp(4.5f, f.S - 4.5f, (float)j / nb);
                        var go = new GameObject("DeptLight");
                        go.transform.SetParent(lights, false);
                        go.transform.localPosition = f.P(a, yc - 0.45f, b);
                        var light = go.AddComponent<Light>();
                        light.type = LightType.Point;
                        light.color = new Color(1f, 0.84f, 0.66f);
                        light.intensity = 3f;
                        light.range = 6.5f;
                        light.shadows = LightShadows.None;
                        light.lightmapBakeType = LightmapBakeType.Realtime;
                    }
                }

                // 반사 기준점 — 이 층 실내를 덮는 상자(맵 축에 나란히 — 상자는 회전을 안 따라서 회전 없는 Buildings 묶음 밑에 둔다).
                // 찍는 자리는 뚫린 곳 옆 복도 눈높이 (뚫린 곳 가운데는 무빙워크 난간 사이라 가까운 유리 · 몸통이 다 덮는다).
                // 상자는 바닥 · 천장 · 안쪽 벽면보다 10 cm 씩 벽 · 바닥판 속으로 넉넉히, 섞는 거리 0 — 상자 면에 딱 붙은 바닥 · 벽은
                // 섞는 거리 안이라 실내가 거의 안 비치고 하늘이 비쳐 파랗게 떴다 (11/06). 위아래 층 상자는 바닥판 속에서 맞닿는다
                var probeGo = new GameObject($"DeptProbe_{root.name}_{fl + 1}F");
                probeGo.transform.SetParent(root.parent, false);
                probeGo.transform.position = root.TransformPoint(f.P(f.L * 0.5f, y + 1.6f, (f.B(12) + vb0) * 0.5f));
                const float margin = 0.1f;
                float x0 = k_Wall - margin, x1 = W - k_Wall + margin, z0 = k_Wall - margin, z1 = D - k_Wall + margin;
                var bounds = new Bounds(root.TransformPoint(new Vector3(x0, y - margin, z0)), Vector3.zero);
                foreach (Vector3 c in new[] { new Vector3(x1, y - margin, z0), new Vector3(x0, y - margin, z1), new Vector3(x1, yc + margin, z1) })
                    bounds.Encapsulate(root.TransformPoint(c));
                var probe = probeGo.AddComponent<ReflectionProbe>();
                probe.mode = ReflectionProbeMode.Custom;
                probe.boxProjection = true;
                probe.resolution = 128;
                probe.hdr = true;
                probe.blendDistance = 0f;
                probe.center = bounds.center - probeGo.transform.position;
                probe.size = bounds.size;
                s_DeptProbes.Add(probe);
            }
        }

        // 선분 [s0, s1] (b 방향, a 자리 고정)에서 a 를 덮는 구멍들의 b 구간을 뺀 나머지
        static List<Vector2> Spans(float s0, float s1, float a, float halfWidth, List<Rect> holes)
        {
            var spans = new List<Vector2> { new Vector2(s0, s1) };
            foreach (Rect h in holes)
            {
                if (a + halfWidth <= h.xMin || a - halfWidth >= h.xMax)
                    continue;
                var next = new List<Vector2>();
                foreach (Vector2 sp in spans)
                {
                    if (h.yMax <= sp.x || h.yMin >= sp.y)
                    {
                        next.Add(sp);
                        continue;
                    }
                    if (h.yMin > sp.x)
                        next.Add(new Vector2(sp.x, h.yMin));
                    if (h.yMax < sp.y)
                        next.Add(new Vector2(h.yMax, sp.y));
                }
                spans = next;
            }
            spans.RemoveAll(sp => sp.y - sp.x < 0.2f);
            return spans;
        }

        // 아래를 보는 팔각형 판 하나(반지름 r, 가운데 c) — 사각형 셋(가운데 직사각형 + 위아래 사다리꼴)이라 합치기(꼭짓점 4개씩)에 그대로 들어간다
        static void DownDisc(List<Vector3[]> quads, List<Vector3> normals, Vector3 c, float r)
        {
            float p = r * Mathf.Cos(Mathf.PI / 8f), q = r * Mathf.Sin(Mathf.PI / 8f);
            Vector3 V(float x, float z) => c + new Vector3(x, 0f, z);
            quads.Add(new[] { V(-p, -q), V(p, -q), V(p, q), V(-p, q) });
            quads.Add(new[] { V(-p, q), V(p, q), V(q, p), V(-q, p) });
            quads.Add(new[] { V(-q, -p), V(q, -p), V(p, -q), V(-p, -q) });
            for (int i = 0; i < 3; i++)
                normals.Add(Vector3.down);
        }

        static bool InAny(List<Rect> rects, float a, float b, float margin)
        {
            foreach (Rect r in rects)
            {
                if (a > r.xMin - margin && a < r.xMax + margin && b > r.yMin - margin && b < r.yMax + margin)
                    return true;
            }
            return false;
        }

        // 난간 (높이 1 m): 콜라이더 상자 하나 — 백화점이면 상자는 안 보이게 두고 원목 손잡이 · 황동 아래띠 · 황동 살로 겉모양.
        // 백화점 상자는 'Ignore Raycast' 층 (11/18 사용자: "투명한 곳을 쏘는데 벽에 막힌거 마냥") — 몸은 그대로 막고, 총알 · 적 시선은
        // 살 사이로 지나간다. 원목 손잡이(0.93~1 m)만 총알이 막히게 얇은 상자를 따로 둔다
        static void MallRailing(Transform root, MallFrame f, float a0, float a1, float y, float b0, float b1, Materials m, bool dept)
        {
            GameObject box = MBox("Railing", root, f, a0, a1, y, y + k_Railing, b0, b1, m.dark, true);
            if (!dept)
                return;
            ColliderOnly(box);
            box.layer = k_IgnoreRaycastLayer;
            bool alongA = Mathf.Abs(a1 - a0) >= Mathf.Abs(b1 - b0);
            float s0 = alongA ? Mathf.Min(a0, a1) : Mathf.Min(b0, b1), s1 = alongA ? Mathf.Max(a0, a1) : Mathf.Max(b0, b1);
            float c = alongA ? (b0 + b1) * 0.5f : (a0 + a1) * 0.5f;
            GameObject Bar(string name, float u0, float u1, float v0, float v1, float halfT, Material mat, bool collider = false)
            {
                if (alongA)
                    return MBox(name, root, f, u0, u1, y + v0, y + v1, c - halfT, c + halfT, mat, collider);
                return MBox(name, root, f, c - halfT, c + halfT, y + v0, y + v1, u0, u1, mat, collider);
            }
            ColliderOnly(Bar("RailTopHit", s0, s1, 0.93f, k_Railing, 0.045f, m.dark, true));   // 손잡이 총알 판정 (Default 층)
            Bar("RailTop", s0, s1, 0.93f, k_Railing, 0.045f, m.deptWood);
            Bar("RailFoot", s0, s1, 0.08f, 0.12f, 0.02f, m.facadeGold);
            int n = Mathf.Max(1, Mathf.RoundToInt((s1 - s0) / 0.14f));
            for (int i = 1; i < n; i++)
            {
                float s = Mathf.Lerp(s0, s1, (float)i / n);
                Bar("RailBar", s - 0.0125f, s + 0.0125f, 0.12f, 0.93f, 0.0125f, m.facadeGold);
            }
        }

        // 경사형 무빙워크 겉모양 — Escalator() 가 콜라이더(몸통 · 옆 난간 · 계단 밑 채움 · 경사로)를 만든 뒤에 덮는다.
        // c0~c1: 에스컬레이터 폭(b), aBot → aTop 으로 오른다(i0 ~ i1 이 기운 구간), s0~s1: 몸통 폭.
        // 가로 단면(c0 쪽, c1 쪽은 거울): 받침 s0 ~ c0+0.15 (유리가 선다) · 옆판 ~ c0+0.2 · 판(알루미늄) ~ 가운데
        static void DeptWalkway(Transform root, MallFrame f, float c0, float c1, float aBot, float aTop, float i0, float i1,
            float yBot, float yTop, float baseY, float s0, float s1, Materials m)
        {
            // 1층 바닥은 마감만큼 높다 — 1층에서 출발하는 것의 아래쪽 발판 · 받침 · 유리는 그 위에서
            float floor1 = k_Curb + k_FloorFinish;
            float yb = Mathf.Max(yBot, floor1);
            float pb0 = c0 + k_EscRail + 0.05f, pb1 = c1 - k_EscRail - 0.05f;
            Vector3 bDir = f.longIsX ? Vector3.forward : Vector3.right;
            float run = Mathf.Abs(i1 - i0), rise = yTop - yBot, slope = Mathf.Sqrt(run * run + rise * rise);
            Vector3 S(float t, float dy, float b) => f.P(Mathf.LerpUnclamped(i0, i1, t), Mathf.LerpUnclamped(yBot, yTop, t) + dy, b);

            // 알루미늄 판: UV u = 경사를 따라 잰 거리(m), v = 가로(m) — 재질 Tiling 이 판 한 장(0.8 × 0.408 m)을 정한다
            Vector3 p0 = S(0f, 0.004f, pb0), p1 = S(1f, 0.004f, pb0), p2 = S(1f, 0.004f, pb1), p3 = S(0f, 0.004f, pb1);
            Vector3 up = Vector3.Cross(p1 - p0, bDir).normalized;
            if (up.y < 0f)
                up = -up;
            UvQuadMesh("DeptPallet", root, new[] { p0, p1, p2, p3 },
                new[] { new Vector2(0f, 0f), new Vector2(slope, 0f), new Vector2(slope, pb1 - pb0), new Vector2(0f, pb1 - pb0) }, up, m.deptPallet);
            foreach (float b in new[] { pb0 + 0.0225f, pb1 - 0.0225f })
                SlopedVisual("DeptEdgeLine", root, S(0f, 0f, b), S(1f, 0f, b), 0.045f, 0.004f, 0.01f, m.deptYellow);

            // 양 끝 평평한 곳: 스테인리스 발판 · 노란 빗살판
            MBox("DeptLanding", root, f, aBot, i0, yb, yb + 0.012f, pb0, pb1, m.deptStainless, false);
            MBox("DeptLanding", root, f, i1, aTop, yTop, yTop + 0.012f, pb0, pb1, m.deptStainless, false);
            MBox("DeptComb", root, f, i0 - 0.05f, i0 + 0.05f, yb + 0.006f, yb + 0.02f, pb0, pb1, m.deptYellow, false);
            MBox("DeptComb", root, f, i1 - 0.05f, i1 + 0.05f, yTop + 0.006f, yTop + 0.02f, pb0, pb1, m.deptYellow, false);

            // 양옆: 흰 대리석 받침 · 스테인리스 옆판 · 통유리 + 검은 손잡이 — 아래 끝부터 위 끝까지 한 덩어리씩 (11/16 사용자: "유리쪽 마감이 제대로 안 됨").
            // 전에는 평평한 곳 상자 + 기운 상자를 따로 놓아서 꺾이는 곳에 유리 두 장이 겹쳐 진한 띠가 보이고, 기운 손잡이(경사에 수직으로 잰 높이)가
            // 평평한 손잡이보다 5 cm 높아 끊겨 보였고, 끝은 유리 · 손잡이가 네모로 잘려 있었다.
            // 이제 걷는 선(WalkLine — 꺾이는 곳은 부드럽게)을 따라 높이를 수직으로 재서 한 줄로 잇고, 유리 끝은 반원(실제 무빙워크 끝 '뉴얼'),
            // 손잡이는 그 반원을 감아 받침 바로 위까지 내려온 뒤 안쪽으로 꺾여 입구 상자로 들어간다 (11/16 사용자: 입구가 너무 높았다 —
            // "손잡이는 맨 밑 바닥에 거의 다 와서 들어간다")
            var line = new WalkLine(aBot, aTop, i0, i1, yb, yTop);
            Vector3 aDir = (f.P(1f, 0f, 0f) - f.P(0f, 0f, 0f)) * Mathf.Sign(aTop - aBot);
            foreach (bool low in new[] { true, false })
            {
                float d0 = low ? s0 : c1 - k_EscRail, d1 = low ? c0 + k_EscRail : s1;          // 받침
                float k0 = low ? c0 + k_EscRail : pb1, k1 = low ? pb0 : c1 - k_EscRail;        // 옆판
                float rm = low ? c0 + k_EscRail * 0.5f : c1 - k_EscRail * 0.5f;                // 유리 · 손잡이 가운데
                // 받침은 걷는 선 5 cm 밑부터 — 꺾이는 곳을 부드럽게 한 만큼(최대 3 cm) 몸통과 틈이 안 생기게
                Band("DeptDeck", root, f, line, aDir, bDir, 0f, line.L, 0f, -0.05f, 0.08f, d0, d1, m.deptWhite);
                Band("DeptSkirt", root, f, line, aDir, bDir, 0f, line.L, 0f, -0.05f, 0.13f, k0, k1, m.deptStainless);
                // 유리: 받침 속(걷는 선 3 cm 위)부터 0.95 m 까지, 양 끝은 반원(손잡이가 감아 도는 둥근 끝)
                GlassPanel(root, f, line, aDir, bDir, rm, m);
                Handrail(root, f, line, aDir, bDir, rm, m);
            }

            // 흰 대리석 몸통: 기운 구간 아래 옆면 둘(사다리꼴 — 1층에서 출발하면 세모) + 밑면(1층 바닥에 앉은 것은 빼고).
            // 계단 밑을 채운 콜라이더와 같은 덩어리. 꼭짓점은 높은 끝 밑에서 시작 — 세모여도 (v3 - v0) × (v1 - v0) 로 감는 쪽을 맞출 수 있게
            var quads = new List<Vector3[]>();
            var normals = new List<Vector3>();
            foreach (float s in new[] { s0, s1 })
            {
                quads.Add(new[] { f.P(i1, baseY, s), f.P(i1, yTop, s), f.P(i0, yBot, s), f.P(i0, baseY, s) });
                normals.Add(s == s0 ? -bDir : bDir);
            }
            if (baseY > floor1 + 0.01f)
            {
                quads.Add(new[] { f.P(i0, baseY, s0), f.P(i1, baseY, s0), f.P(i1, baseY, s1), f.P(i0, baseY, s1) });
                normals.Add(Vector3.down);
            }
            QuadsMesh("DeptEscBody", root, quads, normals, m.deptWhite);
        }

        // ---------------------------------------------------------------- 무빙워크 난간 (11/16)

        const float k_NewelInset = 0.1f;     // 유리 반원 끝(가장 바깥)을 받침 끝에서 이만큼 안쪽 — 손잡이 바깥면이 받침 위에 오게
        const float k_GlassTop = 0.95f;      // 걷는 선에서 유리 윗면까지 (수직으로 잰다 — 경사에서도 손잡이 높이가 같다)
        const float k_RailLow = 0.16f;       // 반원 맨 아래 유리 끝 높이 — 손잡이 아랫면이 받침 윗면(0.08)보다 조금 위
        const float k_NewelRadius = (k_GlassTop - k_RailLow) * 0.5f;   // 반원 반지름 (약 0.4 m)
        const float k_InletRun = 0.26f;      // 반원 아래에서 안쪽으로 꺾여 입구로 들어가는 길이
        const int k_ArcSteps = 16;           // 반원 하나를 나누는 수

        // 무빙워크 걷는 선 — u = 아래 끝(aBot)에서 위 끝 쪽으로 잰 평면 거리, Y(u) = 그 자리 높이.
        // 아래 평평한 곳(yb) → 경사 → 위 평평한 곳(yTop). 꺾이는 곳 앞뒤 k_Blend 는 2차 곡선으로 이어 손잡이 · 유리가 꺾이지 않고 휜다
        // (꺾이는 점에서 직선과 3 cm 차이 — 받침 · 옆판이 걷는 판 가장자리를 그대로 덮는다)
        class WalkLine
        {
            const float k_Blend = 0.4f;
            readonly float m_ABot, m_Dir, m_U0, m_U1, m_Yb, m_YTop, m_K;
            public readonly float L;

            public WalkLine(float aBot, float aTop, float i0, float i1, float yb, float yTop)
            {
                m_ABot = aBot;
                m_Dir = Mathf.Sign(aTop - aBot);
                L = Mathf.Abs(aTop - aBot);
                m_U0 = Mathf.Abs(i0 - aBot);
                m_U1 = Mathf.Abs(i1 - aBot);
                m_Yb = yb;
                m_YTop = yTop;
                m_K = (yTop - yb) / Mathf.Max(0.01f, m_U1 - m_U0);
            }

            public float A(float u) => m_ABot + m_Dir * u;

            public float Y(float u)
            {
                const float d = k_Blend;
                if (u <= m_U0 - d)
                    return m_Yb;
                if (u >= m_U1 + d)
                    return m_YTop;
                if (u < m_U0 + d)
                {
                    float t = (u - (m_U0 - d)) / (2f * d);
                    return m_Yb + m_K * d * t * t;
                }
                if (u > m_U1 - d)
                {
                    float t = (u - (m_U1 - d)) / (2f * d);
                    return m_YTop - m_K * d * (1f - t) * (1f - t);
                }
                return m_Yb + m_K * (u - m_U0);
            }

            // u0 ~ u1 를 나눌 자리 — 양 끝, 꺾이는 곳 둘레는 촘촘히, 그 밖은 1 m 마다 (곧은 곳이라 충분)
            public List<float> Samples(float u0, float u1, IEnumerable<float> extra = null)
            {
                var list = new List<float> { u0, u1 };
                foreach (float c in new[] { m_U0, m_U1 })
                {
                    for (int i = 0; i <= 8; i++)
                        list.Add(c - k_Blend + 2f * k_Blend * i / 8f);
                }
                for (float u = Mathf.Ceil(u0); u < u1; u += 1f)
                    list.Add(u);
                if (extra != null)
                    list.AddRange(extra);
                list.RemoveAll(u => u < u0 - 0.0001f || u > u1 + 0.0001f);
                list.Sort();
                var result = new List<float>();
                foreach (float u in list)
                {
                    if (result.Count == 0 || u - result[result.Count - 1] > 0.005f)
                        result.Add(u);
                }
                return result;
            }
        }

        // 걷는 선을 따라 가는 막대 하나 — u0 ~ u1, 높이 걷는 선 + lo ~ + hi(수직), 옆 b0 ~ b1. 한 덩어리라 꺾이는 곳에 겹침 · 틈이 없다.
        // radius > 0 이면 양 끝 위 모서리를 그 반지름으로 둥글게 (유리 끝). aDir · bDir: u · b 가 느는 쪽 (건물 기준)
        static void Band(string name, Transform root, MallFrame f, WalkLine line, Vector3 aDir, Vector3 bDir, float u0, float u1, float radius,
            float lo, float hi, float b0, float b1, Material mat)
        {
            if (b0 > b1)
                (b0, b1) = (b1, b0);
            var arc = new List<float>();
            if (radius > 0f)
            {
                for (int i = 1; i <= k_ArcSteps; i++)
                {
                    float c = radius * (1f - Mathf.Cos(0.5f * Mathf.PI * i / k_ArcSteps));
                    arc.Add(u0 + c);
                    arc.Add(u1 - c);
                }
            }
            List<float> us = line.Samples(u0, u1, arc);
            float Bottom(float u) => line.Y(u) + lo;
            float Top(float u)
            {
                float y = line.Y(u) + hi;
                if (radius <= 0f)
                    return y;
                float x = u < u0 + radius ? u0 + radius - u : u > u1 - radius ? u - (u1 - radius) : 0f;
                return y - radius + Mathf.Sqrt(Mathf.Max(0f, radius * radius - x * x));
            }
            Vector3 P(float u, float y, float b) => f.P(line.A(u), y, b);
            // 가로로 자른 면 위의 점 (a 쪽 거리, 높이) → 건물 기준 방향
            Vector3 Flat(float du, float dy) => aDir * du + Vector3.up * dy;

            var quads = new List<Vector3[]>();
            var normals = new List<Vector3>();
            for (int i = 0; i + 1 < us.Count; i++)
            {
                float ua = us[i], ub = us[i + 1];
                float loA = Bottom(ua), loB = Bottom(ub), hiA = Top(ua), hiB = Top(ub);
                quads.Add(new[] { P(ua, loA, b0), P(ub, loB, b0), P(ub, hiB, b0), P(ua, hiA, b0) });
                normals.Add(-bDir);
                quads.Add(new[] { P(ua, loA, b1), P(ub, loB, b1), P(ub, hiB, b1), P(ua, hiA, b1) });
                normals.Add(bDir);
                // 윗면: 둥근 끝이면 그 원의 가운데에서 바깥쪽, 아니면 위쪽을 보게
                Vector3 top = Vector3.Cross(Flat(ub - ua, hiB - hiA), bDir).normalized;
                float um = (ua + ub) * 0.5f, uc = radius > 0f ? Mathf.Clamp(um, u0 + radius, u1 - radius) : um;
                float yc = radius > 0f ? line.Y(uc) + hi - radius : line.Y(um) + (lo + hi) * 0.5f;
                if (Vector3.Dot(top, Flat(um - uc, (hiA + hiB) * 0.5f - yc)) < 0f)
                    top = -top;
                quads.Add(new[] { P(ua, hiA, b0), P(ub, hiB, b0), P(ub, hiB, b1), P(ua, hiA, b1) });
                normals.Add(top);
                Vector3 bottom = Vector3.Cross(Flat(ub - ua, loB - loA), bDir).normalized;
                if (bottom.y > 0f)
                    bottom = -bottom;
                quads.Add(new[] { P(ua, loA, b0), P(ub, loB, b0), P(ub, loB, b1), P(ua, loA, b1) });
                normals.Add(bottom);
            }
            quads.Add(new[] { P(u0, Bottom(u0), b0), P(u0, Bottom(u0), b1), P(u0, Top(u0), b1), P(u0, Top(u0), b0) });
            normals.Add(-aDir);
            quads.Add(new[] { P(u1, Bottom(u1), b0), P(u1, Bottom(u1), b1), P(u1, Top(u1), b1), P(u1, Top(u1), b0) });
            normals.Add(aDir);
            QuadsMesh(name, root, quads, normals, mat);
        }

        // 유리 끝 반원의 가운데 (u, 높이) — 아래 끝(left)이면 u = 안쪽 끝, 위 끝이면 바깥쪽 끝에서 반지름만큼 안쪽
        static Vector2 NewelCenter(WalkLine line, bool top)
        {
            float u = top ? line.L - k_NewelInset - k_NewelRadius : k_NewelInset + k_NewelRadius;
            return new Vector2(u, line.Y(u) + k_GlassTop - k_NewelRadius);
        }

        // 통유리 한 장 — 가운데는 걷는 선 따라 (받침 속 3 cm ~ 0.95 m), 양 끝은 반원. 반원 아래(반원 맨 아래 ~ 받침 속)는 손잡이 입구 상자가 덮는다.
        // 한 덩어리라 꺾이는 곳에 겹친 유리가 없다. 비치는 재질이라 안쪽 이음면(가운데 조각 ↔ 반원)은 만들지 않는다
        static void GlassPanel(Transform root, MallFrame f, WalkLine line, Vector3 aDir, Vector3 bDir, float b, Materials m)
        {
            const float lo = 0.03f, halfT = 0.008f;
            float b0 = b - halfT, b1 = b + halfT, r = k_NewelRadius;
            Vector2 cL = NewelCenter(line, false), cR = NewelCenter(line, true);
            Vector3 P(float u, float y, float side) => f.P(line.A(u), y, side);
            Vector3 Flat(float du, float dy) => aDir * du + Vector3.up * dy;
            var quads = new List<Vector3[]>();
            var normals = new List<Vector3>();

            // 가운데: 반원 가운데 사이
            List<float> us = line.Samples(cL.x, cR.x);
            for (int i = 0; i + 1 < us.Count; i++)
            {
                float ua = us[i], ub = us[i + 1];
                float loA = line.Y(ua) + lo, loB = line.Y(ub) + lo, hiA = line.Y(ua) + k_GlassTop, hiB = line.Y(ub) + k_GlassTop;
                quads.Add(new[] { P(ua, loA, b0), P(ub, loB, b0), P(ub, hiB, b0), P(ua, hiA, b0) });
                normals.Add(-bDir);
                quads.Add(new[] { P(ua, loA, b1), P(ub, loB, b1), P(ub, hiB, b1), P(ua, hiA, b1) });
                normals.Add(bDir);
                Vector3 up = Vector3.Cross(Flat(ub - ua, hiB - hiA), bDir).normalized;
                if (up.y < 0f)
                    up = -up;
                quads.Add(new[] { P(ua, hiA, b0), P(ub, hiB, b0), P(ub, hiB, b1), P(ua, hiA, b1) });
                normals.Add(up);
                quads.Add(new[] { P(ua, loA, b0), P(ub, loB, b0), P(ub, loB, b1), P(ua, loA, b1) });
                normals.Add(-up);
            }
            // 양 끝: 반원 (가운데에서 부채꼴) + 둘레 + 반원 아래 끊긴 곳의 세로 면
            foreach (bool top in new[] { false, true })
            {
                Vector2 c = top ? cR : cL;
                float side = top ? 1f : -1f;   // 반원이 나가는 쪽 (u)
                var arc = new List<Vector2>();
                for (int i = 0; i <= k_ArcSteps; i++)
                {
                    float a = (top ? -0.5f : 0.5f) * Mathf.PI + Mathf.PI * i / k_ArcSteps;   // 위 끝: −90° → 90°, 아래 끝: 90° → 270°
                    arc.Add(c + r * new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
                }
                for (int i = 0; i + 1 < arc.Count; i++)
                {
                    Vector2 p = arc[i], q = arc[i + 1];
                    quads.Add(new[] { P(c.x, c.y, b0), P(p.x, p.y, b0), P(q.x, q.y, b0), P(q.x, q.y, b0) });
                    normals.Add(-bDir);
                    quads.Add(new[] { P(c.x, c.y, b1), P(p.x, p.y, b1), P(q.x, q.y, b1), P(q.x, q.y, b1) });
                    normals.Add(bDir);
                    Vector2 mid = ((p + q) * 0.5f - c).normalized;
                    quads.Add(new[] { P(p.x, p.y, b0), P(q.x, q.y, b0), P(q.x, q.y, b1), P(p.x, p.y, b1) });
                    normals.Add(Flat(mid.x, mid.y).normalized);
                }
                // 반원 맨 아래 → 받침 속: 세로 면 (바깥쪽을 본다)
                float yLow = c.y - r, yFloor = line.Y(c.x) + lo;
                quads.Add(new[] { P(c.x, yFloor, b0), P(c.x, yFloor, b1), P(c.x, yLow, b1), P(c.x, yLow, b0) });
                normals.Add(aDir * side);
            }
            QuadsMesh("DeptGlass", root, quads, normals, m.deptGlass);
        }

        // 검은 고무 손잡이 — 유리 윗면을 감싸고, 아래 끝 입구 상자에서 나와 반원을 감아 올라가 경사를 따라가고, 위 끝 반원을 감아 내려와 입구 상자로 들어간다.
        // 반원 맨 아래(걷는 선 위 0.16 m)에서 안쪽으로 꺾여 받침 바로 위를 지나 입구로 — 실제 무빙워크처럼 바닥 가까이에서 들어간다.
        // 단면은 폭 8 cm × 높이 7.5 cm 둥근 사각형(유리가 2 cm 박힌다). 입구 상자는 스테인리스
        static void Handrail(Transform root, MallFrame f, WalkLine line, Vector3 aDir, Vector3 bDir, float b, Materials m)
        {
            float r = k_NewelRadius;
            Vector2 cL = NewelCenter(line, false), cR = NewelCenter(line, true);
            // 길 (u, 높이) — 아래 끝 입구 속에서 출발해 바깥(−u)으로 나와 반원(270° → 90°)을 돌고, 위를 따라 위 끝 반원(90° → −90°)을 돌아 입구 속으로
            var path = new List<Vector2> { new Vector2(cL.x + k_InletRun, cL.y - r), new Vector2(cL.x + k_InletRun * 0.5f, cL.y - r) };
            for (int i = 0; i <= k_ArcSteps; i++)
            {
                float a = 1.5f * Mathf.PI - Mathf.PI * i / k_ArcSteps;
                path.Add(cL + r * new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
            }
            foreach (float u in line.Samples(cL.x, cR.x))
                path.Add(new Vector2(u, line.Y(u) + k_GlassTop));
            for (int i = 0; i <= k_ArcSteps; i++)
            {
                float a = 0.5f * Mathf.PI - Mathf.PI * i / k_ArcSteps;
                path.Add(cR + r * new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
            }
            path.Add(new Vector2(cR.x - k_InletRun * 0.5f, cR.y - r));
            path.Add(new Vector2(cR.x - k_InletRun, cR.y - r));
            for (int i = path.Count - 1; i > 0; i--)
            {
                if ((path[i] - path[i - 1]).sqrMagnitude < 1e-6f)
                    path.RemoveAt(i);
            }

            // 단면 (w = b 쪽, h = 유리 끝에서 바깥쪽) — 둥근 사각형, 모서리마다 4점
            const float halfW = 0.04f, hMin = -0.02f, hMax = 0.055f, cr = 0.018f;
            var profile = new List<Vector2>();
            Vector2[] centers = { new Vector2(halfW - cr, hMax - cr), new Vector2(-halfW + cr, hMax - cr), new Vector2(-halfW + cr, hMin + cr), new Vector2(halfW - cr, hMin + cr) };
            for (int c = 0; c < 4; c++)
            {
                for (int k = 0; k < 4; k++)
                {
                    float a = (c + k / 3f) * 0.5f * Mathf.PI;
                    profile.Add(centers[c] + cr * new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
                }
            }
            var mid = new Vector2(0f, (hMin + hMax) * 0.5f);

            // 길의 점마다 바깥쪽(유리에서 멀어지는 쪽) — 진행 방향을 왼쪽으로 90° (반원 아래에선 아래, 바깥 끝에선 ∓u, 위를 갈 땐 위)
            var bases = new Vector3[path.Count];
            var outs = new Vector3[path.Count];
            for (int i = 0; i < path.Count; i++)
            {
                Vector2 t = (path[Mathf.Min(i + 1, path.Count - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
                outs[i] = (aDir * -t.y + Vector3.up * t.x).normalized;
                bases[i] = f.P(line.A(path[i].x), path[i].y, b);
            }
            Vector3 V(int i, int j) => bases[i] + bDir * profile[j].x + outs[i] * profile[j].y;
            var quads = new List<Vector3[]>();
            var normals = new List<Vector3>();
            for (int i = 0; i + 1 < path.Count; i++)
            {
                Vector3 o = (outs[i] + outs[i + 1]).normalized;
                for (int j = 0; j < profile.Count; j++)
                {
                    int k = (j + 1) % profile.Count;
                    Vector2 e = profile[k] - profile[j];
                    var n = new Vector2(e.y, -e.x).normalized;
                    if (Vector2.Dot(n, (profile[j] + profile[k]) * 0.5f - mid) < 0f)
                        n = -n;
                    quads.Add(new[] { V(i, j), V(i + 1, j), V(i + 1, k), V(i, k) });
                    normals.Add((bDir * n.x + o * n.y).normalized);
                }
            }
            QuadsMesh("DeptHandrail", root, quads, normals, m.deptRubber);

            // 손잡이 입구 상자 — 받침 위에 앉아 손잡이 끝(안쪽으로 꺾인 부분)과 반원 아래 유리 끝을 덮는다. 바깥쪽 면이 입구
            foreach (bool top in new[] { false, true })
            {
                Vector2 c = top ? cR : cL;
                float s = top ? -1f : 1f;   // 안쪽(입구 상자가 놓이는 쪽)
                float deck = line.Y(c.x) + 0.08f;
                MBox("DeptRailInlet", root, f, line.A(c.x + s * 0.07f), line.A(c.x + s * (k_InletRun + 0.04f)), deck, c.y - r + 0.065f,
                    b - 0.065f, b + 0.065f, m.deptStainless, false);
            }
        }

        // 보이는 메시를 떼고 콜라이더만 남긴다 — 합치기(CombineVisuals)에서 빠지고 MergeColliders 가 콜라이더를 모은다
        static void ColliderOnly(GameObject go)
        {
            var mf = go.GetComponent<MeshFilter>();
            if (mf != null)
            {
                Object.DestroyImmediate(mf.sharedMesh);
                Object.DestroyImmediate(mf);
            }
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
                Object.DestroyImmediate(mr);
        }

        // SlopedBox 와 같은 기운 상자인데 보이기만 (콜라이더 없음). 상자를 만든 뒤 돌려서 빈 묶음 오브젝트가 안 남는다 —
        // UV 는 돌리기 전 자리 기준이라 맞는 재질은 단색 또는 무늬가 고른 것(대리석)만
        static void SlopedVisual(string name, Transform parent, Vector3 from, Vector3 to, float width, float below, float above, Material mat)
        {
            Vector3 dir = to - from;
            float half = dir.magnitude * 0.5f;
            GameObject go = Box(name, parent, new Vector3(-width * 0.5f, -below, -half), new Vector3(width * 0.5f, above, half), mat, false);
            Quaternion rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            go.transform.localRotation = rotation;
            go.transform.localPosition = (from + to) * 0.5f + rotation * go.transform.localPosition;
        }

        // UV 를 직접 준 사각형 면 하나 — CombineVisuals 가 UV 를 그대로 옮긴다 (꼭짓점 차례는 QuadsMesh 와 같은 규칙으로 맞춘다)
        static void UvQuadMesh(string name, Transform root, Vector3[] v, Vector2[] uv, Vector3 normal, Material mat)
        {
            if (Vector3.Dot(Vector3.Cross(v[3] - v[0], v[1] - v[0]), normal) < 0f)
            {
                v = new[] { v[0], v[3], v[2], v[1] };
                uv = new[] { uv[0], uv[3], uv[2], uv[1] };
            }
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var mesh = new Mesh { name = name };
            mesh.vertices = v;
            mesh.uv = uv;
            mesh.normals = new[] { normal, normal, normal, normal };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        // 맵 만들기 끝(배치를 감춘 뒤) — 백화점 층마다 반사 기준점을 구워 씬 폴더(씬 이름 폴더 — 길찾기 지도와 같은 곳)에 둔다
        static void BakeDeptProbes(UnityEngine.SceneManagement.Scene scene)
        {
            s_DeptProbes.RemoveAll(p => p == null);
            if (s_DeptProbes.Count == 0)
                return;
            string parent = Path.GetDirectoryName(scene.path).Replace('\\', '/');
            string folder = $"{parent}/{scene.name}";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(parent, scene.name);
            int baked = 0;
            foreach (ReflectionProbe probe in s_DeptProbes)
            {
                string path = $"{folder}/{probe.name}.exr";
                if (!Lightmapping.BakeReflectionProbe(probe, path))
                {
                    Debug.LogWarning($"[C] 반사 기준점을 못 구웠다 — {probe.name}");
                    continue;
                }
                AssetDatabase.ImportAsset(path);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null && importer.textureShape != TextureImporterShape.TextureCube)
                {
                    importer.textureShape = TextureImporterShape.TextureCube;
                    importer.SaveAndReimport();
                }
                probe.customBakedTexture = AssetDatabase.LoadAssetAtPath<Texture>(path);
                baked++;
            }
            Debug.Log($"[C] 반사 기준점(백화점 · 가게) {baked}/{s_DeptProbes.Count}개를 구웠다 — {folder}");
        }

        // 투명 유리 (무빙워크 난간) — 비치는 재질은 CombineVisuals 가 다른 면을 가린다고 보지 않고, 그림자도 안 드리운다
        static Material TransparentMat(string name, Color color, float smoothness)
        {
            string path = $"{k_MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
                return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            BaseShaderGUI.SetMaterialKeywords(mat);
            mat.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // 빛나는 재질 (매입등 · 천창) — 보이기만, 둘레를 밝히는 건 점광원
        static Material EmissiveMat(string name, Color color, float strength)
        {
            string path = $"{k_MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
                return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_EmissionColor", color * strength);
            // None 으로 두면 URP 가 키워드를 다시 맞출 때 _EMISSION 이 꺼진다 (11/06 — 매입등이 회색 네모로 나왔다). Realtime 은 남는다
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            BaseShaderGUI.SetMaterialKeywords(mat);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
