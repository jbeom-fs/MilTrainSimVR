using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MilTrainSimVR.UrbanCombat.EditorTools
{
    // 배치 편집 도구 — 지도(또는 위성 사진)를 바닥에 깔고 건물·바닥·지하 '배치 상자'를 옮기고 늘려서 맞춘다.
    //   ① 메뉴 배치 편집 모드 → 위에서 내려다보는 씬 뷰 + 바닥 그림 + 배치 상자
    //   ② 상자를 W(이동) · T(사각형 — 모서리를 끌어 크기) · E(회전)로 맞추고, 층수는 인스펙터에서
    //   ③ 메뉴 맵 만들기 → 회색 박스 맵을 다시 만든다
    // 바닥 그림(카카오맵 지도 뷰·스카이뷰 캡처)은 이 PC 전용 폴더에 두고 씬에도 저장하지 않는다 — 저장소에 올리지 않으려고.
    // 지도 뷰는 건물 바닥 모양을 그대로 그려서 정확하고, 위성 사진은 높은 건물 지붕이 기울어 보여 몇 m 밀린다 → 기본은 지도
    public static class UrbanLayoutTools
    {
        public const string LayoutRootName = "C_Layout";
        const string k_MapPath = "Assets/C/_LocalRef/MapBlock.png";
        const string k_SatellitePath = "Assets/C/_LocalRef/SkyviewBlock.png";
        const string k_UnderlayName = "C_LayoutRef (저장 안 됨)";
        const string k_OldUnderlayName = "C_SatelliteRef";              // 예전 이름 — 남아 있으면 같이 지운다
        const string k_UnderlayPref = "MilTrainSimVR.C.LayoutUnderlay";  // 마지막에 깐 그림 (map / sky)
        const string k_MaterialFolder = "Assets/C/Materials/Greybox/Layout";
        // 바닥 그림이 덮는 맵 범위(m) — 그림을 만들 때 맞춘 값 (x 동쪽, z 북쪽). 두 그림 모두 같은 범위
        static readonly Rect k_UnderlayArea = Rect.MinMaxRect(-40f, -60f, 210f, 330f);

        [MenuItem("MilTrainSimVR/C/배치 편집 모드 (지도 깔기)", false, 10)]
        public static void EnterLayoutMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            GameObject layout = FindRoot(LayoutRootName);
            if (layout == null)
            {
                Debug.LogWarning("[C] 이 씬에는 배치(C_Layout)가 없다");
                return;
            }
            Undo.RecordObject(layout, "배치 편집 모드");
            layout.SetActive(true);
            GameObject map = FindRoot("C_Greybox");
            if (map != null)
            {
                Undo.RecordObject(map, "배치 편집 모드");
                map.SetActive(false);
            }
            ShowUnderlay(EditorPrefs.GetString(k_UnderlayPref, "map") == "sky");

            // 씬 뷰를 위에서 내려다보는 평면도로
            SceneView view = SceneView.lastActiveSceneView;
            if (view != null)
            {
                view.orthographic = true;
                view.rotation = Quaternion.Euler(90f, 0f, 0f);
                view.pivot = new Vector3(k_UnderlayArea.center.x, 0f, k_UnderlayArea.center.y);
                view.size = k_UnderlayArea.height * 0.55f;
                view.Repaint();
            }
        }

        [MenuItem("MilTrainSimVR/C/바닥 그림 바꾸기 (지도 ↔ 위성)", false, 11)]
        public static void ToggleUnderlay()
        {
            bool sky = EditorPrefs.GetString(k_UnderlayPref, "map") != "sky";
            EditorPrefs.SetString(k_UnderlayPref, sky ? "sky" : "map");
            ShowUnderlay(sky);
        }

        [MenuItem("MilTrainSimVR/C/배치에 건물 상자 추가", false, 30)]
        public static void AddBuilding()
        {
            Transform parent = LayoutGroup("Buildings");
            if (parent == null)
                return;
            GameObject go = CreateBuildingBox(parent, "새 건물", ViewCenter(), 12f, 12f, 0f, 4);
            Undo.RegisterCreatedObjectUndo(go, "건물 상자 추가");
            Selection.activeGameObject = go;
        }

        [MenuItem("MilTrainSimVR/C/배치에 바닥 상자 추가", false, 31)]
        public static void AddGround()
        {
            Transform parent = LayoutGroup("Ground");
            if (parent == null)
                return;
            GameObject go = CreateGroundBox(parent, "새 바닥", GreyboxGroundSpec.Kind.Road, ViewCenter(), 10f, 30f, 0f);
            Undo.RegisterCreatedObjectUndo(go, "바닥 상자 추가");
            Selection.activeGameObject = go;
        }

        [MenuItem("MilTrainSimVR/C/배치에 지하 상자 추가", false, 32)]
        public static void AddUnderground()
        {
            Transform parent = LayoutGroup("Underground");
            if (parent == null)
                return;
            GameObject go = CreateUndergroundBox(parent, "새 지하", GreyboxUndergroundSpec.Kind.Passage, ViewCenter(), 4.4f, 20f, 0f);
            Undo.RegisterCreatedObjectUndo(go, "지하 상자 추가");
            Selection.activeGameObject = go;
        }

        [MenuItem("MilTrainSimVR/C/바닥 그림 끄기", false, 40)]
        public static void HideUnderlay()
        {
            foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == k_UnderlayName || root.name.StartsWith(k_OldUnderlayName))
                    Object.DestroyImmediate(root);
            }
        }

        // sky = true 면 위성 사진, 아니면 지도 뷰
        public static void ShowUnderlay(bool sky)
        {
            HideUnderlay();
            string path = sky ? k_SatellitePath : k_MapPath;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                Debug.LogWarning($"[C] 바닥 그림이 없다: {path} — 이 PC 전용이라 다른 PC 에는 없다");
                return;
            }
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = k_UnderlayName;
            quad.hideFlags = HideFlags.DontSave;   // 씬에 저장하지 않는다
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            // 사각형은 -Z 쪽이 앞면 → X 로 90° 눕히면 하늘을 본다. 그림 위쪽이 +z(북쪽, 호수)
            quad.transform.SetPositionAndRotation(new Vector3(k_UnderlayArea.center.x, -0.02f, k_UnderlayArea.center.y), Quaternion.Euler(90f, 0f, 0f));
            quad.transform.localScale = new Vector3(k_UnderlayArea.width, k_UnderlayArea.height, 1f);
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { hideFlags = HideFlags.DontSave };
            material.SetTexture("_BaseMap", texture);
            quad.GetComponent<MeshRenderer>().sharedMaterial = material;
            // 사진을 잘못 눌러 고르지 않게 (저장 안 하는 오브젝트라 안 될 수도 있다 — 그래도 편집엔 지장 없다)
            try { SceneVisibilityManager.instance.DisablePicking(quad, true); }
            catch (System.Exception) { }
        }

        public static GameObject FindRoot(string name)
        {
            foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == name)
                    return root;
            }
            return null;
        }

        // 건물 배치 상자 — 상자 바닥이 건물 바닥, 파란 띠(-Z)가 앞면. 높이는 보기용으로 2m
        public static GameObject CreateBuildingBox(Transform parent, string name, Vector3 center, float width, float depth, float yaw, int floors)
        {
            EnsureMaterials();
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());   // 배치 상자는 보기·고르기용
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(new Vector3(center.x, 1f, center.z), Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = new Vector3(width, 2f, depth);
            go.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(k_MaterialFolder + "/Layout_Building.mat");
            var spec = go.AddComponent<GreyboxBuildingSpec>();
            spec.floors = floors;
            return go;
        }

        // 바닥 배치 상자 — 얇게(0.3m) 깔아서 건물 상자 아래로 보이게
        public static GameObject CreateGroundBox(Transform parent, string name, GreyboxGroundSpec.Kind kind, Vector3 center, float width, float depth, float yaw)
        {
            EnsureMaterials();
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(new Vector3(center.x, 0.15f, center.z), Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = new Vector3(width, 0.3f, depth);
            var spec = go.AddComponent<GreyboxGroundSpec>();
            spec.kind = kind;
            // 재질은 종류를 정한 뒤에 — 컴포넌트를 붙이는 순간 기본값(차도) 색이 먼저 칠해진다
            go.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{k_MaterialFolder}/Layout_{kind}.mat");
            return go;
        }

        // 지하 배치 상자 — 위에서 내려다볼 때 건물 상자에 가리지 않게 건물 상자(높이 2m) 바로 위에 얇게 띄운다
        public static GameObject CreateUndergroundBox(Transform parent, string name, GreyboxUndergroundSpec.Kind kind, Vector3 center, float width, float depth, float yaw)
        {
            EnsureMaterials();
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(new Vector3(center.x, 2.15f, center.z), Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = new Vector3(width, 0.1f, depth);
            var spec = go.AddComponent<GreyboxUndergroundSpec>();
            spec.kind = kind;
            go.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(k_MaterialFolder + "/Layout_Underground.mat");
            return go;
        }

        // 배치 상자용 반투명 재질 (위성 사진이 비쳐 보이게)
        public static void EnsureMaterials()
        {
            if (!AssetDatabase.IsValidFolder(k_MaterialFolder))
                AssetDatabase.CreateFolder("Assets/C/Materials/Greybox", "Layout");
            LayoutMaterial("Layout_Building", new Color(0.55f, 0.65f, 0.85f, 0.45f));
            LayoutMaterial("Layout_Enterable", new Color(1f, 0.55f, 0.15f, 0.55f));
            LayoutMaterial("Layout_Road", new Color(0.2f, 0.45f, 1f, 0.3f));
            LayoutMaterial("Layout_Sidewalk", new Color(0.95f, 0.95f, 0.95f, 0.4f));
            LayoutMaterial("Layout_Alley", new Color(1f, 0.85f, 0.2f, 0.35f));
            LayoutMaterial("Layout_Lot", new Color(0.3f, 0.9f, 0.4f, 0.3f));
            LayoutMaterial("Layout_Underground", new Color(0.65f, 0.3f, 1f, 0.45f));
        }

        static void LayoutMaterial(string name, Color color)
        {
            string path = $"{k_MaterialFolder}/{name}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
                return;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetFloat("_Surface", 1f);   // 반투명
            mat.SetColor("_BaseColor", color);
            BaseShaderGUI.SetMaterialKeywords(mat);
            AssetDatabase.CreateAsset(mat, path);
        }

        static Transform LayoutGroup(string group)
        {
            GameObject layout = FindRoot(LayoutRootName);
            if (layout == null)
            {
                Debug.LogWarning("[C] 이 씬에는 배치(C_Layout)가 없다");
                return null;
            }
            Transform t = layout.transform.Find(group);
            return t != null ? t : layout.transform;
        }

        static Vector3 ViewCenter()
        {
            SceneView view = SceneView.lastActiveSceneView;
            return view != null ? view.pivot : Vector3.zero;
        }
    }
}
