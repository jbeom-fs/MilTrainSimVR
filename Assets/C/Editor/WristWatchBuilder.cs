using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MilTrainSimVR.UrbanCombat.EditorTools
{
    // 손목시계 프리팹 (11/18 사용자 — 왼 손목 안쪽 둥근 시계).
    //   화면: 위 = 적 아이콘 + 남은/전체, 구분선, 아래 = 타이머 아이콘 + 분:초 (오버타임엔 깃발 + '나 12초 / 적 0초', 같이 있으면 '교착').
    //   오버타임이 열리면 화면 전체 알림('오버타임' + 점령지) + 삐비비빅 × 4 — 앞판 알림 대신 (11/18 사용자: 상시 손목 표시는 지우고 알림은 시계로)
    // 모델: Tripo 러기드 스마트워치 (그림 5 + H3.1 Ultra 60 + FBX 5 크레딧). 190만 → 12,000 삼각형 (make_prop_lowpoly.py, Source~/watch/spec_watch.json)
    //   · 화면 · 고리 치수는 Unity 에서 Watch.fbx 에 광선을 쏴 쟀다 (모델 단위, 아래 상수). 케이스가 고리 위에서 15.8° 돌아가 있다 (비스듬한 그림에서 만들어서)
    //   · 손목(XR Hands 샘플 손, L_Wrist 끝 단면 5.2 × 3.7 cm)은 납작한데 고리는 거의 둥글다 → 손등 쪽 줄만 부드럽게 눌러
    //     안쪽 두께를 4.0 cm 로 맞춘 메시를 따로 저장한다 (Watch_Fitted.asset — 위치만 옮기고 UV 는 그대로라 텍스처가 안 깨진다)
    //   · 화면은 어두운 원판으로 덮고(켜진 화면처럼) 그 위에 아이콘(SpriteRenderer) · 글자(TMP) — 아이콘은 여기서 거리 함수로 그린다
    //   · 삐비비빅 소리도 여기서 만든다 (3.2 kHz 피에조 소리, 짧은 삑 4번 한 묶음 × 4)
    // 실제 크기 배율 0.066: 케이스 4.3 cm, 화면 지름 2.7 cm, 고리 안쪽 5.4 × 4.0 cm.
    // 다시 누르면 아이콘 · 소리 · 메시 · 프리팹을 다시 만들고, 열린 씬의 MatchDisplay 에 프리팹을 넣는다 (씬은 저장하지 않는다)
    static class WristWatchBuilder
    {
        public const string k_Prefab = "Assets/C/Prefabs/C_WristWatch.prefab";
        const string k_Folder = "Assets/C/Models/Tripo/Watch";
        const string k_Fbx = k_Folder + "/Watch.fbx";
        const string k_Material = k_Folder + "/Watch.mat";
        const string k_FittedMesh = k_Folder + "/Watch_Fitted.asset";
        const string k_ScreenFolder = k_Folder + "/Screen";
        const string k_AlarmClip = "Assets/C/Audio/Watch/C_WatchAlarm.wav";
        const string k_Font = "Assets/C/Fonts/NotoSansKR-Regular SDF.asset";
        const string k_SpriteMaterial = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        // Watch.fbx 치수 (모델 단위, 11/18 광선으로 잼)
        static readonly Vector3 k_ScreenNormal = new Vector3(-0.9619f, 0.2732f, 0f).normalized;   // 화면 바깥쪽
        static readonly Vector3 k_ThreeOClock = new Vector3(0f, 0f, -1f);                          // 홈 파인 버튼 2개 쪽
        static readonly Vector3 k_ScreenCenter = new Vector3(-0.4257f, 0.1081f, 0f);
        const float k_ScreenRadius = 0.205f;                                                      // 유리 가장자리 0.212 — 베젤 안쪽으로 조금
        static readonly Vector3 k_LoopCenter = new Vector3(0.1155f, -0.0269f, 0f);
        const float k_CaseBack = 0.369f;       // 고리 가운데 → 케이스 뒷면 안쪽 (화면 법선 방향)
        const float k_FarInner = 0.369f;       // 고리 가운데 → 손등 쪽 줄 안쪽
        const float k_Scale = 0.066f;

        // 손목 (L_Wrist 기준, m): 단면 너비(엄지-새끼, X) 5.2 · 두께(손바닥-손등, Y) 3.7 — 손바닥 면 y = -0.0185
        const float k_WristThickness = 0.037f;
        const float k_FitClearance = 0.003f;
        const float k_PressFrom = 0.20f;       // 고리 가운데에서 화면 쪽 이 거리보다 뒤(손등 쪽)만 누른다
        static readonly Vector3 k_WearPosition = new Vector3(-0.001f, -0.0200f, -0.011f);   // 케이스 뒷면 가운데: 손바닥 면에서 1.5 mm, 줄이 손 메시 끝(z -0.0205)을 덮게

        // 화면 배치 (m) — 화면 반지름 R = 0.205 × 0.066 = 13.5 mm
        const float k_IconSize = 0.0048f, k_IconGap = 0.0008f;
        const float k_Fit = 0.92f;             // 글자 · 아이콘 모서리가 화면 원의 이 비율 안에

        [MenuItem("MilTrainSimVR/C/손목시계 만들기 (모델 맞추기 · 화면 · 소리 · 프리팹 · 경기 진행에 연결)", false, 57)]
        static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[C] 손목시계 — Play 중에는 만들지 않는다");
                return;
            }
            var source = AssetDatabase.LoadAssetAtPath<Mesh>(k_Fbx);
            var material = AssetDatabase.LoadAssetAtPath<Material>(k_Material);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(k_Font);
            if (source == null || material == null || font == null)
            {
                Debug.LogError($"[C] 손목시계 — {k_Fbx} · {k_Material} · {k_Font} 중 없는 것이 있다");
                return;
            }
            var art = new ScreenArt
            {
                enemy = DrawIcon("WatchIcon_Enemy", 256, 256, EnemyShape),
                timer = DrawIcon("WatchIcon_Timer", 256, 256, TimerShape),
                flag = DrawIcon("WatchIcon_Flag", 256, 256, FlagShape),
                disc = DrawIcon("WatchScreen_Disc", 256, 256, p => Circle(p, Vector2.zero, 0.985f)),
                line = DrawIcon("WatchScreen_Line", 256, 32, p => Capsule(p, new Vector2(-0.94f, 0f), new Vector2(0.94f, 0f), 0.09f)),
            };
            Mesh fitted = BuildFittedMesh(source);
            AudioClip alarm = BuildAlarm();
            GameObject prefab = BuildPrefab(fitted, material, font, art, alarm);
            ConnectToMatch(prefab);
            Debug.Log("[C] 손목시계를 만들었다 — " + k_Prefab);
        }

        struct ScreenArt
        {
            public Sprite enemy, timer, flag, disc, line;
        }

        // ---------------------------------------------------------------- 손목에 맞춘 메시

        // 고리 가운데에서 화면 법선 방향 거리 d 가 k_PressFrom 보다 작은 점만 d 를 줄인다: x = from - d, x' = x - a·x² (x = 0 에서 기울기 1 — 꺾이지 않는다)
        static Mesh BuildFittedMesh(Mesh source)
        {
            Vector3 n = k_ScreenNormal;
            float targetInner = (k_WristThickness + k_FitClearance) / k_Scale;   // 고리 안쪽 두께 (모델 단위)
            float x0 = k_PressFrom + k_FarInner;                                 // 지금 손등 쪽 안쪽까지
            float x1 = k_PressFrom + (targetInner - k_CaseBack);                 // 바꿀 자리
            float a = (x0 - x1) / (x0 * x0);

            var mesh = Object.Instantiate(source);
            mesh.name = "Watch_Fitted";
            Vector3[] v = mesh.vertices;
            Vector3[] nor = mesh.normals;
            Vector4[] tan = mesh.tangents;
            for (int i = 0; i < v.Length; i++)
            {
                float d = Vector3.Dot(v[i] - k_LoopCenter, n);
                if (d >= k_PressFrom)
                    continue;
                float x = k_PressFrom - d;
                float k = 1f - 2f * a * x;                 // 이 자리의 늘림 배율 (법선 방향만)
                v[i] += n * (a * x * x);                   // d' = from - (x - a x²) → 화면 쪽으로 a x² 만큼
                if (nor.Length == v.Length)
                {
                    Vector3 m = nor[i];
                    nor[i] = (m + (1f / k - 1f) * Vector3.Dot(m, n) * n).normalized;   // 법선은 역전치로
                }
                if (tan.Length == v.Length)
                {
                    Vector3 t = tan[i];
                    t = (t + (k - 1f) * Vector3.Dot(t, n) * n).normalized;
                    tan[i] = new Vector4(t.x, t.y, t.z, tan[i].w);
                }
            }
            mesh.vertices = v;
            if (nor.Length == v.Length)
                mesh.normals = nor;
            if (tan.Length == v.Length)
                mesh.tangents = tan;
            mesh.RecalculateBounds();

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(k_FittedMesh);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssetIfDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, k_FittedMesh);
            return mesh;
        }

        // ---------------------------------------------------------------- 삐비비빅 소리

        // 디지털 시계 알람 — 짧은 삑(50 ms) 4번이 한 묶음(삐비비빅), 0.9 초마다 4묶음. 3.2 kHz + 약한 3 · 5배음(피에조처럼 네모파 쪽)
        static AudioClip BuildAlarm()
        {
            const int rate = 44100, bursts = 4, perBurst = 4;
            const float beep = 0.05f, gap = 0.035f, every = 0.9f, freq = 3200f, gain = 0.32f;
            float length = every * (bursts - 1) + perBurst * (beep + gap) + 0.05f;
            var samples = new float[Mathf.CeilToInt(length * rate)];
            int len = Mathf.RoundToInt(beep * rate);
            for (int b = 0; b < bursts; b++)
                for (int k = 0; k < perBurst; k++)
                {
                    int s0 = Mathf.RoundToInt((b * every + k * (beep + gap)) * rate);
                    for (int i = 0; i < len && s0 + i < samples.Length; i++)
                    {
                        float t = i / (float)rate;
                        float env = Mathf.Min(1f, i / (0.002f * rate)) * Mathf.Min(1f, (len - i) / (0.003f * rate));
                        float w = 2f * Mathf.PI * freq * t;
                        float s = Mathf.Sin(w) + 0.25f * Mathf.Sin(3f * w) + 0.1f * Mathf.Sin(5f * w);
                        samples[s0 + i] = s * env * gain;
                    }
                }

            string dir = Path.GetDirectoryName(k_AlarmClip).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder("Assets/C/Audio", "Watch");
            using (var stream = new FileStream(Path.GetFullPath(k_AlarmClip), FileMode.Create))
            using (var w = new BinaryWriter(stream))
            {
                int bytes = samples.Length * 2;
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + bytes);
                w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                w.Write(16);
                w.Write((short)1);      // PCM
                w.Write((short)1);      // 모노
                w.Write(rate);
                w.Write(rate * 2);
                w.Write((short)2);
                w.Write((short)16);
                w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                w.Write(bytes);
                foreach (float s in samples)
                    w.Write((short)Mathf.RoundToInt(Mathf.Clamp(s, -1f, 1f) * 32767f));
            }
            AssetDatabase.ImportAsset(k_AlarmClip, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(k_AlarmClip);
        }

        // ---------------------------------------------------------------- 화면 아이콘 (거리 함수, 흰색 — 색은 SpriteRenderer.color)

        delegate float Shape(Vector2 p);

        static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;

        static float Box(Vector2 p, Vector2 c, Vector2 half)
        {
            Vector2 d = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half;
            return new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f);
        }

        static float Capsule(Vector2 p, Vector2 a, Vector2 b, float r)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude - r;
        }

        static float Ellipse(Vector2 p, Vector2 c, Vector2 r)
        {
            Vector2 q = new Vector2((p.x - c.x) / r.x, (p.y - c.y) / r.y);
            return (q.magnitude - 1f) * Mathf.Min(r.x, r.y);   // 근사 — 가장자리 부드럽게 하기엔 충분
        }

        // 헬멧 쓴 병사 상반신
        static float EnemyShape(Vector2 p)
        {
            float torso = Ellipse(p, new Vector2(0f, -1.06f), new Vector2(0.86f, 0.62f));
            float neck = Box(p, new Vector2(0f, -0.40f), new Vector2(0.15f, 0.10f));
            float head = Circle(p, new Vector2(0f, -0.10f), 0.30f);
            float dome = Mathf.Max(Circle(p, new Vector2(0f, 0.02f), 0.46f), -0.02f - p.y);
            float brim = Box(p, new Vector2(0f, -0.045f), new Vector2(0.56f, 0.05f)) - 0.02f;
            return Mathf.Min(Mathf.Min(torso, neck), Mathf.Min(head, Mathf.Min(dome, brim)));
        }

        // 초시계
        static float TimerShape(Vector2 p)
        {
            Vector2 c = new Vector2(0f, -0.12f);
            float ring = Mathf.Abs(Circle(p, c, 0.70f)) - 0.10f;
            float crown = Box(p, new Vector2(0f, 0.80f), new Vector2(0.15f, 0.07f)) - 0.02f;
            float stem = Box(p, new Vector2(0f, 0.68f), new Vector2(0.06f, 0.08f));
            Vector2 q = p - new Vector2(0.60f, 0.48f);
            float s = Mathf.Sqrt(0.5f);
            q = new Vector2(q.x * s + q.y * s, -q.x * s + q.y * s);   // 45° 돌린 옆 단추
            float side = Box(q, Vector2.zero, new Vector2(0.05f, 0.10f));
            float hand = Capsule(p, c, c + new Vector2(0.27f, 0.33f), 0.07f);
            float hub = Circle(p, c, 0.10f);
            return Mathf.Min(Mathf.Min(ring, Mathf.Min(crown, stem)), Mathf.Min(side, Mathf.Min(hand, hub)));
        }

        // 점령 깃발 — 깃대 + 오른쪽으로 날리는 깃발(끝이 제비꼬리)
        static float FlagShape(Vector2 p)
        {
            float pole = Box(p, new Vector2(-0.52f, -0.05f), new Vector2(0.07f, 0.88f)) - 0.02f;
            float knob = Circle(p, new Vector2(-0.52f, 0.86f), 0.11f);
            float cloth = Box(p, new Vector2(0.115f, 0.42f), new Vector2(0.565f, 0.36f)) - 0.02f;
            // 제비꼬리: 오른쪽 끝 가운데를 삼각형으로 판다 — 경계 x = 0.36 + 0.889·|y - 0.42| 보다 오른쪽을 뺀다 (0.747 = 기울기 보정)
            float notch = (0.36f + 0.889f * Mathf.Abs(p.y - 0.42f) - p.x) * 0.747f;
            cloth = Mathf.Max(cloth, -notch);
            float baseBar = Box(p, new Vector2(-0.52f, -0.92f), new Vector2(0.30f, 0.06f)) - 0.02f;
            return Mathf.Min(Mathf.Min(pole, knob), Mathf.Min(cloth, baseBar));
        }

        static Sprite DrawIcon(string name, int width, int height, Shape shape)
        {
            if (!AssetDatabase.IsValidFolder(k_ScreenFolder))
                AssetDatabase.CreateFolder(k_Folder, "Screen");
            string path = $"{k_ScreenFolder}/{name}.png";
            // 좌표: 긴 변 기준 -1..1 (y 위)
            float unit = 2f / Mathf.Max(width, height);
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    var p = new Vector2((x + 0.5f - width * 0.5f) * unit, (y + 0.5f - height * 0.5f) * unit);
                    float alpha = Mathf.Clamp01(0.5f - shape(p) / unit);
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(Path.GetFullPath(path), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = Mathf.Max(width, height);   // 긴 변 = 1 단위 → Transform 배율 = 크기(m)
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = true;
            ti.filterMode = FilterMode.Trilinear;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ---------------------------------------------------------------- 프리팹

        static GameObject BuildPrefab(Mesh fitted, Material material, TMP_FontAsset font, ScreenArt art, AudioClip alarm)
        {
            Vector3 n = k_ScreenNormal;
            Vector3 twelve = Vector3.Cross(-n, k_ThreeOClock).normalized;
            Vector3 back = k_LoopCenter + n * k_CaseBack;                         // 케이스 뒷면 가운데 = 시계 원점
            Material spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(k_SpriteMaterial);
            float radius = k_ScreenRadius * k_Scale;

            var root = new GameObject("C_WristWatch");
            var watch = root.AddComponent<WristWatch>();

            var model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = -back * k_Scale;
            model.transform.localScale = Vector3.one * k_Scale;
            model.AddComponent<MeshFilter>().sharedMesh = fitted;
            model.AddComponent<MeshRenderer>().sharedMaterial = material;

            // 알람 — 손목에서 나는 소리 (머리에서 0.3 ~ 0.6 m)
            var audio = root.AddComponent<AudioSource>();
            audio.clip = alarm;
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            audio.rolloffMode = AudioRolloffMode.Logarithmic;
            audio.minDistance = 0.4f;
            audio.maxDistance = 8f;
            audio.dopplerLevel = 0f;
            audio.volume = 0.8f;

            // 화면: +Z = 시계 안쪽 (TMP · 스프라이트는 -Z 쪽에서 읽힌다), 위 = 12시
            var screen = new GameObject("Screen").transform;
            screen.SetParent(root.transform, false);
            screen.localPosition = (k_ScreenCenter + n * 0.006f - back) * k_Scale;
            screen.localRotation = Quaternion.LookRotation(-n, twelve);

            SpriteRenderer MakeSprite(Transform parent, string name, Sprite sprite, Vector3 pos, Vector2 size, Color color, int order)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.transform.localPosition = pos;
                go.transform.localScale = new Vector3(size.x, size.y, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = color;
                sr.sortingOrder = order;
                sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                sr.receiveShadows = false;
                if (spriteMaterial != null)
                    sr.sharedMaterial = spriteMaterial;
                return sr;
            }

            TextMeshPro MakeText(Transform parent, string name, TextAlignmentOptions align, Vector2 pivot)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                var t = go.AddComponent<TextMeshPro>();
                t.font = font;
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.overflowMode = TextOverflowModes.Overflow;
                t.alignment = align;
                t.rectTransform.pivot = pivot;
                t.rectTransform.sizeDelta = new Vector2(0.03f, 0.01f);
                t.sortingOrder = 2;
                var r = t.GetComponent<MeshRenderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                return t;
            }

            // 글자 높이(m)가 되는 글자 크기 — 10 으로 재서 비례
            float SizeFor(TextMeshPro t, string sample, float height)
            {
                t.text = sample;
                t.fontSize = 10f;
                t.ForceMeshUpdate();
                float h = t.textBounds.size.y;
                return h > 0f ? 10f * height / h : 0.5f;
            }

            // y 줄에 [아이콘][틈][sample] 이 화면 원 안(k_Fit)에 들어가는 가장 큰 글자 크기 (높이는 height 까지)
            float FitRow(TextMeshPro t, string sample, float y, float height)
            {
                float size = SizeFor(t, sample, height);
                for (int i = 0; i < 30; i++)
                {
                    t.fontSize = size;
                    t.ForceMeshUpdate();
                    Vector2 b = t.textBounds.size;
                    float half = Mathf.Max(k_IconSize, b.y) * 0.5f;
                    float corner = new Vector2((k_IconSize + k_IconGap + b.x) * 0.5f, Mathf.Abs(y) + half).magnitude;
                    if (corner <= radius * k_Fit)
                        break;
                    size *= 0.96f;
                }
                return size;
            }

            MakeSprite(screen, "Disc", art.disc, Vector3.zero, Vector2.one * radius * 2f, new Color(0.025f, 0.035f, 0.03f, 1f), 0);

            // 두 줄 화면
            const float z = -0.0002f;
            float topY = radius * 0.36f, bottomY = -radius * 0.38f;
            var rows = new GameObject("Rows").transform;
            rows.SetParent(screen, false);
            SpriteRenderer enemyIcon = MakeSprite(rows, "EnemyIcon", art.enemy, new Vector3(0f, topY, z), Vector2.one * k_IconSize, Color.white, 1);
            TextMeshPro enemyText = MakeText(rows, "EnemyText", TextAlignmentOptions.MidlineLeft, new Vector2(0f, 0.5f));
            enemyText.fontSize = FitRow(enemyText, "20/20", topY, 0.0040f);
            enemyText.transform.localPosition = new Vector3(0f, topY, z);
            // 선: 그림의 굵기 0.18/2 → 높이 배율 0.0044 면 0.4 mm
            MakeSprite(rows, "Divider", art.line, new Vector3(0f, -radius * 0.01f, z), new Vector2(radius * 1.5f, 0.0044f), new Color(1f, 1f, 1f, 0.5f), 1);
            SpriteRenderer bottomIcon = MakeSprite(rows, "BottomIcon", art.timer, new Vector3(0f, bottomY, z), Vector2.one * k_IconSize, Color.white, 1);
            TextMeshPro bottomText = MakeText(rows, "BottomText", TextAlignmentOptions.MidlineLeft, new Vector2(0f, 0.5f));
            float timeSize = FitRow(bottomText, "88:88", bottomY, 0.0048f);
            float captureSize = FitRow(bottomText, "나 30초 / 적 30초", bottomY, 0.0040f);
            bottomText.transform.localPosition = new Vector3(0f, bottomY, z);

            // 알림 화면 — 제목(오버타임) · 내용(점령지). 글자 길이가 바뀌어도 원 안에 들게 상자 안 자동 크기
            var notice = new GameObject("Notice").transform;
            notice.SetParent(screen, false);
            TextMeshPro NoticeText(string name, float y, float height, string sample)
            {
                TextMeshPro t = MakeText(notice, name, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f));
                float max = SizeFor(t, sample, height);
                float chord = 2f * Mathf.Sqrt(Mathf.Max(0f, (radius * k_Fit) * (radius * k_Fit) - (Mathf.Abs(y) + height * 0.5f) * (Mathf.Abs(y) + height * 0.5f)));
                t.rectTransform.sizeDelta = new Vector2(chord, height * 1.6f);
                t.enableAutoSizing = true;
                t.fontSizeMax = max;
                t.fontSizeMin = max * 0.3f;
                t.transform.localPosition = new Vector3(0f, y, z);
                return t;
            }
            TextMeshPro noticeTitle = NoticeText("NoticeTitle", radius * 0.26f, 0.0052f, "오버타임");
            TextMeshPro noticeBody = NoticeText("NoticeBody", -radius * 0.30f, 0.0034f, "백화점 3층");
            noticeTitle.text = "오버타임";
            noticeBody.text = "백화점 3층";
            notice.gameObject.SetActive(false);

            // 손목 뼈 기준 회전: 화면 법선 → 손바닥(-Y), 3시 → 손가락(+Z)  (그러면 12시 → 엄지(+X))
            Quaternion modelFrame = Quaternion.LookRotation(k_ThreeOClock, n);
            Quaternion wristFrame = Quaternion.LookRotation(Vector3.forward, Vector3.down);
            Quaternion wear = wristFrame * Quaternion.Inverse(modelFrame);

            var so = new SerializedObject(watch);
            so.FindProperty("m_Rows").objectReferenceValue = rows.gameObject;
            so.FindProperty("m_EnemyText").objectReferenceValue = enemyText;
            so.FindProperty("m_EnemyIcon").objectReferenceValue = enemyIcon;
            so.FindProperty("m_BottomText").objectReferenceValue = bottomText;
            so.FindProperty("m_BottomIcon").objectReferenceValue = bottomIcon;
            so.FindProperty("m_TimerSprite").objectReferenceValue = art.timer;
            so.FindProperty("m_FlagSprite").objectReferenceValue = art.flag;
            so.FindProperty("m_IconSize").floatValue = k_IconSize;
            so.FindProperty("m_IconGap").floatValue = k_IconGap;
            so.FindProperty("m_TimeFontSize").floatValue = timeSize;
            so.FindProperty("m_CaptureFontSize").floatValue = captureSize;
            so.FindProperty("m_Notice").objectReferenceValue = notice.gameObject;
            so.FindProperty("m_NoticeTitle").objectReferenceValue = noticeTitle;
            so.FindProperty("m_NoticeBody").objectReferenceValue = noticeBody;
            so.FindProperty("m_Beep").objectReferenceValue = audio;
            so.FindProperty("m_WristPosition").vector3Value = k_WearPosition;
            so.FindProperty("m_WristEuler").vector3Value = wear.eulerAngles;
            so.ApplyModifiedPropertiesWithoutUndo();
            watch.ShowTime(WristWatch.State.Idle, 300, 20, 20);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, k_Prefab);
            Object.DestroyImmediate(root);
            return saved;
        }

        // ---------------------------------------------------------------- 경기 진행에 연결

        static void ConnectToMatch(GameObject prefab)
        {
            var display = Object.FindFirstObjectByType<MatchDisplay>(FindObjectsInactive.Include);
            if (display == null)
            {
                Debug.LogWarning("[C] 손목시계 — 열린 씬에 MatchDisplay 가 없어 연결하지 않았다 (C_UrbanBlock 을 열고 다시 누를 것)");
                return;
            }
            var so = new SerializedObject(display);
            var prop = so.FindProperty("m_WatchPrefab");
            if (prop == null || prop.objectReferenceValue == prefab.GetComponent<WristWatch>())
                return;
            prop.objectReferenceValue = prefab.GetComponent<WristWatch>();
            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(display.gameObject.scene);
        }
    }
}
