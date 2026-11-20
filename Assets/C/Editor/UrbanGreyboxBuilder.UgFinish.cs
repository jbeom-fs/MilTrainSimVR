using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MilTrainSimVR.UrbanCombat.EditorTools
{
    // 지하보도 마감 (사용자 11/18: "지하보도 계단, 벽, 천장 텍스쳐가 없다 · 지지기둥을 원기둥으로 · 좀 어둡게 하고 전등을 붙여서 빛났으면 ·
    // 광택이 나는 텍스쳐 · 전등은 조금만 써서 어둡게 유지해 색적은 조금은 힘들면 좋겠음").
    //   · 재질: 벽 = 유광 타일(Textures/Map/UgWallTile — ambientCG Tiles107 흰 타일에 색을 입힌다. 허리 아래 1 m 는 짙은 띠), 천장 = 어두운 콘크리트(Wall A),
    //     계단 = 바닥과 같은 화강암 + 디딤판 끝 노란 미끄럼 방지 줄, 바닥 = 있던 화강암의 광택만 올림
    //   · 반사: 덮인 방을 감싸는 어두운 반사 프로브 하나(UgReflection). 지하 재질 · 그 안의 사람 · 총 · 놓인 물건이 바깥 하늘 대신 이것을 비춘다
    //     (11/18 에는 재질에서 환경 반사를 껐는데, URP 는 그러면 씬 전체의 평균 하늘빛을 대신 써서 유광 바닥 · 타일이 그대로 훤했다.
    //      11/19 측정: 전등 · 환경광 · 해를 다 꺼도 바닥 밝기 92 가운데 45~56 이 남았고, 반사 세기를 0 으로 하니 0) — 광택은 전등 빛이 번쩍이는 것으로 본다
    //   · 기둥: 네모 상자(0.8 m) → 지름 0.8 m 원기둥(24각) + 아래 · 위 띠. 판정은 캡슐(둥근 끝은 바닥 · 천장 속)
    //   · 전등: 덮인 방의 긴 방향 가운데 줄에 약 16 m 마다 형광등 하나 — 아래를 보는 스포트라이트(위로 새면 지상 길바닥이 밝아진다), 그림자 없음.
    //     빛이 닿는 둥근 자리 사이는 어둡다
    //   · 어둡게: UndergroundAmbient (지하 구조물 · 그 안의 적 · 플레이어의 환경광을 줄인다)
    // 원기둥 · 전등은 상자가 아니라서 합치기(CombineVisuals) 뒤에 따로 놓는다 — 자리는 짓는 동안 적어 둔다
    public static partial class UrbanGreyboxBuilder
    {
        const float k_UgBand = 1f;            // 벽 아래 짙은 타일 띠 높이 (타일 넉 줄)
        const float k_UgColumnRadius = 0.4f;
        const float k_UgLampSpacing = 16f;    // 전등 간격 — 넓을수록 어둡다
        const float k_UgLampIntensity = 4f;   // 전등 밝기 (11/18: 5. 11/19 사용자: "지하보도의 밝기를 좀 낮추는게 좋을 듯")
        const float k_UgReflection = 0.3f;    // 지하 반사 프로브 세기 — 바깥 평균 하늘빛의 몇 배를 비추나 (UndergroundAmbient 의 환경광과 비슷하게)
        const string k_UgReflectionName = "UgReflection";

        class UgMaterials
        {
            public Material wall, band, ceiling, step, nose, column, trim, lampGlow;
        }

        static UgMaterials s_Ug;
        static readonly List<Vector3> s_UgColumns = new List<Vector3>();                 // 기둥 자리 (월드 x · z, y 는 안 쓴다)
        static readonly List<(Vector3 at, bool alongX)> s_UgLamps = new List<(Vector3, bool)>();   // 전등 자리 (천장 밑) · 등이 누운 방향

        static UgMaterials UgMats(Materials m)
        {
            var ug = new UgMaterials
            {
                // 타일 한 장 25 cm (그림 한 장 1 m = 4 × 4), 줄은 지하 바닥에서 시작
                wall = UgMat("GB_UgWall_Tile", "UgWallTile", Vector2.one, k_UgFloor, 0.8f, new Color(0.5f, 0.6f, 0.58f)),
                band = UgMat("GB_UgWall_TileDark", "UgWallTile", Vector2.one, k_UgFloor, 0.8f, new Color(0.16f, 0.22f, 0.24f)),
                column = UgMat("GB_UgColumn_Tile", "UgWallTile", Vector2.one, k_UgFloor, 0.8f, new Color(0.74f, 0.64f, 0.46f)),
                ceiling = UgMat("GB_UgCeiling_Concrete", "Wall A", Vector2.one * 3f, 0f, 0.15f, new Color(0.4f, 0.41f, 0.43f)),
                step = UgMat("GB_UgStep_Granite", "UnderGround", Vector2.one * 2f, 0f, 0.5f, new Color(0.86f, 0.86f, 0.86f)),
                nose = UgPlainMat("GB_UgStep_Nose", new Color(0.78f, 0.6f, 0.1f), 0.3f),
                trim = UgPlainMat("GB_Ug_Trim", new Color(0.1f, 0.11f, 0.12f), 0.6f),
                lampGlow = EmissiveMat("GB_Ug_LampGlow", new Color(0.86f, 0.95f, 0.9f), 3f),
            };
            // 있던 바닥 화강암: 광택을 올린다 — 처음 만든 값(0.55)일 때 한 번만 (그 뒤엔 .mat 에서 고친 값이 남는다)
            if (m.ugFloor != null && Mathf.Abs(m.ugFloor.GetFloat("_Smoothness") - 0.55f) < 0.005f)
            {
                m.ugFloor.SetFloat("_Smoothness", 0.78f);
                EditorUtility.SetDirty(m.ugFloor);
            }
            // 11/18 에 환경 반사를 꺼 둔 재질을 되켠다 — 반사 프로브를 비추게
            foreach (Material mat in new[] { ug.wall, ug.band, ug.column, ug.ceiling, ug.step, ug.nose, ug.trim, m.ugFloor })
                ProbeReflection(mat);
            return ug;
        }

        // 지하 재질 — 그림을 입힌다(MapMat). 있는 재질은 그대로 쓴다 (값은 그 .mat 에서 고친다)
        static Material UgMat(string name, string folder, Vector2 meters, float startY, float smoothness, Color tint)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{k_MaterialFolder}/{name}.mat");
            if (mat != null)
                return mat;
            SmoothnessFromRoughness($"{k_FloorTextureFolder}/{folder}");
            mat = MapMat(name, folder, meters, startY, smoothness, GridTexture(), tint, tint);
            return mat;
        }

        static Material UgPlainMat(string name, Color color, float smoothness)
        {
            string path = $"{k_MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
                return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            BaseShaderGUI.SetMaterialKeywords(mat);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // 환경 반사를 켠다 (꺼져 있을 때만) — 꺼 두면 URP 가 반사 프로브 대신 씬 전체의 평균 하늘빛을 쓴다
        static void ProbeReflection(Material mat)
        {
            if (mat == null || !mat.HasProperty("_EnvironmentReflections") || mat.GetFloat("_EnvironmentReflections") > 0.5f)
                return;
            mat.SetFloat("_EnvironmentReflections", 1f);
            mat.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(mat);
        }

        // 지하 반사 프로브가 비추는 그림 — 한 가지 색(바깥 평균 하늘빛)의 작은 큐브맵. 세기는 프로브에서 줄인다
        static Cubemap UgReflectionCubemap()
        {
            string path = $"{k_FloorTextureFolder}/UgReflection.cubemap";
            var cube = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
            if (cube != null)
                return cube;
            SphericalHarmonicsL2 sky = RenderSettings.ambientProbe;
            Color colour = new Color(sky[0, 0], sky[1, 0], sky[2, 0]).gamma;
            cube = new Cubemap(16, TextureFormat.RGBA32, false);
            var pixels = new Color[16 * 16];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = colour;
            for (int face = 0; face < 6; face++)
                cube.SetPixels(pixels, (CubemapFace)face);
            cube.Apply();
            AssetDatabase.CreateAsset(cube, path);
            return cube;
        }

        // 덮인 방 전체를 감싸는 반사 프로브 — 있으면 고쳐 쓴다. 상자는 지하 높이(바닥~천장)만이라 길 위는 그대로 하늘을 비춘다
        // (URP Forward+ 는 프로브를 화소마다 고른다 — 상자 안에 있는 화소만 이 프로브를 쓴다)
        static ReflectionProbe UgReflectionProbe(Transform group, List<Bounds> rooms)
        {
            if (rooms.Count == 0)
                return null;
            Bounds all = rooms[0];
            foreach (Bounds room in rooms)
                all.Encapsulate(room);
            Transform existing = group.Find(k_UgReflectionName);
            GameObject go = existing != null ? existing.gameObject : new GameObject(k_UgReflectionName);
            if (existing == null)
            {
                Undo.RegisterCreatedObjectUndo(go, "지하 반사 프로브");
                go.transform.SetParent(group, false);
            }
            go.transform.SetPositionAndRotation(all.center, Quaternion.identity);
            ReflectionProbe probe = go.GetComponent<ReflectionProbe>();
            if (probe == null)
                probe = Undo.AddComponent<ReflectionProbe>(go);
            Undo.RecordObject(probe, "지하 반사 프로브");
            probe.mode = ReflectionProbeMode.Custom;
            probe.customBakedTexture = UgReflectionCubemap();
            probe.center = Vector3.zero;
            probe.size = all.size + new Vector3(0.6f, 0.2f, 0.6f);   // 벽 두께만큼 넉넉히 — 위아래는 바닥판 · 천장판 속까지만
            probe.boxProjection = false;
            probe.blendDistance = 0f;
            probe.importance = 100;
            probe.intensity = k_UgReflection;
            return probe;
        }

        // ambientCG 는 거칠기(_Roughness)를 준다 — URP 는 매끄러움을 금속 그림의 알파에서 읽으니 한 번 바꿔 둔다 (금속 0, 알파 = 1 − 거칠기).
        // 타일은 반짝이고 줄눈은 안 반짝이는 차이가 여기서 나온다
        static void SmoothnessFromRoughness(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
                return;
            string rough = null;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = Path.GetFileNameWithoutExtension(path);
                if (file.EndsWith("_MetallicSmoothness"))
                    return;
                if (file.EndsWith("_Roughness"))
                    rough = path;
            }
            if (rough == null)
                return;
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            source.LoadImage(File.ReadAllBytes(rough));
            Color32[] pixels = source.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(0, 0, 0, (byte)(255 - pixels[i].r));
            source.SetPixels32(pixels);
            source.Apply();
            string target = rough.Substring(0, rough.LastIndexOf("_Roughness")) + "_MetallicSmoothness.png";
            File.WriteAllBytes(target, source.EncodeToPNG());
            Object.DestroyImmediate(source);
            AssetDatabase.ImportAsset(target);
        }

        // 덮인 방 하나의 전등 자리 — 긴 방향 가운데 줄에 k_UgLampSpacing 마다 (방마다 적어도 하나)
        static void UgPlanLamps(UgRoom room)
        {
            bool alongX = room.r.width >= room.r.height;
            float length = alongX ? room.r.width : room.r.height;
            int count = Mathf.Max(1, Mathf.RoundToInt(length / k_UgLampSpacing));
            for (int i = 0; i < count; i++)
            {
                float u = (i + 0.5f) / count;
                Vector2 p = alongX ? new Vector2(Mathf.Lerp(room.r.xMin, room.r.xMax, u), room.r.center.y) : new Vector2(room.r.center.x, Mathf.Lerp(room.r.yMin, room.r.yMax, u));
                s_UgLamps.Add((new Vector3(p.x, k_UgCeiling, p.y), alongX));
            }
        }

        // 합친 뒤: 원기둥 · 전등을 놓고, 지하 구조물의 환경광을 줄인다
        static void UgFinish(Transform group, List<UgRoom> rooms)
        {
            Transform fixtures = Group("Fixtures", group);
            float height = k_UgCeiling - k_UgFloor;
            Mesh shaft = CylinderMesh("UgColumn", k_UgColumnRadius, height, 24, 2.5f, k_UgFloor);
            Mesh footing = CylinderMesh("UgColumnBase", k_UgColumnRadius + 0.03f, 0.12f, 24, 1f, 0f);
            Mesh collar = CylinderMesh("UgColumnTop", k_UgColumnRadius + 0.03f, 0.08f, 24, 1f, 0f);
            foreach (Vector3 c in s_UgColumns)
            {
                var column = new GameObject("Column");
                column.transform.SetParent(fixtures, false);
                column.transform.position = new Vector3(c.x, k_UgFloor, c.z);
                UgPart(column, shaft, s_Ug.column);
                // 캡슐 — 둥근 끝은 바닥 · 천장 속이라 방 안에서는 곧은 원기둥
                var capsule = column.AddComponent<CapsuleCollider>();
                capsule.direction = 1;
                capsule.radius = k_UgColumnRadius;
                capsule.height = height + k_UgColumnRadius * 2f;
                capsule.center = new Vector3(0f, height * 0.5f, 0f);
                UgPart(UgChild(column.transform, "Base", Vector3.zero), footing, s_Ug.trim);
                UgPart(UgChild(column.transform, "Top", new Vector3(0f, height - 0.08f, 0f)), collar, s_Ug.trim);
            }
            foreach ((Vector3 at, bool alongX) in s_UgLamps)
            {
                // 형광등: 천장에 붙은 몸통(1.3 m) + 그 밑 빛나는 관, 빛은 관 바로 아래에서 아래로
                var lamp = new GameObject("Lamp");
                lamp.transform.SetParent(fixtures, false);
                lamp.transform.position = at;
                Vector3 half = alongX ? new Vector3(0.65f, 0f, 0.08f) : new Vector3(0.08f, 0f, 0.65f);
                Vector3 tube = alongX ? new Vector3(0.6f, 0f, 0.05f) : new Vector3(0.05f, 0f, 0.6f);
                UgLayer(Box("LampBody", lamp.transform, new Vector3(-half.x, -0.05f, -half.z), new Vector3(half.x, 0f, half.z), s_Ug.trim, false));
                GameObject glow = Box("LampTube", lamp.transform, new Vector3(-tube.x, -0.08f, -tube.z), new Vector3(tube.x, -0.05f, tube.z), s_Ug.lampGlow, false);
                UgLayer(glow);
                glow.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                var lightGo = new GameObject("LampLight");
                lightGo.transform.SetParent(lamp.transform, false);
                lightGo.transform.localPosition = new Vector3(0f, -0.12f, 0f);
                lightGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Spot;
                light.spotAngle = 150f;
                light.innerSpotAngle = 80f;
                light.range = 10f;
                light.intensity = k_UgLampIntensity;
                light.color = new Color(0.84f, 0.95f, 0.9f);
                light.shadows = LightShadows.None;
                light.lightmapBakeType = LightmapBakeType.Realtime;
                // 지하 구조물(레이어 1)과 사람 · 총(기본 레이어) 둘 다 비춘다 — 총구 불빛과 같다.
                // URP 는 이 값을 조명의 추가 데이터에서 읽는다 — 없으면 기본 레이어만 비춰서 지하 벽 · 바닥에 빛이 안 닿는다 (11/18 미리보기)
                var layers = new RenderingLayerMask { value = k_UgRenderingLayer | 1u };
                lightGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>().renderingLayers = layers;
                light.renderingLayerMask = (int)layers.value;
            }

            // 환경광 — 덮인 방의 렌더러는 '직접 준 값'으로, 값은 UndergroundAmbient 가 넣는다
            var rooms3 = new List<Bounds>();
            foreach (UgRoom room in rooms)
                if (!room.open)
                    rooms3.Add(new Bounds(new Vector3(room.r.center.x, (k_UgFloor + k_UgCeiling) * 0.5f, room.r.center.y), new Vector3(room.r.width, height, room.r.height)));
            foreach (Renderer r in group.GetComponentsInChildren<Renderer>(true))
                if (r.renderingLayerMask == k_UgRenderingLayer)
                    r.lightProbeUsage = LightProbeUsage.CustomProvided;
            var ambient = group.gameObject.AddComponent<UndergroundAmbient>();
            var so = new SerializedObject(ambient);
            SerializedProperty list = so.FindProperty("m_Rooms");
            list.arraySize = rooms3.Count;
            for (int i = 0; i < rooms3.Count; i++)
                list.GetArrayElementAtIndex(i).boundsValue = rooms3[i];
            so.FindProperty("m_StaticLayer").longValue = k_UgRenderingLayer;
            so.ApplyModifiedPropertiesWithoutUndo();
            ambient.ApplyStatic();
            UgReflectionProbe(group, rooms3);
        }

        static GameObject UgChild(Transform parent, string name, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go;
        }

        static void UgPart(GameObject go, Mesh mesh, Material mat)
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            UgLayer(go);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic);
        }

        static void UgLayer(GameObject go) => go.GetComponent<MeshRenderer>().renderingLayerMask = k_UgRenderingLayer;

        // 세로 원기둥 (밑면 가운데가 원점, 위아래는 막지 않는다 — 바닥 · 천장에 닿는다). 옆면 법선은 부드럽게.
        // UV: 가로 = 둘레를 따라 0 ~ around (그림 몇 장), 세로 = 높이 + vStart (m — 벽과 같은 재질 줄 맞춤)
        static Mesh CylinderMesh(string name, float radius, float height, int sides, float around, float vStart)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var tris = new List<int>();
            for (int i = 0; i <= sides; i++)
            {
                float t = (float)i / sides, angle = t * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                v.Add(dir * radius);
                v.Add(dir * radius + Vector3.up * height);
                n.Add(dir);
                n.Add(dir);
                uv.Add(new Vector2(t * around, vStart));
                uv.Add(new Vector2(t * around, vStart + height));
            }
            for (int i = 0; i < sides; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                tris.AddRange(new[] { a, b, c, c, b, d });
            }
            // 띠(받침 · 머리)는 윗면 · 아랫면이 보인다 — 납작한 것만 막는다
            if (height < 0.5f)
                foreach (bool top in new[] { true, false })
                {
                    int center = v.Count;
                    v.Add(Vector3.up * (top ? height : 0f));
                    n.Add(top ? Vector3.up : Vector3.down);
                    uv.Add(Vector2.zero);
                    for (int i = 0; i <= sides; i++)
                    {
                        float angle = (float)i / sides * Mathf.PI * 2f;
                        v.Add(new Vector3(Mathf.Cos(angle) * radius, top ? height : 0f, Mathf.Sin(angle) * radius));
                        n.Add(top ? Vector3.up : Vector3.down);
                        uv.Add(Vector2.zero);
                    }
                    for (int i = 0; i < sides; i++)
                        tris.AddRange(top ? new[] { center, center + 2 + i, center + 1 + i } : new[] { center, center + 1 + i, center + 2 + i });
                }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        // ---------------------------------------------------------------- 지하만 다시 짓기

        // 지하 묶음을 parent 아래에 새로 짓는다 (재질 · 원기둥 · 전등까지). 배치(C_Layout)의 지하 상자를 읽는다. 없으면 null
        static Transform BuildUndergroundGroup(Transform parent, string name)
        {
            GameObject layout = UrbanLayoutTools.FindRoot(UrbanLayoutTools.LayoutRootName);
            if (layout == null)
                return null;
            GreyboxUndergroundSpec[] undergrounds = layout.GetComponentsInChildren<GreyboxUndergroundSpec>(true);
            GreyboxGroundSpec[] grounds = layout.GetComponentsInChildren<GreyboxGroundSpec>(true);
            Materials m = LoadOrCreateMaterials();
            ResetProps();
            List<UgRoom> rooms = PlanUnderground(undergrounds, grounds, m);
            if (rooms.Count == 0)
                return null;
            Transform group = Group(name, parent);
            BuildUnderground(group, rooms, m);
            CombineVisuals(group);
            UgFinish(group, rooms);
            return group;
        }

        // 맵 전체를 다시 짓지 않고 지하(C_Greybox/Underground)만 — 다른 건물 · 땅 · 길찾기 지도는 그대로 둔다.
        // 기둥이 네모(0.8 m)에서 같은 폭 원기둥이 되는 것뿐이라 길찾기 지도는 다시 굽지 않는다. 씬은 저장하지 않는다
        [MenuItem("MilTrainSimVR/C/지하만 다시 짓기 (재질 · 원기둥 · 전등)", false, 21)]
        public static void RebuildUnderground()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[C] Play 중에는 지하를 다시 짓지 않는다");
                return;
            }
            GameObject root = UrbanLayoutTools.FindRoot(k_RootName);
            if (root == null)
            {
                Debug.LogWarning("[C] 이 씬에는 맵(C_Greybox)이 없다");
                return;
            }
            Transform old = root.transform.Find("Underground");
            int index = old != null ? old.GetSiblingIndex() : -1;
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);
            Transform group = BuildUndergroundGroup(root.transform, "Underground");
            if (group == null)
            {
                Debug.LogWarning("[C] 지하 상자가 없다 (C_Layout)");
                return;
            }
            Undo.RegisterCreatedObjectUndo(group.gameObject, "지하 다시 짓기");
            if (index >= 0)
                group.SetSiblingIndex(index);
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log($"[C] 지하를 다시 지었다 — 원기둥 {s_UgColumns.Count}개 · 전등 {s_UgLamps.Count}개");
        }
    }
}
