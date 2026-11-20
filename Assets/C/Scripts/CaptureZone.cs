using System.Collections.Generic;
using UnityEngine;

namespace MilTrainSimVR.UrbanCombat
{
    // 점령지 (10/27) — 오버타임에 열리는 곳 (C 범위 문서 3번: N21 2층 전체 → 11/16 사용자: 백화점 3층 전체). 이 상자(위치·회전·크기) 안에 누가 있는지 센다.
    // 콜라이더·트리거를 안 쓰고 위치로 본다 — 플레이어는 머리(카메라) 밑 발 자리, 적은 발(원점).
    // 오버타임에 적이 가서 지킬 자리(앞 = 보는 쪽)와, 열리면 켜지는 표시(멀리서도 보이는 빛기둥)를 갖고 있다
    public class CaptureZone : MonoBehaviour
    {
        [Tooltip("화면에 쓰는 이름 — 건물 번호(N21 등) 말고 누구나 아는 이름 (손목 표시 · 알림: \"점령지 백화점 3층이 열렸다\")")]
        [SerializeField] string m_DisplayName = "백화점 3층";

        [Tooltip("상자 크기(m) — 이 오브젝트의 위치·회전 기준. 크기(Scale)는 1 로 둔다")]
        [SerializeField] Vector3 m_Size = new Vector3(13.8f, 3f, 16.5f);

        [Tooltip("오버타임에 적이 지킬 자리 — 자리의 앞(+Z)이 보는 쪽")]
        [SerializeField] Transform[] m_Spots = new Transform[0];

        [Tooltip("점령지가 열리면 켜지는 표시")]
        [SerializeField] GameObject m_Marker;

        public string DisplayName => m_DisplayName;
        public IReadOnlyList<Transform> Spots => m_Spots;

        public bool Contains(Vector3 worldPoint)
        {
            Vector3 p = transform.InverseTransformPoint(worldPoint);
            return Mathf.Abs(p.x) <= m_Size.x * 0.5f && Mathf.Abs(p.y) <= m_Size.y * 0.5f && Mathf.Abs(p.z) <= m_Size.z * 0.5f;
        }

        public void SetOpen(bool open)
        {
            if (m_Marker != null)
                m_Marker.SetActive(open);
        }

#if UNITY_EDITOR
        // 위에서 볼 때 점령지 상자와 지킬 자리(보는 쪽 선)가 보이게
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.9f);
            Matrix4x4 old = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, m_Size);
            Gizmos.matrix = old;
            foreach (Transform spot in m_Spots)
            {
                if (spot == null)
                    continue;
                Gizmos.DrawSphere(spot.position + Vector3.up * 0.2f, 0.25f);
                Gizmos.DrawLine(spot.position + Vector3.up * 0.2f, spot.position + Vector3.up * 0.2f + spot.forward * 1.5f);
            }
            UnityEditor.Handles.Label(transform.position + Vector3.up * (m_Size.y * 0.5f + 1f), m_DisplayName);
        }
#endif
    }
}
