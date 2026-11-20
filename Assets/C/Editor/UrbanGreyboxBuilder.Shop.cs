using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MilTrainSimVR.UrbanCombat.EditorTools
{
    // 1층 가챠 가게 (사용자 11/09 — 뫄뫄굿즈 N24. 메모리 목업 확인 → 계단 양옆 벽 · 기계 수를 줄여 적용)
    //   · 1층 칸막이 없음. 2층 가는 계단은 양옆 벽 사이 — 계단 아래 끝(앞쪽)으로만 들어간다
    //   · 옆문 쪽 벽에 큰 입구(폭 4.4 m · 높이 2.35 m — 옆문 위 간판 밑), 1층 창 없음, 앞문은 그대로 (BuildInterior 의 shopFloor)
    //   · 입구 맞은편 벽 · 뒷벽을 따라 인형뽑기 (뒷벽은 계단 자리 양쪽으로 끊음), 맞은편 벽 가운데 · 입구 바로 옆에 지폐 교환기
    //   · 가운데 등을 맞댄 가챠 섬: 입구에서 곧게 가는 통로(폭 3.8 m) 양옆, 섬마다 앞문 가로 통로 · 계단 자리 양쪽으로 한 토막씩(토막당 한 줄 6대)
    //   · 기계는 상자(부딪힘 · 총알 · 적 길찾기)에 모델을 씌운다 — 모델은 Models/Tripo 의 ClawMachine · CapsuleMachine · CoinChanger (Props.cs 의 exact)
    //   · 가게 조명: 그림자 없는 점 조명 격자 (청운백화점과 같은 방식)
    public static partial class UrbanGreyboxBuilder
    {
        const float k_ShopEntranceHalf = 2.2f;     // 큰 입구 폭의 반
        const float k_ShopEntranceHeight = 2.35f;  // 큰 입구 높이 — 옆문 위 간판(밑면 = 바닥 + 약 2.45 m) 밑

        // 기계 상자 크기 = 모델 실제 비율 (앞에서 본 폭 · 높이 · 깊이). 가챠 깊이는 모델보다 얕게 — 실제 가챠는 0.5 m 남짓이라 등을 맞대도 섬이 두껍지 않게
        static readonly Vector3 k_ClawBox = new Vector3(1.49f, 1.90f, 1.21f);
        static readonly Vector3 k_GachaBox = new Vector3(0.484f, 1.60f, 0.547f);
        static readonly Vector3 k_ChangerBox = new Vector3(0.66f, 1.50f, 0.73f);

        // 계단 양옆 벽 (가게 층) — 왼쪽은 복도 왼쪽 벽 자리, 오른쪽은 계단 띠 오른쪽 끝. 계단 아래 끝 조금 앞에서 뒷벽까지
        static void ShopStairWalls(Transform root, InteriorPlan p, float y, float height, Material wall)
        {
            float z0 = p.stairZ0 - 0.3f, z1 = p.D - k_Wall;
            if (p.leftRooms)   // 왼쪽에 방이 없는 좁은 건물은 왼쪽 바깥벽이 대신한다
                Wall("ShopStairWall_L", root, new Vector3(p.hall0, y, z0), new Vector3(p.hall0, y, z1), Vector3.left, null, wall, k_Partition, height);
            float xr = p.hall0 + k_StairStrip;
            Wall("ShopStairWall_R", root, new Vector3(xr, y, z0), new Vector3(xr, y, z1), Vector3.right, null, wall, k_Partition, height);
        }

        // 기계 상자 하나 — 바닥 가운데 (x, z), 앞이 보는 방향(건물 기준), 크기(폭 · 높이 · 깊이), variant: 바꿔 끼울 재질 순서(PropKind.variants)
        static GameObject Machine(Transform root, string kind, float x, float z, float y, Vector3 face, Vector3 size, Material mat, int variant = -1)
        {
            bool frontZ = Mathf.Abs(face.z) > 0.5f;   // 앞이 ±z 면 폭이 x 방향
            float hx = (frontZ ? size.x : size.z) * 0.5f, hz = (frontZ ? size.z : size.x) * 0.5f;
            GameObject go = Box("Machine_" + kind, root, new Vector3(x - hx, y, z - hz), new Vector3(x + hx, y + size.y, z + hz), mat, true);
            Skin(go, kind, face, variant);
            return go;
        }

        // entranceSide: 큰 입구가 난 벽(2 왼 · 3 오른, 없으면 -1 → 왼쪽으로 친다), ec: 입구 가운데(z)
        static void FurnishGachaShop(Transform root, InteriorPlan p, float y, float height, int entranceSide, float ec, Material mat)
        {
            float W = p.W, D = p.D;
            bool left = entranceSide != 3;
            Vector3 intoRoom = left ? Vector3.right : Vector3.left;
            // 계단 자리(양옆 벽 포함) — 기계를 두지 않는다
            float stx0 = p.hall0 - k_Partition - 0.3f, stx1 = p.hall0 + k_StairStrip + k_Partition + 0.3f, stz0 = p.stairZ0 - 1.3f;
            bool InStair(float x0, float x1, float z1) => p.stairs && x1 > stx0 && x0 < stx1 && z1 > stz0;
            float doorX0 = p.lane0 - 0.6f, doorX1 = p.lane1 + 0.6f;   // 앞문에서 들어오는 가로 통로
            Vector3 claw = k_ClawBox, gacha = k_GachaBox, ch = k_ChangerBox;
            float step = claw.x + 0.04f;
            int nClaw = 0, nGacha = 0;

            // 1) 입구 맞은편 벽 — 인형뽑기 줄(9칸까지), 가운데 칸은 지폐 교환기(입구에서 가장 먼 곳)
            float farX = left ? W - k_Wall - 0.03f : k_Wall + 0.03f;
            float rz0 = k_Wall + 0.25f, rz1 = D - k_Wall - claw.z - 0.25f;   // 뒤쪽 모서리는 뒷벽 줄
            int n = Mathf.Min(9, Mathf.FloorToInt((rz1 - rz0) / step));
            float start = (rz0 + rz1) * 0.5f - (n - 1) * step * 0.5f;
            for (int i = 0; i < n; i++)
            {
                float z = start + i * step;
                if (i == n / 2)
                {
                    Machine(root, "CoinChanger", left ? farX - ch.z * 0.5f : farX + ch.z * 0.5f, z, y, -intoRoom, ch, mat);
                    continue;
                }
                Machine(root, "ClawMachine", left ? farX - claw.z * 0.5f : farX + claw.z * 0.5f, z, y, -intoRoom, claw, mat);
                nClaw++;
            }

            // 2) 뒷벽 — 인형뽑기, 계단 자리 양쪽 토막마다 5대까지 (맞은편 벽 쪽 모서리는 1) 줄)
            float bz = D - k_Wall - 0.03f - claw.z * 0.5f;
            float bx0 = left ? k_Wall + 0.25f : k_Wall + claw.z + 0.25f, bx1 = left ? W - k_Wall - claw.z - 0.25f : W - k_Wall - 0.25f;
            var spans = new List<Vector2>();
            if (p.stairs)
            {
                spans.Add(new Vector2(bx0, Mathf.Min(bx1, stx0)));
                spans.Add(new Vector2(Mathf.Max(bx0, stx1), bx1));
            }
            else
            {
                spans.Add(new Vector2(bx0, bx1));
            }
            foreach (Vector2 s in spans)
            {
                int m = Mathf.Min(5, Mathf.FloorToInt((s.y - s.x) / step));
                for (int i = 0; i < m; i++)
                {
                    Machine(root, "ClawMachine", (s.x + s.y) * 0.5f + (i - (m - 1) * 0.5f) * step, bz, y, Vector3.back, claw, mat);
                    nClaw++;
                }
            }

            // 3) 입구 바로 안쪽 옆(뒤쪽) — 지폐 교환기
            Machine(root, "CoinChanger", left ? k_Wall + 0.03f + ch.z * 0.5f : W - k_Wall - 0.03f - ch.z * 0.5f,
                ec + k_ShopEntranceHalf + 0.15f + ch.x * 0.5f, y, intoRoom, ch, mat);

            // 4) 가운데 가챠 섬 — 입구에서 곧게 가는 통로 양옆, 등을 맞댄 두 줄. 섬마다 앞문 가로 통로(+ 뒤쪽 섬은 계단 자리) 양쪽으로 한 토막
            const float aisle = 1.9f;
            float near = left ? 3.6f : W - 3.6f, far = left ? W - k_Wall - claw.z - 1.5f : k_Wall + claw.z + 1.5f;
            float lo = Mathf.Min(near, far), hi = Mathf.Max(near, far);
            foreach (float zc in new[] { ec - aisle - gacha.z, ec + aisle + gacha.z })
            {
                float bl0 = doorX0, bl1 = doorX1;
                if (InStair(lo, hi, zc + gacha.z))
                {
                    bl0 = Mathf.Min(bl0, stx0);
                    bl1 = Mathf.Max(bl1, stx1);
                }
                foreach (Vector2 seg in new[] { new Vector2(lo, bl0 - 0.3f), new Vector2(bl1 + 0.3f, hi) })
                {
                    int m = Mathf.Min(6, Mathf.FloorToInt((seg.y - seg.x) / gacha.x));
                    if (m < 2)
                        continue;
                    for (int i = 0; i < m; i++)
                    {
                        float x = (seg.x + seg.y) * 0.5f + (i - (m - 1) * 0.5f) * gacha.x;
                        Machine(root, "CapsuleMachine", x, zc - gacha.z * 0.5f, y, Vector3.back, gacha, mat);
                        Machine(root, "CapsuleMachine", x, zc + gacha.z * 0.5f, y, Vector3.forward, gacha, mat);
                        nGacha += 2;
                    }
                }
            }

            // 5) 가게 조명 — 그림자 없는 점 조명, 약 7 × 8 m 격자, 천장 30 cm 아래
            Transform lights = Group("ShopLights_1F", root);
            int na = Mathf.Max(1, Mathf.RoundToInt((W - 7f) / 7f)), nb = Mathf.Max(1, Mathf.RoundToInt((D - 6f) / 8f));
            for (int i = 0; i <= na; i++)
            {
                for (int j = 0; j <= nb; j++)
                {
                    var go = new GameObject("ShopLight");
                    go.transform.SetParent(lights, false);
                    go.transform.localPosition = new Vector3(Mathf.Lerp(3.5f, W - 3.5f, (float)i / na), y + height - 0.3f, Mathf.Lerp(3.5f, D - 3.5f, (float)j / nb));
                    var light = go.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = new Color(1f, 0.95f, 0.88f);
                    light.intensity = 3f;
                    light.range = 8f;
                    light.shadows = LightShadows.None;
                    light.lightmapBakeType = LightmapBakeType.Realtime;
                }
            }
            Debug.Log($"[C] {root.name}: 1층 가챠 가게 — 인형뽑기 {nClaw} · 가챠 {nGacha} · 지폐 교환기 2 · 조명 {(na + 1) * (nb + 1)}");
        }

        // 2층 당구장 (사용자 11/09 — 제일당구를 N24 2층에서 N09 2층으로. 사용자 그림 88 × 64 px 대로)
        //   · 2층 칸막이는 계단 둘레 벽만 (BilliardStairWalls) — 바깥벽 · 창은 그대로 (BuildInterior 의 billiardFloor)
        //   · 당구대(대대) 다섯 · 카운터는 그림에서 읽은 자리 (실내 폭 · 깊이에 대한 비율 — k_BilliardTables · k_BilliardCounterUV)
        //   · 당구대마다 가까운 벽에 큐 장 하나 — 창 · 문을 피하고, 긴 변이 벽을 보면 당구대 끝 쪽으로 비켜서
        //   · 당구대마다 낮게 매단 등 + 그림자 없는 점 조명, 카운터 위 등 하나
        static readonly Vector3 k_TableBox = new Vector3(1.74f, 0.84f, 2.90f);    // 폭 · 높이 · 길이 — 모델(폭 : 길이 = 0.6)을 대대(약 1.7 × 3.1 m) 가깝게. 앞 = 긴 변 방향
        const float k_TableCloth = 0.766f;   // 당구대 천 높이 — 난간 윗면(0.84)보다 7 cm 낮다 (모델을 k_TableBox 에 맞췄을 때 잰 값, 11/17)
        static readonly Vector3 k_CueRackBox = new Vector3(0.99f, 1.50f, 0.29f);  // 폭 · 높이 · 깊이 — 모델 비율 그대로
        const float k_TableClear = 1.4f;   // 당구대와 벽 사이 큐 칠 자리 (큐 길이 약 1.4 m)

        // 사용자 그림(안쪽 86 × 62 px)에서 읽은 가운데 자리 — u: 실내 왼쪽 → 오른쪽, v: 실내 앞 → 뒤 비율.
        // 그림의 당구대는 실제보다 1.3 배쯤 크게 그려져 있어 크기는 k_TableBox, 자리는 가운데만 따른다
        static readonly (Vector2 uv, bool alongZ)[] k_BilliardTables =
        {
            (new Vector2(0.1744f, 0.7581f), true),    // 왼쪽 뒤 (세로)
            (new Vector2(0.1744f, 0.2581f), true),    // 왼쪽 앞 (세로)
            (new Vector2(0.4651f, 0.2581f), false),   // 앞 가운데 (가로)
            (new Vector2(0.7791f, 0.2581f), false),   // 앞 오른쪽 (가로)
            (new Vector2(0.7791f, 0.5968f), false),   // 오른쪽 가운데 (가로)
        };
        static readonly Vector2 k_BilliardCounterUV = new Vector2(0.9128f, 0.8387f);   // 검은 박스(오른쪽 벽에 붙은 약 3.1 × 1.8 m 칸) 가운데
        const float k_BilliardEntrance = 2.0f;   // 홀 입구 폭(오른쪽 벽 끝 ~ 뒷벽) — 계단 꼭대기에 맞추면 1.2 m 라 좁아 보여 조금 넓힘 (사용자 11/09)

        // 당구장 층 계단 둘레 벽 (사용자 그림): 계단 왼쪽 — 뒷벽부터 계단 앞 끝까지(1층 복도 왼쪽 벽 바로 위),
        // 계단 앞 끝 가로 벽 — 계단 왼쪽부터 복도 오른쪽 벽까지, 복도 오른쪽 벽 — 계단 앞 끝부터 계단 꼭대기까지.
        // 계단을 올라와 꼭대기 옆(오른쪽 벽 끝과 뒷벽 사이, 폭 k_BilliardEntrance)으로 홀에 들어간다. 계단 구멍 오른쪽 난간은 그대로(BuildInterior)
        static void BilliardStairWalls(Transform root, InteriorPlan p, float y, float height, Material wall)
        {
            float z0 = p.stairZ0;
            float xl = p.leftRooms ? p.hall0 - k_Partition : p.hall0;   // 왼쪽에 방이 없는 좁은 건물은 왼쪽 바깥벽이 계단 왼쪽 벽
            Wall("BilliardStairWall_F", root, new Vector3(xl, y, z0), new Vector3(p.hall1 + k_Partition, y, z0), Vector3.back, null, wall, k_Partition, height);
            if (p.leftRooms)
                Wall("BilliardStairWall_L", root, new Vector3(p.hall0, y, z0), new Vector3(p.hall0, y, p.D - k_Wall), Vector3.left, null, wall, k_Partition, height);
            float zr = Mathf.Min(p.stairZ1, p.D - k_Wall - k_BilliardEntrance);
            Wall("BilliardStairWall_R", root, new Vector3(p.hall1, y, z0), new Vector3(p.hall1, y, zr), Vector3.right, null, wall, k_Partition, height);
        }

        static void FurnishBilliardHall(Transform root, InteriorPlan p, float y, float height, List<Vector4>[] openings, Materials m)
        {
            float W = p.W, D = p.D;
            float x0 = k_Wall, x1 = W - k_Wall, z0 = k_Wall, z1 = D - k_Wall;
            float L = k_TableBox.z, B = k_TableBox.x, c = k_TableClear;
            // 계단 둘레 벽 안(계단 구멍 · 그 오른쪽 복도 · 꼭대기 내리는 자리) — 아무것도 두지 않는다
            Rect stairZone = Rect.MinMaxRect(p.hall0 - k_Partition, p.stairZ0 - k_Partition, p.hall1 + k_Partition, z1);
            Vector2 At(Vector2 uv) => new Vector2(x0 + uv.x * (x1 - x0), z0 + uv.y * (z1 - z0));

            // 1) 당구대 자리 (가운데 · 긴 변이 z 방향인지) — 사용자 그림대로
            Vector2 cc = At(k_BilliardCounterUV);
            var counter = new Rect(cc.x - 1f, cc.y - 0.3f, 2f, 0.6f);   // 검은 박스 가운데, 손님 쪽이 앞(당구대 쪽)
            var plan = new List<(Vector2 c, bool alongZ)>();
            foreach (var (uv, alongZ) in k_BilliardTables)
                plan.Add((At(uv), alongZ));
            Rect Footprint((Vector2 c, bool alongZ) t)
            {
                float hx = (t.alongZ ? B : L) * 0.5f, hz = (t.alongZ ? L : B) * 0.5f;
                return Rect.MinMaxRect(t.c.x - hx, t.c.y - hz, t.c.x + hx, t.c.y + hz);
            }
            var tables = new List<(Vector2 c, bool alongZ)>();
            var tableRects = new List<Rect>();
            foreach (var t in plan)
            {
                Rect r = Footprint(t);
                bool ok = r.xMin >= x0 + c && r.xMax <= x1 - c && r.yMin >= z0 + c && r.yMax <= z1 - c
                          && !Expand(r, 1f).Overlaps(stairZone) && !Expand(r, 1f).Overlaps(Expand(counter, 0.3f));
                foreach (Rect o in tableRects)
                    ok &= !Expand(r, 1.2f).Overlaps(o);
                if (!ok)
                {
                    Debug.LogWarning($"[C] {root.name}: 2층 당구장 — 당구대 자리 {t.c} 가 벽 · 계단 · 다른 당구대와 너무 가까워 뺀다");
                    continue;
                }
                Vector3 face = t.alongZ ? (tables.Count % 2 == 0 ? Vector3.forward : Vector3.back) : (tables.Count % 2 == 0 ? Vector3.right : Vector3.left);
                GameObject table = Machine(root, "BilliardTable", t.c.x, t.c.y, y, face, k_TableBox, m.furniture);
                // 부딪힘 상자 윗면은 난간(0.84 m)이 아니라 천 높이 — 위에 놓은 탄창(보급 · 사용자 11/17)이 천에 닿게 (난간은 7 cm 라 막을 일이 없다)
                var tableBox = table.GetComponent<BoxCollider>();
                tableBox.size = new Vector3(tableBox.size.x, k_TableCloth, tableBox.size.z);
                tableBox.center = new Vector3(0f, (k_TableCloth - k_TableBox.y) * 0.5f, 0f);
                tables.Add(t);
                tableRects.Add(r);
            }

            // 2) 카운터 — 손님 쪽 앞판이 당구대 쪽(앞)
            Piece(root, counter, y, 1.05f, m.furniture, "2F", "ShopCounter", Vector3.back);

            // 3) 큐 장 — 당구대마다 가까운 벽부터. 벽 번호 0 앞 · 1 뒤 · 2 왼 · 3 오른 (바깥벽 구멍 목록과 같은 순서)
            var taken = new List<Rect> { Expand(counter, 0.3f), stairZone };
            float rw = k_CueRackBox.x, rd = k_CueRackBox.z;
            int nRack = 0;
            bool RackFits(int w, float u, out Rect foot)
            {
                bool alongX = w < 2;
                foot = w == 0 ? new Rect(u - rw * 0.5f, z0, rw, rd) : w == 1 ? new Rect(u - rw * 0.5f, z1 - rd, rw, rd)
                     : w == 2 ? new Rect(x0, u - rw * 0.5f, rd, rw) : new Rect(x1 - rd, u - rw * 0.5f, rd, rw);
                float lo = alongX ? x0 : z0, hi = alongX ? x1 : z1;
                if (u - rw * 0.5f < lo + 0.3f || u + rw * 0.5f > hi - 0.3f)
                    return false;
                float wu = alongX ? u : u - k_Wall;   // 옆벽 구멍은 앞 끝(z = k_Wall)부터 잰다
                foreach (Vector4 o in openings[w])
                {
                    if (o.z < k_CueRackBox.y + 0.1f && wu + rw * 0.5f > o.x - 0.1f && wu - rw * 0.5f < o.y + 0.1f)
                        return false;   // 창(창턱이 큐 장보다 낮다) · 문 앞
                }
                foreach (Rect r in tableRects)
                {
                    if (foot.Overlaps(Expand(r, 1f)))
                        return false;   // 당구대 둘레 지나갈 자리
                }
                foreach (Rect r in taken)
                {
                    if (foot.Overlaps(r))
                        return false;
                }
                return true;
            }
            void PlaceRack(int w, float u, Rect foot)
            {
                const float gap = 0.02f;   // 벽과 겹쳐 깜빡이지 않게
                Vector2 at = w == 0 ? new Vector2(u, z0 + gap + rd * 0.5f) : w == 1 ? new Vector2(u, z1 - gap - rd * 0.5f)
                           : w == 2 ? new Vector2(x0 + gap + rd * 0.5f, u) : new Vector2(x1 - gap - rd * 0.5f, u);
                Machine(root, "CueRack", at.x, at.y, y, WallInward(w), k_CueRackBox, m.furniture);
                taken.Add(Expand(foot, 0.3f));
                nRack++;
            }
            for (int i = 0; i < tables.Count; i++)
            {
                Rect r = tableRects[i];
                float[] dist = { r.yMin - z0, z1 - r.yMax, r.xMin - x0, x1 - r.xMax };
                var walls = new List<int> { 0, 1, 2, 3 };
                walls.Sort((a, b) => dist[a].CompareTo(dist[b]));
                bool done = false;
                // 벽마다 먼저 정한 자리: 긴 변이 벽을 보면 당구대 끝 바로 옆(바깥쪽 먼저), 짧은 변이면 당구대 가운데 맞은편
                foreach (int w in walls)
                {
                    if (done || dist[w] > 3f)
                        continue;
                    bool alongX = w < 2;
                    float uc = alongX ? r.center.x : r.center.y, half = alongX ? r.width * 0.5f : r.height * 0.5f;
                    bool parallel = half > (alongX ? r.height : r.width) * 0.5f;
                    float outward = uc >= (alongX ? W : D) * 0.5f ? 1f : -1f;
                    float off = half + 0.15f + rw * 0.5f;
                    float[] us = parallel ? new[] { uc + outward * off, uc - outward * off } : new[] { uc };
                    foreach (float u in us)
                    {
                        if (!done && RackFits(w, u, out Rect foot))
                        {
                            PlaceRack(w, u, foot);
                            done = true;
                        }
                    }
                }
                // 안 되면 가장 가까운 벽을 따라 당구대 가운데에서 바깥으로 훑는다
                for (int k = 1; k <= 48 && !done; k++)
                {
                    int w = walls[0];
                    float uc = w < 2 ? r.center.x : r.center.y;
                    float u = uc + (k % 2 == 1 ? 1f : -1f) * ((k + 1) / 2) * 0.25f;
                    if (RackFits(w, u, out Rect foot))
                    {
                        PlaceRack(w, u, foot);
                        done = true;
                    }
                }
                if (!done)
                    Debug.LogWarning($"[C] {root.name}: 2층 당구장 — 당구대 {r.center} 옆에 큐 장 놓을 벽 자리가 없다");
            }

            // 4) 당구대 등 — 바닥 위 1.95 m 에 낮게 매단 갓(긴 변 방향 2 m) · 아랫면 빛 · 매단 줄 둘 + 그림자 없는 점 조명. 카운터 위에도 작은 등
            Transform lamps = Group("BilliardLamps_2F", root);
            float lampY = y + 1.95f, ceil = y + height;
            int nLight = 0;
            void Lamp(Vector2 at, Vector2 half, float intensity, float range)
            {
                Box("BilliardLamp", lamps, new Vector3(at.x - half.x, lampY, at.y - half.y), new Vector3(at.x + half.x, lampY + 0.14f, at.y + half.y), m.dark, false);
                Box("BilliardLampGlow", lamps, new Vector3(at.x - half.x + 0.04f, lampY - 0.01f, at.y - half.y + 0.04f),
                    new Vector3(at.x + half.x - 0.04f, lampY, at.y + half.y - 0.04f), m.deptLight, false);
                bool longX = half.x >= half.y;
                for (int k = -1; k <= 1; k += 2)
                {
                    Vector2 o = at + (longX ? new Vector2(half.x * 0.7f * k, 0f) : new Vector2(0f, half.y * 0.7f * k));
                    Box("BilliardLampRod", lamps, new Vector3(o.x - 0.01f, lampY + 0.14f, o.y - 0.01f), new Vector3(o.x + 0.01f, ceil, o.y + 0.01f), m.dark, false);
                }
                var go = new GameObject("BilliardLight");
                go.transform.SetParent(lamps, false);
                go.transform.localPosition = new Vector3(at.x, lampY - 0.15f, at.y);
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.94f, 0.82f);
                light.intensity = intensity;
                light.range = range;
                light.shadows = LightShadows.None;
                light.lightmapBakeType = LightmapBakeType.Realtime;
                nLight++;
            }
            foreach (var t in tables)
                Lamp(t.c, t.alongZ ? new Vector2(0.26f, 1f) : new Vector2(1f, 0.26f), 2.5f, 5f);
            Lamp(counter.center, new Vector2(0.6f, 0.15f), 1.5f, 4f);

            Debug.Log($"[C] {root.name}: 2층 당구장 — 당구대 {tables.Count} · 큐 장 {nRack} · 카운터 1 · 조명 {nLight}");
        }

        // 1층 고깃집 (사용자 11/09 — 도야지천국 N23: 간판 쪽 구역은 합치고 다른 쪽은 그대로)
        //   · 남북 복도 한쪽 방들을 칸막이 없이 한 홀로 (BuildInterior 의 bbqFloor — 그쪽 방 칸막이 · 동서 복도 벽만 뺀다. 복도 벽 · 문 · 바깥벽 · 창은 그대로)
        //   · 드럼통 불판 탁자 + 탁자마다 플라스틱 의자 넷(앞 · 뒤 · 왼 · 오른, 앞이 탁자 쪽). 의자 색은 탁자마다 흰 · 초록 · 짙은 파랑 돌려 씀
        //   · 동서 복도 자리(옆문 ~ 복도)는 홀을 가로지르는 통로로 비우고, 그 앞뒤 칸에 탁자 묶음을 고르게 — 묶음 사이 · 벽까지 1 m 이상
        //   · 그림자 없는 점 조명 + 홀 반사 기준점(스테인리스 탁자가 하늘 대신 실내를 비추게 — 백화점과 같이 맵 만들기 끝에 굽는다)
        static readonly Vector3 k_BbqTableBox = new Vector3(1.0f, 0.71f, 1.0f);      // 윗판 지름 1 m · 높이 0.71 m — 모델 비율 그대로
        static readonly Vector3 k_PlasticChairBox = new Vector3(0.59f, 0.80f, 0.595f);  // 폭 · 높이 · 깊이 — 모델 비율 (0.735 : 1 : 0.741)
        const float k_ChairOffset = 0.82f;   // 탁자 가운데 ~ 의자 가운데 (의자 앞 끝이 윗판 밑 2 cm 앞)

        // 냉동창고 (사용자 11/09 — 도야지천국 N23 앞 오른쪽 방): 그 방으로 나는 문마다 철문을 달아 막는다. 창은 BuildInterior 에서 뺀다
        //   · 철문: 문 자리(폭 1 m · 높이 2.2 m)를 채우는 문짝(부딪힘 — 사람 · 총알 · 적 길찾기 모두 막힘)을 복도 쪽 면에서 2 cm 들여 단다,
        //     복도 쪽에 어두운 문틀 · 스테인리스 손잡이, 문짝 눈높이에 안내판 '냉동창고 · 관계자 외 출입금지'(Images/Sign/cold_storage.png —
        //     Source~/make_cold_storage_sign.py 로 그림)
        const string k_ColdStorageSign = "Assets/C/Images/Sign/cold_storage.png";
        static readonly Vector2 k_ColdSignSize = new Vector2(0.42f, 0.30f);

        // 모서리로 고른 1층 방 — 복도 왼쪽 / 오른쪽 방 중 가장 앞 / 뒤
        static Room ColdStorageRoom(InteriorPlan p, GreyboxBuildingSpec.RoomCorner corner)
        {
            if (corner == GreyboxBuildingSpec.RoomCorner.None)
                return null;
            bool front = corner == GreyboxBuildingSpec.RoomCorner.FrontLeft || corner == GreyboxBuildingSpec.RoomCorner.FrontRight;
            bool left = corner == GreyboxBuildingSpec.RoomCorner.FrontLeft || corner == GreyboxBuildingSpec.RoomCorner.BackLeft;
            Room best = null;
            foreach (Room r in p.rooms)
            {
                bool side = left ? r.area.xMax <= p.hall0 + 0.01f : r.area.xMin >= p.hall1 - 0.01f;
                if (r.stair || !side)
                    continue;
                if (best == null || (front ? r.area.yMin < best.area.yMin : r.area.yMax > best.area.yMax))
                    best = r;
            }
            return best;
        }

        // 그림 한 장을 붙이는 재질 (판 모양 안내판용 — 상자 재질은 월드 기준 UV 라 그림이 안 맞는다)
        static Material DecalMat(string name, string texture, Color fallback)
        {
            string path = $"{k_MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texture);
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", tex != null ? Color.white : fallback);
            mat.SetFloat("_Smoothness", 0.35f);
            mat.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // 바깥(n) 쪽을 보는 판 하나 — 가운데 c, 크기(가로 · 세로), UV 0~1 (건물 좌표)
        static void Plate(string name, Transform root, Vector3 c, Vector3 n, Vector2 size, Material mat)
        {
            Vector3 r = Vector3.Cross(Vector3.up, -n).normalized * (size.x * 0.5f), u = Vector3.up * (size.y * 0.5f);
            var mesh = new Mesh { name = name };
            mesh.vertices = new[] { c - r - u, c + r - u, c + r + u, c - r + u };
            mesh.normals = new[] { n, n, n, n };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            mesh.triangles = new[] { 0, 3, 2, 0, 2, 1 };
            mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        static void ColdStorageDoors(Transform root, InteriorPlan p, Room room, float y, Materials m)
        {
            Material sign = DecalMat("GB_Sign_ColdStorage", k_ColdStorageSign, new Color(0.95f, 0.95f, 0.93f));
            Rect a = room.area;
            const float hw = k_DoorWidth * 0.5f, H = k_DoorHeight, jamb = 0.06f;
            int n0 = 0;
            foreach (Vector2 d in room.doors)
            {
                int w = (int)d.x;
                float c = d.y;
                // 문이 난 벽: 바깥벽이면 두께 k_Wall, 아니면 칸막이. 복도(방 밖) 쪽 면 자리와 그쪽 방향
                bool outer = w == 0 ? a.yMin <= k_Wall + 0.01f : w == 1 ? a.yMax >= p.D - k_Wall - 0.01f : w == 2 ? a.xMin <= k_Wall + 0.01f : a.xMax >= p.W - k_Wall - 0.01f;
                float t = outer ? k_Wall : k_Partition;
                float face = w == 0 ? a.yMin - t : w == 1 ? a.yMax + t : w == 2 ? a.xMin - t : a.xMax + t;
                Vector3 n = w == 0 ? Vector3.back : w == 1 ? Vector3.forward : w == 2 ? Vector3.left : Vector3.right;
                bool alongX = w < 2;
                // u: 벽을 따라, h: 바닥에서, s: 복도 쪽 면에서 바깥(+) / 벽 속(-)
                Vector3 P(float u, float h, float s) => alongX ? new Vector3(u, y + h, face + n.z * s) : new Vector3(face + n.x * s, y + h, u);
                void Part(string name, float u0, float u1, float h0, float h1, float s0, float s1, Material mat, bool collide)
                {
                    Vector3 p0 = P(u0, h0, s0), p1 = P(u1, h1, s1);
                    Box(name, root, Vector3.Min(p0, p1), Vector3.Max(p0, p1), mat, collide);
                }
                Part("ColdStorageDoor", c - hw, c + hw, 0f, H, -0.07f, -0.02f, m.extDoor, true);                        // 문짝
                Part("ColdStorageFrame", c - hw - jamb, c - hw, 0f, H + jamb, -0.02f, 0.015f, m.dark, false);           // 문틀
                Part("ColdStorageFrame", c + hw, c + hw + jamb, 0f, H + jamb, -0.02f, 0.015f, m.dark, false);
                Part("ColdStorageFrame", c - hw - jamb, c + hw + jamb, H, H + jamb, -0.02f, 0.015f, m.dark, false);
                Part("ColdStorageHandle", c + hw - 0.13f, c + hw - 0.09f, 0.97f, 1.07f, -0.02f, -0.006f, m.deptStainless, false);   // 손잡이 받침
                Part("ColdStorageHandle", c + hw - 0.25f, c + hw - 0.09f, 1.005f, 1.03f, -0.006f, 0.02f, m.deptStainless, false);   // 손잡이
                Plate("ColdStorageSign", root, P(c, 1.55f, -0.016f), n, k_ColdSignSize, sign);                         // 안내판
                n0++;
            }
            Debug.Log($"[C] {root.name}: 냉동창고 — 방 x[{a.xMin:F1},{a.xMax:F1}] z[{a.yMin:F1},{a.yMax:F1}] · 철문 {n0} · 창 없앰");
        }

        // 고깃집 홀 쪽에 온전히 들어가는 선분 · 방인지 — 남북 복도 벽(hall0 · hall1 위)은 아니다
        static bool OnBbqSide(InteriorPlan p, float xa, float xb, bool left)
        {
            return left ? Mathf.Max(xa, xb) < p.hall0 - 0.01f : Mathf.Min(xa, xb) > p.hall1 + 0.01f;
        }

        static void FurnishBbqHall(Transform root, InteriorPlan p, float y, float height, bool left, Materials m)
        {
            Rect hall = left ? Rect.MinMaxRect(k_Wall, k_Wall, p.hall0 - k_Partition, p.D - k_Wall)
                             : Rect.MinMaxRect(p.hall1 + k_Partition, k_Wall, p.W - k_Wall, p.D - k_Wall);
            float group = 2f * (k_ChairOffset + k_PlasticChairBox.z * 0.5f);   // 탁자 + 의자 넷 묶음 한 변
            const float gap = 1.0f;
            int cols = Mathf.Max(1, Mathf.FloorToInt((hall.width - gap) / (group + gap)));
            float gx = (hall.width - cols * group) / (cols + 1);
            var blocks = new List<Vector2>();
            if (p.cross)
            {
                blocks.Add(new Vector2(hall.yMin, p.cz0));   // 동서 복도 자리는 통로로 비운다
                blocks.Add(new Vector2(p.cz1, hall.yMax));
            }
            else
            {
                blocks.Add(new Vector2(hall.yMin, hall.yMax));
            }
            var tables = new List<Vector2>();
            foreach (Vector2 bl in blocks)
            {
                int rows = Mathf.FloorToInt((bl.y - bl.x - gap) / (group + gap));
                if (rows < 1)
                    continue;
                float gz = (bl.y - bl.x - rows * group) / (rows + 1);
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                        tables.Add(new Vector2(hall.xMin + gx + group * 0.5f + c * (group + gx), bl.x + gz + group * 0.5f + r * (group + gz)));
                }
            }
            int nChair = 0;
            for (int i = 0; i < tables.Count; i++)
            {
                Vector2 t = tables[i];
                Machine(root, "BbqTable", t.x, t.y, y, i % 2 == 0 ? Vector3.forward : Vector3.right, k_BbqTableBox, m.furniture);
                foreach (Vector3 d in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                {
                    Machine(root, "PlasticChair", t.x + d.x * k_ChairOffset, t.y + d.z * k_ChairOffset, y, -d, k_PlasticChairBox, m.furniture, i % 3);
                    nChair++;
                }
            }

            // 조명 — 탁자 줄마다 홀 가운데 하나 (점 조명, 그림자 없음)
            Transform lights = Group("BbqLights_1F", root);
            var rowsZ = new List<float>();
            foreach (Vector2 t in tables)
            {
                if (!rowsZ.Exists(z => Mathf.Abs(z - t.y) < 0.1f))
                    rowsZ.Add(t.y);
            }
            foreach (float z in rowsZ)
            {
                var go = new GameObject("BbqLight");
                go.transform.SetParent(lights, false);
                go.transform.localPosition = new Vector3(hall.center.x, y + height - 0.3f, z);
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.95f, 0.86f);
                light.intensity = 3f;
                light.range = 7f;
                light.shadows = LightShadows.None;
                light.lightmapBakeType = LightmapBakeType.Realtime;
            }

            // 반사 기준점 — 홀을 덮는 상자 (백화점 것과 같은 방식 · 같은 굽기, 회전 없는 Buildings 묶음 밑)
            var probeGo = new GameObject($"ShopProbe_{root.name}_1F");
            probeGo.transform.SetParent(root.parent, false);
            probeGo.transform.position = root.TransformPoint(new Vector3(hall.center.x, y + 1.6f, hall.center.y));
            const float margin = 0.1f;
            var bounds = new Bounds(root.TransformPoint(new Vector3(hall.xMin - margin, y - margin, hall.yMin - margin)), Vector3.zero);
            foreach (Vector3 c in new[] { new Vector3(hall.xMax + margin, y - margin, hall.yMin - margin), new Vector3(hall.xMin - margin, y - margin, hall.yMax + margin),
                                          new Vector3(hall.xMax + margin, y + height + margin, hall.yMax + margin) })
                bounds.Encapsulate(root.TransformPoint(c));
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Custom;
            probe.boxProjection = true;
            probe.resolution = 128;
            probe.hdr = true;
            probe.blendDistance = 0f;
            probe.center = bounds.center - probeGo.transform.position;
            probe.size = bounds.size;
            s_DeptProbes.Add(probe);

            // 환풍구는 건물을 합친 뒤 BuildHoods 가 — 탁자 자리 · 천장 높이를 남겨 둔다
            s_HoodSites[root] = new HoodSite { tables = tables, floorY = y, ceilY = y + height, W = p.W, hall = hall };

            Debug.Log($"[C] {root.name}: 1층 고깃집 — 홀 {hall.width:F1} × {hall.height:F1} m · 드럼통 탁자 {tables.Count} · 플라스틱 의자 {nChair} · 조명 {rowsZ.Count} · 반사 기준점 1");
        }

        // 고깃집 환풍구 (사용자 11/09 — 도야지천국 N23, 사용자 사진: 한국 고깃집 신축 연통)
        //   · 탁자마다 천장 덕트에서 내려오는 연통 — 위 컵(테 · 옆 검은 도르래) · 목 고리 · 주름관(가로 골) · 고리 둘(옆 T 손잡이) · 곧은 관 · 아래 나팔 후드(안쪽 흡입판)
        //     후드 아래 끝은 불판 윗면 위 k_HoodAboveTable — 서 있는 눈높이보다 낮아 홀 너머가 보인다. 관 길이는 천장 높이에 맞춘다(실제 연통처럼 관이 주름관 속으로 들어간다)
        //   · 천장에서 내려 매단 원형 덕트 — 앞벽 · 뒷벽을 따라가는 큰 관 둘 사이에 탁자 열마다 하나(연통 위 컵이 밑에 붙는다), 1.2 m 마다 이음 띠.
        //     관 가운데가 천장 43 cm 밑 — 철제 매달이 틀(천장 고정판 · 전산볼트 둘 · 관 밑 받침대 · 관 띠)로 1.8 m 마다 매단다 (사용자 11/09: 천장에 딱 붙일 필요 없이 실제 고깃집처럼)
        //   · 큰 관 둘 다 홀 쪽 바깥벽(N23 은 서쪽) 안쪽 면의 팬 상자(관 높이 가운데)로 들어가고, 바깥벽 겉면에는 벽에 박힌 환풍 팬(0.46 m 네모 틀 · 어두운 구멍 · 날개 다섯 · 둥근 보호망 —
        //     2층 바닥 띠(천장 높이)보다 20 cm 아래, 앞 모서리 가스관보다 위).
        //     반대쪽 끝은 테 두른 마개 — 사용자 11/09: 덕트 끝이 벽 속으로 그냥 사라져 마감이 덜 돼 보였다 → 북서쪽처럼 남서쪽도
        //   · 모두 보이기만 한다 (콜라이더 없음 — 몸 · 총알 · 적 길찾기 그대로). 스테인리스는 홀 반사 기준점(ShopProbe)이 비친다
        //   · 탁자 자리 · 천장 높이는 FurnishBbqHall 이 남기고, 건물을 합친 뒤(CombineVisuals 는 네모 면만 다룬다) BuildHoods 가 재질별 메시로 만든다 — 설비와 같은 방식
        class HoodSite
        {
            public List<Vector2> tables;
            public float floorY, ceilY, W;
            public Rect hall;
        }

        static readonly Dictionary<Transform, HoodSite> s_HoodSites = new Dictionary<Transform, HoodSite>();
        const float k_HoodAboveTable = 0.5f;
        const float k_DuctRadius = 0.15f;
        const float k_DuctDrop = 0.43f;   // 천장 ~ 덕트 가운데 — 관 위 28 cm 를 매달이 틀로 (바깥 팬이 2층 띠와 겹치지 않는 높이)
        static int s_HoodCount;

        // 단면(높이, 반지름)을 축(xf 의 y)으로 돌린 면 — 마디마다 따로(마디 기울기로 법선: 둘레는 부드럽고 단면 꺾임은 그대로). inward: 안쪽을 보는 면
        static void Lathe(UtilMesh um, Matrix4x4 xf, (float h, float r)[] prof, int sides, bool inward = false)
        {
            for (int i = 0; i + 1 < prof.Length; i++)
            {
                (float h0, float r0) = prof[i];
                (float h1, float r1) = prof[i + 1];
                float dh = h1 - h0, dr = r1 - r0;
                if (Mathf.Abs(dh) < 1e-5f && Mathf.Abs(dr) < 1e-5f)
                    continue;
                Vector2 nn = new Vector2(dh, -dr).normalized;   // (바깥, 축) 성분
                if (inward)
                    nn = -nn;
                int s0 = um.v.Count;
                for (int k = 0; k <= sides; k++)
                {
                    float a = k * Mathf.PI * 2f / sides;
                    var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    um.v.Add(xf.MultiplyPoint3x4(dir * r0 + Vector3.up * h0));
                    um.v.Add(xf.MultiplyPoint3x4(dir * r1 + Vector3.up * h1));
                    Vector3 n = xf.MultiplyVector(dir * nn.x + Vector3.up * nn.y).normalized;
                    um.n.Add(n);
                    um.n.Add(n);
                }
                for (int k = 0; k < sides; k++)
                {
                    int a0 = s0 + k * 2, b0 = a0 + 1, a1 = a0 + 2, b1 = a0 + 3;
                    if (inward)
                        um.t.AddRange(new[] { a0, b1, b0, a0, a1, b1 });
                    else
                        um.t.AddRange(new[] { a0, b0, b1, a0, b1, a1 });
                }
            }
        }

        // 축이 dir 인 원기둥 (양 끝 막음) — p0 에서 dir 쪽으로 len
        static void Cylinder(UtilMesh um, Vector3 p0, Vector3 dir, float len, float r, int sides)
        {
            Matrix4x4 xf = Matrix4x4.TRS(p0, Quaternion.FromToRotation(Vector3.up, dir), Vector3.one);
            Lathe(um, xf, new[] { (0f, 0f), (0f, r), (len, r), (len, 0f) }, sides);
        }

        // 축에 나란한 상자 (UtilMesh — 설비의 AddBox 와 같은 면 순서)
        static void BoxU(UtilMesh um, Vector3 a, Vector3 b)
        {
            Vector3 mn = Vector3.Min(a, b), mx = Vector3.Max(a, b), c = (mn + mx) * 0.5f, h = (mx - mn) * 0.5f;
            foreach (Vector3 nn in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
            {
                Vector3 ax = nn.x != 0 ? Vector3.up : nn.y != 0 ? Vector3.forward : Vector3.right;
                Vector3 bx = Vector3.Cross(nn, ax);
                if (Vector3.Dot(Vector3.Cross(ax, bx), nn) < 0)
                    bx = -bx;
                Quad(um, c + Vector3.Scale(nn, h), Vector3.Scale(ax, h), Vector3.Scale(bx, h), nn);
            }
        }

        // 축 u · v (서로 직각인 단위 벡터)로 기울인 상자 — 반 크기 half (u, v, u×v)
        static void OBox(UtilMesh um, Vector3 c, Vector3 u, Vector3 v, Vector3 half)
        {
            Vector3 w = Vector3.Cross(u, v);
            Vector3 hu = u * half.x, hv = v * half.y, hw = w * half.z;
            Quad(um, c + hu, hv, hw, u);
            Quad(um, c - hu, hw, hv, -u);
            Quad(um, c + hv, hw, hu, v);
            Quad(um, c - hv, hu, hw, -v);
            Quad(um, c + hw, hu, hv, w);
            Quad(um, c - hw, hv, hu, -w);
        }

        // 면 하나 — 가운데 cc, 반 변 aa · bb (aa × bb 가 nn 쪽일 때 앞면이 nn 쪽)
        static void Quad(UtilMesh um, Vector3 cc, Vector3 aa, Vector3 bb, Vector3 nn)
        {
            int s0 = um.v.Count;
            foreach (Vector3 p in new[] { cc - aa - bb, cc - aa + bb, cc + aa + bb, cc + aa - bb })
            {
                um.v.Add(p);
                um.n.Add(nn);
            }
            um.t.AddRange(new[] { s0, s0 + 2, s0 + 1, s0, s0 + 3, s0 + 2 });
        }

        // 바깥벽 겉면(가운데 c, 바깥 방향 x 부호 sx)에 박힌 환풍 팬 — 네모 틀(0.46 m) · 어두운 구멍 · 날개 다섯 · 둥근 보호망
        static void ExhaustFan(UtilMesh frame, UtilMesh chrome, UtilMesh dark, Vector3 c, float sx)
        {
            var o = new Vector3(sx, 0f, 0f);
            const float half = 0.23f, rim = 0.045f, depth = 0.06f;
            float x0 = c.x, x1 = c.x + sx * depth;
            BoxU(frame, new Vector3(x0, c.y + half - rim, c.z - half), new Vector3(x1, c.y + half, c.z + half));             // 틀 위
            BoxU(frame, new Vector3(x0, c.y - half, c.z - half), new Vector3(x1, c.y - half + rim, c.z + half));             // 틀 아래
            BoxU(frame, new Vector3(x0, c.y - half + rim, c.z - half), new Vector3(x1, c.y + half - rim, c.z - half + rim)); // 틀 옆
            BoxU(frame, new Vector3(x0, c.y - half + rim, c.z + half - rim), new Vector3(x1, c.y + half - rim, c.z + half));
            float hole = half - rim;
            BoxU(dark, new Vector3(x0, c.y - hole, c.z - hole), new Vector3(c.x + sx * 0.004f, c.y + hole, c.z + hole));   // 벽 속 어두운 구멍
            Cylinder(chrome, c + o * 0.004f, o, 0.032f, 0.034f, 12);                                                     // 날개 축
            for (int k = 0; k < 5; k++)
            {
                float a = k * Mathf.PI * 2f / 5f + 0.3f;
                var radial = new Vector3(0f, Mathf.Sin(a), Mathf.Cos(a));
                Vector3 tangent = Vector3.Cross(o, radial).normalized;
                Vector3 pitched = (tangent * Mathf.Cos(0.45f) + o * Mathf.Sin(0.45f)).normalized;   // 날개를 25° 틀어
                OBox(frame, c + o * 0.022f + radial * 0.098f, radial, pitched, new Vector3(0.066f, 0.034f, 0.004f));
            }
            Matrix4x4 guard = Matrix4x4.TRS(c + o * 0.05f, Quaternion.FromToRotation(Vector3.up, o), Vector3.one);
            foreach (float r in new[] { 0.065f, 0.12f, 0.17f })
                Lathe(chrome, guard, new[] { (0f, r + 0.004f), (0f, r - 0.004f) }, 28);                                   // 보호망 고리 (밖을 본다)
            BoxU(chrome, new Vector3(c.x + sx * 0.047f, c.y - 0.175f, c.z - 0.004f), new Vector3(c.x + sx * 0.053f, c.y + 0.175f, c.z + 0.004f));
            BoxU(chrome, new Vector3(c.x + sx * 0.047f, c.y - 0.004f, c.z - 0.175f), new Vector3(c.x + sx * 0.053f, c.y + 0.004f, c.z + 0.175f));
        }

        static void BuildHoods(Transform root, Materials m)
        {
            if (!s_HoodSites.TryGetValue(root, out HoodSite site) || site.tables.Count == 0)
                return;
            var meshes = new Dictionary<Material, UtilMesh>();
            UtilMesh L(Material mat)
            {
                if (!meshes.TryGetValue(mat, out UtilMesh um))
                    meshes[mat] = um = new UtilMesh();
                return um;
            }
            Material chrome = FacadeMat("GB_Bbq_Hood", new Color(0.80f, 0.80f, 0.82f), 0.86f, 1f);
            Material sleeve = FacadeMat("GB_Bbq_HoodSleeve", new Color(0.70f, 0.70f, 0.72f), 0.58f, 1f);
            Material duct = FacadeMat("GB_Bbq_Duct", new Color(0.74f, 0.75f, 0.76f), 0.42f, 1f);
            const int sides = 18;
            float yc = site.ceilY;
            float ductY = yc - k_DuctDrop;                            // 덕트 가운데 (천장에서 내려 매단다)
            Material hanger = FacadeMat("GB_Bbq_Hanger", new Color(0.21f, 0.22f, 0.23f), 0.4f, 0.8f);   // 철제 매달이 틀
            float cupTop = ductY - k_DuctRadius + 0.03f;            // 연통 위 컵이 덕트 밑으로 조금 들어간다
            float hb = site.floorY + k_BbqTableBox.y + k_HoodAboveTable;   // 후드 아래 끝

            // 1) 연통 — 탁자마다
            foreach (Vector2 t in site.tables)
            {
                Matrix4x4 at = Matrix4x4.Translate(new Vector3(t.x, 0f, t.y));
                // 아래 나팔 후드 (아래 끝 hb) — 테 · 휘어 오르는 나팔 · 이음 띠
                Lathe(L(chrome), at * Matrix4x4.Translate(Vector3.up * hb), new[]
                {
                    (0f, 0.196f), (0f, 0.206f), (0.014f, 0.206f), (0.016f, 0.198f), (0.032f, 0.172f), (0.052f, 0.145f), (0.076f, 0.121f),
                    (0.100f, 0.104f), (0.126f, 0.093f), (0.150f, 0.087f), (0.150f, 0.092f), (0.192f, 0.092f), (0.192f, 0.073f),
                }, sides);
                Lathe(L(chrome), at * Matrix4x4.Translate(Vector3.up * hb), new[] { (0f, 0.196f), (0.05f, 0.18f) }, sides, true);   // 테 안쪽
                Lathe(L(m.dark), at * Matrix4x4.Translate(Vector3.up * (hb + 0.05f)), new[] { (0f, 0f), (0f, 0.181f) }, sides);       // 흡입판 (아래를 본다)
                // 위 컵 · 목 · 주름관 · 고리 — 천장 덕트에 매달린 위 토막
                float cupBot = cupTop - 0.20f, neckBot = cupBot - 0.03f, sleeveBot = neckBot - 0.40f;
                // 단면은 아래에서 위로 (Lathe 의 법선 방향이 거기에 맞춰져 있다)
                Lathe(L(chrome), at, new[]
                {
                    (neckBot, 0.088f), (neckBot, 0.10f), (cupBot, 0.10f), (cupBot, 0.118f), (cupBot + 0.01f, 0.126f), (cupTop - 0.018f, 0.126f),
                    (cupTop - 0.016f, 0.137f), (cupTop, 0.137f), (cupTop, 0f),
                }, sides);
                var rib = new List<(float, float)>();
                const int ribs = 10;
                for (int k = 0; k <= ribs * 2; k++)
                    rib.Add((sleeveBot + k * (0.40f / (ribs * 2)), k % 2 == 0 ? 0.088f : 0.084f));
                Lathe(L(sleeve), at, rib.ToArray(), sides);
                Lathe(L(chrome), at, new[]
                {
                    (sleeveBot - 0.09f, 0.072f), (sleeveBot - 0.09f, 0.098f), (sleeveBot - 0.06f, 0.098f), (sleeveBot - 0.06f, 0.088f),
                    (sleeveBot - 0.035f, 0.088f), (sleeveBot - 0.035f, 0.098f), (sleeveBot, 0.098f), (sleeveBot, 0.088f),
                }, sides);
                // 곧은 관 — 후드 위 이음 띠에서 고리 속까지
                Lathe(L(chrome), at, new[] { (hb + 0.192f, 0.072f), (sleeveBot - 0.09f, 0.072f) }, sides);
                // 옆 도르래(검정) · T 손잡이 — 홀 가운데 쪽(통로 쪽)을 보게
                Vector3 side = t.x < site.hall.center.x ? Vector3.right : Vector3.left;
                Vector3 c = new Vector3(t.x, 0f, t.y);
                float cupMid = (cupTop + cupBot) * 0.5f;
                Cylinder(L(m.dark), c + Vector3.up * cupMid + side * 0.128f, side, 0.032f, 0.074f, 20);
                Cylinder(L(chrome), c + Vector3.up * cupMid + side * 0.16f, side, 0.012f, 0.018f, 10);
                Cylinder(L(m.dark), c + Vector3.up * (sleeveBot - 0.075f) + side * 0.097f, side, 0.045f, 0.010f, 8);
                Cylinder(L(m.dark), c + Vector3.up * (sleeveBot - 0.075f) + side * 0.142f + Vector3.forward * 0.03f, Vector3.back, 0.06f, 0.009f, 8);
                s_HoodCount++;
            }

            // 2) 천장 덕트 — 탁자 열마다 하나(앞 끝은 막음) + 뒷벽 따라 바깥벽(홀 왼쪽 · 오른쪽 중 바깥벽 쪽)으로 나가는 큰 관
            var cols = new List<float>();
            foreach (Vector2 t in site.tables)
            {
                if (!cols.Exists(x => Mathf.Abs(x - t.x) < 0.1f))
                    cols.Add(t.x);
            }
            cols.Sort();
            float zBack = site.hall.yMax - 0.6f;    // 뒷벽 따라가는 큰 관 — 바깥 팬이 뒤 모서리 배수관과 붙지 않게 0.6 m
            float zFront = site.hall.yMin + 0.9f;   // 앞벽 따라가는 큰 관 — 바깥 팬이 앞 모서리 가스관 꺾이는 곳 위에 겹치지 않게 0.9 m
            void Ring(Matrix4x4 xf, float s, float w, float r)   // 관 둘레 띠 (s ~ s + w)
            {
                Lathe(L(duct), xf, new[] { (s, k_DuctRadius), (s, r), (s + w, r), (s + w, k_DuctRadius) }, 20);
            }
            void Duct(Vector3 a, Vector3 b, bool capA, bool capB)
            {
                Vector3 d = b - a;
                float len = d.magnitude;
                Matrix4x4 xf = Matrix4x4.TRS(a, Quaternion.FromToRotation(Vector3.up, d / len), Vector3.one);
                var prof = new List<(float, float)>();
                if (capA)
                    prof.Add((0f, 0f));
                prof.Add((0f, k_DuctRadius));
                prof.Add((len, k_DuctRadius));
                if (capB)
                    prof.Add((len, 0f));
                Lathe(L(duct), xf, prof.ToArray(), 20);
                for (float s = 1.2f; s < len - 0.3f; s += 1.2f)   // 이음 띠
                    Ring(xf, s - 0.012f, 0.024f, k_DuctRadius + 0.008f);
                if (capA)
                    Ring(xf, 0f, 0.03f, k_DuctRadius + 0.014f);   // 마개 테
                if (capB)
                    Ring(xf, len - 0.03f, 0.03f, k_DuctRadius + 0.014f);
            }
            foreach (float x in cols)
                Duct(new Vector3(x, ductY, zFront), new Vector3(x, ductY, zBack), false, false);   // 탁자 열 덕트 — 앞뒤 큰 관 사이

            // 앞뒤 큰 관 → 홀 쪽 바깥벽 안쪽 면의 팬 상자 (홀 왼쪽이 바깥벽이면 왼쪽, 아니면 오른쪽)
            bool outLeft = site.hall.xMin <= k_Wall + 0.01f;
            float sx = outLeft ? -1f : 1f;                                   // 바깥 쪽
            float xi = outLeft ? site.hall.xMin : site.hall.xMax;           // 바깥벽 안쪽 면
            float xo = outLeft ? 0f : site.W;                                // 바깥벽 겉면
            const float fanBox = 0.48f, fanDepth = 0.22f;
            float fanY = ductY, boxTop = fanY + fanBox * 0.5f, boxBot = fanY - fanBox * 0.5f;   // 관이 상자 옆 가운데로 들어간다 (천장과 띄움)
            float xf0 = xi - sx * fanDepth;                                  // 팬 상자 앞면 (홀 쪽)
            float farX = outLeft ? cols[cols.Count - 1] + k_DuctRadius : cols[0] - k_DuctRadius;
            foreach (float zh in new[] { zFront, zBack })
            {
                BoxU(L(duct), new Vector3(xi, boxBot, zh - fanBox * 0.5f), new Vector3(xf0, boxTop, zh + fanBox * 0.5f));
                Matrix4x4 joint = Matrix4x4.TRS(new Vector3(xf0, ductY, zh), Quaternion.FromToRotation(Vector3.up, new Vector3(-sx, 0f, 0f)), Vector3.one);
                Ring(joint, 0f, 0.035f, k_DuctRadius + 0.022f);              // 상자에 붙는 이음 테
                Duct(new Vector3(xf0, ductY, zh), new Vector3(farX, ductY, zh), false, true);
                ExhaustFan(L(duct), L(chrome), L(m.dark), new Vector3(xo, fanY, zh), sx);   // 바깥벽 겉면 — 벽에 박힌 환풍 팬
            }

            // 철제 매달이 틀 — 관 밑 받침대 + 양옆 전산볼트(천장 고정판 · 너트) + 관 띠. 1.8 m 마다, 연통 위 컵 · 관 이음(T) 자리는 비켜서
            int nHanger = 0;
            void Hanger(Vector3 at, Vector3 axis)
            {
                Vector3 across = Vector3.Cross(Vector3.up, axis).normalized;
                float barY0 = at.y - k_DuctRadius - 0.03f, barY1 = at.y - k_DuctRadius;
                Vector3 hb0 = axis * 0.02f + across * (k_DuctRadius + 0.07f);
                BoxU(L(hanger), new Vector3(at.x - Mathf.Abs(hb0.x), barY0, at.z - Mathf.Abs(hb0.z)), new Vector3(at.x + Mathf.Abs(hb0.x), barY1, at.z + Mathf.Abs(hb0.z)));
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 rp = at + across * side * (k_DuctRadius + 0.05f);
                    BoxU(L(hanger), new Vector3(rp.x - 0.007f, barY0 - 0.02f, rp.z - 0.007f), new Vector3(rp.x + 0.007f, yc, rp.z + 0.007f));          // 전산볼트
                    BoxU(L(hanger), new Vector3(rp.x - 0.016f, barY0 - 0.022f, rp.z - 0.016f), new Vector3(rp.x + 0.016f, barY0 - 0.004f, rp.z + 0.016f)); // 너트
                    BoxU(L(hanger), new Vector3(rp.x - 0.04f, yc - 0.012f, rp.z - 0.04f), new Vector3(rp.x + 0.04f, yc, rp.z + 0.04f));                 // 천장 고정판
                }
                Matrix4x4 band = Matrix4x4.TRS(at - axis * 0.015f, Quaternion.FromToRotation(Vector3.up, axis), Vector3.one);
                Lathe(L(hanger), band, new[] { (0f, k_DuctRadius), (0f, k_DuctRadius + 0.006f), (0.03f, k_DuctRadius + 0.006f), (0.03f, k_DuctRadius) }, 20);   // 관 띠
                nHanger++;
            }
            void Hangers(Vector3 a, Vector3 b, List<float> avoid)   // a → b 축에 나란한 관, avoid: 비킬 자리(축 위 좌표)
            {
                Vector3 axis = (b - a).normalized;
                bool alongX = Mathf.Abs(axis.x) > 0.5f;
                float u0 = alongX ? Mathf.Min(a.x, b.x) : Mathf.Min(a.z, b.z), u1 = alongX ? Mathf.Max(a.x, b.x) : Mathf.Max(a.z, b.z);
                bool Clear(float u)
                {
                    foreach (float v in avoid)
                    {
                        if (Mathf.Abs(u - v) < 0.45f)
                            return false;
                    }
                    return u > u0 + 0.3f && u < u1 - 0.3f;
                }
                for (float u = u0 + 0.5f; u < u1 - 0.3f; u += 1.8f)
                {
                    float pick = Clear(u) ? u : Clear(u + 0.5f) ? u + 0.5f : Clear(u - 0.5f) ? u - 0.5f : float.NaN;
                    if (!float.IsNaN(pick))
                        Hanger(alongX ? new Vector3(pick, a.y, a.z) : new Vector3(a.x, a.y, pick), axis);
                }
            }
            foreach (float x in cols)
            {
                var avoid = new List<float> { zFront, zBack };
                foreach (Vector2 t in site.tables)
                {
                    if (Mathf.Abs(t.x - x) < 0.1f)
                        avoid.Add(t.y);   // 연통 위 컵
                }
                Hangers(new Vector3(x, ductY, zFront), new Vector3(x, ductY, zBack), avoid);
            }
            foreach (float zh in new[] { zFront, zBack })
                Hangers(new Vector3(xf0, ductY, zh), new Vector3(farX, ductY, zh), new List<float>(cols));   // 열 덕트가 붙는 자리는 비켜서

            int verts = 0;
            foreach (KeyValuePair<Material, UtilMesh> kv in meshes)
            {
                UtilMesh um = kv.Value;
                var mesh = new Mesh { name = root.name + "_Hood_" + kv.Key.name };
                mesh.indexFormat = um.v.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
                mesh.SetVertices(um.v);
                mesh.SetNormals(um.n);
                mesh.SetTriangles(um.t, 0);
                mesh.RecalculateBounds();
                var go = new GameObject("Hood_" + kv.Key.name);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = kv.Key;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic);
                verts += um.v.Count;
            }
            Debug.Log($"[C] {root.name}: 고깃집 환풍구 — 연통 {site.tables.Count} · 천장 덕트 {cols.Count + 2}줄 · 벽 팬 2 · 매달이 틀 {nHanger} · 꼭짓점 {verts:N0}");
        }
        // ---------------------------------------------------------------- 1층 창고 (사용자 11/17 — N06)
        // 앞면 '창 · 창 · 문 · 창 · 창' 대신 큰 창고 문 둘 (셀프 스토리지 창고 줄 같은 입구 — 사용자가 든 예: 워존 '스토리지 타운').
        //   · 앞에서 볼 때 왼쪽 문은 셔터를 끝까지 내려 막는다 (셔터 판이 판정). 그 뒤 왼쪽 절반은 가운데 벽으로 막은 빈 칸
        //   · 오른쪽 문은 셔터를 말아 올린 채 — 셔터 통(바깥벽 디테일 Details.cs 가 문 머리 25 cm 안에 붙인다) 밑에 셔터 끝 바만 보인다
        //   · 두 문 모두 양옆 가이드 레일, 문 앞 인도에 노랑 · 검정 볼라드 둘
        //   · 오른쪽 절반 = 창고: 양쪽 벽과 가운데에 높은 선반 줄(TallShelf 모델), 입구 안쪽 양옆 · 통로 · 뒷벽에 팔레트에 쌓은 상자(낮은 엄폐),
        //     문 안 왼쪽 보급 책상(OfficeDesk — 위의 탄창 3개는 씬 C_Supplies, 왼쪽 통로 끝의 적 19가 지킨다), 입구 앞 바닥에 노란 하역 구역 줄, 서늘한 점 조명
        //   · 다른 바깥벽에는 창 · 문을 내지 않는다 (BuildInterior 의 warehouseFloor). 1층만 들어가는 복도형 건물만
        const float k_WarehouseDoorWidth = 4.0f;    // 큰 문 폭 — 건물이 좁으면 절반 폭에서 1.6 m 뺀 만큼
        const float k_WarehouseDoorHeight = 2.9f;   // 큰 문 높이 — 머리 25 cm 안에 셔터 통이 들어가 지나갈 높이는 2.65 m
        const float k_WarehouseShutterBox = 0.25f;  // Details.cs 의 1층 앞면 셔터 통 높이와 같게
        const float k_WarehouseRack = 2.0f;         // 선반 높이
        const float k_WarehouseRackDepth = 0.6f;

        static float WarehouseDoorHalf(float W) => Mathf.Min(k_WarehouseDoorWidth, W * 0.5f - 1.6f) * 0.5f;
        static float WarehouseDoorCenter(float W, bool left) => left ? W * 0.25f : W * 0.75f;

        // 앞벽 구멍 — 왼쪽 · 오른쪽 문 (앞벽 좌표: 건물 왼쪽 끝부터, 앞에서 볼 때 왼쪽이 x 작은 쪽)
        static void WarehouseDoors(List<Vector4> front, float W)
        {
            float half = WarehouseDoorHalf(W);
            foreach (bool left in new[] { true, false })
            {
                float c = WarehouseDoorCenter(W, left);
                front.Add(new Vector4(c - half, c + half, 0f, k_WarehouseDoorHeight));
            }
        }

        // 가운데 벽 — 왼쪽 절반(셔터를 내린 칸)을 막는다. 앞벽 · 뒷벽 사이 통째로
        static void WarehousePartition(Transform root, InteriorPlan p, float y, float height, Material wall)
        {
            float x = p.W * 0.5f - k_Partition * 0.5f;
            Wall("WarehouseWall_1F", root, new Vector3(x, y, k_Wall), new Vector3(x, y, p.D - k_Wall), Vector3.right, null, wall, k_Partition, height);
        }

        static void FurnishWarehouse(Transform root, InteriorPlan p, float y, float height, Materials m)
        {
            float W = p.W, D = p.D;
            float half = WarehouseDoorHalf(W);
            float floorTop = y + k_FloorFinish;
            float head = y + k_WarehouseDoorHeight;
            float boxBottom = head - k_WarehouseShutterBox;
            Material cardboard = FacadeMat("GB_Cardboard", new Color(0.66f, 0.5f, 0.33f), 0.15f);
            Material wood = FacadeMat("GB_PalletWood", new Color(0.72f, 0.6f, 0.43f), 0.2f);

            // 1) 문 — 둘 다 가이드 레일 · 문 앞 볼라드. 왼쪽은 셔터를 내리고, 오른쪽은 말아 올린 셔터의 끝 바만
            foreach (bool left in new[] { true, false })
            {
                float c = WarehouseDoorCenter(W, left), u0 = c - half, u1 = c + half;
                const float frame = 0.045f, railW = 0.06f;
                // 가이드 레일 — 문틀(Details) 안쪽에 붙어 문 높이 끝(셔터 통 밑)까지
                Box("ShutterRail", root, new Vector3(u0 + frame, floorTop, 0.015f), new Vector3(u0 + frame + railW, boxBottom, 0.09f), m.utilSteel, false);
                Box("ShutterRail", root, new Vector3(u1 - frame - railW, floorTop, 0.015f), new Vector3(u1 - frame, boxBottom, 0.09f), m.utilSteel, false);
                float c0 = u0 + frame + railW * 0.5f, c1 = u1 - frame - railW * 0.5f;   // 셔터 막 양끝은 레일 속
                if (left)
                {
                    // 내린 셔터: 판(판정) + 7.6 cm 마다 가로 골 + 아래 바 · 자물쇠
                    Box("ShutterCurtain", root, new Vector3(c0, floorTop, 0.035f), new Vector3(c1, boxBottom, 0.065f), m.extShutter, true);
                    int ribs = Mathf.FloorToInt((boxBottom - floorTop - 0.08f) / 0.076f);
                    for (int i = 0; i < ribs; i++)
                    {
                        float ry = floorTop + 0.08f + i * 0.076f;
                        Box("ShutterRib", root, new Vector3(c0, ry, 0.025f), new Vector3(c1, ry + 0.05f, 0.035f), m.extShutter, false);
                    }
                    Box("ShutterBottom", root, new Vector3(c0, floorTop, 0.02f), new Vector3(c1, floorTop + 0.06f, 0.075f), m.dark, false);
                    Box("ShutterLock", root, new Vector3(c - 0.07f, floorTop + 0.06f, 0.012f), new Vector3(c + 0.07f, floorTop + 0.13f, 0.02f), m.dark, false);
                }
                else
                {
                    // 말아 올린 셔터 — 통 밑으로 끝 바만 나와 있다
                    Box("ShutterBottom", root, new Vector3(c0, boxBottom - 0.07f, 0.03f), new Vector3(c1, boxBottom, 0.07f), m.dark, false);
                }
                // 볼라드 (노랑 · 검정 띠) — 문 양옆 앞 인도
                foreach (float bx in new[] { u0 - 0.35f, u1 + 0.35f })
                {
                    Box("Bollard", root, new Vector3(bx - 0.08f, k_Curb, -0.58f), new Vector3(bx + 0.08f, k_Curb + 1.0f, -0.42f), m.deptYellow, true);
                    foreach (float by in new[] { 0.58f, 0.8f })
                        Box("BollardBand", root, new Vector3(bx - 0.082f, k_Curb + by, -0.582f), new Vector3(bx + 0.082f, k_Curb + by + 0.09f, -0.418f), m.dark, false);
                }
            }

            // 2) 창고 칸 = 가운데 벽 오른쪽
            float ax0 = W * 0.5f + k_Partition * 0.5f + 0.03f, ax1 = W - k_Wall - 0.03f;
            float az0 = k_Wall + 0.03f, az1 = D - k_Wall - 0.03f;
            float dc = WarehouseDoorCenter(W, false);

            // 높은 선반 줄 — 양쪽 벽(입구 안쪽 3.8 m 비우고 뒤 1 m 남김), 가운데 줄(입구 정면 5.5 m 안쪽부터, 뒤 3 m 통로)
            float rd = k_WarehouseRackDepth;
            Piece(root, Rect.MinMaxRect(ax0, az0 + 3.8f, ax0 + rd, az1 - 1.0f), y, k_WarehouseRack, m.furniture, "1F", "TallShelf", Vector3.right);
            Piece(root, Rect.MinMaxRect(ax1 - rd, az0 + 3.8f, ax1, az1 - 1.0f), y, k_WarehouseRack, m.furniture, "1F", "TallShelf", Vector3.left);
            float mid = (ax0 + ax1) * 0.5f;
            Piece(root, Rect.MinMaxRect(mid - rd * 0.5f, az0 + 5.5f, mid + rd * 0.5f, az1 - 3.0f), y, k_WarehouseRack, m.furniture, "1F", "TallShelf", Vector3.right);

            // 팔레트 상자 — 입구 안쪽 양옆(들어오며 숨는 자리), 두 통로에 하나씩(엇갈리게), 뒷벽 가운데
            float aisleL = (ax0 + rd + mid - rd * 0.5f) * 0.5f, aisleR = (mid + rd * 0.5f + ax1 - rd) * 0.5f;
            PalletStack(root, ax0 + 1.3f, az0 + 2.6f, y, 1.2f, 1.0f, 1.15f, false, wood, cardboard);
            PalletStack(root, ax1 - 1.3f, az0 + 2.9f, y, 1.2f, 1.0f, 1.3f, true, wood, cardboard);
            PalletStack(root, aisleL, (az0 + az1) * 0.5f + 1.5f, y, 1.0f, 1.2f, 1.05f, false, wood, cardboard);
            PalletStack(root, aisleR, (az0 + az1) * 0.5f - 1.8f, y, 1.0f, 1.2f, 1.2f, true, wood, cardboard);
            PalletStack(root, mid, az1 - 0.7f, y, 1.2f, 1.0f, 1.25f, true, wood, cardboard);

            // 보급 책상 (사용자 11/17 — 사용자가 직접 놓은 자리): 오른쪽 문 바로 안 왼쪽, 앞벽에 붙여 긴 변이 안쪽으로. 위에 탄창 3개는 씬의 C_Supplies 에
            // 따로 둔다 (맵을 다시 만들어도 남게). 책상 모델은 OfficeDesk(gray_office_desk_3d_model), 상자 0.7 × 0.75 × 1.4 m
            Piece(root, Rect.MinMaxRect(dc - half - 0.72f, k_Wall, dc - half - 0.02f, k_Wall + 1.4f), y, 0.75f, m.furniture, "1F", "OfficeDesk", Vector3.right);

            // 바닥 노란 하역 구역 줄 — 오른쪽 문 안쪽 2.8 m (보이기만)
            float lz = az0 + 2.8f, lw = 0.08f, ly = floorTop + 0.003f;
            Box("FloorLine", root, new Vector3(dc - half + 0.1f, floorTop, k_Wall), new Vector3(dc - half + 0.1f + lw, ly, lz), m.deptYellow, false);
            Box("FloorLine", root, new Vector3(dc + half - 0.1f - lw, floorTop, k_Wall), new Vector3(dc + half - 0.1f, ly, lz), m.deptYellow, false);
            Box("FloorLine", root, new Vector3(dc - half + 0.1f, floorTop, lz - lw), new Vector3(dc + half - 0.1f, ly, lz), m.deptYellow, false);

            // 3) 조명 — 서늘한 흰빛 점 조명 셋, 창고 칸 가운데 줄 (그림자 없음)
            Transform lights = Group("WarehouseLights_1F", root);
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject("WarehouseLight");
                go.transform.SetParent(lights, false);
                go.transform.localPosition = new Vector3(dc, y + height - 0.3f, Mathf.Lerp(az0, az1, (i + 0.5f) / 3f));
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(0.9f, 0.95f, 1f);
                light.intensity = 2.5f;
                light.range = 8f;
                light.shadows = LightShadows.None;
                light.lightmapBakeType = LightmapBakeType.Realtime;
            }
            Debug.Log($"[C] {root.name}: 1층 창고 — 큰 문 2 (왼쪽 셔터 내림) · 선반 줄 3 · 팔레트 상자 5 · 보급 책상 1 · 볼라드 4 · 조명 3");
        }

        // 팔레트 위 상자 더미 — 바닥 가운데 (x, z), 폭 w(x) · 깊이 d(z) · 전체 높이 h. 상자는 2 × 2 칸 두 단(틈 1.5 cm)이라 낱개 상자로 보인다.
        // topBox: 위에 작은 상자 하나 더 (실루엣이 덜 반듯하게)
        static void PalletStack(Transform root, float x, float z, float y, float w, float d, float h, bool topBox, Material wood, Material cardboard)
        {
            const float pallet = 0.14f, gap = 0.015f, inset = 0.03f;
            Box("Pallet", root, new Vector3(x - w * 0.5f, y, z - d * 0.5f), new Vector3(x + w * 0.5f, y + pallet, z + d * 0.5f), wood, true);
            float cargoTop = y + h - (topBox ? 0.28f : 0f);
            float x0 = x - w * 0.5f + inset, x1 = x + w * 0.5f - inset, z0 = z - d * 0.5f + inset, z1 = z + d * 0.5f - inset;
            float layer = (cargoTop - y - pallet) * 0.5f;
            for (int k = 0; k < 2; k++)
            {
                float b0 = y + pallet + k * layer, b1 = b0 + layer - (k == 0 ? gap : 0f);
                for (int i = 0; i < 2; i++)
                {
                    for (int j = 0; j < 2; j++)
                    {
                        float cx0 = Mathf.Lerp(x0, x1, i * 0.5f) + (i == 1 ? gap * 0.5f : 0f), cx1 = Mathf.Lerp(x0, x1, (i + 1) * 0.5f) - (i == 0 ? gap * 0.5f : 0f);
                        float cz0 = Mathf.Lerp(z0, z1, j * 0.5f) + (j == 1 ? gap * 0.5f : 0f), cz1 = Mathf.Lerp(z0, z1, (j + 1) * 0.5f) - (j == 0 ? gap * 0.5f : 0f);
                        Box("Cargo", root, new Vector3(cx0, b0, cz0), new Vector3(cx1, b1, cz1), cardboard, true);
                    }
                }
            }
            if (topBox)
                Box("Cargo", root, new Vector3(x - w * 0.3f, cargoTop, z - d * 0.28f), new Vector3(x + w * 0.12f, cargoTop + 0.28f, z + d * 0.22f), cardboard, true);
        }
        // ---------------------------------------------------------------- 닫은 건물 1층 셔터 (사용자 11/17 — N11)
        // 들어갈 수 없는 건물(덩어리)의 1층 앞 · 뒤 면(길 쪽)을 칸마다 내린 셔터로 덮는다 — '빈 방으로 만들고 셔터 달아서 다 닫아버리자'.
        // 옆 두 면(골목 쪽)은 셔터 없이 창 없는 벽 그대로 (사용자 11/17: 네 면 다 셔터는 어색하다 — 골목 쪽은 그냥 벽)
        // 칸 폭 약 4 m, 칸 사이 0.35 m 는 벽이 보인다. 칸마다 골진 셔터 막(7.6 cm 골이 아니라 15 cm — 멀리서 봐도 셔터로 보이게) · 위 셔터 통 · 양옆 가이드 레일 · 아래 바.
        // 벽 겉면에서 10 cm 밖 — 1층 화강석 · 띠 장식보다 앞. 보이기만 한다 (부딪힘은 건물 덩어리)
        static void ClosedShutters(Transform root, float W, float D, Materials m)
        {
            float y0 = k_Curb + 0.02f, yTop = k_Curb + 2.55f;
            for (int side = 0; side < 2; side++)   // 0 앞 · 1 뒤 — 옆(골목) 면은 벽
            {
                float len = W;
                float usable = len - 1.0f;
                int bays = Mathf.Max(1, Mathf.RoundToInt(usable / 4f));
                const float pier = 0.35f;
                float bay = (usable - pier * (bays - 1)) / bays;
                // 면 좌표 (u = 면을 따라, n = 벽 겉면에서 밖으로, y) → 건물 좌표
                System.Func<float, float, float, Vector3> P = (u, n, y) => side == 0 ? new Vector3(u, y, -n) : side == 1 ? new Vector3(u, y, D + n)
                    : side == 2 ? new Vector3(-n, y, u) : new Vector3(W + n, y, u);
                System.Action<string, float, float, float, float, float, float, Material> B = (name, u0, u1, n0, n1, ya, yb, mat) =>
                {
                    Vector3 a = P(u0, n0, ya), b = P(u1, n1, yb);
                    Box(name, root, Vector3.Min(a, b), Vector3.Max(a, b), mat, false);
                };
                for (int i = 0; i < bays; i++)
                {
                    float u0 = 0.5f + i * (bay + pier), u1 = u0 + bay;
                    B("ClosedShutter", u0, u1, 0.10f, 0.13f, y0, yTop, m.extShutter);
                    for (float ry = y0 + 0.12f; ry < yTop - 0.05f; ry += 0.15f)
                        B("ClosedShutterRib", u0, u1, 0.13f, 0.145f, ry, ry + 0.035f, m.extShutter);
                    B("ClosedShutterBottom", u0, u1, 0.09f, 0.16f, y0, y0 + 0.06f, m.dark);
                    B("ClosedShutterBox", u0 - 0.06f, u1 + 0.06f, 0.06f, 0.36f, yTop, yTop + 0.32f, m.extShutter);
                    B("ClosedShutterRail", u0 - 0.06f, u0, 0.06f, 0.17f, k_Curb, yTop, m.utilSteel);
                    B("ClosedShutterRail", u1, u1 + 0.06f, 0.06f, 0.17f, k_Curb, yTop, m.utilSteel);
                }
            }
        }
    }
}
