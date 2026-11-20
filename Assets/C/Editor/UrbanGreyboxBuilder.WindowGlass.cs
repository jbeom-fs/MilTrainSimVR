using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MilTrainSimVR.UrbanCombat.EditorTools
{
    // 창 유리 (사용자 11/18: "모든 창틀에 유리창을 설치" · "백화점 유리창은 좀 더 고급진 유리창" · "공영주차장 부스에도").
    // 창틀마다 통유리 한 장 — 깨지는 유리(BreakableGlass): 콜라이더가 없어 적 시선 · 총알이 그대로 지나가고, 총알이 지나가면 그 판이 통째로 부서진다.
    // 든 총의 개머리판으로 쳐도 깨진다 (m_StockBreaks — TempRifle.StockStrike). 안 깨진 유리는 몸을 막는다 — 판마다 'Ignore Raycast' 층의
    // 상자(Body)가 붙고 깨지는 순간 꺼진다. 총알 · 적 시선은 그 층을 안 보니 그대로 지나간다.
    //   · 일반 건물: 창틀(Details.cs 의 GB_Ext_Frame 네 변) 안에 맑은 유리(GB_Window_Glass). 셔터 · 벽으로 다 막힌 창은 달지 않는다
    //   · 백화점(세라믹 외벽, Facade.cs 의 청동 틀): 무늬 유리(GB_Dept_WindowGlass) — 옅은 청동빛 판유리에 둘레 젖빛 띠 · 금빛 줄 두 가닥 ·
    //     네 귀퉁이 해돋이 부채(외벽 꼭대기 금색 해돋이와 같은 무늬). 그림은 9 조각으로 붙어 창 크기가 달라도 테두리 무늬 크기가 같다
    //   · 주차장 관리 부스: 프리팹(C_ParkingBooth) 창 셋에 맑은 유리 — 프리팹에 넣어 씬의 부스가 모두 따라온다
    // 창 자리는 지어진 씬의 창틀 메시에서 읽는다(이어진 덩어리 하나 = 창틀 하나) — 맵을 다시 짓지 않고도 지금 씬에 달 수 있다.
    // 맵 만들기(Rebuild) 끝 · 막힌 곳 셔터 다시 달기(UrbanClosureBuilder) 끝에서도 부른다. 다시 불러도 같다(지난 유리를 지우고 다시). 씬은 저장하지 않는다
    public static partial class UrbanGreyboxBuilder
    {
        const string k_WindowGlassHolder = "WindowGlass_Breakable";
        const string k_GlassShardsPrefab = "Assets/C/Prefabs/FX/C_GlassShards.prefab";
        const string k_GlassCrackedMaterial = "Assets/C/Prefabs/FX/C_GlassCracked.mat";
        const string k_DeptGlassTexture = "Assets/C/Textures/Map/DeptWindowGlass/DeptWindowGlass.png";
        const string k_ClearGlassTexture = "Assets/C/Textures/Map/WindowGlass/WindowGlass.png";
        const float k_PaneThickness = 0.006f, k_DeptPaneThickness = 0.012f;
        const float k_PaneBodyThickness = 0.04f;   // 몸을 막는 상자의 두께 — 보이는 유리보다 두껍게 (얇으면 빠르게 움직일 때 뚫린다)
        // 유리 그림은 9 조각으로 붙는다: 그림 한 변의 k_GlassBorderShare(네 귀퉁이)가 창 가장자리 이 폭(m)에 맞고 가운데는 늘어난다
        const float k_DeptGlassBorder = 0.30f, k_ClearGlassBorder = 0.25f;
        const float k_GlassBorderShare = 0.25f;
        // 창틀 치수 — 창 자리를 틀 메시에서 거꾸로 읽는 데 쓴다. 틀을 만드는 쪽을 바꾸면 여기도 같이:
        const float k_ExtFrameBar = 0.045f;      // Details.cs 창틀 네 변의 폭 (fw)
        const float k_Dept1FRing = 0.08f;        // Facade.cs Frame1F — 구멍 바깥 8 cm 청동 띠
        const float k_Dept1FRingOut = 0.02f;     //   그 띠가 화강석 겉면(k_FacadeGraniteT)에서 나온 만큼
        const float k_DeptInnerInset = 0.02f;    // Facade.cs InnerFrame — 구멍 가장자리에서 2 cm 안쪽에 선 세 변
        const float k_Dept1FGlassRecess = 0.06f; // 1층 유리가 벽 겉면에서 들어간 깊이

        [MenuItem("MilTrainSimVR/C/창틀에 깨지는 유리창 달기 (건물 · 백화점 · 주차장 부스)", false, 60)]
        static void InstallWindowGlassMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[C] 창 유리 — Play 중에는 하지 않는다");
                return;
            }
            GameObject root = GameObject.Find(k_RootName);
            if (root == null)
            {
                Debug.LogWarning("[C] 창 유리 — C_Greybox 가 없다 (C_UrbanBlock 을 열고 누를 것)");
                return;
            }
            Debug.Log(InstallWindowGlass(root.transform));
            EditorSceneManager.MarkSceneDirty(root.scene);
        }

        // greybox 아래 건물마다 창 유리를 다시 단다 + 부스 프리팹. 돌려주는 것은 한 일 요약
        public static string InstallWindowGlass(Transform greybox)
        {
            var shards = AssetDatabase.LoadAssetAtPath<GlassShards>(k_GlassShardsPrefab);
            var cracked = AssetDatabase.LoadAssetAtPath<Material>(k_GlassCrackedMaterial);
            Material clear = ClearWindowGlassMat();
            Material dept = DeptWindowGlassMat();
            var log = new System.Text.StringBuilder("[C] 창 유리를 달았다 —");
            int total = 0, skipped = 0;
            Physics.SyncTransforms();
            Transform buildings = greybox != null ? greybox.Find("Buildings") : null;
            if (buildings != null)
            {
                var meshes = new Dictionary<string, Mesh>();
                foreach (Transform building in buildings)
                {
                    Transform old = building.Find(k_WindowGlassHolder);
                    if (old != null)
                        Undo.DestroyObjectImmediate(old.gameObject);
                    Transform holder = null;
                    int made = 0;
                    foreach (MeshRenderer mr in building.GetComponentsInChildren<MeshRenderer>(false))
                    {
                        if (mr.transform.parent != building)
                            continue;
                        Material[] mats = mr.sharedMaterials;
                        for (int sub = 0; sub < mats.Length; sub++)
                        {
                            if (mats[sub] == null || (mats[sub].name != "GB_Ext_Frame" && mats[sub].name != "GB_Facade_Bronze"))
                                continue;
                            bool deptFrame = mats[sub].name == "GB_Facade_Bronze";
                            List<Bounds> frames = FrameOpenings(building, mr, sub);
                            if (frames.Count == 0)
                                continue;
                            // 건물 안쪽이 어느 쪽인지 — 틀 전체의 가운데 (틀은 바깥벽을 따라 둘러 있다)
                            Bounds all = frames[0];
                            foreach (Bounds b in frames)
                                all.Encapsulate(b);
                            foreach (Bounds b in frames)
                            {
                                if (!GlassSpot(b, all.center, deptFrame, out bool alongX, out float a0, out float a1, out float y0, out float y1, out float depth))
                                    continue;
                                Vector3 A = building.TransformPoint(alongX ? new Vector3(a0, y0, depth) : new Vector3(depth, y0, a0));
                                Vector3 B = building.TransformPoint(alongX ? new Vector3(a1, y0, depth) : new Vector3(depth, y0, a1));
                                Vector3 normal = building.TransformDirection(alongX ? Vector3.forward : Vector3.right);
                                if (GlassSpotBlocked(A, B, y1 - y0, normal))
                                {
                                    skipped++;
                                    continue;
                                }
                                if (holder == null)
                                {
                                    holder = new GameObject(k_WindowGlassHolder).transform;
                                    Undo.RegisterCreatedObjectUndo(holder.gameObject, "창 유리");
                                    holder.SetParent(building, false);
                                }
                                float length = Vector3.Distance(A, B), height = y1 - y0;
                                float thickness = deptFrame ? k_DeptPaneThickness : k_PaneThickness, border = deptFrame ? k_DeptGlassBorder : k_ClearGlassBorder;
                                string key = $"{length:0.000}x{height:0.000}x{thickness:0.000}_{border:0.00}";
                                if (!meshes.TryGetValue(key, out Mesh mesh))
                                    meshes[key] = mesh = GlassPaneMesh($"WindowGlass_{key}", length, height, thickness, border);
                                int floor = Mathf.FloorToInt((y0 - k_Curb) / k_FloorHeight) + 1;
                                GlassPane(holder, $"WindowGlass_{floor}F_{made}", A, B, height, normal, mesh, deptFrame ? dept : clear, thickness, border, shards, cracked, true);
                                made++;
                            }
                        }
                    }
                    if (made > 0)
                        log.Append($" {building.name.Replace("B_", "")} {made}");
                    total += made;
                }
            }
            int booth = GlazeBoothPrefab();
            log.Append($" · 건물 창 {total}장 (셔터 · 벽에 막혀 안 단 창 {skipped}) · 부스 프리팹 {booth}장");
            return log.ToString();
        }

        // 맑은 창 유리 — 에스컬레이터 난간 유리(0.5)보다 훨씬 맑게: 적은 유리 너머를 그대로 보니 플레이어도 잘 보여야 한다.
        // 한 가지 색으로 옅게 칠하면(0.2) 판 전체에 뿌연 막이 끼어 안이 흐려 보였다(11/18 렌더) → 가운데는 거의 안 보이게 맑고,
        // 가장자리로 갈수록 먼지가 낀 그림(ClearWindowGlassTexture)으로 유리가 있다는 걸 알린다. 나머지는 창틀과 비친 하늘(매끈함 0.95)
        // 정면 반사율 0.2 — 유리 기본값(0.04)으로는 비친 하늘이 거의 안 보여 창이 뚫린 것처럼 보였다. 0.3 은 안이 흐려진다 (11/18 렌더 비교)
        static Material ClearWindowGlassMat() => TexturedGlassMat("GB_Window_Glass", ClearWindowGlassTexture(), 0.95f, 0.2f);

        // 그림(알파 = 불투명도)을 입힌 비치는 재질. 있는 재질은 그대로 쓴다 — 값은 그 .mat 에서 고친다
        // specular: 정면 반사율(비친 하늘의 세기) — 0 이면 유리 기본값(0.04) 그대로
        static Material TexturedGlassMat(string name, Texture2D texture, float smoothness, float specular = 0f)
        {
            string path = $"{k_MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
                return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BaseMap", texture);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            BaseShaderGUI.SetMaterialKeywords(mat);
            if (specular > 0f)
            {
                mat.SetFloat("_WorkflowMode", 0f);
                mat.SetColor("_SpecColor", new Color(specular, specular, specular, 1f));
                mat.EnableKeyword("_SPECULAR_SETUP");
            }
            mat.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // 맑은 창 유리 그림 (512², 알파 = 불투명도) — 없을 때만 그린다. 네 귀퉁이 1/4 이 창 가장자리 k_ClearGlassBorder 에 맞는다(9 조각).
        //   가운데: 거의 안 보임(0.05). 가장자리 25 cm: 틀에 가까울수록 먼지(틀에서 약 0.3, 아래 틀 쪽이 조금 더) · 얼룩
        static Texture2D ClearWindowGlassTexture()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(k_ClearGlassTexture);
            if (texture != null)
                return texture;
            const int size = 512;
            var pixels = new Color[size * size];
            for (int j = 0; j < size; j++)
            {
                float v = (j + 0.5f) / size;
                float fy = Mathf.Pow(1f - EdgeDistance(v, k_ClearGlassBorder) / k_ClearGlassBorder, 2.2f) * (v < 0.5f ? 1.15f : 0.85f);
                for (int i = 0; i < size; i++)
                {
                    float fx = Mathf.Pow(1f - EdgeDistance((i + 0.5f) / size, k_ClearGlassBorder) / k_ClearGlassBorder, 2.2f);
                    float edge = 1f - (1f - fx) * (1f - Mathf.Min(1f, fy));
                    float blotch = Mathf.PerlinNoise(i * 0.05f + 11f, j * 0.05f + 3f) * 0.7f + Mathf.PerlinNoise(i * 0.21f, j * 0.21f + 70f) * 0.3f;
                    float alpha = 0.05f + 0.27f * edge * (0.6f + 0.8f * blotch);
                    pixels[j * size + i] = new Color(0.74f, 0.78f, 0.77f, Mathf.Clamp01(alpha));
                }
            }
            return SaveGlassTexture(k_ClearGlassTexture, size, pixels);
        }

        // 그림 좌표(0 ~ 1) → 가까운 창 가장자리까지 거리(m). 가운데(늘어나는 구역)는 테두리 폭으로 본다 — 무늬는 모두 그 안쪽에 있다
        static float EdgeDistance(float t, float border)
        {
            float e = Mathf.Min(t, 1f - t);
            return e >= k_GlassBorderShare ? border : e / k_GlassBorderShare * border;
        }

        // 그림을 PNG 로 쓰고 들여온다 — 알파는 투명도, 가장자리는 늘이지 않게(Clamp)
        static Texture2D SaveGlassTexture(string path, int size, Color[] pixels)
        {
            var made = new Texture2D(size, size, TextureFormat.RGBA32, false);
            made.SetPixels(pixels);
            made.Apply();
            string folder = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(folder).Replace('\\', '/'), System.IO.Path.GetFileName(folder));
            System.IO.File.WriteAllBytes(path, made.EncodeToPNG());
            Object.DestroyImmediate(made);
            AssetDatabase.ImportAsset(path);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.anisoLevel = 8;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // 틀 한 덩어리(건물 좌표) → 유리 자리: 벽을 따라 a0 ~ a1, 높이 y0 ~ y1, 벽 두께 방향 자리 depth. 문(바닥부터 뚫린 구멍) · 자잘한 틀은 false
        static bool GlassSpot(Bounds b, Vector3 inside, bool deptFrame, out bool alongX, out float a0, out float a1, out float y0, out float y1, out float depth)
        {
            alongX = b.size.x >= b.size.z;
            float lo = alongX ? b.min.x : b.min.z, hi = alongX ? b.max.x : b.max.z;
            float thin0 = alongX ? b.min.z : b.min.x, thin1 = alongX ? b.max.z : b.max.x;
            a0 = a1 = y0 = y1 = depth = 0f;
            if (hi - lo < 0.5f || b.size.y < 0.5f)
                return false;
            // 층 바닥에서 틀 아래 끝까지 — 문은 바닥부터, 창은 창턱(0.5 m ~) 위
            float above = Mathf.Repeat(b.min.y - k_Curb + 0.2f, k_FloorHeight) - 0.2f;
            if (above < 0.3f)
                return false;
            if (!deptFrame)
            {
                // 창틀 네 변(폭 4.5 cm, 깊이 5 cm) 안에 — 가장자리 1.2 cm 는 틀 속에 묻힌다
                if (thin1 - thin0 > 0.1f)
                    return false;
                float inset = k_ExtFrameBar - 0.012f;
                a0 = lo + inset; a1 = hi - inset; y0 = b.min.y + inset; y1 = b.max.y - inset;
                depth = (thin0 + thin1) * 0.5f;
                return true;
            }
            if (thin1 - thin0 > 0.15f)
            {
                // 백화점 1층: 바깥 8 cm 청동 띠 + 구멍 안쪽을 두른 청동 판(벽 두께) — 유리는 구멍에 맞춰, 벽 겉면에서 조금 들어간 자리
                a0 = lo + k_Dept1FRing; a1 = hi - k_Dept1FRing; y0 = b.min.y + k_Dept1FRing; y1 = b.max.y - k_Dept1FRing;
                float center = alongX ? inside.z : inside.x;
                bool outIsHigh = (thin0 + thin1) * 0.5f > center;
                float back = k_FacadeGraniteT + k_Dept1FRingOut + k_Dept1FGlassRecess;
                depth = outIsHigh ? thin1 - back : thin0 + back;
                return true;
            }
            // 백화점 위층: 구멍 가장자리 2 cm 안쪽에 선 청동 안쪽 틀(양옆 · 위) — 유리는 그 틀 가운데 면에, 구멍 가장자리까지
            a0 = lo - k_DeptInnerInset; a1 = hi + k_DeptInnerInset; y0 = b.min.y - k_DeptInnerInset; y1 = b.max.y + k_DeptInnerInset;
            depth = (thin0 + thin1) * 0.5f;
            return true;
        }

        // 창 구멍이 다 막혔나 — 구멍 안 아홉 점에서 벽 두께 방향 양쪽으로 쏴 본다. 내린 셔터(UrbanClosureBuilder) · 창을 덮은 벽이면 유리를 달지 않는다
        static bool GlassSpotBlocked(Vector3 a, Vector3 b, float height, Vector3 normal)
        {
            for (int iy = 0; iy < 3; iy++)
                for (int ia = 0; ia < 3; ia++)
                {
                    Vector3 p = Vector3.Lerp(a, b, 0.2f + 0.3f * ia) + Vector3.up * (height * (0.2f + 0.3f * iy));
                    if (!Physics.Raycast(p - normal * 0.45f, normal, 0.9f, Gunfire.ShotMask, QueryTriggerInteraction.Ignore)
                        && !Physics.Raycast(p + normal * 0.45f, -normal, 0.9f, Gunfire.ShotMask, QueryTriggerInteraction.Ignore))
                        return false;
                }
            return true;
        }

        // 합친 메시에서 그 재질 면들의 이어진 덩어리마다 상자(건물 좌표). 같은 자리 꼭짓점(1 mm)은 하나로 본다 — 창틀 네 변은 귀퉁이에서 만난다
        static List<Bounds> FrameOpenings(Transform building, MeshRenderer mr, int submesh)
        {
            var result = new List<Bounds>();
            MeshFilter mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
                return result;
            Mesh mesh = mf.sharedMesh;
            Vector3[] verts = mesh.vertices;
            int[] tris = mesh.GetTriangles(Mathf.Min(submesh, mesh.subMeshCount - 1));
            var local = new Vector3[verts.Length];
            var weld = new Dictionary<Vector3Int, int>();
            var id = new int[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                local[i] = building.InverseTransformPoint(mr.transform.TransformPoint(verts[i]));
                var key = Vector3Int.RoundToInt(local[i] * 1000f);
                if (!weld.TryGetValue(key, out int w))
                    weld[key] = w = weld.Count;
                id[i] = w;
            }
            int[] uf = Enumerable.Range(0, weld.Count).ToArray();
            int Find(int x) { while (uf[x] != x) x = uf[x] = uf[uf[x]]; return x; }
            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                int a = Find(id[tris[t]]);
                uf[Find(id[tris[t + 1]])] = a;
                uf[Find(id[tris[t + 2]])] = a;
            }
            var boxes = new Dictionary<int, Bounds>();
            foreach (int i in tris)
            {
                int r = Find(id[i]);
                if (boxes.TryGetValue(r, out Bounds b))
                {
                    b.Encapsulate(local[i]);
                    boxes[r] = b;
                }
                else
                    boxes[r] = new Bounds(local[i], Vector3.zero);
            }
            result.AddRange(boxes.Values);
            // 다시 불러도 판 차례 · 이름이 같게
            result.Sort((p, q) => p.min.y != q.min.y ? p.min.y.CompareTo(q.min.y) : p.min.x != q.min.x ? p.min.x.CompareTo(q.min.x) : p.min.z.CompareTo(q.min.z));
            return result;
        }

        // 유리 한 장 — a · b: 아래 두 귀퉁이(월드), normal: 두께 방향. 판의 로컬 축은 BreakableGlass 와 같다 (x 유리를 따라 · y 위 · z 두께, 원점은 아래 왼쪽)
        static void GlassPane(Transform holder, string name, Vector3 a, Vector3 b, float height, Vector3 normal, Mesh mesh, Material glass, float thickness,
            float uvBorder, GlassShards shards, Material cracked, bool undo)
        {
            Quaternion rotation = Quaternion.LookRotation(normal, Vector3.up);
            Vector3 right = rotation * Vector3.right;
            float length = Vector3.Distance(a, b);
            var go = new GameObject(name);
            if (undo)
                Undo.RegisterCreatedObjectUndo(go, "창 유리");
            go.transform.SetParent(holder, false);
            go.transform.SetPositionAndRotation(Vector3.Dot(a, right) <= Vector3.Dot(b, right) ? a : b, rotation);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = glass;
            mr.shadowCastingMode = ShadowCastingMode.Off;   // 비치는 재질 — URP 그림자는 알파를 몰라 유리가 벽처럼 그늘진다
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.BlendProbes;
            mr.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            // 정적(static)으로 두지 않는다 — 정적 묶음이 되면 깨질 때 바꾼 메시가 안 보인다
            GameObjectUtility.SetStaticEditorFlags(go, 0);

            var breakable = go.AddComponent<BreakableGlass>();
            var so = new SerializedObject(breakable);
            SerializedProperty outline = so.FindProperty("m_Outline");
            var corners = new[] { new Vector2(0f, 0f), new Vector2(length, 0f), new Vector2(length, height), new Vector2(0f, 0f), new Vector2(length, height), new Vector2(0f, height) };
            outline.arraySize = corners.Length;
            for (int i = 0; i < corners.Length; i++)
                outline.GetArrayElementAtIndex(i).vector2Value = corners[i];
            so.FindProperty("m_Length").floatValue = length;
            so.FindProperty("m_Height").floatValue = height;
            so.FindProperty("m_Thickness").floatValue = thickness;
            so.FindProperty("m_ShardsPrefab").objectReferenceValue = shards;
            so.FindProperty("m_CrackedMaterial").objectReferenceValue = cracked;
            so.FindProperty("m_StockBreaks").boolValue = true;
            // 안 깨진 동안 몸만 막는 판정 (사용자 11/18 "네, 막아 줘") — 'Ignore Raycast' 층이라 총알 · 적 시선은 지나가고, 깨지면 BreakableGlass 가 끈다
            var body = new GameObject("Body");
            body.transform.SetParent(go.transform, false);
            body.layer = k_IgnoreRaycastLayer;
            var box = body.AddComponent<BoxCollider>();
            box.center = new Vector3(length * 0.5f, height * 0.5f, 0f);
            box.size = new Vector3(length, height, k_PaneBodyThickness);
            so.FindProperty("m_BodyBlock").objectReferenceValue = box;
            so.FindProperty("m_UvBorder").floatValue = uvBorder;
            so.FindProperty("m_UvBorderShare").floatValue = k_GlassBorderShare;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 유리 판 메시 — 앞 · 뒤 두 면 (옆면은 틀 속). UV: 맑은 유리는 미터, 무늬 유리는 9 조각 (BreakableGlass.SliceUv — 깨질 때 칸 메시와 같은 식)
        static Mesh GlassPaneMesh(string name, float length, float height, float thickness, float uvBorder)
        {
            float[] xs = uvBorder > 0f ? new[] { 0f, Mathf.Min(uvBorder, length * 0.5f), length - Mathf.Min(uvBorder, length * 0.5f), length } : new[] { 0f, length };
            float[] ys = uvBorder > 0f ? new[] { 0f, Mathf.Min(uvBorder, height * 0.5f), height - Mathf.Min(uvBorder, height * 0.5f), height } : new[] { 0f, height };
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var tris = new List<int>();
            foreach (bool front in new[] { true, false })
            {
                int start = v.Count;
                float z = (front ? 0.5f : -0.5f) * thickness;
                foreach (float y in ys)
                    foreach (float x in xs)
                    {
                        v.Add(new Vector3(x, y, z));
                        n.Add(front ? Vector3.forward : Vector3.back);
                        uv.Add(uvBorder > 0f
                            ? new Vector2(BreakableGlass.SliceUv(x, length, uvBorder, k_GlassBorderShare), BreakableGlass.SliceUv(y, height, uvBorder, k_GlassBorderShare))
                            : new Vector2(x, y));
                    }
                for (int j = 0; j + 1 < ys.Length; j++)
                    for (int i = 0; i + 1 < xs.Length; i++)
                    {
                        int c00 = start + j * xs.Length + i, c10 = c00 + 1, c01 = c00 + xs.Length, c11 = c01 + 1;
                        if (front)
                            tris.AddRange(new[] { c00, c10, c11, c00, c11, c01 });
                        else
                            tris.AddRange(new[] { c00, c11, c10, c00, c01, c11 });
                    }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---------------------------------------------------------------- 주차장 관리 부스

        // 부스 프리팹의 창 셋(앞 1.6 m · 양옆 0.9 m)에 유리 — 프리팹 안에 자식으로. 있던 유리는 지우고 다시. 단 장 수
        static int GlazeBoothPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(k_BoothPrefabPath) == null)
                return 0;
            var shards = AssetDatabase.LoadAssetAtPath<GlassShards>(k_GlassShardsPrefab);
            var cracked = AssetDatabase.LoadAssetAtPath<Material>(k_GlassCrackedMaterial);
            Material glass = ClearWindowGlassMat();
            GameObject go = PrefabUtility.LoadPrefabContents(k_BoothPrefabPath);
            int made = 0;
            try
            {
                for (int i = go.transform.childCount - 1; i >= 0; i--)
                    if (go.transform.GetChild(i).name.StartsWith("Glass_"))
                        Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
                // 틀(Booth.cs OpeningFrame): 구멍 안쪽 4 cm 띠 · 창턱 띠는 창턱 ±2 cm — 유리 가장자리 1 cm 는 틀 속에
                const float side = 0.03f, bottom = 0.01f, top = 0.03f, frontHalf = 0.8f, sideHalf = 0.45f;
                float y0 = k_BoothSill + bottom, height = k_BoothHead - top - y0;
                float fz = k_BoothHalfD - k_BoothWall * 0.5f, sx = k_BoothHalfW - k_BoothWall * 0.5f;
                Mesh front = BoothGlassMesh("C_ParkingBooth_Glass_Front", (frontHalf - side) * 2f, height);
                Mesh flank = BoothGlassMesh("C_ParkingBooth_Glass_Side", (sideHalf - side) * 2f, height);
                GlassPane(go.transform, "Glass_Front", new Vector3(-frontHalf + side, y0, fz), new Vector3(frontHalf - side, y0, fz), height, Vector3.forward,
                    front, glass, k_PaneThickness, k_ClearGlassBorder, shards, cracked, false);
                GlassPane(go.transform, "Glass_Left", new Vector3(-sx, y0, -sideHalf + side), new Vector3(-sx, y0, sideHalf - side), height, Vector3.right,
                    flank, glass, k_PaneThickness, k_ClearGlassBorder, shards, cracked, false);
                GlassPane(go.transform, "Glass_Right", new Vector3(sx, y0, -sideHalf + side), new Vector3(sx, y0, sideHalf - side), height, Vector3.right,
                    flank, glass, k_PaneThickness, k_ClearGlassBorder, shards, cracked, false);
                made = 3;
                PrefabUtility.SaveAsPrefabAsset(go, k_BoothPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(go);
            }
            return made;
        }

        // 프리팹이 쓰는 메시는 파일로 (씬에 든 메시는 프리팹에 못 넣는다)
        static Mesh BoothGlassMesh(string name, float length, float height)
        {
            Mesh mesh = GlassPaneMesh(name, length, height, k_PaneThickness, k_ClearGlassBorder);
            string path = $"{k_BoothMeshFolder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            existing.Clear();
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        // ---------------------------------------------------------------- 백화점 무늬 유리

        // 옅은 청동빛 판유리 + 그림(둘레 젖빛 띠 · 금빛 줄 · 귀퉁이 해돋이). 있는 재질은 그대로 쓴다 — 값은 GB_Dept_WindowGlass.mat 에서 고친다
        static Material DeptWindowGlassMat() => TexturedGlassMat("GB_Dept_WindowGlass", DeptWindowGlassTexture(), 0.96f);

        // 무늬 유리 그림 (1024², 알파 = 불투명도) — 없을 때만 그린다(그림을 손으로 고쳤으면 그대로 둔다).
        // 그림의 네 귀퉁이 1/4 이 창 가장자리 0.30 m 에 맞는다(9 조각). 가장자리에서 잰 거리로:
        //   0 ~ 7.5 cm 젖빛(샌드블라스트) 띠 · 7.5 ~ 8.7 cm 금빛 줄 · 10.5 ~ 11 cm 가는 금빛 줄 · 그 안은 맑은 청동빛 유리.
        //   귀퉁이: 가는 줄이 꺾이는 점에서 퍼지는 해돋이 — 금빛 4분원(3.2 cm) + 젖빛 살 다섯(가운데 17 cm · 그 옆 14.5 cm · 바깥 12 cm)
        static Texture2D DeptWindowGlassTexture()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(k_DeptGlassTexture);
            if (texture != null)
                return texture;
            const int size = 1024;
            const float band = 0.075f, line0 = 0.075f, line1 = 0.087f, hair0 = 0.105f, hair1 = 0.110f, sun = 0.032f, ray0 = 0.042f;
            var clearGlass = new Color(0.60f, 0.46f, 0.30f, 0.16f);
            var frost = new Color(0.93f, 0.90f, 0.84f, 0.5f);
            var gold = new Color(0.80f, 0.62f, 0.26f, 0.93f);
            float px = k_DeptGlassBorder / (size * k_GlassBorderShare);   // 테두리 구역의 픽셀 하나 (m)
            var pixels = new Color[size * size];
            for (int j = 0; j < size; j++)
            {
                float dy = EdgeDistance((j + 0.5f) / size, k_DeptGlassBorder);
                for (int i = 0; i < size; i++)
                {
                    float dx = EdgeDistance((i + 0.5f) / size, k_DeptGlassBorder);
                    float d = Mathf.Min(dx, dy);
                    Color c = clearGlass;
                    // 젖빛 띠 — 고운 알갱이
                    float grain = (Mathf.PerlinNoise(i * 0.37f, j * 0.37f) - 0.5f) * 0.10f + (Mathf.PerlinNoise(i * 0.09f + 40f, j * 0.09f) - 0.5f) * 0.06f;
                    var frosted = new Color(frost.r, frost.g, frost.b, Mathf.Clamp01(frost.a + grain));
                    c = Color.Lerp(c, frosted, Cover(d - band, px));
                    // 해돋이 (귀퉁이에서만 — 가는 줄 안쪽)
                    float ex = dx - hair1, ey = dy - hair1;
                    if (ex > -px && ey > -px && dx < k_DeptGlassBorder && dy < k_DeptGlassBorder)
                    {
                        float r = Mathf.Sqrt(Mathf.Max(0f, ex) * Mathf.Max(0f, ex) + Mathf.Max(0f, ey) * Mathf.Max(0f, ey));
                        float wedge = Mathf.PI * 0.5f / 11f;
                        float phase = Mathf.Atan2(Mathf.Max(0f, ey), Mathf.Max(1e-6f, ex)) / wedge;
                        int index = Mathf.Clamp(Mathf.FloorToInt(phase), 0, 10);
                        float edge = Mathf.Min(phase - index, index + 1 - phase) * wedge * r;   // 살 가장자리까지 (m)
                        bool ray = index % 2 == 1;
                        float reach = index == 5 ? 0.17f : index == 3 || index == 7 ? 0.145f : 0.12f;
                        float inRay = (ray ? Cover(-edge, px) : Cover(edge, px)) * Cover(ray0 - r, px) * Cover(r - reach, px);
                        if (!ray)
                        {
                            // 살 사이 칸이라도 바로 옆 살 가장자리 반 픽셀은 섞는다 — 그 살의 길이로
                            int near = phase - index < 0.5f ? index - 1 : index + 1;
                            float nearReach = near == 5 ? 0.17f : near == 3 || near == 7 ? 0.145f : 0.12f;
                            inRay = near >= 1 && near <= 9 ? Cover(edge, px) * Cover(ray0 - r, px) * Cover(r - nearReach, px) : 0f;
                        }
                        c = Color.Lerp(c, frosted, inRay);
                        c = Color.Lerp(c, gold, Cover(r - sun, px) * Cover(-ex, px) * Cover(-ey, px));
                    }
                    // 금빛 줄 두 가닥
                    c = Color.Lerp(c, gold, Cover(line0 - d, px) * Cover(d - line1, px));
                    c = Color.Lerp(c, gold, Cover(hair0 - d, px) * Cover(d - hair1, px));
                    pixels[j * size + i] = c;
                }
            }
            return SaveGlassTexture(k_DeptGlassTexture, size, pixels);
        }

        // 부호 있는 거리(안쪽이 음수, m) → 픽셀 덮인 정도 0 ~ 1 (가장자리 한 픽셀을 부드럽게)
        static float Cover(float signedDistance, float pixel) => Mathf.Clamp01(0.5f - signedDistance / pixel);
    }
}
