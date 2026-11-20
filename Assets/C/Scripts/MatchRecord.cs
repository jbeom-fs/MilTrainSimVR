using System.Collections.Generic;
using UnityEngine;

namespace MilTrainSimVR.UrbanCombat
{
    // 한 경기의 기록 — 끝난 뒤 결과판(평가, C 범위 문서 6번)에 쓴다 (10/27).
    //   · 플레이어가 쏜 탄 수와 적 몸에 맞힌 수 → 명중률
    //   · 적이 쓰러질 때마다 어느 부위로(출혈이면 부위 없음), 누가 쓰러뜨렸는지 → 부위별 처치
    //   · 플레이어가 맞은 부위와 결과는 플레이어 몸 상태(CombatantHealth.Hits)에 이미 쌓여 있다
    // 경기 중(MatchFlow 가 Begin ~ Stop)에만 센다. 누가 맞혔는지는 Gunfire.BodyHit 로 듣는다 — 몸 상태는 쏜 사람을 모른다
    public class MatchRecord : MonoBehaviour
    {
        // 적 한 명이 쓰러진 기록
        public struct Kill
        {
            public string name;
            public HitZone.Part? part;   // 마지막으로 맞은 부위 — 출혈로 쓰러지면 없음
            public bool byPlayer;        // 마지막으로 맞힌 게 플레이어 총인가 (아니면 적끼리 맞힘)
            public float time;           // 경기 시작부터(초)
        }

        bool m_Recording;
        float m_StartTime;
        CombatantHealth m_Player;
        readonly List<CombatantHealth> m_Enemies = new List<CombatantHealth>();
        readonly Dictionary<CombatantHealth, Component> m_LastHitter = new Dictionary<CombatantHealth, Component>();
        readonly List<Kill> m_Kills = new List<Kill>();

        public int Shots { get; private set; }
        public int Hits { get; private set; }
        public IReadOnlyList<Kill> Kills => m_Kills;
        public float StartTime => m_StartTime;

        void OnEnable()
        {
            Gunfire.ShotFired += OnShotFired;
            Gunfire.BodyHit += OnBodyHit;
        }

        void OnDisable()
        {
            Gunfire.ShotFired -= OnShotFired;
            Gunfire.BodyHit -= OnBodyHit;
        }

        void OnDestroy()
        {
            foreach (CombatantHealth enemy in m_Enemies)
            {
                if (enemy != null)
                    enemy.StateChanged -= OnEnemyStateChanged;
            }
        }

        public void Begin(CombatantHealth player, IEnumerable<CombatantHealth> enemies)
        {
            m_Player = player;
            m_StartTime = Time.time;
            m_Recording = true;
            foreach (CombatantHealth enemy in enemies)
            {
                m_Enemies.Add(enemy);
                enemy.StateChanged += OnEnemyStateChanged;
            }
        }

        public void Stop()
        {
            m_Recording = false;
        }

        // 플레이어 총(TempRifle)의 탄만 센다 — 벽 속 총구라 막힌 탄도 쏜 탄이다
        void OnShotFired(Vector3 origin, Component shooter)
        {
            if (m_Recording && shooter is TempRifle)
                Shots++;
        }

        void OnBodyHit(Component shooter, HitZone zone)
        {
            CombatantHealth target = zone.Owner;
            if (target == null)
                return;
            m_LastHitter[target] = shooter;
            // 살아 있는 적에게 맞힌 것만 명중 — 쓰러진 몸에 더 쏜 건 빼고
            if (m_Recording && shooter is TempRifle && target != m_Player && target.IsAlive)
                Hits++;
        }

        void OnEnemyStateChanged(CombatantHealth who, CombatantHealth.State state, HitZone.Part? part)
        {
            if (!m_Recording || state != CombatantHealth.State.Dead)
                return;
            m_LastHitter.TryGetValue(who, out Component hitter);
            m_Kills.Add(new Kill { name = who.DisplayName, part = part, byPlayer = hitter is TempRifle, time = Time.time - m_StartTime });
        }
    }
}
