using UnityEngine;

namespace MilTrainSimVR.UrbanCombat
{
    // 회색 박스 맵 '배치'의 건물 한 동. 이 상자의 위치·가로·세로·회전이 건물 바닥이고, 아래 값이 층수다.
    // 맵 생성기(메뉴 MilTrainSimVR → C → 맵 만들기)가 이걸 읽어 벽·창·계단이 있는 건물을 만든다.
    // 상자의 파란 띠(로컬 -Z 쪽 면)가 건물 앞면 = 입구·창이 나는 거리 쪽이다.
    // 배치 상자는 EditorOnly 태그 아래에 있어서 게임 빌드에는 들어가지 않는다.
    public class GreyboxBuildingSpec : MonoBehaviour
    {
        // 맵 기준 방향 (북 = 호수 쪽 +z, 동 = 대로 쪽 +x) — 건물이 어느 쪽을 보든 같은 뜻
        [System.Flags]
        public enum Sides { None = 0, North = 1, South = 2, East = 4, West = 8, All = 15 }

        // 실내 평면 — 복도형(방·복도, 2층까지) 또는 빈 쇼핑몰(사용자 10/29 N05 배치 그림, 3층까지 같은 구조)
        public enum Layout { Corridor = 0, Mall = 1 }

        [Tooltip("전체 층수 — 겉모양 높이 (층 높이 3.4m)")]
        [Min(1)] public int floors = 4;

        [Tooltip("들어갈 수 있는 층수 — 0 이면 겉모양만, 1 이면 1층만, 2 면 계단으로 2층까지, 3 은 쇼핑몰만. 앞면 폭 7.7m·깊이 5m 이상(2층은 깊이 7.4m 이상)이어야 한다")]
        [Range(0, 3)] public int enterable;

        [Tooltip("실내 평면 — 복도형(방·복도) / 쇼핑몰(모서리 계단실·빈 가게·가판대·가운데 에스컬레이터, 층마다 같은 구조)")]
        public Layout layout = Layout.Corridor;

        [Tooltip("들어갈 수 있는 층의 창을 내는 방향(맵 기준) — 길·샛길 쪽만 고른다(맵 끝 큰길 쪽은 빼기). 나머지 바깥벽은 막힌다. 문은 앞면(파란 띠)")]
        public Sides windowSides = Sides.All;

        [Tooltip("앞문 말고도 1층 출입구를 낼 방향(맵 기준) — 뒷벽이면 앞문과 마주 보는 문, 옆벽이면 동서 복도 끝(없으면 끝 방)에 문")]
        public Sides doorSides = Sides.None;

        [Tooltip("1층 오른쪽 옆벽에 문 (골목 쪽)")]
        public bool sideDoor;

        [Tooltip("뒷벽에도 앞문과 마주 보는 문(2층은 창) — 건물을 앞뒤로 지나다닐 수 있다. 들어갈 수 있는 건물일 때만")]
        public bool throughPassage;

        [Tooltip("가운데 계단 말고 양 끝 뒤쪽 방에도 1→2층 계단 — 그 방은 1·2층 모두 계단방이 된다. 동서 복도가 있는 2층 건물만")]
        public bool sideStairs;

        [Tooltip("외벽 색 0~2 — 옆 건물과 구분")]
        [Range(0, 2)] public int tint;

        [Tooltip("실내 바닥 재질 1~5 (Textures/Map/Interior1~5) — 0 이면 실내가 있는 건물끼리 배치 순서대로 1 · 2 · 4 · 5 를 돌려 쓴다 " +
                 "(3 오닉스는 여기서 3 으로 정한 건물만 — N05). 한 건물은 모든 층 · 계단이 같은 바닥")]
        [Range(0, 5)] public int floorMaterial;

        public enum Facade { Plain = 0, Ceramic = 1 }

        [Tooltip("바깥벽 겉모양 — Plain: tint 색 벽. Ceramic: 청운백화점(N05)식 — 1층 진회색 화강석 · 위층 아이보리 세라믹 블록 · 청록 처마·띠 · " +
                 "창 사이 세로 기둥 띠 · 꼭대기 층 금색 해돋이 장식판과 백화점 이름 (사용자 11/06). 쇼핑몰(layout Mall)만")]
        public Facade facade = Facade.Plain;

        // 바깥벽 마감 (사용자 11/06 — 목업 두 번 확인 후 적용). Grid 는 예전 회색 1 m 격자(tint 색)
        public enum Finish
        {
            Grid = 0, TileBrown = 1, TileBeige = 2, TileGrey = 3, Brick = 4, GraniteLight = 5, PanelSilver = 6, PanelWhite = 7, PanelDark = 8,
            Concrete = 9, StuccoIvory = 10, StuccoGrey = 11, StuccoApricot = 12, StuccoSage = 13,
        }

        [Tooltip("바깥벽 마감 (facade 가 Plain 일 때) — Grid: 예전 1 m 격자(tint 색). 나머지: 외장 타일(갈색 · 베이지 · 회색) · 붉은 벽돌 · 밝은 화강석 · " +
                 "알루미늄 패널(은 · 흰 · 진회) · 노출 콘크리트 · 미장(아이보리 · 웜그레이 · 살구 · 세이지). 마감을 고르면 실내 벽은 흰 미장 · 옥상은 콘크리트가 되고, " +
                 "바깥벽에 창 아래 빗물 줄 · 1층 아래 때 · 옥상 밑 줄이 붙는다. 같은 마감도 건물마다 밝기 · 얼룩이 조금씩 다르다 (사용자 11/06)")]
        public Finish finish = Finish.Grid;

        [Tooltip("1층 바깥벽을 진회색 화강석으로 — 1층 가게 건물. finish 가 Grid 가 아니고 들어갈 수 있는 건물일 때만")]
        public bool stoneBase;

        [Tooltip("백화점 실내 — 쇼핑몰(layout Mall) 구조는 그대로 두고 청운백화점(N05)식 마감: 벽 검정 대리석 걸레받이 · 호두나무 판벽 · 황동 띠 · 크림 대리석, " +
                 "크림 천장 · 원목 들보 · 매입등 · 꼭대기 천창, 원목 · 황동 난간, 에스컬레이터 대신 통유리 경사형 무빙워크, 따뜻한 실내등 · 층마다 반사 기준점 " +
                 "(사용자 11/06). 부딪힘 · 길찾기는 쇼핑몰과 같다")]
        public bool departmentStore;

        [Tooltip("쇼핑몰 1층 앞면 가운데를 큰 정문으로 — 가로 통로 끝 출입구 둘과 그 사이 창을 한 구멍(높이 2.8m)으로 트고, " +
                 "그 뒤 앞쪽 가운데 줄 가게를 비워 에스컬레이터까지 트인 로비로 (사용자 11/16 청운백화점 N05). 앞면이 긴 벽인 쇼핑몰만")]
        public bool grandEntrance;

        // 1층 가게 (사용자 11/09 — 뫄뫄굿즈 N24 는 1층 전체를 한 칸, 도야지천국 N23 은 복도 한쪽만 홀)
        public enum GroundShop { None = 0, GachaShop = 1, BbqRestaurant = 2, Warehouse = 3 }
        public enum ShopSide { Left = 0, Right = 1 }

        [Tooltip("1층 전체를 가게 한 칸으로 (사용자 11/09 뫄뫄굿즈 N24). GachaShop: 1층 칸막이 없음 · 2층 가는 계단은 양옆 벽 사이(계단 아래 끝으로만 들어감) · " +
                 "옆문 쪽 벽에 큰 입구(폭 4.4 m · 간판 밑) · 1층 창 없음(앞문은 그대로) · 벽 따라 인형뽑기 · 가운데 등 맞댄 가챠 섬 · 입구 옆과 맞은편 벽에 지폐 교환기 · " +
                 "가게 조명. 복도형 건물만, 큰 입구는 doorSides 의 옆문 쪽에 난다. " +
                 "BbqRestaurant(사용자 11/09 도야지천국 N23): 남북 복도 한쪽(restaurantHallSide) 방들을 칸막이 없이 한 홀로 — 드럼통 불판 탁자 · 탁자마다 플라스틱 의자 넷(탁자마다 흰 · 초록 · 짙은 파랑 돌려 씀) · " +
                 "조명 · 반사 기준점. 다른 쪽 방 · 복도 벽과 문 · 바깥벽은 그대로. " +
                 "Warehouse(사용자 11/17 N06): 앞면에 창 · 문 대신 큰 창고 문 둘 — 앞에서 볼 때 왼쪽은 셔터를 내려 막고 그 뒤 절반은 가운데 벽으로 막은 빈 칸, " +
                 "오른쪽은 셔터를 말아 올린 창고(높은 선반 줄 · 팔레트 상자 · 바닥 노란 줄 · 조명) · 문 앞 볼라드. 다른 바깥벽엔 창 · 문 없음. 1층만 들어가는 복도형 건물만")]
        public GroundShop groundShop = GroundShop.None;

        [Tooltip("BbqRestaurant 의 홀 쪽 — 건물 앞(파란 띠)을 바라볼 때 남북 복도의 왼쪽 / 오른쪽 방들. 간판이 달린 쪽을 고른다 (사용자 11/09: 간판 쪽 구역은 합치고 다른 쪽은 그대로)")]
        public ShopSide restaurantHallSide = ShopSide.Left;

        // 1층 모서리 방 — 건물 앞(파란 띠)을 바라볼 때
        public enum RoomCorner { None = 0, FrontLeft = 1, FrontRight = 2, BackLeft = 3, BackRight = 4 }

        [Tooltip("1층 방 하나를 냉동창고로 막는다 (사용자 11/09 도야지천국 N23 앞 오른쪽) — 그 방으로 나는 문마다 철문(안내판 '냉동창고 · 관계자 외 출입금지')을 달아 " +
                 "들어갈 수 없게 하고, 그 방 창은 없앤다(바깥벽은 막힌 벽). 안에는 가구를 두지 않는다. 고깃집 홀 쪽 방은 고르지 않는다")]
        public RoomCorner coldStorageRoom = RoomCorner.None;

        // 2층 전체를 가게 한 칸으로 (사용자 11/09 — 제일당구를 N24 에서 N09 2층으로)
        public enum UpperShop { None = 0, BilliardHall = 1 }

        [Tooltip("2층 전체를 가게 한 칸으로 (사용자 11/09 제일당구 N09). BilliardHall: 사용자 그림대로 — 2층 칸막이는 계단 둘레 벽만(계단 왼쪽은 뒷벽까지 · " +
                 "계단 앞 끝 가로 벽 · 복도 오른쪽 벽은 계단 꼭대기까지, 바깥벽 · 창은 그대로) · 당구대 5(왼쪽 벽 쪽 세로 둘 · 앞쪽 가로 둘 · 오른쪽 가운데 가로 하나) · " +
                 "당구대마다 큐 장 · 오른쪽 벽 쪽 뒤 카운터 · 당구대 등. 복도형 2층 건물만")]
        public UpperShop secondFloorShop = UpperShop.None;

        [Tooltip("들어갈 수 없는 건물(enterable 0)의 1층 앞 · 뒤 면(길 쪽)을 내린 셔터로 덮는다 — 옆 두 면(골목 쪽)은 창 없는 벽 — 1층 어두운 유리판 대신 칸마다 골진 셔터 · 셔터 통 · 가이드 레일 (사용자 11/17 N11: " +
                 "'빈 방으로 만들고 셔터 달아서 다 닫아버리자'). 보이기만 한다 — 부딪힘은 건물 덩어리")]
        public bool closedShutters;

        [Tooltip("메모 — 실제 건물 이름 등 (맵에는 안 나온다)")]
        public string note;

#if UNITY_EDITOR
        // 씬 뷰에서 위에서 내려다볼 때 이름·층수와 앞면(파란 띠)이 보이게
        void OnDrawGizmos()
        {
            string extra = (layout == Layout.Mall ? departmentStore ? " · 백화점" : " · 쇼핑몰" : "") + (grandEntrance ? " · 큰 정문" : "") + (throughPassage ? " · 통과" : "") + (sideStairs ? " · 계단 셋" : "")
                + (floorMaterial > 0 ? $" · 바닥 {floorMaterial}" : "");
            string doors = doorSides != Sides.None ? $" · 문 더 {SidesLabel(doorSides)}" : "";
            string label = enterable > 0 ? $"{name}\n{floors}층 · 들어감 {enterable}{extra}\n창 {SidesLabel(windowSides)}{doors}" : $"{name}\n{floors}층";
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, label);
            Gizmos.color = new Color(0.15f, 0.45f, 1f, 0.9f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(new Vector3(0f, 0f, -0.5f), new Vector3(1f, 1.05f, 0.04f));
        }

        static string SidesLabel(Sides sides)
        {
            if (sides == Sides.None)
                return "없음";
            if (sides == Sides.All)
                return "사방";
            var parts = new System.Collections.Generic.List<string>();
            if ((sides & Sides.North) != 0) parts.Add("북");
            if ((sides & Sides.South) != 0) parts.Add("남");
            if ((sides & Sides.East) != 0) parts.Add("동");
            if ((sides & Sides.West) != 0) parts.Add("서");
            return string.Join("·", parts);
        }

        // 들어갈 수 있는 건물은 주황, 겉모양만인 건물은 회청색 — 값을 바꾸면 바로 색이 따라온다
        void OnValidate()
        {
            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
                return;
            string path = enterable > 0 ? "Assets/C/Materials/Greybox/Layout/Layout_Enterable.mat" : "Assets/C/Materials/Greybox/Layout/Layout_Building.mat";
            var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null && meshRenderer.sharedMaterial != mat)
                meshRenderer.sharedMaterial = mat;
        }
#endif
    }
}
