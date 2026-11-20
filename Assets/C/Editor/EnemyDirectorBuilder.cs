using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MilTrainSimVR.UrbanCombat.EditorTools
{
    // 적 지휘 규칙 만들기 (11/17 사용자) — C_UrbanBlock 을 열고 메뉴를 누른다. 다시 눌러도 같다 (만든 것만 지우고 다시 짓는다). Ctrl+Z 로 되돌릴 수 있다.
    // 씬은 저장하지 않는다 — 확인하고 직접 저장한다.
    //   1. 적 20 — 시작 30초 뒤 N23 에서 나와 백화점 앞(동쪽 길)을 순찰 (오버타임 때 백화점까지 가장 멀었다)
    //   2. 적 12 사살 → 적 2(N20) · 적 6(N27) · 적 8 이 북동쪽 지하 입구(북1)로 뛰어 내려가 지하 홀 북쪽 엄폐 자리를 지킨다
    //      (11/17 사용자: 적 10 은 백화점 2·3층 순찰로 옮겨서 빠짐 — 3명만)
    //   3. 적 2 또는 적 6 사살 → 지하의 적 4 · 12 · 13 이 북1 밖으로 나와 자리 잡고 플레이어 쪽으로 대응 사격
    //      2 · 3 은 먼저 일어난 쪽만 (지하로 가면 지상 적이 내려오고, 지상으로 가면 지하 적이 나온다)
    //   4. 플레이어가 백화점 2층에 들어오면 → 적 19 추적
    //   5. 플레이어가 백화점 3층에 들어오면 → 적 3 추적
    //   6. 플레이어가 백화점 1층에 들어오면 → 적 10(2·3층 순찰)이 에스컬레이터로 1층에 내려와 추적 (11/17 사용자 — 모서리 계단이 더 짧아도 에스컬레이터로)
    //   8. 플레이어가 백화점 1층에 들어오면 → 10초 뒤 적 17(2층 동쪽 창)이 북동 계단으로 1층에 내려와 추적 (11/18 사용자 — 창가에 남지 말고)
    //   9. 플레이어가 백화점 2층에 들어오면 → 적 9(3층 동쪽 창)가 남동 계단으로 2층에 내려와 추적 (11/18 사용자)
    //   7. 적 6 이 경계한 지 6초 → N27 창에서 나와 승합차 3(고리 도로 동쪽 북) 뒤로 엄폐하며 옮긴다 (11/18 사용자 — "초기 대응 이후 그 자리를 지키지 말고")
    //      자리 = 밴 동쪽 옆 (118.2, 209.2): 남쪽에서 올라오는 플레이어 접근길 29곳 중 17곳을 쏠 수 있고, 그 17곳 모두 2.5m 안에 밴 뒤 앉아 숨을 곳이 있다
    //      규칙 2 가 먼저 일어나면 7 은 일어나지 않는다 (11/19 사용자 — 밴 엄폐는 지상전 대비라 지하에서 싸우면 밴에 있을 이유가 없다).
    //      7 이 먼저였으면 규칙 2 가 적 6 을 밴에서 지하 홀로 내려보낸다
    //   오버타임 — 모든 적이 백화점 3층으로 (MatchFlow, 원래 있던 것. 규칙은 자유 전투 동안만)
    // 자리는 11/17 에 길찾기 지도 · 시야로 골랐다: 엄폐물 옆이고 플레이어가 올 쪽이 보이는 곳 (지하 홀 남쪽 표적 21곳 중 14~20곳, 북1 밖은 도로 표적 35곳 중 17~18곳이 보임)
    static class EnemyDirectorBuilder
    {
        const string k_Root = "C_EnemyOrders";
        const string k_Route20 = "적 20 경로 · 30초 뒤 백화점 앞 순찰";
        const float k_FloorHeight = 3.4f;   // 백화점 층 높이 (2층 바닥 3.6 · 3층 바닥 7.0)

        // 규칙 8 · 9 만 지금 씬의 지휘에 덧붙인다 (11/18) — 전체를 다시 지으면 씬에서 옮긴 자리 · 구역이 처음대로 돌아가서.
        // 같은 이름의 규칙 · 경유점은 바꿔 끼운다 (다시 눌러도 같다). 씬은 저장하지 않는다
        [MenuItem("MilTrainSimVR/C/적 지휘 — 창가 적 계단으로 내려와 추적 (규칙 8 · 9 만 덧붙이기)")]
        static void AppendStairRules()
        {
            var flow = Object.FindFirstObjectByType<MatchFlow>();
            var director = flow != null ? flow.GetComponent<EnemyDirector>() : null;
            GameObject root = GameObject.Find(k_Root);
            Transform floor1 = root != null ? root.transform.Find("구역 · 백화점 1층") : null;
            Transform floor2 = root != null ? root.transform.Find("구역 · 백화점 2층") : null;
            var enemies = FindEnemies();
            if (director == null || floor1 == null || floor2 == null || !enemies.ContainsKey(9) || !enemies.ContainsKey(17))
            {
                Debug.LogError("[C] 적 지휘 — 지휘(EnemyDirector) · C_EnemyOrders 의 백화점 1 · 2층 구역 · 적 9 · 17 이 있어야 한다 (없으면 '적 지휘 규칙 만들기' 먼저)");
                return;
            }
            Undo.SetCurrentGroupName("창가 적 계단으로");
            int group = Undo.GetCurrentGroup();
            foreach (string old in new[] { StairsNorthEast, StairsSouthEast })
            {
                Transform t = root.transform.Find(old);
                if (t != null)
                    Undo.DestroyObjectImmediate(t.gameObject);
            }
            Transform[] ne = Spots(root.transform, StairsNorthEast, new[] { new Vector3(52.1f, 3.6f, 236.6f), new Vector3(55.7f, 1.9f, 237.6f), new Vector3(52.2f, 0.2f, 238.1f) }, p => Vector3.back);
            Transform[] se = Spots(root.transform, StairsSouthEast, new[] { new Vector3(52.1f, 7.0f, 196.8f), new Vector3(55.7f, 5.3f, 195.9f), new Vector3(52.2f, 3.6f, 195.6f) }, p => Vector3.forward);
            foreach (Transform[] made in new[] { ne, se })
                Undo.RegisterCreatedObjectUndo(made[0].parent.gameObject, "경유점");
            EnemyDirector.Rule a = StairsRule1(enemies, floor1.GetComponent<TriggerArea>(), ne);
            EnemyDirector.Rule b = StairsRule2(enemies, floor2.GetComponent<TriggerArea>(), se);
            var rules = new List<EnemyDirector.Rule>();
            foreach (EnemyDirector.Rule r in director.Rules)
                if (r.name != a.name && r.name != b.name)
                    rules.Add(r);
            rules.Add(a);
            rules.Add(b);
            Undo.RecordObject(director, "창가 적 계단으로");
            director.EditorSetup(flow, rules.ToArray());
            EditorUtility.SetDirty(director);
            Undo.CollapseUndoOperations(group);
            Debug.Log($"[C] 적 지휘 — 규칙 8 · 9 (창가 적 17 · 9 계단으로 내려와 추적) 덧붙임, 지금 규칙 {rules.Count}개 (씬은 저장하지 않음)");
        }

        [MenuItem("MilTrainSimVR/C/적 지휘 규칙 만들기 (적 20 순찰 · 지상↔지하 · 백화점 추적 · 적 10 에스컬레이터 · 적 6 밴 엄폐 · 창가 적 계단)")]
        static void Build()
        {
            var flow = Object.FindFirstObjectByType<MatchFlow>();
            if (flow == null)
            {
                Debug.LogError("[C] 적 지휘 — 경기 진행(MatchFlow)이 없다. C_UrbanBlock 을 열고 누를 것");
                return;
            }
            var enemies = FindEnemies();
            foreach (int n in new[] { 2, 3, 4, 6, 8, 9, 10, 12, 13, 17, 19, 20 })
            {
                if (!enemies.ContainsKey(n))
                {
                    Debug.LogError($"[C] 적 지휘 — '적 {n} · …' 이 씬에 없다");
                    return;
                }
            }
            Undo.SetCurrentGroupName("적 지휘 규칙 만들기");
            int group = Undo.GetCurrentGroup();

            // 지난번에 만든 것 지우기
            GameObject old = GameObject.Find(k_Root);
            if (old != null)
                Undo.DestroyObjectImmediate(old);
            Transform routes = GameObject.Find("C_EnemyRoutes")?.transform;
            Transform oldRoute = routes != null ? routes.Find(k_Route20) : null;
            if (oldRoute != null)
                Undo.DestroyObjectImmediate(oldRoute.gameObject);

            var root = new GameObject(k_Root);
            Undo.RegisterCreatedObjectUndo(root, "적 지휘");

            // 구역 — 점령지(백화점 3층 상자)와 같은 상자, 2층은 한 층 아래
            CaptureZone zone = flow.Zone;
            Vector3 zoneSize = new SerializedObject(zone).FindProperty("m_Size").vector3Value;
            TriggerArea floor2 = Area(root.transform, "구역 · 백화점 2층", "백화점 2층", zone.transform.position + Vector3.down * k_FloorHeight, zone.transform.rotation, zoneSize);
            TriggerArea floor3 = Area(root.transform, "구역 · 백화점 3층", "백화점 3층", zone.transform.position, zone.transform.rotation, zoneSize);
            TriggerArea floor1 = Area(root.transform, "구역 · 백화점 1층", "백화점 1층", zone.transform.position + Vector3.down * (2f * k_FloorHeight), zone.transform.rotation, zoneSize);

            // 자리
            Transform[] underground = Spots(root.transform, "자리 · 적 12 사살 → 지하 홀 북쪽 (북1 로 내려옴)", new[]
            {
                new Vector3(88.4f, -4.5f, 215.2f),   // 홀 서쪽 벽 옆, 동쪽 콘크리트 벽(엄폐 2) 뒤로 숨을 수 있음
                new Vector3(87.6f, -4.5f, 222.2f),   // 홀 서쪽 벽 옆, 북쪽 매대 뒤로 숨을 수 있음
                new Vector3(93.4f, -4.5f, 214.1f),   // 낮은 매대 뒤
                new Vector3(90.8f, -4.5f, 208.0f),   // 낮은 매대 뒤 (가장 앞)
            }, p => Vector3.back);
            Vector3 eastRoad = new Vector3(117f, 0f, 220f);
            Transform[] northExit = Spots(root.transform, "자리 · 적 2·6 사살 → 북1 밖 (지하에서 나옴)", new[]
            {
                new Vector3(117.2f, 0.1f, 247.6f),   // 낮은 엄폐 뒤, 옆에 벽
                new Vector3(115.6f, 0.1f, 239.6f),   // 낮은 엄폐 뒤
                new Vector3(110.8f, 0.2f, 250.0f),   // 낮은 엄폐 뒤 (입구 바로 옆)
            }, p => eastRoad - p);
            // 에스컬레이터로 1층까지 — 3층 → 2층은 2·3층 에스컬레이터(2층 쪽 끝이 북쪽), 2층 → 1층은 1·2층 에스컬레이터(1층 쪽 끝이 남쪽)
            Transform[] escalatorDown = Spots(root.transform, "경유 · 백화점 1층 → 적 10 에스컬레이터로 내려옴", new[]
            {
                new Vector3(41.5f, 7.0f, 209.0f),    // 3층 — 2·3층 에스컬레이터 위 (여기서 북쪽으로 내려감)
                new Vector3(38.8f, 3.6f, 224.6f),    // 2층 — 1·2층 에스컬레이터 위 (여기서 남쪽으로 내려감)
                new Vector3(38.8f, 0.2f, 209.0f),    // 1층 — 1·2층 에스컬레이터 아래
            }, p => Vector3.back);
            // 창가 적이 모서리 계단으로 한 층 내려오는 길 (11/18 — 길찾기 지도로 잼: 계단 입구 → 층계참 → 아래층 계단 끝)
            Transform[] stairsNE = Spots(root.transform, StairsNorthEast, new[]
            {
                new Vector3(52.1f, 3.6f, 236.6f),    // 2층 — 북동 계단 입구
                new Vector3(55.7f, 1.9f, 237.6f),    // 층계참
                new Vector3(52.2f, 0.2f, 238.1f),    // 1층 — 계단 아래
            }, p => Vector3.back);
            Transform[] stairsSE = Spots(root.transform, StairsSouthEast, new[]
            {
                new Vector3(52.1f, 7.0f, 196.8f),    // 3층 — 남동 계단 입구
                new Vector3(55.7f, 5.3f, 195.9f),    // 층계참
                new Vector3(52.2f, 3.6f, 195.6f),    // 2층 — 계단 아래
            }, p => Vector3.forward);
            // 적 6 이 엄폐하며 옮겨 갈 자리 — 승합차 3 동쪽 옆, 남남서(플레이어가 올라오는 동쪽 고리 도로)를 본다
            Transform[] vanCover = Spots(root.transform, "자리 · 적 6 경계 6초 → 승합차 3 동쪽 (엄폐하며 옮김)", new[]
            {
                new Vector3(118.2f, 0.1f, 209.2f),
            }, p => new Vector3(113f, 0f, 190f) - p);

            // 적 20 의 새 순찰 길 — 백화점 동쪽 길, 점마다 동쪽(길)을 본다
            EnemyRoute route20 = Route(routes != null ? routes : root.transform, k_Route20, new[]
            {
                new Vector3(59.5f, 0.22f, 200f),
                new Vector3(59.5f, 0.22f, 232f),
            }, Vector3.right);

            // 규칙
            var director = flow.GetComponent<EnemyDirector>();
            if (director == null)
                director = Undo.AddComponent<EnemyDirector>(flow.gameObject);
            Undo.RecordObject(director, "적 지휘 규칙");
            const string down = "적 12 사살 → 지상 적 지하로";
            const string up = "적 2·6 사살 → 지하 적 밖으로";
            const string van = "적 6 · 경계 6초 뒤 승합차 3 뒤로 엄폐";
            director.EditorSetup(flow, new[]
            {
                new EnemyDirector.Rule
                {
                    name = "적 20 · 30초 뒤 백화점 앞 순찰",
                    when = EnemyDirector.When.AfterSeconds, seconds = 30f,
                    enemies = Movers(enemies, 20), order = EnemyDirector.Order.NewRoute, route = route20,
                },
                new EnemyDirector.Rule
                {
                    name = down,
                    when = EnemyDirector.When.AnyKilled, killed = Healths(enemies, 12),
                    enemies = Movers(enemies, 2, 6, 8), order = EnemyDirector.Order.HoldSpots, spots = underground,
                    // 밴 엄폐는 지상전 대비 — 지하로 내려간 뒤엔 일어나지 않게 막는다 (안 막으면 적 6 이 내려가다 6초 뒤 밴으로 되돌아갔다)
                    alertToPlayer = true, blocks = new[] { up, van },
                },
                new EnemyDirector.Rule
                {
                    name = up,
                    when = EnemyDirector.When.AnyKilled, killed = Healths(enemies, 2, 6),
                    enemies = Movers(enemies, 4, 12, 13), order = EnemyDirector.Order.HoldSpots, spots = northExit,
                    alertToPlayer = true, blocks = new[] { down },
                },
                new EnemyDirector.Rule
                {
                    name = "백화점 2층 → 적 19 추적",
                    when = EnemyDirector.When.PlayerEnters, area = floor2,
                    enemies = Movers(enemies, 19), order = EnemyDirector.Order.ChasePlayer,
                },
                new EnemyDirector.Rule
                {
                    name = "백화점 3층 → 적 3 추적",
                    when = EnemyDirector.When.PlayerEnters, area = floor3,
                    enemies = Movers(enemies, 3), order = EnemyDirector.Order.ChasePlayer,
                },
                new EnemyDirector.Rule
                {
                    name = "백화점 1층 → 적 10 에스컬레이터로 내려와 추적",
                    when = EnemyDirector.When.PlayerEnters, area = floor1,
                    enemies = Movers(enemies, 10), order = EnemyDirector.Order.ChaseVia, spots = escalatorDown,
                },
                new EnemyDirector.Rule
                {
                    name = van,
                    when = EnemyDirector.When.AlertedFor, seconds = 6f,
                    enemies = Movers(enemies, 6), order = EnemyDirector.Order.TakeCover, spots = vanCover,
                    alertToPlayer = true,
                },
                StairsRule1(enemies, floor1, stairsNE),
                StairsRule2(enemies, floor2, stairsSE),
            });
            EditorUtility.SetDirty(director);
            Undo.CollapseUndoOperations(group);
            Debug.Log("[C] 적 지휘 규칙 9개 · 구역 3 · 자리 8 · 경유점 9 · 적 20 경로를 만들었다 (씬은 저장하지 않음)");
        }

        public const string StairsNorthEast = "경유 · 백화점 1층 → 적 17 북동 계단으로 내려옴";
        public const string StairsSouthEast = "경유 · 백화점 2층 → 적 9 남동 계단으로 내려옴";

        // 8. 백화점 1층 진입 → 10초 뒤 적 17(2층 동쪽 창)이 북동 계단으로 1층에 내려와 추적 — 적 10(에스컬레이터)과 한꺼번에 들이닥치지 않게 (11/18 사용자)
        public static EnemyDirector.Rule StairsRule1(Dictionary<int, CombatantHealth> enemies, TriggerArea floor1, Transform[] stairs) => new EnemyDirector.Rule
        {
            name = "백화점 1층 → 10초 뒤 적 17 계단으로 내려와 추적",
            when = EnemyDirector.When.PlayerEnters, area = floor1, delay = 10f,
            enemies = Movers(enemies, 17), order = EnemyDirector.Order.ChaseVia, spots = stairs,
        };

        // 9. 백화점 2층 진입 → 적 9(3층 동쪽 창)가 남동 계단으로 2층에 내려와 추적 (11/18 사용자)
        public static EnemyDirector.Rule StairsRule2(Dictionary<int, CombatantHealth> enemies, TriggerArea floor2, Transform[] stairs) => new EnemyDirector.Rule
        {
            name = "백화점 2층 → 적 9 계단으로 내려와 추적",
            when = EnemyDirector.When.PlayerEnters, area = floor2,
            enemies = Movers(enemies, 9), order = EnemyDirector.Order.ChaseVia, spots = stairs,
        };

        // '적 N · …' 이름에서 번호로
        public static Dictionary<int, CombatantHealth> FindEnemiesByNumber() => FindEnemies();
        static Dictionary<int, CombatantHealth> FindEnemies()
        {
            var map = new Dictionary<int, CombatantHealth>();
            foreach (CombatantHealth h in Object.FindObjectsByType<CombatantHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!h.name.StartsWith("적 "))
                    continue;
                string[] parts = h.name.Substring(2).Split(' ');
                if (int.TryParse(parts[0], out int n))
                    map[n] = h;
            }
            return map;
        }

        static EnemyMover[] Movers(Dictionary<int, CombatantHealth> enemies, params int[] numbers)
        {
            var list = new List<EnemyMover>();
            foreach (int n in numbers)
                list.Add(enemies[n].GetComponent<EnemyMover>());
            return list.ToArray();
        }

        static CombatantHealth[] Healths(Dictionary<int, CombatantHealth> enemies, params int[] numbers)
        {
            var list = new List<CombatantHealth>();
            foreach (int n in numbers)
                list.Add(enemies[n]);
            return list.ToArray();
        }

        static TriggerArea Area(Transform parent, string name, string displayName, Vector3 position, Quaternion rotation, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            var area = go.AddComponent<TriggerArea>();
            var so = new SerializedObject(area);
            so.FindProperty("m_DisplayName").stringValue = displayName;
            so.FindProperty("m_Size").vector3Value = size;
            so.ApplyModifiedPropertiesWithoutUndo();
            return area;
        }

        // 자리 — 길찾기 지도 위로 붙이고, 앞(+Z)이 보는 쪽
        static Transform[] Spots(Transform parent, string name, Vector3[] points, System.Func<Vector3, Vector3> look)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            var list = new List<Transform>();
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 p = Snap(points[i]);
                Vector3 dir = look(p);
                dir.y = 0f;
                var spot = new GameObject($"자리 {i + 1}").transform;
                spot.SetParent(group, false);
                spot.SetPositionAndRotation(p, Quaternion.LookRotation(dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.forward, Vector3.up));
                list.Add(spot);
            }
            return list.ToArray();
        }

        static EnemyRoute Route(Transform parent, string name, Vector3[] points, Vector3 look)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "적 20 경로");
            for (int i = 0; i < points.Length; i++)
            {
                var point = new GameObject($"점 {i + 1}").transform;
                point.SetParent(go.transform, false);
                point.SetPositionAndRotation(Snap(points[i]), Quaternion.LookRotation(look, Vector3.up));
            }
            return go.AddComponent<EnemyRoute>();
        }

        static Vector3 Snap(Vector3 p)
        {
            return UnityEngine.AI.NavMesh.SamplePosition(p, out UnityEngine.AI.NavMeshHit hit, 1f, UnityEngine.AI.NavMesh.AllAreas) ? hit.position : p;
        }
    }
}
