using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace MilTrainSimVR.UrbanCombat.EditorTools
{
    // 적 길찾기 지도(내비메시) 굽기 (10/27) — 맵을 다시 만들면 바닥·벽·실내가 통째로 바뀌니 '맵 만들기' 끝에 같이 굽는다.
    // 프로젝트 공용 Agent 설정(Humanoid, 반지름 0.5m)은 그대로 두고, 그 복사본의 값만 C 적에 맞게 바꿔 굽는다 — 문 폭 1m · 계단 35°.
    // 구운 지도는 씬 옆 폴더(씬 이름과 같은 폴더)에 저장하고, 맵 뿌리(C_Greybox)의 GreyboxNavMesh 가 켠다
    // 길가 차량(씬 루트 C_Vehicles)도 넣어 굽는다 (11/10) — 차를 놓거나 옮기면 지도를 다시 굽는다
    public static partial class UrbanGreyboxBuilder
    {
        public const string k_VehicleRootName = "C_Vehicles";
        const float k_NavRadius = 0.3f;    // 적 몸 반지름 — 문 1m 에 0.4m 폭 길이 남는다
        const float k_NavHeight = 1.8f;    // 적 키(1.7m)보다 조금 크게
        const float k_NavClimb = 0.3f;     // 넘는 턱 — 인도 턱 0.15m 는 넘고, 가구·바리케이드·창턱은 못 넘는다
        const float k_NavSlope = 45f;      // 계단 위 보이지 않는 경사로 (가장 가파른 2층 계단 약 35°)
        const float k_NavVoxel = 0.1f;     // 지도 칸 크기 — 반지름의 1/3 (유니티 기본 비율)
        // 타일(지도를 나눠 굽는 조각) 한 변의 칸 수 — 기본 256(25.6m)이면 조각 경계마다 길이 잘게 나뉘어
        // 200m 넘는 먼 길은 길찾기 탐색 한도에 걸려 '일부만' 나온다(10/27 출발 → N02 2층). 1024(102.4m)면 끝까지 이어진다
        const int k_NavTile = 1024;
        // 굽는 높이: 지하 바닥(-4.5m)부터 3층(쇼핑몰, 바닥 6.95m) 머리 위까지. 옥상·위층 덩어리 윗면(13.75m 이상)은 걷는 곳이 아니라 뺀다
        const float k_NavBottom = -5.5f, k_NavTop = 9.5f;
        const int k_NotWalkableArea = 1;   // 유니티 기본 구역 'Not Walkable'

        [MenuItem("MilTrainSimVR/C/적 길찾기 지도 다시 굽기", false, 21)]
        public static void RebakeNavMesh()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            GameObject root = UrbanLayoutTools.FindRoot(k_RootName);
            GameObject layout = UrbanLayoutTools.FindRoot(UrbanLayoutTools.LayoutRootName);
            GreyboxPlayAreaSpec playArea = layout != null ? layout.GetComponentInChildren<GreyboxPlayAreaSpec>(true) : null;
            if (EditorApplication.isPlayingOrWillChangePlaymode || root == null || playArea == null)
            {
                Debug.LogWarning("[C] Play 중이 아니고, 맵(C_Greybox)과 놀 곳이 있는 씬에서만 굽는다");
                return;
            }
            BakeNavMesh(scene, root, playArea.Corners());
            EditorSceneManager.MarkSceneDirty(scene);
        }

        static void BakeNavMesh(Scene scene, GameObject root, List<Vector3> corners)
        {
            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);   // Humanoid 의 복사본 — 공용 설정 파일은 안 바뀐다
            settings.agentRadius = k_NavRadius;
            settings.agentHeight = k_NavHeight;
            settings.agentClimb = k_NavClimb;
            settings.agentSlope = k_NavSlope;
            settings.overrideVoxelSize = true;
            settings.voxelSize = k_NavVoxel;
            settings.overrideTileSize = true;
            settings.tileSize = k_NavTile;

            // 맵(바닥·건물·지하·보이지 않는 벽)의 콜라이더 + 손으로 놓은 콘크리트 벽 — 적·플레이어·총은 맵 밖 오브젝트라 안 들어간다
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(root.transform, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            // 콘크리트 벽(UrbanGreyboxBuilder.Barrier.cs) — 씬 루트 C_Barriers 밑 전부 + 그 밖에 놓인 벽 프리팹. 맵 밖 오브젝트지만 길을 막는다
            foreach (Transform barrier in HandPlacedBarriers(scene, root))
            {
                var extra = new List<NavMeshBuildSource>();
                NavMeshBuilder.CollectSources(barrier, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), extra);
                sources.AddRange(extra);
            }
            // 길가 차량(사용자 11/10) — 씬 루트 C_Vehicles 밑 차량 프리팹의 판정 상자(몸통 · 객실)가 길을 막는다.
            // 좁은 고리 도로(폭 4 m)에선 차 옆으로 2 m 쯤 남아 순찰이 돌아서 지나간다. 판정 상자가 없는 물체는 안 들어간다.
            // C_Vehicles 밖(씬 루트 등)에 놓은 차량 프리팹도 넣는다 (사용자 11/17 — 차를 씬 루트에 바로 놓았다)
            foreach (Transform vehicle in HandPlacedVehicles(scene, root))
            {
                var extra = new List<NavMeshBuildSource>();
                NavMeshBuilder.CollectSources(vehicle, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), extra);
                sources.AddRange(extra);
            }
            AddOutsideModifiers(sources, corners);

            Vector3 min = corners[0], max = corners[0];
            foreach (Vector3 c in corners)
            {
                min = Vector3.Min(min, c);
                max = Vector3.Max(max, c);
            }
            var bounds = new Bounds();
            bounds.SetMinMax(new Vector3(min.x - 1f, k_NavBottom, min.z - 1f), new Vector3(max.x + 1f, k_NavTop, max.z + 1f));
            NavMeshData baked = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (baked == null)
            {
                Debug.LogWarning("[C] 길찾기 지도를 못 구웠다");
                return;
            }

            // 같은 파일에 덮어써서 GUID 를 지킨다 — 다시 구울 때마다 .meta 가 바뀌지 않게.
            // 켜져 있는(길찾기에 쓰이는) 지도를 그대로 덮어쓰면 나중에 뗄 때 에디터가 죽는다(10/27 실제로 죽음) → 먼저 뗀다
            var loader = root.GetComponent<GreyboxNavMesh>();
            if (loader == null)
                loader = root.AddComponent<GreyboxNavMesh>();
            loader.Data = null;
            string folder = scene.path.Substring(0, scene.path.Length - ".unity".Length);
            string path = $"{folder}/NavMesh_{scene.name}.asset";
            baked.name = $"NavMesh_{scene.name}";
            var data = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
            if (data != null)
            {
                EditorUtility.CopySerialized(baked, data);
                Object.DestroyImmediate(baked);
                EditorUtility.SetDirty(data);
            }
            else
            {
                if (!AssetDatabase.IsValidFolder(folder))
                    AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(folder).Replace('\\', '/'), System.IO.Path.GetFileName(folder));
                AssetDatabase.CreateAsset(baked, path);
                data = baked;
            }
            AssetDatabase.SaveAssets();
            loader.Data = data;
            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
            Debug.Log($"[C] 적 길찾기 지도를 구웠다 — {path} (삼각형 {tri.indices.Length / 3})");
        }

        // 굽기에 넣을 차량 — 씬 루트 C_Vehicles 통째로 + 그 밖에 놓인 차량 프리팹(Prefabs/Vehicle) 하나하나. 맵(C_Greybox) 안은 보지 않는다
        const string k_VehiclePrefabFolder = "Assets/C/Prefabs/Vehicle";

        static List<Transform> HandPlacedVehicles(Scene scene, GameObject map)
        {
            var list = new List<Transform>();
            foreach (GameObject top in scene.GetRootGameObjects())
            {
                if (top == map)
                    continue;
                if (top.name == k_VehicleRootName)
                {
                    list.Add(top.transform);
                    continue;
                }
                foreach (Transform t in top.GetComponentsInChildren<Transform>(true))
                {
                    if (!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))
                        continue;
                    string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject);
                    if (path != null && path.StartsWith(k_VehiclePrefabFolder + "/"))
                        list.Add(t);
                }
            }
            return list;
        }

        // 놀 곳 밖(바깥 도로)을 '걷지 못함' 구역으로 덮는다 — 벽 너머에 따로 떨어진 길 조각이 생기지 않게.
        // 변마다 선 바깥쪽 40m 두께 상자. 놀 곳이 볼록한 다각형이면 바깥쪽 반평면에만 놓여서 안쪽을 덮지 않는다
        static void AddOutsideModifiers(List<NavMeshBuildSource> sources, List<Vector3> corners)
        {
            Vector3 middle = Vector3.zero;
            foreach (Vector3 c in corners)
                middle += c;
            middle /= corners.Count;
            for (int i = 0; i < corners.Count; i++)
            {
                Vector3 a = corners[i], b = corners[(i + 1) % corners.Count];
                Vector3 along = b - a;
                float length = along.magnitude;
                if (length < 0.01f)
                    continue;
                Vector3 dir = along / length;
                var outward = new Vector3(dir.z, 0f, -dir.x);
                if (Vector3.Dot(outward, (a + b) * 0.5f - middle) < 0f)
                    outward = -outward;
                Vector3 center = (a + b) * 0.5f + outward * 20f + Vector3.up * (k_NavBottom + k_NavTop) * 0.5f;
                sources.Add(new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.ModifierBox,
                    transform = Matrix4x4.TRS(center, Quaternion.LookRotation(dir, Vector3.up), Vector3.one),
                    size = new Vector3(40f, k_NavTop - k_NavBottom + 2f, length + 80f),
                    area = k_NotWalkableArea,
                });
            }
        }
    }
}
