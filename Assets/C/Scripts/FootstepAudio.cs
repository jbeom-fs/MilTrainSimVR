using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;

namespace MilTrainSimVR.UrbanCombat
{
    // 발소리 (11/16) — 플레이어(스틱 이동)와 적(걷기 · 뛰기)이 움직인 거리만큼 한 걸음씩 낸다. 플레이어 리그(C_XR Origin)에 붙는다.
    // 클립은 달리기 녹음을 걸음마다 자른 8개(Assets/C/Audio/Footsteps, 임시) — 무작위로, 바로 앞 걸음과 다르게.
    //   · 플레이어: 리그(XR Origin)가 움직인 수평 거리만 센다 — 몸으로 걷는 것(룸스케일)은 실제 발소리가 나니까. 땅에 있을 때만(GravityProvider),
    //     점프 뒤 착지하면 한 번 크게. 내 발소리는 머리 가운데에서 들리게 2D
    //   · 적: 길찾기로 움직인 거리, 살아 있는 동안만. 적 발밑에서 3D + 거리 · 벽 가림(CombatAudio) — 소리로 적이 어디 있는지 안다
    // 소리 내는 목소리 · 거리 · 가림 계산은 CombatAudio 를 같이 쓴다 (같은 오브젝트에 있어야 한다)
    public class FootstepAudio : MonoBehaviour
    {
        [SerializeField] AudioClip[] m_Steps;

        [Header("플레이어")]
        [Tooltip("한 걸음(m) — 이동 속도 2.5 m/s 면 초당 3.3걸음")]
        [SerializeField] float m_PlayerStride = 0.75f;
        [SerializeField] float m_PlayerVolume = 0.35f;
        [Tooltip("이만큼(초) 넘게 공중에 있다 내려오면 착지 소리")]
        [SerializeField] float m_LandingAirTime = 0.25f;
        [SerializeField] float m_LandingVolume = 0.7f;

        [Header("적")]
        [SerializeField] float m_EnemyStride = 0.8f;
        [SerializeField] float m_EnemyVolume = 0.8f;
        [SerializeField] float m_EnemyMinDistance = 1.5f;
        [SerializeField] float m_EnemyMaxDistance = 30f;

        class Walker
        {
            public Transform Body;
            public CombatantHealth Health;
            public Vector3 Last;
            public float Travelled;
        }

        CombatAudio m_Audio;
        XROrigin m_Rig;
        GravityProvider m_Gravity;
        Vector3 m_PlayerLast;
        float m_PlayerTravelled;
        float m_AirTime;
        int m_LastStep = -1;
        readonly List<Walker> m_Enemies = new List<Walker>();

        // 테스트(Unity CLI)가 읽는다
        public int PlayerSteps { get; private set; }
        public int EnemySteps { get; private set; }
        public int Landings { get; private set; }

        void Awake()
        {
            m_Audio = GetComponent<CombatAudio>();
            m_Rig = GetComponentInParent<XROrigin>();
            m_Gravity = m_Rig != null ? m_Rig.GetComponentInChildren<GravityProvider>(true) : null;
        }

        void Start()
        {
            // 적은 씬에 처음부터 있다 (다시하기도 같은 오브젝트를 처음 자리로 되돌린다)
            foreach (EnemyMover mover in FindObjectsByType<EnemyMover>(FindObjectsSortMode.None))
                m_Enemies.Add(new Walker { Body = mover.transform, Health = mover.GetComponent<CombatantHealth>(), Last = mover.transform.position });
            if (m_Rig != null)
                m_PlayerLast = m_Rig.transform.position;
        }

        void Update()
        {
            if (m_Audio == null || m_Steps == null || m_Steps.Length == 0)
                return;
            UpdatePlayer();
            foreach (Walker w in m_Enemies)
                UpdateEnemy(w);
        }

        void UpdatePlayer()
        {
            if (m_Rig == null)
                return;
            Vector3 now = m_Rig.transform.position;
            Vector3 moved = now - m_PlayerLast;
            m_PlayerLast = now;
            bool grounded = m_Gravity == null || m_Gravity.isGrounded;
            if (!grounded)
            {
                m_AirTime += Time.deltaTime;
                return;
            }
            if (m_AirTime >= m_LandingAirTime)
            {
                PlayerStep(m_LandingVolume);
                Landings++;
                m_PlayerTravelled = 0f;
            }
            m_AirTime = 0f;
            float flat = new Vector2(moved.x, moved.z).magnitude;
            if (flat > 1.5f)
            {
                // 순간이동(다시하기 · 포탈) — 걸은 게 아니다
                m_PlayerTravelled = 0f;
                return;
            }
            m_PlayerTravelled += flat;
            if (m_PlayerTravelled >= m_PlayerStride)
            {
                m_PlayerTravelled -= m_PlayerStride;
                PlayerStep(m_PlayerVolume);
                PlayerSteps++;
            }
        }

        void PlayerStep(float volume)
        {
            Transform head = m_Rig.Camera != null ? m_Rig.Camera.transform : m_Rig.transform;
            CombatAudio.Sound s = m_Audio.Basic(NextClip(), new Vector3(head.position.x, m_Rig.transform.position.y, head.position.z), volume, CombatAudio.Kind.Step);
            s.Pitch = 1f + Random.Range(-0.05f, 0.05f);
            s.SpatialBlend = 0f;
            m_Audio.Play(s);
        }

        void UpdateEnemy(Walker w)
        {
            if (w.Body == null)
                return;
            Vector3 now = w.Body.position;
            Vector3 moved = now - w.Last;
            w.Last = now;
            if (w.Health != null && !w.Health.IsAlive)
            {
                w.Travelled = 0f;
                return;
            }
            float flat = new Vector2(moved.x, moved.z).magnitude;
            if (flat > 1.5f)
            {
                w.Travelled = 0f;
                return;
            }
            w.Travelled += flat;
            if (w.Travelled < m_EnemyStride)
                return;
            w.Travelled -= m_EnemyStride;
            // 로그 감쇠는 최대 거리 밖에서도 그 크기로 남는다 — 멀리 있는 적의 발소리는 아예 내지 않는다
            Transform head = m_Rig != null && m_Rig.Camera != null ? m_Rig.Camera.transform : null;
            if (head != null && (head.position - now).sqrMagnitude > m_EnemyMaxDistance * m_EnemyMaxDistance)
                return;
            CombatAudio.Sound s = m_Audio.Basic(NextClip(), now, m_EnemyVolume, CombatAudio.Kind.Step);
            s.Pitch = 1f + Random.Range(-0.06f, 0.06f);
            s.MinDistance = m_EnemyMinDistance;
            s.MaxDistance = m_EnemyMaxDistance;
            m_Audio.AddDistanceAndWalls(ref s, w.Body, false);
            m_Audio.Play(s);
            EnemySteps++;
        }

        AudioClip NextClip()
        {
            int i = Random.Range(0, m_Steps.Length);
            if (m_Steps.Length > 1 && i == m_LastStep)
                i = (i + 1) % m_Steps.Length;
            m_LastStep = i;
            return m_Steps[i];
        }
    }
}
