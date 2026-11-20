using System.IO;
using UnityEditor;
using UnityEngine;

namespace MilTrainSimVR.A.EditorTools
{
    // 사격장 바닥 반복 무늬 줄이기 (11/02 사용자) — Floor_2 원본 텍스처는 그대로 두고 두 장을 새로 만든다.
    //   1) AlbedoFlat: 원본 색에서 크게 흐린 것을 빼고 평균 색을 더한다 → 2 m 마다 반복되던 큰 얼룩이 없어지고 잔 질감만 남는다.
    //      흐리기는 가장자리를 감아서(이음새 없는 텍스처라) 해서 결과도 이음새가 없다
    //   2) Variation: 바닥 전체에 한 번만 펴는 회색 큰 얼룩 (Perlin 노이즈) — 재질 디테일 맵(곱하기 ×2, 128 = 그대로)으로 얹어 반복되지 않는 명암을 준다.
    //      URP 디테일은 텍스처 값을 그대로 ×2 하므로 sRGB 를 끈다 (켜면 128 이 0.22 로 읽혀 바닥이 어두워진다)
    // 값을 바꾸면 메뉴를 다시 누르고, 사격장 바닥 메뉴도 다시 누른다.
    static class FloorTextureBuilder
    {
        internal const string k_Folder = "Assets/A/Models/Floor_2";
        internal const string k_SourcePath = k_Folder + "/Tile_DefaultMaterial_AlbedoTransparency.png";
        internal const string k_FlatPath = k_Folder + "/Tile_DefaultMaterial_AlbedoFlat.png";
        internal const string k_VariationPath = k_Folder + "/Range_FloorVariation.png";

        const int k_BlurRadius = 24;          // 픽셀 (1024 = 2 m → 약 5 cm), 상자 흐리기 3번 ≈ 가우스. 이보다 큰 얼룩을 지운다
        const int k_BlurPasses = 3;

        // 큰 얼룩 맵 — 바닥(15.5 × 35.4 m)에 한 장
        static readonly Vector2 k_FloorMeters = new Vector2(15.5f, 35.4f);
        const int k_VariationPixelsPerMeter = 16;
        const float k_VariationFeatureMeters = 5f;   // 큰 얼룩 크기
        const float k_VariationAmplitude = 28f;      // 128 ± (0~255) — 디테일은 ×2 곱하기라 약 ±22% 밝기
        const int k_Seed = 1102;

        // 잔디 (11/02 사용자: Models/GrassTextures.jpg — 인조잔디 사진 7952 × 5304) — 원본은 그대로 두고 쓰기 좋은 사본을 만든다:
        //   폭 k_GrassWidth 로 줄이고, 큰 명암(사진 가장자리 어두움)을 평평하게 한다. 사진이라 이음새가 있어 재질에서 거울 반복(Mirror)으로 잇는다
        internal const string k_GrassSourcePath = "Assets/A/Models/GrassTextures.jpg";
        internal const string k_GrassPath = "Assets/A/Models/GrassTextures_Tiled.png";
        const int k_GrassWidth = 2048;
        const int k_GrassBlurRadius = 96;     // 픽셀 — 이보다 큰 명암 차이를 지운다

        [MenuItem("A/만들기/잔디 텍스처 (사진 → 반복용)")]
        internal static void BuildGrass()
        {
            var source = ReadPixels(k_GrassSourcePath);
            if (source == null)
                return;
            int sw = source.width, sh = source.height;
            Color32[] src = source.GetPixels32();
            Object.DestroyImmediate(source);

            // 칸 평균으로 줄이기
            int w = k_GrassWidth, h = Mathf.RoundToInt(k_GrassWidth * sh / (float)sw);
            var c = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                int y0 = y * sh / h, y1 = Mathf.Max(y0 + 1, (y + 1) * sh / h);
                for (int x = 0; x < w; x++)
                {
                    int x0 = x * sw / w, x1 = Mathf.Max(x0 + 1, (x + 1) * sw / w);
                    float r = 0f, g = 0f, b = 0f;
                    for (int yy = y0; yy < y1; yy++)
                        for (int xx = x0; xx < x1; xx++)
                        {
                            Color32 p = src[yy * sw + xx];
                            r += p.r; g += p.g; b += p.b;
                        }
                    float n = (x1 - x0) * (y1 - y0) * 255f;
                    c[y * w + x] = new Color(r / n, g / n, b / n, 1f);
                }
            }
            Write(k_GrassPath, Flatten(c, w, h, k_GrassBlurRadius), w, h, true, true, TextureWrapMode.Mirror);
            Debug.Log($"[A] 잔디 텍스처: {k_GrassPath} ({w} × {h}, 거울 반복)");
        }

        [MenuItem("A/만들기/사격장 바닥 텍스처 (얼룩 평탄화 + 큰 얼룩 맵)")]
        internal static void Build()
        {
            BuildFlat();
            BuildVariation();
            Debug.Log($"[A] 바닥 텍스처: {k_FlatPath}, {k_VariationPath}");
        }

        static void BuildFlat()
        {
            var source = ReadPixels(k_SourcePath);
            if (source == null)
                return;
            int w = source.width, h = source.height;
            Color[] c = source.GetPixels();
            Object.DestroyImmediate(source);

            Write(k_FlatPath, Flatten(c, w, h, k_BlurRadius), w, h, true, true);
        }

        // 큰 명암 차이 지우기 — 크게 흐린 것을 빼고 평균 색을 더한다
        static Color[] Flatten(Color[] c, int w, int h, int radius)
        {
            Color mean = Color.black;
            foreach (Color p in c)
                mean += p;
            mean /= c.Length;

            Color[] blur = (Color[])c.Clone();
            for (int i = 0; i < k_BlurPasses; i++)
            {
                blur = BoxBlur(blur, w, h, radius, true);
                blur = BoxBlur(blur, w, h, radius, false);
            }
            var result = new Color[c.Length];
            for (int i = 0; i < c.Length; i++)
            {
                Color v = c[i] - blur[i] + mean;
                result[i] = new Color(Mathf.Clamp01(v.r), Mathf.Clamp01(v.g), Mathf.Clamp01(v.b), 1f);
            }
            return result;
        }

        static void BuildVariation()
        {
            int w = Mathf.RoundToInt(k_FloorMeters.x * k_VariationPixelsPerMeter);
            int h = Mathf.RoundToInt(k_FloorMeters.y * k_VariationPixelsPerMeter);
            var random = new System.Random(k_Seed);
            Vector2 o1 = new Vector2(random.Next(1000), random.Next(1000));
            Vector2 o2 = new Vector2(random.Next(1000), random.Next(1000));
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // 미터 단위 좌표 — 큰 얼룩 + 반 크기 얼룩
                    float mx = x / (float)k_VariationPixelsPerMeter, my = y / (float)k_VariationPixelsPerMeter;
                    float n = Mathf.PerlinNoise(o1.x + mx / k_VariationFeatureMeters, o1.y + my / k_VariationFeatureMeters) * 0.65f
                            + Mathf.PerlinNoise(o2.x + mx * 2f / k_VariationFeatureMeters, o2.y + my * 2f / k_VariationFeatureMeters) * 0.35f;
                    float g = (128f + (n - 0.5f) * 2f * k_VariationAmplitude) / 255f;
                    pixels[y * w + x] = new Color(g, g, g, 1f);
                }
            Write(k_VariationPath, pixels, w, h, false, false);
        }

        // 한 방향 상자 흐리기. wrap = 가장자리를 감는다 (이음새 없는 텍스처)
        static Color[] BoxBlur(Color[] src, int w, int h, int r, bool horizontal)
        {
            var dst = new Color[src.Length];
            int len = horizontal ? w : h, lines = horizontal ? h : w;
            float inv = 1f / (r * 2 + 1);
            for (int line = 0; line < lines; line++)
            {
                Color sum = Color.black;
                for (int k = -r; k <= r; k++)
                    sum += src[Index(Wrap(k, len), line, w, horizontal)];
                for (int i = 0; i < len; i++)
                {
                    dst[Index(i, line, w, horizontal)] = sum * inv;
                    sum += src[Index(Wrap(i + r + 1, len), line, w, horizontal)] - src[Index(Wrap(i - r, len), line, w, horizontal)];
                }
            }
            return dst;
        }

        static int Wrap(int i, int n) => ((i % n) + n) % n;

        static int Index(int i, int line, int w, bool horizontal) => horizontal ? line * w + i : i * w + line;

        // 원본을 읽기 가능 여부와 상관없이 읽는다 (파일에서 바로)
        static Texture2D ReadPixels(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"[A] {path} 이 없다");
                return null;
            }
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            texture.LoadImage(File.ReadAllBytes(path));
            return texture;
        }

        static void Write(string path, Color[] pixels, int w, int h, bool mipmapsAniso, bool sRGB, TextureWrapMode wrap = TextureWrapMode.Repeat)
        {
            var texture = new Texture2D(w, h, TextureFormat.RGB24, false);
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = sRGB;
            importer.wrapMode = wrap;
            if (mipmapsAniso)
                importer.anisoLevel = 8;   // 바닥은 비스듬히 본다
            importer.SaveAndReimport();
        }
    }
}
