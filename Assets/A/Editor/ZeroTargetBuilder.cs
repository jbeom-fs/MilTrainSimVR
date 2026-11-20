using System.IO;
using UnityEditor;
using UnityEngine;

namespace MilTrainSimVR.A.EditorTools
{
    // 25 m 영점 표적지 화이트박스 그림과 재질을 만든다 (사로 기획 4절) — 흰 바탕 + 1 cm 격자 + 가운데 조준점.
    // 실제 영점 표적 모양은 교안을 확인한 뒤 이 그림을 바꾼다. 크기를 바꾸면 씬의 표적지(ZeroSheet) 크기도 같이 바꾼다.
    static class ZeroTargetBuilder
    {
        const string k_Folder = "Assets/A/Materials/Range";
        const string k_TexturePath = k_Folder + "/ZeroTarget.png";
        const string k_MaterialPath = k_Folder + "/ZeroTarget.mat";

        const int k_SizeCm = 30;          // 표적지 한 변 (cm)
        const int k_PixelsPerCm = 20;
        const float k_AimDiameterCm = 3f; // 가운데 조준점 지름

        static readonly Color k_Paper = new Color(0.96f, 0.96f, 0.94f);
        static readonly Color k_Grid = new Color(0.75f, 0.78f, 0.80f);
        static readonly Color k_Grid5 = new Color(0.45f, 0.50f, 0.55f);
        static readonly Color k_Aim = new Color(0.05f, 0.05f, 0.05f);

        // K-2 25 m 영점표적지 그림 (11/03 사용자 Models/ShootingRange.png, 1088 × 1445) — 화이트박스 그림을 대신한다.
        // 표적지 폭 30 cm(세로는 그림 비율 ≈ 39.9 cm → 칸 하나 ≈ 1 cm). 조준점 = 흰 원 가운데 '×' (그림 (543.6, 807.2) px).
        // 씬의 표적지(ZeroSheet, 교육·보기용 모두) 크기, 교육 표적의 조준점(ZeroTarget.m_AimPoint → 자식 AimPoint), 결과 화면 그림(TargetMap)을 같이 맞춘다
        const string k_K2Texture = "Assets/A/Models/ShootingRange.png";
        const float k_K2WidthM = 0.30f;
        static readonly Vector2 k_K2AimUv = new Vector2(543.6f / 1088f, 1f - 807.2f / 1445f);

        [MenuItem("A/만들기/영점 표적지 (K-2 사격지 그림)")]
        static void ApplyK2()
        {
            if (AssetImporter.GetAtPath(k_K2Texture) is TextureImporter ti)
            {
                ti.npotScale = TextureImporterNPOTScale.None;   // 늘리지 않는다 — 가는 격자·숫자가 흐려지지 않게
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.anisoLevel = 8;                               // 멀리서 비스듬히 보므로
                ti.SaveAndReimport();
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(k_K2Texture);
            var material = AssetDatabase.LoadAssetAtPath<Material>(k_MaterialPath);
            if (texture == null || material == null)
            {
                Debug.LogError($"[A] {k_K2Texture} 또는 {k_MaterialPath} 이 없다");
                return;
            }
            material.SetTexture("_BaseMap", texture);
            material.SetTexture("_MainTex", texture);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();

            Undo.SetCurrentGroupName("A: K-2 영점표적지");
            int group = Undo.GetCurrentGroup();
            float heightM = k_K2WidthM * texture.height / texture.width;
            int sheets = 0;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (r.sharedMaterial != material)
                    continue;
                Undo.RecordObject(r.transform, "A: K-2 영점표적지");
                r.transform.localScale = new Vector3(k_K2WidthM, heightM, r.transform.localScale.z);
                sheets++;
            }
            foreach (var target in Object.FindObjectsByType<Range.ZeroTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Transform aim = target.transform.Find("AimPoint");
                if (aim == null)
                {
                    aim = new GameObject("AimPoint").transform;
                    Undo.RegisterCreatedObjectUndo(aim.gameObject, "A: K-2 영점표적지");
                    aim.SetParent(target.transform, false);
                }
                aim.localPosition = new Vector3(k_K2AimUv.x - 0.5f, k_K2AimUv.y - 0.5f, 0f);   // 쿼드 기준 (-0.5 ~ 0.5)
                aim.localRotation = Quaternion.identity;
                var so = new SerializedObject(target);
                so.FindProperty("m_AimPoint").objectReferenceValue = aim;
                so.ApplyModifiedProperties();
            }
            foreach (var map in Object.FindObjectsByType<Range.TargetMap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var so = new SerializedObject(map);
                so.FindProperty("m_Sheet").objectReferenceValue = texture;
                so.FindProperty("m_SheetSizeCm").vector2Value = new Vector2(k_K2WidthM, heightM) * 100f;
                so.FindProperty("m_AimUv").vector2Value = k_K2AimUv;
                so.ApplyModifiedProperties();
            }
            Undo.CollapseUndoOperations(group);
            Debug.Log($"[A] K-2 영점표적지: {k_K2WidthM * 100f:0} × {heightM * 100f:0.#} cm, 표적지 {sheets}장, 조준점 그림 ({k_K2AimUv.x:0.###}, {k_K2AimUv.y:0.###})");
        }

        [MenuItem("A/만들기/영점 표적지 (화이트박스)")]
        static void Build()
        {
            int size = k_SizeCm * k_PixelsPerCm;
            var pixels = new Color[size * size];
            float c = size / 2f - 0.5f;
            float aimR = k_AimDiameterCm * k_PixelsPerCm / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color p = k_Paper;
                    // 가운데에서 cm 단위 격자 — 1 cm 가는 선, 5 cm 굵은 선
                    int dx = Mathf.Abs(x - size / 2), dy = Mathf.Abs(y - size / 2);
                    bool line1 = dx % k_PixelsPerCm == 0 || dy % k_PixelsPerCm == 0;
                    bool line5 = dx % (5 * k_PixelsPerCm) <= 1 || dy % (5 * k_PixelsPerCm) <= 1;
                    if (line5) p = k_Grid5;
                    else if (line1) p = k_Grid;
                    if ((new Vector2(x - c, y - c)).sqrMagnitude <= aimR * aimR)
                        p = k_Aim;
                    pixels[y * size + x] = p;
                }
            }
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
            texture.SetPixels(pixels);
            texture.Apply();
            if (!AssetDatabase.IsValidFolder(k_Folder))
                AssetDatabase.CreateFolder("Assets/A/Materials", "Range");
            File.WriteAllBytes(k_TexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(k_TexturePath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(k_TexturePath);
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.anisoLevel = 8;   // 멀리서 비스듬히 보므로
            importer.SaveAndReimport();

            var material = AssetDatabase.LoadAssetAtPath<Material>(k_MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, k_MaterialPath);
            }
            var map = AssetDatabase.LoadAssetAtPath<Texture2D>(k_TexturePath);
            material.SetTexture("_BaseMap", map);
            material.SetTexture("_MainTex", map);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0f);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            Debug.Log($"[A] 영점 표적지 {k_SizeCm} cm, 격자 1 cm, 조준점 {k_AimDiameterCm} cm → {k_MaterialPath}");
        }
    }
}
