using Unity.XR.CoreUtils;
using UnityEngine;

namespace MilTrainSimVR.A.Common
{
    // 시험용 — 앉아서 헤드셋 테스트를 할 때 눈높이를 서 있는 높이로 맞춘다. 교육 기획은 서서 하는 것이 기준이다.
    // XR Origin 에 붙인다. 머리 추적이 잡히면 한 번, 눈높이가 m_EyeHeight 가 되도록 Camera Offset 높이를 정하고 그 값을 유지한다.
    // 추적 기준(Floor/Device/Unbounded)에 상관없이 실제 머리 높이를 재서 맞춘다 — XROrigin 은 Floor 면 0, 그 밖이면 Camera Y Offset 을
    // 쓰는데, Quest Link 가 어느 기준으로 올지 확인하지 않았다. XROrigin 이 기준이 바뀔 때 높이를 다시 정하므로 매 프레임 맞춘다.
    [RequireComponent(typeof(XROrigin))]
    public class SeatedTestHeight : MonoBehaviour
    {
        [Tooltip("켜면 앉아서 테스트해도 눈높이를 아래 값으로 맞춘다. 서서 할 때는 끈다")]
        [SerializeField] bool m_Seated = true;

        [Tooltip("맞출 눈높이(m) — 서 있을 때 눈높이")]
        [SerializeField] float m_EyeHeight = 1.6f;

        [Tooltip("머리 추적이 잡힌 뒤 이만큼(초) 기다렸다가 잰다 — 헤드셋을 쓰는 동안 흔들린 값을 피한다")]
        [SerializeField] float m_SettleSeconds = 1f;

        XROrigin m_Origin;
        float m_TrackedSince = -1f;
        float m_OffsetY = float.NaN;   // 정한 Camera Offset 높이. 아직 안 쟀으면 NaN

        void Awake() => m_Origin = GetComponent<XROrigin>();

        void LateUpdate()
        {
            if (!m_Seated || m_Origin.Camera == null || m_Origin.CameraFloorOffsetObject == null)
                return;
            var offset = m_Origin.CameraFloorOffsetObject.transform;

            if (float.IsNaN(m_OffsetY))
            {
                // 추적 전에는 카메라 로컬 위치가 정확히 0 이다
                float headY = m_Origin.Camera.transform.localPosition.y;
                if (Mathf.Approximately(headY, 0f))
                    return;
                if (m_TrackedSince < 0f)
                    m_TrackedSince = Time.time;
                if (Time.time - m_TrackedSince < m_SettleSeconds)
                    return;
                m_OffsetY = m_EyeHeight - headY;
                Debug.Log($"[A] 앉은 테스트 높이: 추적 기준 {m_Origin.CurrentTrackingOriginMode}, 머리 {headY:F2} m → Camera Offset {m_OffsetY:F2} m (눈높이 {m_EyeHeight:F2} m)");
            }

            var p = offset.localPosition;
            if (!Mathf.Approximately(p.y, m_OffsetY))
                offset.localPosition = new Vector3(p.x, m_OffsetY, p.z);
        }

        // 지금 머리 높이로 다시 잰다 — 자세를 바꿨을 때 (Inspector 우클릭)
        [ContextMenu("다시 재기")]
        public void Recalibrate()
        {
            m_OffsetY = float.NaN;
            m_TrackedSince = -1f;
        }
    }
}
