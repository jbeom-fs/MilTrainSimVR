using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MilTrainSimVR.A.EditorTools
{
    // 가로등 (11/10 사용자 Models/Korean_Streetlight_v01 — 높이 8.45 m, 원점 = 기둥 아래 가운데, 팔이 -X 로 2.9 m, 등(SL_Luminaire) 아래면 약 8.2 m).
    // 허브 정문 밖 길(HubBuilder.Avenue 의 LightSpots)과 A_Sandbox 창밖 길(OutdoorBuilder)이 같이 쓴다.
    // 재질: BaseColor + UnityMetallicSmoothness(sRGB 끔) + Emission(등 면만 빛남). 빛: 등 아래 스포트 하나 — 등 색(Emission 텍스처 평균, 따뜻한 흰색)으로
    // 은은하게(사용자: 살짝만), 그림자 없음, 실시간 (PC 렌더러가 Forward+ 라 여러 개여도 물체마다 개수 제한 없음).
    static class StreetLightBuilder
    {
        const string k_Folder = "Assets/A/Models/Korean_Streetlight_v01/Korean_Streetlight_v01";
        const string k_ModelPath = k_Folder + "/Korean_Streetlight_v01.fbx";
        const string k_MaterialPath = "Assets/A/Models/Korean_Streetlight_v01/Materials/Korean_Streetlight.mat";

        static readonly Vector3 k_LampLocal = new Vector3(-2.88f, 8.15f, 0f);   // 등 아래면 가운데 (모델 기준)
        static readonly Color k_LampColor = new Color(0.81f, 0.67f, 0.42f);      // T_Streetlight_Emission 밝은 곳 평균
        const float k_LightIntensity = 1.5f, k_LightRange = 12f, k_SpotAngle = 120f, k_InnerSpotAngle = 60f;
        const float k_EmissionIntensity = 1.2f;

        internal static bool Available => AssetDatabase.LoadAssetAtPath<GameObject>(k_ModelPath) != null;

        // 가로등 하나. headYaw = 등(팔 끝)이 향할 방향 (parent 기준, 0 = +Z). 모델이 없으면 null
        internal static GameObject Place(Transform parent, Vector3 localPosition, float headYaw)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(k_ModelPath);
            if (model == null)
                return null;
            var lamp = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            lamp.name = "StreetLight";
            lamp.transform.localPosition = localPosition;
            lamp.transform.localRotation = Quaternion.Euler(0f, headYaw + 90f, 0f);   // 모델 -X(팔) → parent 의 headYaw 방향
            PaintedMaterial.Assign(lamp, Material());

            var glow = new GameObject("Glow").transform;
            glow.SetParent(lamp.transform, false);
            glow.localPosition = k_LampLocal;
            glow.localRotation = Quaternion.Euler(90f, 0f, 0f);   // 아래를 비춘다
            var light = glow.gameObject.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = k_LampColor;
            light.intensity = k_LightIntensity;
            light.range = k_LightRange;
            light.spotAngle = k_SpotAngle;
            light.innerSpotAngle = k_InnerSpotAngle;
            light.shadows = LightShadows.None;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            return lamp;
        }

        static Material Material()
        {
            string baseColor = k_Folder + "/textures/T_Streetlight_BaseColor.png";
            string metallic = k_Folder + "/textures/T_Streetlight_UnityMetallicSmoothness.png";
            string emission = k_Folder + "/textures/T_Streetlight_Emission.png";
            if (AssetImporter.GetAtPath(metallic) is TextureImporter ti && ti.sRGBTexture)
            {
                ti.sRGBTexture = false;
                ti.SaveAndReimport();
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(k_MaterialPath);
            if (material == null)
            {
                string folder = System.IO.Path.GetDirectoryName(k_MaterialPath).Replace('\\', '/');
                if (!AssetDatabase.IsValidFolder(folder))
                    AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(folder).Replace('\\', '/'), System.IO.Path.GetFileName(folder));
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Korean_Streetlight" };
                AssetDatabase.CreateAsset(material, k_MaterialPath);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(baseColor));
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(metallic));
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", 1f);
            material.SetFloat("_SmoothnessTextureChannel", 0f);   // 금속 맵 알파
            material.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(emission));
            material.SetColor("_EmissionColor", Color.white * k_EmissionIntensity);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(material);
            return material;
        }

        // A_Sandbox 창밖 길의 임시 가로등(Outdoor/Props/Road/Lamp — 기둥 · 팔 · 머리 도형)을 모델로 바꾼다. 자리 · 방향(등이 길 쪽 -Z)은 그대로.
        // 바깥 소품 전체를 다시 짓지 않는다 (OutdoorBuilder 도 다시 지을 때 모델을 쓴다)
        [MenuItem("A/만들기/바깥 가로등 (Korean_Streetlight 모델)")]
        static void ReplaceSandboxLamps()
        {
            var road = GameObject.Find("Outdoor/Props/Road");
            if (road == null || !Available)
            {
                Debug.LogError($"[A] Outdoor/Props/Road · {k_ModelPath} 중 없는 것이 있다 — A_Sandbox 를 열고 누른다");
                return;
            }
            Undo.SetCurrentGroupName("A: 바깥 가로등");
            int group = Undo.GetCurrentGroup();
            var old = new System.Collections.Generic.List<Transform>();
            foreach (Transform t in road.transform)
                if (t.name == "Lamp" || t.name == "StreetLight")
                    old.Add(t);
            foreach (var t in old)
            {
                Vector3 position = t.localPosition;
                Undo.DestroyObjectImmediate(t.gameObject);
                var lamp = Place(road.transform, position, OutdoorBuilder.LampHeadYaw);
                Undo.RegisterCreatedObjectUndo(lamp, "A: 바깥 가로등");
                foreach (var c in lamp.GetComponentsInChildren<Transform>())
                    GameObjectUtility.SetStaticEditorFlags(c.gameObject, StaticEditorFlags.BatchingStatic);
            }
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(road.scene);
            Debug.Log($"[A] 바깥 가로등 {old.Count}개 → Korean_Streetlight");
        }
    }
}
