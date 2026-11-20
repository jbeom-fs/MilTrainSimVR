using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MilTrainSimVR.UrbanCombat.EditorTools
{
    // 안 쓰는 방 막기 (11/16 사용자: "N31, N32 의 안 가는 방 — 복도는 셔터로 닫아버리고, 문은 철문으로 막아 줘. N32 는 2층 없애도 될 듯",
    //   "막아둔 곳은 이후에 구현을 안 해놓을 거라서 빈방으로 만들거나 없애버려도 됨"). 자리는 사용자가 놓은 바리케이드(C_PrecastWall)를 따랐고,
    //   그것만으로 새는 곳(다른 문 · 계단 · 1층 창)은 더 막았다.
    //   · 복도 셔터: 복도 단면(바닥 ~ 천장)을 말아 내린 셔터 — 위 셔터 통, 양옆 가이드 레일, 아래 하단 바
    //   · 철문: 방 문 구멍에 닫힌 철문 — 문짝 · 양면 문틀 · 양면 손잡이
    //   · 창 셔터: 막힌 방의 1층 바깥 창. 창턱(0.5 ~ 0.9 m)은 점프로 넘으니 창 구멍도 판정으로 막고 바깥에 가게 셔터를 내린 모습
    //   · 계단실: N31 가운데 계단 — 2층 꼭대기가 막힌 복도라 1층에서 벽(흰 미장)으로 두르고 철문
    //   · 막힌 곳 안 소품(선반 · 책상 모델)은 지운다 — 보이지 않는데 그려지기만 한다
    // 모두 C_Barriers/막힌 곳 밑: 맵 만들기(C_Greybox)가 건드리지 않고, 적 길찾기 지도 굽기가 막힌 곳으로 읽는다(UrbanGreyboxBuilder.Navigation.cs).
    // 자리는 건물 좌표(앞면 왼쪽 아래 모서리 원점, x 폭 · z 깊이 — 빌더와 같다)로 대강 적고, 문 · 창 · 복도 크기는 맵 콜라이더에 광선을 쏘아 잰다.
    // 보이는 부품은 건물마다 재질별 메시 하나로 합친다(그리기 호출 줄이기) — 메시는 Assets/C/Models/Closures 에.
    // 맵 만들기를 다시 돌리면 소품이 되살아나니 이 메뉴를 한 번 더 — 셔터 · 철문은 그대로 다시 단다
    public static class UrbanClosureBuilder
    {
        const string k_GroupName = "막힌 곳 (N31 · N32)";
        const string k_MeshFolder = "Assets/C/Models/Closures";
        const string k_MeshAsset = k_MeshFolder + "/C_ClosureMeshes.asset";
        const string k_MaterialFolder = "Assets/C/Materials/Greybox/";
        const float k_FloorHeight = 3.4f;
        const float k_FirstFloorTop = 0.17f;

        enum Kind { CorridorShutter, SteelDoor, WindowShutter }

        // wallAlongX: 벽이 건물 x 를 따라 놓였다(벽의 법선 = z). line: 벽 자리(법선 축 좌표, 대강), along: 벽을 따라 구멍 가운데(대강).
        // openSide: 사람이 보는 쪽 — 법선 축에서 +1 / -1. 셔터 통 · 가이드는 그쪽 벽면에 붙는다
        struct Spot
        {
            public string building; public int floor; public Kind kind; public bool wallAlongX; public float line, along; public int openSide; public string label;
            public Spot(string b, int f, Kind k, bool x, float l, float a, int o, string name) { building = b; floor = f; kind = k; wallAlongX = x; line = l; along = a; openSide = o; label = name; }
        }

        static readonly Spot[] k_Spots =
        {
            // ---- N31 1층: 가운데 남북 복도 동쪽(x 16.1) 너머 — 동서 복도 동쪽 + 남동 방 넷
            new Spot("N31", 0, Kind.CorridorShutter, false, 16.1f, 11.45f, -1, "1층 동쪽 복도 셔터"),
            new Spot("N31", 0, Kind.SteelDoor, false, 16.1f, 7.85f, -1, "1층 남동 방 문"),
            new Spot("N31", 0, Kind.SteelDoor, false, 16.1f, 2.62f, -1, "1층 남동 끝 방 문"),
            new Spot("N31", 0, Kind.SteelDoor, true, 12.5f, 19.65f, 1, "1층 북동 방 → 동쪽 복도 문"),       // 북동 방은 그대로 쓴다 — 막힌 복도로 새는 문만
            new Spot("N31", 0, Kind.SteelDoor, true, 12.5f, 26.45f, 1, "1층 동쪽 계단방 → 동쪽 복도 문"),   // 계단방은 2층에서 내려오면 막다른 방
            new Spot("N31", 0, Kind.WindowShutter, true, 0f, 17.88f, -1, "1층 남쪽 창 셔터 1"),
            new Spot("N31", 0, Kind.WindowShutter, true, 0f, 21.23f, -1, "1층 남쪽 창 셔터 2"),
            new Spot("N31", 0, Kind.WindowShutter, true, 0f, 24.83f, -1, "1층 남쪽 창 셔터 3"),
            new Spot("N31", 0, Kind.WindowShutter, true, 0f, 28.23f, -1, "1층 남쪽 창 셔터 4"),
            // ---- N31 2층: 가운데 복도 북쪽 + 북동 안쪽 북쪽 방
            new Spot("N31", 1, Kind.CorridorShutter, true, 12.53f, 14.35f, -1, "2층 가운데 복도 북쪽 셔터"),
            new Spot("N31", 1, Kind.SteelDoor, false, 16.1f, 15.02f, 1, "2층 북동 방 문 (복도 쪽)"),
            new Spot("N31", 1, Kind.SteelDoor, true, 17.63f, 19.65f, -1, "2층 북동 방 사잇문"),
            // ---- N32 1층(11/16 2층 없앰): 가운데 남북 복도 동쪽(x 10.83) 너머 전부
            new Spot("N32", 0, Kind.CorridorShutter, false, 10.83f, 9.4f, -1, "1층 동쪽 복도 셔터"),
            new Spot("N32", 0, Kind.SteelDoor, false, 10.83f, 14.6f, -1, "1층 북동 방 문"),
            new Spot("N32", 0, Kind.SteelDoor, false, 10.83f, 4.15f, -1, "1층 남동 방 문"),
            new Spot("N32", 0, Kind.WindowShutter, true, 18.76f, 13.05f, 1, "1층 북쪽 창 셔터 1"),
            new Spot("N32", 0, Kind.WindowShutter, true, 18.76f, 17.2f, 1, "1층 북쪽 창 셔터 2"),
            new Spot("N32", 0, Kind.WindowShutter, true, 0f, 13.05f, -1, "1층 남쪽 창 셔터 1"),
            new Spot("N32", 0, Kind.WindowShutter, true, 0f, 17.2f, -1, "1층 남쪽 창 셔터 2"),
            new Spot("N32", 0, Kind.WindowShutter, false, 19.58f, 16.55f, 1, "1층 동쪽 창 셔터 1"),
            new Spot("N32", 0, Kind.WindowShutter, false, 19.58f, 12.52f, 1, "1층 동쪽 창 셔터 2"),
            new Spot("N32", 0, Kind.WindowShutter, false, 19.58f, 9.35f, 1, "1층 동쪽 창 셔터 3"),
            new Spot("N32", 0, Kind.WindowShutter, false, 19.58f, 6.23f, 1, "1층 동쪽 창 셔터 4"),
            new Spot("N32", 0, Kind.WindowShutter, false, 19.58f, 2.23f, 1, "1층 동쪽 창 셔터 5"),
        };

        // 소품을 지울 막힌 곳 — (건물, 층, 건물 좌표 x0, z0, x1, z1)
        static readonly (string building, int floor, Rect r)[] k_ClosedAreas =
        {
            ("N31", 0, Rect.MinMaxRect(16.1f, 0f, 30.2f, 12.55f)),
            ("N31", 1, Rect.MinMaxRect(12.6f, 12.55f, 16.1f, 23f)),
            ("N31", 1, Rect.MinMaxRect(16.1f, 17.63f, 23.1f, 23f)),
            ("N32", 0, Rect.MinMaxRect(10.83f, 0f, 19.7f, 18.9f)),
        };

        // 건물 좌표틀 — 빌더(UrbanGreyboxBuilder.BuildBuilding)와 같다
        struct Frame
        {
            public Vector3 o; public Quaternion r;
            public Vector3 W(float x, float y, float z) => o + r * new Vector3(x, 0f, z) + Vector3.up * y;
            public Vector3 L(Vector3 world) { Vector3 v = Quaternion.Inverse(r) * (world - o); return new Vector3(v.x, world.y, v.z); }
        }

        class Parts
        {
            public readonly Dictionary<Material, List<CombineInstance>> byMaterial = new Dictionary<Material, List<CombineInstance>>();
            public void Add(Material m, Mesh mesh, Matrix4x4 local)
            {
                if (!byMaterial.TryGetValue(m, out var list))
                    byMaterial[m] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = mesh, transform = local });
            }
        }

        static Material s_Shutter, s_Door, s_Steel, s_Dark, s_Plaster;
        static Mesh s_Cube;
        static readonly List<Mesh> s_NewMeshes = new List<Mesh>();
        static readonly Collider[] s_Overlap = new Collider[16];
        static readonly RaycastHit[] s_Hits = new RaycastHit[32];
        static readonly System.Text.StringBuilder s_Log = new System.Text.StringBuilder();

        [MenuItem("MilTrainSimVR/C/막힌 곳 셔터 · 철문 다시 달기 (N31 · N32)", false, 56)]
        public static void RebuildMenu() => Debug.Log(Rebuild());

        public static string Rebuild()
        {
            s_Log.Clear();
            var scene = EditorSceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return "[C] Play 중에는 안 한다";
            GameObject map = GameObject.Find("C_Greybox");
            GameObject barriers = GameObject.Find(UrbanGreyboxBuilder.k_BarrierRootName);
            GameObject layout = null;
            foreach (GameObject top in scene.GetRootGameObjects())
                if (top.name == "C_Layout")
                    layout = top;
            if (map == null || barriers == null || layout == null)
                return "[C] C_Greybox · C_Barriers · C_Layout 이 있는 씬에서만";

            s_Shutter = LoadMat("GB_Ext_Shutter"); s_Door = LoadMat("GB_Ext_Door"); s_Steel = LoadMat("GB_Util_Steel");
            s_Dark = LoadMat("GB_Util_MetalDark"); s_Plaster = LoadMat("GB_Ext_Interior");
            s_Cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            s_NewMeshes.Clear();
            Physics.SyncTransforms();

            Transform old = barriers.transform.Find(k_GroupName);
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);
            var group = new GameObject(k_GroupName).transform;
            Undo.RegisterCreatedObjectUndo(group.gameObject, "막힌 곳");
            group.SetParent(barriers.transform, false);

            var specs = layout.GetComponentsInChildren<GreyboxBuildingSpec>(true);
            var frames = new Dictionary<string, Frame>();
            foreach (GreyboxBuildingSpec s in specs)
            {
                Transform t = s.transform;
                float width = Mathf.Abs(t.lossyScale.x), depth = Mathf.Abs(t.lossyScale.z);
                Quaternion rot = Quaternion.Euler(0f, t.eulerAngles.y, 0f);
                frames[s.name] = new Frame { o = new Vector3(t.position.x, 0f, t.position.z) + rot * new Vector3(-width * 0.5f, 0f, -depth * 0.5f), r = rot };
            }

            var parts = new Dictionary<string, Parts>();
            var holders = new Dictionary<string, Transform>();
            Transform Holder(string building)
            {
                if (!holders.TryGetValue(building, out Transform h))
                {
                    Frame f = frames[building];
                    h = new GameObject(building).transform;
                    h.SetParent(group, false);
                    h.SetPositionAndRotation(f.o, f.r);
                    holders[building] = h;
                    parts[building] = new Parts();
                }
                return h;
            }

            int made = 0;
            foreach (Spot spot in k_Spots)
            {
                if (!frames.TryGetValue(spot.building, out Frame f))
                {
                    s_Log.AppendLine($"  {spot.building} 없음 — {spot.label} 건너뜀");
                    continue;
                }
                Transform holder = Holder(spot.building);
                bool ok = spot.kind switch
                {
                    Kind.CorridorShutter => CorridorShutter(f, spot, holder, parts[spot.building]),
                    Kind.SteelDoor => SteelDoorAtOpening(f, spot, holder, parts[spot.building]),
                    _ => WindowShutter(f, spot, holder, parts[spot.building]),
                };
                if (ok) made++;
            }
            if (frames.ContainsKey("N31") && Stairwell(frames["N31"], map, Holder("N31"), parts["N31"]))
                made++;

            // 재질별로 합친다
            foreach (var kv in parts)
            {
                Transform h = holders[kv.Key];
                foreach (var mk in kv.Value.byMaterial)
                {
                    var mesh = new Mesh { name = kv.Key + "_" + mk.Key.name, indexFormat = IndexFormat.UInt32 };
                    mesh.CombineMeshes(mk.Value.ToArray(), true, true);
                    mesh.RecalculateBounds();
                    s_NewMeshes.Add(mesh);
                    var go = new GameObject("Mesh_" + mk.Key.name);
                    go.transform.SetParent(h, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    go.AddComponent<MeshRenderer>().sharedMaterial = mk.Key;
                }
            }
            SaveMeshes();

            int removed = RemoveProps(map, frames);
            // 창 유리를 다시 단다 — 셔터를 내린 창엔 유리를 달지 않으니, 셔터가 바뀌면 유리도 맞춘다 (UrbanGreyboxBuilder.WindowGlass.cs, 11/18)
            s_Log.AppendLine(UrbanGreyboxBuilder.InstallWindowGlass(map.transform));
            EditorSceneManager.MarkSceneDirty(scene);
            return $"[C] 막힌 곳 {made}곳 · 막힌 곳 소품 {removed}개 지움\n" + s_Log;
        }

        // ---------------------------------------------------------------- 재기

        static Material LoadMat(string name) => AssetDatabase.LoadAssetAtPath<Material>(k_MaterialFolder + name + ".mat");

        // 창 유리의 몸 판정(BreakableGlass 아래 상자)은 벽이 아니다 — 창 구멍을 잴 때 벽으로 읽으면 안 된다 (11/18)
        static bool IsMap(Collider c) => c != null && !c.isTrigger && c.transform.root.name == "C_Greybox" && c.GetComponentInParent<BreakableGlass>() == null;

        // 맵(C_Greybox)만 — 사용자 바리케이드 · 소품 · 사람은 안 본다
        static bool Solid(Vector3 p, float half = 0.015f)
        {
            int n = Physics.OverlapBoxNonAlloc(p, Vector3.one * half, s_Overlap, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (IsMap(s_Overlap[i]))
                    return true;
            return false;
        }

        static bool MapRay(Vector3 from, Vector3 dir, float max, out RaycastHit best)
        {
            best = default;
            int n = Physics.RaycastNonAlloc(from, dir, s_Hits, max, ~0, QueryTriggerInteraction.Ignore);
            float d = float.MaxValue;
            for (int i = 0; i < n; i++)
                if (IsMap(s_Hits[i].collider) && s_Hits[i].distance < d)
                {
                    d = s_Hits[i].distance;
                    best = s_Hits[i];
                }
            return d < float.MaxValue;
        }

        // 벽을 따라 a, 법선 n, 높이 y → 건물 좌표점 → 세계
        static Vector3 P(Frame f, Spot s, float a, float n, float y) => s.wallAlongX ? f.W(a, y, n) : f.W(n, y, a);

        static float FloorY(Frame f, Spot s, float a, float n)
        {
            float guess = k_FirstFloorTop + s.floor * k_FloorHeight;
            return MapRay(P(f, s, a, n, guess + 1.2f), Vector3.down, 2.5f, out RaycastHit h) ? h.point.y : guess;
        }

        // 벽 두 면 — 법선 축에서 line 둘레를 0.01 m 씩 훑어 맵이 차 있는 구간
        static bool WallFaces(Frame f, Spot s, float a, float y, out float f0, out float f1)
        {
            f0 = f1 = 0f;
            float start = float.NaN, end = float.NaN;
            for (float n = s.line - 0.6f; n <= s.line + 0.6f; n += 0.01f)
            {
                bool solid = Solid(P(f, s, a, n, y), 0.004f);
                if (solid && float.IsNaN(start)) start = n;
                if (solid) end = n;
                if (!solid && !float.IsNaN(start) && n > s.line + 0.05f) break;
            }
            if (float.IsNaN(start)) return false;
            f0 = start - 0.005f; f1 = end + 0.005f;
            return true;
        }

        // 구멍 양쪽 끝 — 벽 가운데 면에서 벽을 따라 훑는다
        static bool Jambs(Frame f, Spot s, float n, float y, out float a0, out float a1)
        {
            a0 = a1 = s.along;
            if (Solid(P(f, s, s.along, n, y), 0.01f)) return false;
            float lo = s.along, hi = s.along;
            while (lo > s.along - 4f && !Solid(P(f, s, lo - 0.01f, n, y), 0.006f)) lo -= 0.01f;
            while (hi < s.along + 4f && !Solid(P(f, s, hi + 0.01f, n, y), 0.006f)) hi += 0.01f;
            a0 = lo; a1 = hi;
            return hi - lo > 0.3f && hi - lo < 7.9f;
        }

        static float ScanUp(Frame f, Spot s, float a, float n, float y0, float max)
        {
            float y = y0;
            while (y < y0 + max && !Solid(P(f, s, a, n, y + 0.01f), 0.006f)) y += 0.01f;
            return y;
        }

        static float ScanDown(Frame f, Spot s, float a, float n, float y0, float max)
        {
            float y = y0;
            while (y > y0 - max && !Solid(P(f, s, a, n, y - 0.01f), 0.006f)) y -= 0.01f;
            return y;
        }

        // ---------------------------------------------------------------- 만들기

        // 건물 좌표틀 안의 상자 하나 — (a, n, y) 범위로
        static void Box(Parts parts, Material m, Spot s, float a0, float a1, float n0, float n1, float y0, float y1)
        {
            if (a1 < a0) (a0, a1) = (a1, a0);
            if (n1 < n0) (n0, n1) = (n1, n0);
            Vector3 c = s.wallAlongX ? new Vector3((a0 + a1) * 0.5f, (y0 + y1) * 0.5f, (n0 + n1) * 0.5f) : new Vector3((n0 + n1) * 0.5f, (y0 + y1) * 0.5f, (a0 + a1) * 0.5f);
            Vector3 size = s.wallAlongX ? new Vector3(a1 - a0, y1 - y0, n1 - n0) : new Vector3(n1 - n0, y1 - y0, a1 - a0);
            parts.Add(m, s_Cube, Matrix4x4.TRS(c, Quaternion.identity, size));
        }

        static void Blocker(Transform holder, Spot s, string name, float a0, float a1, float n0, float n1, float y0, float y1)
        {
            if (a1 < a0) (a0, a1) = (a1, a0);
            if (n1 < n0) (n0, n1) = (n1, n0);
            var go = new GameObject("판정 · " + name);
            go.transform.SetParent(holder, false);
            var box = go.AddComponent<BoxCollider>();
            box.center = s.wallAlongX ? new Vector3((a0 + a1) * 0.5f, (y0 + y1) * 0.5f, (n0 + n1) * 0.5f) : new Vector3((n0 + n1) * 0.5f, (y0 + y1) * 0.5f, (a0 + a1) * 0.5f);
            box.size = s.wallAlongX ? new Vector3(a1 - a0, y1 - y0, n1 - n0) : new Vector3(n1 - n0, y1 - y0, a1 - a0);
        }

        // 말아 내린 셔터 막 — 7.6 cm 마다 홈이 진 앞면 + 평평한 뒷면. 앞면은 face 쪽(openSide)을 본다
        static void Curtain(Parts parts, Spot s, float a0, float a1, float nFront, int side, float y0, float y1)
        {
            const float pitch = 0.076f, groove = 0.011f, lip = 0.011f, back = 0.014f;
            int slats = Mathf.Max(1, Mathf.RoundToInt((y1 - y0) / pitch));
            float p = (y1 - y0) / slats;
            var profile = new List<Vector2> { new Vector2(0f, groove) };   // (높이, 벽 쪽으로 들어간 깊이)
            for (int i = 0; i < slats; i++)
            {
                float b = i * p;
                profile.Add(new Vector2(b + lip, 0f));
                profile.Add(new Vector2(b + p - lip, 0f));
                profile.Add(new Vector2(b + p, groove));
            }
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            float w = a1 - a0;
            // 메시 좌표: x = 벽을 따라(0 ~ w), y = 높이, z = 벽 쪽(+) — 앞면은 -z 를 본다
            for (int i = 1; i < profile.Count; i++)
            {
                Vector2 q0 = profile[i - 1], q1 = profile[i];
                var v0 = new Vector3(0f, q0.x, q0.y); var v1 = new Vector3(w, q0.x, q0.y);
                var v2 = new Vector3(w, q1.x, q1.y); var v3 = new Vector3(0f, q1.x, q1.y);
                Vector3 nrm = Vector3.Cross(v3 - v0, v1 - v0).normalized;
                if (nrm.z > 0f) nrm = -nrm;
                int k = verts.Count;
                verts.AddRange(new[] { v0, v1, v2, v3 });
                norms.AddRange(new[] { nrm, nrm, nrm, nrm });
                uvs.AddRange(new[] { new Vector2(0f, q0.x), new Vector2(w, q0.x), new Vector2(w, q1.x), new Vector2(0f, q1.x) });
                tris.AddRange(new[] { k, k + 2, k + 1, k, k + 3, k + 2 });
            }
            int bk = verts.Count;
            float h = y1 - y0;
            verts.AddRange(new[] { new Vector3(0f, 0f, back), new Vector3(w, 0f, back), new Vector3(w, h, back), new Vector3(0f, h, back) });
            norms.AddRange(new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward });
            uvs.AddRange(new[] { Vector2.zero, new Vector2(w, 0f), new Vector2(w, h), new Vector2(0f, h) });
            tris.AddRange(new[] { bk, bk + 1, bk + 2, bk, bk + 2, bk + 3 });
            var mesh = new Mesh { name = "curtain" };
            mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            s_NewMeshes.Add(mesh);   // 합칠 때 쓰고 버린다 — 저장하지 않는다(SaveMeshes 가 이름으로 거른다)

            // 메시 -z(앞면) → 건물 좌표의 openSide 쪽. x 축은 up × z 로 — 거울상(행렬식 음수)이면 합칠 때 면이 뒤집힌다.
            // 그래서 x 가 벽을 따라 음의 방향이면 원점을 a1 끝에 둔다
            Vector3 zAxis = s.wallAlongX ? new Vector3(0f, 0f, -side) : new Vector3(-side, 0f, 0f);
            Vector3 xAxis = Vector3.Cross(Vector3.up, zAxis);
            float alongSign = s.wallAlongX ? xAxis.x : xAxis.z;
            float aStart = alongSign > 0f ? a0 : a1;
            Vector3 origin = s.wallAlongX ? new Vector3(aStart, y0, nFront) : new Vector3(nFront, y0, aStart);
            var m = Matrix4x4.identity;
            m.SetColumn(0, xAxis); m.SetColumn(1, Vector3.up); m.SetColumn(2, zAxis); m.SetColumn(3, new Vector4(origin.x, origin.y, origin.z, 1f));
            parts.Add(s_Shutter, mesh, m);
        }

        static bool CorridorShutter(Frame f, Spot s, Transform holder, Parts parts)
        {
            float floor = FloorY(f, s, s.along, s.line);
            float yScan = floor + 1.0f;
            if (!Jambs(f, s, s.line, yScan, out float a0, out float a1))
                return Fail(s, "복도 폭을 못 쟀다");
            // 벽 자리: 복도 한쪽 끝 너머(벽이 이어지는 곳), 선반(1.8 m)보다 높은 데서 — 벽에 붙은 선반까지 벽으로 재지 않게
            if (!WallFaces(f, s, a1 + 0.3f, floor + 2.6f, out float n0, out float n1) && !WallFaces(f, s, a0 - 0.3f, floor + 2.6f, out n0, out n1))
                return Fail(s, "벽 면을 못 찾았다");
            float ceil = ScanUp(f, s, (a0 + a1) * 0.5f, (n0 + n1) * 0.5f, yScan, 4f);
            float face = s.openSide > 0 ? n1 : n0;
            int side = s.openSide;
            const float boxH = 0.34f, boxD = 0.3f, guideW = 0.055f, guideD = 0.075f;
            Curtain(parts, s, a0 + 0.01f, a1 - 0.01f, face + side * 0.035f, side, floor, ceil - boxH + 0.02f);
            Box(parts, s_Shutter, s, a0, a1, face, face + side * boxD, ceil - boxH, ceil);                                  // 셔터 통
            Box(parts, s_Steel, s, a0, a0 + guideW, face, face + side * guideD, floor, ceil - boxH);                        // 가이드 레일
            Box(parts, s_Steel, s, a1 - guideW, a1, face, face + side * guideD, floor, ceil - boxH);
            Box(parts, s_Dark, s, a0 + guideW, a1 - guideW, face + side * 0.015f, face + side * 0.06f, floor, floor + 0.05f); // 하단 바
            float mid = (a0 + a1) * 0.5f;
            Box(parts, s_Dark, s, mid - 0.06f, mid + 0.06f, face + side * 0.06f, face + side * 0.08f, floor + 0.05f, floor + 0.12f); // 손잡이 · 자물쇠
            Blocker(holder, s, s.label, a0, a1, face + side * 0.08f, s.openSide > 0 ? n0 : n1, floor, ceil);
            s_Log.AppendLine($"  {s.building} {s.label}: 폭 {a1 - a0:F2} m · 높이 {ceil - floor:F2} m (벽 {n1 - n0:F2})");
            return true;
        }

        static bool SteelDoorAtOpening(Frame f, Spot s, Transform holder, Parts parts)
        {
            float floor = FloorY(f, s, s.along, s.line);
            // 문 머리 위 벽으로 벽 면 — 문 머리(약 2.1 m)와 천장 사이
            if (!WallFaces(f, s, s.along, floor + 2.6f, out float n0, out float n1))
                return Fail(s, "문 위 벽을 못 찾았다");
            float nc = (n0 + n1) * 0.5f;
            if (!Jambs(f, s, nc, floor + 1.0f, out float a0, out float a1))
                return Fail(s, "문 구멍을 못 쟀다");
            float head = ScanUp(f, s, (a0 + a1) * 0.5f, nc, floor + 1.0f, 2.5f);
            SteelDoor(parts, s, a0, a1, n0, n1, floor, head);
            Blocker(holder, s, s.label, a0, a1, n0, n1, floor, head);
            s_Log.AppendLine($"  {s.building} {s.label}: {a1 - a0:F2} × {head - floor:F2} m (벽 {n1 - n0:F2})");
            return true;
        }

        // 닫힌 철문 — 문짝(벽 가운데) · 양면 문틀 · 양면 손잡이(경첩 반대쪽)
        static void SteelDoor(Parts parts, Spot s, float a0, float a1, float n0, float n1, float floor, float head)
        {
            float nc = (n0 + n1) * 0.5f;
            Box(parts, s_Door, s, a0 + 0.003f, a1 - 0.003f, nc - 0.0225f, nc + 0.0225f, floor + 0.004f, head - 0.003f);
            const float fw = 0.05f, fp = 0.012f;
            foreach (float face in new[] { n0, n1 })
            {
                float out1 = face < nc ? face - fp : face + fp;
                Box(parts, s_Dark, s, a0 - fw, a0, face, out1, floor, head + fw);
                Box(parts, s_Dark, s, a1, a1 + fw, face, out1, floor, head + fw);
                Box(parts, s_Dark, s, a0 - fw, a1 + fw, face, out1, head, head + fw);
                // 문틀 안쪽(벽 두께) — 구멍 둘레를 감싼다
            }
            Box(parts, s_Dark, s, a0 - 0.002f, a0 + 0.012f, n0, n1, floor, head);
            Box(parts, s_Dark, s, a1 - 0.012f, a1 + 0.002f, n0, n1, floor, head);
            Box(parts, s_Dark, s, a0, a1, n0, n1, head - 0.012f, head + 0.002f);
            // 손잡이: 문짝 양면, 높이 1.0 m, 경첩 반대쪽(a1 쪽) 끝에서 7 cm
            float ah = a1 - 0.07f, yh = floor + 1.0f;
            foreach (int dir in new[] { -1, 1 })
            {
                float leaf = nc + dir * 0.0225f;
                Box(parts, s_Steel, s, ah - 0.025f, ah + 0.025f, leaf, leaf + dir * 0.01f, yh - 0.07f, yh + 0.07f);          // 판
                Box(parts, s_Steel, s, ah - 0.012f, ah + 0.012f, leaf + dir * 0.01f, leaf + dir * 0.055f, yh - 0.012f, yh + 0.012f); // 목
                Box(parts, s_Steel, s, ah - 0.13f, ah + 0.012f, leaf + dir * 0.045f, leaf + dir * 0.067f, yh - 0.011f, yh + 0.011f); // 레버
                Box(parts, s_Dark, s, ah - 0.01f, ah + 0.01f, leaf, leaf + dir * 0.013f, yh - 0.14f, yh - 0.11f);            // 열쇠 구멍
            }
        }

        static bool WindowShutter(Frame f, Spot s, Transform holder, Parts parts)
        {
            float floor = FloorY(f, s, s.along, s.line - s.openSide * 1.0f);   // 안쪽 바닥
            // 바깥 면: 밖에서 창 아래 벽으로 쏜다. 안쪽 면 = 바깥 면 - 벽 두께(0.2 m) — 안에서 재면 창 밑 낮은 장까지 벽으로 잡힌다
            Vector3 outDir = (s.wallAlongX ? f.r * Vector3.forward : f.r * Vector3.right) * s.openSide;
            if (!MapRay(P(f, s, s.along, s.line + s.openSide * 1.0f, floor + 0.3f), -outDir, 1.5f, out RaycastHit wallHit))
                return Fail(s, "창 아래 벽을 못 찾았다");
            Vector3 wl = f.L(wallHit.point);
            float outer = s.wallAlongX ? wl.z : wl.x;
            float n0 = Mathf.Min(outer, outer - s.openSide * 0.2f), n1 = Mathf.Max(outer, outer - s.openSide * 0.2f);
            float nc = (n0 + n1) * 0.5f;
            float yScan = floor + 1.5f;
            if (!Jambs(f, s, nc, yScan, out float a0, out float a1))
                return Fail(s, "창 구멍을 못 쟀다");
            float mid = (a0 + a1) * 0.5f;
            float sill = ScanDown(f, s, mid, nc, yScan, 1.6f);
            float head = ScanUp(f, s, mid, nc, yScan, 1.5f);
            int side = s.openSide;
            float face = side > 0 ? n1 : n0;
            // 창틀 · 창턱이 벽면에서 몇 cm 나와 있다 — 셔터는 그 앞에
            const float off = 0.09f, boxH = 0.3f, boxD = 0.26f, guideW = 0.05f, guideD = 0.12f, over = 0.06f;
            float g0 = a0 - over - guideW, g1 = a1 + over + guideW;
            Curtain(parts, s, a0 - over, a1 + over, face + side * off, side, sill, head + 0.02f);
            Box(parts, s_Shutter, s, g0 - 0.03f, g1 + 0.03f, face, face + side * boxD, head, head + boxH);
            Box(parts, s_Steel, s, g0, g0 + guideW, face, face + side * guideD, sill - 0.02f, head);
            Box(parts, s_Steel, s, g1 - guideW, g1, face, face + side * guideD, sill - 0.02f, head);
            Box(parts, s_Dark, s, a0 - over, a1 + over, face + side * (off - 0.02f), face + side * (off + 0.03f), sill, sill + 0.05f);
            Box(parts, s_Dark, s, mid - 0.06f, mid + 0.06f, face + side * (off + 0.03f), face + side * (off + 0.05f), sill + 0.05f, sill + 0.11f);
            Blocker(holder, s, s.label, a0, a1, n0, n1, sill, head);   // 창 구멍 자체를 막는다 — 점프로 넘어 들어오지 못하게
            s_Log.AppendLine($"  {s.building} {s.label}: 창 {a1 - a0:F2} × {head - sill:F2} m, 창턱 {sill - floor:F2} m");
            return true;
        }

        // N31 가운데 계단 — 1층에서 계단 옆(복도 쪽)과 발치를 벽으로 두르고 발치에 철문
        static bool Stairwell(Frame f, GameObject map, Transform holder, Parts parts)
        {
            Transform b = map.transform.Find("Buildings/B_N31");
            if (b == null) return false;
            Bounds? ramp = null;
            foreach (Collider c in b.GetComponentsInChildren<Collider>(true))
            {
                if (!c.name.StartsWith("StairRamp")) continue;
                Vector3 lc = f.L(c.bounds.center);
                if (lc.x > 11f && lc.x < 16f) ramp = c.bounds;
            }
            if (ramp == null) { s_Log.AppendLine("  N31 가운데 계단을 못 찾았다"); return false; }
            Vector3 lmin = f.L(ramp.Value.min), lmax = f.L(ramp.Value.max);
            float rx0 = Mathf.Min(lmin.x, lmax.x), rx1 = Mathf.Max(lmin.x, lmax.x), rz0 = Mathf.Min(lmin.z, lmax.z);
            var sx = new Spot("N31", 0, Kind.SteelDoor, false, 0f, 0f, 1, "가운데 계단실");   // 벽이 z 를 따라 (옆벽)
            var sz = new Spot("N31", 0, Kind.SteelDoor, true, 0f, 0f, -1, "가운데 계단실 철문"); // 벽이 x 를 따라 (발치), 보는 쪽 = 남(-z)
            float floor = FloorY(f, sz, (rx0 + rx1) * 0.5f, rz0 - 0.5f);
            float y1 = floor + 1.0f;
            float xc = (rx0 + rx1) * 0.5f;
            // 2층 바닥 구멍 남쪽 끝 — 위로 쏜 광선이 2층 바닥 밑면에 닿다가 안 닿는 곳
            float slabUnder = float.NaN, holeZ = float.NaN;
            for (float z = rz0 - 2f; z <= rz0 + 1.5f; z += 0.02f)
            {
                bool hit = MapRay(f.W(xc, y1, z), Vector3.up, 3f, out RaycastHit h);
                if (hit && h.point.y < floor + 3.6f) slabUnder = h.point.y;
                else if (!float.IsNaN(slabUnder)) { holeZ = z; break; }
            }
            if (float.IsNaN(holeZ)) { holeZ = rz0 - 1f; s_Log.AppendLine("  계단 구멍 끝을 못 찾아 계단 아래 1 m 로"); }
            if (float.IsNaN(slabUnder)) slabUnder = floor + 3.2f;
            float west = MapRay(f.W(xc, y1, holeZ - 0.3f), f.r * Vector3.left, 3f, out RaycastHit hw) ? f.L(hw.point).x : 12.65f;
            // 북쪽 벽은 계단 옆 복도 쪽 줄에서 — 계단 위로 쏘면 계단 밑 채움 콜라이더 속에서 출발한다
            float north = MapRay(f.W(rx1 + 0.3f, y1, rz0 + 2f), f.r * Vector3.forward, 10f, out RaycastHit hn) ? f.L(hn.point).z : 22.8f;
            float px0 = rx1 + 0.005f, px1 = rx1 + 0.105f;   // 옆벽 — 계단 바로 옆
            float fz0 = holeZ - 0.12f, fz1 = holeZ;         // 발치 벽 — 2층 바닥 구멍 끝 밑
            // 옆벽 (흰 미장) — 발치 벽부터 북쪽 벽까지, 바닥 ~ 2층 바닥 밑면
            float sideTop = MapRay(f.W(px1 + 0.2f, y1, (fz1 + north) * 0.5f), Vector3.up, 3f, out RaycastHit hu) ? hu.point.y : slabUnder;
            Box(parts, s_Plaster, sx, fz0, north, px0, px1, floor, sideTop);
            Blocker(holder, sx, "가운데 계단실 옆벽", fz0, north, px0, px1, floor, sideTop);
            // 발치 벽 + 철문
            float dw = Mathf.Min(0.9f, px1 - west - 0.2f), dc = (west + px0) * 0.5f, d0 = dc - dw * 0.5f, d1 = dc + dw * 0.5f, dh = floor + 2.1f;
            Box(parts, s_Plaster, sz, west, d0, fz0, fz1, floor, slabUnder);
            Box(parts, s_Plaster, sz, d1, px1, fz0, fz1, floor, slabUnder);
            Box(parts, s_Plaster, sz, d0, d1, fz0, fz1, dh, slabUnder);
            SteelDoor(parts, sz, d0, d1, fz0, fz1, floor, dh);
            Blocker(holder, sz, "가운데 계단실 발치", west, px1, fz0, fz1, floor, slabUnder);
            s_Log.AppendLine($"  N31 가운데 계단실: 계단 x {rx0:F2}~{rx1:F2} z {rz0:F2}~, 2층 구멍 끝 z {holeZ:F2}, 옆벽 z {fz0:F2}~{north:F2} 높이 {sideTop - floor:F2}, 철문 {dw:F2} m (서쪽 벽 x {west:F2})");
            return true;
        }

        static bool Fail(Spot s, string why)
        {
            s_Log.AppendLine($"  {s.building} {s.label}: {why} — 건너뜀");
            return false;
        }

        // 합친 메시만 한 파일에 — 셔터 막 조각(curtain)은 합칠 때만 쓰고 버린다
        static void SaveMeshes()
        {
            if (!AssetDatabase.IsValidFolder(k_MeshFolder))
                AssetDatabase.CreateFolder("Assets/C/Models", "Closures");
            AssetDatabase.DeleteAsset(k_MeshAsset);
            Mesh main = null;
            foreach (Mesh m in s_NewMeshes)
            {
                if (m.name == "curtain")
                {
                    Object.DestroyImmediate(m);
                    continue;
                }
                if (main == null) { AssetDatabase.CreateAsset(m, k_MeshAsset); main = m; }
                else AssetDatabase.AddObjectToAsset(m, k_MeshAsset);
            }
            AssetDatabase.SaveAssets();
            s_NewMeshes.Clear();
        }

        static int RemoveProps(GameObject map, Dictionary<string, Frame> frames)
        {
            int removed = 0;
            foreach (var area in k_ClosedAreas)
            {
                Transform props = map.transform.Find("Buildings/B_" + area.building + "/Props");
                if (props == null || !frames.TryGetValue(area.building, out Frame f)) continue;
                float y0 = k_FirstFloorTop + area.floor * k_FloorHeight - 0.3f, y1 = y0 + k_FloorHeight;
                for (int i = props.childCount - 1; i >= 0; i--)
                {
                    Transform p = props.GetChild(i);
                    Vector3 l = f.L(p.position);
                    if (l.y >= y0 && l.y < y1 && area.r.Contains(new Vector2(l.x, l.z)))
                    {
                        Undo.DestroyObjectImmediate(p.gameObject);
                        removed++;
                    }
                }
            }
            return removed;
        }
    }
}
