using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace MilTrainSimVR.A.Common
{
    // 허브 시작 자리 (11/09). 처음 켜면 m_Start(정문 앞)에, 과정에서 돌아오면 그 과정 입구의 돌아오는 자리에 리그를 놓고 화면을 밝힌다.
    // 리그 자체를 옮긴다 — 플레이 공간 안에서 서 있는 자리는 그대로 (씬을 처음 켤 때와 같다).
    public class HubStart : MonoBehaviour
    {
        [Tooltip("처음 켰을 때 설 자리 — 앞(+Z)이 바라볼 방향")]
        [SerializeField] Transform m_Start;

        [Tooltip("비우면 씬에서 찾는다")]
        [SerializeField] XROrigin m_Origin;
        [SerializeField] ScreenFader m_Fader;

        [SerializeField] float m_FadeInSeconds = 0.6f;

        void Awake()
        {
            if (m_Origin == null)
                m_Origin = FindFirstObjectByType<XROrigin>();
            if (m_Fader == null)
                m_Fader = FindFirstObjectByType<ScreenFader>();

            Transform point = m_Start;
            if (!string.IsNullOrEmpty(CourseRoute.Last))
                foreach (var portal in FindObjectsByType<HubPortal>(FindObjectsSortMode.None))
                    if (portal.CourseId == CourseRoute.Last && portal.ReturnPoint != null)
                        point = portal.ReturnPoint;

            if (m_Origin != null && point != null)
            {
                m_Origin.transform.SetPositionAndRotation(point.position, Quaternion.Euler(0f, point.eulerAngles.y, 0f));
                Debug.Log($"[Hub] 시작 자리 {point.name} (돌아온 과정 {CourseRoute.Last ?? "-"})");
            }
        }

        IEnumerator Start()
        {
            if (m_Fader == null)
                yield break;
            m_Fader.SetAlpha(1f);
            yield return m_Fader.FadeTo(0f, m_FadeInSeconds);
        }
    }
}
