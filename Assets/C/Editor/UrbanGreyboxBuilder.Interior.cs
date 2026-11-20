using System.Collections.Generic;
using UnityEngine;

namespace MilTrainSimVR.UrbanCombat.EditorTools
{
    // 들어갈 수 있는 건물의 실내 — 복도형 평면 (사용자 10/23: "방 전체가 뚫려 있으면 안 되고 복도형 통로", "계단은 복도 옆", "2층도 북쪽으로 창")
    //   · 앞문에서 들어오는 남북 복도(폭 3.4m = 걷는 줄 2m + 왼쪽 계단 자리 1.4m)가 건물 깊이 끝까지 간다. 통과 건물은 뒷문까지 일직선
    //   · 앞면이 18m 넘게 넓으면 가운데에 동서 복도(2m)를 하나 더 — 복도에서 먼 방도 복도로 들어가게
    //   · 나머지는 방(한 칸 약 8m 이하)으로 나누고 방마다 복도 쪽 문(1×2.2m) · 바깥벽 창 · 가구(선반·책상·카운터 = 엄폐물)
    //   · 계단은 남북 복도 왼쪽 벽에 붙어 뒷벽 쪽으로 오른다. 2층은 같은 평면이고 복도 양 끝은 문 대신 창. 1층만인 건물은 계단 자리 없이 복도 2m
    //   · 계단 셋(sideStairs, 사용자 10/29 N31): 양 끝 뒤쪽 방 줄을 한 칸으로 합쳐 바깥벽에 붙은 계단방으로 — 1층은 계단, 2층은 올라와 복도로 나가는 통로
    //   · 창은 건물마다 정한 방향(GreyboxBuildingSpec.windowSides — 길·샛길 쪽)의 바깥벽에만. 나머지 바깥벽은 막혀서 선반을 붙인다
    //   · 1층 출입구: 앞문은 늘, 뒷문(앞문과 마주 봄)·옆문(동서 복도 끝, 없으면 끝 방)은 doorSides(맵 기준)·throughPassage·sideDoor 로
    // 치수는 실제 건물 기준(문 1×2.2m, 복도 2m, 단 0.17m)이라 사람 모델이 와도 다시 맞출 일이 거의 없다
    public static partial class UrbanGreyboxBuilder
    {
        const float k_Partition = 0.15f;     // 실내 칸막이 두께
        const float k_Lane = 2f;             // 복도 걷는 폭 = 앞·뒷문 폭
        const float k_StairStrip = 1.4f;     // 복도 왼쪽 계단 자리 (계단 1.2m + 여유)
        const float k_StairRun = 6f;         // 계단 수평 길이 — 3.4m 를 단 20개(0.17m)로
        const float k_StairRunMin = 4.8f;    // 깊이가 모자란 건물은 계단을 여기까지 가파르게 (약 35°)
        const float k_StairApproach = 1f;    // 계단 아래 끝 앞 디딜 자리
        const float k_StairLanding = 1.2f;   // 계단 꼭대기 앞 내리는 자리
        const float k_MinInteriorWidth = 7.7f;   // 앞면 폭 — 복도(2m) 오른쪽에 폭 2.5m 방이 들어가야
        const float k_MinInteriorDepth = 5f;
        const float k_MinStairDepth = 2f * k_Wall + k_StairApproach + k_StairRunMin + k_StairLanding;   // 7.4m
        const float k_DoorWidth = 1f;
        const float k_DoorHeight = 2.2f;     // 2.1m 면 캐릭터 컨트롤러 머리가 문틀에 걸렸다
        const float k_MainDoorHeight = 2.4f;
        const float k_RoomTarget = 8f;       // 방 한 칸의 대략 최대 길이

        // 실내 벽 하나 (높이 0 기준) — 층마다 높이만 올려서 다시 쓴다
        class InnerWall
        {
            public readonly Vector3 a, b, inward;
            public readonly List<Vector4> holes = new List<Vector4>();
            readonly bool m_AlongX;

            public InnerWall(Vector3 a, Vector3 b, Vector3 inward)
            {
                this.a = a;
                this.b = b;
                this.inward = inward;
                m_AlongX = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.z - a.z);
            }

            // 벽을 따라 잰 절대 좌표(x 또는 z)를 가운데로 문
            public void AddDoor(float center)
            {
                float u = center - (m_AlongX ? a.x : a.z);
                holes.Add(new Vector4(u - k_DoorWidth * 0.5f, u + k_DoorWidth * 0.5f, 0f, k_DoorHeight));
            }

            // 바닥부터 천장까지 뚫린 곳 — 복도끼리 만나는 자리
            public void AddOpening(float from, float to)
            {
                float s = m_AlongX ? a.x : a.z;
                holes.Add(new Vector4(from - s, to - s, 0f, 100f));
            }
        }

        class Room
        {
            public Rect area;                                         // x = 폭 방향, y = 깊이(z) 방향
            public readonly List<Vector2> doors = new List<Vector2>(); // (벽, 벽을 따라 잰 문 가운데) — 벽: 0 앞(-z) 1 뒤(+z) 2 왼(-x) 3 오른(+x)
            public bool stair;                                        // 계단방 — 가구를 두지 않는다 (지나가는 통로)

            public bool HasDoor(int wall)
            {
                foreach (Vector2 d in doors)
                {
                    if ((int)d.x == wall)
                        return true;
                }
                return false;
            }
        }

        class InteriorPlan
        {
            public float W, D;
            public float hall0, hall1, lane0, lane1;   // 남북 복도 x 범위, 걷는 줄(앞·뒷문) x 범위
            public bool stairs, leftRooms, cross;
            public bool[] open = { true, true, true, true };   // 창을 내는 바깥벽 — 0 앞 1 뒤 2 왼 3 오른
            public float cz0, cz1;                     // 동서 복도 z 범위
            public float stairZ0, stairZ1;             // 계단 아래·위 끝 z
            public Rect Hole => new Rect(hall0, stairZ0, k_StairStrip, stairZ1 - stairZ0);
            public readonly List<Rect> sideHoles = new List<Rect>();   // 양 끝 계단방의 계단 자리 (바깥벽에 붙은 폭 1.4m 띠)
            public readonly List<Room> rooms = new List<Room>();
            public readonly List<InnerWall> walls = new List<InnerWall>();

            // 2층 바닥판에 뚫는 계단 자리 모두
            public List<Rect> Holes
            {
                get
                {
                    var holes = new List<Rect> { Hole };
                    holes.AddRange(sideHoles);
                    return holes;
                }
            }
        }

        static InteriorPlan PlanInterior(float W, float D, bool stairs, bool sideStairs = false)
        {
            var p = new InteriorPlan { W = W, D = D, stairs = stairs };
            p.lane0 = W * 0.5f - k_Lane * 0.5f;
            p.lane1 = W * 0.5f + k_Lane * 0.5f;
            p.hall1 = p.lane1;
            // 계단 자리는 2층까지 가는 건물만 — 1층만인 건물은 복도가 걷는 줄 폭 그대로
            float strip = stairs ? k_StairStrip : 0f;
            // 왼쪽에 방(폭 2.5m 이상)이 들어갈 때만 복도 왼쪽 벽을 세운다 — 좁은 건물은 복도가 왼쪽 바깥벽까지
            p.leftRooms = p.lane0 - strip - k_Partition - k_Wall >= 2.5f;
            p.hall0 = p.leftRooms ? p.lane0 - strip : k_Wall;
            // 계단 꼭대기는 뒷벽 앞 내리는 자리에서 끝난다. 깊이가 모자라면 아래 끝 앞 디딜 자리를 남기려고 조금 가파르게
            float run = Mathf.Clamp(D - 2f * k_Wall - k_StairLanding - k_StairApproach, k_StairRunMin, k_StairRun);
            p.stairZ1 = D - k_Wall - k_StairLanding;
            p.stairZ0 = p.stairZ1 - run;
            // 동서 복도는 계단 바로 앞을 지나게(계단 아래 끝 = 복도 뒤쪽 끝) — 앞쪽 방 깊이가 3m 는 돼야 낸다
            float zc = stairs ? Mathf.Min(D * 0.5f, p.stairZ0 - k_Lane * 0.5f) : D * 0.5f;
            p.cross = p.leftRooms && W > 18f && zc - k_Lane * 0.5f - k_Partition - k_Wall >= 3f;
            p.cz0 = zc - k_Lane * 0.5f;
            p.cz1 = zc + k_Lane * 0.5f;

            float leftX1 = p.hall0 - k_Partition, rightX0 = p.hall1 + k_Partition;
            InnerWall hallLeft = p.leftRooms ? new InnerWall(new Vector3(p.hall0, 0f, k_Wall), new Vector3(p.hall0, 0f, D - k_Wall), Vector3.left) : null;
            var hallRight = new InnerWall(new Vector3(p.hall1, 0f, k_Wall), new Vector3(p.hall1, 0f, D - k_Wall), Vector3.right);
            if (hallLeft != null)
                p.walls.Add(hallLeft);
            p.walls.Add(hallRight);

            if (p.cross)
            {
                hallLeft.AddOpening(p.cz0, p.cz1);
                hallRight.AddOpening(p.cz0, p.cz1);
                var frontL = new InnerWall(new Vector3(k_Wall, 0f, p.cz0), new Vector3(leftX1, 0f, p.cz0), Vector3.back);
                var frontR = new InnerWall(new Vector3(rightX0, 0f, p.cz0), new Vector3(W - k_Wall, 0f, p.cz0), Vector3.back);
                var backL = new InnerWall(new Vector3(k_Wall, 0f, p.cz1), new Vector3(leftX1, 0f, p.cz1), Vector3.forward);
                var backR = new InnerWall(new Vector3(rightX0, 0f, p.cz1), new Vector3(W - k_Wall, 0f, p.cz1), Vector3.forward);
                p.walls.Add(frontL);
                p.walls.Add(frontR);
                p.walls.Add(backL);
                p.walls.Add(backR);
                float frontZ1 = p.cz0 - k_Partition, backZ0 = p.cz1 + k_Partition;
                // 계단 셋: 뒤쪽 양 끝 방 줄이 계단방 (2층까지 가는 건물만)
                bool side = sideStairs && stairs;
                // 앞 왼쪽: 동서 복도는 뒤, 남북 복도는 오른쪽 (앞쪽이라 계단과 안 겹친다)
                AddRegion(p, Rect.MinMaxRect(k_Wall, k_Wall, leftX1, frontZ1), frontL, 1, hallLeft, 3);
                // 뒤 왼쪽: 남북 복도 쪽은 계단 옆이라 그쪽 문은 없다 (계단이 없는 1층 건물은 낸다)
                AddRegion(p, Rect.MinMaxRect(k_Wall, backZ0, leftX1, D - k_Wall), backL, 0, stairs ? null : hallLeft, stairs ? -1 : 3, side ? 2 : -1);
                AddRegion(p, Rect.MinMaxRect(rightX0, k_Wall, W - k_Wall, frontZ1), frontR, 1, hallRight, 2);
                AddRegion(p, Rect.MinMaxRect(rightX0, backZ0, W - k_Wall, D - k_Wall), backR, 0, hallRight, 2, side ? 3 : -1);
            }
            else
            {
                if (p.leftRooms)
                    AddStrip(p, Rect.MinMaxRect(k_Wall, k_Wall, leftX1, D - k_Wall), hallLeft, 3, stairs);
                AddStrip(p, Rect.MinMaxRect(rightX0, k_Wall, W - k_Wall, D - k_Wall), hallRight, 2, false);
            }
            return p;
        }

        // 동서 복도 옆 구역 → 열(x)로 나눈 방. 깊이가 10m 넘으면 앞뒤 두 줄(바깥 줄 방은 안쪽 방을 거쳐 들어간다)
        // corridorSide: 방에서 본 동서 복도 쪽 벽(0 앞 / 1 뒤), hallSide: 방에서 본 남북 복도 쪽 벽(2 왼 / 3 오른)
        // stairSide: 바깥 끝 열(2 왼쪽 끝 / 3 오른쪽 끝)을 줄 나누지 않은 계단방으로 — 뒤쪽 구역(복도가 앞)에만 쓴다
        static void AddRegion(InteriorPlan p, Rect area, InnerWall corridorWall, int corridorSide, InnerWall hallWall, int hallSide, int stairSide = -1)
        {
            if (area.width < 2.5f || area.height < 2.5f)
                return;
            int nx = Mathf.Max(1, Mathf.RoundToInt(area.width / k_RoomTarget));
            int nz = area.height > 10f ? 2 : 1;
            float cw = area.width / nx, rh = area.height / nz, h = k_Partition * 0.5f;
            // 계단(디딜 자리·가장 가파른 길이·내리는 자리)이 방 깊이에 들어갈 때만
            int stairCol = stairSide == 2 ? 0 : stairSide == 3 ? nx - 1 : -1;
            if (area.height < k_StairApproach + k_StairRunMin + k_StairLanding || cw < k_StairStrip + 2.5f)
                stairCol = -1;
            for (int i = 0; i < nx; i++)
            {
                float x0 = area.xMin + i * cw, x1 = x0 + cw, xm = (x0 + x1) * 0.5f;
                if (i > 0)
                    p.walls.Add(new InnerWall(new Vector3(x0 - h, 0f, area.yMin), new Vector3(x0 - h, 0f, area.yMax), Vector3.right));
                bool byHall = hallWall != null && (hallSide == 3 ? i == nx - 1 : i == 0);
                if (i == stairCol)
                {
                    AddStairRoom(p, Rect.MinMaxRect(x0 + (i > 0 ? h : 0f), area.yMin, x1 - (i < nx - 1 ? h : 0f), area.yMax), stairSide == 3,
                        corridorWall, corridorSide, byHall ? hallWall : null, hallSide, xm);
                    continue;
                }
                int corridorRow = corridorSide == 1 ? nz - 1 : 0;
                for (int j = 0; j < nz; j++)
                {
                    float z0 = area.yMin + j * rh, z1 = z0 + rh;
                    var room = new Room
                    {
                        area = Rect.MinMaxRect(x0 + (i > 0 ? h : 0f), z0 + (j > 0 ? h : 0f), x1 - (i < nx - 1 ? h : 0f), z1 - (j < nz - 1 ? h : 0f)),
                    };
                    if (j == corridorRow)
                    {
                        corridorWall.AddDoor(xm);
                        room.doors.Add(new Vector2(corridorSide, xm));
                        if (nz == 2)
                            room.doors.Add(new Vector2(1 - corridorSide, xm));   // 바깥 줄 방으로 가는 문
                    }
                    else
                    {
                        room.doors.Add(new Vector2(corridorSide, xm));            // 안쪽 줄 방으로 나가는 문
                    }
                    if (byHall)
                    {
                        float zm = (room.area.yMin + room.area.yMax) * 0.5f;
                        hallWall.AddDoor(zm);
                        room.doors.Add(new Vector2(hallSide, zm));
                    }
                    p.rooms.Add(room);
                }
                if (nz == 2)
                {
                    float zb = area.yMin + rh;
                    var w = new InnerWall(new Vector3(x0 + (i > 0 ? h : 0f), 0f, zb - h), new Vector3(x1 - (i < nx - 1 ? h : 0f), 0f, zb - h), Vector3.forward);
                    w.AddDoor(xm);
                    p.walls.Add(w);
                }
            }
        }

        // 계단방 한 칸: 문은 동서 복도 쪽(남북 복도 옆이면 그쪽도). 계단은 바깥벽에 붙어 복도 반대쪽(뒷벽)으로 오른다 —
        // 복도 문으로 들어와 디딜 자리(1m) → 계단 → 뒷벽 앞 내리는 자리. 2층은 구멍 옆으로 걸어 나와 같은 자리의 복도 문으로
        static void AddStairRoom(InteriorPlan p, Rect area, bool right, InnerWall corridorWall, int corridorSide, InnerWall hallWall, int hallSide, float doorX)
        {
            var room = new Room { area = area, stair = true };
            corridorWall.AddDoor(doorX);
            room.doors.Add(new Vector2(corridorSide, doorX));
            if (hallWall != null)
            {
                hallWall.AddDoor(area.center.y);
                room.doors.Add(new Vector2(hallSide, area.center.y));
            }
            p.rooms.Add(room);
            float run = Mathf.Clamp(area.height - k_StairApproach - k_StairLanding, k_StairRunMin, k_StairRun);
            float x0 = right ? area.xMax - k_StairStrip : area.xMin;
            p.sideHoles.Add(new Rect(x0, area.yMin + k_StairApproach, k_StairStrip, run));
        }

        // 남북 복도 한쪽 띠 → 깊이(z) 방향으로 나눈 방(약 6m), 문은 복도 쪽.
        // stairBeside: 왼쪽 띠 — 계단 옆 구간엔 복도 문을 못 내서 비키거나, 통째로 계단 옆인 방은 앞쪽 방을 거쳐 들어간다
        static void AddStrip(InteriorPlan p, Rect area, InnerWall hallWall, int hallSide, bool stairBeside)
        {
            if (area.width < 2.5f || area.height < 2.5f)
                return;
            int nz = Mathf.Max(1, Mathf.RoundToInt(area.height / 6f));
            float rh = area.height / nz, h = k_Partition * 0.5f, xm = area.center.x;
            float blocked0 = p.stairZ0 - 0.5f, blocked1 = p.stairZ1 + 0.3f;
            for (int j = 0; j < nz; j++)
            {
                float z0 = area.yMin + j * rh, z1 = z0 + rh;
                var room = new Room { area = Rect.MinMaxRect(area.xMin, z0 + (j > 0 ? h : 0f), area.xMax, z1 - (j < nz - 1 ? h : 0f)) };
                InnerWall divider = null;
                if (j > 0)
                {
                    divider = new InnerWall(new Vector3(area.xMin, 0f, z0 - h), new Vector3(area.xMax, 0f, z0 - h), Vector3.forward);
                    p.walls.Add(divider);
                }
                float r0 = room.area.yMin, r1 = room.area.yMax, zm = (r0 + r1) * 0.5f;
                float door = float.NaN;
                if (!stairBeside || zm + 0.5f < blocked0 || zm - 0.5f > blocked1)
                    door = zm;
                else if (blocked0 - r0 >= 1.6f)
                    door = (r0 + blocked0) * 0.5f;
                else if (r1 - blocked1 >= 1.6f)
                    door = (blocked1 + r1) * 0.5f;
                if (!float.IsNaN(door))
                {
                    hallWall.AddDoor(door);
                    room.doors.Add(new Vector2(hallSide, door));
                }
                else if (divider != null)
                {
                    divider.AddDoor(xm);
                    room.doors.Add(new Vector2(0, xm));
                    p.rooms[p.rooms.Count - 1].doors.Add(new Vector2(1, xm));
                }
                p.rooms.Add(room);
            }
        }

        // 돌려주는 값: 층마다(0 = 1층) 바깥벽 넷(0 앞 · 1 뒤 · 2 왼 · 3 오른)의 구멍(창 · 문) — 쇼핑몰(BuildMall)과 같은 모양.
        // 바깥벽 입체 디테일(UrbanGreyboxBuilder.Details.cs)이 창턱 · 창틀 · 셔터 박스 자리를 맞춘다 (11/06)
        static List<Vector4>[][] BuildInterior(Transform root, GreyboxBuildingSpec spec, int enterable, int floors, float W, float D, Material wall, Material floor, Materials m)
        {
            var openings = new List<Vector4>[enterable][];
            InteriorPlan p = PlanInterior(W, D, enterable >= 2, spec.sideStairs);
            // 창 방향은 맵 기준(북·남·동·서)으로 정해 두었으니 이 건물이 돌아간 만큼 앞·뒤·왼·오른 벽으로 옮긴다
            for (int w = 0; w < 4; w++)
                p.open[w] = (spec.windowSides & WorldSide(root.rotation, w)) != 0;
            bool[] open = p.open;
            bool[] door = OuterDoors(spec, root.rotation);
            // 1층 가챠 가게 (UrbanGreyboxBuilder.Shop.cs): 큰 입구는 옆문 쪽 벽, 가운데는 동서 복도 자리(없으면 깊이 가운데)
            bool shop1F = spec.groundShop == GreyboxBuildingSpec.GroundShop.GachaShop;
            // 1층 고깃집 (사용자 11/09 도야지천국 N23): 남북 복도 한쪽(간판 쪽) 방들만 한 홀로 — 다른 쪽은 그대로 (Shop.cs)
            bool bbqLeft = spec.restaurantHallSide == GreyboxBuildingSpec.ShopSide.Left;
            bool bbq1F = spec.groundShop == GreyboxBuildingSpec.GroundShop.BbqRestaurant && (!bbqLeft || p.leftRooms);
            if (spec.groundShop == GreyboxBuildingSpec.GroundShop.BbqRestaurant && !bbq1F)
                Debug.LogWarning($"[C] {spec.name}: 고깃집 홀 쪽(왼쪽)에 방이 없다 — 좁은 건물이면 오른쪽으로", spec);
            // 냉동창고로 막을 1층 방 (사용자 11/09 도야지천국 N23 앞 오른쪽 — Shop.cs). 고깃집 홀로 합쳐지는 쪽 방이면 고르지 않는다
            Room coldRoom = ColdStorageRoom(p, spec.coldStorageRoom);
            if (coldRoom != null && bbq1F && OnBbqSide(p, coldRoom.area.xMin, coldRoom.area.xMax, bbqLeft))
            {
                Debug.LogWarning($"[C] {spec.name}: 냉동창고 방이 고깃집 홀 쪽이라 막지 않는다", spec);
                coldRoom = null;
            }
            // 1층 창고 (사용자 11/17 N06 — Shop.cs): 앞면 큰 문 둘 · 왼쪽 절반 막음 · 오른쪽 절반 창고. 계단이 가운데 벽에 걸리니 1층만 들어가는 건물만
            bool warehouse1F = spec.groundShop == GreyboxBuildingSpec.GroundShop.Warehouse;
            if (warehouse1F && enterable > 1)
            {
                Debug.LogWarning($"[C] {spec.name}: 1층 창고는 1층만 들어가는 건물만 — 보통 1층으로 만든다", spec);
                warehouse1F = false;
            }
            int shopSide = door[2] ? 2 : door[3] ? 3 : -1;
            float shopCenter = p.cross ? (p.cz0 + p.cz1) * 0.5f : D * 0.5f;
            if (shop1F && shopSide < 0)
                Debug.LogWarning($"[C] {spec.name}: 1층 가챠 가게는 옆문(doorSides 의 왼 · 오른쪽)이 있어야 큰 입구가 난다 — 앞문으로만 들어간다", spec);
            // 1층 바닥은 벽 밑까지 통째로 — 입구 문턱에 틈이 생기지 않게. 마감 두께만큼 인도보다 높다 —
            // 건물 밑까지 깔린 대지(인도 높이)와 윗면이 겹치면 대지 격자가 바닥 위로 비쳤다 (11/03)
            Box("Floor_1F", root, new Vector3(0f, 0f, 0f), new Vector3(W, k_Curb + k_FloorFinish, D), floor, true);
            int seed = StableHash(spec.name);

            // 옆문(왼·오른 벽): 동서 복도가 있으면 그 끝, 없으면 그쪽 맨 뒤 방 (왼쪽에 방이 없는 좁은 건물은 아래에서 복도 옆벽에)
            var sideDoorRoom = new Room[4];
            if (!p.cross)
            {
                for (int w = 2; w < 4; w++)
                {
                    if (!door[w])
                        continue;
                    foreach (Room r in p.rooms)
                    {
                        bool touches = w == 3 ? r.area.xMax >= W - k_Wall - 0.01f : r.area.xMin <= k_Wall + 0.01f;
                        if (touches && !r.stair && (sideDoorRoom[w] == null || r.area.yMax > sideDoorRoom[w].area.yMax))
                            sideDoorRoom[w] = r;
                    }
                    sideDoorRoom[w]?.doors.Add(new Vector2(w, sideDoorRoom[w].area.center.y));
                }
            }

            for (int f = 0; f < enterable; f++)
            {
                float y = k_Curb + f * k_FloorHeight;   // 이 층 바닥 윗면
                string tag = (f + 1) + "F";
                bool ground = f == 0;
                // 칸막이는 위층 바닥판(또는 지붕판) 밑까지 — 바로 위가 막힌 층(Mass)이면 층 높이 그대로
                float innerHeight = f < enterable - 1 || floors == enterable ? k_FloorHeight - k_Slab : k_FloorHeight;

                // 바깥벽 구멍. 복도 양 끝: 1층 앞은 늘 문, 뒤는 뒷문을 낸 건물이면 문. 나머지(2층 복도 끝·방 창)는 창 방향인 벽에만
                var front = new List<Vector4>();
                var back = new List<Vector4>();
                var left = new List<Vector4>();
                var right = new List<Vector4>();
                var side = new[] { front, back, left, right };
                bool shopFloor = ground && shop1F;
                bool billiardFloor = f == 1 && spec.secondFloorShop == GreyboxBuildingSpec.UpperShop.BilliardHall;   // 2층 당구장 (Shop.cs)
                bool bbqFloor = ground && bbq1F;
                bool warehouseFloor = ground && warehouse1F;
                if (warehouseFloor)
                    WarehouseDoors(front, W);   // 창고 층: 앞면 큰 문 둘뿐 — 다른 바깥벽은 막힌 벽
                else if (ground)
                    front.Add(new Vector4(p.lane0, p.lane1, 0f, k_MainDoorHeight));
                else if (open[0])
                    front.Add(new Vector4(p.lane0, p.lane1, 0.9f, 2.1f));
                if (ground && door[1] && !warehouseFloor)
                    back.Add(new Vector4(p.lane0, p.lane1, 0f, k_MainDoorHeight));
                else if (open[1] && !shopFloor && !warehouseFloor)
                    back.Add(new Vector4(p.lane0, p.lane1, 0.9f, 2.1f));
                if (shopFloor)
                {
                    // 가게 층: 창 없이 앞문(뒷문)과 옆문 쪽 큰 입구만. 반대쪽에도 옆문을 낸 건물이면 그쪽은 보통 문
                    if (shopSide >= 0)
                        side[shopSide].Add(new Vector4(shopCenter - k_ShopEntranceHalf - k_Wall, shopCenter + k_ShopEntranceHalf - k_Wall, 0f, k_ShopEntranceHeight));
                    int other = shopSide == 2 ? 3 : 2;
                    if (shopSide >= 0 && door[other])
                        side[other].Add(new Vector4(shopCenter - 0.7f - k_Wall, shopCenter + 0.7f - k_Wall, 0f, k_DoorHeight));
                }
                else if (p.cross && !warehouseFloor)
                {
                    // 동서 복도 양 끝: 옆문을 낸 쪽은 1층 문, 아니면 창 방향일 때 창
                    float c0 = p.cz0 + 0.3f - k_Wall, c1 = p.cz1 - 0.3f - k_Wall;
                    for (int w = 2; w < 4; w++)
                    {
                        if (ground && door[w])
                            side[w].Add(new Vector4(c0, c1, 0f, k_DoorHeight));
                        else if (open[w])
                            side[w].Add(new Vector4(c0, c1, 0.9f, 2.1f));
                    }
                }
                // 왼쪽에 방이 없는 좁은 건물은 복도가 왼쪽 바깥벽에 닿는다 — 계단 옆을 뺀 앞뒤 구간에 창(왼쪽 옆문이면 앞 구간 가운데에 문)
                if (!shopFloor && !warehouseFloor && !p.leftRooms && (open[2] || ground && door[2]))
                {
                    float s0 = p.stairs ? p.stairZ0 - 0.3f : D - k_Wall, s1 = p.stairs ? p.stairZ1 + 0.3f : D - k_Wall;
                    if (ground && door[2])
                    {
                        float c = (k_Wall + s0) * 0.5f - k_Wall;
                        left.Add(new Vector4(c - 0.7f, c + 0.7f, 0f, k_DoorHeight));
                    }
                    else
                    {
                        Windows(left, k_Wall, s0, k_Wall, 0.9f, 2.1f);
                    }
                    if (open[2])
                        Windows(left, s1, D - k_Wall, k_Wall, 0.9f, 2.1f);
                }
                foreach (Room r in shopFloor || warehouseFloor ? new List<Room>() : p.rooms)
                {
                    if (ground && r == coldRoom)
                        continue;   // 냉동창고는 창 없이 막힌 벽
                    bool shop = IsShop(r, ground, p);   // 1층 앞줄 길가 상가 → 크고 낮은 창
                    if (r.area.yMin <= k_Wall + 0.01f && open[0])
                        Windows(front, r.area.xMin, r.area.xMax, 0f, shop ? 0.5f : 0.9f, shop ? 2.4f : 2.1f);
                    if (r.area.yMax >= D - k_Wall - 0.01f && open[1])
                        Windows(back, r.area.xMin, r.area.xMax, 0f, 0.9f, 2.1f);
                    for (int w = 2; w < 4; w++)
                    {
                        bool touches = w == 3 ? r.area.xMax >= W - k_Wall - 0.01f : r.area.xMin <= k_Wall + 0.01f;
                        if (!touches)
                            continue;
                        if (ground && r == sideDoorRoom[w])
                        {
                            float c = r.area.center.y - k_Wall;
                            side[w].Add(new Vector4(c - 0.7f, c + 0.7f, 0f, k_DoorHeight));
                        }
                        else if (open[w])
                        {
                            Windows(side[w], r.area.yMin, r.area.yMax, k_Wall, 0.9f, 2.1f);
                        }
                    }
                }
                openings[f] = new[] { new List<Vector4>(front), new List<Vector4>(back), new List<Vector4>(left), new List<Vector4>(right) };
                Wall("Front_" + tag, root, new Vector3(0f, y, 0f), new Vector3(W, y, 0f), Vector3.forward, front, wall);
                Wall("Back_" + tag, root, new Vector3(0f, y, D), new Vector3(W, y, D), Vector3.back, back, wall);
                // 옆벽은 앞·뒤 벽 사이에만 — 모서리가 겹치면 같은 면이 두 번 그려져 깜빡인다
                Wall("Left_" + tag, root, new Vector3(0f, y, k_Wall), new Vector3(0f, y, D - k_Wall), Vector3.right, left, wall);
                Wall("Right_" + tag, root, new Vector3(W, y, k_Wall), new Vector3(W, y, D - k_Wall), Vector3.left, right, wall);

                // 실내 벽 — 복도 벽·방 칸막이 (가게 층은 계단 양옆 벽만, 당구장 층은 사용자 그림의 계단 둘레 벽만)
                if (shopFloor)
                {
                    if (p.stairs)
                        ShopStairWalls(root, p, y, innerHeight, wall);
                }
                else if (billiardFloor)
                {
                    BilliardStairWalls(root, p, y, innerHeight, wall);
                }
                else if (warehouseFloor)
                {
                    WarehousePartition(root, p, y, innerHeight, wall);
                }
                else
                {
                    foreach (InnerWall w in p.walls)
                    {
                        if (bbqFloor && OnBbqSide(p, w.a.x, w.b.x, bbqLeft))
                            continue;   // 고깃집 홀 쪽 방 칸막이 · 동서 복도 벽은 없앤다 (남북 복도 벽과 문은 그대로)
                        Wall("Partition_" + tag, root, w.a + Vector3.up * y, w.b + Vector3.up * y, w.inward, w.holes, wall, k_Partition, innerHeight);
                    }
                }

                if (f >= 1)
                    SlabWithHoles("Floor_" + tag, root, new Rect(k_Wall, k_Wall, W - 2f * k_Wall, D - 2f * k_Wall), p.Holes, y, floor, m.interior);

                if (shopFloor)
                {
                    FurnishGachaShop(root, p, y, innerHeight, shopSide, shopCenter, m.furniture);
                }
                else if (billiardFloor)
                {
                    FurnishBilliardHall(root, p, y, innerHeight, side, m);
                }
                else if (warehouseFloor)
                {
                    FurnishWarehouse(root, p, y, innerHeight, m);
                }
                else
                {
                    for (int i = 0; i < p.rooms.Count; i++)
                    {
                        Rect a = p.rooms[i].area;
                        if (ground && p.rooms[i] == coldRoom)
                            continue;   // 냉동창고 — 가구 없음, 문은 철문으로 막는다
                        if (!p.rooms[i].stair && !(bbqFloor && OnBbqSide(p, a.xMin, a.xMax, bbqLeft)))
                            Furnish(root, p.rooms[i], y, IsShop(p.rooms[i], ground, p), p, seed + i * 7919 + f * 104729, m.furniture, tag);
                    }
                    if (bbqFloor)
                        FurnishBbqHall(root, p, y, innerHeight, bbqLeft, m);
                    if (ground && coldRoom != null)
                        ColdStorageDoors(root, p, coldRoom, y, m);
                }
            }

            if (enterable >= 2)
            {
                // 계단: 남북 복도 왼쪽 벽에 붙어 뒷벽 쪽으로 오른다 (보이는 단 20개 + 보이지 않는 경사로).
                // 계단방(양 끝)도 같은 모양 — 바깥벽 쪽에 붙고 난간은 방 안쪽 옆
                float y1 = k_Curb, y2 = k_Curb + k_FloorHeight;
                foreach (Rect hole in p.Holes)
                {
                    bool openLeft = hole.x > W * 0.5f && hole.xMax >= W - k_Wall - 0.01f;   // 오른쪽 끝 계단방 — 구멍 왼쪽이 방 안
                    float sx0 = openLeft ? hole.xMax - 1.3f : hole.x + 0.1f, sx1 = sx0 + 1.2f;
                    Steps("Stairs_1F", root, new Vector3(sx0, y1, hole.y), new Vector3(sx1, y2, hole.yMax), true, floor);
                    Ramp("StairRamp_1F", root, new Vector3((sx0 + sx1) * 0.5f, y1, hole.y), new Vector3((sx0 + sx1) * 0.5f, y2, hole.yMax), 1.2f);
                    // 2층 난간: 구멍의 방(복도) 쪽 옆과 앞 끝 — 꼭대기 쪽은 내리는 자리라 비운다
                    float rx = openLeft ? hole.x : hole.xMax;
                    Box("Railing_2F_Side", root, new Vector3(rx - 0.05f, y2, hole.y), new Vector3(rx + 0.05f, y2 + 1f, hole.yMax), m.dark, true);
                    // 2층 당구장은 가운데 계단 앞 끝에 가로 벽이 서서(Shop.cs BilliardStairWalls) 앞 끝 난간이 필요 없다
                    if (spec.secondFloorShop != GreyboxBuildingSpec.UpperShop.BilliardHall || hole != p.Hole)
                        Box("Railing_2F_End", root, new Vector3(hole.x, y2, hole.y - 0.05f), new Vector3(hole.xMax, y2 + 1f, hole.y + 0.05f), m.dark, true);
                }
            }
            return openings;
        }

        // 1층 바깥 문을 낼 로컬 벽(0 앞 1 뒤 2 왼 3 오른) — 앞은 늘. 나머지는 출입구 방향(맵 기준)에서, 예전 설정(통과 건물 = 뒷문, 옆문 = 오른쪽)도 그대로
        static bool[] OuterDoors(GreyboxBuildingSpec spec, Quaternion rotation)
        {
            var door = new bool[4];
            door[0] = true;
            for (int w = 1; w < 4; w++)
                door[w] = (spec.doorSides & WorldSide(rotation, w)) != 0;
            door[1] |= spec.throughPassage;
            door[3] |= spec.sideDoor;
            return door;
        }

        // 길가 상가 = 1층 앞줄 방, 앞면이 창 방향일 때만 (앞이 막힌 건물의 앞줄 방은 보통 방)
        static bool IsShop(Room r, bool ground, InteriorPlan p)
        {
            return ground && p.open[0] && r.area.yMin <= k_Wall + 0.01f;
        }

        // 건물 로컬 벽(0 앞 -z, 1 뒤 +z, 2 왼 -x, 3 오른 +x)이 맵에서 바라보는 방향
        static GreyboxBuildingSpec.Sides WorldSide(Quaternion rotation, int wall)
        {
            Vector3 n = wall == 0 ? Vector3.back : wall == 1 ? Vector3.forward : wall == 2 ? Vector3.left : Vector3.right;
            Vector3 d = rotation * n;
            if (Mathf.Abs(d.x) > Mathf.Abs(d.z))
                return d.x > 0f ? GreyboxBuildingSpec.Sides.East : GreyboxBuildingSpec.Sides.West;
            return d.z > 0f ? GreyboxBuildingSpec.Sides.North : GreyboxBuildingSpec.Sides.South;
        }

        // 방 한쪽 바깥벽에 창 — 6.5m 넘으면 둘. offset: 벽 시작점까지 거리(옆벽은 k_Wall 부터 잰다)
        static void Windows(List<Vector4> holes, float s0, float s1, float offset, float v0, float v1)
        {
            float len = s1 - s0;
            int count = len > 6.5f ? 2 : 1;
            float w = Mathf.Min(2.4f, len / count - 1.2f);
            if (w < 0.8f)
                return;
            for (int k = 0; k < count; k++)
            {
                float c = s0 + (k + 0.5f) * len / count - offset;
                holes.Add(new Vector4(c - w * 0.5f, c + w * 0.5f, v0, v1));
            }
        }

        // 방 가구 = 엄폐물. 문 앞 1.4m 는 비우고, 창 없는 벽엔 높은 선반(1.8m — 서서도 가림), 창 밑엔 낮은 장(0.85m),
        // 가운데는 책상(큰 방은 둘, 또는 하나는 시야를 끊는 칸막이), 1층 길가 상가는 카운터
        static void Furnish(Transform root, Room r, float y, bool shop, InteriorPlan p, int seed, Material mat, string tag)
        {
            Rect a = r.area;
            if (a.width < 2f || a.height < 2f)
                return;
            var rng = new System.Random(seed);
            // 창이 난 벽 — 창 방향이 아닌 바깥벽은 막혀 있어서 실내 벽처럼 높은 선반을 붙일 수 있다
            bool[] window =
            {
                a.yMin <= k_Wall + 0.01f && p.open[0], a.yMax >= p.D - k_Wall - 0.01f && p.open[1],
                a.xMin <= k_Wall + 0.01f && p.open[2], a.xMax >= p.W - k_Wall - 0.01f && p.open[3],
            };
            var blocked = new List<Rect>();
            foreach (Vector2 d in r.doors)
                blocked.Add(DoorClearance(a, (int)d.x, d.y));

            // 선반·장: 문 없는 벽 중 창 없는 벽을 먼저, 그다음 긴 벽
            int best = -1;
            float bestLen = 0f;
            bool bestTall = false;
            for (int w = 0; w < 4; w++)
            {
                if (r.HasDoor(w) || (shop && w == 0))   // 길가 상가 앞 큰 창 앞은 비운다
                    continue;
                float len = w < 2 ? a.width : a.height;
                bool tall = !window[w];
                if (best < 0 || (tall && !bestTall) || (tall == bestTall && len > bestLen))
                {
                    best = w;
                    bestLen = len;
                    bestTall = tall;
                }
            }
            if (best >= 0 && bestLen >= 2.4f)
            {
                Rect s = AlongWall(a, best, bestLen - 1.4f, bestTall ? 0.4f : 0.5f);
                if (!Overlaps(s, blocked))
                {
                    Piece(root, s, y, bestTall ? 1.8f : 0.85f, mat, tag, bestTall ? "TallShelf" : "LowCabinet", WallInward(best));
                    blocked.Add(Expand(s, 0.6f));
                }
            }

            if (shop)
            {
                // 카운터 2.0×0.6×1.05 — 앞 창과 복도 문 사이, 한쪽으로 붙여 지나갈 길을 남긴다
                float cz = a.yMin + a.height * 0.55f;
                bool startLeft = rng.Next(2) == 0;
                for (int t = 0; t < 2; t++)
                {
                    bool leftSide = startLeft ^ (t == 1);
                    float x0 = leftSide ? a.xMin + 0.9f : a.xMax - 0.9f - 2f;
                    var c = new Rect(x0, cz - 0.3f, 2f, 0.6f);
                    if (Inside(c, a, 0.3f) && !Overlaps(c, blocked))
                    {
                        Piece(root, c, y, 1.05f, mat, tag, "ShopCounter", Vector3.back);   // 손님 쪽 앞판이 길(앞 창) 쪽
                        blocked.Add(Expand(c, 0.6f));
                        break;
                    }
                }
                return;
            }

            // 책상 1.4×0.7×0.75 — 방이 크면 둘(반쯤은 하나 대신 시야를 끊는 칸막이 1.6×1.5m)
            float areaM2 = a.width * a.height;
            int pieces = areaM2 >= 30f ? 2 : (Mathf.Min(a.width, a.height) >= 3f ? 1 : 0);
            bool alongX = a.width >= a.height;
            for (int k = 0; k < pieces; k++)
            {
                float t = pieces == 1 ? 0.5f : (k == 0 ? 0.33f : 0.67f);
                Vector2 c = alongX ? new Vector2(Mathf.Lerp(a.xMin, a.xMax, t), a.center.y) : new Vector2(a.center.x, Mathf.Lerp(a.yMin, a.yMax, t));
                bool screen = k == 1 && rng.Next(2) == 0;
                Vector2 size = screen ? (alongX ? new Vector2(0.08f, 1.6f) : new Vector2(1.6f, 0.08f))
                                      : (alongX ? new Vector2(1.4f, 0.7f) : new Vector2(0.7f, 1.4f));
                for (int tries = 0; tries < 3; tries++)
                {
                    float shift = tries == 0 ? 0f : (tries == 1 ? 0.8f : -0.8f);
                    Vector2 cc = c + (alongX ? new Vector2(shift, 0f) : new Vector2(0f, shift));
                    var d = new Rect(cc.x - size.x * 0.5f, cc.y - size.y * 0.5f, size.x, size.y);
                    if (Inside(d, a, 0.3f) && !Overlaps(d, blocked))
                    {
                        Piece(root, d, y, screen ? 1.5f : 0.75f, mat, tag, screen ? null : "OfficeDesk");
                        blocked.Add(Expand(d, 0.6f));
                        break;
                    }
                }
            }
        }

        // 문 앞 비울 자리 — 문 가운데 ±0.8m, 벽에서 1.4m 안쪽까지
        static Rect DoorClearance(Rect a, int wall, float center)
        {
            switch (wall)
            {
                case 0: return Rect.MinMaxRect(center - 0.8f, a.yMin, center + 0.8f, a.yMin + 1.4f);
                case 1: return Rect.MinMaxRect(center - 0.8f, a.yMax - 1.4f, center + 0.8f, a.yMax);
                case 2: return Rect.MinMaxRect(a.xMin, center - 0.8f, a.xMin + 1.4f, center + 0.8f);
                default: return Rect.MinMaxRect(a.xMax - 1.4f, center - 0.8f, a.xMax, center + 0.8f);
            }
        }

        // 벽에 붙인 길쭉한 가구 자리 (벽 가운데 기준)
        static Rect AlongWall(Rect a, int wall, float length, float depth)
        {
            switch (wall)
            {
                case 0: return new Rect(a.center.x - length * 0.5f, a.yMin + 0.02f, length, depth);
                case 1: return new Rect(a.center.x - length * 0.5f, a.yMax - 0.02f - depth, length, depth);
                case 2: return new Rect(a.xMin + 0.02f, a.center.y - length * 0.5f, depth, length);
                default: return new Rect(a.xMax - 0.02f - depth, a.center.y - length * 0.5f, depth, length);
            }
        }

        static bool Overlaps(Rect r, List<Rect> others)
        {
            foreach (Rect o in others)
            {
                if (r.Overlaps(o))
                    return true;
            }
            return false;
        }

        static bool Inside(Rect r, Rect area, float margin)
        {
            return r.xMin >= area.xMin + margin && r.xMax <= area.xMax - margin && r.yMin >= area.yMin + margin && r.yMax <= area.yMax - margin;
        }

        static Rect Expand(Rect r, float m)
        {
            return Rect.MinMaxRect(r.xMin - m, r.yMin - m, r.xMax + m, r.yMax + m);
        }

        // kind: 씌울 겉모양 모델 종류 (UrbanGreyboxBuilder.Props.cs — 칸막이는 없음), front: 모델 앞면이 볼 쪽
        static void Piece(Transform root, Rect r, float y, float height, Material mat, string tag, string kind = null, Vector3 front = default)
        {
            GameObject go = Box("Furniture_" + tag, root, new Vector3(r.xMin, y, r.yMin), new Vector3(r.xMax, y + height, r.yMax), mat, true);
            if (kind != null)
                Skin(go, kind, front);
        }

        // 방 벽 번호(0 앞 -z · 1 뒤 +z · 2 왼 -x · 3 오른 +x — AlongWall 과 같다)에서 방 안쪽을 보는 쪽 = 벽에 붙인 가구의 앞
        static Vector3 WallInward(int wall)
        {
            return wall == 0 ? Vector3.forward : wall == 1 ? Vector3.back : wall == 2 ? Vector3.right : Vector3.left;
        }

        // 이름으로 정하는 씨앗 — 다시 만들어도 같은 가구 배치가 나오게 (string.GetHashCode 는 실행마다 달라질 수 있다)
        static int StableHash(string s)
        {
            unchecked
            {
                int h = 17;
                foreach (char c in s)
                    h = h * 31 + c;
                return h;
            }
        }
    }
}
