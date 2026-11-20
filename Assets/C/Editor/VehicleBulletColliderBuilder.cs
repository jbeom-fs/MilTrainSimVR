using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MilTrainSimVR.UrbanCombat.EditorTools
{
    // 차량 총알 판정 = 보이는 차체 (11/18 사용자: "차량에도 투명벽 같은 게 있어서 맞춰야 할 목표물에 안 맞고 투명 벽에 막혀 총탄이 박힌다").
    // 차량 프리팹(uveh_prefab.cs 가 만든 Prefabs/Vehicle/*)의 판정은 몸통 상자 + 객실 상자 둘뿐이었다 → 보닛 · 앞유리 · 지붕 끝처럼
    // 차체가 상자보다 작은 곳에서 총알이 허공에 박혔다. 고친 방법:
    //   · 상자 둘이 붙은 뿌리를 'Ignore Raycast' 층으로 — 몸(플레이어 · 적)은 그대로 막고, 길찾기 지도도 그대로(굽기는 모든 층을 본다).
    //     총알 · 시야 · 엄폐 판단(Gunfire.ShotMask)은 이 층을 보지 않는다 (백화점 난간 · 맵 끝 벽과 같은 방식)
    //   · 모델(.fbx 자식)에 MeshCollider — 보이는 차체 메시 그대로(위치만, 같은 자리 꼭짓점은 합침)를 따로 저장한 메시.
    //     모델 자식에 붙여서, 씬에서 모델만 옮긴 차(세단 3)도 판정이 모델을 따라간다.
    //     따로 저장하는 까닭: 원본 FBX 는 Read/Write 가 꺼져 있고 모델 배율이 축마다 달라(Model 의 크기 보정)
    //     빌드에서 실행 중에 판정을 만들려면 읽을 수 있는 메시가 있어야 한다 (Prebake Collision Meshes 는 꺼져 있다)
    // 다시 눌러도 된다 (메시는 같은 파일에 덮어쓰고, MeshCollider 는 하나만). uveh_prefab.cs 로 프리팹을 다시 만들면 이 메뉴도 다시 누른다
    static class VehicleBulletColliderBuilder
    {
        const string k_PrefabFolder = "Assets/C/Prefabs/Vehicle";
        const int k_IgnoreRaycastLayer = 2;

        [MenuItem("MilTrainSimVR/C/차량 총알 판정 맞추기 (보이는 차체 = 총알 판정, 상자는 몸만 막음)", false, 58)]
        static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[C] 차량 총알 판정 — Play 중에는 하지 않는다");
                return;
            }
            var report = new System.Text.StringBuilder();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { k_PrefabFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    MeshFilter model = root.GetComponentInChildren<MeshFilter>(true);
                    if (model == null || model.sharedMesh == null || root.GetComponent<BoxCollider>() == null)
                    {
                        report.Append($"\n  {path}: 모델 · 상자가 없어 건너뜀");
                        continue;
                    }
                    root.layer = k_IgnoreRaycastLayer;   // 상자 둘은 뿌리에 있다 — 몸만 막는다
                    Mesh collision = SaveCollisionMesh(model.sharedMesh);
                    var colliders = model.GetComponents<MeshCollider>();
                    MeshCollider mc = colliders.Length > 0 ? colliders[0] : model.gameObject.AddComponent<MeshCollider>();
                    for (int i = 1; i < colliders.Length; i++)
                        Object.DestroyImmediate(colliders[i]);
                    mc.sharedMesh = collision;
                    mc.convex = false;
                    model.gameObject.layer = 0;   // Default — 총알 · 시야가 본다
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    report.Append($"\n  {System.IO.Path.GetFileNameWithoutExtension(path)}: 상자 → Ignore Raycast, '{model.name}' 에 판정 메시 {collision.name} (삼각형 {collision.triangles.Length / 3}, 꼭짓점 {collision.vertexCount})");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            Debug.Log("[C] 차량 총알 판정을 보이는 차체에 맞췄다" + report);
        }

        // 보이는 메시에서 위치 · 삼각형만 — 같은 자리 꼭짓점(UV 이음매에서 갈라진 것)은 합친다. FBX 옆에 <이름>_BulletCollider.asset
        // (지도 생성기의 가판대도 쓴다 — UrbanGreyboxBuilder.BulletMesh, 11/19)
        internal static Mesh SaveCollisionMesh(Mesh source)
        {
            string folder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(source)).Replace('\\', '/');
            string path = $"{folder}/{source.name}_BulletCollider.asset";
            Vector3[] v = source.vertices;
            var index = new System.Collections.Generic.Dictionary<Vector3, int>(v.Length);
            var positions = new System.Collections.Generic.List<Vector3>(v.Length);
            var remap = new int[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                if (!index.TryGetValue(v[i], out int id))
                {
                    id = positions.Count;
                    index.Add(v[i], id);
                    positions.Add(v[i]);
                }
                remap[i] = id;
            }
            var triangles = new System.Collections.Generic.List<int>(source.triangles.Length);
            for (int s = 0; s < source.subMeshCount; s++)
            {
                int[] t = source.GetTriangles(s);
                for (int i = 0; i < t.Length; i += 3)
                {
                    int a = remap[t[i]], b = remap[t[i + 1]], c = remap[t[i + 2]];
                    if (a == b || b == c || a == c)
                        continue;
                    triangles.Add(a);
                    triangles.Add(b);
                    triangles.Add(c);
                }
            }
            var mesh = new Mesh { name = source.name + "_BulletCollider" };
            mesh.indexFormat = positions.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(positions);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssetIfDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
    }
}
