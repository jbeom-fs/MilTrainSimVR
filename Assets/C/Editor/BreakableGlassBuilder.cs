using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MilTrainSimVR.UrbanCombat.EditorTools
{
    // 백화점 에스컬레이터 통유리 난간을 깨지게 (11/18 사용자 — 카스2 처럼 맞은 곳부터 부서지게, 쨍그랑 소리).
    //   · 파편 · 소리 프리팹 Prefabs/FX/C_GlassShards: 메시 파편(작은 세모) 파티클 + 소리 4개(Audio/Glass/C_GlassBreak_1~4)
    //   · 씬의 백화점 유리(지도 생성기가 합친 GB_Dept_Glass 메시)를 유리 장마다 약 1.2 m 조각으로 나눠 BreakableGlass 를 붙인다.
    //     1.2 m 마다 평면으로 자르고 걸친 삼각형은 잘라 나눈다(위치 · 법선 · UV 보간). 판 사이엔 6 mm 틈과 유리 옆면 —
    //     실제 에스컬레이터처럼 판이 나뉘어 보인다 (11/18 사용자). 맞으면 그 판이 통째로 부서진다 (BreakableGlass)
    //     원래 합친 메시는 끈다 (지우지 않는다). 다시 누르면 조각을 지우고 다시 나눈다
    //   · 지도 만들기(UrbanGreyboxBuilder.Build) 끝에서도 SplitDeptGlass 를 부른다 — 지도를 다시 지어도 유리가 다시 깨지게 나뉜다
    // 씬은 저장하지 않는다
    static class BreakableGlassBuilder
    {
        const string k_GlassMaterial = "Assets/C/Materials/Greybox/GB_Dept_Glass.mat";
        const string k_FxFolder = "Assets/C/Prefabs/FX";
        const string k_Prefab = k_FxFolder + "/C_GlassShards.prefab";
        const string k_ShardMesh = k_FxFolder + "/C_GlassShard_Mesh.asset";
        const string k_ShardMaterial = k_FxFolder + "/C_GlassShard.mat";
        const string k_CrackedMaterial = k_FxFolder + "/C_GlassCracked.mat";
        const string k_ClipFolder = "Assets/C/Audio/Glass";
        const string k_ChunkParent = "DeptGlass_Breakable";
        const float k_ChunkLength = 1.2f;
        const float k_Gap = 0.006f;   // 판 사이 틈 (6 mm)

        [MenuItem("MilTrainSimVR/C/백화점 에스컬레이터 유리 깨지게 (카스2식 · 파편 · 쨍그랑)", false, 59)]
        static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[C] 깨지는 유리 — Play 중에는 하지 않는다");
                return;
            }
            BuildShardsPrefab();
            BuildCrackedMaterial();
            GameObject root = GameObject.Find("C_Greybox");
            if (root == null)
            {
                Debug.LogWarning("[C] 깨지는 유리 — C_Greybox 가 없다 (C_UrbanBlock 을 열고 누를 것)");
                return;
            }
            int chunks = SplitDeptGlass(root.transform);
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log($"[C] 백화점 에스컬레이터 유리를 깨지게 했다 — 조각 {chunks}개");
        }

        // 구멍 둘레 금 간 유리 — 난간 유리를 하얗고 덜 투명하게 (카스2 의 하얀 금 띠처럼)
        static Material BuildCrackedMaterial()
        {
            var glass = AssetDatabase.LoadAssetAtPath<Material>(k_GlassMaterial);
            var material = AssetDatabase.LoadAssetAtPath<Material>(k_CrackedMaterial);
            if (material == null)
            {
                material = new Material(glass) { name = "C_GlassCracked" };
                AssetDatabase.CreateAsset(material, k_CrackedMaterial);
            }
            else
                material.CopyPropertiesFromMaterial(glass);
            material.SetColor("_BaseColor", new Color(0.86f, 0.93f, 0.94f, 0.8f));
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.55f);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        // ---------------------------------------------------------------- 파편 · 소리 프리팹

        static GlassShards BuildShardsPrefab()
        {
            var glass = AssetDatabase.LoadAssetAtPath<Material>(k_GlassMaterial);
            // 파편: 들쭉날쭉한 세모 (앞뒤 두 면) — 크기 1 기준, 파티클 크기(1.2 ~ 4.5 cm)로 줄인다
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(k_ShardMesh);
            if (mesh == null)
            {
                mesh = new Mesh { name = "C_GlassShard" };
                AssetDatabase.CreateAsset(mesh, k_ShardMesh);
            }
            var v = new[] { new Vector3(0f, 0.62f, 0f), new Vector3(0.52f, -0.33f, 0f), new Vector3(-0.47f, -0.41f, 0f) };
            mesh.Clear();
            mesh.vertices = new[] { v[0], v[1], v[2], v[0], v[1], v[2] };
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.forward, Vector3.forward, Vector3.forward };
            mesh.triangles = new[] { 0, 1, 2, 3, 5, 4 };
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);

            // 파편 재질: 난간 유리와 같은 색, 조금 덜 투명하고 더 매끈하게 — 작은 조각이 반짝여 보이게
            var material = AssetDatabase.LoadAssetAtPath<Material>(k_ShardMaterial);
            if (material == null)
            {
                material = new Material(glass) { name = "C_GlassShard" };
                AssetDatabase.CreateAsset(material, k_ShardMaterial);
            }
            else
                material.CopyPropertiesFromMaterial(glass);
            Color c = glass.GetColor("_BaseColor");
            material.SetColor("_BaseColor", new Color(c.r, c.g, c.b, 0.78f));
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.95f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);

            var clips = AssetDatabase.FindAssets("t:AudioClip C_GlassBreak", new[] { k_ClipFolder })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).Select(AssetDatabase.LoadAssetAtPath<AudioClip>).ToArray();

            var root = new GameObject("C_GlassShards");
            var ps = root.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = 2f;
            main.startSpeed = 0f;
            main.startSize = 0.03f;
            main.startRotation3D = true;
            main.gravityModifier = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 3000;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var collision = ps.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.collidesWith = Gunfire.ShotMask;   // 몸만 막는 보이지 않는 상자(Ignore Raycast)엔 안 튕긴다
            collision.quality = ParticleSystemCollisionQuality.Medium;
            collision.dampen = 0.6f;
            collision.bounce = 0.2f;
            collision.lifetimeLoss = 0f;
            collision.radiusScale = 0.5f;
            collision.enableDynamicColliders = false;
            // 끝 15% 동안 작아지며 사라진다 (Lit 재질이라 투명도는 못 바꾼다)
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.85f, 1f), new Keyframe(1f, 0f)));
            var renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = mesh;
            renderer.sharedMaterial = material;
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var shards = root.AddComponent<GlassShards>();
            var so = new SerializedObject(shards);
            so.FindProperty("m_Particles").objectReferenceValue = ps;
            var list = so.FindProperty("m_Clips");
            list.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, k_Prefab);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log($"[C] 유리 파편 프리팹 — {k_Prefab} (소리 {clips.Length}개)");
            return saved.GetComponent<GlassShards>();
        }

        // ---------------------------------------------------------------- 유리 나누기

        // greybox 아래 GB_Dept_Glass 를 쓰는 합친 메시마다: 유리 장(이어진 덩어리)을 찾고, 장마다 약 1.2 m 조각으로 나눈다. 만든 조각 수
        public static int SplitDeptGlass(Transform greybox)
        {
            var glass = AssetDatabase.LoadAssetAtPath<Material>(k_GlassMaterial);
            var shardsPrefab = AssetDatabase.LoadAssetAtPath<GlassShards>(k_Prefab);
            if (glass == null || greybox == null)
                return 0;
            int made = 0;
            // 원래 합친 유리만 먼저 모은다 — 지난번 판(DeptGlass_Breakable 아래)은 아래에서 지우니 훑는 중에 건드리면 안 된다
            var sources = greybox.GetComponentsInChildren<MeshRenderer>(true)
                .Where(r => r.sharedMaterial == glass && r.GetComponent<BreakableGlass>() == null && (r.transform.parent == null || r.transform.parent.name != k_ChunkParent))
                .ToList();
            foreach (MeshRenderer source in sources)
            {
                if (source == null)
                    continue;
                MeshFilter mf = source.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null)
                    continue;
                Transform parent = source.transform.parent;
                Transform old = parent.Find(k_ChunkParent);
                if (old != null)
                    Undo.DestroyObjectImmediate(old.gameObject);
                var holder = new GameObject(k_ChunkParent).transform;
                Undo.RegisterCreatedObjectUndo(holder.gameObject, "깨지는 유리");
                holder.SetParent(parent, false);
                made += Split(mf, source, holder, shardsPrefab);
                Undo.RecordObject(source.gameObject, "깨지는 유리");
                source.gameObject.SetActive(false);   // 원래 합친 유리는 끈다 (조각이 같은 모양으로 대신 그린다)
            }
            return made;
        }


        struct GV
        {
            public Vector3 p, n;
            public Vector2 uv;
            public static GV Lerp(GV a, GV b, float t) => new GV { p = Vector3.Lerp(a.p, b.p, t), n = Vector3.Lerp(a.n, b.n, t), uv = Vector2.Lerp(a.uv, b.uv, t) };
        }

        // 다각형을 along 축 값 ≥ lo (keepAbove) 또는 ≤ hi 쪽만 남게 자른다 (Sutherland–Hodgman 한 평면). 꼭짓점 차례(감는 쪽)는 그대로
        static List<GV> ClipAlong(List<GV> poly, Vector3 along, float limit, bool keepAbove)
        {
            var result = new List<GV>(poly.Count + 2);
            for (int k = 0; k < poly.Count; k++)
            {
                GV a = poly[k], b = poly[(k + 1) % poly.Count];
                float da = (Vector3.Dot(a.p, along) - limit) * (keepAbove ? 1f : -1f);
                float db = (Vector3.Dot(b.p, along) - limit) * (keepAbove ? 1f : -1f);
                if (da >= 0f)
                    result.Add(a);
                if ((da >= 0f) != (db >= 0f))
                    result.Add(GV.Lerp(a, b, da / (da - db)));
            }
            return result;
        }

        static int Split(MeshFilter mf, MeshRenderer source, Transform holder, GlassShards shardsPrefab)
        {
            Mesh mesh = mf.sharedMesh;
            Transform tr = mf.transform;
            Vector3[] lv = mesh.vertices;
            Vector3[] ln = mesh.normals;
            Vector2[] luv = mesh.uv;
            int[] tris = mesh.triangles;
            var gv = new GV[lv.Length];
            for (int i = 0; i < lv.Length; i++)
                gv[i] = new GV
                {
                    p = tr.TransformPoint(lv[i]),
                    n = ln.Length == lv.Length ? tr.TransformDirection(ln[i]) : Vector3.zero,
                    uv = luv.Length == lv.Length ? luv[i] : Vector2.zero,
                };

            // 같은 자리 꼭짓점을 묶어 이어진 덩어리(유리 장)를 찾는다
            var weld = new Dictionary<Vector3Int, int>();
            var id = new int[lv.Length];
            for (int i = 0; i < lv.Length; i++)
            {
                var key = Vector3Int.RoundToInt(gv[i].p * 1000f);
                if (!weld.TryGetValue(key, out int w))
                    weld[key] = w = weld.Count;
                id[i] = w;
            }
            var uf = Enumerable.Range(0, weld.Count).ToArray();
            int Find(int x) { while (uf[x] != x) x = uf[x] = uf[uf[x]]; return x; }
            for (int t = 0; t < tris.Length; t += 3)
            {
                int a = Find(id[tris[t]]);
                uf[Find(id[tris[t + 1]])] = a;
                uf[Find(id[tris[t + 2]])] = a;
            }
            var panels = new Dictionary<int, List<int>>();
            for (int t = 0; t < tris.Length; t += 3)
            {
                int r = Find(id[tris[t]]);
                if (!panels.TryGetValue(r, out var list))
                    panels[r] = list = new List<int>();
                list.Add(t);
            }

            int made = 0, panelIndex = 0;
            foreach (List<int> panel in panels.Values)
            {
                // 두께 방향: 가장 넓은 수평 법선 면
                Vector3 normal = Vector3.zero;
                float best = 0f;
                foreach (int t in panel)
                {
                    Vector3 n = Vector3.Cross(gv[tris[t + 1]].p - gv[tris[t]].p, gv[tris[t + 2]].p - gv[tris[t]].p);
                    float area = n.magnitude;
                    if (area > best && Mathf.Abs(n.normalized.y) < 0.2f)
                    {
                        best = area;
                        normal = n.normalized;
                    }
                }
                if (best <= 0f)
                    continue;
                normal = new Vector3(normal.x, 0f, normal.z).normalized;
                Vector3 along = Vector3.Cross(Vector3.up, normal).normalized;   // 로컬 x (오른쪽 = 위 × 앞)

                // 앞면(두께 +쪽) · 뒷면 평면 거리 → 가운데 · 두께. 유리를 따라 처음 · 끝
                float front = float.MinValue, back = float.MaxValue, uMin = float.MaxValue, uMax = float.MinValue;
                foreach (int t in panel)
                    for (int k = 0; k < 3; k++)
                    {
                        Vector3 p = gv[tris[t + k]].p;
                        float d = Vector3.Dot(p, normal), u = Vector3.Dot(p, along);
                        front = Mathf.Max(front, d);
                        back = Mathf.Min(back, d);
                        uMin = Mathf.Min(uMin, u);
                        uMax = Mathf.Max(uMax, u);
                    }
                float mid = (front + back) * 0.5f, thickness = front - back;

                // 약 1.2 m 마다 평면으로 자른다 — 걸친 삼각형은 잘라서 양쪽 조각에 나눈다 (위치 · 법선 · UV 는 자른 자리에서 보간)
                int count = Mathf.Max(1, Mathf.RoundToInt((uMax - uMin) / k_ChunkLength));
                for (int k = 0; k < count; k++)
                {
                    // 판 사이 틈 k_Gap — 실제 에스컬레이터처럼 판이 나뉘어 보이게 (11/18 사용자). 장의 맨 끝(반원)은 그대로
                    float u0 = Mathf.Lerp(uMin, uMax, k / (float)count) + (k > 0 ? k_Gap * 0.5f : 0f);
                    float u1 = Mathf.Lerp(uMin, uMax, (k + 1) / (float)count) - (k < count - 1 ? k_Gap * 0.5f : 0f);
                    var pieces = new List<List<GV>>();
                    var isFront = new List<bool>();
                    foreach (int t in panel)
                    {
                        var poly = new List<GV> { gv[tris[t]], gv[tris[t + 1]], gv[tris[t + 2]] };
                        float lo = poly.Min(g => Vector3.Dot(g.p, along)), hi = poly.Max(g => Vector3.Dot(g.p, along));
                        if (hi < u0 - 1e-5f || lo > u1 + 1e-5f)
                            continue;
                        poly = ClipAlong(ClipAlong(poly, along, u0, true), along, u1, false);
                        if (poly.Count < 3)
                            continue;
                        Vector3 fn = Vector3.Cross(poly[1].p - poly[0].p, poly[2].p - poly[0].p);
                        float areaSum = 0f;
                        for (int q = 1; q + 1 < poly.Count; q++)
                            areaSum += Vector3.Cross(poly[q].p - poly[0].p, poly[q + 1].p - poly[0].p).magnitude;
                        if (areaSum < 1e-8f)
                            continue;
                        pieces.Add(poly);
                        Vector3 tn = Vector3.Cross(gv[tris[t + 1]].p - gv[tris[t]].p, gv[tris[t + 2]].p - gv[tris[t]].p);
                        isFront.Add(tn.sqrMagnitude > 1e-12f && Vector3.Dot(tn.normalized, normal) > 0.95f);
                    }
                    if (pieces.Count == 0)
                        continue;
                    float yMin = pieces.SelectMany(pc => pc).Min(g => g.p.y), yMax = pieces.SelectMany(pc => pc).Max(g => g.p.y);
                    Vector3 origin = along * u0 + normal * mid + Vector3.up * yMin;
                    Quaternion rotation = Quaternion.LookRotation(normal, Vector3.up);
                    Quaternion inverse = Quaternion.Inverse(rotation);

                    var go = new GameObject($"DeptGlass_{panelIndex}_{k}");
                    Undo.RegisterCreatedObjectUndo(go, "깨지는 유리");
                    go.transform.SetParent(holder, false);
                    go.transform.SetPositionAndRotation(origin, rotation);
                    go.layer = source.gameObject.layer;

                    var verts = new List<Vector3>();
                    var norms = new List<Vector3>();
                    var uvs = new List<Vector2>();
                    var idx = new List<int>();
                    var outline = new List<Vector2>();
                    for (int pi = 0; pi < pieces.Count; pi++)
                    {
                        List<GV> poly = pieces[pi];
                        int start = verts.Count;
                        foreach (GV g in poly)
                        {
                            verts.Add(inverse * (g.p - origin));
                            norms.Add((inverse * g.n).normalized);
                            uvs.Add(g.uv);
                        }
                        for (int q = 1; q + 1 < poly.Count; q++)
                        {
                            idx.Add(start); idx.Add(start + q); idx.Add(start + q + 1);
                            // 앞면(두께 + 쪽을 보는 면)은 칸을 자를 윤곽
                            if (isFront[pi])
                                foreach (int vi in new[] { start, start + q, start + q + 1 })
                                    outline.Add(new Vector2(verts[vi].x, verts[vi].y));
                        }
                    }
                    // 잘린 자리의 유리 옆면(두께 면) — 판과 판 사이에 가는 선으로 보인다
                    foreach (bool atStart in new[] { true, false })
                    {
                        if (atStart ? k == 0 : k == count - 1)
                            continue;
                        float x = atStart ? 0f : u1 - u0;
                        float lo = float.MaxValue, hi = float.MinValue;
                        foreach (Vector2 p in outline)
                            if (Mathf.Abs(p.x - x) < 1e-4f)
                            {
                                lo = Mathf.Min(lo, p.y);
                                hi = Mathf.Max(hi, p.y);
                            }
                        if (hi <= lo)
                            continue;
                        float hz = thickness * 0.5f;
                        var side = new Vector3(atStart ? -1f : 1f, 0f, 0f);
                        int b = verts.Count;
                        verts.Add(new Vector3(x, lo, hz)); verts.Add(new Vector3(x, hi, hz));
                        verts.Add(new Vector3(x, hi, -hz)); verts.Add(new Vector3(x, lo, -hz));
                        for (int q = 0; q < 4; q++)
                        {
                            norms.Add(side);
                            uvs.Add(Vector2.zero);
                        }
                        bool facesOut = Vector3.Dot(Vector3.Cross(verts[b + 1] - verts[b], verts[b + 2] - verts[b]), side) >= 0f;
                        if (facesOut)
                        {
                            idx.Add(b); idx.Add(b + 1); idx.Add(b + 2);
                            idx.Add(b); idx.Add(b + 2); idx.Add(b + 3);
                        }
                        else
                        {
                            idx.Add(b); idx.Add(b + 2); idx.Add(b + 1);
                            idx.Add(b); idx.Add(b + 3); idx.Add(b + 2);
                        }
                    }
                    var chunkMesh = new Mesh { name = go.name };
                    chunkMesh.SetVertices(verts);
                    chunkMesh.SetNormals(norms);
                    chunkMesh.SetUVs(0, uvs);
                    chunkMesh.SetTriangles(idx, 0);
                    chunkMesh.RecalculateBounds();
                    go.AddComponent<MeshFilter>().sharedMesh = chunkMesh;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterials = source.sharedMaterials;
                    mr.shadowCastingMode = source.shadowCastingMode;
                    mr.receiveShadows = source.receiveShadows;
                    mr.lightProbeUsage = source.lightProbeUsage;
                    mr.reflectionProbeUsage = source.reflectionProbeUsage;
                    // 정적(static)으로 두지 않는다 — 정적 묶음이 되면 깨질 때 바꾼 메시가 안 보인다
                    GameObjectUtility.SetStaticEditorFlags(go, 0);

                    var breakable = go.AddComponent<BreakableGlass>();
                    var so = new SerializedObject(breakable);
                    var o = so.FindProperty("m_Outline");
                    o.arraySize = outline.Count;
                    for (int i = 0; i < outline.Count; i++)
                        o.GetArrayElementAtIndex(i).vector2Value = outline[i];
                    so.FindProperty("m_Length").floatValue = u1 - u0;
                    so.FindProperty("m_Height").floatValue = yMax - yMin;
                    so.FindProperty("m_Thickness").floatValue = thickness;
                    so.FindProperty("m_ShardsPrefab").objectReferenceValue = shardsPrefab;
                    so.FindProperty("m_CrackedMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(k_CrackedMaterial);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    made++;
                }
                panelIndex++;
            }
            return made;
        }
    }
}
