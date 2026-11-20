using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MilTrainSimVR.A.EditorTools
{
    // 조립실 벽(Room_Assembly/Shell/Wall_*)에 벽 재질을 입히고, 벽마다 실제 크기(m)대로 UV 를 넣은 상자 메시로 바꾼다.
    // 벽은 크기가 다른 상자(Cube 를 늘린 것)라 기본 Cube 메시로는 면마다 텍스처가 한 번씩 늘어난다 → 벽마다 무늬 크기가 달라진다.
    // UV 를 m 단위 ÷ k_TileMeters 로 넣으면 재질 하나로 모든 벽의 무늬 크기가 같다. 벽 크기를 바꾸면 메뉴를 다시 누른다.
    // 메시는 Models/Wall/Meshes/ 에 크기별로 저장한다 (같은 크기 벽은 같은 메시를 쓴다). 부딪힘 상자(BoxCollider)는 그대로 둔다.
    // Apply 는 사격장 사수 쪽 벽·천장 덮개도 쓴다 (11/02, IndoorRangeBuilder).
    static class WallUvBuilder
    {
        const string k_Root = "Room_Assembly/Shell";
        const string k_MaterialPath = "Assets/A/Models/Wall/Materials/Wall.mat";
        const string k_MeshFolder = "Assets/A/Models/Wall/Meshes";
        const float k_TileMeters = 4f;   // 텍스처 한 장이 덮는 크기(m) — 11/02 1 → 4 (사용자: 무늬가 잘 보이게, 사격장과 같게)

        [MenuItem("A/만들기/조립실 벽 (m 단위 UV + 벽 재질)")]
        static void Build()
        {
            var shell = GameObject.Find(k_Root);
            var material = AssetDatabase.LoadAssetAtPath<Material>(k_MaterialPath);
            if (shell == null || material == null)
            {
                Debug.LogError($"[A] {k_Root} 또는 {k_MaterialPath} 이 없다");
                return;
            }
            foreach (Transform wall in shell.transform)
                if (wall.name.StartsWith("Wall_"))
                    Apply(wall, material);
            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(shell.scene);
        }

        // 상자 하나를 내무반 벽처럼 — 크기(m)대로 UV 를 넣은 메시 + 벽 재질. 크기가 바뀌면 다시 부른다.
        // tileMeters = 텍스처 한 장이 덮는 크기. 기본은 k_TileMeters (내무반·사격장 모두 이것)
        internal static void Apply(Transform wall) => Apply(wall, AssetDatabase.LoadAssetAtPath<Material>(k_MaterialPath));

        // 재질·무늬 크기를 정해서 — 사격장 잔디(받침대 윗면·부스 바닥)도 쓴다 (11/02)
        internal static void Apply(Transform wall, Material material, float tileMeters = k_TileMeters)
        {
            if (wall == null || material == null || !wall.TryGetComponent(out MeshFilter filter) || !wall.TryGetComponent(out MeshRenderer renderer))
                return;
            if (!AssetDatabase.IsValidFolder(k_MeshFolder))
                AssetDatabase.CreateFolder("Assets/A/Models/Wall", "Meshes");
            Vector3 size = wall.lossyScale;
            string key = $"{size.x:0.###}x{size.y:0.###}x{size.z:0.###}";
            if (!Mathf.Approximately(tileMeters, k_TileMeters))
                key += $"_t{tileMeters:0.##}";   // 무늬 크기가 다르면 메시도 따로
            string path = $"{k_MeshFolder}/WallBox_{key}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var built = BuildBox(size / tileMeters);
            built.name = $"WallBox_{key}";
            if (mesh == null)
            {
                AssetDatabase.CreateAsset(built, path);
                mesh = built;
            }
            else
            {
                EditorUtility.CopySerialized(built, mesh);
                Object.DestroyImmediate(built);
            }
            Undo.RecordObjects(new Object[] { filter, renderer }, "A: 벽 재질");
            filter.sharedMesh = mesh;
            renderer.sharedMaterial = material;
            Debug.Log($"[A] 벽 {wall.name} ({key} m) → {mesh.name}");
        }

        // 크기 1 상자(Cube 와 같은 꼭짓점 범위 ±0.5) — UV 는 각 면의 실제 길이(타일 단위)
        static Mesh BuildBox(Vector3 tiles)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var tri = new List<int>();
            // 면: 법선, 면 위 가로축, 세로축, 그 축들의 타일 수
            AddFace(v, n, uv, tri, Vector3.right, Vector3.forward, Vector3.up, tiles.z, tiles.y);
            AddFace(v, n, uv, tri, Vector3.left, Vector3.back, Vector3.up, tiles.z, tiles.y);
            AddFace(v, n, uv, tri, Vector3.forward, Vector3.left, Vector3.up, tiles.x, tiles.y);
            AddFace(v, n, uv, tri, Vector3.back, Vector3.right, Vector3.up, tiles.x, tiles.y);
            AddFace(v, n, uv, tri, Vector3.up, Vector3.right, Vector3.forward, tiles.x, tiles.z);
            AddFace(v, n, uv, tri, Vector3.down, Vector3.right, Vector3.back, tiles.x, tiles.z);
            var mesh = new Mesh();
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateTangents();   // 노멀 맵용
            mesh.RecalculateBounds();
            return mesh;
        }

        static void AddFace(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> tri, Vector3 normal, Vector3 right, Vector3 up, float uTiles, float vTiles)
        {
            int b = v.Count;
            Vector3 c = normal * 0.5f;
            v.Add(c - right * 0.5f - up * 0.5f); uv.Add(new Vector2(0f, 0f));
            v.Add(c + right * 0.5f - up * 0.5f); uv.Add(new Vector2(uTiles, 0f));
            v.Add(c + right * 0.5f + up * 0.5f); uv.Add(new Vector2(uTiles, vTiles));
            v.Add(c - right * 0.5f + up * 0.5f); uv.Add(new Vector2(0f, vTiles));
            for (int i = 0; i < 4; i++) n.Add(normal);
            // 삼각형 기하 법선 Cross(b-a, c-a) 가 바깥(normal)을 향하게 감는다 — Unity 앞면은 바깥에서 볼 때 시계 방향
            if (Vector3.Dot(Vector3.Cross(right, up), normal) > 0f)
                tri.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
            else
                tri.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
        }
    }
}
