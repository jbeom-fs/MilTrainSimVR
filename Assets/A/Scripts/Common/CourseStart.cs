using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace MilTrainSimVR.A.Common
{
    // 과정 씬 시작 자리 (11/10). 허브에서 고른 과정 ID(CourseRoute.Current)에 맞는 자리에 리그를 놓는다.
    // 목록에 없는 과정은 리그가 씬에 놓인 자리 그대로. 씬을 바로 실행하면(Current = null) 곧바로 허브로 넘어간다 (m_StartFromHub, 11/10 — 끄면 씬 자리 그대로).
    // 허브에서 넘어왔으면 까만 화면에서 밝아진다 (포탈이 까맣게 만든 채 씬을 불러온다).
    // 리그 자체를 옮긴다 — HubStart 와 같은 방식.
    public class CourseStart : MonoBehaviour
    {
        [System.Serializable]
        public struct Entry
        {
            [Tooltip("과정 ID (예: A_Range)")]
            public string CourseId;

            [Tooltip("설 자리 — 바닥 위 점, 앞(+Z)이 바라볼 방향")]
            public Transform Point;
        }

        [SerializeField] Entry[] m_Entries;

        [Tooltip("비우면 씬에서 찾는다")]
        [SerializeField] XROrigin m_Origin;
        [SerializeField] ScreenFader m_Fader;

        [SerializeField] float m_FadeInSeconds = 0.6f;

        [Tooltip("허브를 거치지 않고 이 씬을 바로 실행하면(과정 없음) 곧바로 허브로 넘어간다 (11/10 사용자: 무조건 허브에서 시작). " +
                 "이 씬만 바로 시험할 때는 끈다 — 끄면 씬에 놓인 리그 자리(내무반)에서 시작")]
        [SerializeField] bool m_StartFromHub = true;

        bool m_ToHub;

        void Awake()
        {
            if (m_Origin == null)
                m_Origin = FindFirstObjectByType<XROrigin>();
            if (m_Fader == null)
                m_Fader = FindFirstObjectByType<ScreenFader>();

            string course = CourseRoute.Current;
            if (string.IsNullOrEmpty(course) && m_StartFromHub)
            {
                Debug.Log("[Course] 과정 없이 시작 — 허브로");
                m_ToHub = true;
                CourseRoute.ReturnToHub();
                return;
            }
            if (string.IsNullOrEmpty(course) || m_Origin == null || m_Entries == null)
                return;
            foreach (var entry in m_Entries)
            {
                if (entry.CourseId != course || entry.Point == null)
                    continue;
                m_Origin.transform.SetPositionAndRotation(entry.Point.position, Quaternion.Euler(0f, entry.Point.eulerAngles.y, 0f));
                Debug.Log($"[Course] 시작 자리 {entry.Point.name} (과정 {course})");
                return;
            }
        }

        IEnumerator Start()
        {
            if (m_Fader == null)
                yield break;
            if (m_ToHub)
            {
                m_Fader.SetAlpha(1f);   // 허브를 불러오는 한 프레임 동안 내무반이 비치지 않게 — 허브가 밝힌다
                yield break;
            }
            if (string.IsNullOrEmpty(CourseRoute.Current))
                yield break;
            m_Fader.SetAlpha(1f);
            yield return m_Fader.FadeTo(0f, m_FadeInSeconds);
        }
    }
}
