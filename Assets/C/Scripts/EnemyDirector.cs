using System.Collections.Generic;
using UnityEngine;

namespace MilTrainSimVR.UrbanCombat
{
    // 적 지휘 (11/17 사용자 — "적이 유동적으로 움직이지 않으니 이동 경로 · 순찰을 바꾸고, 창문 고정이 아니라 내려와서 쏘는 적도"):
    // 자유 전투 동안 '언제 → 누가 → 어떻게' 규칙을 한 번씩 실행한다. 규칙은 인스펙터에서 고치고, 자리 · 경로 · 구역은 씬에서 옮긴다.
    //   언제: 시작 후 몇 초 / 이 적들 중 누가 쓰러지면 / 플레이어가 이 구역(TriggerArea)에 들어오면 / 움직일 적이 경계한 지 몇 초(11/18 — 초기 대응 뒤)
    //   어떻게: 경로 바꾸기(새 순찰) / 자리 지키기(자리들을 가까운 적부터 나눠 줌 — 뛰어간다) / 플레이어 추적 / 점들을 거쳐 추적(에스컬레이터로 내려오기)
    //          / 엄폐하며 자리 옮기기(11/18 — 그 자리 옆 엄폐물 뒤로 쏘지 않고 뛰어가 숨었다가 그 자리로 나와 쏜다)
    //   '플레이어 자리 알려 주기'를 켜면 그 적들이 플레이어 쪽을 돌아보고 경계한다 (대응 사격)
    //   '막는 규칙': 이 규칙이 일어나면 이름이 같은 다른 규칙은 다시 안 일어난다 — 지상 · 지하 두 갈래 중 먼저 생긴 쪽만
    //   '지연'(11/18): 조건이 맞은 뒤 몇 초 기다렸다가 실행 — 백화점 1층에 들어가면 적 10(에스컬레이터)이 먼저, 적 17(계단)은 10초 뒤
    // 오버타임이 되면 규칙은 멈추고, 경기 진행(MatchFlow)이 살아 있는 적을 모두 점령지(백화점 3층)로 보낸다 — 추적 · 순찰보다 먼저다
    public class EnemyDirector : MonoBehaviour
    {
        // 시작 후 몇 초 · 이 적 중 누가 쓰러지면 · 플레이어가 구역에 들어오면 · 움직일 적 중 누가 경계한 지 몇 초 (새 값은 뒤에 붙인다 — 씬엔 번호로 저장된다)
        public enum When { AfterSeconds, AnyKilled, PlayerEnters, AlertedFor }
        // 경로 바꾸기 · 자리 지키기 · 플레이어 추적 · 점들(자리)을 거쳐 추적 · 엄폐하며 자리 옮기기
        public enum Order { NewRoute, HoldSpots, ChasePlayer, ChaseVia, TakeCover }

        [System.Serializable]
        public class Rule
        {
            [Tooltip("이름 — 로그 · '막는 규칙'에 쓴다")]
            public string name;

            [Header("언제")]
            public When when;
            [Tooltip("AfterSeconds: 경기 시작 후 이만큼(초)\nAlertedFor: 움직일 적(아래) 중 누가 경계한 지 이만큼(초) — 총소리를 듣거나 플레이어를 본 때부터. 경계가 풀리면 처음부터 다시 센다")]
            public float seconds;
            [Tooltip("AnyKilled: 이 적들 중 하나라도 쓰러지면 (전사)")]
            public CombatantHealth[] killed = new CombatantHealth[0];
            [Tooltip("PlayerEnters: 플레이어가 이 구역에 들어오면")]
            public TriggerArea area;

            [Header("누가 · 어떻게")]
            [Tooltip("움직일 적 — 이미 쓰러진 적은 건너뛴다")]
            public EnemyMover[] enemies = new EnemyMover[0];
            public Order order;
            [Tooltip("NewRoute: 새 경로 (점 둘 이상이면 순찰) — 지금 자리에서 첫 점까지 걸어간다")]
            public EnemyRoute route;
            [Tooltip("HoldSpots · TakeCover: 지킬 자리들 (앞 = 보는 쪽). 자리에 가까운 적부터 자기에게 가장 가까운 빈자리. TakeCover 는 그 자리 3m 안 엄폐물 뒤로 먼저 숨는다\nChaseVia: 차례로 거쳐 갈 점들 — 지금 층의 첫 점부터 지나고 플레이어를 따라간다")]
            public Transform[] spots = new Transform[0];
            [Tooltip("켜면 그 적들에게 플레이어 자리를 알려 준다 — 경계하고 그쪽을 본다 (대응 사격)")]
            public bool alertToPlayer;

            [Tooltip("이 규칙이 일어나면 다시 안 일어나게 막을 다른 규칙 이름")]
            public string[] blocks = new string[0];

            [Tooltip("조건이 맞은 뒤 이만큼(초) 기다렸다가 실행 — 그 사이 조건이 풀려도(플레이어가 구역을 나가도) 실행한다 (11/18 — 두 적이 한꺼번에 들이닥치지 않게)")]
            public float delay;
        }

        [SerializeField] MatchFlow m_Flow;
        [SerializeField] Rule[] m_Rules = new Rule[0];

        bool[] m_Done;
        float[] m_AlertedAt;      // AlertedFor: 움직일 적이 경계하기 시작한 시각 (경계 중이 아니면 -1)
        float[] m_DueAt;          // 지연이 있는 규칙의 조건이 처음 맞은 시각 (아직이면 -1)
        Transform m_PlayerMark;   // 추적하는 적이 따라갈 점 — 매 프레임 플레이어 발 자리로 옮긴다

        public IReadOnlyList<Rule> Rules => m_Rules;

#if UNITY_EDITOR
        // 만들기 메뉴(EnemyDirectorBuilder)가 규칙을 넣는다
        public void EditorSetup(MatchFlow flow, Rule[] rules)
        {
            m_Flow = flow;
            m_Rules = rules;
        }
#endif
        public bool Done(int index) => m_Done != null && m_Done[index];
        public Transform PlayerMark => m_PlayerMark;

        void Awake()
        {
            m_Done = new bool[m_Rules.Length];
            m_AlertedAt = new float[m_Rules.Length];
            m_DueAt = new float[m_Rules.Length];
            for (int i = 0; i < m_AlertedAt.Length; i++)
            {
                m_AlertedAt[i] = -1f;
                m_DueAt[i] = -1f;
            }
            m_PlayerMark = new GameObject("플레이어 발 자리 (추적용)").transform;
            m_PlayerMark.SetParent(transform, false);
        }

        void Update()
        {
            if (m_Flow == null || m_Flow.CurrentPhase != MatchFlow.Phase.Free)
                return;
            m_PlayerMark.position = m_Flow.PlayerFeet;
            for (int i = 0; i < m_Rules.Length; i++)
            {
                if (m_Done[i])
                    continue;
                if (m_DueAt[i] < 0f && Due(i))
                    m_DueAt[i] = Time.time;
                if (m_DueAt[i] >= 0f && Time.time - m_DueAt[i] >= m_Rules[i].delay)
                    Fire(i);
            }
        }

        bool Due(int index)
        {
            Rule rule = m_Rules[index];
            switch (rule.when)
            {
                case When.AfterSeconds:
                    return m_Flow.MatchSeconds >= rule.seconds;
                case When.AnyKilled:
                    foreach (CombatantHealth who in rule.killed)
                    {
                        if (who != null && !who.IsAlive)
                            return true;
                    }
                    return false;
                case When.PlayerEnters:
                    return rule.area != null && rule.area.Contains(m_Flow.PlayerFeet);
                case When.AlertedFor:
                    bool alerted = false;
                    foreach (EnemyMover mover in rule.enemies)
                    {
                        if (mover != null && mover.TryGetComponent(out CombatantHealth health) && health.IsAlive
                            && mover.TryGetComponent(out EnemyShooter shooter) && shooter.Alerted)
                        {
                            alerted = true;
                            break;
                        }
                    }
                    if (!alerted)
                    {
                        m_AlertedAt[index] = -1f;
                        return false;
                    }
                    if (m_AlertedAt[index] < 0f)
                        m_AlertedAt[index] = Time.time;
                    return Time.time - m_AlertedAt[index] >= rule.seconds;
            }
            return false;
        }

        // 규칙 하나 실행 — 테스트(Unity CLI)에서 직접 불러도 된다
        public void Fire(int index)
        {
            Rule rule = m_Rules[index];
            m_Done[index] = true;
            foreach (string other in rule.blocks)
            {
                for (int j = 0; j < m_Rules.Length; j++)
                {
                    if (m_Rules[j].name == other)
                        m_Done[j] = true;
                }
            }

            var alive = new List<EnemyMover>();
            foreach (EnemyMover mover in rule.enemies)
            {
                if (mover != null && mover.TryGetComponent(out CombatantHealth health) && health.IsAlive)
                    alive.Add(mover);
            }
            // 플레이어 자리부터 알려 준다 — 엄폐하며 자리 옮기기는 그 자리를 보고 숨을 곳을 고른다
            if (rule.alertToPlayer || rule.order == Order.ChasePlayer || rule.order == Order.ChaseVia)
            {
                foreach (EnemyMover mover in alive)
                {
                    if (mover.TryGetComponent(out EnemyShooter shooter))
                        shooter.AlertToPlayer();
                }
            }
            switch (rule.order)
            {
                case Order.NewRoute:
                    foreach (EnemyMover mover in alive)
                    {
                        mover.ClearOrder();
                        mover.Route = rule.route;
                    }
                    break;
                case Order.HoldSpots:
                    AssignSpots(alive, rule.spots, false);
                    break;
                case Order.TakeCover:
                    AssignSpots(alive, rule.spots, true);
                    break;
                case Order.ChasePlayer:
                    m_PlayerMark.position = m_Flow.PlayerFeet;
                    foreach (EnemyMover mover in alive)
                        mover.Chase(m_PlayerMark);
                    break;
                case Order.ChaseVia:
                    m_PlayerMark.position = m_Flow.PlayerFeet;
                    foreach (EnemyMover mover in alive)
                        mover.ChaseVia(rule.spots, m_PlayerMark);
                    break;
            }
            Debug.Log($"[C] 적 지휘: {rule.name} — {rule.order} {alive.Count}명");
        }

        // 자리에 가까운 적부터 자기에게 가장 가까운 빈자리 (MatchFlow 의 점령지 자리 나누기와 같은 방식). 적이 자리보다 많으면 자리를 다시 쓴다
        static void AssignSpots(List<EnemyMover> movers, Transform[] spots, bool takeCover)
        {
            if (spots == null || spots.Length == 0)
                return;
            Vector3 center = Vector3.zero;
            foreach (Transform spot in spots)
                center += spot.position;
            center /= spots.Length;
            movers.Sort((a, b) => (a.transform.position - center).sqrMagnitude.CompareTo((b.transform.position - center).sqrMagnitude));
            var taken = new bool[spots.Length];
            int takenCount = 0;
            foreach (EnemyMover mover in movers)
            {
                if (takenCount == spots.Length)
                {
                    System.Array.Clear(taken, 0, taken.Length);
                    takenCount = 0;
                }
                int best = -1;
                float bestDistance = float.MaxValue;
                for (int i = 0; i < spots.Length; i++)
                {
                    float d = (spots[i].position - mover.transform.position).sqrMagnitude;
                    if (!taken[i] && d < bestDistance)
                    {
                        best = i;
                        bestDistance = d;
                    }
                }
                taken[best] = true;
                takenCount++;
                if (takeCover)
                    mover.MoveToCover(spots[best]);
                else
                    mover.HoldAt(spots[best]);
            }
        }
    }
}
