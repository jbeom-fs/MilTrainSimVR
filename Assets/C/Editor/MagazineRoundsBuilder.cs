using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MilTrainSimVR.UrbanCombat.EditorTools
{
    // 손에 든 탄창(C_Magazine)의 윗부분 (11/12) — 남은 탄 수를 눈으로 보게 한다: 좌상탄 · 우상탄 · 한 발 남은 탄창 · 빈 탄창.
    // 11/11 에는 A 에게 탄창 모델 네 가지를 부탁하려 했는데, A 의 일을 줄이려고 C 에서 만든다 (11/12 사용자).
    // A 의 탄창 모델(09_Magazine)은 윗면이 막힌 상자라서 그 위에 얹는다 — A 파일은 건드리지 않고, A 가 모델을 바꾸면 윗면을 다시 재서 맞춘다:
    //   · 입술(Lips): 윗면 테두리를 따라 낮은 벽을 세우고, 뒤쪽 절반은 양옆이 안으로 말려 맨 위 탄을 누른다. 재질은 A 의 탄창 재질 그대로
    //     (UV 를 전부 A 윗면 가운데 한 점에 두어 같은 색이 나온다)
    //   · 탄(Cartridge): 5.56×45mm 를 돌려 만든 모양 — 탄피(황동) + 탄두(구리). 탄창이 실제보다 굵어서 윗면 길이에 맞게 조금 키운다.
    //     두 줄로 엇갈려 쌓이므로 맨 위 탄은 한쪽에, 그 아래 탄은 반대쪽에 반쯤 잠겨 보인다
    //   · 받침판(Follower): 판 + 한쪽의 둥근 턱(가짜 탄). 마지막 한 발은 턱의 반대쪽에 놓인다
    // 어느 것을 보일지 · 좌우는 실행 중에 RifleMagazine 이 탄 수로 정한다. 총에 꽂힌 탄창(A 의 Part_Magazine)과 방탄복에 꽂힌 탄창은
    // 윗면이 가려져 있어 그대로 둔다. 치수를 바꾸려면 아래 숫자를 고치고 '전투 자산 만들기' 메뉴를 다시 누른다
    public static class MagazineRoundsBuilder
    {
        const string k_MeshFolder = "Assets/C/Models/Magazine";
        const string k_MaterialFolder = "Assets/C/Materials/Magazine";

        // 5.56×45mm 탄의 옆모습 (mm) — x: 탄피 바닥에서 앞으로, y: 반지름. 탄피 길이 44.7 · 전체 57.4 · 탄피 지름 9.6 · 탄두 지름 5.7
        static readonly Vector2[] k_Cartridge =
        {
            new Vector2(0f, 0f), new Vector2(0f, 4.30f), new Vector2(0.35f, 4.80f), new Vector2(1.14f, 4.80f),      // 바닥 · 테(림)
            new Vector2(1.14f, 4.22f), new Vector2(2.40f, 4.22f), new Vector2(3.20f, 4.79f),                       // 갈퀴 홈
            new Vector2(36.50f, 4.50f), new Vector2(39.55f, 3.22f), new Vector2(44.70f, 3.22f),                    // 몸통 · 어깨 · 목
            new Vector2(44.70f, 2.85f), new Vector2(47.50f, 2.84f), new Vector2(50.50f, 2.55f),                    // 여기부터 탄두
            new Vector2(53.50f, 1.90f), new Vector2(55.80f, 1.10f), new Vector2(57.00f, 0.50f), new Vector2(57.40f, 0f),
        };
        const int k_BulletFrom = 10;            // 이 점부터 시작하는 마디는 탄두(구리)
        const float k_CartridgeLength = 57.4f;
        const float k_CartridgeRadius = 4.80f;
        const int k_Around = 20;                // 둘레 조각 수

        // 입술 (mm)
        const float k_Wall = 1.4f;              // 벽 두께 — 바깥면이 탄창 옆면과 이어진다
        const float k_WallHeight = 3.6f;        // 옆 · 뒤 벽 높이 (윗면에서)
        const float k_FrontHeight = 2.2f;       // 앞 벽 높이 — 탄두가 넘어 나간다
        const float k_LipThickness = 1.1f;
        const float k_LipShare = 0.52f;         // 입술이 덮는 길이 (뒤에서부터, 윗면 길이 대비)

        // 탄 자리 (탄 지름 대비) — 두 줄의 좌우 간격과, 맨 위 탄이 윗면 아래로 잠긴 깊이
        const float k_ColumnGap = 0.91f;        // 두 줄 가운데 사이 거리 / 탄 지름 → 엇갈림 높이 = 지름 × √(1 − 이 값²)
        const float k_Sunk = 0.32f;             // 맨 위 탄이 A 윗면 아래로 잠긴 깊이 / 탄 지름

        static readonly Color k_Brass = new Color(0.86f, 0.66f, 0.30f);
        static readonly Color k_Copper = new Color(0.74f, 0.44f, 0.27f);
        static readonly Color k_FollowerColor = new Color(0.30f, 0.42f, 0.25f);   // 국방색 받침판 — 어두운 탄창 몸통 · 황동과 한눈에 구별된다

        // magazine: 만들고 있는 C_Magazine(원점에 돌지 않고 놓임), body: 그 안의 A 탄창 모델. 윗부분을 붙이고 RifleMagazine 에 알려 준다
        public static void Build(GameObject magazine, MeshFilter body, RifleMagazine mag)
        {
            if (!MeasureTop(magazine.transform, body, out float top, out float halfWidth, out float rear, out float front, out float chamfer, out Vector2 uv))
            {
                Debug.LogWarning("[C] 탄창 모델의 윗면을 재지 못해 탄 표시를 붙이지 않는다");
                return;
            }
            EnsureFolder(k_MeshFolder);
            EnsureFolder(k_MaterialFolder);

            // 탄 크기 — 뒤 벽에 테가 닿고 앞 벽과 탄두 사이에 조금 남게 윗면 길이에 맞춘다 (mm → m 는 scale 에 들어 있다)
            float inner = (front - rear) * 1000f - 2f * k_Wall;
            float scale = Mathf.Clamp((inner - 4.6f) / k_CartridgeLength, 1f, 1.3f);
            float radius = k_CartridgeRadius * scale;                       // mm
            float column = radius * k_ColumnGap;                            // 가운데에서 좌우로 (mm)
            float stagger = 2f * radius * Mathf.Sqrt(1f - k_ColumnGap * k_ColumnGap);
            float axis = radius - 2f * radius * k_Sunk;                     // 맨 위 탄의 축 높이 (윗면에서, mm)
            float roundBase = rear * 1000f + k_Wall + 0.8f;                 // 탄피 바닥의 z (mm)

            Material bodyMaterial = body.GetComponent<Renderer>().sharedMaterial;
            Material brass = Metal("C_Brass", k_Brass, 1f, 0.62f);
            Material copper = Metal("C_BulletCopper", k_Copper, 1f, 0.55f);
            Material followerMaterial = Metal("C_MagazineFollower", k_FollowerColor, 0f, 0.35f);

            Transform old = magazine.transform.Find("Rounds");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            Transform holder = new GameObject("Rounds").transform;
            holder.SetParent(magazine.transform, false);
            holder.localPosition = new Vector3(0f, top + axis * 0.001f, 0f);   // 원점 = 맨 위 탄의 축 높이, 좌우 가운데

            // 입술 — A 윗면 기준으로 만든 메시라 축 높이만큼 내려 둔다
            Mesh lips = SaveMesh(LipsMesh(halfWidth * 1000f, rear * 1000f, front * 1000f, chamfer * 1000f, radius, column, axis, uv), "C_MagazineLips");
            Transform lipsObject = Part(holder, "Lips", lips, bodyMaterial);
            lipsObject.localPosition = new Vector3(0f, -axis * 0.001f, 0f);

            Mesh cartridge = SaveMesh(CartridgeMesh(scale), "C_Cartridge");
            Transform topRound = Part(holder, "RoundTop", cartridge, brass, copper);
            topRound.localPosition = new Vector3(column * 0.001f, 0f, roundBase * 0.001f);
            Transform nextRound = Part(holder, "RoundNext", cartridge, brass, copper);
            nextRound.localPosition = new Vector3(-column * 0.001f, -stagger * 0.001f, roundBase * 0.001f);

            Mesh followerMesh = SaveMesh(FollowerMesh(halfWidth * 1000f, rear * 1000f, front * 1000f, radius, column, axis, roundBase), "C_MagazineFollower");
            Transform follower = Part(holder, "Follower", followerMesh, followerMaterial);

            var so = new SerializedObject(mag);
            so.FindProperty("m_TopRound").objectReferenceValue = topRound;
            so.FindProperty("m_NextRound").objectReferenceValue = nextRound;
            so.FindProperty("m_Follower").objectReferenceValue = follower;
            so.FindProperty("m_ColumnOffset").floatValue = column * 0.001f;
            so.FindProperty("m_Stagger").floatValue = stagger * 0.001f;
            so.ApplyModifiedPropertiesWithoutUndo();
            // 프리팹에는 가득 찬 모습으로 둔다 (실행하면 Awake 가 다시 맞춘다)
            mag.Rounds = mag.Capacity;
        }

        // A 모델의 윗면 — 탄창 기준(m): 높이 · 반폭 · 뒤끝 · 앞끝 · 모서리를 깎은 크기, 그리고 윗면 가운데의 UV(입술이 같은 색을 내게)
        static bool MeasureTop(Transform root, MeshFilter body, out float top, out float halfWidth, out float rear, out float front, out float chamfer, out Vector2 uv)
        {
            top = halfWidth = rear = front = chamfer = 0f;
            uv = Vector2.zero;
            Mesh mesh = body != null ? body.sharedMesh : null;
            if (mesh == null)
                return false;
            Vector3[] vertices = mesh.vertices;
            var points = new Vector3[vertices.Length];
            top = float.MinValue;
            for (int i = 0; i < vertices.Length; i++)
            {
                points[i] = root.InverseTransformPoint(body.transform.TransformPoint(vertices[i]));
                top = Mathf.Max(top, points[i].y);
            }
            const float flat = 0.0003f;   // 꼭대기에서 이 안이면 윗면
            rear = float.MaxValue;
            front = float.MinValue;
            foreach (Vector3 p in points)
            {
                if (p.y < top - flat)
                    continue;
                halfWidth = Mathf.Max(halfWidth, Mathf.Abs(p.x));
                rear = Mathf.Min(rear, p.z);
                front = Mathf.Max(front, p.z);
            }
            // 뒤끝 줄의 꼭짓점이 옆면보다 안쪽에 있으면 그만큼 모서리가 깎인 것
            float endWidth = 0f;
            foreach (Vector3 p in points)
            {
                if (p.y >= top - flat && p.z < rear + flat)
                    endWidth = Mathf.Max(endWidth, Mathf.Abs(p.x));
            }
            chamfer = Mathf.Clamp(halfWidth - endWidth, 0f, halfWidth * 0.5f);

            Vector2[] uvs = mesh.uv;
            int[] triangles = mesh.triangles;
            float area = 0f;
            Vector2 sum = Vector2.zero;
            for (int t = 0; t < triangles.Length && uvs.Length == vertices.Length; t += 3)
            {
                int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                if (points[a].y < top - flat || points[b].y < top - flat || points[c].y < top - flat)
                    continue;
                float w = Vector3.Cross(points[b] - points[a], points[c] - points[a]).magnitude;
                area += w;
                sum += (uvs[a] + uvs[b] + uvs[c]) / 3f * w;
            }
            if (area > 0f)
                uv = sum / area;
            return front - rear > 0.03f && halfWidth > 0.008f;
        }

        // ---------------------------------------------------------------- 메시 (치수는 mm, 저장은 m)

        // 탄 — 원점 = 탄피 바닥 가운데, 앞(+Z) = 탄두. 서브메시 0 = 탄피, 1 = 탄두
        static Shape CartridgeMesh(float scale)
        {
            var shape = new Shape(2);
            shape.Lathe(Vector3.zero, k_Cartridge, scale, k_Around, i => i >= k_BulletFrom ? 1 : 0);
            return shape;
        }

        // 입술 — 원점 = A 윗면 가운데 높이(x = 0, y = 0), z 는 탄창 기준 그대로
        static Shape LipsMesh(float halfWidth, float rear, float front, float chamfer, float radius, float column, float axis, Vector2 uv)
        {
            var shape = new Shape(1) { uv = uv };
            float x = halfWidth - k_Wall * 0.5f;                                  // 옆벽 가운데 줄
            float z0 = rear + chamfer, z1 = front - chamfer;                       // 옆벽이 곧은 구간
            float lipEnd = rear + (front - rear) * k_LipShare;                     // 입술이 끝나는 곳
            float taperEnd = Mathf.Min(lipEnd + 14f, z1);                          // 그 앞으로 벽이 앞 벽 높이까지 낮아진다
            // 입술 끝 — 맨 위 탄의 바깥 위 45° 자리를 누른다. 그 위로 탄이 조금 올라와 보인다
            float tipX = column + radius * 0.7071f + 0.25f;
            float tipY = axis + radius * 0.7071f + k_LipThickness * 0.5f + 0.15f;
            foreach (float side in new[] { 1f, -1f })
            {
                // 단면의 가운데 줄 (안쪽으로 말린다) — 벽 아래 → 벽 위 → 말림 → 끝
                var curl = new List<Vector2>
                {
                    new Vector2(x, 0f), new Vector2(x, k_WallHeight),
                    new Vector2(Mathf.Lerp(x, tipX, 0.18f), Mathf.Lerp(k_WallHeight, tipY, 0.52f)),
                    new Vector2(Mathf.Lerp(x, tipX, 0.55f), Mathf.Lerp(k_WallHeight, tipY, 0.88f)),
                    new Vector2(tipX, tipY),
                };
                var wall = new List<Vector2> { new Vector2(x, 0f), new Vector2(x, k_WallHeight) };
                var low = new List<Vector2> { new Vector2(x, 0f), new Vector2(x, k_FrontHeight) };
                for (int i = 0; i < curl.Count; i++)
                    curl[i] = new Vector2(curl[i].x * side, curl[i].y);
                wall[0] = new Vector2(wall[0].x * side, wall[0].y);
                wall[1] = new Vector2(wall[1].x * side, wall[1].y);
                low[0] = new Vector2(low[0].x * side, low[0].y);
                low[1] = new Vector2(low[1].x * side, low[1].y);
                // 뒤 절반: 말린 입술 (벽 두께로 올라가 입술 두께로 얇아진다) / 그 앞: 벽만, 앞 벽 높이까지 낮아졌다가 그대로 간다
                shape.Strip(curl, z0, curl, lipEnd, side, k_Wall, k_LipThickness, true, true);
                shape.Strip(wall, lipEnd, low, taperEnd, side, k_Wall, k_Wall, false, false);
                if (taperEnd < z1)
                    shape.Strip(low, taperEnd, low, z1, side, k_Wall, k_Wall, false, false);
                // 깎인 모서리 — 옆벽 끝과 뒤 · 앞 벽 사이를 비스듬한 면으로 잇는다
                if (chamfer > 0.05f)
                {
                    shape.Corner(new Vector2(halfWidth * side, z0), new Vector2((halfWidth - chamfer) * side, rear), new Vector2((halfWidth - chamfer) * side, z0), k_WallHeight);
                    shape.Corner(new Vector2(halfWidth * side, z1), new Vector2((halfWidth - chamfer) * side, front), new Vector2((halfWidth - chamfer) * side, z1), k_FrontHeight);
                }
            }
            // 뒤 벽 · 앞 벽
            shape.Box(new Vector3(-(halfWidth - chamfer), 0f, rear), new Vector3(halfWidth - chamfer, k_WallHeight, rear + k_Wall));
            shape.Box(new Vector3(-(halfWidth - chamfer), 0f, front - k_Wall), new Vector3(halfWidth - chamfer, k_FrontHeight, front));
            return shape;
        }

        // 받침판 — 원점 = 맨 위 탄의 축 높이(비었을 때의 자리), z 는 탄창 기준 그대로. 판 + 오른쪽(+X)의 둥근 턱
        static Shape FollowerMesh(float halfWidth, float rear, float front, float radius, float column, float axis, float roundBase)
        {
            var shape = new Shape(1);
            float inside = halfWidth - k_Wall - 0.25f;
            // 판 — 윗면이 A 윗면보다 0.8mm 위 (비었을 때). 한 발 남았을 때는 통째로 내려가 A 윗면 아래로 숨는다
            float plateTop = 0.8f - axis;
            shape.Box(new Vector3(-inside, plateTop - 2.5f, rear + k_Wall + 0.25f), new Vector3(inside, plateTop, front - k_Wall - 0.25f));
            // 턱 — 탄 굵기의 둥근 막대(앞이 둥글다). 탄의 3/4 길이
            float length = k_CartridgeLength * (radius / k_CartridgeRadius) * 0.75f;
            var hump = new[]
            {
                new Vector2(0f, 0f), new Vector2(0f, radius - 0.8f), new Vector2(0.8f, radius), new Vector2(length - radius, radius),
                new Vector2(length - radius * 0.62f, radius * 0.92f), new Vector2(length - radius * 0.29f, radius * 0.71f),
                new Vector2(length - radius * 0.08f, radius * 0.38f), new Vector2(length, 0f),
            };
            shape.Lathe(new Vector3(column, 0f, roundBase), hump, 1f, k_Around, i => 0);
            return shape;
        }

        // 메시 한 벌을 쌓는 통 — 치수는 mm 로 받고 m 로 바꿔 넣는다. 삼각형마다 겉(법선 쪽)이 보이게 감는 방향을 스스로 맞춘다
        class Shape
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<Vector3> normals = new List<Vector3>();
            public readonly List<Vector2> uvs = new List<Vector2>();
            public readonly List<List<int>> triangles = new List<List<int>>();
            public Vector2 uv;

            const float k_Smooth = 35f;   // 이보다 덜 꺾인 이음매는 매끄럽게 잇는다 (도)

            public Shape(int subMeshes)
            {
                for (int i = 0; i < subMeshes; i++)
                    triangles.Add(new List<int>());
            }

            int Add(Vector3 p, Vector3 n)
            {
                vertices.Add(p * 0.001f);
                normals.Add(n.normalized);
                uvs.Add(uv);
                return vertices.Count - 1;
            }

            public void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc, int sub = 0)
            {
                Vector3 face = Vector3.Cross(b - a, c - a);
                if (face.sqrMagnitude < 1e-10f)
                    return;   // 넓이가 없다 (회전체의 끝점)
                int ia = Add(a, na), ib = Add(b, nb), ic = Add(c, nc);
                // 유니티는 꼭짓점이 시계 방향으로 보이는 쪽이 겉이다 — Cross(b − a, c − a) 가 겉을 본다
                bool keep = Vector3.Dot(face, na + nb + nc) >= 0f;
                triangles[sub].Add(ia);
                triangles[sub].Add(keep ? ib : ic);
                triangles[sub].Add(keep ? ic : ib);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd, int sub = 0)
            {
                Triangle(a, b, c, na, nb, nc, sub);
                Triangle(a, c, d, na, nc, nd, sub);
            }

            // 평평한 면 — outward 쪽이 겉
            public void Flat(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, int sub = 0)
            {
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-10f)
                    n = Vector3.Cross(c - a, d - a);
                if (Vector3.Dot(n, outward) < 0f)
                    n = -n;
                Quad(a, b, c, d, n, n, n, n, sub);
            }

            // 상자 — 밑면은 안 만든다 (A 윗면에 얹히거나 그 아래로 숨는다)
            public void Box(Vector3 min, Vector3 max, int sub = 0)
            {
                Vector3 a = new Vector3(min.x, min.y, min.z), b = new Vector3(max.x, min.y, min.z), c = new Vector3(max.x, min.y, max.z), d = new Vector3(min.x, min.y, max.z);
                Vector3 e = new Vector3(min.x, max.y, min.z), f = new Vector3(max.x, max.y, min.z), g = new Vector3(max.x, max.y, max.z), h = new Vector3(min.x, max.y, max.z);
                Flat(e, f, g, h, Vector3.up, sub);
                Flat(a, b, f, e, Vector3.back, sub);
                Flat(d, c, g, h, Vector3.forward, sub);
                Flat(a, d, h, e, Vector3.left, sub);
                Flat(b, c, g, f, Vector3.right, sub);
            }

            // 깎인 모서리를 메우는 세모 기둥 — 바닥(x, z) 세 점: 옆벽 끝 바깥 · 끝벽 바깥 · 안쪽 구석. 비스듬한 바깥면과 윗면만 만든다
            public void Corner(Vector2 side, Vector2 end, Vector2 inner, float height)
            {
                Vector3 s0 = new Vector3(side.x, 0f, side.y), e0 = new Vector3(end.x, 0f, end.y), i0 = new Vector3(inner.x, 0f, inner.y);
                Vector3 up = Vector3.up * height;
                Vector3 outward = (s0 + e0) * 0.5f - i0;
                Flat(s0, e0, e0 + up, s0 + up, outward, 0);
                Triangle(s0 + up, e0 + up, i0 + up, Vector3.up, Vector3.up, Vector3.up, 0);
            }

            // 회전체 — 축은 origin 에서 +Z. profile 의 x = 축 따라, y = 반지름 (unit 배). 마디마다 subOf(마디 번호) 서브메시에 넣는다
            public void Lathe(Vector3 origin, IList<Vector2> profile, float unit, int around, System.Func<int, int> subOf)
            {
                int count = profile.Count - 1;
                var outward = new Vector2[count];   // 마디의 겉 방향 (x = 축 쪽, y = 반지름 쪽)
                for (int i = 0; i < count; i++)
                {
                    Vector2 d = profile[i + 1] - profile[i];
                    outward[i] = new Vector2(-d.y, d.x).normalized;
                }
                for (int i = 0; i < count; i++)
                {
                    Vector2 n0 = i > 0 && Vector2.Angle(outward[i - 1], outward[i]) < k_Smooth ? (outward[i - 1] + outward[i]).normalized : outward[i];
                    Vector2 n1 = i < count - 1 && Vector2.Angle(outward[i], outward[i + 1]) < k_Smooth ? (outward[i] + outward[i + 1]).normalized : outward[i];
                    for (int k = 0; k < around; k++)
                    {
                        float a0 = k * Mathf.PI * 2f / around, a1 = (k + 1) * Mathf.PI * 2f / around;
                        Vector3 r0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f), r1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f);
                        Vector3 p00 = origin + (r0 * profile[i].y + Vector3.forward * profile[i].x) * unit;
                        Vector3 p01 = origin + (r1 * profile[i].y + Vector3.forward * profile[i].x) * unit;
                        Vector3 p10 = origin + (r0 * profile[i + 1].y + Vector3.forward * profile[i + 1].x) * unit;
                        Vector3 p11 = origin + (r1 * profile[i + 1].y + Vector3.forward * profile[i + 1].x) * unit;
                        Quad(p00, p01, p11, p10,
                            r0 * n0.y + Vector3.forward * n0.x, r1 * n0.y + Vector3.forward * n0.x,
                            r1 * n1.y + Vector3.forward * n1.x, r0 * n1.y + Vector3.forward * n1.x, subOf(i));
                    }
                }
            }

            // 두께가 있는 띠 — 단면의 가운데 줄(x · y)을 z0 에서 z1 까지 민다 (두 끝의 단면이 달라도 된다, 점 수는 같아야 한다).
            // side: +1 = 오른쪽 벽(겉이 +X 쪽), −1 = 왼쪽 벽. 두께는 줄의 첫 점에서 thickness0, 끝 점에서 thickness1.
            // 겉 · 속 면은 줄을 따라 매끄럽게, 줄 끝의 면과 두 마구리(cap0 · cap1)는 평평하게. 줄의 첫 점 쪽 밑면은 안 만든다
            public void Strip(IList<Vector2> line0, float z0, IList<Vector2> line1, float z1, float side, float thickness0, float thickness1, bool cap0, bool cap1)
            {
                int count = line0.Count;
                var o0 = new Vector3[count];
                var i0 = new Vector3[count];
                var o1 = new Vector3[count];
                var i1 = new Vector3[count];
                var n0 = new Vector3[count];
                var n1 = new Vector3[count];
                for (int j = 0; j < count; j++)
                {
                    float half = Mathf.Lerp(thickness0, thickness1, count > 1 ? j / (count - 1f) : 0f) * 0.5f;
                    n0[j] = Outward(line0, j, side);
                    n1[j] = Outward(line1, j, side);
                    o0[j] = new Vector3(line0[j].x, line0[j].y, z0) + n0[j] * half;
                    i0[j] = new Vector3(line0[j].x, line0[j].y, z0) - n0[j] * half;
                    o1[j] = new Vector3(line1[j].x, line1[j].y, z1) + n1[j] * half;
                    i1[j] = new Vector3(line1[j].x, line1[j].y, z1) - n1[j] * half;
                }
                for (int j = 0; j < count - 1; j++)
                {
                    Quad(o0[j], o0[j + 1], o1[j + 1], o1[j], n0[j], n0[j + 1], n1[j + 1], n1[j]);
                    Quad(i0[j], i0[j + 1], i1[j + 1], i1[j], -n0[j], -n0[j + 1], -n1[j + 1], -n1[j]);
                    if (cap0)
                        Flat(o0[j], o0[j + 1], i0[j + 1], i0[j], z0 < z1 ? Vector3.back : Vector3.forward);
                    if (cap1)
                        Flat(o1[j], o1[j + 1], i1[j + 1], i1[j], z0 < z1 ? Vector3.forward : Vector3.back);
                }
                // 줄 끝의 면 (벽 꼭대기 · 입술 끝)
                int last = count - 1;
                Vector3 along = new Vector3(line0[last].x - line0[last - 1].x, line0[last].y - line0[last - 1].y, 0f);
                Flat(o0[last], i0[last], i1[last], o1[last], along);
            }

            // 줄의 j 번째 점에서 겉 방향 — 앞뒤 점을 이은 방향에 수직
            static Vector3 Outward(IList<Vector2> line, int j, float side)
            {
                Vector2 t = (line[Mathf.Min(j + 1, line.Count - 1)] - line[Mathf.Max(j - 1, 0)]).normalized;
                return new Vector3(t.y, -t.x, 0f) * side;
            }
        }

        // ---------------------------------------------------------------- 자산

        // 메시 자산으로 저장 — 이미 있으면 그 자산을 고쳐 쓴다(프리팹이 가리키는 GUID 가 그대로다)
        static Mesh SaveMesh(Shape shape, string name)
        {
            string path = $"{k_MeshFolder}/{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool created = mesh == null;
            if (created)
                mesh = new Mesh { name = name };
            mesh.Clear();
            mesh.SetVertices(shape.vertices);
            mesh.SetNormals(shape.normals);
            mesh.SetUVs(0, shape.uvs);
            // UV 가 한 점이라 탄젠트를 계산할 수 없다 — 법선에 수직인 아무 방향이나 넣어 둔다 (A 재질의 노멀맵이 그 점에서 평평해 문제없다)
            var tangents = new List<Vector4>(shape.normals.Count);
            foreach (Vector3 n in shape.normals)
            {
                Vector3 t = Vector3.Cross(n, Mathf.Abs(n.z) < 0.9f ? Vector3.forward : Vector3.right).normalized;
                tangents.Add(new Vector4(t.x, t.y, t.z, 1f));
            }
            mesh.SetTangents(tangents);
            mesh.subMeshCount = shape.triangles.Count;
            for (int i = 0; i < shape.triangles.Count; i++)
                mesh.SetTriangles(shape.triangles[i], i);
            mesh.RecalculateBounds();
            if (created)
                AssetDatabase.CreateAsset(mesh, path);
            else
                EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // 금속 · 플라스틱 재질 (URP Lit) — 메뉴를 다시 누르면 색도 다시 맞춘다
        static Material Metal(string name, Color color, float metallic, float smoothness)
        {
            string path = $"{k_MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = mat == null;
            if (created)
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            BaseShaderGUI.SetMaterialKeywords(mat);
            if (created)
                AssetDatabase.CreateAsset(mat, path);
            else
                EditorUtility.SetDirty(mat);
            return mat;
        }

        static Transform Part(Transform parent, string name, Mesh mesh, params Material[] materials)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            return go.transform;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
