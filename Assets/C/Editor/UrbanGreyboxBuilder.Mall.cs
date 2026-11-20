using System.Collections.Generic;
using UnityEngine;

namespace MilTrainSimVR.UrbanCombat.EditorTools
{
    // 빈 쇼핑몰 실내 (사용자 10/29 N05 — 배치 그림 Advice/1029_맵수정/N05_쇼핑몰_배치.png, 48×72칸을 건물 바닥에 늘려 맞춘다)
    //   · 1층부터 들어갈 수 있는 층까지 같은 구조: 네 모서리 계단실(꺾인 계단) · L자 빈 가게 넷 + 가운데 줄 빈 가게 둘(바리케이드는 층마다 무작위)
    //     · 가판대 둘 · 가운데 고장 난 에스컬레이터 둘(걸어서 오르내림 — 한쪽은 1→2층, 다른 쪽은 2→3층으로 엇갈려 오른다)
    //   · 그림은 위아래·좌우 대칭이라 긴 쪽(72칸)만 건물의 긴 변에 맞추면 된다.
    //     평면 좌표 (a, b): a = 긴 변을 따라 0~L, b = 짧은 변을 따라 0~S. 건물 로컬 (x, z) 로는 긴 변이 x 면 (a, b), 아니면 (b, a)
    //   · 1층 출입구: 짧은 벽은 가운데 4m, 긴 벽은 가로 통로 끝 2m 둘 — 앞면(파란 띠)은 늘, 나머지는 doorSides
    //     grandEntrance(11/16 청운백화점 N05): 앞면(긴 벽)의 두 출입구와 그 사이 창을 한 구멍으로 — 1층 앞쪽 가운데 줄 가게는 비워 로비로
    //   · 창: windowSides 방향 바깥벽에 계단실·출입구를 비켜 약 6m 마다 (가게는 바깥벽에서 떨어져 있어 창은 바깥 복도에 난다)
    public static partial class UrbanGreyboxBuilder
    {
        const float k_MallMinLong = 40f;     // 계단실 짧은 쪽(그림 4칸)에 오르는 줄·내려오는 줄(약 1m 씩)이 들어가야
        const float k_MallMinShort = 26.5f;  // 계단실 긴 쪽(그림 10칸)에 참 1.1 + 계단 3.0 + 층 바닥 1.2m 가 들어가야
        const float k_MallFlightRun = 3f;    // 꺾인 계단 한 줄 — 반 층(1.7m)을 단 10개로
        const float k_MallLanding = 1.1f;    // 꺾이는 참 (바깥벽 쪽 끝)
        const float k_MallDivider = 0.1f;    // 오르는 줄과 내려오는 줄 사이 벽
        const float k_ShopFront = 3f;        // 가게 입구 폭 (가운데 쪽)
        const float k_MallEntrance = 4f;     // 짧은 벽 가운데 출입구 폭
        const float k_MallEntranceHeight = 2.8f;
        const float k_MallSideDoor = 2f;     // 긴 벽 가로 통로 끝 출입구 폭
        const float k_EscFlat = 1f;          // 에스컬레이터 위·아래 평평한 곳
        const float k_EscRail = 0.15f;       // 에스컬레이터 옆 난간 두께
        const float k_Railing = 1f;          // 뚫린 곳 둘레 난간 높이

        // 그림 칸 → 평면 좌표, 평면 → 건물 로컬
        class MallFrame
        {
            public float L, S;
            public bool longIsX;
            public float A(float row) => row / 72f * L;
            public float B(float col) => col / 48f * S;
            public Vector3 P(float a, float y, float b) => longIsX ? new Vector3(a, y, b) : new Vector3(b, y, a);
            public Rect R(float a0, float a1, float b0, float b1)
            {
                Vector3 p = P(Mathf.Min(a0, a1), 0f, Mathf.Min(b0, b1)), q = P(Mathf.Max(a0, a1), 0f, Mathf.Max(b0, b1));
                return Rect.MinMaxRect(Mathf.Min(p.x, q.x), Mathf.Min(p.z, q.z), Mathf.Max(p.x, q.x), Mathf.Max(p.z, q.z));
            }
        }

        // 평면의 축 나란한 벽: alongA 면 a 를 따라 [s0, s1]·두께는 b 쪽 [t0, t1] (아니면 반대).
        // holes: (from, to, 아래, 위) — 벽을 따라 잰 평면 좌표, 이 층 바닥에서 잰 높이
        struct MallWallSpec
        {
            public bool alongA;
            public float s0, s1, t0, t1;
            public Vector4[] holes;

            public MallWallSpec(bool alongA, float s0, float s1, float t0, float t1, params Vector4[] holes)
            {
                this.alongA = alongA;
                this.s0 = s0;
                this.s1 = s1;
                this.t0 = t0;
                this.t1 = t1;
                this.holes = holes;
            }

            // 평면을 a·b 축으로 뒤집은 것 (네 모서리 거울)
            public MallWallSpec Flip(MallFrame f, bool flipA, bool flipB)
            {
                bool flipS = alongA ? flipA : flipB, flipT = alongA ? flipB : flipA;
                float sizeS = alongA ? f.L : f.S, sizeT = alongA ? f.S : f.L;
                var w = this;
                if (flipS)
                {
                    w.s0 = sizeS - s1;
                    w.s1 = sizeS - s0;
                    w.holes = new Vector4[holes.Length];
                    for (int i = 0; i < holes.Length; i++)
                        w.holes[i] = new Vector4(sizeS - holes[i].y, sizeS - holes[i].x, holes[i].z, holes[i].w);
                }
                if (flipT)
                {
                    w.t0 = sizeT - t1;
                    w.t1 = sizeT - t0;
                }
                return w;
            }
        }

        // 돌려주는 값: 층마다(0 = 1층) 바깥벽 넷(0 앞 · 1 뒤 · 2 왼 · 3 오른)의 구멍(창·출입구) — 세라믹 외벽(UrbanGreyboxBuilder.Facade.cs)이 창 둘레·기둥 자리를 맞춘다.
        // 구멍 좌표는 Wall() 과 같다: (벽 시작점에서 잰 시작 · 끝, 그 층 바닥에서 잰 아래 · 위). 옆벽은 z = k_Wall 에서 시작
        static List<Vector4>[][] BuildMall(Transform root, GreyboxBuildingSpec spec, int enterable, int floors, float W, float D, Material wall, Material floor, Materials m)
        {
            var openings = new List<Vector4>[enterable][];
            var f = new MallFrame { longIsX = W >= D, L = Mathf.Max(W, D), S = Mathf.Min(W, D) };
            var open = new bool[4];
            for (int w = 0; w < 4; w++)
                open[w] = (spec.windowSides & WorldSide(root.rotation, w)) != 0;
            bool[] door = OuterDoors(spec, root.rotation);
            int seed = StableHash(spec.name);
            // 큰 정문 — 앞면(0)이 긴 벽일 때만. 짧은 벽 앞면은 이미 가운데 4m 출입구다
            bool grand = spec.grandEntrance && f.longIsX;
            if (spec.grandEntrance && !grand)
                Debug.LogWarning($"[C] {spec.name}: 큰 정문은 앞면이 긴 벽인 쇼핑몰만 — 그대로 만든다", spec);
            // 백화점(청운백화점 N05 — UrbanGreyboxBuilder.Dept.cs): 구조 · 콜라이더는 같고 겉모양만 고급 마감. wall 은 BuildBuilding 이 벽 한 장(GB_Dept_Wall)으로 넘긴다
            bool dept = spec.departmentStore;
            Material ceiling = dept ? m.deptCeiling : m.interior;
            // 1층 바닥은 벽 밑까지 통째로 — 입구 문턱에 틈이 생기지 않게. 마감 두께만큼 인도보다 높다 (대지와 겹침 — UrbanGreyboxBuilder.Interior.cs)
            Box("Floor_1F", root, Vector3.zero, new Vector3(W, k_Curb + k_FloorFinish, D), floor, true);

            float a4 = f.A(4), b10 = f.B(10);
            float bFloor = k_Wall + k_MallLanding + k_MallFlightRun;   // 계단실에서 층 바닥이 시작하는 곳 (참·계단 다음)
            float crossA = (f.A(25) + f.A(29)) * 0.5f;                 // 가로 통로 가운데
            float va0 = f.A(26), va1 = f.A(46), vb0 = f.B(20), vb1 = f.B(28), vbm = f.S * 0.5f;   // 에스컬레이터 뚫린 곳

            // 위층 바닥판 구멍: 계단실 넷(참·계단 자리) + 에스컬레이터
            var holes = new List<Rect>();
            foreach (bool flipA in new[] { false, true })
            {
                foreach (bool flipB in new[] { false, true })
                {
                    float x0 = flipA ? f.L - (a4 - k_Partition) : k_Wall, x1 = flipA ? f.L - k_Wall : a4 - k_Partition;
                    float z0 = flipB ? f.S - bFloor : k_Wall, z1 = flipB ? f.S - k_Wall : bFloor;
                    holes.Add(f.R(x0, x1, z0, z1));
                }
            }
            holes.Add(f.R(va0, va1, vb0, vb1));

            for (int fl = 0; fl < enterable; fl++)
            {
                float y = k_Curb + fl * k_FloorHeight;
                string tag = (fl + 1) + "F";
                bool ground = fl == 0, top = fl == enterable - 1;
                // 칸막이는 위층 바닥판(또는 지붕판) 밑까지 — 바로 위가 막힌 층(Mass)이면 층 높이 그대로
                float innerHeight = !top || floors == enterable ? k_FloorHeight - k_Slab : k_FloorHeight;

                // 바깥벽: 1층 출입구 + 창
                var sides = new[] { new List<Vector4>(), new List<Vector4>(), new List<Vector4>(), new List<Vector4>() };
                for (int w = 0; w < 4; w++)
                {
                    bool shortWall = f.longIsX ? w >= 2 : w < 2;
                    float offset = w >= 2 ? k_Wall : 0f;   // 옆벽은 앞·뒤 벽 사이(k_Wall 부터)에 선다
                    var skip = new List<Vector2>();
                    if (ground && door[w])
                    {
                        if (shortWall)
                        {
                            float c = vbm;
                            sides[w].Add(new Vector4(c - k_MallEntrance * 0.5f - offset, c + k_MallEntrance * 0.5f - offset, 0f, k_MallEntranceHeight));
                            skip.Add(new Vector2(c - k_MallEntrance * 0.5f - 0.6f, c + k_MallEntrance * 0.5f + 0.6f));
                        }
                        else if (grand && w == 0)
                        {
                            // 두 출입구 바깥 끝까지 한 구멍 — 가로 통로 둘과 그 사이 로비(비운 가운데 가게 자리)로 바로 들어간다
                            float g0 = crossA - k_MallSideDoor * 0.5f, g1 = f.L - crossA + k_MallSideDoor * 0.5f;
                            sides[w].Add(new Vector4(g0 - offset, g1 - offset, 0f, k_MallEntranceHeight));
                            skip.Add(new Vector2(g0 - 0.6f, g1 + 0.6f));
                        }
                        else
                        {
                            foreach (float c in new[] { crossA, f.L - crossA })
                            {
                                sides[w].Add(new Vector4(c - k_MallSideDoor * 0.5f - offset, c + k_MallSideDoor * 0.5f - offset, 0f, k_MainDoorHeight));
                                skip.Add(new Vector2(c - k_MallSideDoor * 0.5f - 0.6f, c + k_MallSideDoor * 0.5f + 0.6f));
                            }
                        }
                    }
                    if (open[w])
                    {
                        float corner = shortWall ? b10 : a4, size = shortWall ? f.S : f.L;
                        MallWindows(sides[w], corner + 0.4f, size - corner - 0.4f, skip, offset);
                    }
                }
                openings[fl] = new[] { new List<Vector4>(sides[0]), new List<Vector4>(sides[1]), new List<Vector4>(sides[2]), new List<Vector4>(sides[3]) };
                Wall("Front_" + tag, root, new Vector3(0f, y, 0f), new Vector3(W, y, 0f), Vector3.forward, sides[0], wall);
                Wall("Back_" + tag, root, new Vector3(0f, y, D), new Vector3(W, y, D), Vector3.back, sides[1], wall);
                // 옆벽은 앞·뒤 벽 사이에만 — 모서리가 겹치면 같은 면이 두 번 그려져 깜빡인다
                Wall("Left_" + tag, root, new Vector3(0f, y, k_Wall), new Vector3(0f, y, D - k_Wall), Vector3.right, sides[2], wall);
                Wall("Right_" + tag, root, new Vector3(W, y, k_Wall), new Vector3(W, y, D - k_Wall), Vector3.left, sides[3], wall);

                // 빈 가게: 네 모서리 L자 + 가운데 줄 둘. 칸막이 두께는 가게 안쪽으로 — 그림의 통로 폭이 그대로 남게
                int room = 0;
                foreach (bool flipA in new[] { false, true })
                {
                    foreach (bool flipB in new[] { false, true })
                    {
                        MallShop(root, f, LShop(f), flipA, flipB, y, innerHeight, wall, m, seed + fl * 104729 + room++ * 7919);
                        if (!flipA)
                        {
                            // 큰 정문이면 1층 앞쪽(b 작은 쪽 = 앞면) 가운데 가게는 비운다 — 번호(room)는 그대로 세어 다른 가게의 바리케이드 자리가 안 바뀌게
                            int shopSeed = seed + fl * 104729 + room++ * 7919;
                            if (!(grand && ground && !flipB))
                                MallShop(root, f, MiddleShop(f), false, flipB, y, innerHeight, wall, m, shopSeed);
                        }
                    }
                }

                // 계단실 벽: 띠 복도 쪽 옆벽, 오르는 줄·내려오는 줄 사이 칸막이(바닥판이 없는 자리라 층 높이를 꽉 채워 위아래로 잇는다)
                foreach (bool flipA in new[] { false, true })
                {
                    foreach (bool flipB in new[] { false, true })
                    {
                        StairwellWalls(root, f, flipA, flipB, y, innerHeight, top ? innerHeight : k_FloorHeight, top && fl >= 1, wall, m, dept);
                    }
                }

                if (fl >= 1)
                {
                    SlabWithHoles("Floor_" + tag, root, new Rect(k_Wall, k_Wall, W - 2f * k_Wall, D - 2f * k_Wall), holes, y, floor, ceiling);
                    EscalatorRailings(root, f, fl, enterable, y, va0, va1, vb0, vb1, vbm, m, dept);
                }

                // 큰 정문 백화점: 비운 가게 자리(로비)에 안내데스크 (UrbanGreyboxBuilder.Dept.cs)
                if (grand && ground && dept)
                    DeptInfoDesk(root, f, y, m);

                // 가판대 둘 (에스컬레이터 앞뒤)
                foreach (bool flipA in new[] { false, true })
                {
                    float k0 = flipA ? f.L - f.A(19) : f.A(9), k1 = flipA ? f.L - f.A(9) : f.A(19);
                    Kiosk(root, f, k0, k1, f.B(22), f.B(26), y, m, dept);
                }
            }

            // 꺾인 계단: 층마다 한 벌 (네 모서리)
            foreach (bool flipA in new[] { false, true })
            {
                foreach (bool flipB in new[] { false, true })
                {
                    for (int fl = 0; fl < enterable - 1; fl++)
                        StairwellFlights(root, f, flipA, flipB, fl, floor, m);
                }
            }

            // 에스컬레이터: 1→2층은 b 큰 쪽 반에서 a 큰 쪽으로, 2→3층은 b 작은 쪽 반에서 a 작은 쪽으로 (엇갈림)
            if (enterable >= 2)
                Escalator(root, f, vbm, vb1, va0, va1, k_Curb, k_Curb + k_FloorHeight, k_Curb, m, dept);
            if (enterable >= 3)
                Escalator(root, f, vb0, vbm, va1, va0, k_Curb + k_FloorHeight, k_Curb + 2f * k_FloorHeight, k_Curb + k_FloorHeight - k_Slab, m, dept);
            // 백화점: 천장 들보 · 매입등 · 천창, 뚫린 곳 둘레 바닥 테두리 · 원목 띠, 실내등 · 반사 기준점
            if (dept)
                DeptInterior(root, f, holes, enterable, floors, W, D, va0, va1, vb0, vb1, m);
            return openings;
        }

        // ---------------------------------------------------------------- 가게

        // 모서리 L자 가게 (그림 왼쪽 위 기준): 넓은 칸 a 7~20 × b 4~17, 좁은 칸 a 20~25 × b 4~12.
        // 문: 넓은 칸 안쪽 옆(가운데 쪽)에 가게 입구 3m, 좁은 칸 끝(가로 통로 쪽)에 1m
        static MallShopPlan LShop(MallFrame f)
        {
            float t = k_Partition;
            float a7 = f.A(7), a20 = f.A(20), a25 = f.A(25), b4 = f.B(4), b12 = f.B(12), b17 = f.B(17);
            float shopA = (a7 + a20) * 0.5f, doorB = (b4 + b12) * 0.5f;
            return new MallShopPlan
            {
                walls = new[]
                {
                    new MallWallSpec(false, b4, b17, a7, a7 + t),
                    new MallWallSpec(true, a7 + t, a20 - t, b17 - t, b17, new Vector4(shopA - k_ShopFront * 0.5f, shopA + k_ShopFront * 0.5f, 0f, k_MainDoorHeight)),
                    new MallWallSpec(false, b12, b17, a20 - t, a20),
                    new MallWallSpec(true, a20, a25 - t, b12 - t, b12),
                    new MallWallSpec(false, b4, b12, a25 - t, a25, new Vector4(doorB - k_DoorWidth * 0.5f, doorB + k_DoorWidth * 0.5f, 0f, k_DoorHeight)),
                    new MallWallSpec(true, a7 + t, a25 - t, b4, b4 + t),
                },
                areas = new[] { Rect.MinMaxRect(a7 + t, b4 + t, a20 - t, b17 - t), Rect.MinMaxRect(a20 - t, b4 + t, a25 - t, b12 - t) },
                clear = new[] { Rect.MinMaxRect(shopA - 1.8f, b17 - 2f, shopA + 1.8f, b17), Rect.MinMaxRect(a25 - 2f, doorB - 1f, a25, doorB + 1f) },
            };
        }

        // 가운데 줄 가게 (그림 왼쪽 가운데): a 29~43 × b 4~12. 가게 입구는 안쪽(에스컬레이터 쪽) 3m, 바깥 복도 쪽 1m
        static MallShopPlan MiddleShop(MallFrame f)
        {
            float t = k_Partition;
            float a29 = f.A(29), a43 = f.A(43), b4 = f.B(4), b12 = f.B(12), am = (a29 + a43) * 0.5f;
            return new MallShopPlan
            {
                walls = new[]
                {
                    new MallWallSpec(false, b4, b12, a29, a29 + t),
                    new MallWallSpec(false, b4, b12, a43 - t, a43),
                    new MallWallSpec(true, a29 + t, a43 - t, b4, b4 + t, new Vector4(am - k_DoorWidth * 0.5f, am + k_DoorWidth * 0.5f, 0f, k_DoorHeight)),
                    new MallWallSpec(true, a29 + t, a43 - t, b12 - t, b12, new Vector4(am - k_ShopFront * 0.5f, am + k_ShopFront * 0.5f, 0f, k_MainDoorHeight)),
                },
                areas = new[] { Rect.MinMaxRect(a29 + t, b4 + t, a43 - t, b12 - t) },
                clear = new[] { Rect.MinMaxRect(am - 1.8f, b12 - 2f, am + 1.8f, b12), Rect.MinMaxRect(am - 1f, b4, am + 1f, b4 + 2f) },
            };
        }

        // 가게 한 칸 — 평면 좌표(Rect: x = a, y = b). areas: 바리케이드 놓을 곳, clear: 문 앞 비울 곳
        class MallShopPlan
        {
            public MallWallSpec[] walls;
            public Rect[] areas, clear;
        }

        static void MallShop(Transform root, MallFrame f, MallShopPlan plan, bool flipA, bool flipB, float y, float height, Material wall, Materials m, int seed)
        {
            foreach (MallWallSpec w in plan.walls)
                MallWall(root, f, w.Flip(f, flipA, flipB), y, height, wall);
            var areas = new List<Rect>();
            foreach (Rect r in plan.areas)
                areas.Add(FlipRect(f, r, flipA, flipB));
            var clear = new List<Rect>();
            foreach (Rect r in plan.clear)
                clear.Add(FlipRect(f, r, flipA, flipB));
            ShopBarricades(root, f, areas, clear, new System.Random(seed), y, m.barricade);
        }

        static Rect FlipRect(MallFrame f, Rect r, bool flipA, bool flipB)
        {
            float a0 = flipA ? f.L - r.xMax : r.xMin, a1 = flipA ? f.L - r.xMin : r.xMax;
            float b0 = flipB ? f.S - r.yMax : r.yMin, b1 = flipB ? f.S - r.yMin : r.yMax;
            return Rect.MinMaxRect(a0, b0, a1, b1);
        }

        // 빈 가게 바리케이드 1~3개 (높이 1.1m — 앉으면 가려지고 서면 너머로 쏜다). 벽·서로 0.9m 넘게 떼어 어디로든 지나갈 수 있게, 문 앞(clear)은 비운다.
        // 겉모양은 모래주머니 벽 모델 (UrbanGreyboxBuilder.Props.cs)
        static void ShopBarricades(Transform root, MallFrame f, List<Rect> areas, List<Rect> clear, System.Random rng, float y, Material mat)
        {
            var placed = new List<Rect>();
            int count = rng.Next(1, 4);
            for (int k = 0; k < count; k++)
            {
                for (int tries = 0; tries < 16; tries++)
                {
                    Rect area = areas[rng.Next(areas.Count)];
                    float len = 1.6f + 0.2f * rng.Next(6);
                    bool alongA = rng.Next(2) == 0;
                    float la = alongA ? len : k_BarricadeThick, lb = alongA ? k_BarricadeThick : len;
                    if (area.width < la + 1.8f || area.height < lb + 1.8f)
                        continue;
                    float a0 = Mathf.Lerp(area.xMin + 0.9f, area.xMax - 0.9f - la, (float)rng.NextDouble());
                    float b0 = Mathf.Lerp(area.yMin + 0.9f, area.yMax - 0.9f - lb, (float)rng.NextDouble());
                    var r = new Rect(a0, b0, la, lb);
                    if (Overlaps(r, clear) || Overlaps(Expand(r, 0.9f), placed))
                        continue;
                    placed.Add(r);
                    Skin(MBox("Barricade", root, f, r.xMin, r.xMax, y, y + k_Barricade, r.yMin, r.yMax, mat, true), "SandbagWall");
                    break;
                }
            }
        }

        // 가판대: 카운터 1.05m (앉으면 가림) + 가운데 진열대 2.1m (서서 보는 눈높이도 일부 끊는다). 카운터 겉모양만 낮은 가판대 모델.
        // 백화점은 진열대를 회색 대신 호두나무로 (11/06 — 원목 들보 · 난간과 맞춘다)
        static void Kiosk(Transform root, MallFrame f, float a0, float a1, float b0, float b1, float y, Materials m, bool dept)
        {
            Skin(MBox("Kiosk", root, f, a0, a1, y, y + 1.05f, b0, b1, m.furniture, true), "KioskLow");
            float am = (a0 + a1) * 0.5f, bm = (b0 + b1) * 0.5f, half = Mathf.Max(0.5f, (a1 - a0) * 0.5f - 1.2f);
            MBox("KioskDisplay", root, f, am - half, am + half, y + 1.05f, y + 2.1f, bm - 0.2f, bm + 0.2f, dept ? m.deptWood : m.sign, true);
        }

        // ---------------------------------------------------------------- 계단실 (그림 왼쪽 위 기준: a 0~4칸 × b 0~10칸, 바깥 두 벽이 모서리)
        //   b 쪽으로 달리는 줄 둘: 오르는 줄(a 바깥쪽)은 층 바닥(b 큰 쪽) → 참(b 작은 쪽, 긴 벽 앞), 내려오는 줄(a 안쪽)은 참 → 다음 층 바닥.
        //   층마다 같은 자리에 쌓인다 — 위층 계단은 단이 얇아 아래층 계단 머리 위가 3m 넘게 빈다

        static void StairwellFlights(Transform root, MallFrame f, bool flipA, bool flipB, int fl, Material floor, Materials m)
        {
            float a4 = f.A(4), strip = (a4 - k_Partition - k_Wall - k_MallDivider) * 0.5f;
            float aUp0 = k_Wall, aUp1 = k_Wall + strip, aDown0 = aUp1 + k_MallDivider, aDown1 = a4 - k_Partition;
            float bLanding = k_Wall + k_MallLanding, bFloor = bLanding + k_MallFlightRun;
            float y0 = k_Curb + fl * k_FloorHeight, ym = y0 + k_FloorHeight * 0.5f, y1 = y0 + k_FloorHeight;
            bool ground = fl == 0;   // 1층 계단은 바닥까지 꽉 — 밑으로 파고들 틈이 없게
            float FA(float a) => flipA ? f.L - a : a;
            float FB(float b) => flipB ? f.S - b : b;
            void Flight(float a0, float a1, float bStart, float bEnd, float yb, float yt)
            {
                Steps("Stairs", root, f.P(FA(a0), yb, FB(bStart)), f.P(FA(a1), yt, FB(bEnd)), f.longIsX, floor, 10,
                    ground ? k_Curb : float.NaN, ground ? 0f : 0.3f, ground);
                float am = FA((a0 + a1) * 0.5f);
                Ramp("StairRamp", root, f.P(am, yb, FB(bStart)), f.P(am, yt, FB(bEnd)), a1 - a0);
            }
            Flight(aUp0, aUp1, bFloor, bLanding, y0, ym);
            MBox("Landing", root, f, FA(k_Wall), FA(aDown1), ground ? k_Curb : ym - k_Slab, ym, FB(k_Wall), FB(bLanding), floor, true);
            Flight(aDown0, aDown1, bLanding, bFloor, ym, y1);
        }

        static void StairwellWalls(Transform root, MallFrame f, bool flipA, bool flipB, float y, float wallHeight, float dividerHeight, bool topRailing, Material wall,
            Materials m, bool dept)
        {
            float a4 = f.A(4), strip = (a4 - k_Partition - k_Wall - k_MallDivider) * 0.5f;
            float aUp1 = k_Wall + strip, bLanding = k_Wall + k_MallLanding, bFloor = bLanding + k_MallFlightRun;
            float FA(float a) => flipA ? f.L - a : a;
            float FB(float b) => flipB ? f.S - b : b;
            MBox("StairWall", root, f, FA(a4 - k_Partition), FA(a4), y, y + wallHeight, FB(k_Wall), FB(bFloor), wall, true);
            MBox("StairDivider", root, f, FA(aUp1), FA(aUp1 + k_MallDivider), y, y + dividerHeight, FB(bLanding), FB(bFloor), wall, true);
            // 맨 위층: 오르는 줄 자리는 더 오를 계단 없이 뚫려 있다 — 층 바닥 끝에 난간
            if (topRailing)
                MallRailing(root, f, FA(k_Wall), FA(aUp1), y, FB(bFloor), FB(bFloor + 0.1f), m, dept);
        }

        // ---------------------------------------------------------------- 에스컬레이터

        // 한 대: 옆 범위 b c0~c1, a aBot → aTop 으로 yBot → yTop 오른다. 몸통(단 아래)은 baseY 까지 꽉 — 2→3층 것은 2층 바닥판 밑면에서 끝나 1층 머리 위가 비어 있다
        // dept: 백화점 — 콜라이더(몸통 · 난간 · 채움 · 경사로)는 그대로 두고 단 · 난간 상자는 안 보이게, 겉모양은 경사형 무빙워크(DeptWalkway)
        static void Escalator(Transform root, MallFrame f, float c0, float c1, float aBot, float aTop, float yBot, float yTop, float baseY, Materials m, bool dept)
        {
            float dir = Mathf.Sign(aTop - aBot);
            float i0 = aBot + dir * k_EscFlat, i1 = aTop - dir * k_EscFlat;   // 기울어진 구간 (약 18°)
            // 몸통·단은 옆 난간 바깥면보다 2cm 안쪽 — 같은 평면에 같은 쪽을 보는 면이 겹치면 깜빡인다
            float s0 = c0 + 0.02f, s1 = c1 - 0.02f;
            Material body = dept ? m.deptWhite : m.dark;
            if (yBot - baseY > 0.05f)
                MBox("EscFlat", root, f, aBot, i0, baseY, yBot, s0, s1, body, true);
            MBox("EscFlat", root, f, i1, aTop, baseY, yTop, s0, s1, body, true);
            Steps("EscSteps", root, f.P(i0, yBot, s0), f.P(i1, yTop, s1), !f.longIsX, m.dark, 20, baseY, 0f, true, !dept);
            float cm = (c0 + c1) * 0.5f;
            Ramp("EscRamp", root, f.P(i0, yBot, cm), f.P(i1, yTop, cm), c1 - c0 - 2f * k_EscRail);
            // 옆 난간 (높이 1m): 평평한 곳은 상자, 기울어진 곳은 경사 따라 기운 상자 — 단과 겹치게 0.3m 아래까지
            foreach (float r0 in new[] { c0, c1 - k_EscRail })
            {
                float r1 = r0 + k_EscRail, rm = (r0 + r1) * 0.5f;
                GameObject lowFlat = MBox("EscRail", root, f, aBot, i0, yBot, yBot + k_Railing, r0, r1, m.sign, true);
                GameObject highFlat = MBox("EscRail", root, f, i1, aTop, yTop, yTop + k_Railing, r0, r1, m.sign, true);
                GameObject sloped = SlopedBox("EscRail", root, f.P(i0, yBot, rm), f.P(i1, yTop, rm), k_EscRail - 0.01f, 0.3f, k_Railing, m.sign);
                if (dept)
                {
                    // 백화점: 통유리 난간 — 'Ignore Raycast' 층이라 몸은 막고 총알 · 적 시선은 유리를 지나간다 (11/18 사용자)
                    foreach (GameObject rail in new[] { lowFlat, highFlat, sloped })
                    {
                        ColliderOnly(rail);
                        rail.layer = k_IgnoreRaycastLayer;
                    }
                }
            }
            if (dept)
                DeptWalkway(root, f, c0, c1, aBot, aTop, i0, i1, yBot, yTop, baseY, s0, s1, m);
        }

        // 뚫린 곳 둘레 난간 (2층 이상) — 에스컬레이터가 이 층 바닥에 닿는 반쪽 끝만 비운다. 백화점이면 원목 · 황동 난간 (MallRailing)
        static void EscalatorRailings(Transform root, MallFrame f, int fl, int enterable, float y, float va0, float va1, float vb0, float vb1, float vbm,
            Materials m, bool dept)
        {
            const float t = 0.1f;
            MallRailing(root, f, va0 - t, va1 + t, y, vb0 - t, vb0, m, dept);
            MallRailing(root, f, va0 - t, va1 + t, y, vb1, vb1 + t, m, dept);
            bool upArrives = fl == 1;                       // 1→2층 것(b vbm~vb1)이 a va1 끝에서 2층에 닿는다
            bool secondStarts = fl == 1 && enterable >= 3;  // 2→3층 것(b vb0~vbm)이 a va1 끝 2층에서 출발
            bool secondArrives = fl == 2;                   // 2→3층 것이 a va0 끝에서 3층에 닿는다
            if (!secondStarts)
                MallRailing(root, f, va1, va1 + t, y, vb0, vbm, m, dept);
            if (!upArrives)
                MallRailing(root, f, va1, va1 + t, y, vbm, vb1, m, dept);
            if (!secondArrives)
                MallRailing(root, f, va0 - t, va0, y, vb0, vbm, m, dept);
            MallRailing(root, f, va0 - t, va0, y, vbm, vb1, m, dept);
        }

        // ---------------------------------------------------------------- 작은 도구

        // 평면 좌표 상자 (a·b 범위는 순서 상관없음)
        static GameObject MBox(string name, Transform root, MallFrame f, float a0, float a1, float y0, float y1, float b0, float b1, Material mat, bool collide)
        {
            Vector3 p = f.P(Mathf.Min(a0, a1), y0, Mathf.Min(b0, b1)), q = f.P(Mathf.Max(a0, a1), y1, Mathf.Max(b0, b1));
            return Box(name, root, Vector3.Min(p, q), Vector3.Max(p, q), mat, collide);
        }

        // 평면 벽 하나를 문 구멍을 피해 상자 여러 개로 (Wall 과 같은 방식 — 구멍 띠는 문 위 인방만)
        static void MallWall(Transform root, MallFrame f, MallWallSpec w, float y, float height, Material mat)
        {
            var cuts = new List<float> { w.s0, w.s1 };
            foreach (Vector4 h in w.holes)
            {
                cuts.Add(Mathf.Clamp(h.x, w.s0, w.s1));
                cuts.Add(Mathf.Clamp(h.y, w.s0, w.s1));
            }
            cuts.Sort();
            for (int i = 0; i < cuts.Count - 1; i++)
            {
                float u0 = cuts[i], u1 = cuts[i + 1];
                if (u1 - u0 < 0.01f)
                    continue;
                float mid = (u0 + u1) * 0.5f, v0 = 0f, v1 = height;
                foreach (Vector4 h in w.holes)
                {
                    if (mid > h.x && mid < h.y)
                        v0 = Mathf.Min(h.w, height);   // 문 위 인방만 남긴다 (문은 바닥부터라 문턱이 없다)
                }
                if (v1 - v0 < 0.01f)
                    continue;
                if (w.alongA)
                    MBox("Partition", root, f, u0, u1, y + v0, y + v1, w.t0, w.t1, mat, true);
                else
                    MBox("Partition", root, f, w.t0, w.t1, y + v0, y + v1, u0, u1, mat, true);
            }
        }

        // 바깥벽 창: s0~s1 에서 skip(출입구) 을 뺀 구간마다 약 6m 에 하나씩 (offset: 벽 시작점까지 거리 — Windows 와 같은 뜻)
        static void MallWindows(List<Vector4> holes, float s0, float s1, List<Vector2> skip, float offset)
        {
            foreach (Vector2 span in UgMinus(new Vector2(s0, s1), skip))
            {
                int n = Mathf.Max(1, Mathf.RoundToInt((span.y - span.x) / 6f));
                float len = (span.y - span.x) / n;
                for (int k = 0; k < n; k++)
                    Windows(holes, span.x + k * len, span.x + (k + 1) * len, offset, 0.9f, 2.1f);
            }
        }

        // 경사를 따라 기운 상자 (에스컬레이터 옆 난간): from→to 가 상자 가운데 선, 옆 폭 width, 선 아래 below · 위 above (경사에 수직으로 잰 두께).
        // 돌려주는 것은 상자(묶음 밑) — 백화점은 겉모양을 떼고 콜라이더만 쓴다
        static GameObject SlopedBox(string name, Transform parent, Vector3 from, Vector3 to, float width, float below, float above, Material mat)
        {
            var holder = new GameObject(name).transform;
            holder.SetParent(parent, false);
            Vector3 dir = to - from;
            holder.localRotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            holder.localPosition = (from + to) * 0.5f;
            float half = dir.magnitude * 0.5f;
            return Box(name, holder, new Vector3(-width * 0.5f, -below, -half), new Vector3(width * 0.5f, above, half), mat, true);
        }
    }
}
