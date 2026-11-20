using UnityEditor;
using UnityEngine;

namespace MilTrainSimVR.A.EditorTools
{
    // 내무반 벽 포스터 (11/03 사용자 그림 Models/Poster_1.jpg 국방헬프콜 · Poster_2.png 청년미래적금).
    // 문 오른쪽 벽(Room_Assembly/Shell/Wall_North_R, 안쪽 면 z -0.1)에 나란히 — 스위치·소화기(x 0.75)와 기타(x 3.5 ~ 3.9) 사이.
    // 종이 한 장(쿼드)을 벽에서 2 mm 띄워 붙인다. 폭 0.6 m(세로는 그림 비율), 윗변 높이를 맞춘다. 다시 누르면 지우고 다시 붙인다.
    static class BarracksPosterBuilder
    {
        const string k_Parent = "Room_Assembly";
        const string k_Root = "Posters";
        const string k_MaterialFolder = "Assets/A/Materials/Common";
        const float k_WallFaceZ = -0.1f;
        const float k_Gap = 0.002f;
        const float k_Width = 0.6f;
        const float k_TopY = 1.95f;   // 윗변 — 서 있는 눈높이(약 1.6 m)가 포스터 위쪽 3분의 1쯤

        static readonly (string texture, float x)[] k_Posters =
        {
            ("Assets/A/Models/Poster_1.jpg", 2.05f),
            ("Assets/A/Models/Poster_2.png", 2.85f),
        };

        [MenuItem("A/만들기/내무반 포스터 (문 오른쪽 벽)")]
        static void Build()
        {
            var parent = GameObject.Find(k_Parent);
            if (parent == null)
            {
                Debug.LogError($"[A] {k_Parent} 이 없다");
                return;
            }
            Undo.SetCurrentGroupName("A: 내무반 포스터");
            int group = Undo.GetCurrentGroup();
            var old = parent.transform.Find(k_Root);
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);
            var root = new GameObject(k_Root).transform;
            Undo.RegisterCreatedObjectUndo(root.gameObject, "A: 내무반 포스터");
            root.SetParent(parent.transform, false);

            foreach (var (path, x) in k_Posters)
            {
                var texture = PosterTexture(path);
                if (texture == null)
                {
                    Debug.LogError($"[A] {path} 이 없다");
                    continue;
                }
                float height = k_Width * texture.height / texture.width;
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);   // 앞면 -Z = 방 안쪽
                quad.name = System.IO.Path.GetFileNameWithoutExtension(path);
                Object.DestroyImmediate(quad.GetComponent<Collider>());
                quad.transform.SetParent(root, false);
                quad.transform.localPosition = new Vector3(x, k_TopY - height * 0.5f, k_WallFaceZ - k_Gap);
                quad.transform.localScale = new Vector3(k_Width, height, 1f);
                quad.GetComponent<MeshRenderer>().sharedMaterial = PosterMaterial(quad.name, texture);
            }
            Undo.CollapseUndoOperations(group);
            Debug.Log($"[A] 내무반 포스터 {k_Posters.Length}장: 문 오른쪽 벽, 폭 {k_Width} m, 윗변 {k_TopY} m");
        }

        // 2의 거듭제곱으로 늘리지 않게 (글자 흐림 방지)
        static Texture2D PosterTexture(string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter ti && ti.npotScale != TextureImporterNPOTScale.None)
            {
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.anisoLevel = 4;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material PosterMaterial(string name, Texture2D texture)
        {
            string path = $"{k_MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                material.SetFloat("_Smoothness", 0.15f);   // 코팅 종이
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseMap", texture);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }
    }
}
