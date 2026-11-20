using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MilTrainSimVR.UrbanCombat.EditorTools
{
    // 차량 불 연출 프리팹 (사용자 11/17 — 서쪽 지하 입구 둘을 처박힌 차로 막고 '불이 나는 것처럼').
    // 메뉴 하나로 텍스처 · 재질 · 프리팹을 만든다:
    //   · Textures/FX/FX_FireBlob.png (부드러운 불덩이) · FX_SmokePuff.png (잡음 낀 연기 뭉치) — 코드로 그린다
    //   · Materials/FX/FX_Flame · FX_Ember (URP 파티클, 더하기) · FX_Smoke (알파 섞기)
    //   · Prefabs/FX/C_VehicleFire: Flames(불길) · Smoke(검은 연기) · Embers(불티) · FireLight(주황 점 조명 + FireFlicker, 그림자 없음 + 불 소리)
    // 프리팹 뿌리는 똑바로 세워 차 가운데에 둔다 — 파티클은 월드 기준으로 위로 오른다 (차가 기울어도 불은 위로)
    // 불 상자 크기(1.7 × 3.8 m, 높이 1.6 m)는 승용차 · 1톤 트럭에 맞춘 값 — 다른 크기면 인스턴스의 Flames 모양 크기만 고친다
    public static class FireFxBuilder
    {
        const string k_TexFolder = "Assets/C/Textures/FX";
        const string k_MatFolder = "Assets/C/Materials/FX";
        const string k_PrefabPath = "Assets/C/Prefabs/FX/C_VehicleFire.prefab";

        [MenuItem("MilTrainSimVR/C/차량 불 연출 프리팹 만들기", false, 60)]
        public static void Build()
        {
            EnsureFolder("Assets/C/Textures", "FX");
            EnsureFolder("Assets/C/Materials", "FX");
            EnsureFolder("Assets/C/Prefabs", "FX");
            Texture2D blob = WriteTexture("FX_FireBlob", FireBlob);
            Texture2D puff = WriteTexture("FX_SmokePuff", SmokePuff);
            Material flame = ParticleMat("FX_Flame", blob, true);
            Material ember = ParticleMat("FX_Ember", blob, true);
            Material smoke = ParticleMat("FX_Smoke", puff, false);

            var root = new GameObject("C_VehicleFire");
            Flames(root.transform, flame);
            Smoke(root.transform, smoke);
            Embers(root.transform, ember);
            var lightGo = new GameObject("FireLight");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.55f, 0.22f);
            light.range = 9f;
            light.intensity = 4f;
            light.shadows = LightShadows.None;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            AddFireSound(lightGo);   // 불 소리 — FireFlicker 가 시작 자리 · 크기 출렁임을 맡는다
            lightGo.AddComponent<FireFlicker>();
            PrefabUtility.SaveAsPrefabAsset(root, k_PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log($"[C] 차량 불 연출 프리팹: {k_PrefabPath}");
        }

        // 불 소리 (11/17) — Audio/Ambience/C_FireCrackle.wav (Audio/Source~/make_fire_crackle.py 로 합성한 10초 반복). 3D, 2.5 m 안은 그대로, 30 m 까지 줄어든다
        public static void AddFireSound(GameObject go)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/C/Audio/Ambience/C_FireCrackle.wav");
            if (clip == null) return;
            var src = go.GetComponent<AudioSource>();
            if (src == null) src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.playOnAwake = true;
            src.volume = 0.7f;
            src.spatialBlend = 1f;
            // 들리는 범위 (사용자 11/17 "멀리서도 조금씩 들려서 들리는 범위만 줄여 줘"): 로그 감쇠는 최대 거리(30 m)를 넘으면 더 줄지 않아
            // 맵 어디서든 약 6 % 크기로 들렸다 → 같은 로그 모양(2.5 m 안 그대로, 8 m 까지 같음)에 8 m 부터 줄여 15 m 에서 0 이 되는 곡선
            src.rolloffMode = AudioRolloffMode.Custom;
            src.minDistance = k_FireNear;
            src.maxDistance = k_FireSilent;
            src.SetCustomCurve(AudioSourceCurveType.CustomRolloff, FireRolloff());
            src.dopplerLevel = 0f;
            src.spread = 60f;
        }

        const float k_FireNear = 2.5f;     // 이 안은 최대 크기
        const float k_FireFadeFrom = 8f;   // 여기까지는 전과 같은 로그 감쇠 (2.5 / 거리)
        const float k_FireSilent = 15f;    // 여기서 0 — 더 멀면 안 들린다

        // 거리(최대 거리로 나눈 값) → 크기. 로그 감쇠 × 8 m 부터 코사인으로 0 까지
        static AnimationCurve FireRolloff()
        {
            var curve = new AnimationCurve();
            for (int i = 0; i <= 30; i++)
            {
                float d = k_FireSilent * i / 30f;
                float v = d <= k_FireNear ? 1f : k_FireNear / d;
                if (d > k_FireFadeFrom)
                    v *= 0.5f * (1f + Mathf.Cos(Mathf.PI * (d - k_FireFadeFrom) / (k_FireSilent - k_FireFadeFrom)));
                curve.AddKey(new Keyframe(d / k_FireSilent, v));
            }
            for (int i = 0; i < curve.length; i++)
                curve.SmoothTangents(i, 0f);
            return curve;
        }

        // ---------------------------------------------------------------- 파티클

        // 불 상자: 뿌리 기준 가로 1.7 · 앞뒤 3.8 · 높이 1.6 m. 모양 회전 -90° 로 상자의 +Z(뿜는 쪽)가 월드 위를 본다
        static void Shape(ParticleSystem ps, Vector3 sizeXZY, float y)
        {
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            shape.scale = new Vector3(sizeXZY.x, sizeXZY.y, sizeXZY.z);   // (가로, 앞뒤, 높이) — 회전 뒤 로컬 y 가 앞뒤, z 가 높이
            shape.position = new Vector3(0f, y, 0f);
        }

        static ParticleSystem NewSystem(string name, Transform parent, Material mat, int max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.maxParticleSize = 2f;
            return ps;
        }

        static void Flames(Transform parent, Material mat)
        {
            var ps = NewSystem("Flames", parent, mat, 160);
            var main = ps.main;
            main.duration = 4f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.5f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = -0.15f;
            var em = ps.emission; em.rateOverTime = 70f;
            Shape(ps, new Vector3(1.7f, 3.8f, 1.6f), 0.6f);
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = Grad(new[] { (0f, new Color(1f, 0.95f, 0.7f)), (0.15f, new Color(1f, 0.75f, 0.3f)), (0.55f, new Color(1f, 0.38f, 0.1f)), (1f, new Color(0.45f, 0.08f, 0.03f)) },
                new[] { (0f, 0f), (0.12f, 0.9f), (0.6f, 0.6f), (1f, 0f) });
            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.3f)));
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.6f; noise.frequency = 1.2f; noise.scrollSpeed = 1f;
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
        }

        static void Smoke(Transform parent, Material mat)
        {
            var ps = NewSystem("Smoke", parent, mat, 80);
            var main = ps.main;
            main.duration = 8f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = -0.04f;
            var em = ps.emission; em.rateOverTime = 8f;
            Shape(ps, new Vector3(1.2f, 2.8f, 0.4f), 1.6f);
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = Grad(new[] { (0f, new Color(0.16f, 0.14f, 0.12f)), (1f, new Color(0.3f, 0.29f, 0.28f)) }, new[] { (0f, 0f), (0.1f, 0.55f), (0.7f, 0.3f), (1f, 0f) });
            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 2.6f)));
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.2f, 0.45f); vel.y = new ParticleSystem.MinMaxCurve(0f, 0f); vel.z = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);   // 약한 바람
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.35f; noise.frequency = 0.4f;
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
        }

        static void Embers(Transform parent, Material mat)
        {
            var ps = NewSystem("Embers", parent, mat, 60);
            var main = ps.main;
            main.duration = 4f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            main.startColor = new Color(1f, 0.6f, 0.2f);
            main.gravityModifier = -0.2f;
            var em = ps.emission; em.rateOverTime = 18f;
            Shape(ps, new Vector3(1.5f, 3.4f, 1.0f), 0.9f);
            var col = ps.colorOverLifetime; col.enabled = true;
            col.color = Grad(new[] { (0f, new Color(1f, 0.85f, 0.4f)), (1f, new Color(1f, 0.3f, 0.05f)) }, new[] { (0f, 1f), (0.7f, 0.8f), (1f, 0f) });
            var noise = ps.noise; noise.enabled = true; noise.strength = 1.5f; noise.frequency = 2f;
        }

        static Gradient Grad((float t, Color c)[] colors, (float t, float a)[] alphas)
        {
            var g = new Gradient();
            var ck = new GradientColorKey[colors.Length];
            for (int i = 0; i < colors.Length; i++) ck[i] = new GradientColorKey(colors[i].c, colors[i].t);
            var ak = new GradientAlphaKey[alphas.Length];
            for (int i = 0; i < alphas.Length; i++) ak[i] = new GradientAlphaKey(alphas[i].a, alphas[i].t);
            g.SetKeys(ck, ak);
            return g;
        }

        // ---------------------------------------------------------------- 텍스처 · 재질

        // 부드러운 불덩이 — 가운데가 밝고 위로 조금 길쭉, 가장자리에 잔물결 (색은 파티클 색이 정한다)
        static Color FireBlob(float u, float v)
        {
            float x = u * 2f - 1f, y = (v * 2f - 1f) * 0.85f + 0.08f;
            float r = Mathf.Sqrt(x * x + y * y);
            float wobble = (Mathf.PerlinNoise(u * 6f, v * 6f) - 0.5f) * 0.25f;
            float a = Mathf.Clamp01(1f - (r + wobble));
            a = a * a * (3f - 2f * a);
            return new Color(1f, 1f, 1f, a);
        }

        // 연기 뭉치 — 둥근 테두리 안에 여러 겹 잡음
        static Color SmokePuff(float u, float v)
        {
            float x = u * 2f - 1f, y = v * 2f - 1f;
            float r = Mathf.Sqrt(x * x + y * y);
            float n = Mathf.PerlinNoise(u * 4f + 3.1f, v * 4f + 7.7f) * 0.6f + Mathf.PerlinNoise(u * 9f + 11f, v * 9f + 2f) * 0.4f;
            float edge = Mathf.Clamp01(1f - r);
            edge = edge * edge * (3f - 2f * edge);
            return new Color(1f, 1f, 1f, Mathf.Clamp01(edge * (0.45f + 0.75f * n)));
        }

        static Texture2D WriteTexture(string name, System.Func<float, float, Color> fill)
        {
            const int N = 128;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                    tex.SetPixel(x, y, fill((x + 0.5f) / N, (y + 0.5f) / N));
            tex.Apply();
            string path = $"{k_TexFolder}/{name}.png";
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // URP 파티클 Unlit — 투명. additive: 불 · 불티(더하기), 아니면 연기(알파 섞기)
        static Material ParticleMat(string name, Texture2D tex, bool additive)
        {
            string path = $"{k_MatFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", additive ? 2f : 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.DisableKeyword("_ALPHAMODULATE_ON");
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
