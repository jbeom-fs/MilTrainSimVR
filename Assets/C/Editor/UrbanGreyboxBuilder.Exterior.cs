using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MilTrainSimVR.UrbanCombat.EditorTools
{
    // 일반 건물 바깥벽 마감 (사용자 11/06 — 목업 두 번 확인: 배정안 → '반복이 보여 옛날 게임 같다' 1~3단계 → 적용).
    // 배치의 건물 상자 finish(마감) · stoneBase(1층 화강석)를 고른 건물은 지금처럼 벽을 tint 재질로 다 지은 뒤, 합칠 때(CombineVisuals) 벽 재질 면을 나눠 칠한다:
    //   · 바깥 겉면(건물 좌표 x = 0 · W, z = 0 · D 평면에서 바깥을 보는 면)과 창 · 문 테두리(벽 두께 안) → 마감 재질 (stoneBase 면 1층 높이 안은 진회색 화강석)
    //   · 옥상(맨 위에서 위를 보는 면) → 콘크리트 · 나머지(실내 칸막이 · 바깥벽 안쪽 · 막힌 층 밑면) → 흰 미장
    //   → 실내 · 구조 코드는 그대로이고 부딪힘 · 길찾기도 같다
    // 반복이 덜 보이게 (목업 2차 1~3단계):
    //   1) 텍스처에서 반복을 드러내는 무늬를 뺐다 — 타일 · 벽돌의 진한 장, 미장의 흙손 자국. 대신 줄눈 폭 흔들림 · 줄 어긋남 · 모서리 깨짐 · 생산 묶음 띠
    //      (Textures/Map/Source~/make_exterior_textures.py)
    //   2) 큰 얼룩층: URP Lit Detail 맵 칸에 한 장 30 m 의 밝기 · 색 얼룩(Macro1). Detail UV = 바탕 UV × 비율이라 재질마다 (한 장 크기 / 30).
    //      같은 마감도 재질 3벌 — 밝기 0.96 · 1 · 1.04, 색 ±2 %, 얼룩 자리를 달리해 건물마다 다르게 (건물 이름으로 고른다)
    //   3) 때 자국: 창 아래 빗물 줄 · 막힌 층 창 띠 아래 빗물 줄 · 1층 아래 흙탕 때 · 옥상 밑 검은 줄 — 바깥 겉면과 겹치는 만큼만 얇은 판(벽에서 1.2 cm)으로.
    //      판은 알파 블렌드(Unlit) — 곱하기 블렌드는 URP Unlit 에서 텍스처 알파가 안 먹었다 (11/06 시험)
    public static partial class UrbanGreyboxBuilder
    {
        const float k_MacroSize = 30f;          // 큰 얼룩층 한 장 (m)
        const float k_GrimeOffset = 0.012f;     // 때 자국 판을 벽에서 띄우는 거리 — 멀리서도 벽과 깜빡이지 않게

        // 마감마다: 텍스처 폴더 · 한 장 크기(m) · 시작 높이 · 매끄러움 · 금속 · 곱하는 색. 크기는 make_exterior_textures.py 의 줄눈 치수와 같다
        static readonly (GreyboxBuildingSpec.Finish finish, string folder, float mw, float mh, float y0, float smooth, float metal, Color tint)[] k_Finishes =
        {
            (GreyboxBuildingSpec.Finish.TileBrown, "TileBrown", 1.88f, 1.904f, k_Curb, 0.5f, 0f, Color.white),
            (GreyboxBuildingSpec.Finish.TileBeige, "TileBeige", 1.88f, 1.904f, k_Curb, 0.5f, 0f, Color.white),
            (GreyboxBuildingSpec.Finish.TileGrey, "TileGrey", 2.4f, 2.4f, k_Curb, 0.45f, 0f, Color.white),
            (GreyboxBuildingSpec.Finish.Brick, "Brick1", 2.0f, 2.01f, k_Curb, 0.15f, 0f, Color.white),
            (GreyboxBuildingSpec.Finish.GraniteLight, "GraniteLight", 2.4f, 2.4f, k_Curb, 0.55f, 0f, Color.white),
            (GreyboxBuildingSpec.Finish.PanelSilver, "Panel1", 2.4f, 3.4f, k_Curb, 0.62f, 0.55f, new Color(0.88f, 0.9f, 0.93f)),
            (GreyboxBuildingSpec.Finish.PanelWhite, "Panel1", 2.4f, 3.4f, k_Curb, 0.6f, 0.1f, Color.white),
            (GreyboxBuildingSpec.Finish.PanelDark, "Panel1", 2.4f, 3.4f, k_Curb, 0.55f, 0.5f, new Color(0.33f, 0.34f, 0.36f)),
            (GreyboxBuildingSpec.Finish.Concrete, "ExposedConcrete", 3.6f, 3.6f, k_Curb, 0.25f, 0f, Color.white),
            (GreyboxBuildingSpec.Finish.StuccoIvory, "Stucco1", 2.4f, 2.4f, 0f, 0.2f, 0f, new Color(1.0f, 0.95f, 0.84f)),
            (GreyboxBuildingSpec.Finish.StuccoGrey, "Stucco1", 2.4f, 2.4f, 0f, 0.2f, 0f, new Color(0.86f, 0.83f, 0.78f)),
            (GreyboxBuildingSpec.Finish.StuccoApricot, "Stucco1", 2.4f, 2.4f, 0f, 0.2f, 0f, new Color(1.0f, 0.85f, 0.7f)),
            (GreyboxBuildingSpec.Finish.StuccoSage, "Stucco1", 2.4f, 2.4f, 0f, 0.2f, 0f, new Color(0.82f, 0.88f, 0.76f)),
        };

        // 합칠 때 건물 하나의 벽을 나눠 칠하는 데 필요한 것
        class ExteriorSkin
        {
            public Material wall, windows;              // 바꿀 벽 재질(tint) · 막힌 층 창 띠 재질(때 자국 자리를 찾는다)
            public Material face, bas, interior, roof, grime;
            public float W, D, roofY;
            public int seed;
        }

        // 마감을 고르지 않았거나(Grid) 세라믹 외벽(청운백화점)이면 null — 예전 그대로
        static ExteriorSkin ExteriorFor(GreyboxBuildingSpec spec, Transform root, Materials m)
        {
            // 겉면 재질(마감 3벌 중 하나)은 바깥벽 입체 디테일(파라펫 · 옥탑)과 같은 것 — UrbanGreyboxBuilder.Details.cs
            Material face = FaceMaterialFor(spec, m);
            if (face == null)
                return null;
            int hash = StableHash(spec.name) & 0x7fffffff;
            Transform t = spec.transform;
            return new ExteriorSkin
            {
                wall = m.walls[Mathf.Abs(spec.tint) % m.walls.Length],
                windows = m.windows,
                face = face,
                bas = spec.stoneBase && spec.enterable > 0 ? m.facadeGranite : null,
                interior = m.extInterior,
                roof = m.extRoof,
                grime = m.grime,
                W = Mathf.Abs(t.lossyScale.x),
                D = Mathf.Abs(t.lossyScale.z),
                roofY = k_Curb + Mathf.Max(1, spec.floors) * k_FloorHeight - 0.3f,
                seed = hash,
            };
        }

        // 벽 재질 면을 나눠 칠하고(면 재질을 바꾼다 — 가려진 면은 빼고), 때 자국 판을 만든다
        static void ApplyExterior(Transform root, List<Face> faces, ExteriorSkin s)
        {
            const float e = 0.01f;
            var rects = new List<Vector4>[4];   // 면마다 바깥 겉면 사각형 (u0, u1, y0, y1) — 0 앞(-z) · 1 뒤(+z) · 2 왼(-x) · 3 오른(+x), u 는 그 면을 따라(x 또는 z)
            for (int k = 0; k < 4; k++)
                rects[k] = new List<Vector4>();
            var sills = new List<(int side, float u0, float u1, float y)>();
            var bands = new List<(int side, float u0, float u1, float y)>();
            foreach (Face f in faces)
            {
                if (f.hidden)
                    continue;
                Vector3 lo = Vector3.Min(Vector3.Min(f.p[0], f.p[1]), Vector3.Min(f.p[2], f.p[3]));
                Vector3 hi = Vector3.Max(Vector3.Max(f.p[0], f.p[1]), Vector3.Max(f.p[2], f.p[3]));
                Vector3 c = (f.p[0] + f.p[1] + f.p[2] + f.p[3]) * 0.25f;
                Vector3 n = f.normal;
                if (f.material == s.windows)
                {
                    // 막힌 층 창 띠(벽에서 5 cm 나온 상자)의 바깥 앞면 → 그 아래로 빗물 줄
                    int bs = n.z < -0.9f && hi.z < 0f ? 0 : n.z > 0.9f && lo.z > s.D ? 1 : n.x < -0.9f && hi.x < 0f ? 2 : n.x > 0.9f && lo.x > s.W ? 3 : -1;
                    if (bs >= 0)
                        bands.Add((bs, bs < 2 ? lo.x : lo.z, bs < 2 ? hi.x : hi.z, lo.y));
                    continue;
                }
                if (f.material != s.wall)
                    continue;
                int side = n.z < -0.9f && c.z < e ? 0 : n.z > 0.9f && c.z > s.D - e ? 1 : n.x < -0.9f && c.x < e ? 2 : n.x > 0.9f && c.x > s.W - e ? 3 : -1;
                bool inF = c.z > e && c.z < k_Wall - e, inB = c.z < s.D - e && c.z > s.D - k_Wall + e;
                bool inL = c.x > e && c.x < k_Wall - e, inR = c.x < s.W - e && c.x > s.W - k_Wall + e;
                bool reveal = ((inF || inB) && Mathf.Abs(n.z) < 0.1f) || ((inL || inR) && Mathf.Abs(n.x) < 0.1f);
                if (n.y > 0.9f && c.y > s.roofY)
                    f.material = s.roof;
                else if (side >= 0 || reveal)
                    f.material = s.bas != null && hi.y <= k_Curb + k_FloorHeight + e ? s.bas : s.face;
                else
                    f.material = s.interior;
                if (side >= 0)
                {
                    rects[side].Add(new Vector4(side < 2 ? lo.x : lo.z, side < 2 ? hi.x : hi.z, lo.y, hi.y));
                }
                else if (reveal && n.y > 0.9f && c.y > k_Curb + 0.3f && c.y < s.roofY)
                {
                    // 창턱 윗면 → 그 창 아래로 빗물 줄
                    int sd = inF ? 0 : inB ? 1 : inL ? 2 : 3;
                    sills.Add((sd, sd < 2 ? lo.x : lo.z, sd < 2 ? hi.x : hi.z, c.y));
                }
            }
            BuildGrime(root, s, rects, sills, bands);
        }

        // 때 자국 판: 자리(zone)마다 같은 면의 바깥 겉면 사각형과 겹치는 만큼만 — 창 · 문 구멍 위로는 안 생긴다.
        // 아틀라스(Textures/Map/Grime1) 세 띠: 0 1층 흙탕 때(가로 한 장 4 m) · 1 빗물 줄(3 m) · 2 옥상 밑 줄(4 m)
        static void BuildGrime(Transform root, ExteriorSkin s, List<Vector4>[] rects, List<(int side, float u0, float u1, float y)> sills,
            List<(int side, float u0, float u1, float y)> bands)
        {
            if (s.grime == null)
                return;
            var rng = new System.Random(s.seed);
            var zones = new List<(int side, float u0, float u1, float y0, float y1, int kind, float off)>();
            for (int sd = 0; sd < 4; sd++)
            {
                if (rects[sd].Count == 0)
                    continue;
                float len = sd < 2 ? s.W : s.D, top = 0f;
                foreach (Vector4 r in rects[sd])
                    top = Mathf.Max(top, r.w);
                zones.Add((sd, 0f, len, k_Curb, k_Curb + 1.3f, 0, (float)rng.NextDouble() * 4f));
                zones.Add((sd, 0f, len, top - 1.9f, top, 2, (float)rng.NextDouble() * 4f));
            }
            foreach (var sl in sills)
                zones.Add((sl.side, sl.u0 - 0.08f, sl.u1 + 0.08f, Mathf.Max(k_Curb, sl.y - 0.9f - 0.8f * (float)rng.NextDouble()), sl.y, 1, (float)rng.NextDouble() * 3f));
            foreach (var bd in bands)
                zones.Add((bd.side, bd.u0, bd.u1, bd.y - 1.0f - 0.6f * (float)rng.NextDouble(), bd.y, 1, (float)rng.NextDouble() * 3f));

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var norms = new List<Vector3>();
            var tris = new List<int>();
            Vector3[] outward = { Vector3.back, Vector3.forward, Vector3.left, Vector3.right };
            float[] texW = { 4f, 3f, 4f };
            Vector3 Pt(int sd, float u, float y) => sd == 0 ? new Vector3(u, y, -k_GrimeOffset) : sd == 1 ? new Vector3(u, y, s.D + k_GrimeOffset)
                : sd == 2 ? new Vector3(-k_GrimeOffset, y, u) : new Vector3(s.W + k_GrimeOffset, y, u);
            foreach (var z in zones)
            {
                foreach (Vector4 r in rects[z.side])
                {
                    float u0 = Mathf.Max(z.u0, r.x), u1 = Mathf.Min(z.u1, r.y), y0 = Mathf.Max(z.y0, r.z), y1 = Mathf.Min(z.y1, r.w);
                    if (u1 - u0 < 0.05f || y1 - y0 < 0.05f)
                        continue;
                    Vector2 UV(float u, float y) => new Vector2((u + z.off) / texW[z.kind], (z.kind + 0.02f + 0.96f * (y - z.y0) / (z.y1 - z.y0)) / 3f);
                    Vector3 nrm = outward[z.side];
                    var q = new[] { Pt(z.side, u0, y0), Pt(z.side, u0, y1), Pt(z.side, u1, y1), Pt(z.side, u1, y0) };
                    var quv = new[] { UV(u0, y0), UV(u0, y1), UV(u1, y1), UV(u1, y0) };
                    // CombineVisuals 와 같은 감김: (v3 - v0) × (v1 - v0) 이 법선 쪽
                    if (Vector3.Dot(Vector3.Cross(q[3] - q[0], q[1] - q[0]), nrm) < 0f)
                    {
                        q = new[] { q[0], q[3], q[2], q[1] };
                        quv = new[] { quv[0], quv[3], quv[2], quv[1] };
                    }
                    int start = verts.Count;
                    for (int k = 0; k < 4; k++)
                    {
                        verts.Add(q[k]);
                        uvs.Add(quv[k]);
                        norms.Add(nrm);
                    }
                    tris.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
                }
            }
            if (verts.Count == 0)
                return;
            var mesh = new Mesh { name = root.name + "_Grime" };
            mesh.indexFormat = verts.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            var go = new GameObject("Mesh_GB_Ext_Grime");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = s.grime;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        // ---------------------------------------------------------------- 재질

        static void ExteriorMaterials(Materials m, Texture2D grid)
        {
            Texture2D macro = MapTexture($"{k_FloorTextureFolder}/Macro1", "_Detail", false, true);
            if (macro == null)
                Debug.LogWarning("[C] Textures/Map/Macro1 에 _Detail 텍스처가 없다 — 큰 얼룩층 없이 만든다");
            m.finishes = new Dictionary<GreyboxBuildingSpec.Finish, Material[]>();
            foreach (var f in k_Finishes)
            {
                var variants = new Material[3];
                for (int k = 0; k < variants.Length; k++)
                {
                    float b = 1f + (k - 1) * 0.04f, h = (k - 1) * 0.02f;
                    Color tint = f.tint * new Color(b * (1f + h), b, b * (1f - h), 1f);
                    variants[k] = ExteriorMat($"GB_Ext_{f.finish}_{k + 1}", f.folder, f.mw, f.mh, f.y0, f.smooth, f.metal, tint, macro,
                        new Vector2(0.37f * k, 0.61f * k), grid);
                }
                m.finishes[f.finish] = variants;
            }
            m.extInterior = ExteriorMat("GB_Ext_Interior", "Stucco1", 2.4f, 2.4f, 0f, 0.2f, 0f, new Color(0.98f, 0.97f, 0.94f), null, Vector2.zero, grid);
            m.extRoof = ExteriorMat("GB_Ext_Roof", "RoofConcrete", 3f, 3f, 0f, 0.15f, 0f, new Color(0.85f, 0.85f, 0.85f), macro, new Vector2(0.3f, 0.7f), grid);
            m.grime = GrimeMat("GB_Ext_Grime", MapTexture($"{k_FloorTextureFolder}/Grime1", "_Color", false, false));
            // 바깥벽 입체 디테일 (UrbanGreyboxBuilder.Details.cs) — 목업과 같은 색 · 매끄러움 · 금속
            var stone = new Color(0.92f, 0.9f, 0.86f);
            m.extStone = MapMat("GB_Ext_Trim_Stone", "Stucco1", Vector2.one * 2.4f, 0f, 0.3f, grid, stone, stone);
            m.extConcrete = MapMat("GB_Ext_Trim_Concrete", "RoofConcrete", Vector2.one * 3f, 0f, 0.2f, grid, new Color(0.7f, 0.7f, 0.7f), Color.white);
            m.extGroove = FacadeMat("GB_Ext_Trim_Groove", new Color(0.2f, 0.2f, 0.21f), 0.2f);
            m.extAlu = FacadeMat("GB_Ext_Trim_Alu", new Color(0.62f, 0.64f, 0.67f), 0.5f, 0.75f);
            m.extAluDark = FacadeMat("GB_Ext_Trim_AluDark", new Color(0.2f, 0.21f, 0.22f), 0.45f, 0.6f);
            m.extFrame = FacadeMat("GB_Ext_Frame", new Color(0.56f, 0.58f, 0.61f), 0.5f, 0.7f);
            m.extShutter = FacadeMat("GB_Ext_Shutter", new Color(0.7f, 0.71f, 0.72f), 0.35f, 0.5f);
            m.extDoor = FacadeMat("GB_Ext_Door", new Color(0.25f, 0.29f, 0.28f), 0.35f, 0.4f);
            m.extVent = FacadeMat("GB_Ext_Vent", new Color(0.16f, 0.17f, 0.18f), 0.3f, 0.3f);
            m.extRails = new[]
            {
                FacadeMat("GB_Ext_Rail_Green", new Color(0.2f, 0.33f, 0.27f), 0.4f, 0.3f),
                FacadeMat("GB_Ext_Rail_Grey", new Color(0.55f, 0.57f, 0.6f), 0.45f, 0.6f),
                FacadeMat("GB_Ext_Rail_White", new Color(0.82f, 0.83f, 0.82f), 0.35f, 0.2f),
            };
            // 막힌 층 창 띠(GB_Window_1)에도 큰 얼룩층 — 같은 창 그림이 길게 이어지는 게 덜 보이게
            if (macro != null && m.windows != null && m.windows.GetTexture("_DetailAlbedoMap") == null)
            {
                AddMacro(m.windows, macro, k_WindowTileWidth, 4f * k_FloorHeight, new Vector2(0.5f, 0.2f));
                EditorUtility.SetDirty(m.windows);
            }
            AssetDatabase.SaveAssets();
        }

        // MapMat 으로 만들고, 처음 만들 때만 금속 · 큰 얼룩층을 얹는다 (있으면 그대로 — Inspector 에서 고친 값이 남는다)
        static Material ExteriorMat(string name, string folder, float mw, float mh, float y0, float smooth, float metal, Color tint, Texture2D macro,
            Vector2 macroOffset, Texture2D grid)
        {
            bool exists = AssetDatabase.LoadAssetAtPath<Material>($"{k_MaterialFolder}/{name}.mat") != null;
            Material mat = MapMat(name, folder, new Vector2(mw, mh), y0, smooth, grid, tint, tint);
            if (exists)
                return mat;
            mat.SetFloat("_Metallic", metal);
            if (macro != null)
                AddMacro(mat, macro, mw, mh, macroOffset);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // 큰 얼룩층 — URP Lit Detail 맵(× 2, 평균 0.5 = 그대로). Detail UV 는 바탕 UV(한 장 = 1)에 곱해지니 비율 = 한 장 크기 / 30 m
        static void AddMacro(Material mat, Texture2D macro, float mw, float mh, Vector2 offset)
        {
            mat.SetTexture("_DetailAlbedoMap", macro);
            mat.SetTextureScale("_DetailAlbedoMap", new Vector2(mw / k_MacroSize, mh / k_MacroSize));
            mat.SetTextureOffset("_DetailAlbedoMap", offset);
            mat.SetFloat("_DetailAlbedoMapScale", 1f);
            mat.EnableKeyword("_DETAIL_MULX2");
        }

        // 때 자국 — Unlit 반투명(알파 블렌드), 그림자 없음
        static Material GrimeMat(string name, Texture2D tex)
        {
            string path = $"{k_MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
                return mat;
            if (tex == null)
            {
                Debug.LogWarning("[C] Textures/Map/Grime1 에 _Color 텍스처가 없다 — 때 자국 없이 만든다");
                return null;
            }
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_ZWrite", 0f);
            BaseShaderGUI.SetMaterialKeywords(mat);
            mat.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
