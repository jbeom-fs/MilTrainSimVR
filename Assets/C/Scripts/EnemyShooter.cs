using System.Collections.Generic;
using UnityEngine;

namespace MilTrainSimVR.UrbanCombat
{
    // 적이 플레이어를 보고 쏜다 (10/26 첫 판 — 몸 돌리기·쏘기. 걸어 다니기는 10/27 부터 EnemyMover 가 맡는다).
    //   · 본다: 앞 140° 안, 60m 안에서 눈(머리) → 플레이어 머리나 가슴까지 막힌 데가 없으면
    //   · 듣는다: 45m 안에서 총소리가 나거나 자기가 맞으면 플레이어 쪽으로 돈다 (못 봤어도)
    //   · 쏜다: 플레이어 쪽으로 몸을 돌리고, 처음 본 뒤 0.8초 있다가 3발 점사 → 1.2~2초 쉬고 또.
    //     겨누는 오차는 처음 본 순간 4° 에서 3초 동안 계속 보면 1.5° 까지 준다. 부상이면 1.5배, 중상이면 2.5배
    //   · 총알 길은 Gunfire.Trace — 트리거인 플레이어 몸 판정까지 본다. 맞으면 그 부위 규칙대로(CombatantHealth)
    //   · 같은 편이 사선에 있으면 쏘지 않는다 (10/29 사용자) — 눈의 시선은 다른 적 몸에 막히지만 총알은 총구(어깨 앞)에서 오차를 갖고 나가
    //     시선 바로 옆에 선 적을 맞혔다. 총구 → 겨눈 곳 선 둘레(몸 폭 + 그 거리에서 오차만큼 벌어진 폭)에 산 적이 있으면 잠깐 뒤에 다시 본다
    //   · 걷는 중(Moving — EnemyMover 가 켬)엔 몸은 걷는 쪽을 보고 겨누지 않는다. 플레이어를 보면 EnemyMover 가 멈춰 세운다
    //   · 겨누는 모습(상체를 굽혀 총구를 겨눌 곳으로)은 EnemySoldier 가 Aiming · LastKnown 을 보고 만든다 (11/03)
    //   · 30초 동안 보지도 듣지도 못하면 경계를 푼다 (조사하러 간 곳에 아무도 없으면 EnemyMover 가 먼저 푼다)
    //   · 자기가 전사하거나 플레이어가 전사하면 멈춘다
    //   · 11/16 사용자: "멀리 있는 적을 쏴도 나를 인식하지 않으면 반응조차 안 한다 — 쐈지만 빗나간 적은 대응사격하게"
    //     → 플레이어 총알이 가슴에서 3m 안을 지나가면(맞지 않아도) 쏜 쪽을 알고 경계한다. 경계 중엔(어디서 쐈는지 알 때) 60m 가 아니라
    //       110m 까지 보인다 — 총소리(45m)도 시야(60m)도 닿지 않는 먼 적이 그대로 서 있기만 하던 것
    //   · 11/16 사용자: "콜옵처럼 일정 시간마다 엄폐" → 엄폐할 때를 여기서 정하고(CoverWanted), 엄폐할 곳으로 가고 돌아오는 건 EnemyMover:
    //     점사를 3~5번 쏠 때마다 · 탄창(30발)이 비면(엄폐해서 갈아 끼운다) · 한 번이라도 쏜 뒤 총알이 스치거나 맞으면 70% 로.
    //     엄폐 중(Holding)엔 겨누지도 쏘지도 않는다. 엄폐할 곳이 없으면 그 자리에서 갈아 끼운다(2.6초)
    //   · 11/16 재장전 애니메이션: 숨을 자리에 닿으면(StartCoverReload) 한 발이라도 쐈을 때 갈아 끼우기 시작해 다 끼울 때까지 숨어 있다.
    //     갈아 끼우는 중(ReloadingNow)이면 EnemySoldier 가 재장전 동작을 튼다
    [RequireComponent(typeof(CombatantHealth))]
    public class EnemyShooter : MonoBehaviour
    {
        [Header("총")]
        [Tooltip("총구 (K2C1 의 Anchor_Muzzle) — 총알이 나가는 곳")]
        [SerializeField] Transform m_Muzzle;

        [SerializeField] MuzzleFlash m_MuzzleFlash;
        [SerializeField] LineRenderer m_Tracer;
        // 사격음은 여기서 내지 않는다 — 플레이어 리그의 CombatAudio 가 Gunfire.ShotFired 를 듣고 거리 · 벽 · 실내외에 맞춰 낸다 (11/16)

        [Tooltip("눈 — 볼 수 있는지 재는 곳 (머리)")]
        [SerializeField] Transform m_Eyes;

        [Header("보기 · 듣기")]
        [SerializeField] float m_ViewDistance = 60f;
        [Tooltip("앞으로 보이는 범위(°, 전체 폭)")]
        [SerializeField] float m_ViewAngle = 140f;
        [SerializeField] float m_HearingDistance = 45f;
        [Tooltip("처음 본 뒤 첫 발까지(초)")]
        [SerializeField] float m_ReactionSeconds = 0.8f;
        [Tooltip("몸 돌리는 빠르기(°/초)")]
        [SerializeField] float m_TurnSpeed = 150f;
        [Tooltip("보지도 듣지도 못한 채 이만큼(초) 지나면 경계를 푼다")]
        [SerializeField] float m_CalmSeconds = 30f;
        [Tooltip("경계 중(플레이어가 어디 있는지 알 때) 보이는 거리(m) — 평소(m_ViewDistance)보다 멀리. 총구 불빛 · 총소리로 어디서 쐈는지 안다")]
        [SerializeField] float m_AlertedViewDistance = 110f;
        [Tooltip("플레이어 총알이 가슴에서 이 거리(m) 안을 지나가면 맞지 않았어도 쏜 쪽을 안다 (총알이 스치는 소리)")]
        [SerializeField] float m_NearMissDistance = 3f;

        [Header("엄폐 (11/16)")]
        [Tooltip("탄창 — 이만큼 쏘면 엄폐해서 갈아 끼운다")]
        [SerializeField] int m_MagazineRounds = 30;
        [Tooltip("갈아 끼우는 시간(초) — 엄폐해 있는 동안 또는 그 자리에서")]
        [SerializeField] float m_ReloadSeconds = 2.6f;
        // 11/16 저녁: "엄폐가 너무 빡빡하다"는 피드백으로 덜 숨게 — 점사 3~5 → 4~7번, 스치면 숨기 0.7 → 0.4, 나온 뒤 안 숨기 2.5 → 4초
        // (첫 발을 빗맞히면 거의 늘 바로 숨어 두 번째 기회가 없었다)
        [Tooltip("점사를 이 사이(번) 쏠 때마다 한 번 엄폐한다 (무작위)")]
        [SerializeField] Vector2Int m_BurstsBeforeCover = new Vector2Int(4, 7);
        [Tooltip("한 번이라도 쏜 뒤 총알이 스치거나 맞으면 이 확률로 엄폐한다 (처음 알아챘을 땐 대응사격부터)")]
        [SerializeField, Range(0f, 1f)] float m_CoverWhenShotAt = 0.4f;
        [Tooltip("엄폐에서 나온 뒤 이만큼(초)은 총알이 스쳐도 다시 숨지 않는다 — 나오자마자 또 숨지 않게")]
        [SerializeField] float m_MinExposedSeconds = 4f;

        [Header("쏘기")]
        [SerializeField] int m_BurstCount = 3;
        [SerializeField] float m_RoundsPerMinute = 700f;
        [Tooltip("점사 사이 쉬는 시간(초) — 이 사이에서 무작위")]
        [SerializeField] Vector2 m_BurstPause = new Vector2(1.2f, 2f);
        [SerializeField] float m_Range = 120f;
        [Tooltip("겨누는 오차(°) — 처음 본 순간 → 3초 넘게 계속 봤을 때")]
        [SerializeField] Vector2 m_Spread = new Vector2(4f, 1.5f);
        [SerializeField] float m_TracerSeconds = 0.06f;

        static readonly RaycastHit[] s_Hits = new RaycastHit[16];
        static readonly List<EnemyShooter> s_All = new List<EnemyShooter>();   // 같은 편 — 사선 확인용

        const float k_AllyRadius = 0.3f;       // 적 몸 반 폭(팔 포함)
        const float k_AllyHeight = 1.75f;      // 적 키 (원점 = 발)
        const float k_AllyMargin = 1f;         // 오차에 더 얹는 여유(°) — 움직이는 표적·몸 돌리는 중
        const float k_RecheckSeconds = 0.25f;  // 사선이 막혔을 때 다시 볼 때까지

        CombatantHealth m_Health;
        PlayerHitboxes m_Player;          // 플레이어 몸 판정 — 머리·가슴 자리
        CombatantHealth m_PlayerHealth;
        bool m_Alerted;
        Vector3 m_LastKnown;              // 마지막으로 본(들은) 플레이어 가슴 자리
        float m_LastNews;                 // 마지막으로 보거나 들은 시각
        float m_SeenSince = -1f;          // 지금 보이기 시작한 시각 (안 보이면 -1)
        float m_NextShot;
        int m_BurstLeft;
        float m_TracerOff;
        int m_ShotsFired;
        int m_RoundsLeft = -1;            // 탄창에 남은 발 (-1 = 아직 안 셈 → 처음 쓸 때 가득)
        float m_ReloadUntil = -1f;        // 그 자리에서 갈아 끼우는 중이면 끝나는 시각
        int m_BurstsSinceCover;           // 지난 엄폐 뒤로 쏜 점사 수
        int m_BurstsWanted;               // 이만큼 쏘면 엄폐 (무작위로 다시 뽑는다)
        float m_ExposedSince = -100f;     // 엄폐에서 나온 시각

        public int ShotsFired => m_ShotsFired;
        public int ShotsHeld { get; private set; }   // 같은 편이 사선에 있어 참은 횟수
        public int NearMisses { get; private set; }  // 플레이어 총알이 스쳐 간 횟수 (시험 · 기록용)
        public bool SeesPlayer => m_SeenSince >= 0f;
        public bool Alerted => m_Alerted;
        public Vector3 LastKnown => m_LastKnown;
        public int RoundsLeft => m_RoundsLeft < 0 ? m_MagazineRounds : m_RoundsLeft;

        // 엄폐하고 싶다 — EnemyMover 가 보고 엄폐할 곳을 찾는다 (찾으면 BeginCover, 못 찾으면 CoverUnavailable)
        public bool CoverWanted { get; private set; }
        public string CoverReason { get; private set; } = "";
        // 엄폐 중 — 겨누지도 쏘지도 않는다 (EnemyMover 가 켜고 끈다)
        public bool Holding { get; private set; }
        // 탄창이 비었거나 갈아 끼우는 중 — 쏘지 않는다
        public bool Reloading => m_RoundsLeft == 0 || m_ReloadUntil >= 0f;
        // 지금 손으로 갈아 끼우고 있다 (재장전 애니메이션 — EnemySoldier). 엄폐해 숨는 동안 또는 엄폐할 곳이 없어 그 자리에서
        public bool ReloadingNow => m_ReloadUntil >= 0f;
        public float ReloadSeconds => m_ReloadSeconds;

        // 서서 LastKnown 쪽을 겨누는 중 (경계 중이고 걷지 않을 때)
        public bool Aiming { get; private set; }

        // 걷는 중 — EnemyMover 가 켜고 끈다. 켜져 있으면 몸을 돌리지 않고(걷는 쪽을 본다) 겨누지 않는다
        public bool Moving { get; set; }

        bool m_HasMover;

        void Awake()
        {
            m_Health = GetComponent<CombatantHealth>();
            m_HasMover = GetComponent<EnemyMover>() != null;
            m_Health.Hit += OnShot;
            if (m_Tracer != null)
                m_Tracer.enabled = false;
        }

        void Start()
        {
            m_Player = FindAnyObjectByType<PlayerHitboxes>();
            if (m_Player != null)
                m_PlayerHealth = m_Player.GetComponent<CombatantHealth>();
        }

        void OnEnable()
        {
            Gunfire.ShotFired += OnShotHeard;
            Gunfire.BulletPassed += OnBulletPassed;
            s_All.Add(this);
        }

        void OnDisable()
        {
            Gunfire.ShotFired -= OnShotHeard;
            Gunfire.BulletPassed -= OnBulletPassed;
            s_All.Remove(this);
            Aiming = false;
        }

        void OnDestroy()
        {
            if (m_Health != null)
                m_Health.Hit -= OnShot;
        }

        // 맞으면 플레이어 쪽을 본다 (지금 적을 쏘는 건 플레이어뿐)
        void OnShot(CombatantHealth who, HitZone.Part part, Vector3 point)
        {
            Alert();
            ShotAt();
        }

        // 총알이 지나간 길이 가슴 둘레 m_NearMissDistance 안을 지났으면 — 맞지 않았어도 누가 어디서 쐈는지 안다.
        // 적끼리의 총알은 보지 않는다 (같은 편 총알에 숨지 않게)
        void OnBulletPassed(Vector3 from, Vector3 to, Component shooter)
        {
            if (!enabled || !m_Health.IsAlive || shooter == null || shooter.GetComponentInParent<EnemyShooter>() != null)
                return;
            Vector3 chest = transform.position + Vector3.up * 1.3f;
            Vector3 line = to - from;
            float length = line.magnitude;
            if (length < 0.01f)
                return;
            float along = Mathf.Clamp(Vector3.Dot(chest - from, line / length), 0f, length);
            if ((from + line / length * along - chest).sqrMagnitude > m_NearMissDistance * m_NearMissDistance)
                return;
            NearMisses++;
            Alert();
            ShotAt();
        }

        // 총알이 스치거나 맞았다 — 이미 한 번이라도 쏜 뒤면(대응사격을 시작했으면) 확률로 엄폐. 처음 알아챈 순간엔 대응사격부터
        void ShotAt()
        {
            if (Holding || CoverWanted || Time.time - m_ExposedSince < m_MinExposedSeconds || m_BurstsSinceCover == 0)
                return;
            if (Random.value < m_CoverWhenShotAt)
                WantCover("총알이 스침");
        }

        void WantCover(string reason)
        {
            CoverWanted = true;
            CoverReason = reason;
        }

        // EnemyMover: 엄폐할 곳을 찾아 간다 — 이때부터 다시 나올 때까지 겨누지도 쏘지도 않는다
        public void BeginCover()
        {
            CoverWanted = false;
            Holding = true;
            m_ReloadUntil = -1f;
        }

        // EnemyMover: 엄폐에서 나온다 — 엄폐해 있는 동안 탄창을 갈아 끼웠다 (빈 탄창이 아니었어도 채운다 — 전술 재장전)
        public void EndCover()
        {
            Holding = false;
            m_ReloadUntil = -1f;
            m_RoundsLeft = m_MagazineRounds;
            m_BurstsSinceCover = 0;
            m_BurstsWanted = Random.Range(m_BurstsBeforeCover.x, m_BurstsBeforeCover.y + 1);
            m_ExposedSince = Time.time;
            m_NextShot = Mathf.Max(m_NextShot, Time.time + 0.3f);   // 나오자마자는 아니고 겨눌 틈
            m_BurstLeft = m_BurstCount;
        }

        // EnemyMover: 엄폐할 곳이 없다 — 탄창이 비었으면 그 자리에서 갈아 끼우고, 아니면 다음 차례까지 계속 싸운다
        public void CoverUnavailable()
        {
            CoverWanted = false;
            m_BurstsSinceCover = 0;
            m_BurstsWanted = Random.Range(m_BurstsBeforeCover.x, m_BurstsBeforeCover.y + 1);
            if (m_RoundsLeft == 0 && m_ReloadUntil < 0f)
                m_ReloadUntil = Time.time + m_ReloadSeconds;
        }

        // EnemyMover: 숨을 자리에 닿았다 — 한 발이라도 쐈으면 갈아 끼우기 시작한다 (11/16 재장전 애니메이션. 그 전엔 시간만 셌다)
        public void StartCoverReload()
        {
            if (Holding && RoundsLeft < m_MagazineRounds && m_ReloadUntil < 0f)
                m_ReloadUntil = Time.time + m_ReloadSeconds;
        }

        // 엄폐 시간 — 갈아 끼우는 중이면 다 끼울 때까지, 아직 시작 전인데 탄창이 비었으면 갈아 끼우는 시간만큼은 숨어 있어야 한다
        public float MinHideSeconds => m_ReloadUntil >= 0f ? m_ReloadUntil - Time.time : m_RoundsLeft == 0 ? m_ReloadSeconds : 0f;

        void OnShotHeard(Vector3 origin, Component shooter)
        {
            if (shooter != null && shooter.transform.IsChildOf(transform))
                return;   // 자기 총소리
            // 다른 적의 총소리도 '근처에서 싸운다'는 뜻이라 같이 돌아본다
            if ((origin - transform.position).sqrMagnitude <= m_HearingDistance * m_HearingDistance)
                Alert();
        }

        // 지휘(EnemyDirector)가 플레이어 자리를 알려 준다 — 총소리를 들은 것처럼 경계하고 그쪽을 본다
        public void AlertToPlayer() => Alert();

        void Alert()
        {
            if (m_Player == null || !m_Health.IsAlive)
                return;
            m_Alerted = true;
            m_LastKnown = m_Player.Chest.position;
            m_LastNews = Time.time;
        }

        // 경계를 푼다 — 조사하러 간 곳에 아무도 없을 때(EnemyMover), 또는 오래 아무 일도 없을 때
        public void CalmDown()
        {
            m_Alerted = false;
            m_SeenSince = -1f;
            CoverWanted = false;
        }

        void Update()
        {
            if (m_Tracer != null && m_Tracer.enabled && Time.time >= m_TracerOff)
                m_Tracer.enabled = false;
            Aiming = false;
            if (!m_Health.IsAlive || m_Player == null || m_PlayerHealth == null || !m_PlayerHealth.IsAlive)
            {
                m_SeenSince = -1f;
                return;
            }

            Vector3 chest = m_Player.Chest.position;
            if (CanSee(chest) || CanSee(m_Player.Head.position))
            {
                if (m_SeenSince < 0f)
                {
                    // 처음 본 순간 — 바로 쏘지 않고 반응 시간만큼 있다가
                    m_SeenSince = Time.time;
                    m_NextShot = Mathf.Max(m_NextShot, Time.time + m_ReactionSeconds);
                    m_BurstLeft = m_BurstCount;
                }
                m_Alerted = true;
                m_LastKnown = chest;
                m_LastNews = Time.time;
            }
            else
            {
                m_SeenSince = -1f;
                if (m_Alerted && Time.time - m_LastNews > m_CalmSeconds)
                    CalmDown();
            }

            // 걸어 다니지 않는 적(EnemyMover 없음 — 보기용 씬 등)은 엄폐할 수 없다 — 그 자리에서 갈아 끼운다
            if (CoverWanted && !m_HasMover)
                CoverUnavailable();
            // 그 자리에서 갈아 끼우던 것이 끝났다
            if (m_ReloadUntil >= 0f && Time.time >= m_ReloadUntil)
            {
                m_ReloadUntil = -1f;
                m_RoundsLeft = m_MagazineRounds;
                m_NextShot = Mathf.Max(m_NextShot, Time.time + 0.2f);
                m_BurstLeft = m_BurstCount;
            }

            if (!m_Alerted)
            {
                m_SeenSince = -1f;
                return;
            }
            if (Moving)
                return;
            Turn(m_LastKnown);
            // 엄폐 중 · 갈아 끼우는 중엔 총을 내리고(겨누지 않고) 쏘지 않는다 — 몸은 플레이어 쪽을 보고 있다가 나오면 바로 겨눈다
            if (Holding || Reloading)
                return;
            Aiming = true;
            if (SeesPlayer && Facing(m_LastKnown) < 12f)
                TryFire(chest);
        }

        // 눈에서 그 점까지: 앞 범위·거리 안이고, 플레이어 몸(리그) 말고 막는 게 없으면 보인다.
        // 경계 중이면(어디서 쐈는지 알면) 더 멀리까지 — m_AlertedViewDistance
        bool CanSee(Vector3 point)
        {
            Vector3 eyes = m_Eyes != null ? m_Eyes.position : transform.position + Vector3.up * 1.6f;
            Vector3 to = point - eyes;
            float distance = to.magnitude;
            if (distance > (m_Alerted ? Mathf.Max(m_ViewDistance, m_AlertedViewDistance) : m_ViewDistance) || distance < 0.01f)
                return false;
            if (Facing(point) > m_ViewAngle * 0.5f)
                return false;
            int count = Physics.RaycastNonAlloc(eyes, to / distance, s_Hits, distance, Gunfire.ShotMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform t = s_Hits[i].collider.transform;
                if (t.IsChildOf(transform) || t.IsChildOf(m_Player.transform))
                    continue;   // 자기 몸, 플레이어 몸통 캡슐
                return false;
            }
            return true;
        }

        // 몸 앞(수평)과 그 점 쪽이 벌어진 각도
        float Facing(Vector3 point)
        {
            Vector3 flat = Vector3.ProjectOnPlane(point - transform.position, Vector3.up);
            return flat.sqrMagnitude < 0.0001f ? 0f : Vector3.Angle(Vector3.ProjectOnPlane(transform.forward, Vector3.up), flat);
        }

        void Turn(Vector3 point)
        {
            Vector3 flat = Vector3.ProjectOnPlane(point - transform.position, Vector3.up);
            if (flat.sqrMagnitude < 0.0001f)
                return;
            Quaternion want = Quaternion.LookRotation(flat, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, m_TurnSpeed * Time.deltaTime);
        }

        void TryFire(Vector3 target)
        {
            if (Time.time < m_NextShot || m_Muzzle == null)
                return;
            // 같은 편이 사선에 있으면 이번엔 참고 잠깐 뒤에 다시 본다 — 점사에 남은 발 수는 그대로
            if (AllyInLine(m_Muzzle.position, target, Spread()))
            {
                ShotsHeld++;
                m_NextShot = Time.time + k_RecheckSeconds;
                return;
            }
            if (m_RoundsLeft < 0)
                m_RoundsLeft = m_MagazineRounds;
            if (m_BurstsWanted == 0)
                m_BurstsWanted = Random.Range(m_BurstsBeforeCover.x, m_BurstsBeforeCover.y + 1);
            FireOne(target);
            m_RoundsLeft--;
            m_BurstLeft--;
            if (m_RoundsLeft == 0)
            {
                // 탄창이 비었다 — 엄폐해서 갈아 끼운다 (엄폐할 곳이 없으면 EnemyMover 가 CoverUnavailable → 그 자리에서)
                m_BurstsSinceCover++;
                m_BurstLeft = m_BurstCount;
                m_NextShot = Time.time + Random.Range(m_BurstPause.x, m_BurstPause.y);
                WantCover("탄창 교체");
            }
            else if (m_BurstLeft > 0)
            {
                m_NextShot = Time.time + 60f / m_RoundsPerMinute;
            }
            else
            {
                m_NextShot = Time.time + Random.Range(m_BurstPause.x, m_BurstPause.y);
                m_BurstLeft = m_BurstCount;
                // 점사를 몇 번 쐈으면 한 번 엄폐 (콜옵처럼 쏘고 숨고 다시 나온다)
                if (++m_BurstsSinceCover >= m_BurstsWanted && !CoverWanted)
                    WantCover("주기");
            }
        }

        // 겨누는 오차(°): 오래 볼수록 정확, 다쳤으면 부정확
        float Spread()
        {
            float steady = m_SeenSince >= 0f ? Mathf.Clamp01((Time.time - m_SeenSince) / 3f) : 0f;
            float spread = Mathf.Lerp(m_Spread.x, m_Spread.y, steady);
            if (m_Health.Current == CombatantHealth.State.Wounded)
                spread *= 1.5f;
            else if (m_Health.Current == CombatantHealth.State.Severe)
                spread *= 2.5f;
            return spread;
        }

        // 총구 → 겨눈 곳 선분 둘레에 산 다른 적이 있는지. 적 몸은 발 → 머리 세로 선분으로 보고, 두 선분이 가장 가까운 곳의 거리가
        // 몸 반 폭 + (총구에서 그곳까지 거리 × 오차 각)보다 가까우면 맞힐 수 있다고 본다. 쏘는 사람 뒤·겨눈 곳 너머는 상관없다
        bool AllyInLine(Vector3 origin, Vector3 target, float spread)
        {
            Vector3 line = target - origin;
            float length = line.magnitude;
            if (length < 0.01f)
                return false;
            Vector3 dir = line / length;
            float widen = Mathf.Tan((spread + k_AllyMargin) * Mathf.Deg2Rad);
            foreach (EnemyShooter ally in s_All)
            {
                if (ally == this || !ally.m_Health.IsAlive)
                    continue;
                Closest(origin, dir, length, ally.transform.position, Vector3.up, k_AllyHeight, out float along, out float gap);
                if (along > 0f && along < length && gap < k_AllyRadius + along * widen)
                    return true;
            }
            return false;
        }

        // 두 선분 p + u·s (0 ≤ s ≤ lenU) · q + v·r (0 ≤ r ≤ lenV) 이 가장 가까운 곳 — u·v 는 단위 벡터. along = 그때 s, gap = 거리
        static void Closest(Vector3 p, Vector3 u, float lenU, Vector3 q, Vector3 v, float lenV, out float along, out float gap)
        {
            Vector3 w = p - q;
            float b = Vector3.Dot(u, v), d = Vector3.Dot(u, w), e = Vector3.Dot(v, w);
            float denom = 1f - b * b;
            float s = denom > 0.000001f ? Mathf.Clamp((b * e - d) / denom, 0f, lenU) : 0f;
            float r = Mathf.Clamp(e + b * s, 0f, lenV);
            s = Mathf.Clamp(b * r - d, 0f, lenU);
            along = s;
            gap = (p + u * s - (q + v * r)).magnitude;
        }

        void FireOne(Vector3 target)
        {
            if (m_Muzzle == null)
                return;
            m_ShotsFired++;
            float spread = Spread();

            Vector3 origin = m_Muzzle.position;
            Vector3 aim = (target - origin).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, aim).normalized;
            if (right.sqrMagnitude < 0.0001f)
                right = Vector3.right;
            Vector3 up = Vector3.Cross(aim, right);
            Vector2 r = Random.insideUnitCircle * Mathf.Tan(spread * Mathf.Deg2Rad);
            Vector3 dir = (aim + right * r.x + up * r.y).normalized;

            Vector3 end = origin + dir * m_Range;
            if (Gunfire.Trace(origin, dir, m_Range, transform, out RaycastHit hit, out HitZone zone))
            {
                end = hit.point;
                if (zone != null)
                {
                    Gunfire.RaiseBodyHit(this, zone);
                    zone.OnHit(hit, dir);
                }
                // 탄흔·불꽃은 보이는 것에만 — 플레이어 몸 판정 상자(트리거)는 눈에 안 보이는 상자라 자국을 남기지 않는다
                if (!hit.collider.isTrigger)
                    Gunfire.RaiseSurfaceHit(hit);
            }
            // 적 총알도 지나간 길의 유리를 깬다 (11/18). 총열 길이만큼 뒤에서부터 — 창가에 선 적의 총구가 유리 너머로 나와 있어도 그 창이 깨지게
            BreakableGlass.BulletPath(origin - dir * 0.6f, end);
            if (m_Tracer != null)
            {
                m_Tracer.SetPosition(0, origin);
                m_Tracer.SetPosition(1, end);
                m_Tracer.enabled = true;
                m_TracerOff = Time.time + m_TracerSeconds;
            }
            if (m_MuzzleFlash != null)
                m_MuzzleFlash.Play();
            Gunfire.RaiseShotFired(origin, this);
        }
    }
}
