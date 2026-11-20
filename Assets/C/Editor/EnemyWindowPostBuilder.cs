using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace MilTrainSimVR.UrbanCombat.EditorTools
{
    // 창가 적의 '매판 다른 창' 자리 (11/19 사용자: "창문에 있는 적이 위치가 고정되어 있다보니, 계속 하다보면 위치를 쉽게 특정할 수 있어서,
    // 창문에서 보고 있는 적은 매판마다 바라보고 있는 쪽의 다른 창문에 있으면 좋겠음. 예를 들어 적 1은 … 5개의 창(왼쪽방 2개, 복도 1개, 오른쪽 방 2개) 중
    // 하나의 위치에 서 보고, 엄폐를 했으면 좋겠는데, 이렇게 창문을 끼고 있는 적을 추려내서 똑같이 만들어 줘").
    //   · 창가 적 = 자리를 지키는 적(경로의 점이 하나) 가운데, 보는 쪽 바로 앞(2m 안)에 창 유리(WindowGlass_Breakable 의 판)가 있는 적
    //   · 후보 창 = 그 창과 같은 건물 · 같은 벽면 · 같은 층의 창
    //   · 창마다 설 자리 = 벽에서 원래 자리만큼 떨어진 곳, 창 가운데 · 양 끝 안쪽 가운데 밖이 가장 잘 보이는 곳.
    //     설 수 있고(길찾기 지도 위 · 가구에 안 끼임), 원래 자리에서 걸어갈 수 있고(방에 갇히지 않게 — 지휘 규칙 · 오버타임으로 나가야 한다),
    //     밖(길 위의 점들)이 원래 자리의 30 % 넘게 보여야 후보로 남긴다. 원래 자리는 그대로 후보 1번
    //   · 자리는 C_EnemyRoutes 아래 '창 자리 · 적 N' 묶음의 자식(창 1 …). 씬에서 옮기거나 지워도 된다 — 적(EnemyMover)의 Post Choices 가 가리킨다.
    //     매판 고르는 것은 EnemyMover.PickPost
    //   · 지휘 규칙이 '경계한 지 몇 초 뒤 자리 옮기기'(EnemyDirector.When.AlertedFor)로 따로 움직이는 적은 뺀다 — 처음 선 자리가 그 행동의 일부라서다.
    //     적 6(N27 서쪽 문간 → 경계 6초 뒤 승합차 3 뒤로)이 그렇다. 11/19 에 내가 적 6 까지 넣었다가 사용자가 되돌리게 했다:
    //     "적 6은 왜 고친거야? 이거 특수 행동 패턴 있어서 얘는 고치면 안되는데". 그때 적 6 을 넣으려고 붙였던 '창 옆 문간에 선 적도 창가 적' 규칙도 뺐다
    // 다시 누르면 '창 자리 · ' 묶음을 지우고 지금 적 자리 기준으로 다시 추린다 (손으로 옮긴 후보는 사라진다). 씬은 저장하지 않는다. Ctrl+Z 로 되돌릴 수 있다
    static class EnemyWindowPostBuilder
    {
        const string k_RoutesRoot = "C_EnemyRoutes";
        const string k_GroupPrefix = "창 자리 · ";
        const string k_GlassGroup = "WindowGlass_Breakable";
        const float k_WindowAhead = 2f;        // 자리에서 창까지 — 이 안이어야 창가 적
        const float k_EdgeInset = 0.45f;       // 창 끝에서 안쪽으로 (몸 반 폭 + 창틀)
        const float k_KeepRatio = 0.3f;        // 원래 자리에서 보이던 것의 이만큼은 보여야 한다

        static string s_Report = "";
        public static string LastReport => s_Report;

        struct Pane
        {
            public Transform building;
            public Vector3 centre, normal;     // normal = 유리 면에 수직인 축 (±)
            public float halfWidth, bottom, top;
        }

        [MenuItem("MilTrainSimVR/C/적 — 창가 적 자리 넣기 (매판 같은 쪽 다른 창)", false, 53)]
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            GameObject routes = UrbanLayoutTools.FindRoot(k_RoutesRoot);
            if (routes == null)
            {
                Debug.LogError("[C] 창가 적 자리 — C_EnemyRoutes 가 없다. C_UrbanBlock 을 열고 누를 것");
                return;
            }
            Physics.SyncTransforms();
            Undo.SetCurrentGroupName("창가 적 자리 넣기");
            int undo = Undo.GetCurrentGroup();
            for (int i = routes.transform.childCount - 1; i >= 0; i--)
                if (routes.transform.GetChild(i).name.StartsWith(k_GroupPrefix))
                    Undo.DestroyObjectImmediate(routes.transform.GetChild(i).gameObject);

            // 지휘 규칙으로 따로 움직이는 적 (경계한 지 몇 초 뒤 자리 옮기기) — 처음 자리를 건드리지 않는다
            var scripted = new HashSet<EnemyMover>();
            foreach (EnemyDirector director in Object.FindObjectsByType<EnemyDirector>(FindObjectsSortMode.None))
                foreach (EnemyDirector.Rule rule in director.Rules)
                    if (rule.when == EnemyDirector.When.AlertedFor)
                        scripted.UnionWith(rule.enemies);

            List<Pane> panes = Panes();
            var report = new StringBuilder();
            int enemies = 0, total = 0;
            EnemyMover[] movers = Object.FindObjectsByType<EnemyMover>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            System.Array.Sort(movers, (a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
            foreach (EnemyMover mover in movers)
            {
                var so = new SerializedObject(mover);
                SerializedProperty choices = so.FindProperty("m_PostChoices");
                EnemyRoute route = mover.Route;
                bool skip = scripted.Contains(mover);
                if (skip)
                    report.AppendLine($"{mover.name}: 뺌 — 지휘 규칙(경계 뒤 자리 옮기기)으로 따로 움직이는 적");
                List<(Vector3 position, string note)> posts = !skip && route != null && route.Count == 1 ? Posts(mover, route.Point(0), panes, report) : null;
                if (posts == null || posts.Count < 2)
                {
                    if (choices.arraySize > 0)
                    {
                        choices.arraySize = 0;
                        so.ApplyModifiedProperties();
                    }
                    continue;
                }
                string label = mover.name.Split('·')[0].Trim();
                var group = new GameObject(k_GroupPrefix + label).transform;
                Undo.RegisterCreatedObjectUndo(group.gameObject, "창 자리");
                group.SetParent(routes.transform, false);
                choices.arraySize = posts.Count;
                for (int i = 0; i < posts.Count; i++)
                {
                    var post = new GameObject($"창 {i + 1}{posts[i].note}").transform;
                    post.SetParent(group, false);
                    post.SetPositionAndRotation(posts[i].position, route.Point(0).rotation);
                    choices.GetArrayElementAtIndex(i).objectReferenceValue = post;
                }
                so.ApplyModifiedProperties();
                enemies++;
                total += posts.Count;
            }
            Undo.CollapseUndoOperations(undo);
            EditorSceneManager.MarkSceneDirty(scene);
            s_Report = $"창가 적 {enemies}명 · 자리 {total}곳\n{report}";
            Debug.Log("[C] 창가 적 자리를 넣었다 — " + s_Report);
        }

        // 씬의 창 유리 판 — 건물 창 · 백화점 창 (에스컬레이터 난간 유리 · 주차장 부스는 뺀다)
        static List<Pane> Panes()
        {
            var panes = new List<Pane>();
            foreach (BreakableGlass glass in Object.FindObjectsByType<BreakableGlass>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                Transform t = glass.transform;
                if (t.parent == null || t.parent.name != k_GlassGroup)
                    continue;
                Transform building = null;
                for (Transform p = t; p != null; p = p.parent)
                    if (p.name.StartsWith("B_N"))
                        building = p;
                var renderer = glass.GetComponentInChildren<Renderer>();
                if (building == null || renderer == null)
                    continue;
                Bounds b = renderer.bounds;
                bool alongX = b.size.x >= b.size.z;   // 유리가 x 쪽으로 누웠으면 면은 z 를 본다
                panes.Add(new Pane
                {
                    building = building,
                    centre = b.center,
                    normal = alongX ? Vector3.forward : Vector3.right,
                    halfWidth = (alongX ? b.size.x : b.size.z) * 0.5f,
                    bottom = b.min.y,
                    top = b.max.y,
                });
            }
            return panes;
        }

        // 그 적이 창가 적이면 후보 자리들 (0번 = 원래 자리). 아니면 null
        static List<(Vector3 position, string note)> Posts(EnemyMover mover, Transform point, List<Pane> panes, StringBuilder report)
        {
            Vector3 post = point.position;
            Vector3 forward = Vector3.ProjectOnPlane(point.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            float eye = post.y + 1.55f;
            // 자기 창 — 보는 쪽 바로 앞, 눈높이에 걸친 판 (창 폭 안에 서 있어야 한다 — 창 사이 문간 · 기둥 뒤에 선 적은 창가 적으로 치지 않는다)
            int own = -1;
            float ownAhead = float.MaxValue;
            for (int i = 0; i < panes.Count; i++)
            {
                Pane p = panes[i];
                if (Mathf.Abs(Vector3.Dot(p.normal, forward)) < 0.9f || eye < p.bottom - 0.2f || eye > p.top + 0.4f)
                    continue;
                float ahead = Vector3.Dot(p.centre - post, forward), side = Mathf.Abs(Vector3.Dot(p.centre - post, right));
                if (ahead < 0.1f || ahead > k_WindowAhead || side > p.halfWidth + 0.35f || ahead >= ownAhead)
                    continue;
                own = i;
                ownAhead = ahead;
            }
            if (own < 0)
                return null;
            Pane mine = panes[own];

            // 밖의 표본 — 보는 쪽 부채꼴(±70°, 6 ~ 55 m)의 길 위 점들. 원래 자리에서 보이는 수가 기준
            var samples = new List<Vector3>();
            foreach (float distance in new[] { 6f, 10f, 14f, 18f, 24f, 30f, 38f, 46f, 55f })
                for (float angle = -70f; angle <= 70f; angle += 7f)
                {
                    Vector3 at = post + Quaternion.Euler(0f, angle, 0f) * forward * distance;
                    at.y = 0.2f;
                    if (NavMesh.SamplePosition(at, out NavMeshHit hit, 2.5f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - 0.1f) < 0.8f)
                        samples.Add(hit.position + Vector3.up * 1.5f);
                }
            int Seen(Vector3 feet)
            {
                int n = 0;
                foreach (Vector3 s in samples)
                    if (EnemyCover.Visible(feet, s))
                        n++;
                return n;
            }
            int baseline = Seen(post);
            int needed = Mathf.Max(2, Mathf.CeilToInt(baseline * k_KeepRatio));
            var path = new NavMeshPath();
            var posts = new List<(Vector3, string)> { (post, " · 원래 자리") };
            var line = new StringBuilder();
            int windows = 0;
            // 같은 건물 · 같은 벽면 · 같은 층의 창을 왼쪽부터
            var same = new List<Pane>();
            foreach (Pane p in panes)
                if (p.building == mine.building && Mathf.Abs(Vector3.Dot(p.normal, forward)) > 0.9f
                    && Mathf.Abs(Vector3.Dot(p.centre - mine.centre, forward)) < 0.4f && Mathf.Abs(p.centre.y - mine.centre.y) < 0.5f)
                    same.Add(p);
            same.Sort((a, b) => Vector3.Dot(a.centre, right).CompareTo(Vector3.Dot(b.centre, right)));
            foreach (Pane p in same)
            {
                windows++;
                // 원래 자리가 선 창은 그 자리가 후보다 (0번)
                if (Mathf.Abs(Vector3.Dot(p.centre - post, right)) <= p.halfWidth + 0.35f)
                {
                    line.Append($" [원래 {baseline}]");
                    continue;
                }
                float inset = Mathf.Max(0f, p.halfWidth - k_EdgeInset);
                Vector3 best = Vector3.zero;
                int bestSeen = -1;
                string why = "설 곳 없음";
                foreach (float offset in new[] { 0f, inset, -inset })
                {
                    Vector3 at = p.centre - forward * ownAhead + right * offset;
                    at.y = post.y;
                    if (!NavMesh.SamplePosition(at, out NavMeshHit hit, 0.35f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - post.y) > 0.3f)
                        continue;
                    at = hit.position;
                    // 가구 · 벽에 끼지 않나 (무릎 위 ~ 머리)
                    if (Physics.CheckCapsule(at + Vector3.up * 0.55f, at + Vector3.up * 1.5f, 0.22f, Gunfire.ShotMask, QueryTriggerInteraction.Ignore))
                    {
                        why = "가구에 낌";
                        continue;
                    }
                    if (!NavMesh.CalculatePath(post, at, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                    {
                        why = "원래 자리에서 길이 없음";
                        continue;
                    }
                    int seen = Seen(at);
                    if (seen > bestSeen)
                    {
                        bestSeen = seen;
                        best = at;
                    }
                }
                if (bestSeen < 0)
                {
                    line.Append($" [뺌: {why}]");
                    continue;
                }
                if (bestSeen < needed)
                {
                    line.Append($" [뺌: 시야 {bestSeen}]");
                    continue;
                }
                posts.Add((best, ""));
                line.Append($" [{bestSeen}]");
            }
            report.AppendLine($"{mover.name}: {mine.building.name.Substring(2)} 창 {windows}개 → 자리 {posts.Count}곳 (보이는 길 표본, 원래 {baseline} · 기준 {needed} 이상){line}");
            return posts;
        }
    }
}
