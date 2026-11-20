using UnityEngine;

namespace MilTrainSimVR.UrbanCombat
{
    // 적 동작 보기 씬(C_EnemyShowcase)에서 병사 한 명이 한 가지 동작을 되풀이한다 (11/03) — 총 쥔 손 · 자세를 가까이서 보려고.
    // 길찾기 · 쏘기 없이 EnemySoldier.Show 로 자세만 시킨다 (걷기 · 뛰기는 제자리).
    //   · 돌아가며: 대기 → 걷기 → 뛰기 → 걷기 → 대기 → 조준을 몇 초씩 — 자세가 바뀌는 모습
    //   · 쓰러짐: 머리 · 심장을 번갈아 맞고 몇 초 뒤 다시 일어난다 — 헤드샷 · 보통 쓰러짐이 각각 둘 중 무작위로 나온다
    [RequireComponent(typeof(EnemySoldier))]
    public class EnemyShowcase : MonoBehaviour
    {
        public enum Act { Idle, Walk, Run, Aim, Cycle, Deaths }

        static readonly int[] k_Cycle =
        {
            EnemySoldier.PoseIdle, EnemySoldier.PoseWalk, EnemySoldier.PoseRun, EnemySoldier.PoseWalk, EnemySoldier.PoseIdle, EnemySoldier.PoseAim,
        };

        [SerializeField] Act m_Act;

        [Tooltip("조준할 곳 — 비우면 몸 앞 10m, 가슴 높이")]
        [SerializeField] Transform m_AimTarget;

        [Tooltip("돌아가며 · 쓰러짐: 한 동작을 보여 주는 시간(초)")]
        [SerializeField] float m_Seconds = 3f;

        EnemySoldier m_Soldier;
        CombatantHealth m_Health;
        float m_Next;
        int m_Step;
        bool m_Headshot;

        public void Setup(Act act, Transform aimTarget)
        {
            m_Act = act;
            m_AimTarget = aimTarget;
        }

        void Awake()
        {
            m_Soldier = GetComponent<EnemySoldier>();
            m_Health = GetComponent<CombatantHealth>();
        }

        void Update()
        {
            Vector3 aim = m_AimTarget != null ? m_AimTarget.position : transform.TransformPoint(new Vector3(0f, 1.3f, 10f));
            switch (m_Act)
            {
                case Act.Cycle:
                    if (Time.time >= m_Next)
                    {
                        m_Step = (m_Step + 1) % k_Cycle.Length;
                        m_Next = Time.time + m_Seconds;
                    }
                    m_Soldier.Show(k_Cycle[m_Step], aim);
                    break;
                case Act.Deaths:
                    m_Soldier.Show(EnemySoldier.PoseIdle, aim);
                    if (Time.time < m_Next)
                        break;
                    if (m_Health.IsAlive)
                    {
                        m_Headshot = !m_Headshot;
                        m_Health.TakeHit(m_Headshot ? HitZone.Part.Head : HitZone.Part.Heart, transform.position);
                        m_Next = Time.time + m_Seconds + 1.5f;   // 쓰러짐은 3~4초
                    }
                    else
                    {
                        m_Health.ResetState();
                        m_Next = Time.time + m_Seconds * 0.5f;
                    }
                    break;
                default:
                    m_Soldier.Show(m_Act switch
                    {
                        Act.Walk => EnemySoldier.PoseWalk,
                        Act.Run => EnemySoldier.PoseRun,
                        Act.Aim => EnemySoldier.PoseAim,
                        _ => EnemySoldier.PoseIdle,
                    }, aim);
                    break;
            }
        }
    }
}
