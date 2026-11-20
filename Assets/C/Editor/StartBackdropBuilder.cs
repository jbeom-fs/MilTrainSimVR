using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MilTrainSimVR.UrbanCombat.EditorTools
{
    // 시작 지점 뒤 배경 (11/17 사용자 — "실제 길목 + 집결지", 목업 확인 뒤 "이대로 넣기").
    // 시작 자리(80.9, 118.6, 북쪽 상가를 봄) 뒤는 보이지 않는 벽(z 112) 너머가 빈 땅과 지평선뿐이었다 → 실제 길목 + 아군 집결지로 채운다:
    //   · 바닥: 땅이 끝나는 z≈103 남쪽으로 인도, 가운데 골목은 아스팔트
    //   · 배경 건물: 맵의 닫힌 건물(N33 · N10 · N12 · N29)을 복사해 골목 양옆에, 앞면이 북쪽 도로를 보게. 골목 끝은 N08 복사로 막는다.
    //     복사본에선 스크립트(맵 빌더용 GreyboxBuildingSpec 등) · 조명 · 반사 프로브를 뗀다 — 겉모습 · 충돌체만 (총알이 맞게)
    //   · 아군 통제선: 골목 입구를 5 m 콘크리트+철조망 벽(9.2 m = 골목 폭)으로 막고 양옆에 모래주머니
    //   · 트럭: 군용 트럭 자리 — 지금은 파란 민간 트럭
    //   · 집결지: 총 탁자 위 차양, 탄약 상자 3개, 안내판 (지도 · 임무) — 아래 '집결지 (11/19 다듬기)'
    // 맵 만들기(C_Greybox 를 다시 짓는다)가 건드리지 않게 씬 루트 C_StartBackdrop 아래에 둔다. 다시 누르면 C_StartBackdrop 만 지우고 다시 짓는다. Ctrl+Z 로 되돌릴 수 있다.
    // 씬은 저장하지 않는다.
    static class StartBackdropBuilder
    {
        const string k_Root = "C_StartBackdrop";
        const string k_MaterialFolder = "Assets/C/Materials/Backdrop";
        static readonly Vector3 k_Table = new Vector3(84.4f, 0f, 121.1f);   // 총 탁자 (C_StartArea/RifleTable)

        [MenuItem("MilTrainSimVR/C/시작 지점 뒤 배경 만들기 (실제 길목 + 집결지)")]
        static void Build()
        {
            Transform buildings = GameObject.Find("C_Greybox")?.transform.Find("Buildings");
            Transform ground = GameObject.Find("C_Greybox")?.transform.Find("Ground");
            if (buildings == null || ground == null)
            {
                Debug.LogError("[C] 시작 지점 뒤 배경 — C_Greybox 의 Buildings · Ground 가 없다. C_UrbanBlock 을 열고 누를 것");
                return;
            }
            Undo.SetCurrentGroupName("시작 지점 뒤 배경 만들기");
            int group = Undo.GetCurrentGroup();
            GameObject old = GameObject.Find(k_Root);
            if (old != null)
                Undo.DestroyObjectImmediate(old);
            var root = new GameObject(k_Root).transform;
            Undo.RegisterCreatedObjectUndo(root.gameObject, "시작 지점 뒤 배경");

            Material asphalt = GroundMaterial(ground, "asphalt") ?? Mat("C_Backdrop_Asphalt", new Color(0.2f, 0.2f, 0.2f));
            Material paver = GroundMaterial(ground, "sidewalk") ?? Mat("C_Backdrop_Sidewalk", new Color(0.45f, 0.45f, 0.45f));
            Material sand = Mat("C_Backdrop_Sandbag", new Color(0.55f, 0.48f, 0.33f));

            // 바닥
            var floor = Group(root, "바닥");
            Box(floor, "인도 · 남쪽 블록", new Vector3(82f, -0.05f, 81f), new Vector3(120f, 0.1f, 44f), paver, 0f);
            Box(floor, "골목 · 아스팔트", new Vector3(81.5f, -0.04f, 85f), new Vector3(9f, 0.1f, 40f), asphalt, 0f);

            // 배경 건물 — 맵의 닫힌 건물 복사 (앞면이 북쪽 도로를 보게)
            var backdrop = Group(root, "배경 건물 (닫힌 건물 복사, 들어갈 수 없음)");
            CopyBuilding(buildings, backdrop, "B_N33", 63f, 94f, 180f);
            CopyBuilding(buildings, backdrop, "B_N10", 40f, 93f, 180f);
            CopyBuilding(buildings, backdrop, "B_N12", 99.5f, 93f, 180f);
            CopyBuilding(buildings, backdrop, "B_N29", 122f, 93f, 180f);
            CopyBuilding(buildings, backdrop, "B_N08", 81.5f, 66f, 90f);

            // 아군 통제선
            var line = Group(root, "아군 통제선 · 골목 입구");
            Place(line, "Assets/C/Prefabs/Barrier/C_ConcreteWallWire.prefab", new Vector3(81.5f, 0f, 104.5f), 0f);
            Sandbag(line, "모래주머니 · 왼쪽", new Vector3(75.8f, 0f, 107.2f), 20f, sand);
            Sandbag(line, "모래주머니 · 오른쪽", new Vector3(87.2f, 0f, 107.2f), -20f, sand);
            Place(line, "Assets/C/Prefabs/Vehicle/C_Vehicle_Truck_Blue.prefab", new Vector3(95.5f, 0f, 108.5f), 90f);

            // 집결지 — 총 탁자 둘레
            Camp(root);

            Undo.CollapseUndoOperations(group);
            Debug.Log("[C] 시작 지점 뒤 배경을 만들었다 (C_StartBackdrop — 씬은 저장하지 않음)");
        }

        // ---------------------------------------------------------------- 집결지 (11/19 다듬기)
        // 사용자 11/19: "차양기둥과 안내판 기둥을 실제 쇠 원기둥으로 · 차양천을 실제 느낌으로 · 탄약 상자는 보급 상자 느낌으로 ·
        // UI 로 뜨던 목표도 안내판 + 안내판 기둥 2개를 써서 큰 안내판으로 바꿔서 안내판 옆에 세워 줘".
        //   · 기둥: Unity 기본 원기둥 + 쇠 재질 (A 가 사열대 기둥에 쓴 방식 — A 의 강철 텍스처 Assets/A/Models/Steel 을 그대로 참조한다)
        //   · 차양: 다리 넷 + 위 테두리 파이프 + 가운데 가로대, 천은 두 칸으로 처지고 가장자리가 파이프를 넘어 주름져 늘어진다.
        //     A 쪽에 차양막 · 천은 없었다 (A 의 Canopy 는 문 위 단색 상자) → 천 그물 · 그림을 여기서 만든다
        //   · 탄약 상자: Tripo 로 만든 보급 상자 프리팹(C_SupplyCrate) — 없으면 예전 올리브 상자
        //   · 임무 안내판: 시작 전에 눈앞에 떠 있던 판(MatchDisplay 앞판)의 내용을 그림(TrainingBrief~/make_brief.py)으로 — 지도 안내판 옆
        const string k_CampName = "집결지 · 총 탁자 둘레";
        const string k_ModelFolder = "Assets/C/Models/Backdrop";
        const string k_TextureFolder = "Assets/C/Textures/Backdrop";
        const string k_CratePrefab = "Assets/C/Prefabs/Prop/C_SupplyCrate.prefab";

        static Transform Camp(Transform root)
        {
            Material olive = Mat("C_Backdrop_Olive", new Color(0.30f, 0.33f, 0.20f));
            Material board = Mat("C_Backdrop_SignBoard", new Color(0.15f, 0.20f, 0.14f));
            Material steel = SteelMat();
            var camp = Group(root, k_CampName);
            Awning(camp, steel);
            Crate(camp, "탄약 상자 1", new Vector3(-1.7f, 0f, -1.0f), 10f, 0, olive);
            Crate(camp, "탄약 상자 2", new Vector3(-1.7f, 0f, -1.0f), -5f, 1, olive);
            Crate(camp, "탄약 상자 3", new Vector3(1.9f, 0f, 1.1f), 30f, 0, olive);
            MapBoard(camp, board, steel);
            BriefBoard(camp, board, steel);
            return camp;
        }

        // 집결지만 다시 만든다 — 배경 건물 · 통제선 · 바닥은 그대로 둔다 (메뉴 없음: 한 번 부르는 용도. 씬은 저장하지 않는다)
        static void RebuildCamp()
        {
            GameObject root = GameObject.Find(k_Root);
            if (root == null)
            {
                Debug.LogError("[C] 집결지 다시 만들기 — C_StartBackdrop 이 없다. '시작 지점 뒤 배경 만들기'를 먼저 누를 것");
                return;
            }
            Undo.SetCurrentGroupName("집결지 다시 만들기");
            int group = Undo.GetCurrentGroup();
            Transform old = root.transform.Find(k_CampName);
            int index = old != null ? old.GetSiblingIndex() : root.transform.childCount;
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);
            Transform camp = Camp(root.transform);
            camp.SetSiblingIndex(index);
            Undo.RegisterCreatedObjectUndo(camp.gameObject, "집결지");
            Undo.CollapseUndoOperations(group);
        }

        // 쇠 재질 — A 의 강철 텍스처(얼룩진 아연도금)를 참조한다. 없으면 단색 금속
        static Material SteelMat()
        {
            string path = $"{k_MaterialFolder}/C_Backdrop_Steel.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            Mat("C_Backdrop_Olive", new Color(0.30f, 0.33f, 0.20f));   // 폴더가 없으면 만든다
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "C_Backdrop_Steel" };
            material.SetColor("_BaseColor", new Color(0.86f, 0.87f, 0.88f));
            material.SetFloat("_Metallic", 0.9f);
            material.SetFloat("_Smoothness", 0.62f);
            const string steel = "Assets/A/Models/Steel/Tile_DefaultMaterial_";
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(steel + "AlbedoTransparency.png");
            var metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(steel + "MetallicSmoothness.png");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(steel + "Normal.png");
            if (albedo != null)
            {
                material.SetTexture("_BaseMap", albedo);
                material.SetTextureScale("_BaseMap", new Vector2(1f, 4f));   // 기본 원기둥은 둘레 0~1 · 길이 0~1 — 길이 쪽으로 넉 장
            }
            if (metallic != null)
            {
                material.SetTexture("_MetallicGlossMap", metallic);
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                material.SetFloat("_Metallic", 1f);
                material.SetFloat("_Smoothness", 0.8f);   // 지도의 매끈함에 곱해진다
            }
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // 쇠 파이프 — a 에서 b 까지, 지름 d. Unity 기본 원기둥(높이 2, 지름 1)을 늘려 쓴다
        static GameObject Pipe(Transform parent, string name, Vector3 a, Vector3 b, float diameter, Material material, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation((a + b) * 0.5f, Quaternion.FromToRotation(Vector3.up, b - a));
            go.transform.localScale = new Vector3(diameter, (b - a).magnitude * 0.5f, diameter);
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider)
                Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        // 땅 높이 — 길 · 인도 어디에 서든 기둥 밑이 땅에 닿게
        static float GroundY(Vector3 at)
        {
            Physics.SyncTransforms();
            foreach (RaycastHit hit in Physics.RaycastAll(new Vector3(at.x, 3f, at.z), Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider.transform.root.name == "C_Greybox")
                    return hit.point.y;
            return 0f;
        }

        // ---------------------------------------------------------------- 차양
        const float k_AwningHalfX = 2.2f, k_AwningHalfZ = 1.6f;   // 다리 자리 (탁자 가운데에서)
        const float k_AwningTop = 2.5f;                           // 위 테두리 파이프의 꼭대기
        const float k_LegDiameter = 0.06f, k_BarDiameter = 0.045f;
        const float k_ClothGap = 0.004f;                          // 천과 파이프 사이
        const float k_ClothSag = 0.085f, k_ClothDrop = 0.26f;     // 칸 가운데가 처지는 깊이 · 가장자리가 늘어진 길이

        static void Awning(Transform camp, Material steel)
        {
            var awning = Group(camp, "차양");
            Vector3 c = k_Table;
            float barY = k_AwningTop - k_BarDiameter * 0.5f;
            float hx = k_AwningHalfX, hz = k_AwningHalfZ, over = 0.07f;
            foreach (Vector3 o in new[] { new Vector3(-hx, 0, -hz), new Vector3(hx, 0, -hz), new Vector3(-hx, 0, hz), new Vector3(hx, 0, hz) })
            {
                float ground = GroundY(c + o);
                Pipe(awning, "차양 기둥", c + o + Vector3.up * ground, c + o + Vector3.up * (barY - 0.005f), k_LegDiameter, steel);
                Pipe(awning, "차양 기둥 받침", c + o + Vector3.up * ground, c + o + Vector3.up * (ground + 0.012f), 0.2f, steel, false);
            }
            foreach (float z in new[] { -hz, hz })
                Pipe(awning, "차양 가로대", c + new Vector3(-hx - over, barY, z), c + new Vector3(hx + over, barY, z), k_BarDiameter, steel, false);
            foreach (float x in new[] { -hx, 0f, hx })
                Pipe(awning, "차양 가로대", c + new Vector3(x, barY, -hz - over), c + new Vector3(x, barY, hz + over), k_BarDiameter, steel, false);

            var cloth = new GameObject("차양 천");
            cloth.transform.SetParent(awning, false);
            cloth.transform.position = c;
            cloth.AddComponent<MeshFilter>().sharedMesh = AwningClothMesh();
            var renderer = cloth.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = CanvasMat();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            var box = cloth.AddComponent<BoxCollider>();   // 총알이 천에 맞게 (얇은 판)
            box.center = new Vector3(0f, k_AwningTop - k_ClothSag * 0.5f, 0f);
            box.size = new Vector3(hx * 2f, 0.03f, hz * 2f);
        }

        // 천 그물 — 원점 = 탁자 가운데 땅. 윗판은 테두리 파이프 위에 얹혀 가운데 가로대 양쪽 두 칸이 처지고,
        // 네 변은 파이프를 넘어 k_ClothDrop 만큼 늘어지며 아래로 갈수록 주름이 깊어진다 (모서리는 터 놓는다). 양면은 재질에서 (Render Face = Both)
        static Mesh AwningClothMesh()
        {
            string path = $"{k_ModelFolder}/C_AwningCloth.asset";
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var tris = new List<int>();
            float hx = k_AwningHalfX, hz = k_AwningHalfZ;
            float r = k_BarDiameter * 0.5f + k_ClothGap;          // 파이프를 감는 반지름
            float barY = k_AwningTop - k_BarDiameter * 0.5f, top = barY + r;
            float texel = 1f / (2f * hx);                          // 그림 한 장 = 긴 변 길이 (가로 · 세로 같은 촘촘함)

            // 윗판 (10 cm 칸)
            int nx = Mathf.RoundToInt(2f * hx / 0.1f), nz = Mathf.RoundToInt(2f * hz / 0.1f);
            for (int j = 0; j <= nz; j++)
                for (int i = 0; i <= nx; i++)
                {
                    float x = Mathf.Lerp(-hx, hx, (float)i / nx), z = Mathf.Lerp(-hz, hz, (float)j / nz);
                    float p = 2f * Mathf.Abs(x) / hx - 1f, q = z / hz;                      // 칸 안 자리 (-1 ~ 1)
                    float hold = (1f - p * p) * (1f - q * q);                                 // 파이프 위는 0
                    float sag = k_ClothSag * Mathf.Pow(hold, 0.85f);
                    float wrinkle = 0.012f * (Mathf.PerlinNoise(x * 1.9f + 11.3f, z * 1.9f + 4.7f) - 0.5f) * hold;
                    v.Add(new Vector3(x, top - sag + wrinkle, z));
                    uv.Add(new Vector2((x + hx) * texel, (z + hz) * texel));
                }
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    int a = j * (nx + 1) + i, b = a + 1, c = a + nx + 1, d = c + 1;
                    tris.AddRange(new[] { a, c, b, b, c, d });
                }

            // 늘어진 가장자리 네 장 — side: 0 = +z, 1 = -z (긴 변), 2 = +x, 3 = -x (짧은 변)
            const float corner = 0.075f, wave = 0.5f, depth = 0.042f;
            const int arcSteps = 3, dropSteps = 7;
            float arc = Mathf.PI * 0.5f * r;
            for (int side = 0; side < 4; side++)
            {
                bool alongX = side < 2;
                float half = alongX ? hx : hz, edge = alongX ? hz : hx, sign = side % 2 == 0 ? 1f : -1f;
                float vBase = alongX ? 0.76f : 0.86f;                                         // 그림의 띠 두 줄 (긴 변 · 짧은 변)
                int nt = Mathf.RoundToInt((2f * half - 2f * corner) / 0.05f);
                int start = v.Count, ns = arcSteps + dropSteps;
                float phase = side * 1.7f;
                for (int k = 0; k <= ns; k++)
                    for (int i = 0; i <= nt; i++)
                    {
                        float t = Mathf.Lerp(-half + corner, half - corner, (float)i / nt);
                        float outward, y, s;
                        if (k <= arcSteps)
                        {
                            float angle = Mathf.PI * 0.5f * k / arcSteps;                     // 파이프 꼭대기(0)에서 바깥 옆(90°)까지
                            outward = Mathf.Sin(angle) * r;
                            y = barY + Mathf.Cos(angle) * r;
                            s = arc * k / arcSteps;
                        }
                        else
                        {
                            float d = (float)(k - arcSteps) / dropSteps;                      // 0 = 파이프, 1 = 밑단
                            float fold = Mathf.SmoothStep(0f, 1f, d);
                            float w = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * t / wave + phase) + 0.25f * Mathf.Sin(2f * Mathf.PI * t / (wave * 0.37f) + phase * 2.3f);
                            outward = r + depth * fold * Mathf.Max(0f, w);
                            y = barY - k_ClothDrop * d + 0.012f * fold * Mathf.Sin(2f * Mathf.PI * t / wave + phase + 1.2f);
                            s = arc + k_ClothDrop * d;
                        }
                        float off = sign * (edge + outward);
                        v.Add(alongX ? new Vector3(t, y, off) : new Vector3(off, y, t));
                        uv.Add(new Vector2((t + half) * texel, vBase + s * texel));
                    }
                bool flip = (side == 0) || (side == 3);
                for (int k = 0; k < ns; k++)
                    for (int i = 0; i < nt; i++)
                    {
                        int a = start + k * (nt + 1) + i, b = a + 1, c = a + nt + 1, d = c + 1;
                        tris.AddRange(flip ? new[] { a, b, c, b, d, c } : new[] { a, c, b, b, c, d });
                    }
            }

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew)
                mesh = new Mesh { name = "C_AwningCloth" };
            mesh.Clear();
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            if (isNew)
            {
                EnsureFolder(k_ModelFolder);
                AssetDatabase.CreateAsset(mesh, path);
            }
            else
                EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static void EnsureFolder(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder));
        }

        // 천 재질 — 올리브색 범포: 바탕 그림(얼룩 · 박음질 · 밑단 · 아일릿) + 주름 노멀 + 올(직조) 디테일. 양면
        static Material CanvasMat()
        {
            string path = $"{k_MaterialFolder}/C_Backdrop_AwningCanvas.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            CanvasTextures(out Texture2D baseMap, out Texture2D normal, out Texture2D weave, out Texture2D weaveNormal);
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "C_Backdrop_AwningCanvas" };
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", baseMap);
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP");
            material.SetFloat("_Smoothness", 0.12f);
            material.SetFloat("_Metallic", 0f);
            float tiles = 2f * k_AwningHalfX / 0.04f;   // 올 그림 한 장 = 4 cm
            material.SetTexture("_DetailAlbedoMap", weave);
            material.SetTextureScale("_DetailAlbedoMap", new Vector2(tiles, tiles));
            material.SetTexture("_DetailNormalMap", weaveNormal);
            material.SetFloat("_DetailAlbedoMapScale", 1f);
            material.SetFloat("_DetailNormalMapScale", 0.8f);
            material.EnableKeyword("_DETAIL_MULX2");
            material.SetFloat("_Cull", 0f);             // 양면 — 아래에서 올려다본다
            material.doubleSidedGI = true;
            // 천은 빛이 조금 비친다 — 그늘진 아랫면이 새까맣지 않게 바탕 그림으로 아주 약하게 빛을 낸다
            material.SetTexture("_EmissionMap", baseMap);
            material.SetColor("_EmissionColor", new Color(0.075f, 0.075f, 0.06f));
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            Mat("C_Backdrop_Olive", new Color(0.30f, 0.33f, 0.20f));   // 폴더가 없으면 만든다
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // 천 그림 넉 장을 만들어 저장한다 (있으면 그대로 쓴다). 그림 한 장 = 긴 변 4.4 m: 아래 0~0.727 이 윗판, 0.76~ / 0.86~ 두 띠가 늘어진 가장자리
        static void CanvasTextures(out Texture2D baseMap, out Texture2D normal, out Texture2D weave, out Texture2D weaveNormal)
        {
            string basePath = $"{k_TextureFolder}/C_AwningCanvas_Base.png", normalPath = $"{k_TextureFolder}/C_AwningCanvas_Normal.png";
            string weavePath = $"{k_TextureFolder}/C_CanvasWeave_Detail.png", weaveNormalPath = $"{k_TextureFolder}/C_CanvasWeave_Normal.png";
            if (!File.Exists(basePath) || !File.Exists(normalPath) || !File.Exists(weavePath) || !File.Exists(weaveNormalPath))
            {
                EnsureFolder(k_TextureFolder);
                const int n = 1024;
                float metres = 2f * k_AwningHalfX, topV = k_AwningHalfZ / k_AwningHalfX;   // 윗판이 차지하는 세로 (0.727)
                var colour = new Color[n * n];
                var height = new float[n * n];
                var rng = new System.Random(1119);
                // 당겨진 주름 — 파이프 쪽 점에서 칸 가운데 쪽으로 뻗는 골 · 마루 몇 줄
                var creases = new List<(Vector2 a, Vector2 b, float width, float depth)>();
                for (int k = 0; k < 26; k++)
                {
                    float bay = k % 2 == 0 ? -0.5f : 0.5f;                                   // 칸 가운데 (x = ±1.1 m)
                    var centre = new Vector2((bay * k_AwningHalfX + k_AwningHalfX) / metres, topV * 0.5f);
                    float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                    var from = centre + new Vector2(Mathf.Cos(angle) * 0.25f, Mathf.Sin(angle) * topV * 0.5f);
                    var to = Vector2.Lerp(from, centre, 0.35f + 0.5f * (float)rng.NextDouble());
                    creases.Add((from, to, 0.006f + 0.01f * (float)rng.NextDouble(), (rng.NextDouble() < 0.5 ? -1f : 1f) * (0.4f + 0.6f * (float)rng.NextDouble())));
                }
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float u = (x + 0.5f) / n, vv = (y + 0.5f) / n;
                        float mx = u * metres, mz = vv * metres;                              // 미터
                        bool isTop = vv < topV + 0.004f;
                        bool isFlap = (vv >= 0.755f && vv < 0.835f) || (vv >= 0.855f && vv < 0.935f);
                        float flapS = isFlap ? (vv - (vv < 0.85f ? 0.76f : 0.86f)) * metres : 0f;   // 파이프에서 내려온 길이 (m)
                        // 바탕색: 올리브 범포 + 큰 얼룩 · 볕에 바랜 자리 · 잔 얼룩
                        float big = Mathf.PerlinNoise(mx * 0.55f + 3.1f, mz * 0.55f + 8.2f);
                        float mid = Mathf.PerlinNoise(mx * 2.3f + 17f, mz * 2.3f + 5f);
                        float fine = Mathf.PerlinNoise(mx * 11f + 2f, mz * 11f + 31f);
                        float shade = 0.84f + 0.2f * big + 0.1f * (mid - 0.5f) + 0.06f * (fine - 0.5f);
                        var c = new Color(0.50f, 0.52f, 0.35f) * shade;
                        c = Color.Lerp(c, new Color(0.62f, 0.60f, 0.44f), Mathf.Clamp01((big - 0.55f) * 1.6f) * 0.55f);   // 바랜 자리는 누렇게
                        float h = 0.5f + 0.25f * (big - 0.5f) + 0.12f * (mid - 0.5f);        // 큰 굴곡
                        if (isTop)
                        {
                            // 물이 고였던 자리 — 칸 가운데가 어둡고 테두리에 물때
                            foreach (float bay in new[] { 0.25f, 0.75f })
                            {
                                float d = new Vector2((u - bay) * 2.4f, (vv - topV * 0.5f) / topV * 1.6f).magnitude;
                                c *= 1f - 0.16f * Mathf.Clamp01(1f - d * 2.2f);
                                float ring = Mathf.Abs(d - 0.42f - 0.06f * (mid - 0.5f));
                                c = Color.Lerp(c, new Color(0.64f, 0.62f, 0.48f), Mathf.Clamp01(1f - ring * 40f) * 0.22f);
                            }
                            // 폭 1.1 m 천을 이은 박음질 (가운데는 가로대 위)
                            foreach (float seam in new[] { 0.25f, 0.5f, 0.75f })
                            {
                                float d = Mathf.Abs(u - seam) * metres;
                                if (d < 0.02f)
                                {
                                    c *= 0.9f;
                                    h += 0.1f;
                                    if (Mathf.Abs(d - 0.012f) < 0.0025f && Mathf.Repeat(mz, 0.012f) < 0.008f)
                                        c = Color.Lerp(c, new Color(0.78f, 0.76f, 0.6f), 0.7f);   // 실땀
                                }
                            }
                            // 파이프에 닿은 테두리 · 가운데 가로대 자리 — 닳아서 밝다
                            float edge = Mathf.Min(Mathf.Min(mx, metres - mx), Mathf.Min(mz, topV * metres - mz));
                            float rub = Mathf.Min(edge, Mathf.Abs(mx - metres * 0.5f));
                            c = Color.Lerp(c, new Color(0.64f, 0.64f, 0.5f), Mathf.Clamp01(1f - rub / 0.03f) * 0.35f);
                            foreach (var cr in creases)
                            {
                                Vector2 p = new Vector2(u, vv), ab = cr.b - cr.a;
                                float t = Mathf.Clamp01(Vector2.Dot(p - cr.a, ab) / ab.sqrMagnitude);
                                float d = (p - (cr.a + ab * t)).magnitude;
                                h += cr.depth * 0.5f * Mathf.Exp(-d * d / (cr.width * cr.width)) * Mathf.Sin(t * Mathf.PI);
                            }
                        }
                        else if (isFlap)
                        {
                            float hem = k_ClothDrop + Mathf.PI * 0.25f * (k_BarDiameter + 2f * k_ClothGap) - flapS;   // 밑단까지 남은 길이
                            // 빗물 자국 — 세로 줄
                            float streak = Mathf.PerlinNoise(mx * 9f + 40f, 0.5f);
                            c *= 1f - 0.18f * Mathf.Clamp01((streak - 0.55f) * 3f) * Mathf.Clamp01(flapS / 0.1f);
                            if (hem < 0.035f && hem > -0.01f)
                            {
                                c *= 0.86f;                                                   // 접어 박은 밑단
                                h += 0.12f;
                                if (Mathf.Abs(hem - 0.028f) < 0.0025f && Mathf.Repeat(mx, 0.012f) < 0.008f)
                                    c = Color.Lerp(c, new Color(0.78f, 0.76f, 0.6f), 0.7f);
                            }
                            // 아일릿(놋쇠 고리) — 파이프 바로 아래, 55 cm 마다
                            float gx = Mathf.Repeat(mx + 0.275f, 0.55f) - 0.275f, gy = flapS - 0.075f;
                            float gd = Mathf.Sqrt(gx * gx + gy * gy);
                            if (gd < 0.014f)
                            {
                                c = gd < 0.007f ? new Color(0.05f, 0.05f, 0.04f) : new Color(0.62f, 0.5f, 0.24f);
                                h += gd < 0.007f ? -0.3f : 0.25f;
                            }
                        }
                        else
                            c = new Color(0.48f, 0.50f, 0.33f) * shade;                       // 안 쓰는 자리 (경계 번짐 대비 같은 색)
                        c.a = 1f;
                        colour[y * n + x] = c;
                        height[y * n + x] = h;
                    }
                WriteTexture(basePath, n, colour);
                WriteTexture(normalPath, n, NormalsFromHeight(height, n, 5f, false));

                // 올 — 평직: 가로실 · 세로실이 번갈아 위로. 한 장 4 cm 에 12 올
                const int m = 256, threads = 12;
                var weaveColour = new Color[m * m];
                var weaveHeight = new float[m * m];
                for (int y = 0; y < m; y++)
                    for (int x = 0; x < m; x++)
                    {
                        float fu = (x + 0.5f) / m * threads, fv = (y + 0.5f) / m * threads;
                        bool warpOnTop = ((int)fu + (int)fv) % 2 == 0;
                        float across = warpOnTop ? Mathf.Sin(Mathf.PI * Mathf.Repeat(fu, 1f)) : Mathf.Sin(Mathf.PI * Mathf.Repeat(fv, 1f));
                        float along = warpOnTop ? Mathf.Sin(Mathf.PI * Mathf.Repeat(fv, 1f)) : Mathf.Sin(Mathf.PI * Mathf.Repeat(fu, 1f));
                        float h = Mathf.Pow(across, 0.6f) * (0.55f + 0.45f * along);
                        weaveHeight[y * m + x] = h;
                        float g = Mathf.LinearToGammaSpace(0.2140f * (0.72f + 0.5f * h));     // 중간 회색(변화 없음) 둘레로
                        weaveColour[y * m + x] = new Color(g, g, g, 1f);
                    }
                WriteTexture(weavePath, m, weaveColour);
                WriteTexture(weaveNormalPath, m, NormalsFromHeight(weaveHeight, m, 1.6f, true));
                AssetDatabase.Refresh();
                foreach (string p in new[] { normalPath, weaveNormalPath })
                {
                    var importer = (TextureImporter)AssetImporter.GetAtPath(p);
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.SaveAndReimport();
                }
                foreach (string p in new[] { basePath, normalPath })
                {
                    var importer = (TextureImporter)AssetImporter.GetAtPath(p);
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.SaveAndReimport();
                }
            }
            baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath);
            normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            weave = AssetDatabase.LoadAssetAtPath<Texture2D>(weavePath);
            weaveNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(weaveNormalPath);
        }

        // 높이 그림 → 노멀 그림 (탄젠트 공간, +Y 위). wrap = 이어 붙는 그림
        static Color[] NormalsFromHeight(float[] height, int n, float strength, bool wrap)
        {
            var result = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int x0 = wrap ? (x + n - 1) % n : Mathf.Max(x - 1, 0), x1 = wrap ? (x + 1) % n : Mathf.Min(x + 1, n - 1);
                    int y0 = wrap ? (y + n - 1) % n : Mathf.Max(y - 1, 0), y1 = wrap ? (y + 1) % n : Mathf.Min(y + 1, n - 1);
                    float dx = (height[y * n + x1] - height[y * n + x0]) * strength, dy = (height[y1 * n + x] - height[y0 * n + x]) * strength;
                    Vector3 normal = new Vector3(-dx, -dy, 1f).normalized;
                    result[y * n + x] = new Color(normal.x * 0.5f + 0.5f, normal.y * 0.5f + 0.5f, normal.z * 0.5f + 0.5f, 1f);
                }
            return result;
        }

        static void WriteTexture(string path, int n, Color[] pixels)
        {
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        // ---------------------------------------------------------------- 탄약 상자
        // 보급 상자 프리팹(뿌리 = 바닥 가운데)을 놓는다. stack = 몇 층째 (0 = 땅). 프리팹이 없으면 예전 올리브 상자
        static void Crate(Transform camp, string name, Vector3 offset, float yaw, int stack, Material fallback)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_CratePrefab);
            Vector3 at = k_Table + offset;
            at.y = GroundY(at);
            if (prefab == null)
            {
                Box(camp, name, at + Vector3.up * (0.25f + 0.5f * stack), new Vector3(0.9f, 0.5f, 0.45f), fallback, yaw);
                return;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, camp.gameObject.scene);
            go.name = name;
            go.transform.SetParent(camp, true);
            float height = 0f;
            foreach (Renderer r in prefab.GetComponentsInChildren<Renderer>())
                height = Mathf.Max(height, r.bounds.max.y);
            go.transform.SetPositionAndRotation(at + Vector3.up * (height * stack), Quaternion.Euler(0f, yaw, 0f));
        }

        // 안내판 — 훈련장 지도 (사용자 11/17: 목표 · 지하도 / 지상 경로 · 보급만, 적 위치는 없음. 2000~10년대 안내도 느낌).
        // 지도 그림은 Assets/C/Images/Sign/TrainingMap~/make_map.py 로 만든다 — 차량 · 보급 같은 표시는 거기 map_layers.json 에 더하고 다시 만들면
        // 같은 텍스처(C_TrainingMap.png)가 바뀌어 판에 그대로 보인다. 판은 지도 그림 비율로 세로 1.4 m, 기둥은 판 뒤 (판 가운데를 뚫지 않게)
        const string k_MapTexture = "Assets/C/Images/Sign/C_TrainingMap.png";
        const string k_MapMaterial = "Assets/C/Materials/Sign/C_TrainingMap.mat";
        static readonly Vector3 k_BoardCenter = new Vector3(77.5f, 1.55f, 121.5f);
        const float k_BoardHeight = 1.4f, k_BoardThickness = 0.05f;
        const float k_BoardYaw = 150f;   // 앞면(+Z)이 출발 자리(80.9, 118.6) 쪽 — 사용자가 씬에서 돌려 둔 방향

        const float k_PostDiameter = 0.076f;   // 안내판 기둥 — 쇠 원기둥 (11/19 사용자. 그 전엔 10 cm 각기둥)

        static void MapBoard(Transform parent, Material boardMaterial, Material steel)
        {
            Quaternion rotation = Quaternion.Euler(0f, k_BoardYaw, 0f);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(k_MapTexture);
            float width = texture != null ? k_BoardHeight * texture.width / texture.height : 1.0f;
            Vector3 post = k_BoardCenter + rotation * Vector3.back * PostBehind;
            SignPost(parent, "안내판 기둥", post, k_BoardCenter.y + k_BoardHeight * 0.5f - 0.06f, steel);
            Clamps(parent, post, k_BoardCenter.y, k_BoardHeight, steel);
            GameObject board = Box(parent, "안내판", k_BoardCenter, new Vector3(width, k_BoardHeight, k_BoardThickness), boardMaterial, k_BoardYaw);
            if (texture == null)
            {
                Debug.LogWarning($"[C] 안내판 — 지도 그림 {k_MapTexture} 이 없어 빈 판으로 둔다");
                return;
            }
            Face(board, "지도", k_MapMaterial, "C_TrainingMap", texture);
        }

        // 임무 안내판 — 시작 전에 눈앞에 떠 있던 판의 내용 (11/19 사용자: "UI 로 뜨던 목표도 … 큰 안내판으로 바꿔서 안내판 옆에").
        // 그림은 Assets/C/Images/Sign/TrainingBrief~/make_brief.py — 적 수 · 시간 · 점령지 이름이 바뀌면 거기 brief.json 을 고치고 다시 만든다.
        // 판 가로 2.0 m (세로는 그림 비율), 지도 안내판을 마주 봤을 때 그 오른쪽에 같은 방향으로. 쇠 원기둥 둘이 판 뒤 양쪽에 선다
        const string k_BriefTexture = "Assets/C/Images/Sign/C_TrainingBrief.png";
        const string k_BriefMaterial = "Assets/C/Materials/Sign/C_TrainingBrief.mat";
        const float k_BriefWidth = 2.0f, k_BriefBottom = 0.95f, k_BriefGap = 0.3f;

        static void BriefBoard(Transform parent, Material boardMaterial, Material steel)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(k_BriefTexture);
            if (texture == null)
            {
                Debug.LogWarning($"[C] 임무 안내판 — 그림 {k_BriefTexture} 이 없어 세우지 않는다 (TrainingBrief~/make_brief.py --install)");
                return;
            }
            Quaternion rotation = Quaternion.Euler(0f, k_BoardYaw, 0f);
            float height = k_BriefWidth * texture.height / texture.width;
            var mapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(k_MapTexture);
            float mapWidth = mapTexture != null ? k_BoardHeight * mapTexture.width / mapTexture.height : 1.0f;
            // 판 앞면(+Z)을 마주 보면 판의 -X 쪽이 오른쪽
            Vector3 centre = k_BoardCenter + rotation * Vector3.left * (mapWidth * 0.5f + k_BriefGap + k_BriefWidth * 0.5f);
            centre.y = GroundY(centre) + k_BriefBottom + height * 0.5f;
            var group = Group(parent, "임무 안내판");
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 post = centre + rotation * new Vector3(side * (k_BriefWidth * 0.5f - 0.22f), 0f, -PostBehind);
                SignPost(group, "안내판 기둥", post, centre.y + height * 0.5f + 0.07f, steel);
                Clamps(group, post, centre.y, height, steel);
            }
            GameObject board = Box(group, "판", centre, new Vector3(k_BriefWidth, height, k_BoardThickness), boardMaterial, k_BoardYaw);
            Face(board, "임무", k_BriefMaterial, "C_TrainingBrief", texture);
        }

        // 판 뒷면에서 기둥 축까지
        static float PostBehind => k_BoardThickness * 0.5f + k_PostDiameter * 0.5f + 0.004f;

        // 안내판 기둥 — 쇠 원기둥 + 받침판. at = 기둥 축, top = 꼭대기 높이
        static void SignPost(Transform parent, string name, Vector3 at, float top, Material steel)
        {
            float ground = GroundY(at);
            Pipe(parent, name, new Vector3(at.x, ground, at.z), new Vector3(at.x, top, at.z), k_PostDiameter, steel);
            Pipe(parent, name + " 받침", new Vector3(at.x, ground, at.z), new Vector3(at.x, ground + 0.012f, at.z), 0.22f, steel, false);
        }

        // 판을 기둥에 무는 띠쇠 둘 (판 높이의 위 · 아래 1/4 자리)
        static void Clamps(Transform parent, Vector3 post, float boardCentreY, float boardHeight, Material steel)
        {
            foreach (float k in new[] { -0.28f, 0.28f })
            {
                GameObject clamp = Box(parent, "안내판 띠쇠", new Vector3(post.x, boardCentreY + boardHeight * k, post.z), new Vector3(k_PostDiameter + 0.05f, 0.04f, k_PostDiameter + 0.012f), steel, k_BoardYaw);
                Object.DestroyImmediate(clamp.GetComponent<Collider>());
            }
        }

        // 판 앞면에 그림을 붙인다 (판의 자식 사각형, 판 앞 1 mm). 재질이 없으면 만든다
        static void Face(GameObject board, string name, string materialPath, string materialName, Texture2D texture)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = materialName };
                material.SetTexture("_BaseMap", texture);
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_Smoothness", 0.25f);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(board.transform, false);
            quad.transform.localPosition = new Vector3(0f, 0f, 0.52f);           // 앞면 1 mm 앞 (판 두께 5 cm 의 0.02)
            quad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);      // 사각형 앞면이 판 앞(+Z)을 보게
            quad.GetComponent<Renderer>().sharedMaterial = material;
        }

        // 모래주머니 — 사용자가 만들어 둔 모래주머니 더미(Tripo SandbagWall, 백화점 안에 쓴 것)를 놓는다
        // (11/19 사용자: "C_ConcreteWallWire 의 모래주머니 2개는 내가 만들어놓은 모래주머니로 바꿔 줘"). 크기는 백화점 것과 같은 높이 1.1 m · 두께 0.6 m,
        // 길이는 예전 상자와 같은 2.6 m. at = 바닥 가운데. 모델이 없으면 예전 모래색 상자
        const string k_SandbagModel = "Assets/C/Models/Tripo/SandbagWall/sandbag_stack_3d_model.fbx";
        static readonly Vector3 k_SandbagSize = new Vector3(2.6f, 1.1f, 0.6f);

        static GameObject Sandbag(Transform parent, string name, Vector3 at, float yaw, Material fallback)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(k_SandbagModel);
            if (model == null)
                return Box(parent, name, at + Vector3.up * 0.6f, new Vector3(2.6f, 1.2f, 1.0f), fallback, yaw);
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            // 모델은 가져온 방향 그대로 둔다 (FBX 뿌리가 X 로 누워 있다 — 돌려 놓으면 더미가 옆으로 눕는다). 크기는 그 위 'Model' 에서 맞춘다
            Transform fit = new GameObject("Model").transform;
            fit.SetParent(root.transform, false);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, fit);
            Bounds b = Bounds(visual);
            fit.localScale = new Vector3(k_SandbagSize.x / b.size.x, k_SandbagSize.y / b.size.y, k_SandbagSize.z / b.size.z);
            b = Bounds(visual);
            fit.position -= new Vector3(b.center.x, b.min.y, b.center.z);   // 뿌리 = 바닥 가운데
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, k_SandbagSize.y * 0.5f, 0f);
            box.size = k_SandbagSize;
            root.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            return root;
        }

        static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material material, float yaw)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        static void Place(Transform parent, string path, Vector3 position, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[C] 시작 지점 뒤 배경 — {path} 가 없다");
                return;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        }

        // 맵 건물을 복사해 그 둘레 상자 가운데가 (x, z), 바닥이 0 이 되게. 스크립트 · 조명 · 반사 프로브는 뗀다
        static void CopyBuilding(Transform buildings, Transform parent, string name, float x, float z, float yaw)
        {
            Transform source = buildings.Find(name);
            if (source == null)
            {
                Debug.LogWarning($"[C] 시작 지점 뒤 배경 — 건물 {name} 이 없다");
                return;
            }
            GameObject copy = Object.Instantiate(source.gameObject, parent);
            copy.name = $"배경 · {name.Substring(2)} 복사";
            copy.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * source.rotation;
            StripToVisuals(copy);
            Bounds b = Bounds(copy);
            copy.transform.position += new Vector3(x - b.center.x, -b.min.y, z - b.center.z);
        }

        static void StripToVisuals(GameObject go)
        {
            // 의존 관계(RequireComponent) 때문에 한 번에 안 지워지는 것이 있어 몇 번 돈다
            for (int pass = 0; pass < 4; pass++)
            {
                foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                    Object.DestroyImmediate(mb);
            }
            foreach (Light light in go.GetComponentsInChildren<Light>(true))
                Object.DestroyImmediate(light);
            foreach (ReflectionProbe probe in go.GetComponentsInChildren<ReflectionProbe>(true))
                Object.DestroyImmediate(probe);
        }

        static Bounds Bounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers)
                b.Encapsulate(r.bounds);
            return b;
        }

        static Material GroundMaterial(Transform ground, string key)
        {
            foreach (Renderer r in ground.GetComponentsInChildren<Renderer>())
            {
                if (r.sharedMaterial != null && r.sharedMaterial.name.ToLower().Contains(key))
                    return r.sharedMaterial;
            }
            return null;
        }

        // 단색 재질 — 없으면 만든다 (URP Lit)
        static Material Mat(string name, Color color)
        {
            string path = $"{k_MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            if (!AssetDatabase.IsValidFolder(k_MaterialFolder))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(k_MaterialFolder).Replace('\\', '/'), Path.GetFileName(k_MaterialFolder));
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
