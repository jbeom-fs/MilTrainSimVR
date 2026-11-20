using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MilTrainSimVR.UrbanCombat.EditorTools
{
    // 회색 박스 시가지 생성기.
    // 씬의 배치(C_Layout — 건물 상자 GreyboxBuildingSpec · 바닥 상자 GreyboxGroundSpec)를 읽어
    // "C_Greybox" 아래를 지우고 바닥·건물을 다시 만든다. 리그·총·표적은 건드리지 않는다.
    // 배치는 '배치 편집 모드'(UrbanLayoutTools)에서 지도(또는 위성 사진)를 깔고 고친다.
    // 상자 메시는 생성기가 직접 만들고(ProBuilder 안 씀), 다 만든 뒤 건물마다 재질별 메시 하나로 합친다 — 씬 크기 때문.
    // 부딪힘(콜라이더)은 조각마다 GameObject 를 두지 않고 건물(묶음)마다 한 오브젝트에 상자 콜라이더 여러 개로 모은다.
    // 다시 만들면 통째로 바뀌니 고칠 건 배치에서 고친다.
    // 겉면은 1m 격자 — VR 에서 거리·높이를 눈으로 가늠하려고 월드 기준 UV 로 붙인다.
    // 들어갈 수 있는 건물의 실내(복도·방·계단·가구)는 UrbanGreyboxBuilder.Interior.cs, 지하(홀·통로·입구)는 UrbanGreyboxBuilder.Underground.cs
    public static partial class UrbanGreyboxBuilder
    {
        const string k_RootName = "C_Greybox";
        const string k_MaterialFolder = "Assets/C/Materials/Greybox";
        const string k_GridTexture = "Assets/C/Textures/C_Grid.png";
        const string k_FloorTextureFolder = "Assets/C/Textures/Map";   // 실내 바닥 텍스처 — Interior1~5 폴더

        const float k_Curb = 0.15f;        // 인도 턱 = 건물 1층 바닥 높이
        const float k_FloorHeight = 3.4f;  // 층 높이 (바닥 윗면 ~ 다음 바닥 윗면)
        const float k_Wall = 0.2f;         // 벽 두께
        const float k_Slab = 0.2f;         // 바닥판 두께
        const float k_FloorFinish = 0.02f; // 바닥판 위 마감(나무 · 돌 · 타일) — 그 아래(아래층 천장)는 GB_Interior (흰 미장)
        const float k_Parapet = 0.6f;      // 옥상 난간
        // 간판 띠(회색 GB_Sign 띠 — 1층 인도 위 2.45~3.45 m, 2층 2층 바닥 위 2.3~3.3 m)는 11/06 에 없앴다 (사용자: 띠 없이 간판을 건물에 바로).
        // 가게 간판(씬 루트 C_Signs)은 같은 높이에 건물 벽면에 바로 붙어 있다
        // 들어갈 수 있는 층 — 3층까지는 돌아다닐 곳이 너무 많다 (사용자 10/23). 복도형 실내는 1→2층 계단까지만 만든다.
        // 쇼핑몰(사용자 10/29 N05)만 3층까지 — UrbanGreyboxBuilder.Mall.cs
        const int k_MaxEnterable = 2;
        const int k_MaxMallFloors = 3;

        class Materials
        {
            // interior = 실내 천장(바닥판 밑면). 건물 실내 바닥은 floors 에서 건물마다 하나 (11/03). 지하 바닥은 ugFloor (11/05 — 그 전엔 interior)
            // windows = 막힌 위층의 창 띠 (11/03 — 그 전엔 glass 단색). glass 는 1층 유리 상가 등 나머지 유리
            public Material ground, sidewalk, alley, lot, interior, glass, sign, dark, furniture, underground, barricade, windows, ugFloor, road, asphalt;
            public Material[] walls, floors;
            // 세라믹 외벽 (UrbanGreyboxBuilder.Facade.cs, 11/06): 블록 · 1층 화강석 · 아이보리 장식 · 청록 장식 · 짙은 청록 판 · 금 · 청동
            public Material facadeCeramic, facadeGranite, facadeIvory, facadeTeal, facadePanel, facadeGold, facadeBronze;
            // 백화점 실내 (UrbanGreyboxBuilder.Dept.cs, 11/06): 벽 한 장 · 천장 · 호두나무 · 검정 · 흰 대리석 · 무빙워크 판 · 유리 · 스테인리스 ·
            // 노란 안전 줄 · 검은 손잡이 · 매입등 · 천창 (황동은 facadeGold)
            public Material deptWall, deptCeiling, deptWood, deptBlack, deptWhite, deptPallet, deptGlass, deptStainless, deptYellow, deptRubber,
                deptLight, deptLaylight;
            // 일반 건물 바깥벽 마감 (UrbanGreyboxBuilder.Exterior.cs, 11/06): 마감마다 3벌(건물마다 밝기 · 색 · 큰 얼룩 자리를 조금씩) · 실내 미장 · 옥상 · 때 자국
            public Dictionary<GreyboxBuildingSpec.Finish, Material[]> finishes;
            public Material extInterior, extRoof, grime;
            // 바깥벽 입체 디테일 (UrbanGreyboxBuilder.Details.cs, 11/06): 인조석(띠 · 덮개 · 창턱 · 차양) · 덮개 콘크리트(파라펫 옥상 쪽) · 화강석 줄눈 ·
            // 알루미늄 덮개 · 어두운 알루미늄 띠 · 창틀 · 셔터 · 옥탑 철문 · 환기창 · 철제 난간 3색(녹색 · 회색 · 흰색)
            public Material extStone, extConcrete, extGroove, extAlu, extAluDark, extFrame, extShutter, extDoor, extVent;
            public Material[] extRails;
            // 설비 (UrbanGreyboxBuilder.Utilities.cs, 11/06): 실외기 케이스 · 열교환기 그릴 · 팬 보호망 · 아연도금 철 · 이음 줄 · 검은 보온재 · 배관 테이프 ·
            // 배수관(회색 · 흰 PVC · 어두운 금속) · 노란 가스관 · 계량기함 · 고정쇠 · FRP 물탱크(파란 · 흰) · 판 조립 물탱크 · 콘크리트 받침 · 안테나 판 · 안테나 막대
            public Material utilCasing, utilGrille, utilFan, utilSteel, utilSeam, utilInsulation, utilTape, utilPvcGrey, utilPvcWhite, utilMetalDark, utilGas,
                utilMeter, utilBracket, utilFrpBlue, utilFrpWhite, utilPanelTank, utilBeam, utilAntenna, utilRod;
        }

        const float k_WindowTileWidth = 9.6f;   // 창 띠 텍스처 한 장의 가로 (8칸 × 1.2 m)

        // 실내 바닥 재질 (GB_Floor_1~5) — 텍스처 한 장이 덮는 크기(m) · 매끄러움 (텍스처에 매끄러움이 있으면 그것) · 곱하는 색. 텍스처를 보고 어림했다 (11/03)
        static readonly (float meters, float smoothness, Color tint)[] k_Floors =
        {
            (1.0f, 0.35f, Color.white),   // 1 밝은 나무 (Wood049) — 결 간격으로 어림
            (1.0f, 0.35f, Color.white),   // 2 어두운 나무 (Wood051)
            // 3 오닉스 (Onyx015) — 윤나는 돌, 쇼핑몰 N05 (사용자: 고급진 게 좋다). 11/06 청운백화점: 목업(Cycles)에선 크림 벽 · 원목에서 튄 빛으로
            // 베이지였는데 Unity 엔 튄 빛이 없어 회청색으로 보여서 크림을 곱한다 (벽 텍스처 대리석에 곱한 것과 비슷)
            (2.0f, 0.75f, new Color(1f, 0.92f, 0.82f)),
            (1.8f, 0.4f, Color.white),    // 4 타일 (Tiles142) — 30cm 타일 6 × 6
            (2.4f, 0.32f, Color.white),   // 5 나무 무늬 시멘트 — Textures/Map/Source~/make_wood_cement_floor.py
        };

        // 돌려 쓰는 바닥 번호 — 3 오닉스는 빼고 N05 쇼핑몰만 (배치에서 고정. 11/03 사용자: 다른 건물엔 고급진 느낌이 안 난다)
        static readonly int[] k_FloorRotation = { 1, 2, 4, 5 };

        // 바닥을 돌려 쓸 차례 — 맵을 만들 때마다 처음부터
        static int s_FloorTurn;

        [MenuItem("MilTrainSimVR/C/맵 만들기 (배치 → 회색 박스)", false, 20)]
        public static void Rebuild()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[C] Play 중에는 맵을 만들지 않는다");
                return;
            }
            // 남의 씬에 실수로 만들지 않게 C 폴더 씬에서만
            if (!scene.path.StartsWith("Assets/C/"))
            {
                Debug.LogWarning($"[C] C 씬에서만 만든다 — 지금 씬: {scene.path}");
                return;
            }
            GameObject layout = UrbanLayoutTools.FindRoot(UrbanLayoutTools.LayoutRootName);
            if (layout == null)
            {
                Debug.LogWarning("[C] 이 씬에는 배치(C_Layout)가 없다");
                return;
            }
            GreyboxBuildingSpec[] buildings = layout.GetComponentsInChildren<GreyboxBuildingSpec>(true);
            GreyboxGroundSpec[] grounds = layout.GetComponentsInChildren<GreyboxGroundSpec>(true);
            GreyboxUndergroundSpec[] undergrounds = layout.GetComponentsInChildren<GreyboxUndergroundSpec>(true);
            GreyboxPlayAreaSpec playArea = layout.GetComponentInChildren<GreyboxPlayAreaSpec>(true);

            Materials m = LoadOrCreateMaterials();
            ResetProps();
            s_DeptProbes.Clear();
            s_HoodSites.Clear();
            s_HoodCount = 0;
            ResetUtilities(buildings, grounds);
            // 가게 간판(C_Signs) 자리 — 바깥벽 층 띠를 간판 뒤에서 끊고, 설비를 간판 자리에 안 붙인다 (UrbanGreyboxBuilder.Details.cs · Utilities.cs)
            CollectSignBounds(scene);
            GameObject old = UrbanLayoutTools.FindRoot(k_RootName);
            if (old != null)
                Undo.DestroyObjectImmediate(old);
            var root = new GameObject(k_RootName);
            Undo.RegisterCreatedObjectUndo(root, "회색 박스 맵");

            // 지하 입구(하늘이 열린 방) 자리는 땅 판에 구멍을 내야 해서 지하 계획을 먼저 세운다
            List<UgRoom> rooms = PlanUnderground(undergrounds, grounds, m);
            var holes = new List<Rect>();
            foreach (UgRoom room in rooms)
            {
                if (room.open)
                    holes.Add(room.r);
            }

            Bounds area = LayoutBounds(buildings, grounds);
            Transform groundGroup = Group("Ground", root.transform);
            BuildGround(groundGroup, grounds, area, holes, m);
            CombineVisuals(groundGroup);
            Transform buildingGroup = Group("Buildings", root.transform);
            s_FloorTurn = 0;
            foreach (GreyboxBuildingSpec spec in buildings)
            {
                Transform building = BuildBuilding(buildingGroup, spec, m);
                // 바깥벽 마감을 고른 건물은 합칠 때 벽 면을 겉면 · 1층 기단 · 실내 · 옥상으로 나누고 때 자국을 붙인다 (UrbanGreyboxBuilder.Exterior.cs)
                CombineVisuals(building, ExteriorFor(spec, building, m));
                // 설비(옥상 실외기 · 물탱크 · 안테나, 벽 배수관 · 가스관 · 벽걸이 실외기)는 합친 뒤 따로 — 보이기만 한다 (UrbanGreyboxBuilder.Utilities.cs)
                BuildUtilities(building, m);
                // 고깃집 환풍구(연통 · 천장 덕트)도 합친 뒤 따로 — 보이기만 한다 (UrbanGreyboxBuilder.Shop.cs)
                BuildHoods(building, m);
            }
            Debug.Log("[C] " + UtilitySummary());
            if (rooms.Count > 0)
            {
                Transform undergroundGroup = Group("Underground", root.transform);
                BuildUnderground(undergroundGroup, rooms, m);
                CombineVisuals(undergroundGroup);
                // 원기둥 · 전등 · 어두운 환경광 (UrbanGreyboxBuilder.UgFinish.cs, 11/18)
                UgFinish(undergroundGroup, rooms);
            }
            // 보이지 않는 벽은 놀 곳 테두리 — 없으면 바탕 땅과 같은 범위(배치 전체 + 20m)
            List<Vector3> corners = playArea != null ? playArea.Corners() : RectCorners(area);
            BuildBounds(Group("Bounds", root.transform), corners);
            // 적 길찾기 지도 — 바닥·벽이 다 바뀌었으니 같이 굽는다 (UrbanGreyboxBuilder.Navigation.cs)
            BakeNavMesh(scene, root, corners);

            // 다 만들었으면 배치 상자·바닥 그림은 감춘다 — 고칠 땐 다시 '배치 편집 모드'
            Undo.RecordObject(layout, "배치 숨기기");
            layout.SetActive(false);
            UrbanLayoutTools.HideUnderlay();
            // 백화점 반사 기준점은 배치 상자를 감춘 뒤에 굽는다 — 건물 자리 상자 안쪽이 찍히지 않게
            BakeDeptProbes(scene);
            // 백화점 에스컬레이터 통유리를 깨지는 조각으로 나눈다 (BreakableGlassBuilder, 11/18 — 카스2식으로 맞은 곳부터 부서지게)
            BreakableGlassBuilder.SplitDeptGlass(root.transform);
            // 창틀마다 깨지는 유리 (UrbanGreyboxBuilder.WindowGlass.cs, 11/18 — 건물 창 · 백화점 무늬 유리 · 부스)
            Debug.Log(InstallWindowGlass(root.transform));
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[C] 맵을 만들었다 — 건물 {buildings.Length}동, 바닥 {grounds.Length}칸, 지하 {undergrounds.Length}칸, 겉모양 모델 {s_PropCount}개");
        }

        // ---------------------------------------------------------------- 바닥

        // holes: 지하 입구 자리(맵 x·z) — 바탕 땅과 그 위 판을 뚫는다
        static void BuildGround(Transform parent, GreyboxGroundSpec[] grounds, Bounds area, List<Rect> holes, Materials m)
        {
            // 바탕 땅(아스팔트)을 배치 전체보다 넉넉히 — 걷는 땅·길찾기는 이것. 차도는 이 위에 보이기만 하는 노면을 따로 깐다 (RoadSurface, 11/05)
            foreach (Rect piece in RectMinus(Rect.MinMaxRect(area.min.x, area.min.z, area.max.x, area.max.z), holes))
                Box("Base", parent, new Vector3(piece.xMin, -0.2f, piece.yMin), new Vector3(piece.xMax, 0f, piece.yMax), m.ground, true);
            foreach (GreyboxGroundSpec ground in grounds)
            {
                switch (ground.kind)
                {
                    case GreyboxGroundSpec.Kind.Road:
                        // 사용자 11/05: 차도 텍스처는 도로 방향에 맞춰 — 중앙선이 도로 가운데를 따라간다. 교차로 조각 등은 중앙선 없는 아스팔트
                        RoadSurface(ground.name, parent, ground.transform, ground.centerLine ? m.road : m.asphalt, ground.centerLine, holes);
                        break;
                    case GreyboxGroundSpec.Kind.Sidewalk:
                        Slab(ground.name, parent, ground.transform, 0f, k_Curb, m.sidewalk, true, holes);
                        break;
                    case GreyboxGroundSpec.Kind.Alley:
                        // 샛길은 대지(인도 높이) 위 건물 사이 길 — 대지 판보다 1cm 높게 색만 다르게 깐다
                        Slab(ground.name, parent, ground.transform, 0f, k_Curb + 0.01f, m.alley, true, holes);
                        break;
                    case GreyboxGroundSpec.Kind.Lot:
                        Slab(ground.name, parent, ground.transform, 0f, 0.012f, m.lot, false, holes);
                        break;
                }
            }
            JunctionPatches(parent, grounds, m.asphalt, holes);
        }

        // 배치 상자의 위치·크기·회전 그대로 놓는 판 (높이 y0~y1). 구멍은 이 판 좌표로 옮겨 뺀다 — 판이 비스듬하면 구멍을 감싸는 사각형만큼
        static void Slab(string name, Transform parent, Transform spec, float y0, float y1, Material mat, bool collide, List<Rect> holes)
        {
            Transform holder = new GameObject(name).transform;
            holder.SetParent(parent, false);
            holder.SetPositionAndRotation(new Vector3(spec.position.x, 0f, spec.position.z), Quaternion.Euler(0f, spec.eulerAngles.y, 0f));
            float w = Mathf.Abs(spec.lossyScale.x) * 0.5f, d = Mathf.Abs(spec.lossyScale.z) * 0.5f;
            foreach (Rect piece in RectMinus(Rect.MinMaxRect(-w, -d, w, d), LocalHoles(holder, holes)))
                Box(name, holder, new Vector3(piece.xMin, y0, piece.yMin), new Vector3(piece.xMax, y1, piece.yMax), mat, collide);
        }

        // 구멍(맵 x·z)을 판 좌표로 — 판이 비스듬하면 구멍을 감싸는 사각형만큼
        static List<Rect> LocalHoles(Transform holder, List<Rect> holes)
        {
            var local = new List<Rect>();
            foreach (Rect h in holes)
            {
                Vector3 a = holder.InverseTransformPoint(new Vector3(h.xMin, 0f, h.yMin)), b = holder.InverseTransformPoint(new Vector3(h.xMax, 0f, h.yMax));
                Vector3 c = holder.InverseTransformPoint(new Vector3(h.xMin, 0f, h.yMax)), e = holder.InverseTransformPoint(new Vector3(h.xMax, 0f, h.yMin));
                local.Add(Rect.MinMaxRect(Mathf.Min(a.x, b.x, c.x, e.x), Mathf.Min(a.z, b.z, c.z, e.z), Mathf.Max(a.x, b.x, c.x, e.x), Mathf.Max(a.z, b.z, c.z, e.z)));
            }
            return local;
        }

        // 차도 노면 (사용자 11/05) — 배치 상자의 긴 쪽이 도로 방향. 텍스처(CarRoad = 가운데 노란 중앙선이 든 도로 띠) 가로 한 장 = 도로 폭,
        // 세로는 도로를 따라 같은 길이로 반복한다 → 중앙선이 늘 도로 가운데를 따라간다. 보이기만 하는 판(콜라이더 없음): 걷는 땅·길찾기는 바탕 땅.
        // 넓은 길일수록 조금 높게(1~3 cm) — 교차로에서 넓은 길이 위에 와서 좁은 길의 중앙선이 넓은 길 가장자리에서 끊긴다.
        // centerLine 을 끈 조각(교차로 등)은 중앙선 없는 아스팔트를 맵 좌표로 깔고 중앙선 있는 길보다 아래 — 겹치는 길의 중앙선이 끝까지 보인다
        static void RoadSurface(string name, Transform parent, Transform spec, Material mat, bool centerLine, List<Rect> holes)
        {
            Transform holder = new GameObject(name).transform;
            holder.SetParent(parent, false);
            holder.SetPositionAndRotation(new Vector3(spec.position.x, 0f, spec.position.z), Quaternion.Euler(0f, spec.eulerAngles.y, 0f));
            float w = Mathf.Abs(spec.lossyScale.x) * 0.5f, d = Mathf.Abs(spec.lossyScale.z) * 0.5f;
            bool alongZ = d >= w;
            float width = 2f * (alongZ ? w : d);
            float y = centerLine ? RoadHeight(width) : 0.008f;
            foreach (Rect piece in RectMinus(Rect.MinMaxRect(-w, -d, w, d), LocalHoles(holder, holes)))
            {
                var go = new GameObject(name);
                go.transform.SetParent(holder, false);
                // 꼭짓점 순서는 BoxMesh 와 같게 — CombineVisuals(MeshBuilder)가 (0,2,1)(0,3,2)로 다시 감는다. 반대로 두면 합친 뒤 아래를 봐서 안 보였다 (11/05)
                var verts = new[]
                {
                    new Vector3(piece.xMin, y, piece.yMin), new Vector3(piece.xMax, y, piece.yMin),
                    new Vector3(piece.xMax, y, piece.yMax), new Vector3(piece.xMin, y, piece.yMax),
                };
                var uvs = new Vector2[4];
                for (int i = 0; i < 4; i++)
                {
                    if (!centerLine)   // 중앙선 없는 아스팔트 — 맵 좌표(1 = 1 m), 재질 Tiling 이 한 장 크기를 정한다
                    {
                        Vector3 world = holder.TransformPoint(verts[i]);
                        uvs[i] = new Vector2(world.x, world.z);
                    }
                    else   // u = 도로를 가로질러 (0.5 = 중앙선), v = 도로를 따라 (한 장 = 도로 폭만큼)
                        uvs[i] = alongZ ? new Vector2(verts[i].x / width + 0.5f, verts[i].z / width) : new Vector2(verts[i].z / width + 0.5f, verts[i].x / width);
                }
                var mesh = new Mesh { name = name };
                mesh.vertices = verts;
                mesh.uv = uvs;
                mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
                mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            }
        }

        // 중앙선 있는 차도 노면 높이 — 넓은 길일수록 1 m 에 1 mm 씩 높게 (RoadSurface)
        static float RoadHeight(float width)
        {
            return 0.01f + 0.001f * width;
        }

        // 교차로·모서리 덮개 (사용자 11/05: 코너같이 꺾이는 구간은 중앙선 없는 아스팔트) — 중앙선 있는 차도 둘이 닿거나 겹친 곳마다
        // 한 길이 다른 길의 폭 띠를 가로지르는 칸을 중앙선 없는 아스팔트로 덮는다 → 두 길의 중앙선이 교차로 앞에서 끊긴다.
        // ㄱ 모서리 · ㅜ 삼거리 · + 사거리 모두 같다. 거의 나란한 두 길(30° 안 — 이어 붙인 길)은 건너뛴다.
        // 겹치지 않고 맞닿기만 한 길도 찾는다 — '가운데 도로 연결로'처럼 다른 길 끝 옆구리에 붙은 모서리.
        // 중앙선을 끈 차도 조각(남서쪽 '서쪽 도로 남3' 등)은 그 자체가 교차로 칸이라, 그 위로 들어온 중앙선 있는 길 부분만 덮는다.
        // 덮개는 어느 차도 노면보다 위, UV 는 맵 좌표 — 같은 재질인 바탕 땅·중앙선 없는 조각과 무늬가 이어진다. 보이기만 하는 판(콜라이더 없음)
        static void JunctionPatches(Transform parent, GreyboxGroundSpec[] grounds, Material mat, List<Rect> holes)
        {
            var lined = new List<RoadBand>();
            var plain = new List<RoadBand>();
            foreach (GreyboxGroundSpec g in grounds)
            {
                if (g.kind == GreyboxGroundSpec.Kind.Road)
                    (g.centerLine ? lined : plain).Add(new RoadBand(g.transform));
            }
            float maxWidth = 0f;
            foreach (RoadBand r in lined)
                maxWidth = Mathf.Max(maxWidth, 2f * r.halfWidth);
            float y = RoadHeight(maxWidth) + 0.003f;

            var patches = new List<List<Vector2>>();
            float parallel = Mathf.Cos(30f * Mathf.Deg2Rad);
            for (int i = 0; i < lined.Count; i++)
            {
                RoadBand a = lined[i];
                for (int j = i + 1; j < lined.Count; j++)
                {
                    RoadBand b = lined[j];
                    if (Mathf.Abs(Vector2.Dot(a.along, b.along)) > parallel || !a.Touches(b))
                        continue;
                    // a 안에서 b 의 폭 띠가 지나는 칸, b 안에서 a 의 폭 띠가 지나는 칸 — 보통 한쪽이 다른 쪽을 품는다
                    List<Vector2> inA = a.CrossingCell(b);
                    List<Vector2> inB = b.CrossingCell(a);
                    AddPatch(patches, inA);
                    if (!InsideConvex(inB, inA))
                        AddPatch(patches, inB);
                }
                foreach (RoadBand p in plain)
                    AddPatch(patches, ClipConvex(a.Corners(), p.Corners()));
            }
            foreach (List<Vector2> patch in patches)
            {
                foreach (List<Vector2> piece in MinusHoles(patch, holes))
                    FlatPolygon("Junction", parent, piece, y, mat);
            }
        }

        // 너무 작은 조각(맞닿은 모서리끼리 스친 것 등)은 버린다
        static void AddPatch(List<List<Vector2>> patches, List<Vector2> poly)
        {
            if (poly.Count >= 3 && PolygonArea(poly) >= 0.5f)
                patches.Add(poly);
        }

        // 차도 배치 상자를 맵 x·z 평면의 사각형으로 — along = 긴 쪽(도로 방향, RoadSurface 와 같은 기준), across = 폭 방향
        struct RoadBand
        {
            public Vector2 center, along, across;
            public float halfLength, halfWidth;

            public RoadBand(Transform spec)
            {
                Quaternion rotation = Quaternion.Euler(0f, spec.eulerAngles.y, 0f);
                Vector3 right = rotation * Vector3.right, forward = rotation * Vector3.forward;
                float hx = Mathf.Abs(spec.lossyScale.x) * 0.5f, hz = Mathf.Abs(spec.lossyScale.z) * 0.5f;
                bool alongZ = hz >= hx;
                center = new Vector2(spec.position.x, spec.position.z);
                along = alongZ ? new Vector2(forward.x, forward.z) : new Vector2(right.x, right.z);
                across = alongZ ? new Vector2(right.x, right.z) : new Vector2(forward.x, forward.z);
                halfLength = alongZ ? hz : hx;
                halfWidth = alongZ ? hx : hz;
            }

            public List<Vector2> Corners(float grow = 0f)
            {
                return Quad(halfLength + grow, halfWidth + grow);
            }

            // 폭 띠 — 길을 길이 방향으로 맵 밖까지 늘인 것
            public List<Vector2> Strip()
            {
                return Quad(1000f, halfWidth);
            }

            // 30 cm 안으로 붙어 있으면 닿은 것
            public bool Touches(RoadBand other)
            {
                return PolygonArea(ClipConvex(Corners(), other.Corners(0.3f))) > 0.01f;
            }

            // 이 길 안에서 other 의 폭 띠가 지나는 칸 — 이 길 폭 전체를 길 방향으로 자른 사각형 (비스듬히 만나도 이 길과 나란하게).
            // 칸 너머 남은 길이 k_JunctionStub 보다 짧으면 끝까지 덮는다 — 교차로 뒤로 몇 m 삐죽 나온 중앙선 꼬리가 안 남게
            // ('동쪽 대로'는 가운데 도로와 만나는 곳 남쪽으로 3 m 더 나와 있다)
            public List<Vector2> CrossingCell(RoadBand other)
            {
                List<Vector2> cut = ClipConvex(Corners(), other.Strip());
                if (cut.Count < 3)
                    return cut;
                float s0 = float.MaxValue, s1 = float.MinValue;
                foreach (Vector2 p in cut)
                {
                    float s = Vector2.Dot(p - center, along);
                    s0 = Mathf.Min(s0, s);
                    s1 = Mathf.Max(s1, s);
                }
                if (halfLength - s1 < k_JunctionStub)
                    s1 = halfLength;
                if (s0 + halfLength < k_JunctionStub)
                    s0 = -halfLength;
                return Span(s0, s1, halfWidth);
            }

            List<Vector2> Quad(float halfL, float halfW)
            {
                return Span(-halfL, halfL, halfW);
            }

            // 길 방향 s0~s1 (가운데에서 잰 m), 폭 ±halfW 인 사각형 — 반시계
            List<Vector2> Span(float s0, float s1, float halfW)
            {
                var quad = new List<Vector2>
                {
                    center + along * s0 - across * halfW, center + along * s1 - across * halfW,
                    center + along * s1 + across * halfW, center + along * s0 + across * halfW,
                };
                if (PolygonArea(quad) < 0f)
                    quad.Reverse();
                return quad;
            }
        }

        // 교차로 칸 너머 이보다 짧게 남은 길 끝은 칸에 넣는다 (m)
        const float k_JunctionStub = 5f;

        // 볼록 다각형 subject 를 볼록 다각형 clip(반시계) 안쪽만 남긴다 (Sutherland–Hodgman)
        static List<Vector2> ClipConvex(List<Vector2> subject, List<Vector2> clip)
        {
            List<Vector2> result = subject;
            for (int e = 0; e < clip.Count && result.Count > 0; e++)
                result = ClipHalfPlane(result, clip[e], clip[(e + 1) % clip.Count]);
            return result;
        }

        // 선 a→b 의 왼쪽(반시계 다각형이면 안쪽)만 남긴다
        static List<Vector2> ClipHalfPlane(List<Vector2> poly, Vector2 a, Vector2 b)
        {
            var result = new List<Vector2>();
            Vector2 edge = b - a;
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 p = poly[i], q = poly[(i + 1) % poly.Count];
                float dp = Cross(edge, p - a), dq = Cross(edge, q - a);
                if (dp >= 0f)
                    result.Add(p);
                if ((dp >= 0f) != (dq >= 0f))
                    result.Add(p + (q - p) * (dp / (dp - dq)));
            }
            return result;
        }

        // inner 의 꼭짓점이 모두 볼록 다각형 outer(반시계) 안 (1 cm 여유)
        static bool InsideConvex(List<Vector2> inner, List<Vector2> outer)
        {
            if (outer.Count < 3)
                return false;
            foreach (Vector2 p in inner)
            {
                for (int e = 0; e < outer.Count; e++)
                {
                    Vector2 a = outer[e], edge = outer[(e + 1) % outer.Count] - a;
                    if (Cross(edge, p - a) < -0.01f * edge.magnitude)
                        return false;
                }
            }
            return true;
        }

        // 볼록 다각형에서 구멍(맵 축에 나란한 사각형)들을 뺀 볼록 조각들 — 구멍 변 k 바깥 ∩ 앞 변들 안쪽으로 나눈다
        static List<List<Vector2>> MinusHoles(List<Vector2> poly, List<Rect> holes)
        {
            var pieces = new List<List<Vector2>> { poly };
            foreach (Rect h in holes)
            {
                var hole = new[] { new Vector2(h.xMin, h.yMin), new Vector2(h.xMax, h.yMin), new Vector2(h.xMax, h.yMax), new Vector2(h.xMin, h.yMax) };
                var next = new List<List<Vector2>>();
                foreach (List<Vector2> piece in pieces)
                {
                    List<Vector2> rest = piece;
                    for (int e = 0; e < 4 && rest.Count >= 3; e++)
                    {
                        Vector2 a = hole[e], b = hole[(e + 1) % 4];
                        List<Vector2> outside = ClipHalfPlane(rest, b, a);
                        if (outside.Count >= 3 && PolygonArea(outside) > 0.01f)
                            next.Add(outside);
                        rest = ClipHalfPlane(rest, a, b);
                    }
                }
                pieces = next;
            }
            return pieces;
        }

        static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        // 넓이 — 반시계면 +
        static float PolygonArea(List<Vector2> poly)
        {
            float sum = 0f;
            for (int i = 0; i < poly.Count; i++)
                sum += Cross(poly[i], poly[(i + 1) % poly.Count]);
            return sum * 0.5f;
        }

        // 맵 x·z 평면의 볼록 다각형(반시계)을 높이 y 에서 위를 보는 판으로 — UV 는 맵 좌표(1 = 1 m).
        // CombineVisuals 는 꼭짓점 4개(면 하나)씩 읽어 (0,2,1)(0,3,2)로 감으니 부채꼴 삼각형을 둘씩 사각형 하나에 담는다 (남는 하나는 끝 점을 겹쳐서)
        static void FlatPolygon(string name, Transform parent, List<Vector2> poly, float y, Material mat)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int i = 1; i + 1 < poly.Count; i += 2)
            {
                int start = verts.Count;
                foreach (int k in new[] { 0, i, i + 1, Mathf.Min(i + 2, poly.Count - 1) })
                {
                    verts.Add(parent.InverseTransformPoint(new Vector3(poly[k].x, y, poly[k].y)));
                    uvs.Add(poly[k]);
                }
                triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var normals = new Vector3[verts.Count];
            for (int i = 0; i < normals.Length; i++)
                normals[i] = Vector3.up;
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.normals = normals;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        // 보이지 않는 벽 — 테두리(꼭짓점을 차례로 이은 선) 밖으로 나가지 못하게. 변마다 선 바깥쪽에 두께 1m · 높이 40m 상자 하나.
        // 'Ignore Raycast' 층이라 총알·적의 시선은 지나간다(벽 너머 배경 건물에 탄흔이 난다) — 사람 몸(캐릭터 컨트롤러)과 적 길찾기만 막는다
        static void BuildBounds(Transform parent, List<Vector3> corners)
        {
            Vector3 middle = Vector3.zero;
            foreach (Vector3 c in corners)
                middle += c;
            middle /= corners.Count;
            for (int i = 0; i < corners.Count; i++)
            {
                Vector3 a = corners[i], b = corners[(i + 1) % corners.Count];
                Vector3 along = b - a;
                float length = along.magnitude;
                if (length < 0.01f)
                    continue;
                Vector3 dir = along / length;
                // 변에 수직인 두 방향 중 다각형 가운데에서 먼 쪽이 바깥
                var outward = new Vector3(dir.z, 0f, -dir.x);
                if (Vector3.Dot(outward, (a + b) * 0.5f - middle) < 0f)
                    outward = -outward;
                var go = new GameObject($"Bound_{i + 1}_{Compass(outward)}") { layer = k_IgnoreRaycastLayer };
                go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation((a + b) * 0.5f + outward * 0.5f + Vector3.up * 20f, Quaternion.LookRotation(dir, Vector3.up));
                // 양 끝을 1m 씩 더 늘려 모서리에서 이웃 벽과 겹치게 — 틈으로 빠져나가지 않게
                go.AddComponent<BoxCollider>().size = new Vector3(1f, 40f, length + 2f);
            }
        }

        // 'Ignore Raycast' — 유니티 기본 층(2번). 총알·시선 광선(Physics.DefaultRaycastLayers)이 이 층을 건너뛴다
        const int k_IgnoreRaycastLayer = 2;

        static string Compass(Vector3 d)
        {
            if (Mathf.Abs(d.x) > Mathf.Abs(d.z))
                return d.x > 0f ? "E" : "W";
            return d.z > 0f ? "N" : "S";
        }

        static List<Vector3> RectCorners(Bounds b)
        {
            return new List<Vector3>
            {
                new Vector3(b.min.x, 0f, b.min.z), new Vector3(b.max.x, 0f, b.min.z),
                new Vector3(b.max.x, 0f, b.max.z), new Vector3(b.min.x, 0f, b.max.z),
            };
        }

        static Bounds LayoutBounds(GreyboxBuildingSpec[] buildings, GreyboxGroundSpec[] grounds)
        {
            var bounds = new Bounds();
            bool first = true;
            var all = new List<Transform>();
            foreach (GreyboxBuildingSpec b in buildings) all.Add(b.transform);
            foreach (GreyboxGroundSpec g in grounds) all.Add(g.transform);
            foreach (Transform t in all)
            {
                foreach (Vector3 corner in Footprint(t))
                {
                    if (first) { bounds = new Bounds(corner, Vector3.zero); first = false; }
                    else bounds.Encapsulate(corner);
                }
            }
            bounds.Expand(new Vector3(20f, 0f, 20f));
            return bounds;
        }

        // 배치 상자 바닥의 네 모서리 (회전 포함, y=0)
        static Vector3[] Footprint(Transform t)
        {
            float w = Mathf.Abs(t.lossyScale.x) * 0.5f, d = Mathf.Abs(t.lossyScale.z) * 0.5f;
            Quaternion r = Quaternion.Euler(0f, t.eulerAngles.y, 0f);
            var c = new Vector3(t.position.x, 0f, t.position.z);
            return new[] { c + r * new Vector3(-w, 0f, -d), c + r * new Vector3(w, 0f, -d), c + r * new Vector3(w, 0f, d), c + r * new Vector3(-w, 0f, d) };
        }

        // ---------------------------------------------------------------- 건물

        static Transform BuildBuilding(Transform parent, GreyboxBuildingSpec spec, Materials m)
        {
            Transform t = spec.transform;
            float width = Mathf.Abs(t.lossyScale.x), depth = Mathf.Abs(t.lossyScale.z);
            Quaternion rotation = Quaternion.Euler(0f, t.eulerAngles.y, 0f);
            var center = new Vector3(t.position.x, 0f, t.position.z);
            // 건물마다 자기 좌표를 쓴다: 앞면 왼쪽 아래 모서리가 원점, x = 앞면을 따라(폭), z = 안쪽으로(깊이), 앞면은 -z.
            // 그래서 어느 방향·각도 건물이든 같은 코드로 문·창·계단을 만든다
            Vector3 origin = center + rotation * new Vector3(-width * 0.5f, 0f, -depth * 0.5f);
            Transform root = new GameObject("B_" + spec.name).transform;
            root.SetParent(parent, false);
            root.SetPositionAndRotation(origin, rotation);
            Material wall = m.walls[Mathf.Abs(spec.tint) % m.walls.Length];

            int floors = Mathf.Max(1, spec.floors);
            bool mall = spec.layout == GreyboxBuildingSpec.Layout.Mall;
            int enterable = Mathf.Clamp(spec.enterable, 0, Mathf.Min(mall ? k_MaxMallFloors : k_MaxEnterable, floors));
            if (mall)
            {
                // 쇼핑몰 평면은 그림 비율대로 늘려 쓰니 모서리 계단실(꺾인 계단)이 들어갈 만큼 커야 한다
                if (enterable > 0 && (Mathf.Max(width, depth) < k_MallMinLong || Mathf.Min(width, depth) < k_MallMinShort))
                {
                    Debug.LogWarning($"[C] {spec.name}: 쇼핑몰은 긴 변 {k_MallMinLong}m·짧은 변 {k_MallMinShort}m 이상이어야 한다 — 겉모양만 만든다", spec);
                    enterable = 0;
                }
            }
            else
            {
                // 앞면 폭: 가운데 복도 옆에 방이 들어갈 만큼. 2층: 깊이 쪽으로 계단(가장 가파르게 4.8m)과 앞뒤 디딜 자리
                if (enterable > 0 && (width < k_MinInteriorWidth || depth < k_MinInteriorDepth))
                {
                    Debug.LogWarning($"[C] {spec.name}: 앞면 폭 {k_MinInteriorWidth}m·깊이 {k_MinInteriorDepth}m 이상이어야 실내가 들어간다 — 겉모양만 만든다", spec);
                    enterable = 0;
                }
                if (enterable > 1 && depth < k_MinStairDepth)
                {
                    Debug.LogWarning($"[C] {spec.name}: 깊이 {k_MinStairDepth}m 이상이어야 2층 계단이 들어간다 — 1층만 만든다", spec);
                    enterable = 1;
                }
            }
            // 백화점 실내(UrbanGreyboxBuilder.Dept.cs)는 들어갈 수 있는 쇼핑몰만 — 실내 벽은 한 층 높이 벽 한 장, 막힌 위층 밑면(꼭대기 층 천장)은 크림 천장
            bool dept = mall && enterable > 0 && spec.departmentStore;
            // 층마다 바깥벽 구멍(창 · 문) — 세라믹 외벽 · 바깥벽 입체 디테일이 창 둘레 · 창턱 자리를 맞춘다
            List<Vector4>[][] wallOpenings = null;
            if (enterable > 0)
            {
                Material floor = FloorFor(spec, m);
                if (mall)
                    wallOpenings = BuildMall(root, spec, enterable, floors, width, depth, dept ? m.deptWall : wall, floor, m);
                else
                    wallOpenings = BuildInterior(root, spec, enterable, floors, width, depth, wall, floor, m);
            }
            // 세라믹 외벽은 창 자리를 아는 쇼핑몰만 (사용자 11/06 — 청운백화점 N05)
            bool ceramic = spec.facade == GreyboxBuildingSpec.Facade.Ceramic;
            if (ceramic && !mall)
            {
                Debug.LogWarning($"[C] {spec.name}: 세라믹 외벽은 들어갈 수 있는 쇼핑몰만 — 보통 벽으로 만든다", spec);
                ceramic = false;
            }
            // 바깥벽 마감을 고른 건물은 입체 디테일(층 띠 · 창턱 · 창틀 · 옥상 파라펫 · 옥탑 …)을 붙인다 (UrbanGreyboxBuilder.Details.cs)
            Material face = FaceMaterialFor(spec, m);   // 마감 없음 · 세라믹 외벽은 null
            bool detailed = face != null;

            // 들어갈 수 없는 위층(또는 건물 전체)은 속이 찬 상자 하나 — 아래층 천장 겸 지붕.
            // 디테일을 붙이는 건물은 상자를 지붕 높이에서 끊고 위에 진짜 파라펫(벽 난간)을 세운다 — 예전엔 상자가 0.6 m 더 올라가 난간 겸이었다
            float solidBottom = enterable > 0 ? k_Curb + enterable * k_FloorHeight : 0f;
            float top = k_Curb + floors * k_FloorHeight;
            if (floors > enterable)
                Box("Mass", root, new Vector3(0f, solidBottom, 0f), new Vector3(width, detailed ? top : top + k_Parapet, depth), dept ? m.deptCeiling : wall, true);
            else
                RoofWithParapet(root, width, depth, top, wall);
            // 설비 자리 (옥상 바닥 높이 · 창 · 문 구멍 · 옥탑) — 합친 뒤 BuildUtilities 가 쓴다. 세라믹 외벽은 상자 위에 방수층(2 cm)이 깔린다
            float roofY = ceramic ? top + k_Parapet + 0.02f : floors > enterable && !detailed ? top + k_Parapet : top;
            RegisterUtilitySite(root, spec, floors, enterable, width, depth, roofY, ceramic, detailed, wallOpenings);

            // 앞면 장식: 1층 유리 상가 · 층마다 창 띠 (들어갈 수 있는 층은 진짜 구멍이라 띠를 안 붙인다).
            // 간판 띠는 11/06 에 없앴다 — 가게 간판(C_Signs)은 건물 벽에 바로 붙는다
            if (enterable == 0)
            {
                if (spec.closedShutters)
                    ClosedShutters(root, width, depth, m);   // 1층 네 면을 내린 셔터로 (사용자 11/17 N11 — Shop.cs)
                else
                    Box("Storefront", root, new Vector3(0.6f, k_Curb + 0.1f, -0.05f), new Vector3(width - 0.6f, k_Curb + 2.6f, 0f), m.glass, false);
            }
            // 세라믹 외벽 건물은 막힌 층 창 띠 대신 외벽 장식이 다 한다
            if (ceramic)
            {
                BuildCeramicFacade(root, spec, width, depth, floors, enterable, wallOpenings, m);
                return root;
            }
            if (detailed)
                BuildExteriorDetails(root, spec, face, floors, width, depth, top, floors > enterable, wallOpenings, m);
            // 막힌 층의 창 띠는 네 면 모두 — 앞면에만 있으면 뒤·옆에서 볼 때 그냥 상자다
            for (int f = Mathf.Max(1, enterable); f < floors; f++)
            {
                float y = k_Curb + f * k_FloorHeight;
                string tag = (f + 1) + "F";
                Box("Windows_" + tag, root, new Vector3(0.8f, y + 0.9f, -0.05f), new Vector3(width - 0.8f, y + 2.2f, 0f), m.windows, false);
                Box("Windows_" + tag + "_Back", root, new Vector3(0.8f, y + 0.9f, depth), new Vector3(width - 0.8f, y + 2.2f, depth + 0.05f), m.windows, false);
                Box("Windows_" + tag + "_Left", root, new Vector3(-0.05f, y + 0.9f, 0.8f), new Vector3(0f, y + 2.2f, depth - 0.8f), m.windows, false);
                Box("Windows_" + tag + "_Right", root, new Vector3(width, y + 0.9f, 0.8f), new Vector3(width + 0.05f, y + 2.2f, depth - 0.8f), m.windows, false);
            }
            return root;
        }

        static void RoofWithParapet(Transform root, float W, float D, float top, Material wall)
        {
            // 지붕판은 벽 안쪽에만 — 벽과 겹치면 같은 바깥면이 두 번 그려져 깜빡인다
            Box("Roof", root, new Vector3(k_Wall, top - k_Slab, k_Wall), new Vector3(W - k_Wall, top, D - k_Wall), wall, true);
            Box("Parapet_Front", root, new Vector3(0f, top, 0f), new Vector3(W, top + k_Parapet, k_Wall), wall, true);
            Box("Parapet_Back", root, new Vector3(0f, top, D - k_Wall), new Vector3(W, top + k_Parapet, D), wall, true);
            Box("Parapet_Left", root, new Vector3(0f, top, k_Wall), new Vector3(k_Wall, top + k_Parapet, D - k_Wall), wall, true);
            Box("Parapet_Right", root, new Vector3(W - k_Wall, top, k_Wall), new Vector3(W, top + k_Parapet, D - k_Wall), wall, true);
        }

        // ---------------------------------------------------------------- 만들기 도구

        static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        // 축에 나란한 상자 — 부모 좌표의 최소·최대 모서리로 만든다.
        // 메시는 직접 만든다(꼭짓점 24개, 위치·법선·UV 만). 예전엔 ProBuilder 상자라 원본 데이터와 구운 메시가 씬에 두 번 저장돼
        // 상자 4,658개에 씬이 68MB 가 됐다(GitHub 파일 한도 100MB) — 실내가 늘수록 계속 커지니 원본을 안 남긴다
        static GameObject Box(string name, Transform parent, Vector3 min, Vector3 max, Material mat, bool collide)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = (min + max) * 0.5f;
            go.transform.localRotation = Quaternion.identity;
            // 월드 기준 UV 라 자리를 정한 뒤에 만든다
            go.AddComponent<MeshFilter>().sharedMesh = BoxMesh(name, go.transform, (max - min) * 0.5f);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            if (collide)
            {
                var box = go.AddComponent<BoxCollider>();
                box.center = Vector3.zero;
                box.size = max - min;
            }
            return go;
        }

        // 건물 하나(또는 땅·지하 묶음)의 보이는 상자들을 재질·렌더링 레이어별로 메시 하나씩 합친다.
        // 상자마다 메시를 따로 두면 메시마다 머리말이 붙어 씬이 금방 커진다(상자 4,658개에 36MB). 합치면 그리기 호출도 준다.
        // 합칠 때 화면은 그대로 두고 두 가지를 덜어 낸다 (10/26 — 맵 씬 14MB 의 61% 가 메시였다. 꼭짓점 약 37% 줄어듦):
        //   · 맞닿은 상자끼리 서로 가리는 면 — 바닥판에 선 벽의 밑면, 옆 조각과 붙은 끝면 … (면의 약 31%)
        //   · 같은 자리·같은 법선 꼭짓점은 하나로 묶는다 — UV 가 월드 좌표라 자리·방향이 같으면 UV 도 같다
        // 부딪히는 조각(벽·바닥·가구·바리케이드 …)은 보이는 부분만 떼고 콜라이더는 부모로 모으고(MergeColliders), 보이기만 하는 조각(유리·간판·계단 단)은 지운다
        // skin: 바깥벽 마감을 고른 건물 — 벽 재질 면을 겉면 · 1층 기단 · 실내 · 옥상으로 나눠 칠하고 때 자국 판을 붙인다 (UrbanGreyboxBuilder.Exterior.cs)
        static void CombineVisuals(Transform root, ExteriorSkin skin = null)
        {
            MeshFilter[] pieces = root.GetComponentsInChildren<MeshFilter>();
            Matrix4x4 toRoot = root.worldToLocalMatrix;
            var faces = new List<Face>();
            var props = new List<PropSlot>();
            foreach (MeshFilter mf in pieces)
            {
                var mr = mf.GetComponent<MeshRenderer>();
                Matrix4x4 m = toRoot * mf.transform.localToWorldMatrix;
                // 모델로 겉모양을 씌울 상자(UrbanGreyboxBuilder.Props.cs)는 자리만 받아 두고 면은 합치지 않는다 — 콜라이더는 아래에서 그대로 모인다
                if (TakeSkin(mf, m, props))
                    continue;
                Mesh src = mf.sharedMesh;
                Vector3[] v = src.vertices, n = src.normals;
                Vector2[] uv = src.uv;
                for (int i = 0; i + 3 < v.Length; i += 4)
                    faces.Add(new Face(mr.sharedMaterial, mr.renderingLayerMask, m, v, n, uv, i));
            }
            MarkHiddenFaces(faces);
            if (skin != null)
                ApplyExterior(root, faces, skin);

            var groups = new Dictionary<(Material, uint), MeshBuilder>();
            foreach (Face f in faces)
            {
                if (f.hidden)
                    continue;
                var key = (f.material, f.layer);
                if (!groups.TryGetValue(key, out MeshBuilder builder))
                    groups[key] = builder = new MeshBuilder();
                builder.Add(f);
            }
            foreach (KeyValuePair<(Material, uint), MeshBuilder> g in groups)
            {
                var go = new GameObject("Mesh_" + g.Key.Item1.name);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = g.Value.Build(root.name + "_" + g.Key.Item1.name);
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = g.Key.Item1;
                renderer.renderingLayerMask = g.Key.Item2;
                // 비치는 재질(백화점 무빙워크 유리)은 그림자를 안 드리운다 — URP 그림자는 알파를 몰라 통유리가 벽처럼 그늘진다
                if (g.Key.Item1.renderQueue >= (int)RenderQueue.Transparent)
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                // 반사 기준점(백화점 실내, UrbanGreyboxBuilder.Dept.cs)을 구울 때 그려지게 Reflection Probe Static 도
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic);
            }
            foreach (MeshFilter mf in pieces)
            {
                Object.DestroyImmediate(mf.sharedMesh);
                if (mf.GetComponent<Collider>() == null)
                {
                    Object.DestroyImmediate(mf.gameObject);
                    continue;
                }
                Object.DestroyImmediate(mf.GetComponent<MeshRenderer>());
                Object.DestroyImmediate(mf);
            }
            MergeColliders(root);
            PlaceProps(root, props);
        }

        // 상자 면 하나(꼭짓점 4개)를 root 좌표로 옮겨 둔 것
        class Face
        {
            public Material material;   // 바깥벽 마감(ApplyExterior)이 면마다 바꿔 칠한다
            public readonly uint layer;
            public readonly bool opaque;
            public readonly Vector3[] p = new Vector3[4];
            public readonly Vector2[] uv = new Vector2[4];
            public readonly Vector3 normal;
            // 법선이 root 축(0 x · 1 y · 2 z)과 나란하면 그 축 — 비스듬한 바닥 판(-1)은 가림 검사를 안 한다
            public readonly int axis = -1;
            public readonly bool positive;
            public readonly float plane;    // 면이 놓인 평면 (axis 좌표)
            public readonly long planeKey;  // 평면을 1mm 단위로 — 같은 평면끼리 묶는 열쇠
            public readonly Rect rect;      // 평면 위 나머지 두 축 범위
            // 면이 그 범위(rect)를 꽉 채우는 직사각형인가 — 기운 상자의 옆면은 평행사변형이라 rect 가 실제보다 훨씬 크다.
            // 그런 면이 남을 가린다고 보면 안 된다 (11/16: 무빙워크 노란 줄(기운 얇은 상자) 옆면의 rect 가 경사 전체를 덮어,
            // 그 옆에 붙은 옆판 면 — 걷는 판 쪽을 보는 면 — 이 '가려졌다'고 지워져 구멍이 났다)
            public readonly bool fillsRect;
            public bool hidden;

            public Face(Material material, uint layer, Matrix4x4 m, Vector3[] v, Vector3[] n, Vector2[] uv, int i)
            {
                this.material = material;
                this.layer = layer;
                // 유리처럼 비치는 재질은 뒤에 붙은 면을 가리지 못한다
                opaque = material.renderQueue < (int)RenderQueue.AlphaTest;
                for (int k = 0; k < 4; k++)
                {
                    p[k] = m.MultiplyPoint3x4(v[i + k]);
                    this.uv[k] = uv[i + k];
                }
                normal = m.MultiplyVector(n[i]).normalized;
                for (int a = 0; a < 3; a++)
                {
                    if (Mathf.Abs(normal[a]) > 0.999f)
                        axis = a;
                }
                if (axis < 0)
                    return;
                positive = normal[axis] > 0f;
                plane = p[0][axis];
                planeKey = (long)Mathf.Round(plane * 1000f);
                int u = (axis + 1) % 3, w = (axis + 2) % 3;
                float u0 = Mathf.Min(Mathf.Min(p[0][u], p[1][u]), Mathf.Min(p[2][u], p[3][u]));
                float u1 = Mathf.Max(Mathf.Max(p[0][u], p[1][u]), Mathf.Max(p[2][u], p[3][u]));
                float w0 = Mathf.Min(Mathf.Min(p[0][w], p[1][w]), Mathf.Min(p[2][w], p[3][w]));
                float w1 = Mathf.Max(Mathf.Max(p[0][w], p[1][w]), Mathf.Max(p[2][w], p[3][w]));
                rect = Rect.MinMaxRect(u0, w0, u1, w1);
                // 평면 위 넓이(신발끈 공식) 가 rect 넓이와 같아야 꽉 채운 직사각형
                float area = 0f;
                for (int k = 0; k < 4; k++)
                {
                    Vector3 a = p[k], b = p[(k + 1) % 4];
                    area += a[u] * b[w] - b[u] * a[w];
                }
                fillsRect = Mathf.Abs(area) * 0.5f >= rect.width * rect.height * 0.995f - 1e-6f;
            }
        }

        // 같은 평면에서 반대쪽을 보는 불투명한 면이 통째로 덮는 면 = 두 상자가 맞붙은 자리라 어디서 봐도 안 보인다.
        // 한 면이 통째로 덮을 때만 뺀다 — 여러 면이 나눠 덮는 경우는 그대로 둔다(덜 줄 뿐 틀리지는 않는다)
        static void MarkHiddenFaces(List<Face> faces)
        {
            var planes = new Dictionary<(int, bool, long), List<Face>>();
            foreach (Face f in faces)
            {
                // 가리는 쪽은 불투명하고 rect 를 꽉 채운 직사각형 면만 (기운 상자 옆면의 평행사변형은 rect 가 부풀어 있다)
                if (f.axis < 0 || !f.opaque || !f.fillsRect)
                    continue;
                var key = (f.axis, f.positive, f.planeKey);
                if (!planes.TryGetValue(key, out List<Face> list))
                    planes[key] = list = new List<Face>();
                list.Add(f);
            }
            const float tol = 0.002f;
            foreach (Face f in faces)
            {
                if (f.axis < 0 || !planes.TryGetValue((f.axis, !f.positive, f.planeKey), out List<Face> others))
                    continue;
                foreach (Face o in others)
                {
                    if (o.rect.xMin <= f.rect.xMin + tol && o.rect.xMax >= f.rect.xMax - tol &&
                        o.rect.yMin <= f.rect.yMin + tol && o.rect.yMax >= f.rect.yMax - tol)
                    {
                        f.hidden = true;
                        break;
                    }
                }
            }
        }

        // 남은 면들로 메시 하나를 쌓는다 — 같은 자리(0.1mm)·같은 법선 꼭짓점은 하나만 둔다.
        // 꼭짓점 형식은 보통 메시와 같게(위치·법선·UV 모두 4바이트 실수). 법선을 1바이트로 줄였더니(10/26)
        // Play 때 정적 배칭이 건물 메시를 합치면서 법선을 잘못 옮겨 써서 지붕·가판대 그늘이 깨졌다
        class MeshBuilder
        {
            readonly List<Vector3> m_Positions = new List<Vector3>();
            readonly List<Vector3> m_Normals = new List<Vector3>();
            readonly List<Vector2> m_Uvs = new List<Vector2>();
            readonly List<int> m_Indices = new List<int>();
            readonly Dictionary<(long, long, long, int), int> m_Lookup = new Dictionary<(long, long, long, int), int>();

            public void Add(Face f)
            {
                int i0 = Vertex(f, 0), i1 = Vertex(f, 1), i2 = Vertex(f, 2), i3 = Vertex(f, 3);
                // BoxMesh 와 같은 감김 순서 — 바깥에서 볼 때 앞면
                m_Indices.Add(i0); m_Indices.Add(i2); m_Indices.Add(i1);
                m_Indices.Add(i0); m_Indices.Add(i3); m_Indices.Add(i2);
            }

            int Vertex(Face f, int k)
            {
                Vector3 p = f.p[k];
                // 법선은 1/100 단위로 맞춰 비교 — 상자 법선은 축 방향이라 같은 방향이면 값이 같다
                int n = (Mathf.RoundToInt(f.normal.x * 100f) + 128) << 16 | (Mathf.RoundToInt(f.normal.y * 100f) + 128) << 8 | (Mathf.RoundToInt(f.normal.z * 100f) + 128);
                var key = ((long)Mathf.Round(p.x * 10000f), (long)Mathf.Round(p.y * 10000f), (long)Mathf.Round(p.z * 10000f), n);
                if (!m_Lookup.TryGetValue(key, out int index))
                {
                    index = m_Positions.Count;
                    m_Lookup[key] = index;
                    m_Positions.Add(p);
                    m_Normals.Add(f.normal);
                    m_Uvs.Add(f.uv[k]);
                }
                return index;
            }

            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.indexFormat = m_Positions.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.SetVertices(m_Positions);
                mesh.SetNormals(m_Normals);
                mesh.SetUVs(0, m_Uvs);
                mesh.SetIndices(m_Indices, MeshTopology.Triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        // 콜라이더만 남은 조각(Transform + BoxCollider)을 부모 오브젝트의 BoxCollider 로 옮기고 조각은 지운다.
        // 조각마다 붙던 GameObject·Transform 이 씬의 약 1/5 이었다(3,035개 약 2.8MB). 상자 모양·자리는 그대로라 부딪힘·내비·총알은 같다.
        // 부모와 방향이 다른 조각(기울어진 계단 경사로)은 상자 콜라이더 하나로 못 옮겨서 그대로 둔다
        static void MergeColliders(Transform root)
        {
            foreach (BoxCollider box in root.GetComponentsInChildren<BoxCollider>(true))
            {
                Transform t = box.transform;
                if (t == root || t.childCount > 0 || t.localRotation != Quaternion.identity || t.localScale != Vector3.one)
                    continue;
                if (t.GetComponents<Component>().Length != 2)
                    continue;
                if (t.gameObject.layer != t.parent.gameObject.layer)
                    continue;   // 다른 층(백화점 난간 'Ignore Raycast' 등) — 합치면 층을 잃는다
                var merged = t.parent.gameObject.AddComponent<BoxCollider>();
                merged.center = t.localPosition + box.center;
                merged.size = box.size;
                Object.DestroyImmediate(t.gameObject);
            }
        }

        // 상자 면 여섯: 바깥쪽 법선 n 과 면 위 두 축 (a, b) — a × b = n 이 되게 골라야 바깥에서 볼 때 앞면이다
        static readonly Vector3[][] k_BoxFaces =
        {
            new[] { Vector3.right, Vector3.up, Vector3.forward },
            new[] { Vector3.left, Vector3.forward, Vector3.up },
            new[] { Vector3.up, Vector3.forward, Vector3.right },
            new[] { Vector3.down, Vector3.right, Vector3.forward },
            new[] { Vector3.forward, Vector3.right, Vector3.up },
            new[] { Vector3.back, Vector3.up, Vector3.right },
        };

        // 반 크기 half 인 상자 메시. UV 는 면마다 월드 좌표 두 축 그대로 — 어느 상자든 같은 1m 격자가 끊김 없이 이어진다
        static Mesh BoxMesh(string name, Transform t, Vector3 half)
        {
            var vertices = new Vector3[24];
            var normals = new Vector3[24];
            var uvs = new Vector2[24];
            var triangles = new int[36];
            for (int f = 0; f < 6; f++)
            {
                Vector3 n = k_BoxFaces[f][0];
                Vector3 c = Vector3.Scale(n, half), a = Vector3.Scale(k_BoxFaces[f][1], half), b = Vector3.Scale(k_BoxFaces[f][2], half);
                Vector3 wn = t.TransformDirection(n);
                int i = f * 4;
                vertices[i] = c - a - b;
                vertices[i + 1] = c - a + b;
                vertices[i + 2] = c + a + b;
                vertices[i + 3] = c + a - b;
                for (int k = 0; k < 4; k++)
                {
                    normals[i + k] = n;
                    Vector3 w = t.TransformPoint(vertices[i + k]);
                    // 면이 맵에서 바라보는 축을 빼고 남은 두 축이 UV
                    if (Mathf.Abs(wn.x) > Mathf.Abs(wn.y) && Mathf.Abs(wn.x) > Mathf.Abs(wn.z))
                        uvs[i + k] = new Vector2(w.z, w.y);
                    else if (Mathf.Abs(wn.y) > Mathf.Abs(wn.z))
                        uvs[i + k] = new Vector2(w.x, w.z);
                    else
                        uvs[i + k] = new Vector2(w.x, w.y);
                }
                int[] quad = { i, i + 2, i + 1, i, i + 3, i + 2 };
                System.Array.Copy(quad, 0, triangles, f * 6, 6);
            }
            var mesh = new Mesh { name = name };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        // 벽 하나를 문·창 구멍을 피해 상자 여러 개로 쌓는다.
        // a→b: 벽 바깥면을 따라가는 선(축과 나란히), inward: 벽 두께가 들어가는 쪽,
        // 구멍 (u0, u1, v0, v1): a 에서 벽을 따라 간 거리 u0~u1, 이 층 바닥에서의 높이 v0~v1
        // 바깥벽은 두께 k_Wall · 층 높이 그대로, 실내 칸막이는 더 얇게 · 천장(위층 바닥판 밑)까지만
        static void Wall(string name, Transform parent, Vector3 a, Vector3 b, Vector3 inward, List<Vector4> holes, Material mat,
            float thickness = k_Wall, float height = k_FloorHeight)
        {
            Vector3 dir = b - a;
            float length = dir.magnitude;
            if (length < 0.01f)
                return;
            dir /= length;
            var cuts = new List<float> { 0f, length };
            if (holes != null)
            {
                foreach (Vector4 hole in holes)
                {
                    cuts.Add(Mathf.Clamp(hole.x, 0f, length));
                    cuts.Add(Mathf.Clamp(hole.y, 0f, length));
                }
            }
            cuts.Sort();

            for (int i = 0; i < cuts.Count - 1; i++)
            {
                float u0 = cuts[i], u1 = cuts[i + 1];
                if (u1 - u0 < 0.01f)
                    continue;
                float mid = (u0 + u1) * 0.5f;
                Vector4? opening = null;
                if (holes != null)
                {
                    foreach (Vector4 hole in holes)
                    {
                        if (mid > hole.x && mid < hole.y)
                            opening = hole;
                    }
                }

                // 구멍이 있는 띠는 구멍 아래(창턱)·위(인방)만, 없는 띠는 통째로
                var spans = new List<Vector2>();
                if (opening == null)
                {
                    spans.Add(new Vector2(0f, height));
                }
                else
                {
                    if (opening.Value.z > 0.01f)
                        spans.Add(new Vector2(0f, opening.Value.z));
                    if (opening.Value.w < height - 0.01f)
                        spans.Add(new Vector2(opening.Value.w, height));
                }

                foreach (Vector2 span in spans)
                {
                    Vector3 p0 = a + dir * u0 + Vector3.up * span.x;
                    Vector3 p1 = a + dir * u1 + Vector3.up * span.y + inward * thickness;
                    Box(name, parent, Vector3.Min(p0, p1), Vector3.Max(p0, p1), mat, true);
                }
            }
        }

        // 구멍(계단 자리)들을 남기고 바닥판을 깐다 — 남은 곳을 사각형 몇 개로 (RectMinus).
        // 윗면 k_FloorFinish 는 바닥 재질, 그 아래는 천장 재질 — 상자 하나는 재질이 하나라 통으로 만들면 아래층 천장까지 나무 · 돌이 된다 (11/03)
        static void SlabWithHoles(string name, Transform parent, Rect area, List<Rect> holes, float topY, Material floor, Material ceiling)
        {
            float y0 = topY - k_Slab, y1 = topY - k_FloorFinish;
            foreach (Rect piece in RectMinus(area, holes))
            {
                if (piece.width <= 0.01f || piece.height <= 0.01f)
                    continue;
                Box(name, parent, new Vector3(piece.xMin, y0, piece.yMin), new Vector3(piece.xMax, y1, piece.yMax), ceiling, true);
                Box(name, parent, new Vector3(piece.xMin, y1, piece.yMin), new Vector3(piece.xMax, topY, piece.yMax), floor, true);
            }
        }

        // 실내 바닥 — 배치에서 번호를 정했으면 그것, 아니면 실내가 있는 건물끼리 배치 순서대로 k_FloorRotation 을 돌려 쓴다
        // (11/03 사용자: 건물마다 다르게, 한 건물은 같은 바닥, N05 쇼핑몰은 3번)
        static Material FloorFor(GreyboxBuildingSpec spec, Materials m)
        {
            int number = spec.floorMaterial > 0 ? spec.floorMaterial : k_FloorRotation[s_FloorTurn++ % k_FloorRotation.Length];
            return m.floors[Mathf.Clamp(number, 1, m.floors.Length) - 1];
        }

        // 보이는 계단 단 (밟는 것은 Ramp 가 맡는다). alongZ: 깊이(z) 방향으로 오르는지, 아니면 x 로 — bottom → top 순서로 오르니 좌표가 줄어드는 쪽으로도 된다.
        // 단은 baseY(없으면 bottom 높이)부터 꽉 채운다. thickness 를 주면 단마다 그 두께만 — 아래로 사람이 지나가는 위층 계단(꺾인 계단·에스컬레이터).
        // fill: 단 밑을 보이지 않는 콜라이더로 채운다 — 계단 밑으로 사람·길찾기가 파고들지 않게. 보이는 단 윗면은 경사로 선보다
        // 한 단 높이까지 튀어나와 있어서(단 뒤 모서리가 경사로 선 위) 채움은 단 앞 모서리 높이까지만 — 밟는 면은 늘 매끈한 경사로.
        // visual = false: 단은 안 그리고 채움만 (백화점 무빙워크 — 겉모양은 기운 판이 따로, UrbanGreyboxBuilder.Dept.cs)
        static void Steps(string name, Transform parent, Vector3 bottom, Vector3 top, bool alongZ, Material mat,
            int count = 20, float baseY = float.NaN, float thickness = 0f, bool fill = false, bool visual = true)
        {
            float floor = float.IsNaN(baseY) ? bottom.y : baseY;
            for (int i = 0; i < count; i++)
            {
                float t0 = (float)i / count, t1 = (float)(i + 1) / count;
                float y = Mathf.Lerp(bottom.y, top.y, t1);
                float y0 = thickness > 0f ? y - thickness : floor;
                Vector3 a = alongZ
                    ? new Vector3(bottom.x, y0, Mathf.Lerp(bottom.z, top.z, t0))
                    : new Vector3(Mathf.Lerp(bottom.x, top.x, t0), y0, bottom.z);
                Vector3 b = alongZ
                    ? new Vector3(top.x, y, Mathf.Lerp(bottom.z, top.z, t1))
                    : new Vector3(Mathf.Lerp(bottom.x, top.x, t1), y, top.z);
                if (visual)
                    Box(name, parent, Vector3.Min(a, b), Vector3.Max(a, b), mat, false);
                float yFill = Mathf.Lerp(bottom.y, top.y, t0);
                if (fill && yFill - floor > 0.01f)
                {
                    a.y = floor;
                    b.y = yFill;
                    ColliderBox(name + "Fill", parent, Vector3.Min(a, b), Vector3.Max(a, b));
                }
            }
        }

        // 보이지 않는 상자 콜라이더 (부모 좌표 최소·최대 모서리) — 맵을 합칠 때 부모 콜라이더로 모인다
        static void ColliderBox(string name, Transform parent, Vector3 min, Vector3 max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = (min + max) * 0.5f;
            go.AddComponent<BoxCollider>().size = max - min;
        }

        // 계단 위를 덮는 보이지 않는 경사로 — VR 로 오를 때 단마다 덜컹거리지 않게
        static void Ramp(string name, Transform parent, Vector3 bottom, Vector3 top, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Vector3 dir = top - bottom;
            Quaternion rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            go.transform.localRotation = rotation;
            // 상자 윗면이 계단 모서리 선과 맞게 두께의 절반만큼 내린다
            go.transform.localPosition = (bottom + top) * 0.5f - rotation * Vector3.up * 0.05f;
            go.AddComponent<BoxCollider>().size = new Vector3(width, 0.1f, dir.magnitude);
        }

        // ---------------------------------------------------------------- 재질

        static Materials LoadOrCreateMaterials()
        {
            if (!AssetDatabase.IsValidFolder(k_MaterialFolder))
                AssetDatabase.CreateFolder("Assets/C/Materials", "Greybox");
            Texture2D grid = GridTexture();
            // 중앙선 없는 아스팔트 — CarRoad 에서 중앙선 띠만 잘라낸 것 (Textures/Map/Source~/make_plain_asphalt.ps1). 맵 좌표 UV, 한 장 6 m.
            // 바탕 땅(공터)·주차장·교차로 칸이 같이 쓴다 (사용자 11/05) — 같은 재질·같은 맵 좌표라 서로 맞닿아도 무늬가 이어진다
            Material asphalt = MapMat("GB_Asphalt", "Asphalt", Vector2.one * 6f, 0f, 0.25f, grid, new Color(0.36f, 0.36f, 0.38f));
            var materials = new Materials
            {
                ground = asphalt,
                // 길 텍스처 (사용자 11/05) — 인도: 한국식 보도블록(Textures/Map/Paver, 2 m 한 장 — make_korean_paver.py), 샛길: 자갈 콘크리트(Alley, Gravel043)
                // 차도(CarRoad, Road009B)는 노란 중앙선이 든 도로 띠라 바탕 땅 전체가 아니라 도로마다 방향을 맞춘 노면(RoadSurface)에 — 그래서 UV 는 노면이 직접 정하고 Tiling 1
                sidewalk = MapMat("GB_Sidewalk_Paver", "Paver", Vector2.one * 2f, 0f, 0.3f, grid, new Color(0.64f, 0.62f, 0.58f)),
                alley = MapMat("GB_Alley_Gravel", "Alley", Vector2.one * 1.5f, 0f, 0.15f, grid, new Color(0.62f, 0.47f, 0.4f)),
                road = MapMat("GB_Road", "CarRoad", Vector2.one, 0f, 0.25f, grid, new Color(0.36f, 0.36f, 0.38f)),
                asphalt = asphalt,
                lot = asphalt,
                // 실내 천장(바닥판 밑면) — 다른 천장(막힌 위층 밑면 = GB_Ext_Interior)과 같은 흰 미장 (사용자 11/18: N31 천장이 격자였다)
                interior = MapMatOverGrid("GB_Interior", "Stucco1", Vector2.one * 2.4f, 0f, 0.2f, grid, new Color(0.82f, 0.8f, 0.75f), new Color(0.98f, 0.97f, 0.94f)),
                walls = new[]
                {
                    Mat("GB_Wall_A", new Color(0.8f, 0.8f, 0.8f), grid),
                    Mat("GB_Wall_B", new Color(0.7f, 0.73f, 0.78f), grid),
                    Mat("GB_Wall_C", new Color(0.78f, 0.73f, 0.66f), grid),
                },
                glass = Mat("GB_Glass", new Color(0.17f, 0.21f, 0.25f), null, 0.8f),
                sign = Mat("GB_Sign", new Color(0.55f, 0.58f, 0.62f), null),
                dark = Mat("GB_Dark", new Color(0.22f, 0.22f, 0.24f), null),
                // 실내 가구(엄폐물) 상자 — 모델을 씌우는 가구는 상자가 안 보이고, 모델이 없는 칸막이(1.6 × 1.5 m)만 이 재질로 보인다 → 나무판 (11/18)
                furniture = MapMatOverGrid("GB_Furniture", "Interior1", Vector2.one, 0f, 0.3f, grid, new Color(0.52f, 0.43f, 0.34f)),
                underground = Mat("GB_Underground", new Color(0.6f, 0.63f, 0.67f), grid),   // 지하 벽·천장·기둥 — 콘크리트 회청색
                barricade = Mat("GB_Barricade", new Color(0.74f, 0.7f, 0.58f), grid),       // 지하 바리케이드 — 모래색 콘크리트(벽·가판대와 구분)
                ugFloor = MapMat("GB_UgFloor_Granite", "UnderGround", Vector2.one * 2f, 0f, 0.55f, grid, new Color(0.82f, 0.8f, 0.75f)),   // 지하 바닥 — 화강암 (11/05)
                floors = FloorMaterials(grid),
                windows = WindowMaterial(grid),
                // 청운백화점 세라믹 외벽 (사용자 11/06 — 목업 C 안). 블록 한 장 3.648 × 3.696 m 는 기단 위 띠 윗면부터, 화강석 2.4 m 는 인도 높이부터
                facadeCeramic = MapMat("GB_Facade_Ceramic", "Ceramic1", new Vector2(3.648f, 3.696f), FacadeBlockStart, 0.8f, grid, new Color(0.91f, 0.85f, 0.7f)),
                facadeGranite = MapMat("GB_Facade_Granite", "GraniteDark", new Vector2(2.4f, 2.4f), k_Curb, 0.8f, grid, new Color(0.2f, 0.2f, 0.21f)),
                facadeIvory = FacadeMat("GB_Facade_Ivory", new Color(238 / 255f, 228 / 255f, 201 / 255f), 0.78f),
                facadeTeal = FacadeMat("GB_Facade_Teal", new Color(22 / 255f, 98 / 255f, 92 / 255f), 0.82f),
                facadePanel = FacadeMat("GB_Facade_PanelTeal", new Color(14 / 255f, 70 / 255f, 66 / 255f), 0.75f),
                facadeGold = FacadeMat("GB_Facade_Gold", new Color(198 / 255f, 158 / 255f, 90 / 255f), 0.72f, 1f),
                facadeBronze = FacadeMat("GB_Facade_Bronze", new Color(100 / 255f, 74 / 255f, 50 / 255f), 0.55f, 0.9f),
                // 청운백화점 실내 (사용자 11/06 — 목업 B 원목 + 무빙워크는 C). 벽 한 장 = 가로 2.4 m × 한 층 3.4 m, 아래 끝이 층 바닥(인도 높이부터 층마다)
                deptWall = MapMat("GB_Dept_Wall", "DeptWall1", new Vector2(2.4f, k_FloorHeight), k_Curb, 0.85f, grid, new Color(0.9f, 0.85f, 0.76f)),
                deptCeiling = FacadeMat("GB_Dept_Ceiling", new Color(236 / 255f, 226 / 255f, 206 / 255f), 0.3f),
                deptWood = MapMat("GB_Dept_Walnut", "Interior2", Vector2.one * 2.4f, 0f, 0.45f, grid, new Color(0.36f, 0.24f, 0.16f)),
                deptBlack = MapMat("GB_Dept_BlackMarble", "MarbleBlack", Vector2.one * 1.4f, 0f, 0.88f, grid, new Color(0.08f, 0.08f, 0.09f)),
                deptWhite = MapMat("GB_Dept_WhiteMarble", "Marble", Vector2.one * 1.8f, 0f, 0.88f, grid, new Color(0.92f, 0.91f, 0.89f)),
                deptPallet = MapMat("GB_Dept_Pallet", "Walkway1", new Vector2(0.8f, 0.408f), 0f, 0.6f, grid, new Color(0.6f, 0.61f, 0.62f)),
                // 11/16 사용자: 알파 0.16 은 거의 안 보여 난간이 뚫린 것처럼 · 투명벽처럼 보였다 → 연한 청록 유리로 보이게 (0.5).
                // TransparentMat 은 있는 재질을 그대로 쓰니 GB_Dept_Glass.mat 도 같은 값으로 바꿔 두었다
                deptGlass = TransparentMat("GB_Dept_Glass", new Color(0.64f, 0.82f, 0.85f, 0.5f), 0.95f),
                deptStainless = FacadeMat("GB_Dept_Stainless", new Color(150 / 255f, 152 / 255f, 156 / 255f), 0.8f, 1f),
                deptYellow = FacadeMat("GB_Dept_Yellow", new Color(250 / 255f, 196 / 255f, 20 / 255f), 0.5f),
                deptRubber = FacadeMat("GB_Dept_Handrail", new Color(18 / 255f, 18 / 255f, 20 / 255f), 0.55f),
                deptLight = EmissiveMat("GB_Dept_Downlight", new Color(1f, 0.93f, 0.8f), 3f),
                deptLaylight = EmissiveMat("GB_Dept_Laylight", new Color(1f, 0.95f, 0.86f), 1.6f),
            };
            // 일반 건물 바깥벽 마감 · 실내 미장 · 옥상 · 때 자국, 막힌 층 창 띠에 큰 얼룩층 (사용자 11/06 — UrbanGreyboxBuilder.Exterior.cs)
            ExteriorMaterials(materials, grid);
            // 설비 — 옥상 실외기 · 물탱크 · 안테나, 벽 배수관 · 가스관 · 벽걸이 실외기 (사용자 11/06 — UrbanGreyboxBuilder.Utilities.cs)
            UtilityMaterials(materials, grid);
            return materials;
        }

        // 실내 바닥 재질 GB_Floor_1~5 — 텍스처는 Textures/Map/Interior1~5
        static Material[] FloorMaterials(Texture2D grid)
        {
            var floors = new Material[k_Floors.Length];
            for (int i = 0; i < floors.Length; i++)
            {
                int number = i + 1;
                floors[i] = MapMat($"GB_Floor_{number}", $"Interior{number}", Vector2.one * k_Floors[i].meters, 0f, k_Floors[i].smoothness,
                    grid, new Color(0.82f, 0.8f, 0.75f), k_Floors[i].tint);
            }
            return floors;
        }

        // 막힌 위층 창 띠(Windows_nF) 재질 GB_Window_1 — 일반 건물 띠창 (11/03). 텍스처 한 장 = 가로 8칸 · 4개 층이라
        // 세로는 층 높이에 맞춰 인도 높이(1층 바닥)부터 센다 — Textures/Map/Source~/make_window_band.py
        static Material WindowMaterial(Texture2D grid)
        {
            return MapMat("GB_Window_1", "Window1", new Vector2(k_WindowTileWidth, 4f * k_FloorHeight), k_Curb, 0.9f, grid, new Color(0.17f, 0.21f, 0.25f));
        }

        // Textures/Map/<folder> 의 텍스처로 URP Lit 재질을 만든다 — 텍스처는 이름 끝(_Color · _NormalGL · _AmbientOcclusion · _MetallicSmoothness)으로 찾는다.
        // meters = 텍스처 한 장이 덮는 가로 · 세로 (m), startY = 텍스처 아래 끝이 오는 월드 높이 (m, 맵 UV 는 월드 좌표).
        // 다른 재질처럼 이미 있으면 그대로 둔다 — Inspector 에서 고친 값(Tiling 등)이 맵을 다시 만들어도 남는다.
        // 텍스처를 바꿨으면 그 재질을 지우고 맵을 다시 만든다. tint: 텍스처에 곱하는 색 (없으면 흰색 — 텍스처 그대로)
        static Material MapMat(string name, string folder, Vector2 meters, float startY, float smoothness, Texture2D grid, Color fallback, Color? tint = null)
        {
            string path = $"{k_MaterialFolder}/{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
                return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            ApplyMap(mat, name, folder, meters, startY, smoothness, grid, fallback, tint);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // 격자(그림 없는 임시 재질)로 남아 있던 재질에 그림을 입힌다 (사용자 11/18: 누락된 텍스처). 이미 그림이 있으면 그대로 — Inspector 에서 고친 값이 남는다
        static Material MapMatOverGrid(string name, string folder, Vector2 meters, float startY, float smoothness, Texture2D grid, Color fallback, Color? tint = null)
        {
            Material mat = MapMat(name, folder, meters, startY, smoothness, grid, fallback, tint);
            if (grid == null || mat.GetTexture("_BaseMap") != grid)
                return mat;
            ApplyMap(mat, name, folder, meters, startY, smoothness, grid, fallback, tint);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void ApplyMap(Material mat, string name, string folder, Vector2 meters, float startY, float smoothness, Texture2D grid, Color fallback, Color? tint)
        {
            folder = $"{k_FloorTextureFolder}/{folder}";
            Texture2D color = MapTexture(folder, "_Color", false, false);
            if (color == null)
                Debug.LogWarning($"[C] {folder} 에 _Color 텍스처가 없다 — {name} 는 격자로 둔다");
            mat.SetTexture("_BaseMap", color != null ? color : grid);
            mat.SetColor("_BaseColor", color != null ? tint ?? Color.white : fallback);
            // 이름 끝 — ambientCG 는 _NormalGL · _AmbientOcclusion, Blender 로 만든 것(보도블록)은 _Normal(OpenGL) · _AO
            mat.SetTexture("_BumpMap", MapTexture(folder, "_NormalGL", true, true) ?? MapTexture(folder, "_Normal", true, true));
            mat.SetTexture("_OcclusionMap", MapTexture(folder, "_AmbientOcclusion", false, true) ?? MapTexture(folder, "_AO", false, true));
            Texture2D smooth = MapTexture(folder, "_MetallicSmoothness", false, true);
            mat.SetTexture("_MetallicGlossMap", smooth);
            // 매끄러움 텍스처가 있으면 슬라이더는 곱하는 값이라 1
            mat.SetFloat("_Smoothness", smooth != null ? 1f : smoothness);
            var scale = new Vector2(1f / meters.x, 1f / meters.y);
            mat.SetTextureScale("_BaseMap", scale);
            mat.SetTextureOffset("_BaseMap", new Vector2(0f, -startY * scale.y));
            BaseShaderGUI.SetMaterialKeywords(mat);
        }

        // 폴더에서 이름이 suffix 로 끝나는 텍스처 (없으면 null). 노멀은 Normal map, 색이 아닌 값은 sRGB 를 끄고 가져온다
        static Texture2D MapTexture(string folder, string suffix, bool normal, bool linear)
        {
            if (!AssetDatabase.IsValidFolder(folder))
                return null;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!Path.GetFileNameWithoutExtension(path).EndsWith(suffix))
                    continue;
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                bool reimport = false;
                if (normal && importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    reimport = true;
                }
                if (linear && importer.sRGBTexture)
                {
                    importer.sRGBTexture = false;
                    reimport = true;
                }
                if (reimport)
                    importer.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            return null;
        }

        static Material Mat(string name, Color color, Texture2D grid, float smoothness = 0.15f)
        {
            string path = $"{k_MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
                return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            if (grid != null)
                mat.SetTexture("_BaseMap", grid);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // 1m 한 칸 격자: 칸 테두리는 진하게, 0.5m 선은 옅게 (흰 바탕 — 색은 재질에서 곱한다)
        static Texture2D GridTexture()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(k_GridTexture);
            if (tex != null)
                return tex;
            const int size = 256;
            var img = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool edge = x < 3 || y < 3 || x >= size - 3 || y >= size - 3;
                    bool half = Mathf.Abs(x - size / 2) < 1 || Mathf.Abs(y - size / 2) < 1;
                    float v = edge ? 0.6f : half ? 0.82f : 1f;
                    img.SetPixel(x, y, new Color(v, v, v, 1f));
                }
            }
            File.WriteAllBytes(k_GridTexture, img.EncodeToPNG());
            Object.DestroyImmediate(img);
            AssetDatabase.ImportAsset(k_GridTexture);
            var importer = (TextureImporter)AssetImporter.GetAtPath(k_GridTexture);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 8; // 비스듬히 보는 바닥에서도 격자가 뭉개지지 않게
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(k_GridTexture);
        }
    }
}
