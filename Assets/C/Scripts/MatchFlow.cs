using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MilTrainSimVR.UrbanCombat
{
    // 경기 진행 (C 범위 문서 3번 — 사용자 10/26 결정, 10/27 만듦):
    //   · 시작: 플레이어가 소총을 처음 잡으면. 그 전엔 적이 제자리에 서 있기만 한다 (보지도 쏘지도 않는다)
    //   · 자유 전투 5분 — 점령지 없음
    //   · 오버타임 3분 — 점령지(11/16 부터 백화점 3층 전체, 그 전엔 N21 2층)가 열리고, 살아 있는 적이 모두 그리로 가서 지킨다 (EnemyMover.HoldAt).
    //     점령지에 한 진영만 있으면 그 진영의 점령 시간이 쌓이고 30초가 되면 그 진영이 이긴다.
    //     양쪽이 다 있으면 둘 다 멈춘다(다툼). 아무도 없거나 나가도 쌓인 시간은 그대로 둔다.
    //     적의 점령 시간은 오버타임 1분 뒤부터 쌓인다 (11/16 사용자 — 백화점 3층엔 처음부터 적이 있고, 출발 자리에서 길로 144 m 라
    //     그대로면 오버타임 30초 만에 질 수 있었다)
    //   · 끝: 적이 모두 전사 = 승리(섬멸) · 점령 완료 = 그 진영 승리 · 플레이어 전사 = 패배 · 오버타임 시간이 다 되면 무승부
    //   · 끝나면 적을 그 자리에 멈춘다. 손목 표시·결과판은 MatchDisplay, 명중·처치 기록은 MatchRecord
    [RequireComponent(typeof(MatchRecord))]
    public class MatchFlow : MonoBehaviour
    {
        public enum Phase { Waiting, Free, Overtime, Ended }   // 시작 전 · 자유 전투 · 오버타임 · 끝
        public enum Outcome { None, Annihilation, PlayerCapture, PlayerKilled, EnemyCapture, Draw }

        [Header("시간 (초)")]
        [SerializeField] float m_FreeSeconds = 300f;
        [SerializeField] float m_OvertimeSeconds = 180f;
        [Tooltip("점령지에 한 진영만 이만큼(초) 있으면 그 진영 승리")]
        [SerializeField] float m_CaptureSeconds = 30f;
        [Tooltip("오버타임이 시작되고 이만큼(초) 동안은 적의 점령 시간이 쌓이지 않는다 — 플레이어가 점령지까지 갈 시간")]
        [SerializeField] float m_EnemyCaptureDelay = 60f;

        [Header("참가")]
        [SerializeField] CaptureZone m_Zone;
        [Tooltip("적들의 부모 — 그 아래 몸 상태(CombatantHealth)를 모두 적으로 센다")]
        [SerializeField] Transform m_EnemiesRoot;
        [SerializeField] CombatantHealth m_Player;
        [Tooltip("이 총을 처음 잡으면 시작한다")]
        [SerializeField] XRGrabInteractable m_StartRifle;
        [Tooltip("끄면 씬이 열리자마자 시작한다")]
        [SerializeField] bool m_StartOnGrab = true;

        // 적 한 명 — 몸 상태와 움직임·쏘기
        struct Enemy
        {
            public CombatantHealth health;
            public EnemyMover mover;
            public EnemyShooter shooter;
        }

        readonly List<Enemy> m_Enemies = new List<Enemy>();
        MatchRecord m_Record;
        Transform m_PlayerHead;
        Phase m_Phase = Phase.Waiting;
        Outcome m_Outcome = Outcome.None;
        float m_PhaseStart;
        float m_MatchStart;
        float m_EndTime;
        float m_PlayerCapture;
        float m_EnemyCapture;
        bool m_PlayerInZone;
        bool m_EnemiesInZone;

        // 단계가 바뀔 때마다 (손목 표시·알림·결과판이 듣는다)
        public event System.Action<Phase> PhaseChanged;

        public Phase CurrentPhase => m_Phase;
        public Outcome Result => m_Outcome;
        public CaptureZone Zone => m_Zone;
        public float CaptureSeconds => m_CaptureSeconds;
        public float PlayerCapture => m_PlayerCapture;
        public float EnemyCapture => m_EnemyCapture;
        public bool PlayerInZone => m_PlayerInZone;
        public bool EnemiesInZone => m_EnemiesInZone;
        public int EnemyCount => m_Enemies.Count;
        public float FreeSeconds => m_FreeSeconds;
        public float EnemyCaptureDelay => m_EnemyCaptureDelay;

        // 오버타임 중 적의 점령 시간이 쌓이기 시작할 때까지 남은 초 (그 밖엔 0)
        public float EnemyCaptureWait => m_Phase == Phase.Overtime ? Mathf.Max(0f, m_EnemyCaptureDelay - (Time.time - m_PhaseStart)) : 0f;

        // 지금 단계에 남은 시간(초) — 시작 전·끝난 뒤엔 0
        public float PhaseRemaining => m_Phase switch
        {
            Phase.Free => Mathf.Max(0f, m_FreeSeconds - (Time.time - m_PhaseStart)),
            Phase.Overtime => Mathf.Max(0f, m_OvertimeSeconds - (Time.time - m_PhaseStart)),
            _ => 0f,
        };

        // 시작부터 지금(끝났으면 끝난 때)까지 걸린 시간(초)
        public float MatchSeconds => m_Phase == Phase.Waiting ? 0f : (m_Phase == Phase.Ended ? m_EndTime : Time.time) - m_MatchStart;

        public int EnemiesAlive
        {
            get
            {
                int alive = 0;
                foreach (Enemy enemy in m_Enemies)
                {
                    if (enemy.health.IsAlive)
                        alive++;
                }
                return alive;
            }
        }

        // 플레이어가 서 있는 자리 — 머리(카메라) 바로 밑, 리그 바닥에서 조금 위
        public Vector3 PlayerFeet
        {
            get
            {
                Vector3 head = m_PlayerHead != null ? m_PlayerHead.position : m_Player.transform.position;
                return new Vector3(head.x, m_Player.transform.position.y + 0.2f, head.z);
            }
        }

        void Awake()
        {
            m_Record = GetComponent<MatchRecord>();
            foreach (CombatantHealth health in m_EnemiesRoot.GetComponentsInChildren<CombatantHealth>(true))
                m_Enemies.Add(new Enemy { health = health, mover = health.GetComponent<EnemyMover>(), shooter = health.GetComponent<EnemyShooter>() });
            XROrigin origin = m_Player.GetComponent<XROrigin>();
            if (origin != null && origin.Camera != null)
                m_PlayerHead = origin.Camera.transform;
        }

        void Start()
        {
            m_Zone.SetOpen(false);
            if (m_StartOnGrab && m_StartRifle != null)
            {
                SetEnemiesActive(false);
                m_StartRifle.selectEntered.AddListener(OnRifleGrabbed);
            }
            else
            {
                StartMatch();
            }
        }

        void OnDestroy()
        {
            if (m_StartRifle != null)
                m_StartRifle.selectEntered.RemoveListener(OnRifleGrabbed);
        }

        void OnRifleGrabbed(SelectEnterEventArgs args)
        {
            StartMatch();
        }

        // 자유 전투 시작 — 총을 잡을 때(또는 바로). 시험할 때 직접 불러도 된다
        public void StartMatch()
        {
            if (m_Phase != Phase.Waiting)
                return;
            m_MatchStart = Time.time;
            SetEnemiesActive(true);
            var enemies = new List<CombatantHealth>();
            foreach (Enemy enemy in m_Enemies)
                enemies.Add(enemy.health);
            m_Record.Begin(m_Player, enemies);
            SetPhase(Phase.Free);
        }

        void Update()
        {
            if (m_Phase != Phase.Free && m_Phase != Phase.Overtime)
                return;

            // 끝나는 조건부터 — 플레이어 전사가 먼저다 (같은 순간에 마지막 적도 쓰러졌으면 패배)
            if (!m_Player.IsAlive)
            {
                End(Outcome.PlayerKilled);
                return;
            }
            if (EnemiesAlive == 0)
            {
                End(Outcome.Annihilation);
                return;
            }

            float elapsed = Time.time - m_PhaseStart;
            if (m_Phase == Phase.Free)
            {
                if (elapsed >= m_FreeSeconds)
                    StartOvertime();
                return;
            }

            UpdateCapture();
            if (m_PlayerCapture >= m_CaptureSeconds)
                End(Outcome.PlayerCapture);
            else if (m_EnemyCapture >= m_CaptureSeconds)
                End(Outcome.EnemyCapture);
            else if (elapsed >= m_OvertimeSeconds)
                End(Outcome.Draw);
        }

        void StartOvertime()
        {
            m_Zone.SetOpen(true);
            SendEnemiesToZone();
            SetPhase(Phase.Overtime);
        }

        // 살아 있는 적에게 점령지 자리를 나눠 준다 — 점령지에 가까운 적부터 자기에게 가장 가까운 빈자리를.
        // 적이 자리보다 많으면 자리를 다시 쓴다
        void SendEnemiesToZone()
        {
            IReadOnlyList<Transform> spots = m_Zone.Spots;
            if (spots.Count == 0)
                return;
            var alive = new List<Enemy>();
            foreach (Enemy enemy in m_Enemies)
            {
                if (enemy.health.IsAlive && enemy.mover != null)
                    alive.Add(enemy);
            }
            Vector3 center = m_Zone.transform.position;
            alive.Sort((a, b) => (a.health.transform.position - center).sqrMagnitude.CompareTo((b.health.transform.position - center).sqrMagnitude));
            var taken = new bool[spots.Count];
            int takenCount = 0;
            foreach (Enemy enemy in alive)
            {
                if (takenCount == spots.Count)
                {
                    System.Array.Clear(taken, 0, taken.Length);
                    takenCount = 0;
                }
                int best = -1;
                float bestDistance = float.MaxValue;
                for (int i = 0; i < spots.Count; i++)
                {
                    float d = (spots[i].position - enemy.health.transform.position).sqrMagnitude;
                    if (!taken[i] && d < bestDistance)
                    {
                        best = i;
                        bestDistance = d;
                    }
                }
                taken[best] = true;
                takenCount++;
                enemy.mover.HoldAt(spots[best]);
            }
        }

        // 점령지에 한 진영만 있으면 그 진영 시간이 쌓인다. 양쪽이 다 있으면(다툼) 멈춘다
        void UpdateCapture()
        {
            m_PlayerInZone = m_Zone.Contains(PlayerFeet);
            m_EnemiesInZone = false;
            foreach (Enemy enemy in m_Enemies)
            {
                if (enemy.health.IsAlive && m_Zone.Contains(enemy.health.transform.position + Vector3.up * 0.2f))
                {
                    m_EnemiesInZone = true;
                    break;
                }
            }
            if (m_PlayerInZone && !m_EnemiesInZone)
                m_PlayerCapture += Time.deltaTime;
            else if (m_EnemiesInZone && !m_PlayerInZone && EnemyCaptureWait <= 0f)
                m_EnemyCapture += Time.deltaTime;
        }

        void End(Outcome outcome)
        {
            m_Outcome = outcome;
            m_EndTime = Time.time;
            m_Record.Stop();
            SetEnemiesActive(false);
            m_Zone.SetOpen(false);
            Debug.Log($"[C] 경기 끝: {outcome} ({MatchSeconds:0}초)");
            SetPhase(Phase.Ended);
        }

        // 시작 전·끝난 뒤엔 적이 보지도 쏘지도 걷지도 않는다
        void SetEnemiesActive(bool active)
        {
            foreach (Enemy enemy in m_Enemies)
            {
                if (enemy.mover != null)
                    enemy.mover.enabled = active;
                if (enemy.shooter != null)
                    enemy.shooter.enabled = active;
            }
        }

        void SetPhase(Phase phase)
        {
            m_Phase = phase;
            m_PhaseStart = Time.time;
            PhaseChanged?.Invoke(phase);
        }

        // 다시하기 — 씬을 처음부터 다시 연다
        public void Restart()
        {
            Scene scene = gameObject.scene;
#if UNITY_EDITOR
            // 에디터 Play 중에는 빌드 목록에 없는 씬도 경로로 다시 연다 (C 씬은 아직 빌드 목록에 없다)
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(scene.buildIndex);
#endif
        }
    }
}
