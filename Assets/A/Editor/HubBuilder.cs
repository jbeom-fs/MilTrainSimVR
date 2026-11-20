using System.Collections.Generic;
using MilTrainSimVR.A.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MilTrainSimVR.A.EditorTools
{
    // 허브 맵 (11/09) — 예비군 교장 연병장. 연병장 가운데에 과정별 차원 포탈이 한 줄로 선다.
    // (사용자 11/09: 실외 연병장 · ㄷ자 건물 · 문 대신 포탈 — 참고 그림: 볼록 렌즈 원판, 가장자리가 파랗게 물듦(링 없음), 위에 이름.
    //  렌즈 그림은 사용자가 나중에 넣는다. 건물에는 글씨 없음.)
    //   연병장 40 × 30 m (흙, x ±20 · z ±15), 둘레 아스팔트. 남쪽(-z) 정문 앞이 시작 자리, 북쪽 가장자리 가운데 사열대.
    //   건물(장식, 입구 아님)이 연병장을 ㄷ자로 둘러싼다 — 앞면 북쪽 z +23 · 서쪽 x -28 · 동쪽 x +28.
    //   포탈: z k_PortalZ 줄에 k_PortalSpacing 간격, 시작 자리를 본다. 포탈 = 렌즈(그림) + 가장자리 물듦 + 반짝이 + 이름 + HubPortal + 돌아오는 자리.
    //   과정을 늘릴 때는 k_Portals 에 한 줄 더 쓰고 다시 누르거나, 씬에서 Portal_* 하나를 복제해 HubPortal 값만 바꾼다.
    //   렌즈 재질(Materials/Hub/Portals/Hub_PortalLens_<과정 ID>)은 없을 때만 만든다 — 넣은 그림은 다시 눌러도 남는다.
    // 치수는 제안값 — 렌더 보며 사용자가 조정. 다시 누르면 Hub 아래를 지우고 다시 짓는다 (Points/Point_HubStart 도).
    static class HubBuilder
    {
        const string k_Root = "Hub";
        const string k_MaterialFolder = "Assets/A/Materials/Hub";
        const string k_PortalFolder = k_MaterialFolder + "/Portals";
        const string k_MeshFolder = "Assets/A/Models/Hub";
        const string k_OutdoorFolder = "Assets/A/Materials/Outdoor";
        const string k_DoorModelPath = "Assets/A/Models/DoubleDoor/DoubleDoor.fbx";
        const string k_SkyPath = "Assets/A/Skyboxes/Terrestrial/SBS Terra 4/Medium/Skybox_Terra 4 Medium.mat";

        const float k_FieldHalfX = 20f, k_FieldHalfZ = 15f;
        const float k_NorthFront = 23f, k_SideFront = 28f;
        const float k_GateZ = -24f;
        static readonly Vector3 k_BuildingSize = new Vector3(14f, 7.2f, 10f);   // 폭 · 높이(2층) · 깊이
        const float k_DoorHeight = 2.284f;                                       // DoubleDoor 모델 (RangeDoorBuilder)

        // 건물 (장식) = (이름, 앞면 가운데, 앞이 보는 방향 yaw)
        static readonly (string name, Vector3 front, float yaw)[] k_Buildings =
        {
            ("North_W", new Vector3(-15f, 0f, k_NorthFront), 180f),
            ("North_C", new Vector3(0f, 0f, k_NorthFront + 4f), 180f),   // 사열대 뒤 본부
            ("North_E", new Vector3(15f, 0f, k_NorthFront), 180f),
            ("West_N", new Vector3(-k_SideFront, 0f, 7f), 90f),
            ("West_S", new Vector3(-k_SideFront, 0f, -9f), 90f),
            ("East_N", new Vector3(k_SideFront, 0f, 7f), 270f),
            ("East_S", new Vector3(k_SideFront, 0f, -9f), 270f),
        };

        // 포탈 = (과정 ID, 이름, 씬 경로, 그림 넣기 전 렌즈 색). 왼쪽부터 순서대로 선다
        static readonly (string courseId, string label, string scene, Color color)[] k_Portals =
        {
            ("A_Assembly", "분해·결합", "Assets/A/Scenes/A_Sandbox.unity", new Color(0.35f, 0.55f, 0.30f)),
            ("A_Range", "사로 사격", "Assets/A/Scenes/A_Sandbox.unity", new Color(0.70f, 0.45f, 0.20f)),
            ("B_FirstAid", "구급법", "Assets/B/Scene/B_FirstAid.unity", new Color(0.75f, 0.25f, 0.25f)),
            ("B_CBRN", "화생방", "Assets/B/Scene/B_CBRN.unity", new Color(0.55f, 0.60f, 0.25f)),
            ("C_Urban", "시가지 전투", "Assets/C/Scenes/C_UrbanBlock.unity", new Color(0.35f, 0.40f, 0.60f)),
        };
        const float k_PortalZ = 7f, k_PortalSpacing = 5f;   // 11/09 사용자: 구령대 쪽으로 (0 → 7, 구령대 앞 끝 z 16.2)
        const float k_LensHalfWidth = 0.95f, k_LensHalfHeight = 1.2f, k_LensBulge = 0.18f, k_LensBottom = 0.5f;
        const float k_ReturnDistance = 2.5f;   // 돌아오는 자리 — 포탈 앞

        [MenuItem("A/만들기/허브 (연병장 · 과정 포탈)")]
        static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != CourseRoute.HubScenePath)
            {
                Debug.LogError($"[A] 허브는 {CourseRoute.HubScenePath} 를 열고 짓는다 (지금 {scene.path})");
                return;
            }
            Undo.SetCurrentGroupName("A: 허브");
            int group = Undo.GetCurrentGroup();

            var old = GameObject.Find(k_Root);
            if (old != null)
                Undo.DestroyObjectImmediate(old);
            var root = new GameObject(k_Root).transform;
            Undo.RegisterCreatedObjectUndo(root.gameObject, "A: 허브");

            var dirt = AssetDatabase.LoadAssetAtPath<Material>(k_OutdoorFolder + "/Dirt.mat");
            var field = Tiled("Hub_Field", dirt, new Vector2(k_FieldHalfX * 2f / 4f, k_FieldHalfZ * 2f / 4f), new Color(0.93f, 0.86f, 0.74f));
            var ground = Tiled("Hub_Ground", dirt, new Vector2(400f / 4f, 400f / 4f), new Color(0.72f, 0.70f, 0.62f));
            var asphalt = ConcreteBlack();   // 연병장 둘레 · 정문 밖 길 = 검은 콘크리트 (11/09 사용자)
            var concrete = Outdoor("Outdoor_Concrete");
            var metal = Outdoor("Outdoor_Metal");
            var wall = AssetDatabase.LoadAssetAtPath<Material>("Assets/A/Models/Wall/Materials/Wall.mat");   // 건물 = 내무반 벽 (11/09 사용자)
            var roof = Outdoor("Outdoor_Roof");
            var window = Outdoor("Outdoor_Window");

            // 땅 · 아스팔트 · 연병장 · 연석
            var site = Group("Ground", root);
            Box("Base", site, new Vector3(0f, -0.06f, 0f), new Vector3(400f, 0.1f, 400f), ground).AddComponent<BoxCollider>();   // 걷는 바닥 (윗면 -0.01)
            WallUvBuilder.Apply(Box("Asphalt", site, new Vector3(0f, -0.005f, (k_GateZ + k_NorthFront) * 0.5f), new Vector3(k_SideFront * 2f, 0.01f, k_NorthFront - k_GateZ), asphalt).transform, asphalt);
            Box("Field", site, new Vector3(0f, 0.005f, 0f), new Vector3(k_FieldHalfX * 2f, 0.01f, k_FieldHalfZ * 2f), field);
            const float curb = 0.2f;
            Box("Curb_N", site, new Vector3(0f, 0.05f, k_FieldHalfZ + curb * 0.5f), new Vector3(k_FieldHalfX * 2f + curb * 2f, 0.1f, curb), concrete);
            Box("Curb_S", site, new Vector3(0f, 0.05f, -k_FieldHalfZ - curb * 0.5f), new Vector3(k_FieldHalfX * 2f + curb * 2f, 0.1f, curb), concrete);
            Box("Curb_W", site, new Vector3(-k_FieldHalfX - curb * 0.5f, 0.05f, 0f), new Vector3(curb, 0.1f, k_FieldHalfZ * 2f), concrete);
            Box("Curb_E", site, new Vector3(k_FieldHalfX + curb * 0.5f, 0.05f, 0f), new Vector3(curb, 0.1f, k_FieldHalfZ * 2f), concrete);

            // 건물 (장식)
            var doorModel = AssetDatabase.LoadAssetAtPath<GameObject>(k_DoorModelPath);
            Material doorMaterial = doorModel != null ? PaintedMaterial.Ensure("Assets/A/Models/DoubleDoor", "DoubleDoor") : null;
            var buildings = Group("Buildings", root);
            foreach (var b in k_Buildings)
                Building(buildings, b, wall, roof, window, doorModel, doorMaterial);
            ConnectNorth(buildings, wall, roof);
            AirConditioners(buildings, concrete);

            // 포탈
            var portals = Group("Portals", root);
            var fader = Object.FindFirstObjectByType<ScreenFader>();
            Mesh lens = LensMesh();
            var edgeMaterial = EdgeMaterial();
            float x0 = -(k_Portals.Length - 1) * k_PortalSpacing * 0.5f;
            for (int i = 0; i < k_Portals.Length; i++)
                Portal(portals, k_Portals[i], new Vector3(x0 + i * k_PortalSpacing, 0f, k_PortalZ), lens, edgeMaterial, fader);

            ReviewStand(root, concrete, metal, roof);
            Gate(root, concrete, metal, asphalt, BrickMaterial());
            Trees(root);
            Mountains(root);
            RingMountains(root);
            Avenue(root);
            MoveBounds(root);

            // 시작 자리 — 정문 안쪽, 연병장을 본다
            var points = GameObject.Find("Points");
            if (points == null)
            {
                points = new GameObject("Points");
                Undo.RegisterCreatedObjectUndo(points, "A: 허브");
            }
            Transform start = points.transform.Find("Point_HubStart");
            if (start == null)
            {
                start = new GameObject("Point_HubStart").transform;
                Undo.RegisterCreatedObjectUndo(start.gameObject, "A: 허브");
                start.SetParent(points.transform, false);
            }
            start.SetPositionAndRotation(new Vector3(0f, 0f, -k_FieldHalfZ - 4f), Quaternion.identity);

            var hubStart = Object.FindFirstObjectByType<HubStart>();
            if (hubStart == null)
                hubStart = Undo.AddComponent<HubStart>(points);
            var so = new SerializedObject(hubStart);
            so.FindProperty("m_Start").objectReferenceValue = start;
            so.ApplyModifiedPropertiesWithoutUndo();

            Sky();

            foreach (var t in root.GetComponentsInChildren<Transform>())
                if (t.GetComponentInParent<HubPortal>() == null)
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[A] 허브: 연병장 {k_FieldHalfX * 2f} × {k_FieldHalfZ * 2f} m · 건물 {k_Buildings.Length} · 포탈 {k_Portals.Length} · 시작 {start.position}");
        }

        // 검은 콘크리트 (11/09 사용자 Models/Concrete_Black — Tile_DefaultMaterial_* 3장) → Materials/Concrete_Black.mat.
        // 바닥 상자는 WallUvBuilder 로 m 단위 UV(1 = 4 m) — 무늬 크기가 건물 벽(내무반 벽 재질)과 같다. A_Sandbox 길(OutdoorBuilder)도 쓴다
        internal static Material ConcreteBlack() => PaintedMaterial.Ensure("Assets/A/Models/Concrete_Black", "Tile", "Concrete_Black");

        // ── 벽돌 (11/09) ──
        // 사용자가 준 표면 텍스처(Models/Brick/Tile_DefaultMaterial_* — 붉은 흙 표면, 줄눈 없음)에 벽돌 쌓기 무늬(길이 쌓기, 줄마다 반 장 엇갈림)를 얹어
        // 새 텍스처 두 장을 만든다: Brick_Albedo(벽돌마다 색 조금씩 다르게 · 회색 줄눈) · Brick_Normal(줄눈이 들어가게 + 원래 표면 요철).
        // 텍스처 한 장 = 1.05 m × 1.05 m = 벽돌 5장 × 16줄 (한 칸 210 × 66 mm — 표준 벽돌 190 × 57 + 줄눈 약 10 mm).
        // 원본 세 장은 그대로 둔다. 다시 만들려면 Brick_Albedo.png 를 지우고 메뉴를 누른다
        const string k_BrickFolder = "Assets/A/Models/Brick";
        const float k_BrickTileMeters = 1.05f;
        const int k_BrickColumns = 5, k_BrickRows = 16, k_BrickTexture = 1024;

        static Material BrickMaterial()
        {
            string albedoPath = $"{k_BrickFolder}/Brick_Albedo.png", normalPath = $"{k_BrickFolder}/Brick_Normal.png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath) == null || AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath) == null)
                BuildBrickTextures(albedoPath, normalPath);

            string materialFolder = k_BrickFolder + "/Materials";
            if (!AssetDatabase.IsValidFolder(materialFolder))
                AssetDatabase.CreateFolder(k_BrickFolder, "Materials");
            string path = materialFolder + "/Brick.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Brick" };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath));
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
            material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP");
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.12f);
            // 벽 메시 UV 는 1 = 4 m (WallUvBuilder · 겉벽 메시) → 텍스처 한 장이 1.05 m 가 되게
            var tiling = Vector2.one * (k_FacadeTile / k_BrickTileMeters);
            material.SetTextureScale("_BaseMap", tiling);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void BuildBrickTextures(string albedoPath, string normalPath)
        {
            Texture2D Load(string file)
            {
                var t = new Texture2D(2, 2);
                t.LoadImage(System.IO.File.ReadAllBytes($"{k_BrickFolder}/{file}"));
                return t;
            }
            var surface = Load("Tile_DefaultMaterial_AlbedoTransparency.png");
            var surfaceNormal = Load("Tile_DefaultMaterial_Normal.png");
            int size = k_BrickTexture;
            float cellW = (float)size / k_BrickColumns, cellH = (float)size / k_BrickRows;
            float mortar = cellH * 10f / 66f;   // 줄눈 약 10 mm
            const float bevel = 3f;              // 벽돌 모서리 둥글게 (px)
            var mortarColor = new Color(0.63f, 0.61f, 0.58f);

            // 높이: 벽돌 1 · 줄눈 0, 모서리에서 bevel 만큼 부드럽게
            var height = new float[size * size];
            var albedo = new Color[size * size];
            var rng = new System.Random(1109);
            var tints = new Color[k_BrickRows * k_BrickColumns];
            for (int i = 0; i < tints.Length; i++)
            {
                float b = 0.82f + 0.3f * (float)rng.NextDouble();   // 밝기
                float r = 0.95f + 0.1f * (float)rng.NextDouble();   // 붉은 정도
                tints[i] = new Color(b * r, b * 0.97f, b * (1.05f - (r - 0.95f)));
            }
            for (int y = 0; y < size; y++)
            {
                int row = Mathf.FloorToInt(y / cellH);
                float inRowY = y - row * cellH;
                float offset = (row % 2) * cellW * 0.5f;   // 줄마다 반 장 엇갈림
                for (int x = 0; x < size; x++)
                {
                    float sx = Mathf.Repeat(x + offset, size);
                    int col = Mathf.FloorToInt(sx / cellW);
                    float inColX = sx - col * cellW;
                    // 줄눈은 칸의 아래 · 왼쪽 가장자리. 벽돌 안쪽까지 거리
                    float dx = Mathf.Min(inColX - mortar, cellW - inColX);
                    float dy = Mathf.Min(inRowY - mortar, cellH - inRowY);
                    float d = Mathf.Min(dx, dy);
                    float h = Mathf.Clamp01(d / bevel);
                    height[y * size + x] = h;
                    Color baseColor = surface.GetPixelBilinear((x + 0.5f) / size, (y + 0.5f) / size);
                    Color brick = baseColor * tints[(row % k_BrickRows) * k_BrickColumns + col % k_BrickColumns];
                    brick *= Mathf.Lerp(0.8f, 1f, h);   // 모서리 살짝 어둡게
                    float grain = baseColor.grayscale;
                    Color m = mortarColor * Mathf.Lerp(0.88f, 1.08f, grain * 1.6f);
                    Color c = Color.Lerp(m, brick, Mathf.SmoothStep(0f, 1f, h));
                    c.a = 1f;
                    albedo[y * size + x] = c;
                }
            }
            var albedoTex = new Texture2D(size, size, TextureFormat.RGB24, false);
            albedoTex.SetPixels(albedo);
            System.IO.File.WriteAllBytes(albedoPath, albedoTex.EncodeToPNG());

            // 노멀: 높이의 기울기(줄눈 홈) + 원래 표면 노멀
            var normal = new Color[size * size];
            const float strength = 2.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float hl = height[y * size + (x + size - 1) % size], hr = height[y * size + (x + 1) % size];
                    float hd = height[((y + size - 1) % size) * size + x], hu = height[((y + 1) % size) * size + x];
                    Color sn = surfaceNormal.GetPixelBilinear((x + 0.5f) / size, (y + 0.5f) / size);
                    var n = new Vector3((hl - hr) * strength + (sn.r * 2f - 1f), (hd - hu) * strength + (sn.g * 2f - 1f), 1f).normalized;
                    normal[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            var normalTex = new Texture2D(size, size, TextureFormat.RGB24, false);
            normalTex.SetPixels(normal);
            System.IO.File.WriteAllBytes(normalPath, normalTex.EncodeToPNG());

            Object.DestroyImmediate(surface);
            Object.DestroyImmediate(surfaceNormal);
            Object.DestroyImmediate(albedoTex);
            Object.DestroyImmediate(normalTex);
            AssetDatabase.ImportAsset(albedoPath);
            AssetDatabase.ImportAsset(normalPath);
            if (AssetImporter.GetAtPath(normalPath) is TextureImporter ti)
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.SaveAndReimport();
            }
        }

        // ── 에어컨 실외기 (11/09 사용자 Models/Outdoor_AC — 0.85 × 0.61 × 0.38 m, 원점 = 아래 가운데, 팬(앞) = +Z) ──
        // 건물마다 연병장에서 덜 보이는 옆면(또는 뒷면) 창 사이 가운데에 두 대: 1층은 바닥 콘크리트 받침 위, 2층은 벽 철 받침대(앵글) 위.
        // 실외기 뒤와 벽 사이 0.2 m, 냉매 배관(흰 보온재)이 실외기 옆에서 벽으로 들어간다. 북쪽 ㄷ자 건물: 날개는 바깥 옆면, 가운데 동은 뒷면.
        const string k_AcModelPath = "Assets/A/Models/Outdoor_AC/Outdoor_AC_v01.fbx";
        const float k_AcWallGap = 0.2f, k_AcWallHeight = 4.0f;

        // 건물 이름 → (붙일 면, 면 위 가로 위치들). 면: +1 = 오른쪽 옆면(건물 +x), -1 = 왼쪽 옆면, 0 = 뒷면
        static readonly (string building, int face, float[] offsets)[] k_AcPlaces =
        {
            ("North_W", 1, new[] { 0f }),      // yaw 180 → 건물 +x = 월드 -x (바깥쪽)
            ("North_E", -1, new[] { 0f }),     // 건물 -x = 월드 +x (바깥쪽)
            ("North_C", 0, new[] { -3.5f, 3.5f }),
            ("West_N", -1, new[] { 0f }),      // yaw 90 → 건물 -x = 월드 +z (북쪽)
            ("West_S", 1, new[] { 0f }),       // 건물 +x = 월드 -z (남쪽 담장 쪽)
            ("East_N", 1, new[] { 0f }),       // yaw 270 → 건물 +x = 월드 +z (북쪽)
            ("East_S", -1, new[] { 0f }),      // 건물 -x = 월드 -z (남쪽)
        };

        static void AirConditioners(Transform buildings, Material concrete)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(k_AcModelPath);
            if (model == null)
            {
                Debug.LogWarning($"[A] {k_AcModelPath} 이 없다 — 실외기 없이 짓는다");
                return;
            }
            Material material = AcMaterial();
            var steel = Lit("Hub_Steel", new Color(0.72f, 0.74f, 0.76f), 0.75f, 0.9f);
            var insulation = Lit("Hub_PipeInsulation", new Color(0.86f, 0.86f, 0.83f), 0.2f, 0f);
            Vector3 s = k_BuildingSize;
            float sideCenter = -(s.z + k_WindowDepth) * 0.5f;
            foreach (var (name, face, offsets) in k_AcPlaces)
            {
                Transform b = buildings.Find("Building_" + name);
                if (b == null)
                    continue;
                // 면 기준 틀 — 원점 = 면 위 바닥, +Z = 바깥, +X = 바깥에서 볼 때 왼쪽
                Vector3 origin = face == 0 ? new Vector3(0f, 0f, -s.z) : new Vector3(face * s.x * 0.5f, 0f, sideCenter);
                float yaw = face == 0 ? 180f : face * 90f;
                var g = Group("AirConditioners", b);
                g.localPosition = origin;
                g.localRotation = Quaternion.Euler(0f, yaw, 0f);
                foreach (float x in offsets)
                {
                    // 1층 — 바닥 받침 위
                    Box("Pad", g, new Vector3(x, 0.04f, k_AcWallGap + 0.19f), new Vector3(1.05f, 0.08f, 0.6f), concrete);
                    AcUnit(g, model, material, insulation, new Vector3(x, 0.08f, k_AcWallGap + 0.19f));
                    // 2층 — 벽 받침대 위
                    float y = k_AcWallHeight;
                    foreach (float side in new[] { -0.3f, 0.3f })
                    {
                        Box("Bracket", g, new Vector3(x + side, y - 0.02f, 0.3f), new Vector3(0.04f, 0.04f, 0.6f), steel);              // 받침 앵글
                        Tube("Brace", g, new Vector3(x + side, y - 0.45f, 0.01f), new Vector3(x + side, y - 0.04f, 0.55f), 0.015f, steel); // 빗살
                    }
                    AcUnit(g, model, material, insulation, new Vector3(x, y, k_AcWallGap + 0.19f));
                }
            }
        }

        // 실외기 한 대 + 냉매 배관 (실외기 오른쪽 옆 → 벽). position = 실외기 바닥 가운데 (면 틀 기준)
        static void AcUnit(Transform parent, GameObject model, Material material, Material insulation, Vector3 position)
        {
            var unit = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            unit.name = "Outdoor_AC";
            unit.transform.localPosition = position;
            unit.transform.localRotation = Quaternion.identity;
            PaintedMaterial.Assign(unit, material);
            // 바깥에서 볼 때 실외기 오른쪽(-X) 옆면에서 나와 벽으로
            Vector3 side = position + new Vector3(-0.47f, 0.42f, -0.05f);
            Tube("Pipe", parent, side, new Vector3(side.x, side.y, 0f), 0.035f, insulation);
        }

        static Material AcMaterial() => TexturedLit("Assets/A/Models/Outdoor_AC", "T_Outdoor_AC", "Outdoor_AC");

        // 모델 재질 (실외기 · 트럭) — README 대로: BaseColor(sRGB) + Unity_MetallicSmoothness(R 금속 · A 매끈함, sRGB 끔), 매끈함 배율 1, 노멀 없음.
        // 텍스처 = <folder>/<texturePrefix>_BaseColor.png · _Unity_MetallicSmoothness.png, 재질 = <folder>/Materials/<name>.mat
        static Material TexturedLit(string folder, string texturePrefix, string name)
        {
            string baseColor = $"{folder}/{texturePrefix}_BaseColor.png", metallic = $"{folder}/{texturePrefix}_Unity_MetallicSmoothness.png";
            if (AssetImporter.GetAtPath(metallic) is TextureImporter ti && ti.sRGBTexture)
            {
                ti.sRGBTexture = false;
                ti.SaveAndReimport();
            }
            if (!AssetDatabase.IsValidFolder(folder + "/Materials"))
                AssetDatabase.CreateFolder(folder, "Materials");
            string path = $"{folder}/Materials/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(baseColor));
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(metallic));
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", 1f);
            material.SetFloat("_SmoothnessTextureChannel", 0f);   // 금속 맵 알파
            EditorUtility.SetDirty(material);
            return material;
        }

        // 북쪽 세 동(North_W · North_C · North_E)을 한 건물로 — 사이 틈(1 m)을 벽으로 메우고 지붕을 잇는다 (11/09 사용자: ㄷ자 건물).
        // 가운데 동이 4 m 뒤로 물러나 있어 연병장 쪽으로 열린 ㄷ자가 된다. 메우는 곳은 두 동이 앞뒤로 겹치는 구간(가운데 동 앞면 ~ 양 날개 뒷면)
        static void ConnectNorth(Transform parent, Material wall, Material roof)
        {
            Vector3 s = k_BuildingSize;
            Vector3 center = default, west = default;
            foreach (var b in k_Buildings)
            {
                if (b.name == "North_C") center = b.front;
                if (b.name == "North_W") west = b.front;
            }
            float gapNear = Mathf.Abs(center.x) + s.x * 0.5f;          // 가운데 동 옆면 (|x|)
            float gapFar = Mathf.Abs(west.x) - s.x * 0.5f;             // 날개 동 안쪽 옆면 (|x|)
            float z0 = Mathf.Max(center.z, west.z), z1 = Mathf.Min(center.z, west.z) + s.z;   // 앞뒤로 겹치는 구간 (북쪽 동은 앞면 z 에서 +z 로 깊다)
            if (gapFar <= gapNear || z1 <= z0)
                return;
            var g = Group("North_Connectors", parent);
            foreach (float sgn in new[] { -1f, 1f })
            {
                float x = sgn * (gapNear + gapFar) * 0.5f, width = gapFar - gapNear;
                var fill = Box(sgn < 0f ? "Connector_W" : "Connector_E", g, new Vector3(x, s.y * 0.5f, (z0 + z1) * 0.5f), new Vector3(width, s.y, z1 - z0), wall);
                fill.AddComponent<BoxCollider>();
                WallUvBuilder.Apply(fill.transform, wall);
                var connectorRoof = Box("Roof", g, new Vector3(x, s.y + 0.15f, (z0 + z1) * 0.5f), new Vector3(width + 0.6f, 0.3f, z1 - z0), roof);
                WallUvBuilder.Apply(connectorRoof.transform, RoofMaterial());
            }
        }

        // 건물 지붕 (11/10 사용자 Models/Roof — 주황빛 붉은 지붕 표면 Tile_DefaultMaterial_* 3장 → PaintedMaterial, m 단위 UV 한 장 4 m).
        // 건물 7동 · 북쪽 연결부 지붕만. 출입문 차양 · 구령대 · 위병소 지붕은 그대로 (Outdoor_Roof · 철)
        internal static Material RoofMaterial() => PaintedMaterial.Ensure("Assets/A/Models/Roof", "Tile", "Roof");

        // 건물 지붕 재질만 다시 입히기 — 허브 전체를 다시 짓지 않고 (11/10)
        [MenuItem("A/만들기/허브 건물 지붕 재질 (Roof)")]
        static void ApplyBuildingRoofs()
        {
            var buildings = GameObject.Find(k_Root + "/Buildings");
            if (buildings == null)
            {
                Debug.LogError($"[A] {k_Root}/Buildings 가 없다 — 허브 씬을 연다");
                return;
            }
            var material = RoofMaterial();
            int n = 0;
            foreach (var t in buildings.GetComponentsInChildren<Transform>())
                if (t.name == "Roof" && t.GetComponent<MeshRenderer>() != null)
                {
                    WallUvBuilder.Apply(t, material);
                    n++;
                }
            EditorSceneManager.MarkSceneDirty(buildings.scene);
            Debug.Log($"[A] 건물 지붕 {n}개 → Roof");
        }

        const float k_CanopyDepth = 0.7f;   // 문 위 차양이 벽에서 나온 길이 — 11/10 사용자: 1.4 → 절반

        static void Building(Transform parent, (string name, Vector3 front, float yaw) d,
            Material wall, Material roof, Material window, GameObject doorModel, Material doorMaterial)
        {
            var b = Group("Building_" + d.name, parent);
            b.position = d.front;                        // 원점 = 앞면 가운데 바닥, 앞(+Z) = 연병장 쪽
            b.rotation = Quaternion.Euler(0f, d.yaw, 0f);
            Vector3 s = k_BuildingSize;
            const float t = k_WindowDepth;   // 겉벽 두께 = 창 두께 → 창틀 앞면이 벽 면과 딱 맞는다 (11/09 사용자)

            // 속 상자 — 겉벽 안쪽. 뒷면 · 지붕 밑은 이것이 벽이다
            var body = Box("Body", b, new Vector3(0f, s.y * 0.5f, -(s.z + t) * 0.5f), new Vector3(s.x - t * 2f, s.y, s.z - t), wall);
            WallUvBuilder.Apply(body.transform, wall);
            var collider = b.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, s.y * 0.5f, -s.z * 0.5f);
            collider.size = s;
            var top = Box("Roof", b, new Vector3(0f, s.y + 0.15f, -s.z * 0.5f), new Vector3(s.x + 0.6f, 0.3f, s.z + 0.6f), roof);
            WallUvBuilder.Apply(top.transform, RoofMaterial());   // 11/10 사용자 Roof 텍스처 (차양은 roof 그대로)

            // 창 자리 — 앞면 3칸 × 2층(1층 가운데는 문), 옆면 2칸 × 2층 (옆면 칸은 옆 겉벽 가운데 기준 대칭)
            var front = new List<Rect>();
            int count = Mathf.FloorToInt((s.x - 2f) / 3.5f);
            float x0 = -(count - 1) * 3.5f * 0.5f;
            for (int i = 0; i < count; i++)
            {
                float x = x0 + i * 3.5f;
                if (Mathf.Abs(x) > 2f)
                    front.Add(WindowRect(x, k_Window1F));
                front.Add(WindowRect(x, k_Window2F));
            }
            var frontWindows = new List<Rect>(front);
            front.Add(new Rect(-k_DoorWidth * 0.5f, 0f, k_DoorWidth, k_DoorHeight));   // 문 구멍
            float sideWidth = s.z - t, sideCenter = -(s.z + t) * 0.5f;
            var side = new List<Rect>();
            foreach (float x in new[] { -k_SideWindowOffset, k_SideWindowOffset })
                foreach (float y in new[] { k_Window1F, k_Window2F })
                    side.Add(WindowRect(x, y));

            // 겉벽 — 창 · 문 자리만 비운 판 (내무반 벽 재질, m 단위 UV). 건물이 모두 같아 메시 두 개를 같이 쓴다
            Mesh frontMesh = FacadeMesh("Facade_Front", s.x, s.y, t, front);
            Mesh sideMesh = FacadeMesh("Facade_Side", sideWidth, s.y, t, side);
            Facade(b, "Facade_Front", frontMesh, wall, Vector3.zero, 0f);
            Facade(b, "Facade_Left", sideMesh, wall, new Vector3(-s.x * 0.5f, 0f, sideCenter), -90f);
            Facade(b, "Facade_Right", sideMesh, wall, new Vector3(s.x * 0.5f, 0f, sideCenter), 90f);

            // 창 = 내무반 창 (Prefabs/Window, 11/09 사용자) — 뒷면이 속 상자 면에, 앞면이 겉벽 면에
            var windowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_WindowPrefabPath);
            if (windowPrefab != null)
            {
                foreach (var r in frontWindows)
                    Window(b, windowPrefab, window, "Window_Front", new Vector3(r.center.x, r.yMin, -t), 0f);
                foreach (float sgn in new[] { -1f, 1f })
                    foreach (var r in side)
                    {
                        // 옆 겉벽 기준 x → 건물 z (오른쪽 yaw 90: 겉벽 +x = 건물 -z, 왼쪽 yaw -90: 겉벽 +x = 건물 +z)
                        float z = sideCenter - sgn * r.center.x;
                        Window(b, windowPrefab, window, "Window_Side", new Vector3(sgn * (s.x * 0.5f - t), r.yMin, z), sgn * 90f);
                    }
            }
            Box("Canopy", b, new Vector3(0f, k_DoorHeight + 0.25f, k_CanopyDepth * 0.5f), new Vector3(3.2f, 0.12f, k_CanopyDepth), roof);   // 문 위 차양 — 벽(z 0)에서 앞으로


            if (doorModel == null)
                return;
            var door = new GameObject("Door").transform;   // 장식 — 열리지 않는다. 겉벽 두께 가운데
            door.SetParent(b, false);
            door.localPosition = new Vector3(0f, 0f, -t * 0.5f);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(doorModel, door);
            model.name = "Model";
            PaintedMaterial.Assign(model, doorMaterial);
        }

        const float k_DoorWidth = 1.968f;                   // DoubleDoor 문틀 바깥 폭
        const float k_SideWindowOffset = 2.3f;              // 옆면 창 — 옆 겉벽 가운데에서 좌우

        static Rect WindowRect(float centerX, float bottom) =>
            new Rect(centerX - k_WindowWidth * 0.5f, bottom, k_WindowWidth, k_WindowHeight);

        static void Facade(Transform building, string name, Mesh mesh, Material material, Vector3 position, float yaw)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(building, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        // 겉벽 메시 — 폭 width (x ±width/2) · 높이 height (y 0 ~ height) · 두께 depth (z -depth ~ 0, 앞 = +z).
        // 구멍(openings, x · y 사각형)을 뺀 나머지를 가로 띠로 잘라 상자들로 채운다. UV = 위치(m) ÷ 4 m (내무반 벽 무늬 크기) — 상자 사이에 무늬가 이어진다
        const float k_FacadeTile = 4f;

        static Mesh FacadeMesh(string name, float width, float height, float depth, List<Rect> openings)
        {
            var ys = new SortedSet<float> { 0f, height };
            foreach (var o in openings) { ys.Add(o.yMin); ys.Add(o.yMax); }
            var cuts = new List<float>(ys);
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            float left = -width * 0.5f, right = width * 0.5f;
            for (int k = 0; k + 1 < cuts.Count; k++)
            {
                float y0 = cuts[k], y1 = cuts[k + 1];
                if (y1 - y0 < 1e-4f)
                    continue;
                var holes = openings.FindAll(o => o.yMin <= y0 + 1e-4f && o.yMax >= y1 - 1e-4f);
                holes.Sort((a, c) => a.xMin.CompareTo(c.xMin));
                float cursor = left;
                foreach (var h in holes)
                {
                    if (h.xMin > cursor + 1e-4f)
                        AddBox(new Vector3(cursor, y0, -depth), new Vector3(h.xMin, y1, 0f), vertices, normals, uvs, triangles);
                    cursor = Mathf.Max(cursor, h.xMax);
                }
                if (right > cursor + 1e-4f)
                    AddBox(new Vector3(cursor, y0, -depth), new Vector3(right, y1, 0f), vertices, normals, uvs, triangles);
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return SaveMesh(mesh);
        }

        static void AddBox(Vector3 min, Vector3 max, List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> tri)
        {
            // 면 = (법선, 원점, du = 바깥에서 볼 때 오른쪽, dv = 위) — 삼각형이 바깥에서 시계 방향
            void Face(Vector3 normal, Vector3 origin, Vector3 du, Vector3 dv, Vector2 uvOrigin, Vector2 uvU, Vector2 uvV)
            {
                int start = v.Count;
                v.Add(origin); v.Add(origin + du); v.Add(origin + du + dv); v.Add(origin + dv);
                for (int i = 0; i < 4; i++) n.Add(normal);
                uv.Add(uvOrigin / k_FacadeTile); uv.Add((uvOrigin + uvU) / k_FacadeTile);
                uv.Add((uvOrigin + uvU + uvV) / k_FacadeTile); uv.Add((uvOrigin + uvV) / k_FacadeTile);
                tri.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }
            Vector3 size = max - min;
            // 앞(+z): 바깥에서 보면 +x 가 왼쪽 → 원점을 오른쪽 아래(max.x)에서 -x 로
            Face(Vector3.forward, new Vector3(max.x, min.y, max.z), new Vector3(-size.x, 0, 0), new Vector3(0, size.y, 0),
                new Vector2(max.x, min.y), new Vector2(-size.x, 0), new Vector2(0, size.y));
            Face(Vector3.back, new Vector3(min.x, min.y, min.z), new Vector3(size.x, 0, 0), new Vector3(0, size.y, 0),
                new Vector2(min.x, min.y), new Vector2(size.x, 0), new Vector2(0, size.y));
            Face(Vector3.right, new Vector3(max.x, min.y, min.z), new Vector3(0, 0, size.z), new Vector3(0, size.y, 0),
                new Vector2(min.z, min.y), new Vector2(size.z, 0), new Vector2(0, size.y));
            Face(Vector3.left, new Vector3(min.x, min.y, max.z), new Vector3(0, 0, -size.z), new Vector3(0, size.y, 0),
                new Vector2(max.z, min.y), new Vector2(-size.z, 0), new Vector2(0, size.y));
            Face(Vector3.up, new Vector3(min.x, max.y, min.z), new Vector3(size.x, 0, 0), new Vector3(0, 0, size.z),
                new Vector2(min.x, min.z), new Vector2(size.x, 0), new Vector2(0, size.z));
            Face(Vector3.down, new Vector3(min.x, min.y, min.z), new Vector3(0, 0, size.z), new Vector3(size.x, 0, 0),
                new Vector2(min.x, min.z), new Vector2(0, size.z), new Vector2(size.x, 0));
        }

        // 창 아래 창턱 (11/09 사용자: 사진처럼 창마다, 처음엔 위 머리 띠도 있었다가 뺌) — 창보다 양옆 0.1 m 넓게, 벽 면에서 k_SillDepth 나옴
        const float k_SillDepth = 0.16f, k_SillHeight = 0.12f, k_TrimOverhang = 0.1f;   // 창턱 — 벽 면에서 0.16 m 나온다

        // 재질 = 내무반 바닥 테라조 타일 (11/09 사용자) — Floor.mat 복사본(타일링 1, 원본은 바닥 크기에 맞춘 13.3 배라 그대로 못 씀).
        // 무늬 크기는 내무반과 같게 타일 한 장 0.6 m (WallUvBuilder m 단위 UV)
        const float k_TerrazzoTile = 0.6f;

        static Material TrimMaterial()
        {
            string path = k_MaterialFolder + "/Hub_Trim_Terrazzo.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var floor = AssetDatabase.LoadAssetAtPath<Material>("Assets/A/Models/Floor/Materials/Floor.mat");
                if (floor == null)
                    return Lit("Hub_Trim", new Color(0.93f, 0.93f, 0.91f), 0.25f, 0f);
                EnsureFolder();
                material = new Material(floor) { name = "Hub_Trim_Terrazzo" };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTextureScale("_BaseMap", Vector2.one);
            EditorUtility.SetDirty(material);
            return material;
        }

        // 창 하나 — 원점 = 창 아래 가운데, 속 상자 면(facePoint)에 뒷면을 댄다 → 앞면이 겉벽 면과 맞는다. 유리 뒤에 어두운 판(방 안처럼)
        // yaw = 벽 면이 보는 방향 (0 = 건물 앞 +Z, ±90 = 옆)
        const string k_WindowPrefabPath = "Assets/A/Prefabs/Window.prefab";
        const float k_WindowScale = 1.3f;                   // 내무반과 같게 (1.69 × 1.3 m)
        const float k_WindowWidth = 1.69f, k_WindowHeight = 1.3f;
        const float k_WindowDepth = 0.17f, k_WindowCenterZ = 0.035f;   // 모델 두께 · 원점에서 두께 가운데까지
        const float k_Window1F = 1.0f, k_Window2F = 4.6f;   // 창 아래 끝 높이

        static void Window(Transform building, GameObject prefab, Material backing, string name, Vector3 facePoint, float yaw)
        {
            var w = new GameObject(name).transform;
            w.SetParent(building, false);
            w.localPosition = facePoint;
            w.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, w);
            instance.transform.localPosition = new Vector3(0f, 0f, k_WindowDepth * 0.5f - k_WindowCenterZ);   // 뒷면이 속 상자 면에 닿게
            instance.transform.localScale = new Vector3(k_WindowScale, k_WindowScale, 1f);
            Box("Backing", w, new Vector3(0f, k_WindowHeight * 0.5f, 0.006f), new Vector3(k_WindowWidth - 0.08f, k_WindowHeight - 0.08f, 0.01f), backing);
            // 창턱 · 머리 — 겉벽 면(창 앞면 = k_WindowDepth)에서 나온다
            // 창 아래 창턱만 (11/09 사용자: 위 머리 띠는 없애고 창턱은 바깥으로 더 나오게)
            float trimWidth = k_WindowWidth + k_TrimOverhang * 2f, trimZ = k_WindowDepth + k_SillDepth * 0.5f;
            var trim = TrimMaterial();
            WallUvBuilder.Apply(Box("Sill", w, new Vector3(0f, -k_SillHeight * 0.5f, trimZ), new Vector3(trimWidth, k_SillHeight, k_SillDepth), trim).transform, trim, k_TerrazzoTile);
        }

        // 포탈 (받침 없음, 사용자 11/09) — 원점 = 렌즈 아래 바닥, 앞(+Z) = 시작 자리 쪽(월드 -z)
        static void Portal(Transform parent, (string courseId, string label, string scene, Color color) d, Vector3 position,
            Mesh lensMesh, Material edgeMaterial, ScreenFader fader)
        {
            var p = Group("Portal_" + d.courseId, parent);
            p.position = position;
            p.rotation = Quaternion.Euler(0f, 180f, 0f);
            float centerY = k_LensBottom + k_LensHalfHeight;

            // 렌즈 — 앞뒤로 볼록한 타원 원판. 그림은 정면 평면 UV
            var lens = new GameObject("Lens", typeof(MeshFilter), typeof(MeshRenderer));
            lens.transform.SetParent(p, false);
            lens.transform.localPosition = new Vector3(0f, centerY, 0f);
            lens.GetComponent<MeshFilter>().sharedMesh = lensMesh;
            var lensRenderer = lens.GetComponent<MeshRenderer>();
            lensRenderer.sharedMaterial = LensMaterial(d.courseId, d.color);
            lensRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var lensCollider = lens.AddComponent<BoxCollider>();   // 레이로 고르기
            lensCollider.size = new Vector3(k_LensHalfWidth * 2f, k_LensHalfHeight * 2f, 0.2f);

            // 가장자리 파란 물듦 — 같은 렌즈 모양을 아주 조금 크게 겹쳐, 가운데는 투명 · 가장자리로 갈수록 파랗게 (링 없음, 사용자 11/09)
            var edge = new GameObject("EdgeTint", typeof(MeshFilter), typeof(MeshRenderer));
            edge.transform.SetParent(p, false);
            edge.transform.localPosition = lens.transform.localPosition;
            edge.transform.localScale = new Vector3(1.004f, 1.004f, 1.06f);
            edge.GetComponent<MeshFilter>().sharedMesh = lensMesh;
            var edgeRenderer = edge.GetComponent<MeshRenderer>();
            edgeRenderer.sharedMaterial = edgeMaterial;
            edgeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Sparkles(p, centerY);

            // 이름 — 렌즈 위
            var label = Group("Label", p);
            label.localPosition = new Vector3(0f, k_LensBottom + k_LensHalfHeight * 2f + 0.12f, 0f);   // 렌즈 바로 위 (11/09 사용자: 더 가깝게)
            // 어느 쪽에서 봐도 글씨가 바로 읽히게 — 이름판이 머리 쪽으로 돈다 (FaceHead, 11/09 사용자)
            label.gameObject.AddComponent<FaceHead>();
            var titles = new Text[1];
            var subs = new Text[1];
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            canvasGo.transform.SetParent(label, false);
            var rect = (RectTransform)canvasGo.transform;
            rect.localRotation = Quaternion.Euler(0f, 180f, 0f);   // 캔버스 앞면(-Z) → 이름판 앞(+Z)
            rect.sizeDelta = new Vector2(400f, 100f);
            rect.localScale = Vector3.one * 0.01f;
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            titles[0] = MakeText("Title", rect, font, 31, new Vector2(0f, 0.35f), new Vector2(1f, 1f), FontStyle.Bold);
            subs[0] = MakeText("Sub", rect, font, 17, new Vector2(0f, 0f), new Vector2(1f, 0.38f), FontStyle.Normal);
            titles[0].gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.6f);
            titles[0].text = d.label;
            subs[0].text = CourseRoute.CanLoad(d.scene) ? "" : "준비 중";

            var ret = new GameObject("ReturnPoint").transform;
            ret.SetParent(p, false);
            ret.localPosition = new Vector3(0f, 0f, k_ReturnDistance);   // 포탈 앞, 포탈을 등진다

            p.gameObject.AddComponent<XRSimpleInteractable>();
            var hubPortal = p.gameObject.AddComponent<HubPortal>();
            var so = new SerializedObject(hubPortal);
            so.FindProperty("m_CourseId").stringValue = d.courseId;
            so.FindProperty("m_Label").stringValue = d.label;
            so.FindProperty("m_ScenePath").stringValue = d.scene;
            so.FindProperty("m_ReturnPoint").objectReferenceValue = ret;
            var signs = so.FindProperty("m_Signs");
            var signSubs = so.FindProperty("m_SignSubs");
            signs.arraySize = signSubs.arraySize = titles.Length;
            for (int k = 0; k < titles.Length; k++)
            {
                signs.GetArrayElementAtIndex(k).objectReferenceValue = titles[k];
                signSubs.GetArrayElementAtIndex(k).objectReferenceValue = subs[k];
            }
            so.FindProperty("m_Fader").objectReferenceValue = fader;
            so.FindProperty("m_EnterCenter").vector3Value = new Vector3(0f, centerY, 0f);
            so.FindProperty("m_EnterSize").vector3Value = new Vector3(k_LensHalfWidth * 1.6f, k_LensHalfHeight * 2f, 0.9f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 테두리를 따라 떠도는 작은 파란 반짝이
        static void Sparkles(Transform portal, float centerY)
        {
            var go = new GameObject("Sparkles", typeof(ParticleSystem));
            go.transform.SetParent(portal, false);
            go.transform.localPosition = new Vector3(0f, centerY, 0f);
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.6f, 0.85f, 1f), new Color(0.3f, 0.6f, 1f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;
            var emission = ps.emission;
            emission.rateOverTime = 25f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = k_LensHalfWidth + 0.08f;
            shape.radiusThickness = 0.1f;
            shape.scale = new Vector3(1f, k_LensHalfHeight / k_LensHalfWidth, 1f);
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial =
                AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
        }

        // 렌즈 재질 — 과정마다 하나, 없을 때만 만든다 (Base Map 에 넣은 그림은 다시 눌러도 남는다). 빛 영향 없이 그림 그대로 (Unlit)
        static Material LensMaterial(string courseId, Color color)
        {
            string path = $"{k_PortalFolder}/Hub_PortalLens_{courseId}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            EnsureFolder();
            if (!AssetDatabase.IsValidFolder(k_PortalFolder))
                AssetDatabase.CreateFolder(k_MaterialFolder, "Portals");
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Hub_PortalLens_" + courseId };
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // 렌즈 메시 — 타원 원판, 앞(+Z) · 뒤(-Z) 모두 가운데가 k_LensBulge 만큼 볼록.
        // UV = 정면에서 본 평면 — 그림이 찌그러지지 않고 앞뒤 모두 바로 보인다
        static Mesh LensMesh()
        {
            const int rings = 16, segments = 64;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            foreach (int side in new[] { 1, -1 })
            {
                int start = vertices.Count;
                for (int r = 0; r <= rings; r++)
                {
                    float t = (float)r / rings;
                    float z = side * k_LensBulge * Mathf.Sqrt(Mathf.Max(0f, 1f - t * t));
                    for (int s = 0; s <= segments; s++)
                    {
                        float a = 2f * Mathf.PI * s / segments;
                        float x = Mathf.Cos(a) * t, y = Mathf.Sin(a) * t;
                        vertices.Add(new Vector3(x * k_LensHalfWidth, y * k_LensHalfHeight, z));
                        // 앞(+Z)에서 보면 +x 가 왼쪽 — 그림이 바로 보이게 u 를 반대로 (11/09 빨강·파랑 2칸 그림으로 확인). 뒤에서 봐도 바로 보인다
                        uvs.Add(new Vector2(side > 0 ? 0.5f - x * 0.5f : 0.5f + x * 0.5f, 0.5f + y * 0.5f));
                    }
                }
                for (int r = 0; r < rings; r++)
                    for (int s = 0; s < segments; s++)
                    {
                        int i0 = start + r * (segments + 1) + s, i1 = i0 + 1, i2 = i0 + segments + 1, i3 = i2 + 1;
                        if (side > 0)
                            triangles.AddRange(new[] { i0, i2, i1, i1, i2, i3 });
                        else
                            triangles.AddRange(new[] { i0, i1, i2, i1, i3, i2 });
                    }
            }
            var mesh = new Mesh { name = "PortalLens" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return SaveMesh(mesh);
        }

        // 가장자리 물듦 재질 — 렌즈 UV 기준 타원 거리 t 로 가운데 투명 → 가장자리 파랑 (그림 Materials/Hub/PortalEdge.png). 투명 Unlit
        static readonly Color k_EdgeColor = new Color(0.25f, 0.55f, 1f);
        const float k_EdgeStart = 0.55f;   // 이 거리(가운데 0 · 가장자리 1)부터 물들기 시작

        static Material EdgeMaterial()
        {
            EnsureFolder();
            string texturePath = $"{k_MaterialFolder}/PortalEdge.png";
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    float t = Mathf.Clamp01((Mathf.Sqrt(u * u + v * v) - k_EdgeStart) / (1f - k_EdgeStart));
                    Color c = Color.Lerp(k_EdgeColor, Color.white, 0.25f * (1f - t));
                    c.a = Mathf.Pow(t, 1.6f) * 0.95f;
                    texture.SetPixel(x, y, c);
                }
            System.IO.File.WriteAllBytes(texturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(texturePath);
            if (AssetImporter.GetAtPath(texturePath) is TextureImporter ti && (ti.alphaIsTransparency == false || ti.wrapMode != TextureWrapMode.Clamp))
            {
                ti.alphaIsTransparency = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.SaveAndReimport();
            }

            string path = $"{k_MaterialFolder}/Hub_PortalEdge.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Hub_PortalEdge" };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetColor("_BaseColor", Color.white);
            // 투명 (알파 섞기) — URP Unlit 의 Surface Type = Transparent 와 같은 값
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        static Mesh SaveMesh(Mesh mesh)
        {
            if (!AssetDatabase.IsValidFolder(k_MeshFolder))
                AssetDatabase.CreateFolder("Assets/A/Models", "Hub");
            string path = $"{k_MeshFolder}/{mesh.name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            // CopySerialized 는 씬에 이미 올라간 메시를 다시 그리지 않을 때가 있다 — 값을 직접 덮어쓴다
            existing.Clear();
            existing.SetVertices(mesh.vertices);
            existing.SetUVs(0, mesh.uv);
            existing.SetTriangles(mesh.triangles, 0);
            existing.SetNormals(mesh.normals);
            existing.RecalculateTangents();   // 벽 노멀 맵
            existing.RecalculateBounds();
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        static Text MakeText(string name, RectTransform parent, Font font, int size, Vector2 min, Vector2 max, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = min;
            r.anchorMax = max;
            r.offsetMin = r.offsetMax = Vector2.zero;
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        // 사열대 — 연병장 북쪽 가장자리 가운데. 단 위에 지붕, 뒤에 깃대 셋
        const float k_StoneTileMeters = 1.5f;
        internal static Material StoneMaterial() => PaintedMaterial.Ensure("Assets/A/Models/Stone", "Tile", "Stone");

        // 구령대 돌 재질만 다시 입히기 — 허브 전체를 다시 짓지 않고 (11/10)
        [MenuItem("A/만들기/허브 구령대 돌 재질")]
        static void ApplyStandStone()
        {
            var stand = GameObject.Find(k_Root + "/ReviewStand");
            if (stand == null)
            {
                Debug.LogError($"[A] {k_Root}/ReviewStand 가 없다 — 허브 씬을 연다");
                return;
            }
            var stone = StoneMaterial();
            int n = 0;
            foreach (Transform t in stand.transform)
                if (t.name == "Deck" || t.name == "Step")
                {
                    Undo.RecordObject(t.GetComponent<MeshRenderer>(), "A: 구령대 돌");
                    Undo.RecordObject(t.GetComponent<MeshFilter>(), "A: 구령대 돌");
                    WallUvBuilder.Apply(t, stone, k_StoneTileMeters);
                    n++;
                }
            EditorSceneManager.MarkSceneDirty(stand.scene);
            Debug.Log($"[A] 구령대 돌 재질: {n}개 (무늬 한 장 {k_StoneTileMeters} m)");
        }

        static void ReviewStand(Transform root, Material concrete, Material metal, Material roof)
        {
            var stand = Group("ReviewStand", root);
            stand.position = new Vector3(0f, 0f, k_FieldHalfZ + 3.2f);
            stand.rotation = Quaternion.Euler(0f, 180f, 0f);   // 앞 = 연병장
            const float w = 10f, d = 4f, h = 1.2f;
            // 단은 난간 줄(w × d 에서 0.08 m 안쪽)보다 사방 0.15 m 넓게 — 꼭짓점 철기둥 받침판(반지름 0.13 m)이 단 밖으로 튀어나오지 않게 (11/09 사용자)
            const float deckMargin = 0.15f;
            float deckW = w + deckMargin * 2f, deckD = d + deckMargin * 2f;
            // 단 · 계단 = 돌 (11/10 사용자 Models/Stone — 거친 회색 돌 표면 Tile_DefaultMaterial_* 3장 → PaintedMaterial, m 단위 UV 한 장 k_StoneTileMeters)
            var stone = StoneMaterial();
            var deck = Box("Deck", stand, new Vector3(0f, h * 0.5f, 0f), new Vector3(deckW, h, deckD), stone);
            deck.AddComponent<BoxCollider>();
            WallUvBuilder.Apply(deck.transform, stone, k_StoneTileMeters);
            for (int i = 0; i < 4; i++)   // 뒤 계단 (가운데, 11/09 사용자: 뒤쪽으로) — 단 높이 0.3 m 씩, 뒤로 0.3 m 씩
            {
                float stepHeight = h * (i + 1) / 4f;
                var step = Box("Step", stand, new Vector3(0f, stepHeight * 0.5f, -(deckD * 0.5f + 0.3f * (4 - i) - 0.15f)), new Vector3(3f, stepHeight, 0.3f), stone);
                WallUvBuilder.Apply(step.transform, stone, k_StoneTileMeters);
            }
            // 기둥 · 난간 = 철 (11/09 사용자: 사진의 학교 구령대처럼 — 둥근 철기둥 + 받침판, 철 파이프 난간). 재질은 사용자가 따로 줌 → 지금은 임시 Hub_Steel
            var steel = Lit("Hub_Steel", new Color(0.72f, 0.74f, 0.76f), 0.75f, 0.9f);
            float rx = w * 0.5f - 0.08f, rz = d * 0.5f - 0.08f;   // 난간 줄 — 기둥은 난간이 꺾이는 네 꼭짓점에 (11/09 사용자)
            foreach (float x in new[] { -rx, rx })
                foreach (float z in new[] { -rz, rz })
                {
                    Tube("Pillar", stand, new Vector3(x, h, z), new Vector3(x, h + 3f, z), 0.075f, steel);
                    Tube("PillarBase", stand, new Vector3(x, h, z), new Vector3(x, h + 0.02f, z), 0.13f, steel);   // 받침판
                }
            Box("Roof", stand, new Vector3(0f, h + 3.1f, 0f), new Vector3(w + 0.8f, 0.2f, d + 0.8f), roof);
            // 난간 — 앞(연병장 쪽) · 양옆. 뒤는 계단이 있어 열어 둔다 (앞의 콘크리트 난간 벽 'Front' 를 바꿈)
            var rails = Group("Railing", stand);
            Railing(rails, "Railing_Front", new Vector3(-rx, h, rz), new Vector3(rx, h, rz), steel);
            Railing(rails, "Railing_Left", new Vector3(-rx, h, -rz), new Vector3(-rx, h, rz), steel);
            Railing(rails, "Railing_Right", new Vector3(rx, h, -rz), new Vector3(rx, h, rz), steel);
            // 깃대 3개 — 가운데 태극기(더 높게), 양옆 부대기 (11/09 사용자). 국기는 가운데 · 가장 높게 (국기법 시행령 15조)
            var gold = Lit("Hub_FlagFinial", new Color(0.83f, 0.66f, 0.22f), 0.7f, 0.9f);
            Material taegeuk = FlagMaterial("Hub_Flag_Taegeukgi", "Taegeukgi", DrawTaegeukgi);
            Material unit = FlagMaterial("Hub_Flag_Unit", "UnitFlag", DrawUnitFlag);
            Mesh flagMesh = FlagMesh();
            for (int i = -1; i <= 1; i++)
            {
                float height = i == 0 ? k_NationalPole : k_UnitPole;
                var pole = Group(i == 0 ? "FlagPole_National" : "FlagPole_Unit", stand);
                pole.localPosition = new Vector3(i * 4f, 0f, i == 0 ? -d * 0.5f - 3.5f : -d * 0.5f - 1.5f);   // 가운데 깃대는 계단 뒤로
                Cylinder("Pole", pole, new Vector3(0f, height * 0.5f, 0f), 0.06f, height, metal);
                var finial = GameObject.CreatePrimitive(PrimitiveType.Sphere);   // 깃봉
                finial.name = "Finial";
                Object.DestroyImmediate(finial.GetComponent<Collider>());
                finial.transform.SetParent(pole, false);
                finial.transform.localPosition = new Vector3(0f, height + 0.1f, 0f);
                finial.transform.localScale = Vector3.one * 0.2f;
                finial.GetComponent<MeshRenderer>().sharedMaterial = gold;

                var flag = new GameObject(i == 0 ? "Flag_Taegeukgi" : "Flag_Unit", typeof(MeshFilter), typeof(MeshRenderer));
                flag.transform.SetParent(pole, false);
                flag.transform.localPosition = new Vector3(0.06f, height - 0.15f - k_FlagHeight, 0f);   // 깃봉 바로 아래, 깃대 옆에서 펼쳐진다
                flag.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // 구령대가 180° 돌아 있다 → 연병장에서 보면 깃발이 오른쪽(동쪽)으로 날린다
                flag.GetComponent<MeshFilter>().sharedMesh = flagMesh;
                var fr = flag.GetComponent<MeshRenderer>();
                fr.sharedMaterial = i == 0 ? taegeuk : unit;
            }
        }

        // 철 파이프 난간 한 줄 (a → b, 바닥 높이) — 사진처럼: 기둥(약 1.25 m 간격) · 손잡이 파이프(1.0 m) · 그 아래 가로 파이프(0.88 m) ·
        // 아래 파이프(0.1 m) · 그 사이 세로 살(0.12 m 간격). 형태만 — 재질은 나중에
        const float k_RailTop = 1.0f, k_RailMid = 0.88f, k_RailBottom = 0.1f, k_RailPost = 1.25f, k_RailBar = 0.12f;

        static void Railing(Transform parent, string name, Vector3 a, Vector3 b, Material material)
        {
            var g = Group(name, parent);
            Vector3 up = Vector3.up;
            Tube("Rail_Top", g, a + up * k_RailTop, b + up * k_RailTop, 0.03f, material);
            Tube("Rail_Mid", g, a + up * k_RailMid, b + up * k_RailMid, 0.015f, material);
            Tube("Rail_Bottom", g, a + up * k_RailBottom, b + up * k_RailBottom, 0.015f, material);
            float length = Vector3.Distance(a, b);
            int posts = Mathf.Max(1, Mathf.RoundToInt(length / k_RailPost));
            for (int i = 0; i <= posts; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, (float)i / posts);
                Tube("Post", g, p, p + up * k_RailTop, 0.025f, material);
            }
            int bars = Mathf.RoundToInt(length / k_RailBar);
            for (int i = 1; i < bars; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, (float)i / bars);
                Tube("Bar", g, p + up * k_RailBottom, p + up * k_RailMid, 0.01f, material);
            }
        }

        // 두 점 사이 둥근 파이프 (Cylinder 를 늘리고 돌림)
        static void Tube(string name, Transform parent, Vector3 from, Vector3 to, float radius, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            Vector3 dir = to - from;
            go.transform.localPosition = (from + to) * 0.5f;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
            go.transform.localScale = new Vector3(radius * 2f, dir.magnitude * 0.5f, radius * 2f);
            if (material != null)
                go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        // ── 깃발 (11/09) ──
        const float k_NationalPole = 12f, k_UnitPole = 10f;   // 국기 깃대를 2 m 더 높게
        const float k_FlagWidth = 2.1f, k_FlagHeight = 1.4f;  // 3 : 2

        // 깃발 메시 — 깃대 쪽(x 0)에서 x - 방향으로 펼쳐진 천, 바람에 진 물결 (깃대에서 멀수록 크게). UV 0 ~ 1. 양면은 재질(앞뒤 모두 그림)로
        static Mesh FlagMesh()
        {
            const int nx = 32, ny = 6;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int y = 0; y <= ny; y++)
                for (int x = 0; x <= nx; x++)
                {
                    float u = (float)x / nx, v = (float)y / ny;
                    float wave = Mathf.Sin((u * 1.6f + v * 0.15f) * Mathf.PI * 2f) * 0.09f * Mathf.SmoothStep(0f, 1f, u * 1.5f);
                    vertices.Add(new Vector3(u * k_FlagWidth, v * k_FlagHeight - u * 0.06f, wave));   // 끝이 살짝 처진다
                    uvs.Add(new Vector2(u, v));
                }
            for (int y = 0; y < ny; y++)
                for (int x = 0; x < nx; x++)
                {
                    int i0 = y * (nx + 1) + x, i1 = i0 + 1, i2 = i0 + nx + 1, i3 = i2 + 1;
                    triangles.AddRange(new[] { i0, i2, i1, i1, i2, i3 });
                }
            var mesh = new Mesh { name = "Flag" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return SaveMesh(mesh);
        }

        // 깃발 재질 — 그림(그리는 함수로 만든 PNG)을 입힌 양면 Lit. 그림은 Materials/Hub/Flags/<이름>.png (다시 누르면 다시 그린다)
        static Material FlagMaterial(string name, string textureName, System.Func<float, float, Color> draw)
        {
            EnsureFolder();
            string folder = k_MaterialFolder + "/Flags";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(k_MaterialFolder, "Flags");
            const int w = 1200, h = 800, ss = 3;   // 3 × 3 표본으로 경계를 부드럽게
            var texture = new Texture2D(w, h, TextureFormat.RGB24, false);
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color sum = Color.clear;
                    for (int sy = 0; sy < ss; sy++)
                        for (int sx = 0; sx < ss; sx++)
                            sum += draw((x + (sx + 0.5f) / ss) / w, (y + (sy + 0.5f) / ss) / h);
                    pixels[y * w + x] = sum / (ss * ss);
                }
            texture.SetPixels(pixels);
            string texturePath = $"{folder}/{textureName}.png";
            System.IO.File.WriteAllBytes(texturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(texturePath);
            if (AssetImporter.GetAtPath(texturePath) is TextureImporter ti && ti.wrapMode != TextureWrapMode.Clamp)
            {
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.SaveAndReimport();
            }
            var material = Lit(name, Color.white, 0.15f, 0f);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Cull", 0f);   // 양면 (천)
            material.doubleSidedGI = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        // 태극기 (국가기록원 국기의 제작 규격) — 가로 : 세로 3 : 2. u, v = 0 ~ 1 (v 위로).
        // 태극 원: 가운데, 지름 = 세로의 1/2. 경계는 왼쪽 위 → 오른쪽 아래 대각선 기준, 위쪽 빨강 · 아래쪽 파랑,
        // 지름의 1/2 인 작은 반원 두 개 — 왼쪽 위 끝은 아래로(빨강이 내려옴), 오른쪽 아래 끝은 위로(파랑이 올라감).
        // 4괘: 왼쪽 위 건(☰) · 오른쪽 위 감(☵) · 왼쪽 아래 리(☲) · 오른쪽 아래 곤(☷). 괘 길이 = 원 지름의 1/2, 괘 너비 = 1/3,
        // 효 너비 = 괘 너비의 1/4, 효 사이 = 1/8, 원과 괘 사이 = 원 지름의 1/4. 끊긴 효의 틈 = 효 사이와 같게
        static readonly Color k_TaegeukRed = new Color32(0xCD, 0x2E, 0x3A, 0xFF);
        static readonly Color k_TaegeukBlue = new Color32(0x00, 0x47, 0xA0, 0xFF);

        static Color DrawTaegeukgi(float u, float v)
        {
            // 세로 2 · 가로 3 단위, 가운데 원점
            var p = new Vector2((u - 0.5f) * 3f, (v - 0.5f) * 2f);
            const float R = 0.5f;   // 원 반지름 (지름 = 세로 2 의 1/2)
            var diag = new Vector2(3f, -2f).normalized;   // 왼쪽 위 → 오른쪽 아래
            var up = new Vector2(2f, 3f).normalized;      // 대각선의 위쪽
            if (p.magnitude <= R)
            {
                if ((p + diag * R * 0.5f).magnitude < R * 0.5f)
                    return k_TaegeukRed;    // 왼쪽 위 작은 원 — 빨강
                if ((p - diag * R * 0.5f).magnitude < R * 0.5f)
                    return k_TaegeukBlue;   // 오른쪽 아래 작은 원 — 파랑
                return Vector2.Dot(p, up) >= 0f ? k_TaegeukRed : k_TaegeukBlue;
            }
            // 괘 — (모서리 방향, 효 3개: true = 이어진 효, 안쪽(원 쪽)부터)
            (Vector2 dir, bool[] lines)[] trigrams =
            {
                (new Vector2(-3f, 2f), new[] { true, true, true }),      // 건 ☰ 왼쪽 위
                (new Vector2(3f, 2f), new[] { false, true, false }),     // 감 ☵ 오른쪽 위
                (new Vector2(-3f, -2f), new[] { true, false, true }),    // 리 ☲ 왼쪽 아래
                (new Vector2(3f, -2f), new[] { false, false, false }),   // 곤 ☷ 오른쪽 아래
            };
            float length = R, width = 2f * R / 3f, bar = width / 4f, gap = width / 8f;
            foreach (var (dirRaw, lines) in trigrams)
            {
                var dir = dirRaw.normalized;
                var side = new Vector2(-dir.y, dir.x);
                var center = dir * (R + 2f * R / 4f + width * 0.5f);
                var q = p - center;
                float along = Vector2.Dot(q, side), across = Vector2.Dot(q, dir) + width * 0.5f;   // across 0 = 원 쪽 끝
                if (Mathf.Abs(along) > length * 0.5f || across < 0f || across > width)
                    continue;
                for (int k = 0; k < 3; k++)
                {
                    float start = k * (bar + gap);
                    if (across >= start && across <= start + bar)
                        return lines[k] || Mathf.Abs(along) > gap * 0.5f ? Color.black : Color.white;
                }
            }
            return Color.white;
        }

        // 부대기 (임시 — 실제 부대 표식 아님) — 짙은 남색 바탕, 금색 테두리, 가운데 흰 방패(금색 테두리)에 남색 가로띠 두 줄.
        // 11/09 점검: 처음 그린 '흰 원 안의 별'은 북한 인공기 문양(흰 원 + 별)과 닮았고, 우리 군에서 별은 장성 계급 표지라 뺐다
        static Color DrawUnitFlag(float u, float v)
        {
            var field = new Color32(0x1C, 0x2E, 0x5C, 0xFF);
            var goldColor = new Color32(0xC9, 0xA2, 0x3A, 0xFF);
            float border = 0.045f;
            if (u < border * 2f / 3f || u > 1f - border * 2f / 3f || v < border || v > 1f - border)
                return goldColor;
            var p = new Vector2((u - 0.5f) * 3f, (v - 0.5f) * 2f);   // 가로 3 · 세로 2, 가운데 원점
            float outer = ShieldDistance(p, 0.62f), inner = ShieldDistance(p, 0.56f);
            if (outer > 0f)
                return field;
            if (inner > 0f)
                return goldColor;   // 방패 테두리
            if (Mathf.Abs(p.y - 0.12f) < 0.05f || Mathf.Abs(p.y + 0.08f) < 0.05f)
                return field;       // 가로띠 두 줄
            return Color.white;
        }

        // 방패 모양 (위는 반듯, 아래는 뾰족) — 안이면 음수. size = 방패 반폭
        static float ShieldDistance(Vector2 p, float size)
        {
            float top = size * 1.1f, waist = -size * 0.15f, bottom = -size * 1.25f;
            if (p.y > top || p.y < bottom)
                return 1f;
            if (p.y >= waist)
                return Mathf.Abs(p.x) - size;
            // 허리 아래는 원호처럼 좁아져 아래 끝에서 만난다
            float t = (waist - p.y) / (waist - bottom);
            float half = size * Mathf.Sqrt(Mathf.Max(0f, 1f - t * t));
            return Mathf.Abs(p.x) - half;
        }

        // 남쪽 정문 — 담장 + 문 기둥 + 위병소. 담장은 건물 바깥을 둘러싼다
        // 담장 · 정문 기둥 = 벽돌 (11/09 사용자). 위병소 · 길은 그대로
        static void Gate(Transform root, Material concrete, Material metal, Material asphalt, Material brick)
        {
            var g = Group("Fence", root);
            const float half = 46f, north = 44f, height = 2.2f, gateHalf = k_GateHalf;
            void Wall(string name, Vector3 a, Vector3 b)
            {
                Vector3 c = (a + b) * 0.5f;
                Vector3 size = new Vector3(Mathf.Abs(b.x - a.x) + 0.25f, height, Mathf.Abs(b.z - a.z) + 0.25f);
                var w = Box(name, g, new Vector3(c.x, height * 0.5f, c.z), size, brick);
                WallUvBuilder.Apply(w.transform, brick);   // m 단위 UV — 벽돌 크기가 담장 길이와 상관없이 같게
            }
            Wall("Wall_S_W", new Vector3(-half, 0f, k_GateZ), new Vector3(-gateHalf, 0f, k_GateZ));
            Wall("Wall_S_E", new Vector3(gateHalf, 0f, k_GateZ), new Vector3(half, 0f, k_GateZ));
            Wall("Wall_W", new Vector3(-half, 0f, k_GateZ), new Vector3(-half, 0f, north));
            Wall("Wall_E", new Vector3(half, 0f, k_GateZ), new Vector3(half, 0f, north));
            Wall("Wall_N", new Vector3(-half, 0f, north), new Vector3(half, 0f, north));
            foreach (float x in new[] { -gateHalf, gateHalf })
                WallUvBuilder.Apply(Box("GatePost", g, new Vector3(x, 1.6f, k_GateZ), new Vector3(0.8f, 3.2f, 0.8f), brick).transform, brick);
            // 정문 밖 길
            // 정문 밖 길 — 남쪽 산 골짜기(x 0)로 쭉 이어져 안개 속으로 사라진다 (11/09 사용자: 도로 쭉 이어서)
            WallUvBuilder.Apply(Box("Road", g, new Vector3(0f, -0.005f, (k_GateZ + k_RoadEnd) * 0.5f), new Vector3(gateHalf * 2f, 0.01f, k_GateZ - k_RoadEnd), asphalt).transform, asphalt);
            RoadLines(g);
            Truck(g);
            GuardPost(g, concrete, metal);
        }

        // 위병소 — 정문 안쪽 동쪽 (8, 0, -21). 11/10 사용자: 속을 비우고 문 · 정문 쪽 지나가는 사람이 보이는 창.
        // 벽 네 장(두께 0.15, 높이 2.8) + 지붕. 서쪽 벽(정문 · 정문 안 길 쪽)에 넓은 창, 북쪽 벽(연병장 쪽)에 문 + 작은 창.
        // 문 = 내무반 문 모델(Models/Door/sage_glass_door — 폭 1.016 · 높이 2.644, 한 덩어리라 닫힌 채, 사용자 11/10) → 벽을 2.6 → 2.8 m 로.
        // 창 = 내무반 창 유리(WindowGlass, 투명) + 철 창틀. 벽 · 문에 부딪힘 (내무반 문처럼 지나가지 못한다).
        const float k_PostSize = 2.4f, k_PostHeight = 2.8f, k_PostWall = 0.15f;
        const string k_PostDoorModelPath = "Assets/A/Models/Door/sage_glass_door.fbx";
        const float k_PostDoorWidth = 1.016f, k_PostDoorHeight = 2.644f, k_PostDoorU = 0.5f;   // 북쪽 벽 위 문 가운데 (동쪽 벽 안쪽 면 x 1.05 에 닿지 않게)

        static void GuardPost(Transform fence, Material concrete, Material metal)
        {
            var post = Group("GuardPost", fence);
            post.position = new Vector3(k_GateHalf + 3f, 0f, k_GateZ + 3f);
            var glass = AssetDatabase.LoadAssetAtPath<Material>("Assets/A/Models/Window/Materials/WindowGlass.mat");
            // 벽 = 다른 건물과 같은 내무반 벽 재질 (11/10 사용자), m 단위 UV — 무늬 크기가 건물 벽과 같다. 없으면 콘크리트
            var wallMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/A/Models/Wall/Materials/Wall.mat") ?? concrete;
            float half = k_PostSize * 0.5f, inner = half - k_PostWall * 0.5f;

            // 벽 하나 = 면 틀(원점 = 벽 가운데 바닥, +X = 벽을 따라, +Z = 바깥) 안에서 구멍(가로 u0 ~ u1, 높이 v0 ~ v1)을 뺀 상자들
            void Wall(string name, Vector3 center, float yaw, params (float u0, float u1, float v0, float v1, bool window)[] holes)
            {
                var w = Group(name, post);
                w.localPosition = center;
                w.localRotation = Quaternion.Euler(0f, yaw, 0f);
                float u = -half;
                void Solid(string part, float a, float b, float y0, float y1)
                {
                    if (b - a < 0.01f || y1 - y0 < 0.01f)
                        return;
                    var piece = Box(part, w, new Vector3((a + b) * 0.5f, (y0 + y1) * 0.5f, 0f), new Vector3(b - a, y1 - y0, k_PostWall), wallMaterial);
                    piece.AddComponent<BoxCollider>();
                    WallUvBuilder.Apply(piece.transform, wallMaterial);
                }
                foreach (var h in holes)
                {
                    Solid("Wall", u, h.u0, 0f, k_PostHeight);
                    Solid("Below", h.u0, h.u1, 0f, h.v0);
                    Solid("Above", h.u0, h.u1, h.v1, k_PostHeight);
                    if (h.window)
                    {
                        float cu = (h.u0 + h.u1) * 0.5f, cv = (h.v0 + h.v1) * 0.5f, wu = h.u1 - h.u0, wv = h.v1 - h.v0;
                        if (glass != null)
                            Box("Glass", w, new Vector3(cu, cv, 0f), new Vector3(wu, wv, 0.01f), glass).GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        const float f = 0.05f, fd = k_PostWall + 0.02f;   // 창틀 — 벽보다 살짝 두껍게
                        Box("Frame", w, new Vector3(cu, h.v0 + f * 0.5f, 0f), new Vector3(wu, f, fd), metal);
                        Box("Frame", w, new Vector3(cu, h.v1 - f * 0.5f, 0f), new Vector3(wu, f, fd), metal);
                        Box("Frame", w, new Vector3(h.u0 + f * 0.5f, cv, 0f), new Vector3(f, wv, fd), metal);
                        Box("Frame", w, new Vector3(h.u1 - f * 0.5f, cv, 0f), new Vector3(f, wv, fd), metal);
                        Box("Frame", w, new Vector3(cu, cv, 0f), new Vector3(0.03f, wv, fd), metal);   // 가운데 살
                    }
                    u = h.u1;
                }
                Solid("Wall", u, half, 0f, k_PostHeight);
            }

            // 서쪽 (정문 쪽, 바깥 = -x) — 넓은 창, 창턱 0.9 · 위 2.0 m
            Wall("Wall_W", new Vector3(-inner, 0f, 0f), -90f, (-0.85f, 0.85f, 0.9f, 2.0f, true));
            // 북쪽 (연병장 쪽, 바깥 = +z) — 서쪽 반에 작은 창, 동쪽 반에 문
            float d0 = k_PostDoorU - k_PostDoorWidth * 0.5f, d1 = k_PostDoorU + k_PostDoorWidth * 0.5f;
            Wall("Wall_N", new Vector3(0f, 0f, inner), 0f, (-0.95f, -0.25f, 0.9f, 2.0f, true), (d0, d1, 0f, k_PostDoorHeight, false));
            Wall("Wall_E", new Vector3(inner, 0f, 0f), 90f);
            Wall("Wall_S", new Vector3(0f, 0f, -inner), 180f);
            Box("Roof", post, new Vector3(0f, k_PostHeight + 0.1f, 0f), new Vector3(2.9f, 0.2f, 2.9f), metal);
            Box("Floor", post, new Vector3(0f, 0.01f, 0f), new Vector3(k_PostSize - k_PostWall * 2f, 0.02f, k_PostSize - k_PostWall * 2f), concrete);

            // 문 — 내무반 문 모델, 벽 두께 가운데, 앞(+Z) = 바깥. 문틀(두께 0.13)이 벽(0.15) 안에 든다. 닫힌 채 — 문 구멍을 상자로 막는다
            var doorModel = AssetDatabase.LoadAssetAtPath<GameObject>(k_PostDoorModelPath);
            var door = Group("Door", post);
            door.localPosition = new Vector3(k_PostDoorU, 0f, inner);
            if (doorModel != null)
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(doorModel, door);
                model.name = "Model";
            }
            var block = door.gameObject.AddComponent<BoxCollider>();
            block.center = new Vector3(0f, k_PostDoorHeight * 0.5f, 0f);
            block.size = new Vector3(k_PostDoorWidth, k_PostDoorHeight, k_PostWall);
        }

        // 위병소만 다시 짓기 — 허브 전체를 다시 짓지 않고 (11/10)
        [MenuItem("A/만들기/허브 위병소 (속 비움 · 문 · 창)")]
        static void BuildGuardPost()
        {
            var fence = GameObject.Find(k_Root + "/Fence");
            if (fence == null)
            {
                Debug.LogError($"[A] {k_Root}/Fence 가 없다 — 허브 씬을 연다");
                return;
            }
            Undo.SetCurrentGroupName("A: 위병소");
            int group = Undo.GetCurrentGroup();
            Transform old = fence.transform.Find("GuardPost");
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);
            GuardPost(fence.transform, Outdoor("Outdoor_Concrete"), Outdoor("Outdoor_Metal"));
            Transform post = fence.transform.Find("GuardPost");
            Undo.RegisterCreatedObjectUndo(post.gameObject, "A: 위병소");
            foreach (var c in post.GetComponentsInChildren<Transform>())
                GameObjectUtility.SetStaticEditorFlags(c.gameObject, StaticEditorFlags.BatchingStatic);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(fence.scene);
            Debug.Log("[A] 위병소: 속 비움 · 서쪽 창(정문 쪽) · 북쪽 문 + 작은 창");
        }

        const float k_GateHalf = 5f;                 // 정문 폭의 반 = 정문 밖 길 폭의 반
        const float k_RoadEnd = k_ValleyEnd - 30f;   // 정문 밖 길 끝 — 골짜기가 닫히는 곳, 산 뒤로 들어간다

        // 정문 밖 길 교통선 (11/10 사용자) — 가운데 노란 겹선(중앙선, 넘지 않는 선) · 양 가장자리 흰 실선. 길 윗면(0) 바로 위 얇은 판
        const float k_LineWidth = 0.15f, k_CenterLineGap = 0.15f, k_EdgeLineInset = 0.3f, k_LineStart = 0.5f;

        static void RoadLines(Transform fence)
        {
            var g = Group("RoadLines", fence);
            var yellow = Lit("Hub_RoadLine_Yellow", new Color(0.95f, 0.70f, 0.10f), 0.3f, 0f);
            var white = Lit("Hub_RoadLine_White", new Color(0.92f, 0.92f, 0.90f), 0.3f, 0f);
            float z0 = k_GateZ - k_LineStart, length = z0 - k_RoadEnd, zc = (z0 + k_RoadEnd) * 0.5f;
            void Line(string name, float x, Material m) =>
                Box(name, g, new Vector3(x, 0.002f, zc), new Vector3(k_LineWidth, 0.004f, length), m).GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            float off = (k_CenterLineGap + k_LineWidth) * 0.5f;
            Line("Center_W", -off, yellow);
            Line("Center_E", off, yellow);
            float edge = k_GateHalf - k_EdgeLineInset - k_LineWidth * 0.5f;
            Line("Edge_W", -edge, white);
            Line("Edge_E", edge, white);
        }

        // 정문 밖에 선 군용 트럭 (11/10 사용자 Models/CanvasTruck — 2.5 t 카고, 길이 7.7 · 높이 3.85 · 폭 3.1 m(거울 포함), 원점 = 바닥, 앞 = +X).
        // 정문 안에서 밖을 볼 때 왼쪽(동쪽) 차로에 정문을 보고 선다 — 길가 나무(x ±7.5 ~ 8.6)를 피해 차로 안, 담장에서 앞 범퍼까지 약 3.5 m
        const string k_TruckModelPath = "Assets/A/Models/CanvasTruck/textures/Canvas_Truck_v01.fbx";
        static readonly Vector3 k_TruckPosition = new Vector3(2.5f, 0f, k_GateZ - 7.5f);
        const float k_TruckYaw = -90f;   // 모델 앞(+X) → 북쪽(+Z, 정문)

        static void Truck(Transform fence)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(k_TruckModelPath);
            if (model == null)
            {
                Debug.LogWarning($"[A] {k_TruckModelPath} 이 없다 — 트럭 없이 짓는다");
                return;
            }
            var truck = (GameObject)PrefabUtility.InstantiatePrefab(model, fence);
            truck.name = "Truck";
            truck.transform.localPosition = k_TruckPosition;
            truck.transform.localRotation = Quaternion.Euler(0f, k_TruckYaw, 0f);
            PaintedMaterial.Assign(truck, TexturedLit("Assets/A/Models/CanvasTruck", "textures/T_Canvas_Truck", "Canvas_Truck"));
            // 부딪힘 — 차체 상자 하나
            var bounds = new Bounds();
            bool first = true;
            foreach (var r in truck.GetComponentsInChildren<Renderer>())
            {
                Bounds b = r.localBounds;
                if (first) { bounds = b; first = false; } else bounds.Encapsulate(b);
            }
            var box = truck.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = bounds.size;
        }

        // ── 정문 밖 길 가로수 · 가로등 자리 (11/10 사용자: 길 양옆 나무, 곧 들어올 가로등 모델을 생각해서) ──
        // 길 가장자리(x ±5)에서 바깥으로: 가로등 줄 x ±5.8 (길 끝에서 0.8 m — 등 팔이 길 위로 나온다) → 나무 줄 x ±9.5.
        // 가로등은 k_LightSpacing(25 m)마다 양쪽 같은 z. 나무는 가로등 사이에 두 그루씩, 가로등 z 에서 7.5 m 떨어진 자리 (나무 간격 10 m) — 등 머리를 가리지 않게.
        // 가로등 자리 = 점(Avenue/LightSpots/Spot_*, +Z = 길 쪽), 그 아래에 가로등 모델(StreetLightBuilder, 11/10 — 등이 길 위로 2.9 m 나온다).
        // 담장 바로 밖(z -24 ~ -40)은 기존 숲 나무가 길가에 붙어 있어 가로수는 그 뒤부터. 기존 나무와 4 m 안이면 건너뛴다.
        const float k_LightRowX = k_GateHalf + 0.8f, k_TreeRowX = k_GateHalf + 4.5f;
        const float k_LightSpacing = 25f, k_TreeLightGap = 6f;
        const float k_AvenueStart = k_GateZ - 6f, k_AvenueEnd = k_ValleyEnd + 5f;   // 가로등 첫 줄 z -30 · 골짜기가 닫히기 전까지

        static void Avenue(Transform root)
        {
            var g = Group("Avenue", root);
            var spots = Group("LightSpots", g);
            var trees = Group("Trees", g);
            var rng = new System.Random(1110);
            string[] prefabs = { "Tree_Green_1", "Tree_Green_2", "Tree_Green_3" };

            // 기존 나무 (담장 밖 숲 · 산기슭) — 가로수가 겹치지 않게
            var others = new List<Vector2>();
            foreach (var r in root.GetComponentsInChildren<Transform>())
                if (r.name.StartsWith("Tree_Green") && !r.IsChildOf(g))
                    others.Add(new Vector2(r.position.x, r.position.z));

            int lights = 0, planted = 0;
            for (float z = k_AvenueStart; z >= k_AvenueEnd; z -= k_LightSpacing)
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    var spot = new GameObject(side < 0f ? "Spot_W" : "Spot_E").transform;
                    spot.SetParent(spots, false);
                    spot.localPosition = new Vector3(side * k_LightRowX, 0f, z);
                    spot.localRotation = Quaternion.Euler(0f, side < 0f ? 90f : -90f, 0f);   // +Z = 길 쪽
                    StreetLightBuilder.Place(spot, Vector3.zero, 0f);   // 11/10 사용자 모델 — 등이 길 위로
                    lights++;
                }
                // 이 가로등과 다음 가로등 사이 — 가로등에서 k_TreeLightGap 이상 떨어진 두 자리
                foreach (float dz in new[] { -k_TreeLightGap - 1.5f, -k_LightSpacing + k_TreeLightGap + 1.5f })
                {
                    float tz = z + dz;
                    if (tz < k_AvenueEnd)
                        continue;
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var c = new Vector2(side * k_TreeRowX + ((float)rng.NextDouble() - 0.5f) * 0.6f, tz + ((float)rng.NextDouble() - 0.5f) * 0.6f);
                        double pick = rng.NextDouble(), turn = rng.NextDouble(), size = rng.NextDouble();   // 건너뛰어도 난수 순서가 같게
                        if (tz > k_GateZ - 16f || others.Exists(o => (o - c).sqrMagnitude < 16f))
                            continue;
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/A/Models/Trees/Prefabs/{prefabs[(int)(pick * prefabs.Length) % prefabs.Length]}.prefab");
                        if (prefab == null)
                            continue;
                        var t = (GameObject)PrefabUtility.InstantiatePrefab(prefab, trees);
                        t.transform.position = new Vector3(c.x, 0f, c.y);
                        t.transform.rotation = Quaternion.Euler(0f, (float)turn * 360f, 0f);
                        t.transform.localScale = Vector3.one * (1.0f + 0.25f * (float)size);
                        planted++;
                    }
                }
            }

            // 가로수 뒤 숲 (11/10 사용자: 1 ~ 2겹 더, 숲처럼). 담장 때문에 바깥이 보이는 곳은 정문 쪽뿐이라 이 길가에만.
            // 가로수 줄 바깥 두 줄(x ±13.5 · ±18), 줄 안에서 8 m 간격 + 앞뒤 · 옆으로 흔들어 줄 맞춘 티가 덜 나게, 두 줄은 반 칸 엇갈림.
            // 바닥 높이는 산 메시를 따르되 1 m 넘게 오른 곳(골짜기 끝 가파른 산면)은 건너뛴다 — 멀리서 나무가 떠 보였다. 다른 나무와 3.5 m 안이면 건너뛴다
            var forest = Group("Forest", g);
            foreach (Transform t in trees)
                others.Add(new Vector2(t.position.x, t.position.z));
            int forested = 0;
            for (int row = 0; row < k_ForestRows.Length; row++)
            {
                float rowX = k_ForestRows[row];
                for (float z = k_GateZ - 10f - row * k_ForestSpacing * 0.5f; z >= k_AvenueEnd; z -= k_ForestSpacing)
                {
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var c = new Vector2(side * (rowX + ((float)rng.NextDouble() - 0.5f) * 2.5f), z + ((float)rng.NextDouble() - 0.5f) * 4f);
                        double pick = rng.NextDouble(), turn = rng.NextDouble(), size = rng.NextDouble();
                        float y = Mathf.Max(0f, MountainHeight(c.x, c.y), RingHeight(c.x, c.y));
                        if (y > 1f || others.Exists(o => (o - c).sqrMagnitude < 12.25f))
                            continue;
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/A/Models/Trees/Prefabs/{prefabs[(int)(pick * prefabs.Length) % prefabs.Length]}.prefab");
                        if (prefab == null)
                            continue;
                        var t = (GameObject)PrefabUtility.InstantiatePrefab(prefab, forest);
                        t.transform.position = new Vector3(c.x, y, c.y);
                        t.transform.rotation = Quaternion.Euler(0f, (float)turn * 360f, 0f);
                        t.transform.localScale = Vector3.one * (1.05f + 0.4f * (float)size);
                        others.Add(c);
                        forested++;
                    }
                }
            }
            Debug.Log($"[A] 정문 밖 길: 가로등 자리 {lights} · 가로수 {planted} · 뒤 숲 {forested}");
        }

        static readonly float[] k_ForestRows = { k_TreeRowX + 4f, k_TreeRowX + 8.5f };   // x ±13.5 · ±18
        const float k_ForestSpacing = 8f;

        // 정문 밖 트럭 · 교통선 · 가로수(가로등 자리)만 다시 놓기 — 허브 전체를 다시 짓지 않고 (11/10)
        [MenuItem("A/만들기/허브 정문 밖 (트럭 · 교통선 · 가로수)")]
        static void BuildGateOutside()
        {
            var fence = GameObject.Find(k_Root + "/Fence");
            var root = GameObject.Find(k_Root);
            if (fence == null)
            {
                Debug.LogError($"[A] {k_Root}/Fence 가 없다 — 허브 씬을 열고 메뉴 'A/만들기/허브'를 먼저 누른다");
                return;
            }
            Undo.SetCurrentGroupName("A: 허브 정문 밖");
            int group = Undo.GetCurrentGroup();
            var parts = new[] { (fence.transform, "RoadLines"), (fence.transform, "Truck"), (root.transform, "Avenue") };
            foreach (var (parent, name) in parts)
            {
                Transform old = parent.Find(name);
                if (old != null)
                    Undo.DestroyObjectImmediate(old.gameObject);
            }
            RoadLines(fence.transform);
            Truck(fence.transform);
            Avenue(root.transform);
            foreach (var (parent, name) in parts)
            {
                Transform t = parent.Find(name);
                if (t == null)
                    continue;
                Undo.RegisterCreatedObjectUndo(t.gameObject, "A: 허브 정문 밖");
                foreach (var c in t.GetComponentsInChildren<Transform>())
                    GameObjectUtility.SetStaticEditorFlags(c.gameObject, StaticEditorFlags.BatchingStatic);
            }
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(fence.scene);
            Debug.Log($"[A] 허브 정문 밖: 교통선 · 트럭 {k_TruckPosition} · 가로수");
        }

        static void Trees(Transform root)
        {
            var trees = Group("Trees", root);
            var rng = new System.Random(1109);
            string[] prefabs = { "Tree_Green_1", "Tree_Green_2", "Tree_Green_3" };
            Vector2[] spots =
            {
                new Vector2(-38f, -18f), new Vector2(-40f, 0f), new Vector2(-38f, 20f), new Vector2(-30f, 36f), new Vector2(-12f, 38f),
                new Vector2(12f, 38f), new Vector2(30f, 36f), new Vector2(38f, 20f), new Vector2(40f, 0f), new Vector2(38f, -18f),
                new Vector2(-14f, -30f), new Vector2(-24f, -33f), new Vector2(14f, -30f), new Vector2(24f, -33f), new Vector2(-21f, -19f), new Vector2(21f, -19f),
            };
            var all = new List<Vector2>(spots);

            // 담장 바깥 숲 (11/09 사용자: 벽 바깥에 지금의 4배 → 담장 가까이, 안에서 보이는 빈 곳 위주로).
            // 담장(x ±46 · z -24 ~ 44) 바깥 2.5 ~ 12 m 띠. 연병장 가운데에서 볼 때 건물에 가리는 자리는 건너뛴다 → 담장 너머 빈 하늘 쪽에 모인다.
            // 정문 앞 길(x ±7)은 비우고, 나무끼리 5 m 이상. 자리는 시드 고정이라 다시 지어도 같다
            var footprints = BuildingFootprints();
            var outsideRng = new System.Random(11091);
            int want = k_OutsideTrees, tries = 0;
            while (want > 0 && tries++ < 50000)
            {
                var c = new Vector2(Mathf.Lerp(-58f, 58f, (float)outsideRng.NextDouble()), Mathf.Lerp(-36f, 56f, (float)outsideRng.NextDouble()));
                bool outsideWall = Mathf.Abs(c.x) > 48.5f || c.y > 46.5f || c.y < -26.5f;
                bool onRoad = Mathf.Abs(c.x) < 7f && c.y < -24f;
                if (!outsideWall || onRoad || all.Exists(o => (o - c).sqrMagnitude < 25f) || HiddenByBuilding(c, footprints))
                    continue;
                all.Add(c);
                want--;
            }

            // 담장 안 흙 (11/10 사용자: 건물 구석 흙 부분에 더) — 담장 안, 콘크리트(이동 구역) 밖, 건물에서 2 m 밖, 나무끼리 5 m 이상
            var innerRng = new System.Random(11101);
            want = k_InnerDirtTrees;
            tries = 0;
            while (want > 0 && tries++ < 50000)
            {
                var c = new Vector2(Mathf.Lerp(-44f, 44f, (float)innerRng.NextDouble()), Mathf.Lerp(k_GateZ + 2f, 42f, (float)innerRng.NextDouble()));
                bool onConcrete = Mathf.Abs(c.x) < k_SideFront + 2f && c.y < k_NorthFront + 2f;
                if (onConcrete || footprints.Exists(r => r.Contains(c) || DistanceToRect(c, r) < 2f) || all.Exists(o => (o - c).sqrMagnitude < 25f))
                    continue;
                all.Add(c);
                want--;
            }

            for (int i = 0; i < all.Count; i++)
            {
                float turn = (float)rng.NextDouble(), size = (float)rng.NextDouble();
                // 11/10 사용자: 콘크리트(이동 구역) 위 나무는 없앤다 — 처음 자리 표의 (±21, -19). 난수는 그대로 써서 다른 나무 방향 · 크기가 안 바뀌게
                if (Mathf.Abs(all[i].x) < k_SideFront && all[i].y > k_GateZ && all[i].y < k_NorthFront)
                    continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/A/Models/Trees/Prefabs/{prefabs[i % prefabs.Length]}.prefab");
                if (prefab == null)
                    return;
                var t = (GameObject)PrefabUtility.InstantiatePrefab(prefab, trees);
                t.transform.position = new Vector3(all[i].x, 0f, all[i].y);
                t.transform.rotation = Quaternion.Euler(0f, turn * 360f, 0f);
                t.transform.localScale = Vector3.one * (1.1f + 0.35f * size);
            }
        }

        const int k_InnerDirtTrees = 32;

        static float DistanceToRect(Vector2 p, Rect r)
        {
            float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax), dz = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        // 이동 경계 (11/10 사용자: 콘크리트 둘레(x ±28 · z -24 ~ 23) 안에서만). 리그의 CharacterController 가 막히는 보이지 않는 벽 네 장 —
        // 콘크리트 가장자리 바로 바깥, 높이 4 m. 건물 사이 틈 · 정문으로 나가지 못한다. 담장 · 정문은 보이기만 한다.
        // Ignore Raycast 층 — 손 레이가 걸려 보이지 않게
        const float k_BoundThickness = 1f, k_BoundHeight = 4f;

        static void MoveBounds(Transform root)
        {
            var g = Group("MoveBounds", root);
            float zMid = (k_GateZ + k_NorthFront) * 0.5f, zLen = k_NorthFront - k_GateZ + k_BoundThickness * 2f, xLen = k_SideFront * 2f + k_BoundThickness * 2f;
            void Bound(string name, Vector3 center, Vector3 size)
            {
                var go = new GameObject(name);
                go.transform.SetParent(g, false);
                go.transform.localPosition = center;
                go.layer = 2;   // Ignore Raycast
                go.AddComponent<BoxCollider>().size = size;
            }
            float t = k_BoundThickness * 0.5f, y = k_BoundHeight * 0.5f - 0.5f;
            Bound("Bound_W", new Vector3(-k_SideFront - t, y, zMid), new Vector3(k_BoundThickness, k_BoundHeight, zLen));
            Bound("Bound_E", new Vector3(k_SideFront + t, y, zMid), new Vector3(k_BoundThickness, k_BoundHeight, zLen));
            Bound("Bound_N", new Vector3(0f, y, k_NorthFront + t), new Vector3(xLen, k_BoundHeight, k_BoundThickness));
            Bound("Bound_S", new Vector3(0f, y, k_GateZ - t), new Vector3(xLen, k_BoundHeight, k_BoundThickness));   // 정문 포함
        }

        // 담장 안 나무 · 이동 경계만 다시 놓기 — 허브 전체를 다시 짓지 않고 (11/10)
        [MenuItem("A/만들기/허브 담장 안 나무 · 이동 경계")]
        static void BuildTreesAndBounds()
        {
            var root = GameObject.Find(k_Root);
            if (root == null)
            {
                Debug.LogError($"[A] {k_Root} 가 없다 — 허브 씬을 열고 메뉴 'A/만들기/허브'를 먼저 누른다");
                return;
            }
            Undo.SetCurrentGroupName("A: 허브 나무 · 이동 경계");
            int group = Undo.GetCurrentGroup();
            foreach (var name in new[] { "Trees", "MoveBounds" })
            {
                Transform old = root.transform.Find(name);
                if (old != null)
                    Undo.DestroyObjectImmediate(old.gameObject);
            }
            Trees(root.transform);
            MoveBounds(root.transform);
            foreach (var name in new[] { "Trees", "MoveBounds" })
            {
                Transform t = root.transform.Find(name);
                Undo.RegisterCreatedObjectUndo(t.gameObject, "A: 허브 나무 · 이동 경계");
                foreach (var c in t.GetComponentsInChildren<Transform>())
                    GameObjectUtility.SetStaticEditorFlags(c.gameObject, StaticEditorFlags.BatchingStatic);
            }
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log($"[A] 허브 담장 안 나무 · 이동 경계 (x ±{k_SideFront} · z {k_GateZ} ~ {k_NorthFront})");
        }

        const int k_OutsideTrees = 48;   // 원래 16그루 + 48 = 64 (4배)

        // 건물 바닥 사각형 (월드 x · z)
        static List<Rect> BuildingFootprints()
        {
            var list = new List<Rect>();
            Vector3 s = k_BuildingSize;
            foreach (var b in k_Buildings)
            {
                Vector3 c = b.front + Quaternion.Euler(0f, b.yaw, 0f) * new Vector3(0f, 0f, -s.z * 0.5f);
                bool turned = Mathf.Abs(Mathf.Sin(b.yaw * Mathf.Deg2Rad)) > 0.5f;
                float w = turned ? s.z : s.x, d = turned ? s.x : s.z;
                list.Add(new Rect(c.x - w * 0.5f, c.z - d * 0.5f, w, d));
            }
            return list;
        }

        // 연병장 가운데(0, 0)에서 나무까지 선이 건물을 지나면 가린다 (1 m 간격으로 본다)
        static bool HiddenByBuilding(Vector2 tree, List<Rect> footprints)
        {
            float length = tree.magnitude;
            for (float t = 0f; t < length; t += 1f)
            {
                Vector2 p = tree * (t / length);
                foreach (var r in footprints)
                    if (r.Contains(p))
                        return true;
            }
            return false;
        }

        // ── 남쪽 산 (11/09 사용자: 정문 너머가 휑함 → 산, 방법 ① 산 메시 직접 생성) ──
        // 정문 남쪽 약 130 m 부터 산기슭, 봉우리 몇 개가 겹친 능선 (높이 40 ~ 110 m). 정문 길이 지나가는 x 0 쪽은 골짜기로 비워 길이 산 사이로 이어진다.
        // 높이 = 봉우리(가우스 언덕) 합 + 낮은 능선 + 펄린 잡음. 메시 Models/Hub/Mountain.asset (5 m 칸), 재질 Hub_Mountain(임시 — 숲 얼룩 그림을 빌더가 그림).
        // 산기슭에 나무 프리팹을 크게 흩뿌린다
        const float k_MountainX = 360f, k_MountainZ0 = -120f, k_MountainZ1 = -460f, k_MountainCell = 5f;
        const float k_ValleyHalf = 12f, k_ValleyBlend = 90f, k_ValleyEnd = -300f;   // 길 골짜기 — 바닥 폭 · 산으로 올라가는 폭

        static readonly (float x, float z, float height, float sx, float sz)[] k_Peaks =
        {
            (-170f, -300f, 105f, 110f, 90f),
            (-70f, -230f, 55f, 60f, 50f),
            (-25f, -330f, 70f, 70f, 50f),    // 골짜기를 닫는 봉우리 (길 끝 뒤)
            (110f, -270f, 90f, 90f, 75f),
            (230f, -220f, 60f, 80f, 60f),
            (-290f, -250f, 75f, 90f, 80f),
            (30f, -400f, 110f, 150f, 80f),   // 골짜기 끝 — 길이 그 아래로 사라진다
            (300f, -340f, 95f, 100f, 90f),
        };

        static float MountainHeight(float x, float z)
        {
            float foot = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(k_MountainZ0, k_MountainZ0 - 60f, z));   // 기슭에서 0 → 1
            if (foot <= 0f)
                return 0f;
            float h = 0f;
            foreach (var p in k_Peaks)
            {
                float dx = (x - p.x) / p.sx, dz = (z - p.z) / p.sz;
                h += p.height * Mathf.Exp(-(dx * dx + dz * dz));
            }
            h += 22f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(k_MountainZ0 - 20f, k_MountainZ0 - 130f, z));   // 낮은 능선 — 봉우리 사이를 메운다
            float n = 0f, amp = 1f, freq = 1f / 90f;
            for (int o = 0; o < 4; o++, amp *= 0.5f, freq *= 2f)
                n += (Mathf.PerlinNoise(x * freq + 37.1f, z * freq + 11.3f) - 0.5f) * amp;
            h *= 1f + 0.55f * n;
            h += 6f * n;
            // 길 골짜기 — 바닥은 0, 양옆은 완만하게. 안쪽(k_ValleyEnd 너머)에서는 골짜기가 닫혀 길이 산 뒤로 사라진다
            float valley = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(k_ValleyHalf, k_ValleyHalf + k_ValleyBlend, Mathf.Abs(x)));
            float close = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(k_ValleyEnd + 40f, k_ValleyEnd - 40f, z));
            h *= Mathf.Lerp(valley, 1f, close);
            return Mathf.Max(0f, h * foot) - 0.06f;   // 바닥(0)보다 살짝 아래 — 길 · 땅이 위에 그려진다
        }

        static void Mountains(Transform root)
        {
            var g = Group("Mountains", root);
            int nx = Mathf.RoundToInt(k_MountainX * 2f / k_MountainCell), nz = Mathf.RoundToInt((k_MountainZ0 - k_MountainZ1) / k_MountainCell);
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int j = 0; j <= nz; j++)
                for (int i = 0; i <= nx; i++)
                {
                    float x = -k_MountainX + i * k_MountainCell, z = k_MountainZ0 - j * k_MountainCell;
                    vertices.Add(new Vector3(x, MountainHeight(x, z), z));
                    uvs.Add(new Vector2(x, z) / 40f);   // 그림 한 장 = 40 m
                }
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    int a = j * (nx + 1) + i, b = a + 1, c = a + nx + 1, d = c + 1;
                    triangles.AddRange(new[] { a, b, c, b, d, c });
                }
            var mesh = new Mesh { name = "Mountain", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject("Mountain", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(g, false);
            go.GetComponent<MeshFilter>().sharedMesh = SaveMesh(mesh);
            go.GetComponent<MeshRenderer>().sharedMaterial = MountainMaterial();
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // 먼 산이 낮은 노을 해를 가려 부대 전체가 그늘지지 않게

            // 기슭 나무 — 낮고 완만한 곳, 길 골짜기 밖
            var rng = new System.Random(11092);
            string[] prefabs = { "Tree_Green_1", "Tree_Green_2", "Tree_Green_3" };
            var trees = Group("MountainTrees", g);
            var placed = new List<Vector2>();
            int tries = 0;
            while (placed.Count < k_MountainTrees && tries++ < 20000)
            {
                var c = new Vector2(Mathf.Lerp(-200f, 200f, (float)rng.NextDouble()), Mathf.Lerp(k_MountainZ0 - 5f, k_MountainZ0 - 110f, (float)rng.NextDouble()));
                if (Mathf.Abs(c.x) < k_ValleyHalf + 6f || placed.Exists(o => (o - c).sqrMagnitude < 64f))
                    continue;
                float h = MountainHeight(c.x, c.y);
                if (h > 30f)
                    continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/A/Models/Trees/Prefabs/{prefabs[placed.Count % prefabs.Length]}.prefab");
                if (prefab == null)
                    break;
                var t = (GameObject)PrefabUtility.InstantiatePrefab(prefab, trees);
                t.transform.position = new Vector3(c.x, h, c.y);
                t.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                t.transform.localScale = Vector3.one * (1.6f + 0.8f * (float)rng.NextDouble());
                foreach (var tr in t.GetComponentsInChildren<Renderer>())
                    tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                placed.Add(c);
            }
        }

        const int k_MountainTrees = 70;

        // ── 둘레 낮은 산 (11/09 사용자: 남쪽 산이 좋아서 다른 방향도 조금 낮은 산으로 둘러싸게) ──
        // 부대 가운데(0, 10)에서 거리 k_RingFoot 부터 산기슭, 높이 25 ~ 65 m 의 능선이 빙 둘러선다 (각도에 따라 봉우리 · 안부가 번갈아).
        // 남쪽 큰 산이 있는 곳(z < -100, |x| < 330)은 비워 두고 그 양 끝과 이어진다. 메시 Models/Hub/RingMountains.asset (8 m 칸), 재질은 남쪽 산과 같다
        const float k_RingFoot = 150f, k_RingRise = 90f, k_RingExtent = 480f, k_RingCell = 8f;
        const int k_RingTrees = 90;

        static float RingHeight(float x, float z)
        {
            float dx = x, dz = z - 10f;
            float r = Mathf.Sqrt(dx * dx + dz * dz);
            float foot = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(k_RingFoot, k_RingFoot + k_RingRise, r));
            if (foot <= 0f)
                return -0.06f;
            float angle = Mathf.Atan2(dz, dx);
            // 능선 높이 — 각도 따라 봉우리 (낮은 주파수 잡음 두 겹)
            float ridge = 25f + 40f * Mathf.PerlinNoise(Mathf.Cos(angle) * 2.2f + 5.3f, Mathf.Sin(angle) * 2.2f + 9.1f);
            ridge *= 0.8f + 0.4f * Mathf.PerlinNoise(Mathf.Cos(angle) * 6f + 21f, Mathf.Sin(angle) * 6f + 3f);
            float n = 0f, amp = 1f, freq = 1f / 70f;
            for (int o = 0; o < 4; o++, amp *= 0.5f, freq *= 2f)
                n += (Mathf.PerlinNoise(x * freq + 71.7f, z * freq + 43.9f) - 0.5f) * amp;
            float h = ridge * foot * (1f + 0.5f * n) + 5f * n * foot;
            // 남쪽 큰 산 자리는 비운다 (가장자리는 부드럽게)
            float southX = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(300f, 340f, Mathf.Abs(x)));
            float southZ = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-130f, -90f, z));
            h *= Mathf.Max(southX, southZ);
            return Mathf.Max(0f, h) - 0.06f;
        }

        static void RingMountains(Transform root)
        {
            var g = Group("RingMountains", root);
            int n = Mathf.RoundToInt(k_RingExtent * 2f / k_RingCell);
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int j = 0; j <= n; j++)
                for (int i = 0; i <= n; i++)
                {
                    float x = -k_RingExtent + i * k_RingCell, z = -k_RingExtent + 10f + j * k_RingCell;
                    vertices.Add(new Vector3(x, RingHeight(x, z), z));
                    uvs.Add(new Vector2(x, z) / 40f);
                }
            // 남쪽 산 메시와 같은 감김 — z 가 줄어드는 순서였으므로 여기(z 증가)는 반대로
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    int a = j * (n + 1) + i, b = a + 1, c = a + n + 1, d = c + 1;
                    triangles.AddRange(new[] { a, c, b, b, c, d });
                }
            var mesh = new Mesh { name = "RingMountains", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject("RingMountains", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(g, false);
            go.GetComponent<MeshFilter>().sharedMesh = SaveMesh(mesh);
            go.GetComponent<MeshRenderer>().sharedMaterial = MountainMaterial();
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // 먼 산이 낮은 노을 해를 가려 부대 전체가 그늘지지 않게

            // 기슭 나무
            var rng = new System.Random(11093);
            string[] prefabs = { "Tree_Green_1", "Tree_Green_2", "Tree_Green_3" };
            var trees = Group("RingTrees", g);
            var placed = new List<Vector2>();
            int tries = 0;
            while (placed.Count < k_RingTrees && tries++ < 30000)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = k_RingFoot - 10f + (float)rng.NextDouble() * 60f;
                var c = new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r + 10f);
                float h = RingHeight(c.x, c.y);
                if (h > 22f || (c.y < -100f && Mathf.Abs(c.x) < 330f) || placed.Exists(o => (o - c).sqrMagnitude < 81f))
                    continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/A/Models/Trees/Prefabs/{prefabs[placed.Count % prefabs.Length]}.prefab");
                if (prefab == null)
                    break;
                var t = (GameObject)PrefabUtility.InstantiatePrefab(prefab, trees);
                t.transform.position = new Vector3(c.x, Mathf.Max(0f, h), c.y);
                t.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                t.transform.localScale = Vector3.one * (1.6f + 0.8f * (float)rng.NextDouble());
                foreach (var tr in t.GetComponentsInChildren<Renderer>())
                    tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                placed.Add(c);
            }
        }

        // 산 재질 (임시) — 짙은 초록 숲 얼룩 그림을 빌더가 그린다 (Materials/Hub/Mountain_Albedo.png, 없을 때만). 사용자 재질이 오면 바꾼다
        static Material MountainMaterial()
        {
            EnsureFolder();
            string texturePath = k_MaterialFolder + "/Mountain_Albedo.png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath) == null)
            {
                const int size = 512;
                var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
                var dark = new Color(0.10f, 0.19f, 0.09f);
                var light = new Color(0.22f, 0.32f, 0.14f);
                var brown = new Color(0.27f, 0.25f, 0.16f);
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        // 이음매 없이 반복되게 — 원통 좌표로 펄린 잡음
                        float u = (float)x / size, v = (float)y / size;
                        float n = 0f, amp = 0.5f;
                        for (int o = 0; o < 5; o++, amp *= 0.5f)
                        {
                            float f = 4f * (1 << o);
                            n += amp * Tileable(u, v, f);
                        }
                        Color c = Color.Lerp(dark, light, Mathf.SmoothStep(0.3f, 0.7f, n));
                        float patch = Tileable(u, v, 3f);
                        c = Color.Lerp(c, brown, Mathf.SmoothStep(0.62f, 0.8f, patch) * 0.5f);
                        tex.SetPixel(x, y, c);
                    }
                System.IO.File.WriteAllBytes(texturePath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(texturePath);
            }
            var material = Lit("Hub_Mountain", Color.white, 0.05f, 0f);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }

        // 반복되는 잡음 — 정수 주기 f 로 네 모서리 값을 섞는다
        static float Tileable(float u, float v, float f)
        {
            float a = Mathf.PerlinNoise(u * f, v * f), b = Mathf.PerlinNoise((u - 1f) * f, v * f);
            float c = Mathf.PerlinNoise(u * f, (v - 1f) * f), d = Mathf.PerlinNoise((u - 1f) * f, (v - 1f) * f);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        // 하늘 · 환경광 · 안개 — A_Sandbox 와 같은 노을 하늘 (11/03)
        static void Sky()
        {
            var sky = AssetDatabase.LoadAssetAtPath<Material>(k_SkyPath);
            if (sky != null)
                RenderSettings.skybox = sky;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1f;
            var sun = Object.FindFirstObjectByType<Light>();
            if (sun != null && sun.type == LightType.Directional)
                RenderSettings.sun = sun;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 60f;
            RenderSettings.fogEndDistance = 400f;
        }

        // ── 작은 도우미 ──
        static Material Tiled(string name, Material source, Vector2 tiling, Color tint)
        {
            string path = $"{k_MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                EnsureFolder();
                material = source != null ? new Material(source) { name = name } : new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTextureScale("_BaseMap", tiling);
            material.SetColor("_BaseColor", tint);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material Lit(string name, Color color, float smoothness, float metallic)
        {
            string path = $"{k_MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                EnsureFolder();
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Smoothness", smoothness);
                material.SetFloat("_Metallic", metallic);
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }

        static Material Outdoor(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{k_OutdoorFolder}/{name}.mat");

        static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(k_MaterialFolder))
                AssetDatabase.CreateFolder("Assets/A/Materials", "Hub");
        }

        static Transform Group(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static GameObject Box(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = size;
            if (material != null)
                go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        static void Cylinder(string name, Transform parent, Vector3 localPosition, float radius, float height, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            if (material != null)
                go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
