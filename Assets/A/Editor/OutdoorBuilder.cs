using UnityEditor;
using UnityEngine;

namespace MilTrainSimVR.A.EditorTools
{
    // 창밖 땅 (11/03 사용자: 연병장 흙). 내무반 창밖이 스카이박스 아래쪽 회색으로 보이던 것을 가린다.
    //   · 큰 흙 땅판 하나(Outdoor/Ground, k_GroundSize m 정사각형)를 바닥 바로 아래(y k_GroundY)에 깐다 — 방 바닥 상자(두께 0.1) 안이라 실내에서는 안 보인다.
    //     부딪힘 없음 (창밖으로 나갈 수 없다). 해(노을 Directional Light)의 그림자를 받는다.
    //   · 흙 텍스처는 코드로 만든다 (이음매 없는 잡음, Materials/Outdoor/Dirt_Albedo.png) — 실제 흙 사진 텍스처로 바꾸려면 Dirt.mat 의 Base Map 만 바꾼다.
    //   · 안개(선형, k_FogStart ~ k_FogEnd m)를 노을 하늘의 지평선 색으로 켠다 — 땅 끝과 하늘 지평선이 섞인다.
    //     시작 거리가 사격장에서 가장 먼 곳(끝벽 32 m)보다 멀어서 실내·사격장에는 안개가 끼지 않는다. 안개는 A_Sandbox 씬 전체 설정이다.
    static class OutdoorBuilder
    {
        const string k_Root = "Outdoor";
        internal const float LampHeadYaw = 180f;   // 창밖 가로등 — 길(z -22) 북쪽 가 z -18.5 에 서서 등이 길 쪽(-Z)
        const string k_Folder = "Assets/A/Materials/Outdoor";
        const string k_TexturePath = k_Folder + "/Dirt_Albedo.png";
        const string k_MaterialPath = k_Folder + "/Dirt.mat";

        const float k_GroundY = -0.02f;
        const float k_GroundSize = 2000f;                       // 끝이 지평선 바로 아래(눈높이 1.4 m 에서 0.1° 이내)에 오게
        static readonly Vector3 k_GroundCenter = new Vector3(15f, 0f, 0f);   // 내무반(x 0)과 사격장(x 30) 사이
        const float k_TileMeters = 4f;                          // 텍스처 한 장이 덮는 크기

        const float k_FogStart = 40f;    // 사격장 끝벽(32 m)보다 멀리서부터 — 실내·사격장에는 끼지 않는다
        const float k_FogEnd = 400f;     // 지평선 아래 약 2° 띠가 안개로 하늘에 이어진다
        static readonly Color k_FogColor = new Color(0.50f, 0.45f, 0.38f);   // Terra 4 하늘 지평선의 어두운 띠(0.40)와 그 위(0.61) 사이 — 땅 끝이 띠에 이어진다

        const int k_TextureSize = 512;

        [MenuItem("A/만들기/바깥 땅 (흙 · 노을 안개)")]
        static void Build()
        {
            Undo.SetCurrentGroupName("A: 바깥 땅");
            int group = Undo.GetCurrentGroup();

            Material material = EnsureMaterial();

            var root = GameObject.Find(k_Root);
            if (root == null)
            {
                root = new GameObject(k_Root);
                Undo.RegisterCreatedObjectUndo(root, "A: 바깥 땅");
            }
            var old = root.transform.Find("Ground");
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);   // 10 × 10 m
            Undo.RegisterCreatedObjectUndo(ground, "A: 바깥 땅");
            ground.name = "Ground";
            Object.DestroyImmediate(ground.GetComponent<MeshCollider>());
            ground.transform.SetParent(root.transform, false);
            ground.transform.position = new Vector3(k_GroundCenter.x, k_GroundY, k_GroundCenter.z);
            ground.transform.localScale = new Vector3(k_GroundSize / 10f, 1f, k_GroundSize / 10f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = material;
            GameObjectUtility.SetStaticEditorFlags(ground, StaticEditorFlags.BatchingStatic);

            var settings = GetRenderSettings();
            if (settings != null)
                Undo.RecordObject(settings, "A: 바깥 땅");
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = k_FogStart;
            RenderSettings.fogEndDistance = k_FogEnd;
            RenderSettings.fogColor = k_FogColor;

            Undo.CollapseUndoOperations(group);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(ground.scene);
            Debug.Log($"[A] 바깥 땅: 흙 {k_GroundSize} m, 안개 {k_FogStart} ~ {k_FogEnd} m");
        }

        static Object GetRenderSettings()
        {
            var method = typeof(RenderSettings).GetMethod("GetRenderSettings",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            return method != null ? (Object)method.Invoke(null, null) : null;
        }

        static Material EnsureMaterial()
        {
            if (!AssetDatabase.IsValidFolder(k_Folder))
                AssetDatabase.CreateFolder("Assets/A/Materials", "Outdoor");
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(k_TexturePath) == null)
            {
                System.IO.File.WriteAllBytes(k_TexturePath, DirtTexture().EncodeToPNG());
                AssetDatabase.ImportAsset(k_TexturePath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(k_TexturePath);
                importer.anisoLevel = 8;   // 낮게 내려다보는 땅이라 비스듬한 면에서도 무늬가 뭉개지지 않게
                importer.SaveAndReimport();
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(k_TexturePath);

            var material = AssetDatabase.LoadAssetAtPath<Material>(k_MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Dirt" };
                AssetDatabase.CreateAsset(material, k_MaterialPath);
                material.SetTexture("_BaseMap", texture);
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_Metallic", 0f);
                material.SetFloat("_Smoothness", 0.08f);   // 마른 흙 — 거의 안 빛난다
            }
            float tiles = k_GroundSize / k_TileMeters;
            material.SetTextureScale("_BaseMap", new Vector2(tiles, tiles));
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        // 창밖 소품 (11/03, 임시 — 기본 도형). 내무반 남쪽 창(z -9.5, x ±2.36)으로 보이는 쪽(z < 0)에만 둔다. 사용자가 모델을 주면 같은 자리에 바꿔 끼운다.
        //   · 앞에서부터: 길(z -22) · 가로등 · 부대 담장 + 철조망(z -45) · 나무 · 건너편 막사 2동(z -78 ~ -82, 2층) · 먼 숲 줄(z -140, 안개 속 실루엣)
        //   · 높이는 9 m 이하 — 창에서 노을 해(지평선 위 10°, 방위 x+ 쪽)를 보는 선을 가리지 않는다
        //   · 부딪힘 없음. 다시 누르면 Outdoor/Props 를 지우고 다시 짓는다
        [MenuItem("A/만들기/바깥 소품 (담장·막사·나무 임시)")]
        static void BuildProps()
        {
            Undo.SetCurrentGroupName("A: 바깥 소품");
            int group = Undo.GetCurrentGroup();
            var root = GameObject.Find(k_Root);
            if (root == null)
            {
                root = new GameObject(k_Root);
                Undo.RegisterCreatedObjectUndo(root, "A: 바깥 소품");
            }
            var old = root.transform.Find("Props");
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);
            var props = new GameObject("Props").transform;
            Undo.RegisterCreatedObjectUndo(props.gameObject, "A: 바깥 소품");
            props.SetParent(root.transform, false);

            var concrete = Lit("Outdoor_Concrete", new Color(0.62f, 0.61f, 0.58f), 0.1f, 0f);
            var asphalt = Lit("Outdoor_Asphalt", new Color(0.17f, 0.17f, 0.18f), 0.15f, 0f);
            var metal = Lit("Outdoor_Metal", new Color(0.35f, 0.36f, 0.37f), 0.4f, 0.6f);
            var buildingWall = Lit("Outdoor_BuildingWall", new Color(0.80f, 0.76f, 0.66f), 0.1f, 0f);
            var roof = Lit("Outdoor_Roof", new Color(0.24f, 0.30f, 0.26f), 0.2f, 0f);
            var window = Lit("Outdoor_Window", new Color(0.10f, 0.12f, 0.15f), 0.85f, 0f);
            var trunk = Lit("Outdoor_Trunk", new Color(0.25f, 0.19f, 0.14f), 0.05f, 0f);
            var leaves = Lit("Outdoor_Leaves", new Color(0.17f, 0.27f, 0.13f), 0.05f, 0f);

            // 길 — 담장과 나란히, 북쪽 끝에 가로등
            var road = Group("Road", props);
            Box("Asphalt", road, new Vector3(0f, -0.01f, -22f), new Vector3(200f, 0.02f, 6f), asphalt);
            ApplyConcreteBlack(road.Find("Asphalt"));
            // 차선 (11/03 사용자) — 왕복 2차로: 가운데 황색 실선 두 줄(중앙선), 양 끝 흰색 실선(길가장자리선). 폭 0.15 m, 아스팔트 위 1 cm
            var yellow = Lit("Outdoor_LineYellow", new Color(0.86f, 0.62f, 0.08f), 0.25f, 0f);
            var white = Lit("Outdoor_LineWhite", new Color(0.85f, 0.85f, 0.82f), 0.25f, 0f);
            foreach (float dz in new[] { -0.15f, 0.15f })
                Box("Line_Center", road, new Vector3(0f, 0.005f, -22f + dz), new Vector3(200f, 0.01f, 0.15f), yellow);
            foreach (float dz in new[] { -2.75f, 2.75f })
                Box("Line_Edge", road, new Vector3(0f, 0.005f, -22f + dz), new Vector3(200f, 0.01f, 0.15f), white);
            for (float x = -40f; x <= 40f; x += 16f)
            {
                // 11/10 사용자 모델 Korean_Streetlight_v01 (StreetLightBuilder) — 없으면 아래 임시 도형
                if (StreetLightBuilder.Place(road, new Vector3(x, 0f, -18.5f), LampHeadYaw) != null)
                    continue;
                var lamp = Group("Lamp", road);
                lamp.position = new Vector3(x, 0f, -18.5f);
                Cylinder("Pole", lamp, new Vector3(0f, 3f, 0f), 0.07f, 6f, metal);
                Box("Arm", lamp, new Vector3(0f, 5.9f, -0.6f), new Vector3(0.08f, 0.08f, 1.2f), metal);
                Box("Head", lamp, new Vector3(0f, 5.82f, -1.15f), new Vector3(0.3f, 0.12f, 0.5f), metal);
            }

            // 부대 담장 — 콘크리트 벽 + 기둥 + 위 철조망 세 줄
            const float wallZ = -45f, wallHalf = 100f, wallHeight = 2.2f;
            var fence = Group("Fence", props);
            Box("Wall", fence, new Vector3(0f, wallHeight * 0.5f, wallZ), new Vector3(wallHalf * 2f, wallHeight, 0.25f), concrete);
            for (float x = -wallHalf; x <= wallHalf; x += 4f)
            {
                Box("Post", fence, new Vector3(x, (wallHeight + 0.15f) * 0.5f, wallZ), new Vector3(0.4f, wallHeight + 0.15f, 0.4f), concrete);
                Box("WirePost", fence, new Vector3(x, wallHeight + 0.4f, wallZ), new Vector3(0.05f, 0.5f, 0.05f), metal);
            }
            foreach (float y in new[] { 2.45f, 2.6f, 2.75f })
                Box("Wire", fence, new Vector3(0f, y, wallZ), new Vector3(wallHalf * 2f, 0.03f, 0.03f), metal);

            // 건너편 막사 2동 (2층) — 앞면(+z, 이쪽)에 창 두 줄과 출입문
            Barracks("Barracks_West", props, new Vector3(-28f, 0f, -78f), new Vector3(36f, 7.2f, 10f), buildingWall, roof, window);
            Barracks("Barracks_East", props, new Vector3(26f, 0f, -82f), new Vector3(30f, 7.2f, 10f), buildingWall, roof, window);

            // 나무 — 담장 뒤와 길가에 몇 그루 (자리 고정, 크기·방향·초록 종류만 조금씩 다르게). 나무 모델이 없으면 도형
            GameObject[] treePrefabs = EnsureTreePrefabs();
            var trees = Group("Trees", props);
            var rng = new System.Random(1103);
            Vector2[] spots = { new Vector2(-34f, -52f), new Vector2(-18f, -56f), new Vector2(-6f, -50f), new Vector2(8f, -58f),
                                new Vector2(22f, -53f), new Vector2(38f, -57f), new Vector2(-44f, -30f), new Vector2(46f, -32f),
                                new Vector2(-12f, -64f), new Vector2(14f, -66f) };
            for (int i = 0; i < spots.Length; i++)
            {
                var position = new Vector3(spots[i].x, 0f, spots[i].y);
                float scale = 1.1f + 0.35f * (float)rng.NextDouble();   // 모델 높이 5.3 m → 약 6 ~ 7.7 m
                if (treePrefabs != null)
                    PlaceTree(trees, treePrefabs[i % treePrefabs.Length], position, scale, rng);
                else
                    Tree(trees, position, 0.85f + 0.3f * (float)rng.NextDouble(), trunk, leaves, rng);
            }

            // 먼 숲 줄 — 안개 속에서 지평선 실루엣 (나무 모델을 크게, 앞뒤로 조금씩 엇갈리게)
            var forest = Group("Forest", props);
            int n = 0;
            for (float x = -220f; x <= 220f; x += 7f, n++)
            {
                var position = new Vector3(x + (float)rng.NextDouble() * 3f, 0f, -140f - (float)rng.NextDouble() * 8f);
                if (treePrefabs != null)
                    PlaceTree(forest, treePrefabs[n % treePrefabs.Length], position, 1.6f + 0.6f * (float)rng.NextDouble(), rng);
                else
                {
                    float r = 3f + 1.5f * (float)rng.NextDouble();
                    Sphere("Crown", forest, position + Vector3.up * r * 0.8f, r * 2f, leaves);
                }
            }

            foreach (var t in props.GetComponentsInChildren<Transform>())
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            Undo.CollapseUndoOperations(group);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(props.gameObject.scene);
            Debug.Log($"[A] 바깥 소품: 길·가로등 · 담장(z {wallZ}) · 막사 2동 · 나무 {spots.Length} · 먼 숲 (임시 도형)");
        }

        // 길 = 검은 콘크리트 (11/09 사용자, 허브와 같게) — m 단위 UV(1 = 4 m). 메뉴 'A/만들기/바깥 길 검은 콘크리트' 로 길만 바꿀 수도 있다
        static void ApplyConcreteBlack(Transform asphalt)
        {
            if (asphalt == null)
                return;
            var material = HubBuilder.ConcreteBlack();
            WallUvBuilder.Apply(asphalt, material);
        }

        [MenuItem("A/만들기/바깥 길 검은 콘크리트")]
        static void RoadConcreteBlack()
        {
            var asphalt = GameObject.Find($"{k_Root}/Props/Road/Asphalt");
            if (asphalt == null)
            {
                Debug.LogError($"[A] {k_Root}/Props/Road/Asphalt 이 없다");
                return;
            }
            Undo.RecordObject(asphalt.GetComponent<MeshFilter>(), "A: 길 검은 콘크리트");
            Undo.RecordObject(asphalt.GetComponent<MeshRenderer>(), "A: 길 검은 콘크리트");
            ApplyConcreteBlack(asphalt.transform);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(asphalt.scene);
            Debug.Log("[A] 바깥 길 → 검은 콘크리트");
        }

        static void Barracks(string name, Transform parent, Vector3 position, Vector3 size, Material wall, Material roof, Material window)
        {
            var b = Group(name, parent);
            b.position = position;
            Box("Body", b, new Vector3(0f, size.y * 0.5f, 0f), size, wall);
            Box("Roof", b, new Vector3(0f, size.y + 0.15f, 0f), new Vector3(size.x + 0.6f, 0.3f, size.z + 0.6f), roof);
            float front = size.z * 0.5f + 0.02f;
            int count = Mathf.FloorToInt((size.x - 2f) / 3.5f);
            float start = -(count - 1) * 3.5f * 0.5f;
            for (int i = 0; i < count; i++)
            {
                float x = start + i * 3.5f;
                if (Mathf.Abs(x) > 1.5f)   // 가운데는 출입문
                    Box("Window_1F", b, new Vector3(x, 1.7f, front), new Vector3(1.4f, 1.2f, 0.05f), window);
                Box("Window_2F", b, new Vector3(x, 5.1f, front), new Vector3(1.4f, 1.2f, 0.05f), window);
            }
            Box("Door", b, new Vector3(0f, 1.2f, front), new Vector3(1.8f, 2.4f, 0.05f), window);
            Box("Canopy", b, new Vector3(0f, 2.7f, front + 0.6f), new Vector3(3f, 0.12f, 1.2f), roof);
        }

        static void Tree(Transform parent, Vector3 position, float scale, Material trunk, Material leaves, System.Random rng)
        {
            var t = Group("Tree", parent);
            t.position = position;
            t.localScale = Vector3.one * scale;
            Cylinder("Trunk", t, new Vector3(0f, 1.4f, 0f), 0.18f, 2.8f, trunk);
            Sphere("Crown", t, new Vector3(0f, 4f, 0f), 3.6f, leaves);
            Sphere("Crown", t, new Vector3(0.9f * (float)rng.NextDouble() - 0.45f, 5.2f, 0.9f * (float)rng.NextDouble() - 0.45f), 2.6f, leaves);
        }

        // 나무 모델 (11/03 에셋 스토어 World Space Trees FREE — Models/Trees). 원래 셰이더가 Built-in 용이라 URP Lit 재질을 새로 만든다.
        // 잎 텍스처(Trees_SetC_albedoCutout)는 2×2 칸에 나무 네 종류 — 모델 잎 UV 는 왼쪽 위 칸. 재질 오프셋으로 초록 세 칸(왼쪽 위·왼쪽 아래·오른쪽 아래)을 골라
        // 프리팹 세 개(Tree_Green_1~3)를 만든다. 잎은 카드라 양면 + 알파 잘라내기. 모델 없으면 null (도형 나무로)
        const string k_TreeFolder = "Assets/A/Models/Trees";
        static readonly (string name, Vector2 offset)[] k_LeafVariants =
        {
            ("DarkGreen", new Vector2(0f, 0f)),
            ("Olive", new Vector2(0f, -0.5f)),
            ("Green", new Vector2(0.5f, -0.5f)),
        };

        static GameObject[] EnsureTreePrefabs()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(k_TreeFolder + "/MainTree.FBX");
            if (model == null)
                return null;
            foreach (string normal in new[] { "/Textures/treeBarksCD_norm_normal.png", "/Textures/Trees_SetC_norm.png" })
                if (AssetImporter.GetAtPath(k_TreeFolder + normal) is TextureImporter ti && ti.textureType != TextureImporterType.NormalMap)
                {
                    ti.textureType = TextureImporterType.NormalMap;
                    ti.SaveAndReimport();
                }
            if (!AssetDatabase.IsValidFolder(k_TreeFolder + "/Materials"))
                AssetDatabase.CreateFolder(k_TreeFolder, "Materials");
            if (!AssetDatabase.IsValidFolder(k_TreeFolder + "/Prefabs"))
                AssetDatabase.CreateFolder(k_TreeFolder, "Prefabs");

            var bark = TreeMaterial("Tree_Bark", "treeBarksCD_albedo.png", "treeBarksCD_norm_normal.png", Vector2.zero, false);
            var prefabs = new GameObject[k_LeafVariants.Length];
            for (int i = 0; i < k_LeafVariants.Length; i++)
            {
                string path = $"{k_TreeFolder}/Prefabs/Tree_Green_{i + 1}.prefab";
                var leaves = TreeMaterial("Tree_Leaves_" + k_LeafVariants[i].name, "Trees_SetC_albedoCutout.tga", "Trees_SetC_norm.png", k_LeafVariants[i].offset, true);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    var go = (GameObject)Object.Instantiate(model);
                    go.name = $"Tree_Green_{i + 1}";
                    foreach (var c in go.GetComponentsInChildren<Animator>())
                        Object.DestroyImmediate(c);
                    go.GetComponent<Renderer>().sharedMaterials = new[] { bark, leaves };   // 칸 0 = 기둥, 1 = 잎
                    prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
                    Object.DestroyImmediate(go);
                }
                prefabs[i] = prefab;
            }
            return prefabs;
        }

        static Material TreeMaterial(string name, string albedo, string normal, Vector2 offset, bool leaves)
        {
            string path = $"{k_TreeFolder}/Materials/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{k_TreeFolder}/Textures/{albedo}"));
            material.SetTextureOffset("_BaseMap", offset);   // 노멀 맵도 같은 칸 (URP Lit 은 _BaseMap 배치를 모든 맵에 쓴다)
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{k_TreeFolder}/Textures/{normal}"));
            material.EnableKeyword("_NORMALMAP");
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", leaves ? 0.15f : 0.05f);
            if (leaves)
            {
                material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_Cutoff", 0.33f);   // 원래 재질 값
                material.EnableKeyword("_ALPHATEST_ON");
                material.SetFloat("_Cull", 0f);        // 잎 카드 양면
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                material.SetOverrideTag("RenderType", "TransparentCutout");
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void PlaceTree(Transform parent, GameObject prefab, Vector3 position, float scale, System.Random rng)
        {
            var tree = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            tree.transform.position = position;
            tree.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            tree.transform.localScale = Vector3.one * scale;
        }

        static Transform Group(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        static GameObject Box(string name, Transform parent, Vector3 p, Vector3 size, Material m) => Primitive(PrimitiveType.Cube, name, parent, p, size, m);
        static GameObject Sphere(string name, Transform parent, Vector3 p, float diameter, Material m) => Primitive(PrimitiveType.Sphere, name, parent, p, Vector3.one * diameter, m);
        // Unity 원기둥은 높이 2 — scale.y = 높이 / 2
        static GameObject Cylinder(string name, Transform parent, Vector3 p, float radius, float height, Material m) =>
            Primitive(PrimitiveType.Cylinder, name, parent, p, new Vector3(radius * 2f, height * 0.5f, radius * 2f), m);

        static Material Lit(string name, Color color, float smoothness, float metallic)
        {
            string path = $"{k_Folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                if (!AssetDatabase.IsValidFolder(k_Folder))
                    AssetDatabase.CreateFolder("Assets/A/Materials", "Outdoor");
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Smoothness", smoothness);
                material.SetFloat("_Metallic", metallic);
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }

        // 이음매 없는 흙: 주기 값 잡음 여러 겹(큰 얼룩 + 잔 알갱이) + 드문 자갈 점
        static Texture2D DirtTexture()
        {
            var dark = new Color(0.32f, 0.29f, 0.25f);    // 노을빛(주황)을 받으니 흙 자체는 회갈색으로
            var light = new Color(0.54f, 0.50f, 0.44f);
            var pebble = new Color(0.66f, 0.60f, 0.52f);
            var rng = new System.Random(1103);
            var lattice = new float[256 * 256];
            for (int i = 0; i < lattice.Length; i++)
                lattice[i] = (float)rng.NextDouble();

            var tex = new Texture2D(k_TextureSize, k_TextureSize, TextureFormat.RGB24, true);
            var pixels = new Color[k_TextureSize * k_TextureSize];
            for (int y = 0; y < k_TextureSize; y++)
                for (int x = 0; x < k_TextureSize; x++)
                {
                    float u = (float)x / k_TextureSize, v = (float)y / k_TextureSize;
                    float n = 0.40f * Noise(lattice, u, v, 4) + 0.25f * Noise(lattice, u, v, 8)
                            + 0.17f * Noise(lattice, u, v, 32) + 0.18f * Noise(lattice, u, v, 128);
                    Color c = Color.Lerp(dark, light, Mathf.SmoothStep(0.2f, 0.8f, n));
                    if (Noise(lattice, u + 0.37f, v + 0.11f, 256) > 0.93f)
                        c = Color.Lerp(c, pebble, 0.6f);
                    pixels[y * k_TextureSize + x] = c;
                }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        // period 칸으로 나눈 격자의 값 잡음 — 0~1 범위 밖은 감싸서 이음매가 없다
        static float Noise(float[] lattice, float u, float v, int period)
        {
            float fx = u * period, fy = v * period;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            float a = Lattice(lattice, x0, y0, period), b = Lattice(lattice, x0 + 1, y0, period);
            float c = Lattice(lattice, x0, y0 + 1, period), d = Lattice(lattice, x0 + 1, y0 + 1, period);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        static float Lattice(float[] lattice, int x, int y, int period)
        {
            x = ((x % period) + period) % period;
            y = ((y % period) + period) % period;
            return lattice[(y & 255) * 256 + (x & 255)];
        }
    }
}
