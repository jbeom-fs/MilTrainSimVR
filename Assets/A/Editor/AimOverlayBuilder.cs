using System.IO;
using UnityEditor;
using UnityEngine;

namespace MilTrainSimVR.A.EditorTools
{
    // 조준 확대 모드의 가늠자·가늠쇠 그림과 재질을 만든다 (Docs/A/1028_조준확대모드_설계.md 3-2절).
    // 그림 한 장 = 확대 창 × k_OverlayScale. 가운데 원(창)은 투명하고 가늠쇠(보호 고리 + 기둥)만 검게, 바깥은 가늠자 테처럼 검다가 가장자리로 갈수록 흐려진다.
    // 가늠쇠 크기는 모델에서 잰 실제 크기 × 배율 — SightAim 의 창 크기·배율·그림 배율과 같은 값이어야 한다.
    static class AimOverlayBuilder
    {
        const string k_Folder = "Assets/A/Materials/Aim";
        const string k_TexturePath = k_Folder + "/AimSightPicture.png";
        const string k_MaterialPath = k_Folder + "/AimSightPicture.mat";

        const int k_Size = 1024;
        const float k_WindowDegrees = 30f;      // SightAim.m_WindowDegrees
        const float k_Magnification = 4f;       // SightAim.m_Magnification
        const float k_OverlayScale = 2.4f;      // SightAim.m_OverlayScale

        // 가늠쇠 (모델 측정, mm) — 가늠자 구멍에서 0.513 m 앞
        const float k_SightRadiusMm = 513f;
        const float k_PostHalfWidthMm = 0.85f;
        const float k_RingInnerMm = 7.7f;
        const float k_RingOuterMm = 10.1f;
        const float k_RingAboveTipMm = 2.2f;    // 보호 고리 가운데가 기둥 끝보다 위
        const float k_StemHalfWidthMm = 6f;     // 고리 아래 받침

        [MenuItem("A/만들기/조준 확대 그림 (가늠자·가늠쇠)")]
        static void Build()
        {
            // 창 반지름을 1 로 하는 단위. 기둥 끝 = 창 가운데 = (0, 0)
            float windowRadiusMrad = Mathf.Tan(k_WindowDegrees * 0.5f * Mathf.Deg2Rad) * 1000f;
            float unit = 1f / k_SightRadiusMm * 1000f * k_Magnification / windowRadiusMrad;   // mm → 창 단위
            float post = k_PostHalfWidthMm * unit, ringIn = k_RingInnerMm * unit, ringOut = k_RingOuterMm * unit;
            float ringY = k_RingAboveTipMm * unit, stem = k_StemHalfWidthMm * unit;
            float half = k_OverlayScale;   // 그림 가장자리 = 창 반지름의 k_OverlayScale 배

            var pixels = new Color[k_Size * k_Size];
            const int samples = 3;   // 가장자리 매끄럽게
            for (int py = 0; py < k_Size; py++)
            {
                for (int px = 0; px < k_Size; px++)
                {
                    float alpha = 0f;
                    for (int sy = 0; sy < samples; sy++)
                        for (int sx = 0; sx < samples; sx++)
                        {
                            float x = ((px + (sx + 0.5f) / samples) / k_Size * 2f - 1f) * half;
                            float y = ((py + (sy + 0.5f) / samples) / k_Size * 2f - 1f) * half;
                            alpha += Coverage(x, y, post, ringIn, ringOut, ringY, stem);
                        }
                    pixels[py * k_Size + px] = new Color(0f, 0f, 0f, alpha / (samples * samples));
                }
            }

            var texture = new Texture2D(k_Size, k_Size, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            if (!AssetDatabase.IsValidFolder(k_Folder))
                AssetDatabase.CreateFolder("Assets/A/Materials", "Aim");
            File.WriteAllBytes(k_TexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(k_TexturePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(k_TexturePath);
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();

            var material = AssetDatabase.LoadAssetAtPath<Material>(k_MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("MilTrainSimVR/A/AimOverlay"));
                AssetDatabase.CreateAsset(material, k_MaterialPath);
            }
            material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(k_TexturePath));
            material.SetColor("_Color", Color.white);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            Debug.Log($"[A] 조준 확대 그림: 창 {k_WindowDegrees}° × {k_Magnification}배, 기둥 반폭 {post:0.000} · 고리 {ringIn:0.000}~{ringOut:0.000} (창 반지름 단위) → {k_MaterialPath}");
        }

        // 그 점이 검은지 (0 ~ 1)
        static float Coverage(float x, float y, float post, float ringIn, float ringOut, float ringY, float stem)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            if (r >= 1f)
            {
                // 가늠자 테 — 창 밖은 검고, 멀어지면 흐려진다 (주변 어둡게)
                return Mathf.Clamp01(1f - (r - 1.5f) / 0.8f);
            }
            // 가늠쇠 기둥 — 끝(0, 0)에서 아래로
            if (y <= 0f && Mathf.Abs(x) <= post)
                return 1f;
            // 보호 고리
            float rr = Mathf.Sqrt(x * x + (y - ringY) * (y - ringY));
            if (rr >= ringIn && rr <= ringOut)
                return 1f;
            // 고리 아래 받침 — 고리 가운데 높이보다 아래, 고리 바깥에서
            if (y < ringY - ringIn * 0.6f && Mathf.Abs(x) <= stem && rr > ringIn)
                return 1f;
            return 0f;
        }
    }
}
