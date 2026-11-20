using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MilTrainSimVR.UrbanCombat.EditorTools
{
    // 주차장 관리 부스 프리팹 (사용자 11/16: "이 구역 남는 공간에, 작은 관리사무소를 만들어 줘. 문은 없어도 되니까, 그냥 몸 하나 숨길 공간 +
    //   창문틀이 뚫려 있어, 사격을 할 수 있는 부스") — Prefabs/Barrier/C_ParkingBooth. 콘크리트 벽 프리팹과 같은 폴더라
    //   씬 어디에 놓아도 적 길찾기 지도 굽기가 막힌 곳으로 읽는다(Barrier.cs HandPlacedBarriers) — 옮기면 지도를 다시 굽는다
    //   · 바깥 2.2 × 1.8 m, 안 2.0 × 1.6 m (한 사람이 앉고 일어서며 쏠 만큼), 벽 높이 2.45 m · 두께 0.1 m, 콘크리트 받침 0.12 m
    //   · 앞(+Z) 창 1.6 m, 양옆 창 0.9 m — 창턱 1.05 m(앉으면 몸이 가려지고 서면 창으로 쏜다), 창 위 1.95 m.
    //     창마다 깨지는 유리 한 장 (11/18 사용자 — WindowGlass.cs GlazeBoothPrefab): 총알 · 적 시선은 지나가고 첫 발에 부서진다
    //   · 뒤(-Z) 출입구 0.8 × 2.2 m, 문짝 없음 (2.1 m 면 캐릭터 머리가 문틀에 걸렸다 — Interior.cs k_DoorHeight 와 같은 까닭)
    //   · 흰 패널 벽(GB_Ext_PanelWhite_1) · 알루미늄 창틀 · 짙은 회색 지붕(사방 0.2 m 내밈) · 콘크리트 받침. UV 는 미터 — 맵 재질 반복 크기 그대로
    //   · 콜라이더: 벽 조각 · 지붕 · 받침 상자 — 사람 · 총알을 막고, 창 · 출입구는 뚫려 있다. 피벗은 받침 바닥 가운데
    public static partial class UrbanGreyboxBuilder
    {
        const string k_BoothMeshFolder = "Assets/C/Models/ParkingBooth";
        public const string k_BoothPrefabPath = k_BarrierPrefabFolder + "/C_ParkingBooth.prefab";

        const float k_BoothHalfW = 1.1f, k_BoothHalfD = 0.9f, k_BoothWall = 0.1f, k_BoothBase = 0.12f, k_BoothTop = 2.45f;
        const float k_BoothSill = 1.05f, k_BoothHead = 1.95f, k_BoothDoorHalf = 0.4f, k_BoothDoorCenter = 0.5f, k_BoothDoorTop = 2.32f;

        class BoothParts
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector3> n = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<List<int>> tris = new List<List<int>>();
            public readonly List<Bounds> colliders = new List<Bounds>();

            public BoothParts(int materials)
            {
                for (int i = 0; i < materials; i++)
                    tris.Add(new List<int>());
            }

            // 축 나란한 상자 — 면마다 미터 UV (옆면 u = 가로 좌표, v = 높이 · 위아래 u = x, v = z).
            // textured: 그 방향 면 하나만 그림 한 장(UV 0 ~ 1, 바깥에서 볼 때 바로 읽히게) — 간판 · 요금표
            public void Box(int mat, Vector3 min, Vector3 max, bool collide, Vector3 textured = default)
            {
                m_Textured = textured;
                var c = new[]
                {
                    new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), new Vector3(min.x, max.y, min.z),
                    new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z),
                };
                Face(mat, c[1], c[0], c[3], c[2], Vector3.back);     // -Z
                Face(mat, c[4], c[5], c[6], c[7], Vector3.forward);  // +Z
                Face(mat, c[0], c[4], c[7], c[3], Vector3.left);     // -X
                Face(mat, c[5], c[1], c[2], c[6], Vector3.right);    // +X
                Face(mat, c[3], c[7], c[6], c[2], Vector3.up);       // +Y
                Face(mat, c[0], c[1], c[5], c[4], Vector3.down);     // -Y
                if (collide)
                    colliders.Add(new Bounds((min + max) * 0.5f, max - min));
            }

            // 네 점 — 바깥에서 볼 때 아래 오른 → 아래 왼 → 위 왼 → 위 오른 (Box 의 꼭짓점 순서). 그래서 삼각형은 0-1-2 · 0-2-3 이 시계 방향(앞면)
            void Face(int mat, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
            {
                int k = v.Count;
                bool picture = m_Textured != Vector3.zero;
                var corner = new[] { new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };   // 아래 오른 · 아래 왼 · 위 왼 · 위 오른
                Vector3[] quad = { a, b, c, d };
                for (int i = 0; i < 4; i++)
                {
                    Vector3 p = quad[i];
                    v.Add(p);
                    n.Add(normal);
                    if (picture) uv.Add(normal == m_Textured ? corner[i] : new Vector2(0.004f, 0.5f));   // 다른 면(두께)은 그림 가장자리 색
                    else if (normal.y != 0f) uv.Add(new Vector2(p.x, p.z));
                    else if (normal.x != 0f) uv.Add(new Vector2(-normal.x * p.z, p.y));
                    else uv.Add(new Vector2(normal.z * p.x, p.y));
                }
                tris[mat].AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 });
            }

            Vector3 m_Textured;
        }

        [MenuItem("MilTrainSimVR/C/주차장 관리 부스 프리팹 만들기", false, 55)]
        public static void MakeParkingBoothPrefab()
        {
            // 11/16 사용자 참고 사진(한국 공영주차장 부스): 흰 몸 · 흰 창틀 · 흰 지붕, 위쪽 둘레 파란 간판 띠, 앞 창 아래 파란 요금표.
            // 그림(Textures/Booth, 스크래치 PowerShell 로 그림 — 글꼴 Fonts/BlackHanSans · 맑은 고딕)은 사용자가 씬에서 키운 크기
            // (배율 x 2.23 · y 1.59 · z 1.59)에서 글자가 바로 보이게 그 비율로 그렸다 — 배율을 많이 바꾸면 글자가 눌린다
            const int panel = 0, white = 1, concrete = 2, signFront = 3, signSide = 4, fee = 5, blue = 6;
            var mats = new[]
            {
                AssetDatabase.LoadAssetAtPath<Material>($"{k_MaterialFolder}/GB_Ext_PanelWhite_1.mat"),
                BoothMat("C_Booth_White", null, new Color(0.95f, 0.94f, 0.91f), 0.35f),
                AssetDatabase.LoadAssetAtPath<Material>($"{k_MaterialFolder}/GB_Ext_Trim_Concrete.mat"),
                BoothMat("C_Booth_SignFront", "Booth_SignFront", Color.white, 0.45f),
                BoothMat("C_Booth_SignSide", "Booth_SignSide", Color.white, 0.45f),
                BoothMat("C_Booth_FeeBoard", "Booth_FeeBoard", Color.white, 0.45f),
                BoothMat("C_Booth_Blue", null, new Color(0.10f, 0.33f, 0.70f), 0.45f),
            };
            var p = new BoothParts(mats.Length);
            float hw = k_BoothHalfW, hd = k_BoothHalfD, t = k_BoothWall, y0 = k_BoothBase, y1 = k_BoothTop;

            // 받침 · 지붕(흰색) + 지붕 둘레 파란 띠
            p.Box(concrete, new Vector3(-hw - 0.05f, 0f, -hd - 0.05f), new Vector3(hw + 0.05f, y0, hd + 0.05f), true);
            p.Box(white, new Vector3(-hw - 0.2f, y1, -hd - 0.2f), new Vector3(hw + 0.2f, y1 + 0.12f, hd + 0.2f), true);
            const float rx = 0.2f, fz = 0.006f;
            p.Box(blue, new Vector3(-hw - rx - fz, y1 + 0.02f, hd + rx), new Vector3(hw + rx + fz, y1 + 0.1f, hd + rx + fz), false);
            p.Box(blue, new Vector3(-hw - rx - fz, y1 + 0.02f, -hd - rx - fz), new Vector3(hw + rx + fz, y1 + 0.1f, -hd - rx), false);
            p.Box(blue, new Vector3(-hw - rx - fz, y1 + 0.02f, -hd - rx), new Vector3(-hw - rx, y1 + 0.1f, hd + rx), false);
            p.Box(blue, new Vector3(hw + rx, y1 + 0.02f, -hd - rx), new Vector3(hw + rx + fz, y1 + 0.1f, hd + rx), false);
            // 간판 띠 — 창 위 벽(1.95 ~ 2.45 m)에 앞 · 양옆. 뒤(출입구 쪽)는 없다 (참고 사진도 앞 · 옆만)
            const float b0 = 2.0f, b1 = 2.42f, bt = 0.012f;
            p.Box(signFront, new Vector3(-hw - bt, b0, hd), new Vector3(hw + bt, b1, hd + bt), false, Vector3.forward);
            p.Box(signSide, new Vector3(-hw - bt, b0, -hd), new Vector3(-hw, b1, hd), false, Vector3.left);
            p.Box(signSide, new Vector3(hw, b0, -hd), new Vector3(hw + bt, b1, hd), false, Vector3.right);
            // 요금표 — 앞 창 아래 가운데
            p.Box(fee, new Vector3(-0.6f, 0.22f, hd), new Vector3(0.6f, 0.95f, hd + bt), false, Vector3.forward);

            // 앞 벽(+Z) — 창 x ±0.8
            const float fw = 0.8f;
            WallAroundOpening(p, panel, true, hd - t, hd, -hw, hw, -fw, fw, y0, y1, k_BoothSill, k_BoothHead);
            // 옆 벽(±X) — 앞 · 뒤 벽 사이, 창 z ±0.45
            const float sw = 0.45f;
            WallAroundOpening(p, panel, false, -hw, -hw + t, -hd + t, hd - t, -sw, sw, y0, y1, k_BoothSill, k_BoothHead);
            WallAroundOpening(p, panel, false, hw - t, hw, -hd + t, hd - t, -sw, sw, y0, y1, k_BoothSill, k_BoothHead);
            // 뒤 벽(-Z) — 출입구(바닥부터)
            float d0 = k_BoothDoorCenter - k_BoothDoorHalf, d1 = k_BoothDoorCenter + k_BoothDoorHalf;
            WallAroundOpening(p, panel, true, -hd, -hd + t, -hw, hw, d0, d1, y0, y1, y0, k_BoothDoorTop);

            // 창틀 · 문틀 (흰색 — 벽 두 면에서 1.5 cm 나온 4 cm 띠, 창턱은 바깥으로 6 cm 더)
            OpeningFrame(p, white, true, hd - t, hd, -fw, fw, k_BoothSill, k_BoothHead, true, 1);
            OpeningFrame(p, white, false, -hw, -hw + t, -sw, sw, k_BoothSill, k_BoothHead, true, -1);
            OpeningFrame(p, white, false, hw - t, hw, -sw, sw, k_BoothSill, k_BoothHead, true, 1);
            OpeningFrame(p, white, true, -hd, -hd + t, d0, d1, y0, k_BoothDoorTop, false, -1);

            var mesh = new Mesh { name = "C_ParkingBooth" };
            mesh.SetVertices(p.v);
            mesh.SetNormals(p.n);
            mesh.SetUVs(0, p.uv);
            mesh.subMeshCount = mats.Length;
            for (int i = 0; i < mats.Length; i++)
                mesh.SetTriangles(p.tris[i], i);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            if (!AssetDatabase.IsValidFolder(k_BoothMeshFolder))
                AssetDatabase.CreateFolder("Assets/C/Models", "ParkingBooth");
            string meshPath = $"{k_BoothMeshFolder}/C_ParkingBooth.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing != null)
            {
                existing.Clear();
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                mesh = existing;
                EditorUtility.SetDirty(mesh);
            }
            else
                AssetDatabase.CreateAsset(mesh, meshPath);

            // 프리팹이 있으면 열어서 고친다 — 새로 덮어쓰면 씬에 놓인 부스의 배율 · 위치 같은 오버라이드가 풀릴 수 있다
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(k_BoothPrefabPath) != null;
            GameObject go = exists ? PrefabUtility.LoadPrefabContents(k_BoothPrefabPath) : new GameObject("C_ParkingBooth");
            if (!go.TryGetComponent(out MeshFilter filter)) filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            if (!go.TryGetComponent(out MeshRenderer renderer)) renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = mats;
            var boxes = new List<BoxCollider>(go.GetComponents<BoxCollider>());
            for (int i = 0; i < p.colliders.Count; i++)
            {
                BoxCollider box = i < boxes.Count ? boxes[i] : go.AddComponent<BoxCollider>();
                box.center = p.colliders[i].center;
                box.size = p.colliders[i].size;
            }
            for (int i = boxes.Count - 1; i >= p.colliders.Count; i--)
                Object.DestroyImmediate(boxes[i]);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic);
            PrefabUtility.SaveAsPrefabAsset(go, k_BoothPrefabPath);
            if (exists) PrefabUtility.UnloadPrefabContents(go);
            else Object.DestroyImmediate(go);
            // 창 셋에 깨지는 유리 (UrbanGreyboxBuilder.WindowGlass.cs, 11/18) — 프리팹을 처음 만들 때도 유리가 있게
            GlazeBoothPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log($"[C] 주차장 관리 부스 프리팹 — {k_BoothPrefabPath} (콜라이더 {p.colliders.Count}개)");
        }

        // 부스 재질 — Materials/Booth. texture 가 있으면 Textures/Booth/<이름>.png 를 끝이 늘어나지 않게(Clamp) 들여온다
        static Material BoothMat(string name, string texture, Color color, float smoothness)
        {
            const string folder = "Assets/C/Materials/Booth";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/C/Materials", "Booth");
            string path = $"{folder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0f);
            if (texture != null)
            {
                string texPath = $"Assets/C/Textures/Booth/{texture}.png";
                if (AssetImporter.GetAtPath(texPath) is TextureImporter ti && (ti.wrapMode != TextureWrapMode.Clamp || ti.anisoLevel < 4))
                {
                    ti.wrapMode = TextureWrapMode.Clamp;
                    ti.anisoLevel = 4;
                    ti.SaveAndReimport();
                }
                mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // 구멍 하나 난 벽: alongX = 벽이 x 를 따라(두께는 z [n0, n1]), 아니면 z 를 따라(두께는 x). 벽 [s0, s1] × [y0, y1], 구멍 [h0, h1] × [hy0, hy1]
        static void WallAroundOpening(BoothParts p, int mat, bool alongX, float n0, float n1, float s0, float s1, float h0, float h1,
            float y0, float y1, float hy0, float hy1)
        {
            void B(float a0, float a1, float b0, float b1)
            {
                if (a1 - a0 < 0.001f || b1 - b0 < 0.001f) return;
                if (alongX) p.Box(mat, new Vector3(a0, b0, n0), new Vector3(a1, b1, n1), true);
                else p.Box(mat, new Vector3(n0, b0, a0), new Vector3(n1, b1, a1), true);
            }
            B(s0, h0, y0, y1);        // 구멍 왼쪽 (바닥 ~ 꼭대기)
            B(h1, s1, y0, y1);        // 구멍 오른쪽
            B(h0, h1, y0, hy0);       // 구멍 아래 (창턱 밑)
            B(h0, h1, hy1, y1);       // 구멍 위
        }

        // 구멍 둘레 틀 — 양 옆 · 위, 창이면 아래(창턱 — out 쪽으로 더 나온다)
        static void OpeningFrame(BoothParts p, int mat, bool alongX, float n0, float n1, float h0, float h1, float hy0, float hy1, bool window, int outSide)
        {
            const float w = 0.04f, proud = 0.015f;
            float m0 = n0 - proud, m1 = n1 + proud;
            void B(float a0, float a1, float b0, float b1, float c0, float c1)
            {
                if (alongX) p.Box(mat, new Vector3(a0, b0, c0), new Vector3(a1, b1, c1), false);
                else p.Box(mat, new Vector3(c0, b0, a0), new Vector3(c1, b1, a1), false);
            }
            B(h0, h0 + w, hy0, hy1, m0, m1);
            B(h1 - w, h1, hy0, hy1, m0, m1);
            B(h0, h1, hy1 - w, hy1, m0, m1);
            if (window)
            {
                float s0 = outSide > 0 ? m0 : m0 - 0.06f, s1 = outSide > 0 ? m1 + 0.06f : m1;
                B(h0 - 0.05f, h1 + 0.05f, hy0 - 0.02f, hy0 + 0.02f, s0, s1);
            }
        }
    }
}
