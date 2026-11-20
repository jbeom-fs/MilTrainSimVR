using MilTrainSimVR.A.Rifle;
using UnityEngine;

namespace MilTrainSimVR.A.Range
{
    // 약실 확인 (사로 기획 5절) — 장전 손잡이를 끝까지 뒤로 당긴 채 배출구를 일정 시간 바라보면 확인으로 본다.
    //   · 장전 손잡이: 끝까지 후퇴 (Offset ≥ 행정 - 여유). 놓으면 앞으로 돌아오므로 손으로 잡고 있어야 한다
    //   · 머리(카메라): 정면과 배출구(Anchor_EjectionPort) 방향의 각도 ≤ m_LookAngle, 거리 ≤ m_LookDistance
    // 두 조건을 m_HoldSeconds 동안 이어서 만족하면 Confirmed. 하나라도 끊기면 처음부터 다시 센다. 값은 임시 — 헤드셋으로 조정.
    // 판정만 한다. 언제 켜고 끌지는 사로 판정(RangeProcedure)이 정한다.
    public class ChamberCheck : MonoBehaviour
    {
        [SerializeField] RangeRifle m_Rifle;

        [Tooltip("배출구 위치 표시 — 비우면 총에서 Anchor_EjectionPort 를 찾는다")]
        [SerializeField] Transform m_EjectionPort;

        [Tooltip("머리 — 비우면 Main Camera")]
        [SerializeField] Transform m_Head;

        [SerializeField] float m_LookAngle = 35f;
        [SerializeField] float m_LookDistance = 0.8f;
        [SerializeField] float m_HoldSeconds = 1f;

        [Tooltip("끝까지 당겼다고 볼 여유 (m)")]
        [SerializeField] float m_BackSlack = 0.005f;

        float m_Held;
        bool m_Active;

        public event System.Action Confirmed;

        // 0 ~ 1 — 학습 모드 진행 표시용
        public float Progress => m_HoldSeconds > 0f ? Mathf.Clamp01(m_Held / m_HoldSeconds) : 0f;
        public bool Active => m_Active;

        void Awake()
        {
            if (m_EjectionPort == null && m_Rifle != null)
                m_EjectionPort = m_Rifle.transform.Find("Anchor_EjectionPort");
        }

        // 사격 방식에 따라 총을 바꿀 때 (RifleStyleSwitch) — 배출구도 그 총 것으로
        internal void UseRifle(RangeRifle rifle)
        {
            m_Rifle = rifle;
            Transform port = rifle.transform.Find("Anchor_EjectionPort");
            if (port != null)
                m_EjectionPort = port;
        }

        public void Begin()
        {
            m_Held = 0f;
            m_Active = true;
        }

        public void Stop()
        {
            m_Held = 0f;
            m_Active = false;
        }

        public bool HandleBack
        {
            get
            {
                return m_Rifle.ChargingStroke > 0f && m_Rifle.ChargingOffset >= m_Rifle.ChargingStroke - m_BackSlack;
            }
        }

        public bool Looking
        {
            get
            {
                Transform head = m_Head != null ? m_Head : Camera.main != null ? Camera.main.transform : null;
                if (head == null || m_EjectionPort == null)
                    return false;
                Vector3 toPort = m_EjectionPort.position - head.position;
                return toPort.magnitude <= m_LookDistance && Vector3.Angle(head.forward, toPort) <= m_LookAngle;
            }
        }

        void Update()
        {
            if (!m_Active)
                return;
            m_Held = HandleBack && Looking ? m_Held + Time.deltaTime : 0f;
            if (m_Held >= m_HoldSeconds)
                Confirm();
        }

        // 시험용 — 손·머리 없이 확인된 것으로
        public void TestConfirm()
        {
            if (m_Active)
                Confirm();
        }

        void Confirm()
        {
            m_Active = false;
            Debug.Log("[Range] 약실 확인");
            Confirmed?.Invoke();
        }
    }
}
