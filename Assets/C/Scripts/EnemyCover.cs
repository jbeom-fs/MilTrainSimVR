using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace MilTrainSimVR.UrbanCombat
{
    // 엄폐할 곳 찾기 (11/16 — 사용자: "콜옵처럼 적이 일정 시간마다 엄폐"). EnemyMover 가 부른다.
    //   · 숨을 자리: 지금 자리 둘레 8m 안(0.5m 격자)의 길찾기 지도 위 점 가운데, 같은 층이고 플레이어 쪽으로 다가가지 않으며,
    //     플레이어 눈에서 그 자리에 선 적의 머리 · 가슴 · 양 어깨가 모두 벽 · 차 · 엄폐물에 가리는 곳. 그중 가까운 것부터 길로 가 보고
    //     길이 짧은(곧은 거리의 1.6배 안) 첫 곳. 다른 적이 이미 잡은 자리(1.2m 안)는 피한다
    //   · 나와 쏠 자리: 쏘던 자리가 3m 안이면 거기 (창가를 지키던 적은 창 옆 벽 뒤에 숨었다가 창으로 돌아온다).
    //     멀면 숨을 자리 둘레(0.8 ~ 2.8m)에서 플레이어가 보이는 가장 가까운 점 — 모퉁이에서 고개를 내미는 것처럼
    //   · 11/16 무릎 쏴 자세(Crouch Idle)가 들어와 낮은 엄폐(차 · 모래주머니 · 콘크리트 블록 · 가판대)도 쓴다:
    //     서서 가려지면 서서 숨고(Taking Cover Idle), 앉아야만 가려지면 앉아 숨는다. 앉아 숨은 자리에서 서면 보이면 그 자리에서 일어나 쏜다(콜옵).
    //     지금 자리에서 앉기만 해도 가려지면(낮은 엄폐물 바로 뒤에서 쏘던 중) 움직이지 않고 그 자리에 앉는다 — 일어나 쏘고 앉아 숨기를 되풀이
    //   · 사람(적 · 플레이어 몸)과 굴러다니는 물건(떨어진 총 · 탄창)은 가리는 것으로 치지 않는다. 'Ignore Raycast'(맵 끝 보이지 않는 벽)도
    public static class EnemyCover
    {
        const float k_Head = 1.55f, k_Chest = 1.15f, k_Shoulder = 0.25f;   // 서서 가려야 하는 곳 (발 기준 높이, 어깨 반 폭)
        // 무릎 쏴 자세로 가려야 하는 곳 — Crouch Idle 의 머리 뼈 0.84m(정수리 1.04m) · 가슴 뼈 0.70m (11/16 Unity 에서 잼)
        const float k_CrouchHead = 1.05f, k_CrouchChest = 0.7f, k_CrouchShoulder = 0.2f;
        const float k_SameFloor = 0.6f;
        const float k_ClaimRadius = 1.2f;
        const float k_GridStep = 0.5f;       // 숨을 자리를 찾는 격자 간격
        const float k_ReturnToPost = 3f;     // 쏘던 자리가 숨은 곳에서 이 안이면 거기로 나온다
        static readonly float[] k_PeekRings = { 0.8f, 1.4f, 2f, 2.8f };

        static readonly Dictionary<EnemyMover, Vector3> s_Claims = new Dictionary<EnemyMover, Vector3>();
        static readonly RaycastHit[] s_Hits = new RaycastHit[16];
        static readonly List<(Vector3 point, float distance, bool crouch)> s_Candidates = new List<(Vector3, float, bool)>();
        static NavMeshPath s_Path;

        // 엄폐가 끝났다(나오기 시작 · 전사) — 잡아 둔 자리를 놓는다
        public static void Release(EnemyMover who) => s_Claims.Remove(who);

        // from: 지금 발 자리, threatEye: 플레이어 눈 (마지막으로 안 자리). crouch: 앉아야 가려지는 곳
        public static bool Find(EnemyMover who, Vector3 from, Vector3 threatEye, float radius, out Vector3 cover, out Vector3 peek, out bool crouch)
        {
            s_Path ??= new NavMeshPath();
            cover = peek = from;
            crouch = false;
            // 낮은 엄폐물 바로 뒤에서 쏘던 중 — 그 자리에 앉기만 하면 된다
            if (!Claimed(from, who) && Hidden(from, threatEye, true))
            {
                crouch = true;
                s_Claims[who] = from;
                return true;
            }
            float threatNow = Flat(from - threatEye).magnitude;
            s_Candidates.Clear();
            // 0.5m 격자로 촘촘히 — 처음엔 16방향 고리였는데 벽 끝 · 차 뒤 같은 좁은 틈(폭 1m 안팎)을 건너뛰었다 (11/16 시험: 공영주차장 적 19)
            int cells = Mathf.CeilToInt(radius / k_GridStep);
            for (int ix = -cells; ix <= cells; ix++)
            {
                for (int iz = -cells; iz <= cells; iz++)
                {
                    var offset = new Vector3(ix * k_GridStep, 0f, iz * k_GridStep);
                    float d2 = offset.sqrMagnitude;
                    if (d2 < 0.64f || d2 > radius * radius)
                        continue;
                    if (!NavMesh.SamplePosition(from + offset, out NavMeshHit hit, k_GridStep * 0.6f, NavMesh.AllAreas))
                        continue;
                    Vector3 q = hit.position;
                    if (Mathf.Abs(q.y - from.y) > k_SameFloor || Flat(q - from).sqrMagnitude < 0.64f)
                        continue;
                    if (Flat(q - threatEye).magnitude < threatNow - 1f || Claimed(q, who))
                        continue;
                    bool standing = Hidden(q, threatEye, false);
                    if (!standing && !Hidden(q, threatEye, true))
                        continue;
                    s_Candidates.Add((q, Flat(q - from).magnitude, !standing));
                }
            }
            s_Candidates.Sort((x, y) => x.distance.CompareTo(y.distance));
            bool found = false;
            int tried = 0, reachable = 0;
            foreach ((Vector3 point, float distance, bool crouch) c in s_Candidates)
            {
                // 길이 닿는 곳은 12곳까지, 길 찾기는 모두 40번까지 — 건물 속 섬 같은 못 가는 점(길이 '일부만')이 가까이 몰려 있으면
                // 그것만 보다 끝나서(11/16 공영주차장 적 19: 가까운 12곳이 다 N10 속) 못 가는 점은 앞의 12곳에 세지 않는다
                if (++tried > 40 || reachable >= 12)
                    break;
                if (!NavMesh.CalculatePath(from, c.point, NavMesh.AllAreas, s_Path) || s_Path.status != NavMeshPathStatus.PathComplete)
                    continue;
                reachable++;
                float length = PathLength(s_Path);
                if (length > Mathf.Max(c.distance * 1.6f, c.distance + 1.5f) || length > radius * 1.6f)
                    continue;
                cover = c.point;
                crouch = c.crouch;
                found = true;
                break;
            }
            if (!found)
                return false;
            // 앉아 숨은 자리에서 서기만 해도 보이면 그 자리에서 일어나 쏜다
            peek = crouch && Visible(cover, threatEye) ? cover : FindPeek(who, cover, from, threatEye);
            s_Claims[who] = cover;
            return true;
        }

        // 나와 쏠 자리 — 쏘던 자리가 가까우면(3m 안) 그대로 거기 (실제로 쏘던 곳이라 확실히 보인다. 11/16 시험: 0.7m 옆 '보일 것 같은' 점으로
        // 나왔다가 머리 높이 차이로 못 보고 2.5초 서 있었다). 멀면 숨을 자리 옆에서 플레이어가 보이는 가장 가까운 점
        static Vector3 FindPeek(EnemyMover who, Vector3 cover, Vector3 from, Vector3 threatEye)
        {
            if (Flat(from - cover).magnitude <= k_ReturnToPost)
                return from;
            Vector3 best = from;
            float bestDistance = Visible(from, threatEye) ? Flat(from - cover).magnitude : float.MaxValue;
            foreach (float ring in k_PeekRings)
            {
                if (ring >= bestDistance)
                    break;
                for (int i = 0; i < 12; i++)
                {
                    float a = i * Mathf.PI * 2f / 12f;
                    Vector3 p = cover + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * ring;
                    if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 0.5f, NavMesh.AllAreas))
                        continue;
                    Vector3 q = hit.position;
                    float d = Flat(q - cover).magnitude;
                    if (Mathf.Abs(q.y - cover.y) > k_SameFloor || d >= bestDistance || !Visible(q, threatEye))
                        continue;
                    // 벽 너머로 붙은 점(길로 멀리 돌아가야 하는 곳)은 빼고
                    if (!NavMesh.Raycast(cover, q, out _, NavMesh.AllAreas))
                    {
                        best = q;
                        bestDistance = d;
                    }
                }
            }
            return best;
        }

        // 그 자리에 선(crouch 면 무릎 쏴 자세로 앉은) 적의 머리 · 가슴 · 양 어깨가 플레이어 눈에서 모두 가려지나
        public static bool Hidden(Vector3 feet, Vector3 threatEye, bool crouch = false)
        {
            Vector3 side = Vector3.Cross(Vector3.up, Flat(feet - threatEye)).normalized * (crouch ? k_CrouchShoulder : k_Shoulder);
            Vector3 chest = feet + Vector3.up * (crouch ? k_CrouchChest : k_Chest);
            return Blocked(threatEye, feet + Vector3.up * (crouch ? k_CrouchHead : k_Head)) && Blocked(threatEye, chest)
                && Blocked(threatEye, chest + side) && Blocked(threatEye, chest - side);
        }

        // 그 자리에 선 적의 눈에서 플레이어가 보이나 (머리 높이 — 쏘려면 보여야 한다)
        public static bool Visible(Vector3 feet, Vector3 threatEye) => !Blocked(threatEye, feet + Vector3.up * k_Head);

        // 두 점 사이에 벽 · 바닥 · 차 · 엄폐물이 있나 — 사람 몸과 굴러다니는 물건은 건너뛴다
        static bool Blocked(Vector3 a, Vector3 b)
        {
            Vector3 d = b - a;
            float length = d.magnitude;
            if (length < 0.01f)
                return false;
            int count = Physics.RaycastNonAlloc(a, d / length, s_Hits, length, Gunfire.ShotMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider c = s_Hits[i].collider;
                if (c is CharacterController || c.attachedRigidbody != null || c.GetComponentInParent<CombatantHealth>() != null)
                    continue;
                return true;
            }
            return false;
        }

        static bool Claimed(Vector3 point, EnemyMover who)
        {
            foreach (KeyValuePair<EnemyMover, Vector3> claim in s_Claims)
            {
                if (claim.Key != who && claim.Key != null && (claim.Value - point).sqrMagnitude < k_ClaimRadius * k_ClaimRadius)
                    return true;
            }
            return false;
        }

        static float PathLength(NavMeshPath path)
        {
            float length = 0f;
            Vector3[] corners = path.corners;
            for (int i = 1; i < corners.Length; i++)
                length += Vector3.Distance(corners[i - 1], corners[i]);
            return length;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
