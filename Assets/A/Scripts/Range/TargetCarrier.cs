using System.Collections;
using UnityEngine;

namespace MilTrainSimVR.A.Range
{
    // 표적 이송 — 레일에 매달린 표적(이 오브젝트 통째로)을 앞뒤(부모 기준 z)로 옮긴다 (11/02, 1029 마감 4절 2번).
    // 25 m 자리(Far)와 사수 앞 자리(Near) 사이를 오간다. 출발할 때 빨라지고 도착 전에 느려진다.
    // Exchange: 사수 쪽으로 왔다가 → 표적지 바꾸는 시간만큼 서 있다가 → 다시 25 m 로 (시험 메뉴용).
    // 교육 흐름(RangeTrainingFlow)은 MoveFar·MoveNear·SnapNear 와 Arrived 로 조마다 보낸다.
    public class TargetCarrier : MonoBehaviour
    {
        [Tooltip("25 m 표적 자리 (부모 기준 z)")]
        [SerializeField] float m_FarZ = 29f;

        [Tooltip("사수 앞 자리 (부모 기준 z) — 부스 위 천장 덮개(z 5.6) 앞에서 멈춘다")]
        [SerializeField] float m_NearZ = 6.2f;

        [Tooltip("최고 속도 (m/s, 임시값)")]
        [SerializeField] float m_Speed = 2.5f;

        [Tooltip("빨라지고 느려지는 정도 (m/s², 임시값)")]
        [SerializeField] float m_Acceleration = 1.5f;

        [Tooltip("교체 때 사수 앞에서 서 있는 시간 (초, 임시값)")]
        [SerializeField] float m_SwapSeconds = 2f;

        const float k_MinSpeed = 0.05f;   // 도착 직전에 기어가지 않게

        float m_GoalZ;
        float m_Velocity;
        bool m_Moving;
        Coroutine m_Exchange;

        public bool IsMoving => m_Moving;
        public bool AtFar => !m_Moving && Mathf.Approximately(Z, m_FarZ);
        public bool AtNear => !m_Moving && Mathf.Approximately(Z, m_NearZ);

        // 도착할 때마다 — true 면 25 m 자리
        public event System.Action<bool> Arrived;

        float Z => transform.localPosition.z;

        public void MoveFar() => MoveTo(m_FarZ);

        public void MoveNear() => MoveTo(m_NearZ);

        // 사수 쪽으로 왔다가 다시 25 m 로
        public void Exchange()
        {
            StopExchange();
            m_Exchange = StartCoroutine(ExchangeRoutine());
        }

        // 움직이지 않고 바로 그 자리로
        public void SnapFar() => Snap(m_FarZ);

        public void SnapNear() => Snap(m_NearZ);

        void Snap(float z)
        {
            StopExchange();
            m_Moving = false;
            m_Velocity = 0f;
            SetZ(z);
        }

        void MoveTo(float z)
        {
            StopExchange();
            Go(z);
        }

        void Go(float z)
        {
            m_GoalZ = z;
            m_Moving = !Mathf.Approximately(Z, z);
            if (!m_Moving)
                Arrived?.Invoke(Mathf.Approximately(z, m_FarZ));
        }

        IEnumerator ExchangeRoutine()
        {
            Go(m_NearZ);
            while (m_Moving)
                yield return null;
            yield return new WaitForSeconds(m_SwapSeconds);
            Go(m_FarZ);
            m_Exchange = null;
        }

        void StopExchange()
        {
            if (m_Exchange != null)
                StopCoroutine(m_Exchange);
            m_Exchange = null;
        }

        void Update()
        {
            if (!m_Moving)
                return;
            float distance = m_GoalZ - Z;
            // 남은 거리 안에 멈출 수 있는 속도까지만 (도착 전에 느려진다)
            float allowed = Mathf.Max(k_MinSpeed, Mathf.Min(m_Speed, Mathf.Sqrt(2f * m_Acceleration * Mathf.Abs(distance))));
            m_Velocity = Mathf.Min(allowed, m_Velocity + m_Acceleration * Time.deltaTime);
            float step = m_Velocity * Time.deltaTime;
            if (step >= Mathf.Abs(distance))
            {
                SetZ(m_GoalZ);
                m_Velocity = 0f;
                m_Moving = false;
                Arrived?.Invoke(Mathf.Approximately(m_GoalZ, m_FarZ));
                return;
            }
            SetZ(Z + Mathf.Sign(distance) * step);
        }

        void SetZ(float z)
        {
            Vector3 p = transform.localPosition;
            transform.localPosition = new Vector3(p.x, p.y, z);
        }
    }
}
