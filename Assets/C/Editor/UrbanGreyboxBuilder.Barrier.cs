using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MilTrainSimVR.UrbanCombat.EditorTools
{
    // 프리캐스트 콘크리트 T 벽 프리팹 (사용자 11/09 — 굳이 들어가지 않아도 될 골목을 막아 전투 구역을 좁히고, 지하도엔 엄폐물로)
    //   · 판 하나(폭 1.5 m)짜리 프리팹 둘 — Prefabs/Barrier/C_PrecastWall_3_0m(골목 막기) · C_PrecastWall_2_4m(지하 엄폐 — 서서도 몸이 가려진다)
    //   · 씬 루트 C_Barriers 밑에 직접 놓는다(끌어다 놓기 · 복사 · 붙여넣기). 맵 만들기는 C_Greybox 만 새로 만들어서 그대로 남는다 (사용자 11/09: 배치 상자 말고 프리팹으로)
    //   · 판을 1.5 m 간격으로 이으면 2 cm 틈이 난다. 줄 길이를 맞출 땐 x 크기를 조금 늘이거나 줄인다(콜라이더도 같이)
    //   · 판 · 받침 콜라이더 둘 — 사람 · 총알 · 적 길찾기를 막는다. 적 길찾기 지도는 맵 만들기 · '적 길찾기 지도 다시 굽기' 때
    //     C_Barriers 밑 전부와 그 밖에 놓인 벽 프리팹을 넣어 굽는다 (Navigation.cs) — 벽을 놓거나 옮기면 지도를 다시 굽는다
    //   · 모양: 받침(폭 1.1 m · 0.28 m) · 턱(0.52 m · 0.12 m) · 위로 얇아지는 판(0.30 → 0.20 m) · 위쪽 양면 들어 올리는 구멍 둘.
    //     재질은 옥상 덮개 콘크리트(GB_Ext_Trim_Concrete — Textures/Map/RoofConcrete, 3 m) · 구멍은 GB_Dark. UV 는 판 기준 미터. 피벗은 바닥 가운데
    //   · Tripo 대신 코드: 평평한 판 · 받침이라 크기를 딱 맞추고 콜라이더와 모양이 똑같다. 모양을 바꾸면 메뉴로 다시 만든다(메시 · 프리팹 GUID 는 그대로)
    public static partial class UrbanGreyboxBuilder
    {
        public const string k_BarrierRootName = "C_Barriers";
        const string k_BarrierPrefabFolder = "Assets/C/Prefabs/Barrier";
        const string k_BarrierMeshFolder = "Assets/C/Models/PrecastWall";
        const float k_PanelWidth = 1.5f;
        const float k_FootHalf = 0.55f, k_FootTop = 0.28f, k_StepHalf = 0.26f, k_StepTop = 0.40f;
        const float k_SlabBottomHalf = 0.15f, k_SlabTopHalf = 0.10f;

        [MenuItem("MilTrainSimVR/C/콘크리트 벽 프리팹 만들기 (3 m · 2.4 m)", false, 54)]
        public static void MakePrecastWallPrefabs()
        {
            foreach (float h in new[] { 3f, 2.4f })
                MakePrecastWall(h);
            AssetDatabase.SaveAssets();
            Debug.Log($"[C] 콘크리트 벽 프리팹 — {k_BarrierPrefabFolder}/C_PrecastWall_3_0m · C_PrecastWall_2_4m (판 폭 {k_PanelWidth} m)");
        }

        public static string PrecastWallPrefabPath(float height) => $"{k_BarrierPrefabFolder}/C_PrecastWall_{height.ToString("0.0").Replace('.', '_')}m.prefab";

        // 적 길찾기 지도에 넣을 콘크리트 벽 — 씬 루트 C_Barriers 통째 + 그 밖(맵 C_Greybox 빼고)에 놓인 벽 프리팹.
        // C_Barriers 밑에 안 넣고 씬에 바로 끌어다 놔도 길을 막게
        static List<Transform> HandPlacedBarriers(Scene scene, GameObject map)
        {
            var list = new List<Transform>();
            foreach (GameObject top in scene.GetRootGameObjects())
            {
                if (top == map)
                    continue;
                if (top.name == k_BarrierRootName)
                {
                    list.Add(top.transform);
                    continue;
                }
                foreach (Transform t in top.GetComponentsInChildren<Transform>(true))
                {
                    if (!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))
                        continue;
                    string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject);
                    if (path != null && path.StartsWith(k_BarrierPrefabFolder + "/"))
                        list.Add(t);
                }
            }
            return list;
        }

        static GameObject MakePrecastWall(float h)
        {
            foreach (string folder in new[] { k_BarrierPrefabFolder, k_BarrierMeshFolder })
            {
                if (!AssetDatabase.IsValidFolder(folder))
                    AssetDatabase.CreateFolder(folder.Substring(0, folder.LastIndexOf('/')), folder.Substring(folder.LastIndexOf('/') + 1));
            }
            string tag = h.ToString("0.0").Replace('.', '_');
            var concrete = new UtilMesh();
            var holes = new UtilMesh();
            BarrierPanel(concrete, holes, -k_PanelWidth * 0.5f + 0.01f, k_PanelWidth * 0.5f - 0.01f, h);
            // 메시 하나에 재질 칸 둘 (0 콘크리트 · 1 구멍) — 같은 파일에 덮어써서 GUID 를 지킨다
            string meshPath = $"{k_BarrierMeshFolder}/PrecastWall_{tag}m.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            bool fresh = mesh == null;
            if (fresh)
                mesh = new Mesh();
            mesh.Clear();
            mesh.name = $"PrecastWall_{tag}m";
            var v = new List<Vector3>(concrete.v);
            v.AddRange(holes.v);
            var n = new List<Vector3>(concrete.n);
            n.AddRange(holes.n);
            var uv = new List<Vector2>(concrete.uv);
            uv.AddRange(holes.uv);
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(concrete.t, 0);
            var ht = new List<int>();
            foreach (int i in holes.t)
                ht.Add(i + concrete.v.Count);
            mesh.SetTriangles(ht, 1);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            if (fresh)
                AssetDatabase.CreateAsset(mesh, meshPath);
            else
                EditorUtility.SetDirty(mesh);

            var go = new GameObject($"C_PrecastWall_{tag}m");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = new[]
            {
                AssetDatabase.LoadAssetAtPath<Material>($"{k_MaterialFolder}/GB_Ext_Trim_Concrete.mat"),
                AssetDatabase.LoadAssetAtPath<Material>($"{k_MaterialFolder}/GB_Dark.mat"),
            };
            var slab = go.AddComponent<BoxCollider>();
            slab.center = new Vector3(0f, (k_StepTop + h) * 0.5f, 0f);
            slab.size = new Vector3(k_PanelWidth - 0.02f, h - k_StepTop, k_SlabBottomHalf * 2f);
            var foot = go.AddComponent<BoxCollider>();
            foot.center = new Vector3(0f, k_StepTop * 0.5f, 0f);
            foot.size = new Vector3(k_PanelWidth - 0.02f, k_StepTop, k_FootHalf * 2f);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, PrecastWallPrefabPath(h));
            Object.DestroyImmediate(go);
            return prefab;
        }

        // 판 하나 (u0 ~ u1, 줄 좌표: x = 줄을 따라, z = 두께 쪽, y = 위)
        static void BarrierPanel(UtilMesh c, UtilMesh dark, float u0, float u1, float hgt)
        {
            BarrierBox(c, new Vector3(u0, 0f, -k_FootHalf), new Vector3(u1, k_FootTop, k_FootHalf));
            BarrierBox(c, new Vector3(u0 + 0.01f, k_FootTop, -k_StepHalf), new Vector3(u1 - 0.01f, k_StepTop, k_StepHalf));
            // 위로 갈수록 얇아지는 판 — 앞뒤 기운 면 · 양 끝 사다리꼴 · 윗면
            float a0 = u0 + 0.02f, a1 = u1 - 0.02f, y0 = k_StepTop, y1 = hgt;
            foreach (float s in new[] { -1f, 1f })
            {
                var p0 = new Vector3(a0, y0, s * k_SlabBottomHalf);
                var p1 = new Vector3(a1, y0, s * k_SlabBottomHalf);
                var p2 = new Vector3(a1, y1, s * k_SlabTopHalf);
                var p3 = new Vector3(a0, y1, s * k_SlabTopHalf);
                Vector3 nrm = new Vector3(0f, k_SlabBottomHalf - k_SlabTopHalf, s * (y1 - y0)).normalized;
                BarrierQuad(c, s > 0 ? new[] { p1, p0, p3, p2 } : new[] { p0, p1, p2, p3 }, nrm);
                // 들어 올리는 구멍 둘 — 판 면에서 2 mm 띄운 어두운 네모
                foreach (float f in new[] { 0.25f, 0.75f })
                {
                    float uc = Mathf.Lerp(a0, a1, f), yc = y1 - 0.24f, h = 0.035f;
                    float zc = s * (Mathf.Lerp(k_SlabBottomHalf, k_SlabTopHalf, (yc - y0) / (y1 - y0)) + 0.002f);
                    var q0 = new Vector3(uc - h, yc - h, zc);
                    var q1 = new Vector3(uc + h, yc - h, zc);
                    var q2 = new Vector3(uc + h, yc + h, zc);
                    var q3 = new Vector3(uc - h, yc + h, zc);
                    BarrierQuad(dark, s > 0 ? new[] { q1, q0, q3, q2 } : new[] { q0, q1, q2, q3 }, new Vector3(0f, 0f, s));
                }
            }
            foreach (float s in new[] { -1f, 1f })   // 양 끝 (u0 · u1 쪽)
            {
                float uu = s < 0 ? a0 : a1;
                var e0 = new Vector3(uu, y0, -k_SlabBottomHalf);
                var e1 = new Vector3(uu, y0, k_SlabBottomHalf);
                var e2 = new Vector3(uu, y1, k_SlabTopHalf);
                var e3 = new Vector3(uu, y1, -k_SlabTopHalf);
                BarrierQuad(c, s < 0 ? new[] { e1, e0, e3, e2 } : new[] { e0, e1, e2, e3 }, new Vector3(s, 0f, 0f));
            }
            BarrierQuad(c, new[] { new Vector3(a0, y1, -k_SlabTopHalf), new Vector3(a1, y1, -k_SlabTopHalf), new Vector3(a1, y1, k_SlabTopHalf), new Vector3(a0, y1, k_SlabTopHalf) }, Vector3.up);
        }

        // 네 꼭짓점 면 (앞에서 볼 때 반시계 순서로 넘기면 nrm 쪽이 앞면) — UV 는 큰 축 평면 투영(미터)
        static void BarrierQuad(UtilMesh um, Vector3[] q, Vector3 nrm)
        {
            int s0 = um.v.Count;
            foreach (Vector3 p in q)
            {
                um.v.Add(p);
                um.n.Add(nrm);
                um.uv.Add(Mathf.Abs(nrm.x) > 0.7f ? new Vector2(p.z, p.y) : Mathf.Abs(nrm.y) > 0.7f ? new Vector2(p.x, p.z) : new Vector2(p.x, p.y));
            }
            um.t.AddRange(new[] { s0, s0 + 2, s0 + 1, s0, s0 + 3, s0 + 2 });
        }

        // 축에 나란한 상자 (UV 포함 — 받침 · 턱)
        static void BarrierBox(UtilMesh um, Vector3 a, Vector3 b)
        {
            Vector3 mn = Vector3.Min(a, b), mx = Vector3.Max(a, b), c = (mn + mx) * 0.5f, h = (mx - mn) * 0.5f;
            foreach (Vector3 nn in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
            {
                if (nn == Vector3.down)
                    continue;   // 밑면은 땅에 닿아 안 보인다
                Vector3 ax = nn.x != 0 ? Vector3.up : nn.y != 0 ? Vector3.forward : Vector3.right;
                Vector3 bx = Vector3.Cross(nn, ax);
                if (Vector3.Dot(Vector3.Cross(ax, bx), nn) < 0)
                    bx = -bx;
                Vector3 cc = c + Vector3.Scale(nn, h), aa = Vector3.Scale(ax, h), bb = Vector3.Scale(bx, h);
                BarrierQuad(um, new[] { cc - aa - bb, cc - aa + bb, cc + aa + bb, cc + aa - bb }, nn);
            }
        }
    }
}
