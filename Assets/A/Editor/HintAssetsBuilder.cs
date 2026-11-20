using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MilTrainSimVR.A.EditorTools
{
    // 학습 모드 힌트 재질 3개를 만든다 (Assets/A/Materials/Hints/). 색을 바꾸려면 여기 값을 고치고 다시 만들거나 재질을 직접 고친다.
    // URP Unlit — 조명과 상관없이 같은 색으로 보인다. 강조·끼울 자리는 반투명, 화살표는 불투명.
    static class HintAssetsBuilder
    {
        const string k_Folder = "Assets/A/Materials/Hints";

        [MenuItem("A/만들기/힌트 재질")]
        static void Build()
        {
            if (!AssetDatabase.IsValidFolder(k_Folder))
                AssetDatabase.CreateFolder("Assets/A/Materials", "Hints");
            Save(Make(new Color(1f, 0.75f, 0.1f, 0.35f), true), "Hint_Highlight");   // 지금 할 부품 — 주황
            Save(Make(new Color(0.3f, 0.8f, 1f, 0.3f), true), "Hint_Ghost");         // 끼울 자리 — 하늘색
            Save(Make(new Color(1f, 0.85f, 0.1f, 1f), false), "Hint_Arrow");          // 화살표 — 노랑
            AssetDatabase.SaveAssets();
            Debug.Log($"[A] 힌트 재질 저장: {k_Folder}");
        }

        static Material Make(Color color, bool transparent)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Cull", (float)CullMode.Off);   // 화살표 면 방향을 따지지 않게, 반투명도 안쪽까지 보이게
            if (transparent)
            {
                material.SetFloat("_Surface", 1f);   // Transparent
                material.SetFloat("_Blend", 0f);     // Alpha
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            return material;
        }

        static void Save(Material material, string name)
        {
            string path = $"{k_Folder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                // 이미 있으면 내용만 바꾼다 — 씬의 참조(GUID)가 유지된다
                existing.shader = material.shader;
                existing.CopyPropertiesFromMaterial(material);
                existing.shaderKeywords = material.shaderKeywords;
                existing.renderQueue = material.renderQueue;
                existing.SetOverrideTag("RenderType", material.GetTag("RenderType", false));
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(material);
                return;
            }
            AssetDatabase.CreateAsset(material, path);
        }
    }
}
