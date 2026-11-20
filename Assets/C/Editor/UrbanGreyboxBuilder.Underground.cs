using System.Collections.Generic;
using UnityEngine;

namespace MilTrainSimVR.UrbanCombat.EditorTools
{
    // 지하 — 배치의 지하 상자(GreyboxUndergroundSpec)를 '방'(맵 축에 나란한 사각형)으로 바꿔 짓는다
    // (지하도 3안 10/23 → 5안 10/26: S 를 빼고 고리 블록 밑을 남북으로 지나는 '가운데 레인', 사용자 "FPS 3레인처럼").
    //   · 홀·통로: 바닥 -4.5m, 천장 -1.0m(위 1m 는 도로·땅 두께), 벽. 홀엔 기둥 두 줄과 가판대(엄폐물)
    //     11/18 마감: 벽 유광 타일(허리 아래 짙은 띠) · 천장 어두운 콘크리트 · 계단 화강암 + 노란 미끄럼 방지 줄 · 원기둥 · 전등 · 어두운 환경광
    //     — 재질과 합친 뒤 놓는 것(원기둥 · 전등)은 UrbanGreyboxBuilder.UgFinish.cs
    //   · 바리케이드(사용자 10/23 "대칭적인 바리케이드"): 홀의 통로 입구 앞마다 하나 + 홀 가운데 줄에 셋, 통로마다 엇갈린 둘.
    //     홀 짧은 끝에 길이 붙으면(5안) 가운데 줄 둘은 가로로 엇갈리게, 홀 쪽 길이 옆구리에 붙은 T자 통로(5안 가로 통로)는 갈래마다 하나.
    //     모두 통로 입구·홀 가운데를 기준으로 놓아서 통로가 대칭이면 바리케이드도 대칭이다
    //   · 입구(직선 계단·ㄱ자 계단·선큰 광장): 하늘이 열린 방. 벽이 바깥 땅보다 1.1m 높게 올라와 난간이 되고, 꼭대기 변만 땅과 이어진다
    //   · 맞닿은 방끼리는 맞닿은 부분이 뚫린다. 입구와 통로 사이 뚫린 곳 위(천장 높이 ~ 난간)는 막아서 천장 위 빈 곳이 안 보이게
    //   · 입구 자리만큼 바탕 땅·인도 판에 구멍을 낸다(BuildGround)
    // 벽 두께는 방 안쪽으로 넣는다 — 이웃 방 벽과 겹치지 않아 같은 면이 두 번 그려지지 않는다
    public static partial class UrbanGreyboxBuilder
    {
        const float k_UgFloor = -4.5f;      // 지하 바닥 윗면
        const float k_UgCeiling = -1.0f;    // 지하 천장 아랫면
        const float k_UgParapet = 1.1f;     // 입구 둘레 난간 벽 — 바깥 땅에서 잰 높이
        const float k_UgCanopy = 2.6f;      // 계단 지붕 아랫면 — 바깥 땅에서 잰 높이
        const float k_UgStairWidth = 2.8f;  // ㄱ자 계단 한 줄 바깥 폭 = 걷는 폭 2.4m + 양쪽 벽
        const float k_Riser = 0.17f;
        const float k_Tread = 0.3f;
        const float k_UgLanding = 1.4f;     // 긴 계단 가운데 참
        const float k_Tol = 0.05f;          // 상자끼리 이만큼 떨어져도 맞닿았다고 본다
        const float k_Barricade = 1.1f;     // 바리케이드 높이 — 앉으면 몸이 가려지고, 서면 너머로 쏠 수 있다
        const float k_BarricadeThick = 0.6f;
        // 천장 덮인 홀·통로는 렌더링 레이어 'Light Layer 1' 만 — 해(Directional Light)는 Default 만 비춰서 지하에 해가 들지 않는다.
        // 그림자는 카메라에서 50m(URP 그림자 거리)까지만 계산돼서, 그냥 두면 64m 홀 먼 쪽 벽이 해를 받아 환해진다. 총구 불빛은 두 레이어 다 비춘다
        const uint k_UgRenderingLayer = 1u << 1;

        class UgRoom
        {
            public string name;
            public Rect r;                  // 바깥 테두리 — x = 맵 x, y = 맵 z
            public bool open;               // 하늘이 열린 입구 / 천장 덮인 홀·통로
            public float grade;             // 입구 둘레 땅 높이
            public int top = -1;            // 벽 없이 땅과 이어지는 변(계단 꼭대기) — 0 남(-z) 1 북(+z) 2 서(-x) 3 동(+x)
            public System.Action<Transform, UgRoom> fill;   // 계단·마당·가구
        }

        // 지하 상자 → 방 목록. 입구 계단 꼭대기 높이는 그 앞 땅(바닥 상자)에 맞춘다
        static List<UgRoom> PlanUnderground(GreyboxUndergroundSpec[] specs, GreyboxGroundSpec[] grounds, Materials m)
        {
            var rooms = new List<UgRoom>();
            foreach (GreyboxUndergroundSpec spec in specs)
            {
                Transform t = spec.transform;
                float W = Mathf.Abs(t.lossyScale.x), D = Mathf.Abs(t.lossyScale.z);
                float yaw = Mathf.Round(t.eulerAngles.y / 90f) * 90f;
                if (Mathf.Abs(Mathf.DeltaAngle(yaw, t.eulerAngles.y)) > 1f)
                    Debug.LogWarning($"[C] {spec.name}: 지하 상자는 0·90·180·270° 로만 돌린다 — {yaw}° 로 맞춰 만든다", spec);
                Quaternion q = Quaternion.Euler(0f, yaw, 0f);
                // 건물과 같은 상자 좌표: 앞면 왼쪽 모서리가 원점, x = 앞면을 따라, z = 안쪽
                Vector3 origin = new Vector3(t.position.x, 0f, t.position.z) + q * new Vector3(-W * 0.5f, 0f, -D * 0.5f);
                System.Func<float, float, float, float, Rect> world = (x0, z0, x1, z1) =>
                {
                    Vector3 a = origin + q * new Vector3(x0, 0f, z0), b = origin + q * new Vector3(x1, 0f, z1);
                    return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.z, b.z), Mathf.Max(a.x, b.x), Mathf.Max(a.z, b.z));
                };
                int front = UgWorldEdge(q, 0);
                // 계단 꼭대기 바로 바깥(앞면 가운데에서 0.3m 밖)의 땅 높이
                float grade = GradeAt(origin + q * new Vector3(W * 0.5f, 0f, -0.3f), grounds);
                bool canopy = spec.canopy;

                switch (spec.kind)
                {
                    case GreyboxUndergroundSpec.Kind.Hall:
                        rooms.Add(new UgRoom { name = spec.name, r = world(0f, 0f, W, D), fill = (p, room) => UgHall(p, room, rooms, m) });
                        break;
                    case GreyboxUndergroundSpec.Kind.Passage:
                        rooms.Add(new UgRoom { name = spec.name, r = world(0f, 0f, W, D), fill = (p, room) => UgPassage(p, room, rooms, m) });
                        break;
                    case GreyboxUndergroundSpec.Kind.Stair:
                        rooms.Add(new UgRoom { name = spec.name, r = world(0f, 0f, W, D), open = true, grade = grade, top = front, fill = (p, room) => UgStair(p, room, canopy, m) });
                        break;
                    case GreyboxUndergroundSpec.Kind.SunkenPlaza:
                        rooms.Add(new UgRoom { name = spec.name, r = world(0f, 0f, W, D), open = true, grade = grade, top = front, fill = (p, room) => UgPlaza(p, room, m) });
                        break;
                    case GreyboxUndergroundSpec.Kind.TurnStair:
                    {
                        // ㄱ자: 앞면 왼쪽 줄로 내려가(위 계단) 안쪽 왼쪽 모서리 참에서 꺾어 안쪽 줄을 따라 오른쪽으로(아래 계단). 아래 끝(오른쪽 면)에 통로
                        float s = k_UgStairWidth;
                        if (W < s + 2f || D < s + 2f)
                        {
                            Debug.LogWarning($"[C] {spec.name}: ㄱ자 계단은 가로·세로 {s + 2f}m 이상이어야 한다 — 건너뛴다", spec);
                            break;
                        }
                        bool mirror = spec.mirror;
                        // 꼭대기는 앞면 전체가 아니라 위 계단 줄 앞 — 그 앞 땅 높이로 다시 잰다
                        grade = GradeAt(origin + q * new Vector3(mirror ? W - s * 0.5f : s * 0.5f, 0f, -0.3f), grounds);
                        float runUpper = D - s, runLower = W - s;
                        int n = Mathf.Max(2, Mathf.RoundToInt((grade - k_UgFloor) / k_Riser));
                        int nLower = Mathf.Clamp(Mathf.RoundToInt(n * runLower / (runUpper + runLower)), 1, n - 1);
                        float landingY = k_UgFloor + nLower * (grade - k_UgFloor) / n;
                        Rect upper = mirror ? world(W - s, 0f, W, D - s) : world(0f, 0f, s, D - s);
                        Rect landing = mirror ? world(W - s, D - s, W, D) : world(0f, D - s, s, D);
                        Rect lower = mirror ? world(0f, D - s, W - s, D) : world(s, D - s, W, D);
                        int lowerUp = UgWorldEdge(q, mirror ? 3 : 2);   // 아래 계단은 참 쪽으로 오른다
                        rooms.Add(new UgRoom { name = spec.name + " 위", r = upper, open = true, grade = grade, top = front,
                            fill = (p, room) => { UgFlight(p, room.name, UgSides(room.r, room.top), room.top, landingY, room.grade, n - nLower, s_Ug.step); if (canopy) UgTurnCanopy(p, upper, landing, lower, room.grade, m); } });
                        rooms.Add(new UgRoom { name = spec.name + " 참", r = landing, open = true, grade = grade,
                            fill = (p, room) => Box("Landing", p, new Vector3(room.r.xMin, k_UgFloor - k_Slab, room.r.yMin), new Vector3(room.r.xMax, landingY, room.r.yMax), s_Ug.step, true) });
                        rooms.Add(new UgRoom { name = spec.name + " 아래", r = lower, open = true, grade = grade,
                            fill = (p, room) => UgFlight(p, room.name, UgSides(room.r, lowerUp), lowerUp, k_UgFloor, landingY, nLower, s_Ug.step) });
                        break;
                    }
                }
            }
            return rooms;
        }

        static void BuildUnderground(Transform parent, List<UgRoom> rooms, Materials m)
        {
            // 지하 마감 재질 · 원기둥과 전등 자리 (UrbanGreyboxBuilder.UgFinish.cs, 11/18) — 원기둥 · 전등은 합친 뒤 UgFinish 가 놓는다
            s_Ug = UgMats(m);
            s_UgColumns.Clear();
            s_UgLamps.Clear();
            foreach (UgRoom a in rooms)
            {
                Transform holder = Group(a.name, parent);
                if (!a.open)
                {
                    Box("Floor", holder, new Vector3(a.r.xMin, k_UgFloor - k_Slab, a.r.yMin), new Vector3(a.r.xMax, k_UgFloor, a.r.yMax), m.ugFloor, true);
                    Box("Ceiling", holder, new Vector3(a.r.xMin, k_UgCeiling, a.r.yMin), new Vector3(a.r.xMax, k_UgCeiling + k_Slab, a.r.yMax), s_Ug.ceiling, true);
                    UgPlanLamps(a);
                }
                UgWalls(holder, a, rooms, m);
                a.fill?.Invoke(holder, a);
                if (!a.open)
                {
                    foreach (Renderer r in holder.GetComponentsInChildren<Renderer>())
                        r.renderingLayerMask = k_UgRenderingLayer;
                }
            }
        }

        // 방 하나의 벽. 이웃 방과 맞닿은 곳은 뚫는다(이웃이 더 좁으면 이웃의 걷는 폭만큼). 입구는 꼭대기 변을 통째로 비운다.
        // 가로 변(남·북) 벽이 모서리를 차지하고 세로 변(서·동) 벽은 그 사이만 — 모서리에서 겹치지 않게
        static void UgWalls(Transform holder, UgRoom a, List<UgRoom> rooms, Materials m)
        {
            var walls = new List<Rect>();
            var headers = new List<KeyValuePair<int, Vector2>>();
            for (int e = 0; e < 4; e++)
            {
                float s0 = e < 2 ? a.r.xMin : a.r.yMin, s1 = e < 2 ? a.r.xMax : a.r.yMax;
                var openings = new List<Vector2>();
                if (e == a.top)
                {
                    openings.Add(new Vector2(s0, s1));
                }
                else
                {
                    foreach (UgRoom b in rooms)
                    {
                        if (b == a || !UgShared(a.r, e, b.r, out Vector2 span))
                            continue;
                        // 내 변 가운데에 걸친 끝은 이웃 벽 두께만큼 좁힌다 — 내 모서리에 닿은 끝은 그대로(그쪽은 내 옆 벽이 막는다)
                        if (span.x > s0 + k_Tol)
                            span.x += k_Wall;
                        if (span.y < s1 - k_Tol)
                            span.y -= k_Wall;
                        if (span.y - span.x < 0.05f)
                            continue;
                        openings.Add(span);
                        if (a.open && !b.open)
                            headers.Add(new KeyValuePair<int, Vector2>(e, span));
                    }
                }
                foreach (Vector2 span in UgMinus(new Vector2(s0, s1), openings))
                {
                    if (e < 2)
                    {
                        walls.Add(UgStrip(a.r, e, span));
                        continue;
                    }
                    foreach (Vector2 piece in UgMinus(span, UgCovered(walls, UgStrip(a.r, e, span), e)))
                        walls.Add(UgStrip(a.r, e, piece));
                }
            }
            float wallTop = a.open ? a.grade + k_UgParapet : k_UgCeiling;
            foreach (Rect w in walls)
            {
                // 덮인 방: 허리 아래는 짙은 타일 띠, 그 위는 벽 타일. 입구(계단)는 아래가 계단에 묻히니 벽 타일 한 가지
                if (a.open)
                {
                    Box("Wall", holder, new Vector3(w.xMin, k_UgFloor, w.yMin), new Vector3(w.xMax, wallTop, w.yMax), s_Ug.wall, true);
                    continue;
                }
                Box("Wall", holder, new Vector3(w.xMin, k_UgFloor, w.yMin), new Vector3(w.xMax, k_UgFloor + k_UgBand, w.yMax), s_Ug.band, true);
                Box("Wall", holder, new Vector3(w.xMin, k_UgFloor + k_UgBand, w.yMin), new Vector3(w.xMax, wallTop, w.yMax), s_Ug.wall, true);
            }
            // 입구 → 통로로 뚫린 곳 위: 통로 천장 높이부터 난간 높이까지 막는다 (옆 벽이 차지한 모서리는 비킨다)
            foreach (KeyValuePair<int, Vector2> h in headers)
            {
                foreach (Vector2 piece in UgMinus(h.Value, UgCovered(walls, UgStrip(a.r, h.Key, h.Value), h.Key)))
                {
                    Rect r = UgStrip(a.r, h.Key, piece);
                    Box("Header", holder, new Vector3(r.xMin, k_UgCeiling, r.yMin), new Vector3(r.xMax, wallTop, r.yMax), s_Ug.wall, true);
                }
            }
        }

        // 직선 계단: 앞면(꼭대기, 땅 높이)에서 뒷면(지하 바닥)으로. 길면 가운데 참
        static void UgStair(Transform parent, UgRoom room, bool canopy, Materials m)
        {
            Rect clear = UgSides(room.r, room.top);
            float run = room.top < 2 ? clear.height : clear.width;
            int n = Mathf.Max(2, Mathf.RoundToInt((room.grade - k_UgFloor) / k_Riser));
            float landing = run - n * k_Tread;
            if (landing < 0.8f)
            {
                UgFlight(parent, room.name, clear, room.top, k_UgFloor, room.grade, n, s_Ug.step);
            }
            else
            {
                int nLower = n / 2;
                float lowerRun = nLower * k_Tread;
                float landingY = k_UgFloor + nLower * (room.grade - k_UgFloor) / n;
                UgFlight(parent, room.name, UgSlice(clear, room.top, 0f, lowerRun), room.top, k_UgFloor, landingY, nLower, s_Ug.step);
                Rect mid = UgSlice(clear, room.top, lowerRun, lowerRun + landing);
                Box("Landing", parent, new Vector3(mid.xMin, k_UgFloor - k_Slab, mid.yMin), new Vector3(mid.xMax, landingY, mid.yMax), s_Ug.step, true);
                UgFlight(parent, room.name, UgSlice(clear, room.top, lowerRun + landing, run), room.top, landingY, room.grade, n - nLower, s_Ug.step);
            }
            if (canopy)
                UgCanopy(parent, room, m);
        }

        // 선큰 광장: 앞면에서 넓은 계단(가운데 참)으로 내려가 안쪽 마당 — 마당은 하늘이 보이고 뒷면에 통로 입구
        static void UgPlaza(Transform parent, UgRoom room, Materials m)
        {
            Rect clear = UgSides(room.r, room.top);
            float run = room.top < 2 ? clear.height : clear.width;
            int n = Mathf.Max(2, Mathf.RoundToInt((room.grade - k_UgFloor) / k_Riser));
            int nLower = n / 2;
            float steps = n * k_Tread + k_UgLanding;
            float court = run - steps;
            if (court < 2f)
            {
                Debug.LogWarning($"[C] {room.name}: 선큰 광장은 계단 {steps:F1}m + 마당 2m 이상 깊어야 한다");
                court = Mathf.Max(0f, court);
            }
            Rect yard = UgSlice(room.r, room.top, 0f, court);
            Box("Court", parent, new Vector3(yard.xMin, k_UgFloor - k_Slab, yard.yMin), new Vector3(yard.xMax, k_UgFloor, yard.yMax), m.ugFloor, true);
            float a = court, b = court + nLower * k_Tread;
            float landingY = k_UgFloor + nLower * (room.grade - k_UgFloor) / n;
            UgFlight(parent, room.name, UgSlice(clear, room.top, a, b), room.top, k_UgFloor, landingY, nLower, s_Ug.step);
            Rect mid = UgSlice(clear, room.top, b, b + k_UgLanding);
            Box("Landing", parent, new Vector3(mid.xMin, k_UgFloor - k_Slab, mid.yMin), new Vector3(mid.xMax, landingY, mid.yMax), s_Ug.step, true);
            UgFlight(parent, room.name, UgSlice(clear, room.top, b + k_UgLanding, run), room.top, landingY, room.grade, n - nLower, s_Ug.step);
        }

        // 홀: 기둥 두 줄(8m 간격, 긴 벽에서 3.3m 안쪽) + 가판대 넷(가운데에서 ±6·±18m, 가운데 줄에서 ±1.5m 번갈아).
        // 홀 가운데를 축으로 180° 돌려도 같은 배치라 남북 어느 진영으로 들어와도 엄폐물이 같다. 바깥 둘은 서서 가리는 2.2m, 안쪽 둘은 앉으면 가리는 1.1m
        static void UgHall(Transform parent, UgRoom room, List<UgRoom> rooms, Materials m)
        {
            Rect c = Rect.MinMaxRect(room.r.xMin + k_Wall, room.r.yMin + k_Wall, room.r.xMax - k_Wall, room.r.yMax - k_Wall);
            bool alongX = c.width >= c.height;
            float length = alongX ? c.width : c.height, span = alongX ? c.height : c.width;
            int columns = Mathf.Max(1, Mathf.FloorToInt((length - 12f) / 8f) + 1);
            float row = span * 0.5f - 3.3f;
            for (int i = 0; i < columns; i++)
            {
                float u = (i - (columns - 1) * 0.5f) * 8f;
                // 원기둥 (11/18 — 그 전엔 0.8 m 네모 상자). 상자가 아니라 합친 뒤 UgFinish 가 놓는다 — 여기선 자리만
                foreach (float v in new[] { -row, row })
                {
                    Vector2 p = c.center + (alongX ? new Vector2(u, v) : new Vector2(v, u));
                    s_UgColumns.Add(new Vector3(p.x, 0f, p.y));
                }
            }
            float[] along = { -18f, -6f, 6f, 18f };
            float[] across = { 1.5f, -1.5f, 1.5f, -1.5f };
            float[] height = { 2.2f, 1.1f, 1.1f, 2.2f };
            for (int k = 0; k < along.Length; k++)
            {
                if (Mathf.Abs(along[k]) + 1.5f < length * 0.5f - 2f)
                    Skin(UgPiece(parent, "Kiosk", c, alongX, along[k], across[k], 3f, 1.5f, k_UgFloor, k_UgFloor + height[k], m.furniture), height[k] > 2f ? "KioskTall" : "KioskLow");
            }

            // 바리케이드 ① 통로 입구마다 홀 안쪽 2.5m 에 가림막 — 입구 걷는 폭보다 양쪽 0.8m 씩 짧아서 어느 쪽으로든 돌아 나온다.
            // 통로에서 나오는 사람이 바로 홀 맞은편에 드러나지 않고, 홀에서 통로 안을 곧장 쏘지도 못한다
            foreach (UgRoom b in rooms)
            {
                if (b == room || b.open)
                    continue;
                for (int e = 0; e < 4; e++)
                {
                    if (!UgShared(room.r, e, b.r, out Vector2 shared))
                        continue;
                    float s0 = shared.x + k_Wall + 0.8f, s1 = shared.y - k_Wall - 0.8f;
                    float d0 = k_Wall + 2.5f, d1 = d0 + k_BarricadeThick;
                    Rect r = e == 0 ? Rect.MinMaxRect(s0, room.r.yMin + d0, s1, room.r.yMin + d1)
                           : e == 1 ? Rect.MinMaxRect(s0, room.r.yMax - d1, s1, room.r.yMax - d0)
                           : e == 2 ? Rect.MinMaxRect(room.r.xMin + d0, s0, room.r.xMin + d1, s1)
                           : Rect.MinMaxRect(room.r.xMax - d1, s0, room.r.xMax - d0, s1);
                    UgBarricade(parent, r, m);
                }
            }
            // ② 가운데 줄에 셋 — 한가운데 것은 홀 긴 방향 사격을 막는다. 가판대·기둥 사이 빈자리.
            // ±12m 둘은 길이 홀 어디에 붙었는지에 따라:
            //   · 긴 변(3안 — 두 진영이 홀 맞은편에서 들어온다): 홀을 따라 길게 놓아 맞은편 사격을 막는다
            //   · 짧은 끝(5안 — 홀이 두 진영을 잇는 가운데 길): 긴 방향 사격을 막게 가로로 놓고 한쪽 벽 가까이 엇갈리게
            //     (-12m 는 왼쪽, +12m 는 오른쪽 — 홀 가운데를 축으로 180° 돌려도 같은 자리라 두 진영이 같다)
            Skin(UgPiece(parent, "Barricade", c, alongX, 0f, 0f, k_BarricadeThick, 2.4f, k_UgFloor, k_UgFloor + k_Barricade, m.barricade), "ConcreteBlock");
            bool lane = HallLane(room, rooms, alongX);
            foreach (float u in new[] { -12f, 12f })
            {
                if (Mathf.Abs(u) + 1.5f >= length * 0.5f - 2f)
                    continue;
                if (lane)
                    Skin(UgPiece(parent, "Barricade", c, alongX, u, Mathf.Sign(u) * 3f, k_BarricadeThick, 3f, k_UgFloor, k_UgFloor + k_Barricade, m.barricade), "ConcreteBlock");
                else
                    Skin(UgPiece(parent, "Barricade", c, alongX, u, 0f, 3f, k_BarricadeThick, k_UgFloor, k_UgFloor + k_Barricade, m.barricade), "ConcreteBlock");
            }
        }

        // 덮인 길(통로)이 홀의 짧은 끝에 붙었는지 — 그러면 홀은 두 끝을 잇는 '가운데 길'이다 (5안)
        static bool HallLane(UgRoom hall, List<UgRoom> rooms, bool alongX)
        {
            int e0 = alongX ? 2 : 0;   // 동서로 긴 홀의 짧은 끝 = 서·동 변, 남북으로 긴 홀은 남·북 변
            foreach (UgRoom b in rooms)
            {
                if (b == hall || b.open)
                    continue;
                for (int e = e0; e < e0 + 2; e++)
                {
                    if (UgShared(hall.r, e, b.r, out Vector2 _))
                        return true;
                }
            }
            return false;
        }

        // 통로: 홀 쪽 끝에서 7m·13m 에 엇갈린 바리케이드 둘(걷는 폭의 55%) — 먼저 홀 가운데 쪽 벽에, 다음은 바깥 벽에 붙인다.
        // 지그재그로 지나가게 해서 통로 끝까지 곧게 뚫린 시야(23~26m)를 끊는다
        static void UgPassage(Transform parent, UgRoom room, List<UgRoom> rooms, Materials m)
        {
            // 맞닿은 덮인 방 중 가장 큰 것 = 홀
            UgRoom hall = null;
            int end = -1;
            foreach (UgRoom b in rooms)
            {
                if (b == room || b.open)
                    continue;
                for (int e = 0; e < 4; e++)
                {
                    if (UgShared(room.r, e, b.r, out Vector2 _) && (hall == null || b.r.width * b.r.height > hall.r.width * hall.r.height))
                    {
                        hall = b;
                        end = e;
                    }
                }
            }
            if (hall == null)
                return;
            // 홀 쪽 길이 통로 끝이 아니라 옆구리에 붙었으면(5안 가로 통로 — T자) 갈래마다 하나씩
            bool longX = room.r.width > room.r.height;
            if (longX == (end < 2))
            {
                UgSideBranches(parent, room, hall, end, m);
                return;
            }
            bool alongZ = end < 2;   // 홀이 남·북 끝에 붙었으면 통로는 남북으로 뻗는다
            Rect c = alongZ ? Rect.MinMaxRect(room.r.xMin + k_Wall, room.r.yMin, room.r.xMax - k_Wall, room.r.yMax)
                            : Rect.MinMaxRect(room.r.xMin, room.r.yMin + k_Wall, room.r.xMax, room.r.yMax - k_Wall);
            float length = alongZ ? c.height : c.width, width = alongZ ? c.width : c.height;
            if (length < 16f)
                return;
            bool innerHigh = alongZ ? hall.r.center.x > c.center.x : hall.r.center.y > c.center.y;   // 홀 가운데 쪽 옆 벽이 좌표 큰 쪽인지
            float lo = alongZ ? c.xMin : c.yMin, hi = alongZ ? c.xMax : c.yMax;
            float[] dist = { 7f, 13f };
            for (int k = 0; k < dist.Length; k++)
            {
                bool high = k == 0 ? innerHigh : !innerHigh;
                float a = end == 0 ? c.yMin + dist[k] : end == 1 ? c.yMax - dist[k] : end == 2 ? c.xMin + dist[k] : c.xMax - dist[k];
                float s0 = high ? hi - width * 0.55f : lo, s1 = high ? hi : lo + width * 0.55f;
                float a0 = a - k_BarricadeThick * 0.5f, a1 = a + k_BarricadeThick * 0.5f;
                UgBarricade(parent, alongZ ? Rect.MinMaxRect(s0, a0, s1, a1) : Rect.MinMaxRect(a0, s0, a1, s1), m);
            }
        }

        // T자 통로: 홀 쪽 길(neighbor)이 통로 옆구리(변 end)에 붙었다. 갈래마다 붙은 곳에서 6m 에 가림막 하나(걷는 폭의 55%) —
        // 계단에서 내려와 갈래를 따라 홀 쪽으로 갈 때 숨을 자리. 붙은 곳에서 바깥(홀 반대쪽)을 보고 왼쪽 갈래는 홀 쪽 벽에,
        // 오른쪽 갈래는 바깥 벽에 붙인다 — 홀 가운데를 축으로 180° 돌린 맞은편 통로에서도 같은 자리가 된다
        static void UgSideBranches(Transform parent, UgRoom room, UgRoom neighbor, int end, Materials m)
        {
            if (!UgShared(room.r, end, neighbor.r, out Vector2 joint))
                return;
            bool alongX = end < 2;   // 옆구리가 남·북 변이면 통로는 동서로 뻗는다
            Rect c = alongX ? Rect.MinMaxRect(room.r.xMin, room.r.yMin + k_Wall, room.r.xMax, room.r.yMax - k_Wall)
                            : Rect.MinMaxRect(room.r.xMin + k_Wall, room.r.yMin, room.r.xMax - k_Wall, room.r.yMax);
            float lo = alongX ? c.xMin : c.yMin, hi = alongX ? c.xMax : c.yMax;            // 뻗은 방향 양 끝
            float side0 = alongX ? c.yMin : c.xMin, side1 = alongX ? c.yMax : c.xMax;    // 걷는 폭 양쪽 벽
            float span = (side1 - side0) * 0.55f;
            bool hallHigh = end == 1 || end == 3;   // 홀 쪽 벽이 좌표 큰 쪽(북·동)인지
            bool leftHigh = end == 1 || end == 2;   // 바깥을 볼 때 왼쪽 갈래가 좌표 큰 쪽인지 (남을 보면 왼쪽은 동, 북을 보면 서 …)
            const float dist = 6f;
            foreach (bool high in new[] { false, true })
            {
                float from = high ? joint.y : joint.x, to = high ? hi : lo;
                if (Mathf.Abs(to - from) < dist + 2f)
                    continue;
                float a = from + (high ? dist : -dist);
                bool hallWall = high == leftHigh;
                bool atHigh = hallWall ? hallHigh : !hallHigh;
                float s0 = atHigh ? side1 - span : side0, s1 = atHigh ? side1 : side0 + span;
                float a0 = a - k_BarricadeThick * 0.5f, a1 = a + k_BarricadeThick * 0.5f;
                UgBarricade(parent, alongX ? Rect.MinMaxRect(a0, s0, a1, s1) : Rect.MinMaxRect(s0, a0, s1, a1), m);
            }
        }

        // 겉모양은 콘크리트 블록 모델을 이어 붙인다 (UrbanGreyboxBuilder.Props.cs)
        static void UgBarricade(Transform parent, Rect r, Materials m)
        {
            Skin(Box("Barricade", parent, new Vector3(r.xMin, k_UgFloor, r.yMin), new Vector3(r.xMax, k_UgFloor + k_Barricade, r.yMax), m.barricade, true), "ConcreteBlock");
        }

        // 계단 지붕: 방 위 땅에서 2.6m, 네 모서리 기둥은 난간 벽 위에
        static void UgCanopy(Transform parent, UgRoom room, Materials m)
        {
            Rect r = room.r;
            float y0 = room.grade + k_UgCanopy;
            Box("Canopy", parent, new Vector3(r.xMin, y0, r.yMin), new Vector3(r.xMax, y0 + 0.15f, r.yMax), m.glass, true);
            float yb = room.grade + k_UgParapet;
            foreach (Vector2 p in new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax - 0.15f, r.yMin), new Vector2(r.xMin, r.yMax - 0.15f), new Vector2(r.xMax - 0.15f, r.yMax - 0.15f) })
                Box("CanopyPost", parent, new Vector3(p.x, yb, p.y), new Vector3(p.x + 0.15f, y0, p.y + 0.15f), m.dark, true);
        }

        // ㄱ자 계단 지붕 (사용자 11/17 — 남동 입구에 천장이 없다): 위 계단 · 참 · 아래 계단을 다 덮는다 — 위 계단 + 참은 한 판(같은 폭 줄), 아래 계단은 또 한 판.
        // 높이는 직선 계단 지붕과 같이 꼭대기 땅에서 2.6 m, 기둥은 ㄱ자 바깥 모서리 다섯과 안쪽 꺾인 모서리 하나 (난간 벽 위)
        static void UgTurnCanopy(Transform parent, Rect upper, Rect landing, Rect lower, float grade, Materials m)
        {
            float y0 = grade + k_UgCanopy, yb = grade + k_UgParapet;
            Rect column = Rect.MinMaxRect(Mathf.Min(upper.xMin, landing.xMin), Mathf.Min(upper.yMin, landing.yMin), Mathf.Max(upper.xMax, landing.xMax), Mathf.Max(upper.yMax, landing.yMax));
            foreach (Rect r in new[] { column, lower })
                Box("Canopy", parent, new Vector3(r.xMin, y0, r.yMin), new Vector3(r.xMax, y0 + 0.15f, r.yMax), m.glass, true);
            // 기둥: 두 판의 네 모서리 중 다른 판 안에 들어가지 않는 것
            var posts = new List<Vector2>();
            foreach (Rect r in new[] { column, lower })
                foreach (Vector2 c in new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax - 0.15f, r.yMin), new Vector2(r.xMin, r.yMax - 0.15f), new Vector2(r.xMax - 0.15f, r.yMax - 0.15f) })
                {
                    Rect other = r == column ? lower : column;
                    Vector2 mid = c + new Vector2(0.075f, 0.075f);
                    if (other.Contains(mid) || posts.Exists(q => (q - c).sqrMagnitude < 0.04f))
                        continue;
                    posts.Add(c);
                }
            foreach (Vector2 p in posts)
                Box("CanopyPost", parent, new Vector3(p.x, yb, p.y), new Vector3(p.x + 0.15f, y0, p.y + 0.15f), m.dark, true);
        }

        // 계단 한 줄: clear 안에서 up 변(올라가는 쪽)으로 갈수록 높아진다. 보이는 단은 지하 바닥판 밑까지 꽉 채우고, 밟는 건 보이지 않는 경사로
        static void UgFlight(Transform parent, string name, Rect clear, int up, float y0, float y1, int risers, Material mat)
        {
            bool alongZ = up < 2;
            float lo = alongZ ? clear.yMin : clear.xMin, hi = alongZ ? clear.yMax : clear.xMax;
            float bottom = up == 1 || up == 3 ? lo : hi, top = up == 1 || up == 3 ? hi : lo;
            for (int i = 0; i < risers; i++)
            {
                float u0 = Mathf.Lerp(bottom, top, (float)i / risers), u1 = Mathf.Lerp(bottom, top, (float)(i + 1) / risers);
                float y = Mathf.Lerp(y0, y1, (float)(i + 1) / risers);
                Vector3 a = alongZ ? new Vector3(clear.xMin, k_UgFloor - k_Slab, u0) : new Vector3(u0, k_UgFloor - k_Slab, clear.yMin);
                Vector3 b = alongZ ? new Vector3(clear.xMax, y, u1) : new Vector3(u1, y, clear.yMax);
                Box("Step", parent, Vector3.Min(a, b), Vector3.Max(a, b), mat, false);
                // 디딤판 앞 끝(내려가는 쪽)에 노란 미끄럼 방지 줄 — 폭 5 cm, 4 mm 도드라짐. 어두운 지하에서 단이 보인다
                float edge = Mathf.MoveTowards(u0, u1, 0.05f);
                Vector3 n0 = alongZ ? new Vector3(clear.xMin, y, u0) : new Vector3(u0, y, clear.yMin);
                Vector3 n1 = alongZ ? new Vector3(clear.xMax, y + 0.004f, edge) : new Vector3(edge, y + 0.004f, clear.yMax);
                Box("StepNose", parent, Vector3.Min(n0, n1), Vector3.Max(n0, n1), s_Ug.nose, false);
            }
            float mid = alongZ ? clear.center.x : clear.center.y;
            Vector3 from = alongZ ? new Vector3(mid, y0, bottom) : new Vector3(bottom, y0, mid);
            Vector3 to = alongZ ? new Vector3(mid, y1, top) : new Vector3(top, y1, mid);
            Ramp(name + "_Ramp", parent, from, to, alongZ ? clear.width : clear.height);
        }

        // 홀 가운데 기준 (긴 방향 u, 짧은 방향 v) 자리에 가로 lu · 세로 lv 상자
        static GameObject UgPiece(Transform parent, string name, Rect c, bool alongX, float u, float v, float lu, float lv, float y0, float y1, Material mat)
        {
            Vector2 p = c.center + (alongX ? new Vector2(u, v) : new Vector2(v, u));
            Vector2 half = (alongX ? new Vector2(lu, lv) : new Vector2(lv, lu)) * 0.5f;
            return Box(name, parent, new Vector3(p.x - half.x, y0, p.y - half.y), new Vector3(p.x + half.x, y1, p.y + half.y), mat, true);
        }

        // ---------------------------------------------------------------- 지하용 작은 도구 (변 번호: 0 남 1 북 2 서 3 동)

        // 상자 로컬 변(0 앞 -z, 1 뒤 +z, 2 왼 -x, 3 오른 +x)이 맵에서 어느 변인지
        static int UgWorldEdge(Quaternion q, int localEdge)
        {
            Vector3 n = localEdge == 0 ? Vector3.back : localEdge == 1 ? Vector3.forward : localEdge == 2 ? Vector3.left : Vector3.right;
            Vector3 d = q * n;
            if (Mathf.Abs(d.x) > Mathf.Abs(d.z))
                return d.x > 0f ? 3 : 2;
            return d.z > 0f ? 1 : 0;
        }

        // 계단이 달리는 방향으로는 그대로, 양옆만 벽 두께만큼 줄인 사각형 (걷는 폭)
        static Rect UgSides(Rect r, int up)
        {
            return up < 2 ? Rect.MinMaxRect(r.xMin + k_Wall, r.yMin, r.xMax - k_Wall, r.yMax) : Rect.MinMaxRect(r.xMin, r.yMin + k_Wall, r.xMax, r.yMax - k_Wall);
        }

        // 계단 아래 끝(up 반대쪽 변)에서 a ~ b 만큼 떨어진 띠
        static Rect UgSlice(Rect r, int up, float a, float b)
        {
            switch (up)
            {
                case 1: return Rect.MinMaxRect(r.xMin, r.yMin + a, r.xMax, r.yMin + b);
                case 0: return Rect.MinMaxRect(r.xMin, r.yMax - b, r.xMax, r.yMax - a);
                case 3: return Rect.MinMaxRect(r.xMin + a, r.yMin, r.xMin + b, r.yMax);
                default: return Rect.MinMaxRect(r.xMax - b, r.yMin, r.xMax - a, r.yMax);
            }
        }

        // 변 e 를 따라 span 구간의 벽 자리 (방 안쪽으로 두께 k_Wall)
        static Rect UgStrip(Rect r, int e, Vector2 span)
        {
            switch (e)
            {
                case 0: return Rect.MinMaxRect(span.x, r.yMin, span.y, r.yMin + k_Wall);
                case 1: return Rect.MinMaxRect(span.x, r.yMax - k_Wall, span.y, r.yMax);
                case 2: return Rect.MinMaxRect(r.xMin, span.x, r.xMin + k_Wall, span.y);
                default: return Rect.MinMaxRect(r.xMax - k_Wall, span.x, r.xMax, span.y);
            }
        }

        // 내 변 e 와 이웃 o 의 맞은편 변이 같은 선에 있으면 겹친 구간
        static bool UgShared(Rect r, int e, Rect o, out Vector2 span)
        {
            span = Vector2.zero;
            bool sameLine;
            switch (e)
            {
                case 0: sameLine = Mathf.Abs(r.yMin - o.yMax) < k_Tol; break;
                case 1: sameLine = Mathf.Abs(r.yMax - o.yMin) < k_Tol; break;
                case 2: sameLine = Mathf.Abs(r.xMin - o.xMax) < k_Tol; break;
                default: sameLine = Mathf.Abs(r.xMax - o.xMin) < k_Tol; break;
            }
            if (!sameLine)
                return false;
            span = e < 2 ? new Vector2(Mathf.Max(r.xMin, o.xMin), Mathf.Min(r.xMax, o.xMax)) : new Vector2(Mathf.Max(r.yMin, o.yMin), Mathf.Min(r.yMax, o.yMax));
            return span.y - span.x > k_Tol;
        }

        // 이미 놓인 벽들이 띠 strip 에서 차지한 구간 (변 e 를 따라 잰 값)
        static List<Vector2> UgCovered(List<Rect> walls, Rect strip, int e)
        {
            var covered = new List<Vector2>();
            foreach (Rect w in walls)
            {
                if (w.xMax - strip.xMin < 0.001f || strip.xMax - w.xMin < 0.001f || w.yMax - strip.yMin < 0.001f || strip.yMax - w.yMin < 0.001f)
                    continue;
                covered.Add(e < 2 ? new Vector2(w.xMin, w.xMax) : new Vector2(w.yMin, w.yMax));
            }
            return covered;
        }

        // 구간에서 구간들을 뺀 나머지
        static List<Vector2> UgMinus(Vector2 span, List<Vector2> cuts)
        {
            var result = new List<Vector2> { span };
            foreach (Vector2 c in cuts)
            {
                var next = new List<Vector2>();
                foreach (Vector2 s in result)
                {
                    if (c.y <= s.x || c.x >= s.y)
                    {
                        next.Add(s);
                        continue;
                    }
                    if (c.x - s.x > 0.01f)
                        next.Add(new Vector2(s.x, c.x));
                    if (s.y - c.y > 0.01f)
                        next.Add(new Vector2(c.y, s.y));
                }
                result = next;
            }
            return result;
        }

        // 그 자리 땅 윗면 높이 — 겹친 바닥 상자 중 가장 높은 것 (샛길 0.16 · 인도 0.15 · 주차장 0.012 · 그 밖은 차도 0)
        static float GradeAt(Vector3 p, GreyboxGroundSpec[] grounds)
        {
            float best = 0f;
            foreach (GreyboxGroundSpec g in grounds)
            {
                Transform t = g.transform;
                Vector3 local = Quaternion.Inverse(Quaternion.Euler(0f, t.eulerAngles.y, 0f)) * (p - new Vector3(t.position.x, 0f, t.position.z));
                if (Mathf.Abs(local.x) > Mathf.Abs(t.lossyScale.x) * 0.5f || Mathf.Abs(local.z) > Mathf.Abs(t.lossyScale.z) * 0.5f)
                    continue;
                float h = g.kind == GreyboxGroundSpec.Kind.Alley ? k_Curb + 0.01f : g.kind == GreyboxGroundSpec.Kind.Sidewalk ? k_Curb : g.kind == GreyboxGroundSpec.Kind.Lot ? 0.012f : 0f;
                best = Mathf.Max(best, h);
            }
            return best;
        }

        // 사각형에서 구멍들을 뺀 나머지를 사각형 몇 개로 — 구멍 경계로 칸을 나눠 줄마다 가로로 잇고, 위아래로 같은 모양인 조각은 합친다
        static List<Rect> RectMinus(Rect a, List<Rect> holes)
        {
            var cut = new List<Rect>();
            if (holes != null)
            {
                foreach (Rect h in holes)
                {
                    Rect c = Rect.MinMaxRect(Mathf.Max(a.xMin, h.xMin), Mathf.Max(a.yMin, h.yMin), Mathf.Min(a.xMax, h.xMax), Mathf.Min(a.yMax, h.yMax));
                    if (c.width > 0.001f && c.height > 0.001f)
                        cut.Add(c);
                }
            }
            if (cut.Count == 0)
                return new List<Rect> { a };
            var xs = new SortedSet<float> { a.xMin, a.xMax };
            var zs = new SortedSet<float> { a.yMin, a.yMax };
            foreach (Rect c in cut)
            {
                xs.Add(c.xMin);
                xs.Add(c.xMax);
                zs.Add(c.yMin);
                zs.Add(c.yMax);
            }
            var X = new List<float>(xs);
            var Z = new List<float>(zs);
            var result = new List<Rect>();
            var growing = new List<Vector3>();   // 위로 늘려 가는 조각 (x 시작, x 끝, z 시작)
            for (int j = 0; j < Z.Count - 1; j++)
            {
                var spans = new List<Vector2>();
                for (int i = 0; i < X.Count - 1; i++)
                {
                    var cell = new Vector2((X[i] + X[i + 1]) * 0.5f, (Z[j] + Z[j + 1]) * 0.5f);
                    bool hole = false;
                    foreach (Rect c in cut)
                        hole |= c.Contains(cell);
                    if (hole)
                        continue;
                    if (spans.Count > 0 && spans[spans.Count - 1].y == X[i])
                        spans[spans.Count - 1] = new Vector2(spans[spans.Count - 1].x, X[i + 1]);
                    else
                        spans.Add(new Vector2(X[i], X[i + 1]));
                }
                var next = new List<Vector3>();
                foreach (Vector2 s in spans)
                {
                    int k = growing.FindIndex(g => g.x == s.x && g.y == s.y);
                    next.Add(new Vector3(s.x, s.y, k >= 0 ? growing[k].z : Z[j]));
                    if (k >= 0)
                        growing.RemoveAt(k);
                }
                foreach (Vector3 g in growing)
                    result.Add(Rect.MinMaxRect(g.x, g.z, g.y, Z[j]));
                growing = next;
            }
            foreach (Vector3 g in growing)
                result.Add(Rect.MinMaxRect(g.x, g.z, g.y, Z[Z.Count - 1]));
            return result;
        }
    }
}
