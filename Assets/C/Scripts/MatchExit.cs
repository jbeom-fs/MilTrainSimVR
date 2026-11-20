using System.Collections;
using System.Collections.Generic;
using MilTrainSimVR.A.Common;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.AI;

namespace MilTrainSimVR.UrbanCombat
{
    // 과정에서 나가기 — 연병장(허브, A 의 A_Hub 씬)으로 돌아간다 (11/12 사용자 결정, A 통지 Docs/A/1110_BC통지_허브연결.md 에 대한 C 쪽 구현):
    //   · 성공(섬멸 · 점령)하면 포탈이 열린다 — 끝난 자리(플레이어가 서 있던 곳) 근처에 하나, 출발 자리 근처에 하나. 둘 다 그 자리에서 10m 안.
    //     걸어 들어가거나 광선으로 고르면 넘어간다 (A 의 HubPortal 그대로 — 과정 ID 만 넣은 Variant C_HubPortal 을 세운다)
    //   · 실패(전사 · 적 점령 · 무승부)하면 포탈은 없고, 결과판의 '연병장으로' 버튼이 GoToHub 를 부른다 (MatchDisplay). 옆에 '다시하기'
    // 허브는 과정 ID(C_Urban)를 보고 '시가지 전투' 포탈 앞에 세워 준다. 화면 가림(A 의 ScreenFader)이 리그에 있으면 까맣게 한 뒤 넘어간다.
    // 플레이어 리그(C_XR Origin)에 붙는다 — 출발 자리 = 리그가 처음 선 자리. 경기 진행(MatchFlow)이 없는 씬에서는 아무것도 하지 않는다
    public class MatchExit : MonoBehaviour
    {
        // 허브의 '시가지 전투' 포탈과 같은 과정 ID (A 통지 2절)
        public const string CourseId = "C_Urban";

        [Tooltip("허브로 돌아가는 포탈 — A 의 HubPortal_ToHub 에 과정 ID 만 넣은 Variant (CombatAssetsBuilder 가 만든다)")]
        [SerializeField] GameObject m_PortalPrefab;

        [Tooltip("포탈을 세울 수 있는 가장 먼 거리(m) — 끝난 자리 · 출발 자리에서")]
        [SerializeField] float m_MaxDistance = 10f;

        [Tooltip("되도록 이만큼 떨어진 곳에 세운다(m) — 결과판(눈앞 1.5m)에 가리지 않고 몇 걸음이면 닿게")]
        [SerializeField] float m_PreferredDistance = 5f;

        [Tooltip("넘어가기 전에 화면이 까매지는 시간(초)")]
        [SerializeField] float m_FadeSeconds = 0.5f;

        // 자리를 찾을 때 먼저 볼 방향에서 벌려 가는 각도(°) — 앞쪽 좌우부터
        static readonly float[] k_Angles = { 0f, 35f, -35f, 70f, -70f, 105f, -105f, 140f, -140f, 180f };
        static readonly Collider[] s_Overlaps = new Collider[32];

        MatchFlow m_Flow;
        Transform m_Head;
        ScreenFader m_Fader;
        Vector3 m_StartPoint;
        Vector3 m_StartForward;
        bool m_Opened;
        bool m_Leaving;
        readonly List<GameObject> m_Portals = new List<GameObject>();

        // 지금 열려 있는 포탈들 (없으면 빈 목록)
        public IReadOnlyList<GameObject> Portals => m_Portals;

        // 허브 씬을 불러올 수 있는지 — 빌드에서는 허브가 빌드 목록에 있어야 한다
        public bool HubReady => CourseRoute.CanLoad(CourseRoute.HubScenePath);

        public static bool IsSuccess(MatchFlow.Outcome outcome)
        {
            return outcome == MatchFlow.Outcome.Annihilation || outcome == MatchFlow.Outcome.PlayerCapture;
        }

        void Start()
        {
            m_Flow = FindAnyObjectByType<MatchFlow>();
            if (m_Flow == null)
            {
                enabled = false;
                return;
            }
            XROrigin rig = GetComponent<XROrigin>();
            m_Head = rig != null && rig.Camera != null ? rig.Camera.transform : null;
            m_Fader = FindAnyObjectByType<ScreenFader>();
            m_StartPoint = Feet();
            m_StartForward = Flat(m_Head != null ? m_Head.forward : transform.forward);
            m_Flow.PhaseChanged += OnPhaseChanged;
        }

        void OnDestroy()
        {
            if (m_Flow != null)
                m_Flow.PhaseChanged -= OnPhaseChanged;
        }

        void OnPhaseChanged(MatchFlow.Phase phase)
        {
            if (phase == MatchFlow.Phase.Ended)
                OpenExits();
        }

        // 경기가 성공으로 끝났으면 포탈을 세운다 (한 번만). 열린 포탈 수를 돌려준다 — 결과판(MatchDisplay)이 먼저 불러도 된다
        public int OpenExits()
        {
            if (m_Opened || m_Flow == null || m_Flow.CurrentPhase != MatchFlow.Phase.Ended || !IsSuccess(m_Flow.Result))
                return m_Portals.Count;
            m_Opened = true;
            if (m_PortalPrefab == null)
            {
                Debug.LogWarning("[C] 허브 포탈 프리팹이 없어 출구를 세우지 못했다 — '전투 자산 만들기' 메뉴를 다시 누른다");
                return 0;
            }
            // 끝난 자리 — 눈앞 결과판 옆으로 보이게 보는 쪽에서 조금 비켜서
            Vector3 clear = Feet();
            Vector3 look = Flat(m_Head != null ? m_Head.forward : transform.forward);
            GameObject near = Place(clear, Quaternion.AngleAxis(35f, Vector3.up) * look, "끝난 자리");
            // 출발 자리 — 끝난 자리의 포탈이 이미 출발 자리에서 10m 안이면 하나로 충분하다
            if (near == null || Flat(near.transform.position - m_StartPoint).magnitude > m_MaxDistance)
                Place(m_StartPoint, Quaternion.AngleAxis(90f, Vector3.up) * m_StartForward, "출발 자리");
            Debug.Log($"[C] 출구 포탈 {m_Portals.Count}개 열림 (과정 {CourseId})");
            return m_Portals.Count;
        }

        // 결과판의 '연병장으로' 버튼 — 화면을 까맣게 하고 허브를 불러온다. 시험할 때 직접 불러도 된다
        public void GoToHub()
        {
            if (m_Leaving)
                return;
            if (!HubReady)
            {
                Debug.LogWarning($"[C] 허브 씬을 불러올 수 없다 — 빌드 목록에 {CourseRoute.HubScenePath} 가 있어야 한다");
                return;
            }
            m_Leaving = true;
            StartCoroutine(Leave());
        }

        IEnumerator Leave()
        {
            if (m_Fader != null)
                yield return m_Fader.FadeTo(1f, m_FadeSeconds);
            // 허브는 이 과정 ID 의 포탈 앞에 세워 준다 (A 의 포탈이 넘어갈 때와 같은 길)
            CourseRoute.Enter(CourseId, CourseRoute.HubScenePath);
        }

        // 리그가 서 있는 바닥 자리 — 머리 바로 밑
        Vector3 Feet()
        {
            Vector3 head = m_Head != null ? m_Head.position : transform.position;
            return new Vector3(head.x, transform.position.y, head.z);
        }

        // 수평 성분만
        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        // around 근처에 포탈 하나 — first 방향부터 찾는다. 설 자리가 없으면 around 바로 옆 걸을 수 있는 곳에라도 세운다
        GameObject Place(Vector3 around, Vector3 first, string label)
        {
            if (first.sqrMagnitude < 0.0001f)
                first = Vector3.forward;
            first.Normalize();
            if (!NavMesh.SamplePosition(around, out NavMeshHit origin, 2f, NavMesh.AllAreas))
            {
                Debug.LogWarning($"[C] {label} 근처에 걸을 수 있는 바닥이 없어 포탈을 세우지 못했다");
                return null;
            }
            if (!FindSpot(origin.position, first, out Vector3 position))
            {
                // 좁은 곳 — 가릴 수 있어도 가까이에 세운다 (출구가 없는 것보다 낫다)
                NavMesh.SamplePosition(origin.position + first * 2.5f, out NavMeshHit fallback, 2.5f, NavMesh.AllAreas);
                position = fallback.hit ? fallback.position : origin.position;
                Debug.LogWarning($"[C] {label} 근처에 포탈이 설 빈자리를 못 찾아 가까운 곳에 세웠다");
            }
            Vector3 toward = Flat(origin.position - position);
            Quaternion facing = toward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(toward.normalized, Vector3.up) : Quaternion.LookRotation(-first, Vector3.up);
            GameObject portal = Instantiate(m_PortalPrefab, position, facing);
            portal.name = $"출구 포탈 · {label}";
            m_Portals.Add(portal);
            return portal;
        }

        // origin(걸을 수 있는 점)에서 m_MaxDistance 안, 같은 층에서 포탈이 설 자리:
        // 걸어서 갈 수 있고(벽 너머가 아니고), 렌즈 자리와 그 앞 한 걸음이 비어 있는 곳. 방향은 first 부터 좌우로, 거리는 m_PreferredDistance 부터
        bool FindSpot(Vector3 origin, Vector3 first, out Vector3 position)
        {
            float[] radii =
            {
                m_PreferredDistance, m_PreferredDistance - 1.5f, m_PreferredDistance + 1.5f,
                m_PreferredDistance - 2.5f, m_PreferredDistance + 3f, m_MaxDistance - 0.5f,
            };
            var path = new NavMeshPath();
            foreach (float radius in radii)
            {
                if (radius < 2f || radius > m_MaxDistance)
                    continue;
                foreach (float angle in k_Angles)
                {
                    Vector3 p = origin + Quaternion.AngleAxis(angle, Vector3.up) * first * radius;
                    if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 0.8f, NavMesh.AllAreas))
                        continue;
                    Vector3 offset = hit.position - origin;
                    // 같은 층 · 10m 안 · 너무 가깝지 않게
                    if (Mathf.Abs(offset.y) > 0.6f || Flat(offset).magnitude > m_MaxDistance || Flat(offset).magnitude < 2f)
                        continue;
                    if (!NavMesh.CalculatePath(origin, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete
                        || Length(path) > Flat(offset).magnitude * 2f + 3f)
                        continue;
                    Quaternion facing = Quaternion.LookRotation(Flat(-offset).normalized, Vector3.up);
                    if (Blocked(hit.position, facing))
                        continue;
                    position = hit.position;
                    return true;
                }
            }
            position = default;
            return false;
        }

        static float Length(NavMeshPath path)
        {
            float length = 0f;
            Vector3[] corners = path.corners;
            for (int i = 1; i < corners.Length; i++)
                length += Vector3.Distance(corners[i - 1], corners[i]);
            return length;
        }

        // 포탈이 거기 서면 벽 · 가구 · 차에 걸리는지 — 렌즈와 테(폭 2.1 · 바닥 0.35 ~ 2.95m)와, 그 앞에 사람이 다가설 자리
        static bool Blocked(Vector3 position, Quaternion facing)
        {
            return Overlaps(position + Vector3.up * 1.65f, new Vector3(1.05f, 1.3f, 0.35f), facing)
                || Overlaps(position + facing * Vector3.forward * 1.0f + Vector3.up * 1.25f, new Vector3(0.6f, 0.9f, 0.7f), facing);
        }

        // 그 상자에 움직이지 않는 것이 있는지 — 사람(쓰러진 적 포함)과 굴러다니는 물건(떨어진 총 · 탄창)은 뺀다
        static bool Overlaps(Vector3 center, Vector3 halfExtents, Quaternion rotation)
        {
            int count = Physics.OverlapBoxNonAlloc(center, halfExtents, s_Overlaps, rotation, Gunfire.ShotMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider c = s_Overlaps[i];
                if (c.attachedRigidbody != null || c is CharacterController || c.GetComponentInParent<CombatantHealth>() != null)
                    continue;
                return true;
            }
            return count >= s_Overlaps.Length;
        }
    }
}
