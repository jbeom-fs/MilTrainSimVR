using UnityEditor;
using UnityEngine;

namespace MilTrainSimVR.A.EditorTools
{
    // 사용자가 칠해서 내보낸 모델 텍스처 3장(<이름>_DefaultMaterial_AlbedoTransparency · MetallicSmoothness · Normal .png)으로
    // URP Lit 재질 <모델 폴더>/Materials/<이름>.mat 을 만든다(있으면 값만 다시 넣는다). 텍스처 가져오기 설정도 맞춘다.
    // 사격장 문(DoubleDoor)·받침대(Table_2)가 쓴다 (11/03).
    static class PaintedMaterial
    {
        // materialName — 재질 파일 이름을 텍스처 이름과 다르게 (텍스처가 Tile_* 처럼 일반 이름일 때, 11/09)
        internal static Material Ensure(string folder, string name, string materialName = null)
        {
            string prefix = $"{folder}/{name}_DefaultMaterial_";
            Texture2D albedo = Texture(prefix + "AlbedoTransparency.png", TextureImporterType.Default, true);
            Texture2D metallic = Texture(prefix + "MetallicSmoothness.png", TextureImporterType.Default, false);   // 값 텍스처 → sRGB 끔
            Texture2D normal = Texture(prefix + "Normal.png", TextureImporterType.NormalMap, false);

            string materialFolder = folder + "/Materials";
            string path = $"{materialFolder}/{materialName ?? name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                if (!AssetDatabase.IsValidFolder(materialFolder))
                    AssetDatabase.CreateFolder(folder, "Materials");
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = materialName ?? name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseMap", albedo);
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_MetallicGlossMap", metallic);
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", 1f);                 // 맵 사용 시 맵 알파 × 이 값
            material.SetFloat("_SmoothnessTextureChannel", 0f);   // 금속 맵 알파
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        // 모델 아래 모든 렌더러의 재질 칸을 한 재질로
        internal static void Assign(GameObject model, Material material)
        {
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = material;
                r.sharedMaterials = mats;
            }
        }

        static Texture2D Texture(string path, TextureImporterType type, bool sRGB)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter ti && (ti.textureType != type || ti.sRGBTexture != sRGB))
            {
                ti.textureType = type;
                ti.sRGBTexture = sRGB;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
