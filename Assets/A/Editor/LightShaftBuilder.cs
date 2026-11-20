using UnityEditor;
using UnityEngine;

namespace MilTrainSimVR.A.EditorTools
{
    // 가짜 빛 기둥 (11/02, Docs/A/1102_빛줄기_표현방법.md 1번) — 스포트 조명에 같은 방향·각도의 원뿔 메시(자식 Shaft)를 붙인다.
    // 원뿔은 조명 자리(꼭짓점, 기구 크기만큼 조금 열림)에서 조명 앞(+Z)으로 바닥(y 0)까지. 바깥 각도 = 스포트 바깥 각도.
    // 셰이더 Shaders/LightShaft.shader, 재질 Materials/Range/Range_LightShaft.mat (진하기·색은 재질에서 고친다 — 다시 지어도 덮어쓰지 않음).
    // 메시는 각도·길이마다 Models/LightShaft 에 저장한다.
    // 먼지 (11/02, 같은 문서 3번) — 빛 기둥 안에서만 생겨 천천히 떠다니는 작은 점 (자식 Dust, Particle System). 재질 Range_LightDust (더하기)
    static class LightShaftBuilder
    {
        const string k_ShaderName = "MilTrainSimVR/A/LightShaft";
        const string k_MaterialPath = "Assets/A/Materials/Range/Range_LightShaft.mat";
        const string k_MeshFolder = "Assets/A/Models/LightShaft";
        const string k_Child = "Shaft";
        const int k_Segments = 96;            // 11/02 32 → 96 (옆면 줄)
        const float k_ApexRadius = 0.05f;   // 조명 기구 크기 — 꼭짓점을 한 점으로 모으지 않는다

        // 조명에 빛 기둥을 붙인다 (있으면 다시). 바닥까지 닿지 않으면 maxLength 까지.
        // shaftAngle = 빛 기둥(과 먼지) 원뿔 바깥 각도 — 0 이면 조명 바깥 각도와 같게. 조명보다 좁히면 덜 퍼져 보인다 (조명이 밝히는 넓이는 그대로)
        internal static void Attach(Light light, float maxLength, float shaftAngle = 0f)
        {
            var material = Material();
            if (material == null)
                return;
            var old = light.transform.Find(k_Child);
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);

            Vector3 forward = light.transform.forward;
            float length = forward.y < -0.01f ? Mathf.Min(maxLength, light.transform.position.y / -forward.y) : maxLength;
            float angle = shaftAngle > 0f ? shaftAngle : light.spotAngle;
            float radius = k_ApexRadius + length * Mathf.Tan(angle * 0.5f * Mathf.Deg2Rad);

            var go = new GameObject(k_Child);
            Undo.RegisterCreatedObjectUndo(go, "A: 빛 기둥");
            go.transform.SetParent(light.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = Mesh(length, radius);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            AttachDust(light, length, angle);
        }

        // --- 먼지 ---
        const string k_DustChild = "Dust";
        const string k_DustMaterialPath = "Assets/A/Materials/Range/Range_LightDust.mat";
        const string k_DustTexturePath = "Assets/A/Materials/Range/Range_LightDust.png";
        const int k_DustMax = 40;               // 빛 기둥 하나에 동시에
        const float k_DustLifetime = 8f;
        static readonly Color k_DustColor = new Color(1f, 0.9f, 0.7f, 0.5f);

        static void AttachDust(Light light, float length, float angle)
        {
            var old = light.transform.Find(k_DustChild);
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);
            var material = DustMaterial();
            if (material == null)
                return;

            var go = new GameObject(k_DustChild);
            Undo.RegisterCreatedObjectUndo(go, "A: 빛 기둥 먼지");
            go.transform.SetParent(light.transform, false);   // 조명 앞(+Z) = 원뿔 방향
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.loop = true;
            main.prewarm = true;                     // 시작할 때부터 차 있게
            main.startLifetime = k_DustLifetime;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.014f);
            main.startColor = k_DustColor;
            main.maxParticles = k_DustMax;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;

            var emission = ps.emission;
            emission.rateOverTime = k_DustMax / k_DustLifetime;

            // 빛 기둥과 같은 원뿔 부피 안에서 생긴다 (각도 = 반각, 길이 = 빛 기둥 길이)
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.ConeVolume;
            shape.angle = angle * 0.5f;
            shape.radius = k_ApexRadius;
            shape.length = length * 0.9f;
            shape.radiusThickness = 1f;
            shape.arc = 360f;

            // 천천히 떠다니기
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.03f;
            noise.frequency = 0.3f;
            noise.scrollSpeed = 0.05f;

            // 나타났다 사라지기
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                             new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static Material DustMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(k_DustMaterialPath);
            if (material != null)
                return material;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                Debug.LogError("[A] URP Particles/Unlit 셰이더가 없다");
                return null;
            }
            material = new Material(shader) { name = "Range_LightDust" };
            material.SetTexture("_BaseMap", DustTexture());
            material.SetColor("_BaseColor", Color.white);
            // 투명 + 더하기
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            AssetDatabase.CreateAsset(material, k_DustMaterialPath);
            return material;
        }

        // 가운데가 밝고 가장자리로 옅어지는 동그라미 (흰색, 알파)
        static Texture2D DustTexture()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(k_DustTexturePath);
            if (texture != null)
                return texture;
            const int size = 32;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = new Vector2(x - size / 2f + 0.5f, y - size / 2f + 0.5f).magnitude / (size / 2f);
                    float a = Mathf.Clamp01(1f - d);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            t.Apply();
            System.IO.File.WriteAllBytes(k_DustTexturePath, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(k_DustTexturePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(k_DustTexturePath);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(k_DustTexturePath);
        }

        static Material Material()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(k_MaterialPath);
            if (material != null)
                return material;
            var shader = Shader.Find(k_ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[A] 셰이더 {k_ShaderName} 이 없다");
                return null;
            }
            material = new Material(shader) { name = "Range_LightShaft" };
            AssetDatabase.CreateAsset(material, k_MaterialPath);
            return material;
        }

        // +Z 로 뻗는 열린 원뿔대 옆면. UV v = 0(조명) → 1(끝)
        static Mesh Mesh(float length, float radius)
        {
            if (!AssetDatabase.IsValidFolder(k_MeshFolder))
                AssetDatabase.CreateFolder("Assets/A/Models", "LightShaft");
            string path = $"{k_MeshFolder}/LightShaft_{length:0.##}x{radius:0.###}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null)
                return mesh;

            var vertices = new Vector3[(k_Segments + 1) * 2];
            var normals = new Vector3[vertices.Length];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[k_Segments * 6];
            float slope = (radius - k_ApexRadius) / length;   // 옆면 법선의 뒤로 기운 정도
            for (int i = 0; i <= k_Segments; i++)
            {
                float a = i / (float)k_Segments * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                vertices[i * 2] = dir * k_ApexRadius;
                vertices[i * 2 + 1] = dir * radius + Vector3.forward * length;
                Vector3 n = (dir - Vector3.forward * slope).normalized;
                normals[i * 2] = normals[i * 2 + 1] = n;
                uvs[i * 2] = new Vector2(i / (float)k_Segments, 0f);
                uvs[i * 2 + 1] = new Vector2(i / (float)k_Segments, 1f);
            }
            for (int i = 0; i < k_Segments; i++)
            {
                int b = i * 2, t = i * 6;
                triangles[t] = b; triangles[t + 1] = b + 1; triangles[t + 2] = b + 3;
                triangles[t + 3] = b; triangles[t + 4] = b + 3; triangles[t + 5] = b + 2;
            }
            mesh = new Mesh { name = System.IO.Path.GetFileNameWithoutExtension(path), vertices = vertices, normals = normals, uv = uvs, triangles = triangles };
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
    }
}
