using System.Collections.Generic;
using MilTrainSimVR.A.Common;
using MilTrainSimVR.A.Rifle;
using UnityEngine;

namespace MilTrainSimVR.A.Range
{
    public enum RangeViolation
    {
        PrematureFire,         // 지시 전 격발 — 영점사격 구간이 아닌데 실탄이 나감
        MuzzleDirection,       // 총구 방향 — 탄이 들어 있는 총을 든 채 총구가 표적 방향에서 크게 벗어남
        InsertNotSafe,         // 순서 위반 — 조정간이 안전이 아닌 채 탄창 결합
        MagazineRemovedEarly,  // 순서 위반 — 사격 후 안전 검사 전에 탄창 분리
    }

    // 안전 위반 (사로 기획 2-4절) — 절차 실수(RangeProcedure)와 따로 센다. 총은 막지 않고 기록·경고만 한다.
    // 언제 무엇이 허용되는지는 흐름(RangeTrainingFlow)이 정한다: 격발은 영점사격 구간에서만, 탄창 분리는 안전 검사 구간에서만.
    // 탄창이 꽂힌 채 약실 확인은 절차 실수(MagazineStillIn)로 센다 — 여기서 두 번 세지 않는다.
    public class RangeSafetyMonitor : MonoBehaviour
    {
        [SerializeField] RangeRifle m_Rifle;
        [SerializeField] TrainingFeedback m_Feedback;

        [Tooltip("표적 — 총구 방향의 기준")]
        [SerializeField] Transform m_Downrange;

        [Tooltip("총구가 표적 방향에서 수평으로 이 각도(°)를 넘으면 위반. 임시값")]
        [SerializeField] float m_MuzzleAngle = 90f;

        [Tooltip("위반 뒤 이 각도(°) 안으로 돌아와야 다음 위반을 다시 센다 — 경계에서 떨며 여러 번 세지 않게")]
        [SerializeField] float m_MuzzleReturnAngle = 80f;

        [Tooltip("같은 위반 피드백을 다시 내기까지 기다리는 시간(초)")]
        [SerializeField] float m_Cooldown = 1.5f;

        readonly int[] m_Counts = new int[System.Enum.GetValues(typeof(RangeViolation)).Length];
        readonly float[] m_NextFeedback = new float[System.Enum.GetValues(typeof(RangeViolation)).Length];
        bool m_Active;
        bool m_MuzzleAway;

        public event System.Action<RangeViolation> Violated;

        // 흐름이 구간마다 정한다.
        // 허용을 끈 그 프레임에 나간 탄은 허용된 탄으로 본다 — 흐름은 조의 마지막 탄(Fired)을 받고 바로 조를 닫아 허용을 끄는데,
        // 같은 Fired 를 이 감시가 나중에 받으면 그 마지막 탄이 지시 전 격발로 세졌다 (11/03 시뮬레이션: 조마다 위반 1회)
        public bool FiringAllowed
        {
            get => m_FiringAllowed;
            set
            {
                if (m_FiringAllowed && !value)
                    m_AllowedUntilFrame = Time.frameCount;
                m_FiringAllowed = value;
            }
        }
        bool m_FiringAllowed;
        int m_AllowedUntilFrame = -1;
        public bool MagazineRemovalAllowed { get; set; }

        // 시험용 — 손 없이 "든 것"으로 친다 (총구 방향 검사는 든 총만 본다)
        public bool TestHeld { get; set; }

        public int Total
        {
            get
            {
                int sum = 0;
                foreach (int c in m_Counts)
                    sum += c;
                return sum;
            }
        }

        public int Count(RangeViolation violation) => m_Counts[(int)violation];

        // 사격 방식에 따라 총을 바꿀 때 (RifleStyleSwitch, OnEnable 전에)
        internal void UseRifle(RangeRifle rifle) => m_Rifle = rifle;

        void OnEnable()
        {
            m_Rifle.Fired += OnFired;
            m_Rifle.MagazineInserted += OnInserted;
            m_Rifle.MagazineRemoved += OnRemoved;
        }

        void OnDisable()
        {
            m_Rifle.Fired -= OnFired;
            m_Rifle.MagazineInserted -= OnInserted;
            m_Rifle.MagazineRemoved -= OnRemoved;
        }

        // 한 회를 새로 시작할 때 — 위반 수를 비우고 감시를 켠다
        public void Begin()
        {
            System.Array.Clear(m_Counts, 0, m_Counts.Length);
            System.Array.Clear(m_NextFeedback, 0, m_NextFeedback.Length);
            m_MuzzleAway = false;
            FiringAllowed = false;
            MagazineRemovalAllowed = false;
            m_Active = true;
        }

        public void Stop()
        {
            m_Active = false;
            TestHeld = false;
        }

        void Update()
        {
            if (!m_Active || m_Downrange == null)
                return;
            bool held = m_Rifle.IsHeld || TestHeld;
            bool loaded = m_Rifle.HasMagazine || m_Rifle.Chambered;
            if (!held || !loaded)
            {
                m_MuzzleAway = false;
                return;
            }
            float angle = HorizontalAngle();
            if (!m_MuzzleAway && angle > m_MuzzleAngle)
            {
                m_MuzzleAway = true;
                Report(RangeViolation.MuzzleDirection);
            }
            else if (m_MuzzleAway && angle < m_MuzzleReturnAngle)
            {
                m_MuzzleAway = false;
            }
        }

        // 총구와 표적 방향 사이의 수평 각도 — 위아래로 든 것은 보지 않는다
        public float HorizontalAngle()
        {
            Vector3 muzzle = Vector3.ProjectOnPlane(m_Rifle.MuzzleForward, Vector3.up);
            Vector3 target = Vector3.ProjectOnPlane(m_Downrange.position - m_Rifle.transform.position, Vector3.up);
            if (muzzle.sqrMagnitude < 1e-4f)
                return 0f;   // 총구가 거의 수직 — 수평 방향이 없다
            return Vector3.Angle(muzzle, target);
        }

        void OnFired()
        {
            if (m_Active && !FiringAllowed && m_AllowedUntilFrame != Time.frameCount)
                Report(RangeViolation.PrematureFire);
        }

        void OnInserted()
        {
            if (m_Active && m_Rifle.Mode != RangeRifle.FireMode.Safe)
                Report(RangeViolation.InsertNotSafe);
        }

        void OnRemoved()
        {
            if (m_Active && !MagazineRemovalAllowed)
                Report(RangeViolation.MagazineRemovedEarly);
        }

        // 위반은 매번 세고, 경고음·자막은 위반 종류마다 m_Cooldown 간격으로만 낸다
        void Report(RangeViolation violation)
        {
            m_Counts[(int)violation]++;
            Debug.Log($"[Range] 안전 위반: {violation}");
            if (Time.time < m_NextFeedback[(int)violation])
                return;
            m_NextFeedback[(int)violation] = Time.time + m_Cooldown;
            m_Feedback?.PlayError();
            Violated?.Invoke(violation);
        }
    }
}
