using UnityEngine;
using UnityEngine.AI;

namespace MilTrainSimVR.UrbanCombat
{
    // 적이 걷는다 (10/27) — 길찾기 지도(GreyboxNavMesh) 위를 NavMeshAgent 로 움직인다. 보기·겨누기·쏘기는 EnemyShooter 그대로.
    //   · 평소: 경로(EnemyRoute)의 점이 하나면 그 자리를 지키고, 둘 이상이면 순찰한다.
    //     걸을 땐 가는 쪽을, 점에 서 있을 땐 그 점이 보는 쪽을 본다
    //   · 플레이어를 보면: 그 자리에 멈춰 서서 쏜다 (몸 돌리기·쏘기는 EnemyShooter)
    //   · 안 보이게 되면: 잠깐 그대로 있다가 — 순찰하던 적(그리고 '경계하면 자리를 떠남'을 켠 적)은 마지막으로 안 곳 쪽으로
    //     뛰어가 본다(조사). 거기서 둘러봐도 없으면 경계를 풀고 원래 길로 돌아간다. 자리 지키는 적은 그 자리에서 그쪽만 본다
    //     ('경계해도 순찰 유지'를 켠 순찰병은 조사하러 가지 않고 순찰을 이어 간다 — 11/17 적 10 백화점 2·3층 순찰)
    //   · 오버타임: HoldAt(자리) — 그 자리로 뛰어가 지킨다(조사하러 떠나지 않는다). 경기 진행(나중)이 부른다
    //   · 지휘 규칙(EnemyDirector): Chase(따라가기) · ChaseVia(점들을 거쳐 따라가기 — 에스컬레이터로 내려오기)
    //     · MoveToCover(엄폐하며 자리 옮기기 — 11/18 적 6: 창에서 초기 대응 뒤 나와서 승합차 3 뒤에 엄폐)
    //   · 한 번도 안 맞았을 때만 뛴다. 맞은 뒤(부상·중상)로는 뛰라고 할 때도 보통 걸음으로 걷는다 — 다친 걸음은 넣지 않는다 (11/03 사용자).
    //     전사하면 멈춘다. 다시하기(정상으로)면 처음 자리로
    //   · 엄폐 (11/16 사용자: "콜옵처럼 적이 일정 시간마다 엄폐") — EnemyShooter 가 엄폐하고 싶다고 하면(점사 몇 번 · 탄창 교체 · 총알이 스침)
    //     둘레 8m 안에서 플레이어 눈에 안 보이는 자리(EnemyCover)로 뛰어가 2~3.5초 숨었다가(탄창은 이때 갈아 끼운다), 플레이어가 보이는
    //     가까운 자리(처음 쏘던 자리 또는 엄폐 자리 옆 모퉁이)로 나와 다시 쏜다. 가고 숨는 동안엔 보여도 서서 쏘지 않는다 — 나오는 길에 보이면 바로 멈춰 쏜다.
    //     엄폐할 곳이 없으면 그 자리에서 싸운다(탄창은 그 자리에서).
    //   · 11/16 엄폐 애니메이션: 높은 엄폐는 서서(웅크림), 낮은 엄폐(차 · 모래주머니)는 무릎 쏴 자세로 앉아 숨는다 (EnemyCover 가 고른다).
    //     숨을 자리에 닿으면 탄창을 갈아 끼운다(EnemyShooter.StartCoverReload). 숨을 자리 · 나와 쏠 자리가 3m 안이면 뛰지 않고
    //     플레이어 쪽을 겨눈 채 옆걸음으로 간다 (모퉁이에서 옆으로 나왔다 들어가기) — 숨으러 갈 땐 옆으로 가는 길일 때만
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(EnemyShooter))]
    public class EnemyMover : MonoBehaviour
    {
        [Tooltip("지킬 자리(점 하나) 또는 순찰 길(점 둘 이상). 비우면 처음 선 자리를 지킨다")]
        [SerializeField] EnemyRoute m_Route;

        [Tooltip("매판 시작 때 이 가운데 한 자리를 골라 거기서 지킨다 — 창가 적: 같은 층 같은 쪽의 창들 (11/19). 비우면 늘 경로의 자리. 자리를 지키는 적(경로의 점이 하나)만 쓴다")]
        [SerializeField] Transform[] m_PostChoices = new Transform[0];

        [Header("빠르기")]
        [Tooltip("평소 걷기 (m/초)")]
        [SerializeField] float m_WalkSpeed = 1.4f;
        [Tooltip("경계 중·명령받고 갈 때 뛰기 (m/초)")]
        [SerializeField] float m_RunSpeed = 3.2f;
        [Tooltip("걸을 때 몸 돌리는 빠르기(°/초)")]
        [SerializeField] float m_TurnSpeed = 240f;

        [Header("경계")]
        [Tooltip("자리 지키는 적도 경계하면(총소리·피격) 자리를 떠나 그쪽으로 가 본다")]
        [SerializeField] bool m_LeavePostWhenAlerted;
        [Tooltip("순찰하는 적이 경계해도(총소리·피격) 조사하러 떠나지 않는다 — 보이면 서서 쏘고, 놓치면 순찰을 이어 간다. 자리는 지휘 규칙 · 오버타임으로만 옮긴다 (11/17 적 10 백화점 순찰)")]
        [SerializeField] bool m_StayOnRouteWhenAlerted;
        [Tooltip("플레이어가 안 보이게 된 뒤 이만큼(초) 그 자리에 있다가 움직인다")]
        [SerializeField] float m_HoldAfterLostSeconds = 2.5f;
        [Tooltip("조사: 마지막으로 안 곳에서 이만큼(m) 안에 들어가면 멈춰서 둘러본다")]
        [SerializeField] float m_SearchStopDistance = 4f;
        [Tooltip("조사: 그곳에서 둘러보는 시간(초) — 그래도 못 찾으면 경계를 풀고 돌아간다")]
        [SerializeField] float m_SearchLookSeconds = 6f;

        [Header("엄폐 (11/16)")]
        [Tooltip("엄폐할 곳을 찾는 거리(m) — 이 안에서 플레이어 눈에 안 보이는 가장 가까운 곳")]
        [SerializeField] float m_CoverRadius = 8f;
        [Tooltip("엄폐해 있는 시간(초) — 이 사이에서 무작위 (탄창이 비었으면 갈아 끼우는 시간보다 짧지 않다)")]
        [SerializeField] Vector2 m_HideSeconds = new Vector2(2f, 3.5f);
        [Tooltip("엄폐하러 가는 길이 이만큼(초) 넘게 걸리면 그 자리에서 숨은 것으로 친다 — 길이 막혀 헤매지 않게")]
        [SerializeField] float m_CoverTravelLimit = 5f;
        [Tooltip("숨을 자리 · 나와 쏠 자리가 이 거리(m) 안이면 플레이어 쪽을 겨눈 채 옆걸음으로 간다")]
        [SerializeField] float m_StrafeMaxDistance = 3f;
        [Tooltip("옆걸음 빠르기 (m/초)")]
        [SerializeField] float m_StrafeSpeed = 1.3f;

        enum Mode { Route, Engage, Search, Cover, Dead }
        enum CoverStep { Going, Hiding, Returning }

        NavMeshAgent m_Agent;
        EnemyShooter m_Shooter;
        CombatantHealth m_Health;
        Mode m_Mode = Mode.Route;
        int m_Index;
        int m_Step = 1;
        float m_WaitUntil = -1f;      // 순찰 점에 서 있다가 다음 점으로 갈 시각 (아직 안 섰으면 -1)
        float m_LostAt = -1f;         // 플레이어가 안 보이게 된 시각
        float m_SearchUntil = -1f;    // 조사한 곳에서 둘러보기를 끝낼 시각 (아직 도착 전이면 -1)
        Transform m_Order;            // 명령받은 지킬 자리 (오버타임 점령지 등) — 추적이면 플레이어 발 자리
        bool m_Chasing;               // 명령이 '따라가기'다 — 엄폐하고 나올 자리를 그 점으로 잡지 않는다
        Transform[] m_Via;            // 거쳐 갈 점들 (ChaseVia) — 다 지나면 m_ViaTarget 을 따라간다
        int m_ViaIndex;
        Transform m_ViaTarget;
        Vector3 m_Destination;        // 지금 가는 곳 (길찾기 지도 위로 붙인 점)
        bool m_Going;
        Vector3 m_StartPosition;
        Quaternion m_StartRotation;
        CoverStep m_CoverStep;
        Vector3 m_CoverPoint;         // 숨을 자리
        Vector3 m_PeekPoint;          // 숨었다가 나와 쏠 자리
        float m_CoverStepSince;       // 지금 단계(가기 · 숨기 · 나오기)를 시작한 시각
        float m_HideUntil;
        float m_StopDistance;         // 길찾기의 원래 멈추는 거리 (엄폐로 움직일 때만 줄인다)
        bool m_CoverCrouch;           // 앉아야 가려지는 엄폐
        float m_CoverGoLimit;         // 숨을 자리로 가는 길 시간 한도 — 보통 m_CoverTravelLimit, 자리 옮기기(MoveToCover)는 더 길게

        const float k_RelocateCoverRadius = 3f;    // 자리 옮기기: 새 자리 이 안에서 숨을 곳을 찾는다 (밴 바로 뒤 — 멀리 엉뚱한 데로 가지 않게)
        const float k_RelocateTravelLimit = 15f;   // 자리 옮기기: 다쳐서 걸어가도 닿을 만큼

        public bool IsMoving => m_Going && !m_Agent.isStopped && m_Agent.desiredVelocity.sqrMagnitude > 0.01f;
        // 숨어 있다 — 앉아서(낮은 엄폐) / 서서(높은 엄폐). EnemySoldier 가 자세를 고른다
        public bool Crouching => m_Mode == Mode.Cover && m_CoverStep == CoverStep.Hiding && m_CoverCrouch;
        public bool HidingStanding => m_Mode == Mode.Cover && m_CoverStep == CoverStep.Hiding && !m_CoverCrouch;
        // 플레이어 쪽을 겨눈 채 옆걸음으로 가는 중 (엄폐로 들어가기 · 나오기)
        public bool Strafing { get; private set; }
        public string ModeName => m_Mode == Mode.Cover ? "Cover." + m_CoverStep : m_Mode.ToString();
        public int CoversTaken { get; private set; }    // 엄폐한 횟수 (시험 · 기록용)
        public int CoversMissed { get; private set; }   // 엄폐할 곳을 못 찾은 횟수
        public Vector3 CoverPoint => m_CoverPoint;
        public Vector3 PeekPoint => m_PeekPoint;
        public EnemyRoute Route { get => m_Route; set { m_Route = value; m_Index = 0; m_Step = 1; m_WaitUntil = -1f; } }

        void Awake()
        {
            m_Agent = GetComponent<NavMeshAgent>();
            m_Shooter = GetComponent<EnemyShooter>();
            m_Health = GetComponent<CombatantHealth>();
            // 몸은 여기(평소)와 EnemyShooter(경계)가 돌린다 — 길찾기가 돌리면 둘이 싸운다
            m_Agent.updateRotation = false;
            m_StopDistance = m_Agent.stoppingDistance;
            PickPost();
            m_StartPosition = transform.position;
            m_StartRotation = transform.rotation;
            m_Health.StateChanged += OnStateChanged;
        }

        // 이번 판에 고른 자리 (m_PostChoices 의 번호. 고르지 않았으면 -1) — 시험 · 기록용
        public int PostChoice { get; private set; } = -1;

        // 창가 적은 매판 다른 창에서 (11/19 사용자: "창문에 있는 적이 위치가 고정되어 있다보니, 계속 하다보면 위치를 쉽게 특정할 수 있어서 …
        // 매판마다 바라보고 있는 쪽의 다른 창문에 있으면 좋겠음"). 후보 자리(같은 층 같은 쪽 창 — EnemyWindowPostBuilder 가 추려 넣는다) 가운데
        // 하나를 골라, 경로의 점(지킬 자리)과 몸을 거기로 옮긴다. 그 뒤로는 그 자리가 이 적의 자리다 — 엄폐하고 돌아올 곳 · 놓치면 돌아갈 곳.
        // 다시하기는 씬을 새로 불러오니 그때 다시 고른다. 다른 적이 서 있는 자리(1.2m 안)는 고르지 않는다
        void PickPost()
        {
            if (m_PostChoices == null || m_PostChoices.Length == 0 || m_Route == null || m_Route.Count != 1)
                return;
            EnemyMover[] others = FindObjectsByType<EnemyMover>(FindObjectsSortMode.None);
            int start = Random.Range(0, m_PostChoices.Length);
            for (int k = 0; k < m_PostChoices.Length; k++)
            {
                int index = (start + k) % m_PostChoices.Length;
                Transform post = m_PostChoices[index];
                if (post == null)
                    continue;
                bool taken = false;
                foreach (EnemyMover other in others)
                    taken |= other != this && (other.transform.position - post.position).sqrMagnitude < 1.44f;
                if (taken)
                    continue;
                m_Route.Point(0).SetPositionAndRotation(post.position, post.rotation);
                if (m_Agent.isOnNavMesh)
                    m_Agent.Warp(post.position);
                else
                    transform.position = post.position;   // 길찾기 지도가 아직 안 켜졌다 — Start 에서 지도 위로 올린다
                transform.rotation = post.rotation;
                PostChoice = index;
                return;
            }
        }

#if UNITY_EDITOR
        // 골라 볼 자리들 — 적을 고르면 씬에 주황 점과 보는 방향으로 보인다
        void OnDrawGizmosSelected()
        {
            if (m_PostChoices == null)
                return;
            Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.9f);
            foreach (Transform post in m_PostChoices)
            {
                if (post == null)
                    continue;
                Vector3 p = post.position + Vector3.up * 0.2f;
                Gizmos.DrawSphere(p, 0.3f);
                Gizmos.DrawLine(p, p + Vector3.ProjectOnPlane(post.forward, Vector3.up).normalized * 2f);
            }
        }
#endif

        void Start()
        {
            // 길찾기 지도가 늦게 켜졌으면 지도 위로 올려 놓는다
            if (!m_Agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                m_Agent.Warp(hit.position);
        }

        // 꺼지면(경기 시작 전·끝난 뒤 — MatchFlow) 그 자리에 선다. 다시 켜지면 처음부터 길을 찾는다
        void OnDisable()
        {
            if (m_Agent != null && m_Agent.enabled)
                Halt();
            m_Going = false;
            if (m_Shooter != null)
                m_Shooter.Moving = false;
            if (m_Mode == Mode.Cover && m_Shooter != null)
                EndCover(Mode.Route);
        }

        void OnDestroy()
        {
            if (m_Health != null)
                m_Health.StateChanged -= OnStateChanged;
        }

        // 오버타임 등: 그 자리로 뛰어가 지킨다 — 보이면 쏘고, 안 보이면 조사하러 가지 않고 그 자리로 돌아온다
        public void HoldAt(Transform spot)
        {
            m_Order = spot;
            m_Chasing = false;
            m_Via = null;
            if (m_Mode == Mode.Search)
                m_Mode = Mode.Route;
        }

        // 추적 (11/17, EnemyDirector): 움직이는 점(플레이어 발 자리)을 뛰어서 따라간다 — 보이면 멈춰 쏘고, 놓치면 다시 따라간다.
        // HoldAt 과 같은 길(명령받은 자리)을 쓰고, 점이 1m 넘게 옮기면 길을 다시 찾는다(GoTo). 오버타임의 HoldAt 이 오면 그쪽으로 바뀐다
        public void Chase(Transform target)
        {
            HoldAt(target);
            m_Chasing = true;
        }

        // 점들을 차례로 뛰어 지난 뒤 추적 (11/17, EnemyDirector — 적 10 이 에스컬레이터로 1층에 내려온다).
        // 가장 짧은 길(모서리 계단)이 아니라 그 점들(에스컬레이터 위 · 아래)을 거치게 한다. 지금 층과 같은 높이(1.2m 안)의 첫 점부터 —
        // 그런 점이 없으면(이미 아래층 · 에스컬레이터 위) 바로 따라간다. 가는 길에 보이면 멈춰 쏘고, 놓치면 남은 점으로 다시 간다
        public void ChaseVia(Transform[] via, Transform target)
        {
            int start = -1;
            for (int i = 0; via != null && i < via.Length; i++)
            {
                if (via[i] != null && Mathf.Abs(via[i].position.y - transform.position.y) < 1.2f)
                {
                    start = i;
                    break;
                }
            }
            if (start < 0)
            {
                Chase(target);
                return;
            }
            HoldAt(via[start]);
            m_Via = via;
            m_ViaIndex = start;
            m_ViaTarget = target;
        }

        // 엄폐하며 자리 옮기기 (11/18 사용자 — 적 6 이 창에서 초기 대응한 뒤 나와서 승합차 3 에 엄폐): 그 자리를 지킬 자리로 받고(HoldAt),
        // 그 자리 3m 안에서 플레이어 눈에 안 보이는 곳(밴 뒤)으로 바로 뛰어가 숨는다 — 가는 동안엔 보여도 서서 쏘지 않는다(엄폐하러 갈 때와 같다).
        // 숨었다가 나와 쏠 자리는 그 자리. 그 뒤로는 HoldAt 과 같다 (그 둘레에서 엄폐하고 그 자리로 나온다).
        // 경계 중이 아니거나 숨을 곳이 없으면(플레이어가 바로 옆까지 왔다) HoldAt 만 — 그 자리로 뛰어가되 보이면 멈춰 쏜다
        public void MoveToCover(Transform spot)
        {
            if (m_Mode == Mode.Dead || spot == null)
                return;
            if (m_Mode == Mode.Cover)
                EndCover(Mode.Route);   // 창 옆에 숨으러 가던 중 — 그만두고 새 자리로
            HoldAt(spot);
            Vector3 post = Snap(spot.position);
            Vector3 threat = m_Shooter.LastKnown + Vector3.up * 0.35f;
            if (!m_Shooter.Alerted || !EnemyCover.Find(this, post, threat, k_RelocateCoverRadius, out m_CoverPoint, out _, out m_CoverCrouch))
                return;
            m_PeekPoint = post;
            m_Shooter.BeginCover();
            m_Mode = Mode.Cover;
            m_CoverStep = CoverStep.Going;
            m_CoverStepSince = Time.time;
            m_CoverGoLimit = k_RelocateTravelLimit;
            m_LostAt = m_SearchUntil = -1f;
            Strafing = false;
            CoversTaken++;
        }

        public bool Chasing => m_Chasing && m_Order != null;
        public bool HasOrder => m_Order != null;
        public bool GoingVia => m_Via != null;

        public void ClearOrder()
        {
            m_Order = null;
            m_Chasing = false;
            m_Via = null;
        }

        void OnStateChanged(CombatantHealth who, CombatantHealth.State state, HitZone.Part? part)
        {
            if (state == CombatantHealth.State.Dead)
            {
                // 쓰러지는 모습은 EnemySoldier 가 맡는다 — 길찾기는 끈다
                Halt();
                m_Agent.enabled = false;
                m_Shooter.Moving = false;
                Strafing = false;
                if (m_Mode == Mode.Cover)
                    EnemyCover.Release(this);
                m_Mode = Mode.Dead;
            }
            else if (state == CombatantHealth.State.Healthy && m_Mode == Mode.Dead)
            {
                // 다시하기 — 처음 자리·처음 경로로
                m_Agent.enabled = true;
                m_Agent.Warp(m_StartPosition);
                transform.rotation = m_StartRotation;
                m_Mode = Mode.Route;
                m_Order = null;
                m_Chasing = false;
                m_Via = null;
                Route = m_Route;
                m_LostAt = m_SearchUntil = -1f;
            }
        }

        void Update()
        {
            if (m_Mode == Mode.Dead || !m_Agent.isOnNavMesh)
                return;

            // 엄폐 — 쏘는 쪽(EnemyShooter)이 엄폐하고 싶다고 하면 숨을 곳을 찾는다. 엄폐 중엔 아래(보이면 멈춰 쏘기 · 조사)를 하지 않는다
            if (m_Mode != Mode.Cover && m_Shooter.CoverWanted)
                TryStartCover();
            if (m_Mode == Mode.Cover)
            {
                UpdateCover();
                Face();
                m_Shooter.Moving = IsMoving;
                return;
            }

            if (m_Shooter.SeesPlayer)
            {
                // 보이면 멈춰 서서 쏜다
                m_Mode = Mode.Engage;
                m_LostAt = -1f;
                Halt();
            }
            else if (m_Mode == Mode.Engage)
            {
                // 방금 안 보이게 됐다 — 잠깐 그대로 있다가 조사하러 가거나 제자리로
                if (m_LostAt < 0f)
                    m_LostAt = Time.time;
                if (Time.time - m_LostAt >= m_HoldAfterLostSeconds)
                    StartSearchOrReturn();
            }
            else if (m_Mode == Mode.Route && m_Shooter.Alerted && WantsToSearch)
            {
                StartSearchOrReturn();   // 총소리를 듣거나 맞았다 (못 봤다)
            }

            if (m_Mode == Mode.Search)
                UpdateSearch();
            else if (m_Mode == Mode.Route)
                UpdateRoute();
            Face();
            m_Shooter.Moving = IsMoving;
        }

        // ---------------------------------------------------------------- 엄폐

        void TryStartCover()
        {
            // 플레이어 눈 = 마지막으로 안 가슴 자리 + 35cm
            Vector3 threat = m_Shooter.LastKnown + Vector3.up * 0.35f;
            if (EnemyCover.Find(this, transform.position, threat, m_CoverRadius, out m_CoverPoint, out m_PeekPoint, out m_CoverCrouch))
            {
                // 자리를 지키는 적(점 하나짜리 경로 · 오버타임 자리)이 그 자리 둘레에서 쏘던 중이면 나올 곳은 늘 그 자리 —
                // 엄폐할 때마다 쏘던 자리가 조금씩 엄폐 쪽으로 밀리지 않게
                Transform post = CurrentPoint();
                if (post != null && !m_Chasing && (m_Order != null || m_Route == null || m_Route.Count < 2) && Flat(post.position - transform.position).magnitude < 1.5f)
                    m_PeekPoint = Snap(post.position);
                m_Shooter.BeginCover();
                m_Mode = Mode.Cover;
                m_CoverStep = CoverStep.Going;
                m_CoverStepSince = Time.time;
                m_CoverGoLimit = m_CoverTravelLimit;
                m_LostAt = m_SearchUntil = -1f;
                Strafing = WantsStrafe(m_CoverPoint, true);
                CoversTaken++;
            }
            else
            {
                m_Shooter.CoverUnavailable();   // 숨을 곳이 없다 — 그 자리에서 싸운다 (빈 탄창은 그 자리에서 갈아 끼운다)
                CoversMissed++;
            }
        }

        void UpdateCover()
        {
            if (!m_Shooter.Alerted)
            {
                EndCover(Mode.Route);   // 오래 아무 일 없어 경계가 풀렸다
                return;
            }
            float since = Time.time - m_CoverStepSince;
            switch (m_CoverStep)
            {
                case CoverStep.Going:
                    // 뛰어가는 동안엔 보여도 서지 않는다 (몸은 가는 쪽, 총은 내린 채. 옆걸음이면 플레이어 쪽을 겨눈 채)
                    if (ArriveExactly(m_CoverPoint, Strafing ? m_StrafeSpeed : m_RunSpeed) || since > m_CoverGoLimit)
                    {
                        Halt();
                        Strafing = false;
                        m_CoverStep = CoverStep.Hiding;
                        m_CoverStepSince = Time.time;
                        m_Shooter.StartCoverReload();
                        m_HideUntil = Time.time + Mathf.Max(Random.Range(m_HideSeconds.x, m_HideSeconds.y), m_Shooter.MinHideSeconds);
                    }
                    break;
                case CoverStep.Hiding:
                    // 숨어 있다 (몸은 플레이어 쪽 — EnemyShooter). 플레이어가 돌아 들어와 보이면(엄폐가 뚫렸으면) 갈아 끼우는 중이 아닌 한 바로 나와 쏜다.
                    // 앉아 있으면 앉은 눈 높이로 본다 (눈은 머리 뼈에 붙어 있다)
                    bool flanked = m_Shooter.SeesPlayer && since > 0.5f && !m_Shooter.ReloadingNow;
                    if (Time.time >= m_HideUntil || flanked)
                    {
                        m_Shooter.EndCover();
                        EnemyCover.Release(this);
                        if (flanked)
                        {
                            EndCover(Mode.Engage);
                            return;
                        }
                        m_CoverStep = CoverStep.Returning;
                        m_CoverStepSince = Time.time;
                        Strafing = WantsStrafe(m_PeekPoint, false);
                    }
                    break;
                case CoverStep.Returning:
                    // 나와 쏠 자리로 — 가는 길에 플레이어가 보이면 바로 멈춰 쏜다 (모퉁이에서 고개 내밀기).
                    // 앉아 숨었던 자리에서 서기만 해도 보이면 나와 쏠 자리가 그 자리다 — 바로 다 온 것이 되어 일어서서 쏜다
                    if (m_Shooter.SeesPlayer)
                    {
                        Halt();
                        EndCover(Mode.Engage);
                    }
                    else if (ArriveExactly(m_PeekPoint, Strafing ? m_StrafeSpeed : m_RunSpeed) || since > m_CoverTravelLimit)
                    {
                        Halt();
                        // 나왔는데 안 보인다 (플레이어가 옮겼거나 창틀 각도) — Engage 의 '잠깐 있다가 조사 · 제자리'를 2.5초가 아니라 0.5초 뒤에
                        EndCover(Mode.Engage);
                        m_LostAt = Time.time - Mathf.Max(0f, m_HoldAfterLostSeconds - 0.5f);
                    }
                    break;
            }
        }

        // 엄폐 자리 · 나와 쏠 자리는 그 점에 딱 서야 한다 — 길찾기는 멈추는 거리 앞에서 서서, 숨을 곳에선 몸이 반쯤 보이고
        // 쏘던 자리에선 창틀에 가려 못 보는 일이 있었다 (11/16 시험: 0.5m 앞에서 서서 엄폐할 때마다 쏘던 자리가 밀렸다).
        // 엄폐로 움직이는 동안만 멈추는 거리를 5cm 로 줄인다 (걷는 애니메이션은 길찾기 빠르기를 그대로 따른다)
        bool ArriveExactly(Vector3 point, float speed)
        {
            Vector3 d = point - transform.position;
            if (Mathf.Abs(d.y) < 1.2f && Flat(d).magnitude <= 0.15f)
            {
                m_Agent.stoppingDistance = m_StopDistance;
                return true;
            }
            m_Agent.stoppingDistance = 0.05f;
            GoTo(point, speed);
            // 길찾기가 다 왔다고 멈췄는데 조금 떨어져 있으면(지도 가장자리 등) 다 온 것으로 친다
            if (m_Going && !m_Agent.pathPending && m_Agent.remainingDistance <= 0.1f && m_Agent.velocity.sqrMagnitude < 0.01f)
            {
                m_Agent.stoppingDistance = m_StopDistance;
                return true;
            }
            return false;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        // 그 자리까지 옆걸음으로 갈까 — 3m 안이고, sidewaysOnly 면 플레이어 쪽과 45~135° 벌어진(옆으로 가는) 길일 때만.
        // 길찾기 경로가 꺾이면 실제 걸음 방향은 EnemySoldier 가 매 순간 보고 왼쪽 · 오른쪽 · 앞을 고른다
        bool WantsStrafe(Vector3 point, bool sidewaysOnly)
        {
            Vector3 d = Flat(point - transform.position);
            if (d.magnitude > m_StrafeMaxDistance || d.sqrMagnitude < 0.01f)
                return false;
            if (!sidewaysOnly)
                return true;
            float angle = Vector3.Angle(d, Flat(m_Shooter.LastKnown - transform.position));
            return angle > 45f && angle < 135f;
        }

        void EndCover(Mode next)
        {
            Strafing = false;
            m_Agent.stoppingDistance = m_StopDistance;
            if (m_Shooter.Holding)
                m_Shooter.EndCover();
            EnemyCover.Release(this);
            m_Mode = next;
            m_LostAt = -1f;
            m_WaitUntil = -1f;
        }

        // 순찰하는 적과 '경계하면 자리를 떠남'을 켠 적만 조사하러 간다. 명령받은 자리가 있으면 거기를 지킨다
        bool WantsToSearch => m_Order == null && (m_LeavePostWhenAlerted || (m_Route != null && m_Route.Count > 1 && !m_StayOnRouteWhenAlerted));

        void StartSearchOrReturn()
        {
            m_Mode = m_Shooter.Alerted && WantsToSearch ? Mode.Search : Mode.Route;
            m_SearchUntil = -1f;
            m_WaitUntil = -1f;
        }

        void UpdateSearch()
        {
            if (!m_Shooter.Alerted)
            {
                m_Mode = Mode.Route;   // 오래 아무 일 없어 경계가 풀렸다
                return;
            }
            Vector3 target = m_Shooter.LastKnown;
            if (m_SearchUntil < 0f)
            {
                Vector3 d = target - transform.position;
                if (new Vector2(d.x, d.z).magnitude <= m_SearchStopDistance && Mathf.Abs(d.y) < 2.5f || (m_Going && Reached(m_Destination)))
                {
                    Halt();
                    m_SearchUntil = Time.time + m_SearchLookSeconds;   // 다 왔다 — 둘러본다 (몸은 EnemyShooter 가 마지막 자리 쪽으로)
                }
                else
                {
                    GoTo(target, m_RunSpeed);
                }
            }
            else if (Time.time >= m_SearchUntil)
            {
                // 아무도 없다 — 경계를 풀고 원래 길로
                m_Shooter.CalmDown();
                m_Mode = Mode.Route;
            }
        }

        void UpdateRoute()
        {
            Transform point = CurrentPoint();
            Vector3 goal = point != null ? point.position : m_StartPosition;
            if (!Reached(Snap(goal)))
            {
                GoTo(goal, m_Order != null || m_Shooter.Alerted ? m_RunSpeed : m_WalkSpeed);
                m_WaitUntil = -1f;
                return;
            }
            if (m_Via != null)
            {
                // 거쳐 갈 점에 왔다 — 서지 않고 다음 점으로, 마지막이었으면 따라가기
                if (++m_ViaIndex < m_Via.Length && m_Via[m_ViaIndex] != null)
                {
                    m_Order = m_Via[m_ViaIndex];
                }
                else
                {
                    Transform target = m_ViaTarget;
                    m_Via = null;
                    Chase(target);
                }
                return;
            }
            if (m_Order == null && m_Route != null && m_Route.Count > 1 && m_Route.IsPass(Mathf.Min(m_Index, m_Route.Count - 1)))
            {
                // 길만 잡아 주는 점 — 서지 않고 다음 점으로
                m_Index = m_Route.Next(m_Index, ref m_Step);
                m_WaitUntil = -1f;
                return;
            }
            Halt();
            if (m_Order != null || m_Route == null || m_Route.Count < 2)
                return;   // 자리 지킴
            // 순찰: 점에서 잠깐 섰다가 다음 점으로
            if (m_WaitUntil < 0f)
                m_WaitUntil = Time.time + m_Route.RandomPause();
            else if (Time.time >= m_WaitUntil)
            {
                m_Index = m_Route.Next(m_Index, ref m_Step);
                m_WaitUntil = -1f;
            }
        }

        Transform CurrentPoint()
        {
            if (m_Order != null)
                return m_Order;
            return m_Route != null && m_Route.Count > 0 ? m_Route.Point(Mathf.Min(m_Index, m_Route.Count - 1)) : null;
        }

        void GoTo(Vector3 target, float speed)
        {
            m_Agent.speed = m_Health.Current == CombatantHealth.State.Healthy ? speed : Mathf.Min(speed, m_WalkSpeed);
            m_Agent.isStopped = false;
            Vector3 dest = Snap(target);
            if (!m_Going || (dest - m_Destination).sqrMagnitude > 1f)
            {
                m_Destination = dest;
                m_Going = m_Agent.SetDestination(dest);
            }
            else if (!m_Agent.pathPending && m_Agent.pathStatus == NavMeshPathStatus.PathPartial && m_Agent.remainingDistance < 1f)
            {
                // 아주 먼 길은 '일부만' 찾아질 때가 있다 — 그 끝에 오면 거기서 다시 찾는다
                m_Agent.SetDestination(m_Destination);
            }
        }

        void Halt()
        {
            if (m_Agent.isOnNavMesh)
            {
                m_Agent.isStopped = true;
                m_Agent.ResetPath();
            }
            m_Going = false;
        }

        // 그 점에 와 있나 — 수평 거리가 멈추는 거리 안이고 같은 층(높이 차 1.2m 안)
        bool Reached(Vector3 point)
        {
            Vector3 d = point - transform.position;
            float r = m_Agent.stoppingDistance + 0.25f;
            return Mathf.Abs(d.y) < 1.2f && d.x * d.x + d.z * d.z <= r * r;
        }

        // 길찾기 지도 위의 가장 가까운 점 (4m 안에 없으면 그대로)
        static Vector3 Snap(Vector3 p)
        {
            return NavMesh.SamplePosition(p, out NavMeshHit hit, 4f, NavMesh.AllAreas) ? hit.position : p;
        }

        // 걸을 땐 가는 쪽, 점에 서 있으면 그 점이 보는 쪽. 서서 경계 중일 땐 EnemyShooter 가 플레이어 쪽으로 돌린다
        void Face()
        {
            Vector3 look;
            if (IsMoving && Strafing)
            {
                look = m_Shooter.LastKnown - transform.position;   // 옆걸음 — 몸(총)은 플레이어 쪽
            }
            else if (IsMoving)
            {
                look = m_Agent.desiredVelocity;
            }
            else
            {
                if (m_Shooter.Alerted)
                    return;
                Transform point = CurrentPoint();
                if (point == null || !Reached(Snap(point.position)))
                    return;
                look = point.forward;
            }
            look.y = 0f;
            if (look.sqrMagnitude < 0.0001f)
                return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(look, Vector3.up), m_TurnSpeed * Time.deltaTime);
        }
    }
}
