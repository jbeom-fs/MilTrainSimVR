using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MilTrainSimVR.UrbanCombat.EditorTools
{
    // 한글 네온 간판 '친구 / 맥주'(C_NeonSign_ChinguBeer.prefab)를 만든다 (11/03) — 메뉴 MilTrainSimVR → C → 네온 간판 만들기.
    // 모델은 Blender 스크립트 Models/NeonSign/Source~/make_neon_sign.py 가 만든다: 관 · 전극 덮개 · 받침 · 뒤판 · 빛 판.
    // 여기서는 FBX 재질 이름대로 Unity 재질을 붙이고 빨간 점 조명을 더해 프리팹으로 저장한다.
    //   · 관(C_NeonTube): Lit + 빨간 방출 (HDR × k_TubeGlow). 씬에 Bloom 이 없어 화면에서 밝기가 잘려 분홍빛 도는 심지처럼 보인다
    //   · 뒤판(C_NeonBoard): 검은 판 — 밝은 회색 벽에서도 사진처럼 어두운 바탕. 어두운 벽에 붙일 땐 Model/NeonBoard 를 끈다
    //   · 빛 판(C_NeonGlow): Unlit 반투명 '더하기' — 흑백 번짐 텍스처 × 빨강을 판 · 벽 위에 더한다. 그림자를 드리우지도 받지도 않는다
    //   · 점 조명(NeonLight): 간판 1.2 m 앞에서 바닥 · 맞은편 벽까지 옅게 붉힌다 (그림자 없음). 낮에는 거의 안 보인다.
    //     0.5 m 앞에 두면 밤에 간판 벽에 붉은 원이 진하게 맺혔다 (11/03 밤 화면)
    // 재질은 없을 때만 만든다 — 손으로 바꾼 값은 다시 만들어도 남는다 (처음 값으로 돌리려면 재질을 지우고 다시 만들기).
    // 벽에 붙일 때: 프리팹 원점(글자 아래 가운데)을 벽 면에 두고 파란 축(+Z)이 벽 바깥을 보게 돌린다
    public static class NeonSignBuilder
    {
        const string k_Folder = "Assets/C/Models/NeonSign";
        const string k_ModelPath = k_Folder + "/C_NeonSign.fbx";
        const string k_GlowTexturePath = k_Folder + "/Textures/C_NeonGlow.png";
        const string k_MaterialFolder = "Assets/C/Materials/Neon";
        public const string PrefabPath = "Assets/C/Prefabs/C_NeonSign_ChinguBeer.prefab";

        static readonly Color k_Neon = new Color(1f, 0.06f, 0.04f);   // Blender 스크립트의 NEON 과 같은 빨강
        const float k_TubeGlow = 4f;          // 관 방출 배율
        const float k_SignCenterY = 0.58f;    // 원점에서 글자 가운데까지 (간판 높이 1.15 m)
        const float k_LightOut = 1.2f;        // 점 조명 — 벽에서 앞으로
        const float k_LightRange = 5f;
        const float k_LightIntensity = 0.8f;

        [MenuItem("MilTrainSimVR/C/네온 간판 만들기 (친구맥주)", false, 53)]
        static void BuildFromMenu()
        {
            Build();
            AssetDatabase.SaveAssets();
        }

        public static GameObject Build()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(k_ModelPath) == null)
            {
                Debug.LogWarning($"[C] {k_ModelPath} 가 없다 — Source~/make_neon_sign.py 를 먼저 Blender 로 돌린다");
                return null;
            }
            if (!AssetDatabase.IsValidFolder(k_MaterialFolder))
                AssetDatabase.CreateFolder("Assets/C/Materials", "Neon");

            var tubeGlow = new Color(k_Neon.r * k_TubeGlow, k_Neon.g * k_TubeGlow, k_Neon.b * k_TubeGlow);
            Material tube = LitMaterial("C_NeonTube", new Color(0.9f, 0.3f, 0.25f), 0f, 0.85f, tubeGlow);
            Material black = LitMaterial("C_NeonBlack", new Color(0.03f, 0.03f, 0.03f), 0f, 0.3f, Color.black);
            Material support = LitMaterial("C_NeonSupport", new Color(0.75f, 0.77f, 0.8f), 0.6f, 0.55f, Color.black);
            Material board = LitMaterial("C_NeonBoard", new Color(0.035f, 0.035f, 0.04f), 0f, 0.45f, Color.black);
            Material glow = GlowMaterial(ImportGlowTexture());
            ImportModel(tube, black, support, board, glow);
            GameObject prefab = BuildPrefab();
            Debug.Log($"[C] 네온 간판을 만들었다 — {PrefabPath}");
            return prefab;
        }

        // ---------------------------------------------------------------- 가져오기 설정

        static Texture2D ImportGlowTexture()
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(k_GlowTexturePath);
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;    // 번짐 모양은 sRGB 로 읽는 것으로 맞췄다 — 선형으로 읽으면 너무 넓고 어두운 곳에 계단이 진다
            ti.alphaSource = TextureImporterAlphaSource.None;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.mipmapEnabled = true;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;   // 보통 압축은 부드러운 번짐에 띠가 진다
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(k_GlowTexturePath);
        }

        static void ImportModel(Material tube, Material black, Material support, Material board, Material glow)
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(k_ModelPath);
            mi.animationType = ModelImporterAnimationType.None;
            mi.importAnimation = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importBlendShapes = false;
            mi.addCollider = false;
            // 축 바꾸기를 메시에 구워 넣으면 앞이 -Z 로 뒤집힌다 (11/03 확인) — 물체가 X 270° 로 들어오는 기본 그대로
            mi.bakeAxisConversion = false;
            // FBX 안 재질 이름(Blender 스크립트) → Unity 재질
            Remap(mi, "NeonTube", tube);
            Remap(mi, "NeonBlack", black);
            Remap(mi, "NeonSupport", support);
            Remap(mi, "NeonBoard", board);
            Remap(mi, "NeonGlow", glow);
            mi.SaveAndReimport();
        }

        static void Remap(ModelImporter mi, string sourceName, Material material)
        {
            mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), sourceName), material);
        }

        // ---------------------------------------------------------------- 재질

        static Material LitMaterial(string name, Color baseColor, float metallic, float smoothness, Color emission)
        {
            string path = $"{k_MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
                return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", baseColor);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetColor("_EmissionColor", emission);
            // 이 URP 는 GI 표시가 Realtime/Baked 일 때만 방출(_EMISSION)을 켠다 — None 이면 SetMaterialKeywords 가 끈다 (11/03 확인).
            // 씬에 실시간 GI 가 없어 Realtime 이라도 비용은 없다 (A 의 천장 조명 재질도 같다)
            mat.globalIlluminationFlags = emission.maxColorComponent > 0f
                ? MaterialGlobalIlluminationFlags.RealtimeEmissive
                : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            BaseShaderGUI.SetMaterialKeywords(mat);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static Material GlowMaterial(Texture2D texture)
        {
            string path = $"{k_MaterialFolder}/C_NeonGlow.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
                return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetFloat("_Surface", 1f);   // 반투명
            mat.SetFloat("_Blend", 2f);     // 더하기 — 텍스처에 알파가 없어 (알파 1) 색 그대로 더해진다
            mat.SetTexture("_BaseMap", texture);
            mat.SetColor("_BaseColor", k_Neon);
            BaseShaderGUI.SetMaterialKeywords(mat);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // ---------------------------------------------------------------- 프리팹

        static GameObject BuildPrefab()
        {
            var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(PrefabPath));
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(k_ModelPath), root.transform);
                model.name = "Model";
                foreach (MeshRenderer r in model.GetComponentsInChildren<MeshRenderer>())
                {
                    bool glowSheet = r.name == "NeonGlow";
                    r.shadowCastingMode = glowSheet ? ShadowCastingMode.Off : ShadowCastingMode.On;
                    r.receiveShadows = !glowSheet;
                }

                var light = new GameObject("NeonLight", typeof(Light)).GetComponent<Light>();
                light.transform.SetParent(root.transform, false);
                light.transform.localPosition = new Vector3(0f, k_SignCenterY, k_LightOut);
                light.type = LightType.Point;
                light.color = k_Neon;
                light.range = k_LightRange;
                light.intensity = k_LightIntensity;
                light.shadows = LightShadows.None;
                light.lightmapBakeType = LightmapBakeType.Realtime;

                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
