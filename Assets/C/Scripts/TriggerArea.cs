using UnityEngine;

namespace MilTrainSimVR.UrbanCombat
{
    // 플레이어가 들어오면 무슨 일이 생기는 곳 (11/17, EnemyDirector — 예: 백화점 2층 · 3층에 들어오면 적이 추적).
    // 점령지(CaptureZone)처럼 콜라이더 · 트리거를 쓰지 않고 위치로 본다 — 이 오브젝트의 위치 · 회전 기준 상자 (크기 Scale 은 1 로 둔다).
    // 씬에서 옮기고 m_Size 를 바꿔 맞춘다
    public class TriggerArea : MonoBehaviour
    {
        [Tooltip("화면 · 로그에 쓰는 이름")]
        [SerializeField] string m_DisplayName = "구역";

        [Tooltip("상자 크기(m) — 이 오브젝트의 위치 · 회전 기준")]
        [SerializeField] Vector3 m_Size = new Vector3(10f, 3f, 10f);

        public string DisplayName => m_DisplayName;

        public bool Contains(Vector3 worldPoint)
        {
            Vector3 p = transform.InverseTransformPoint(worldPoint);
            return Mathf.Abs(p.x) <= m_Size.x * 0.5f && Mathf.Abs(p.y) <= m_Size.y * 0.5f && Mathf.Abs(p.z) <= m_Size.z * 0.5f;
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.9f, 0.2f, 0.9f, 0.9f);
            Matrix4x4 old = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, m_Size);
            Gizmos.matrix = old;
            UnityEditor.Handles.Label(transform.position + Vector3.up * (m_Size.y * 0.5f + 0.5f), m_DisplayName);
        }
#endif
    }
}
