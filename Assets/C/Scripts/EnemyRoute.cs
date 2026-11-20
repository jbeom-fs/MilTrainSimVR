using UnityEngine;

namespace MilTrainSimVR.UrbanCombat
{
    // 적 한 명의 지킬 자리 또는 순찰 길 (10/27). 자식 오브젝트 하나하나가 점이다 — 위치 + 그 점에서 볼 방향(자식의 앞 = 파란 축).
    //   · 점 하나 = 자리 지킴: 그 자리에 서서 그쪽을 본다
    //   · 점 둘 이상 = 순찰: 차례로 걷고 점마다 잠깐 서서 그쪽을 본다. 끝에서 되돌아온다(왕복) — '고리'를 켜면 처음으로 이어 돈다
    // 씬에서 점(자식)을 옮기고 돌려서 고친다. 적(EnemyMover)이 이 경로를 가리킨다
    //   · 이름이 '지나감'으로 시작하는 점은 서지 않고 지나간다 (11/17 — 에스컬레이터 앞처럼 길만 잡아 주는 점)
    public class EnemyRoute : MonoBehaviour
    {
        public const string k_PassPrefix = "지나감";

        [Tooltip("켜면 마지막 점 다음에 첫 점으로 (고리). 끄면 왔던 길로 되돌아온다 (왕복)")]
        [SerializeField] bool m_Loop;

        [Tooltip("순찰할 때 점마다 서 있는 시간(초) — 이 사이에서 무작위")]
        [SerializeField] Vector2 m_PauseSeconds = new Vector2(2f, 4f);

        public int Count => transform.childCount;
        public bool Loop => m_Loop;
        public Transform Point(int index) => transform.GetChild(index);
        public float RandomPause() => Random.Range(m_PauseSeconds.x, m_PauseSeconds.y);
        public bool IsPass(int index) => Point(index).name.StartsWith(k_PassPrefix);

        // 다음에 갈 점 — 왕복이면 끝에서 방향을 바꾼다 (step 은 +1 또는 -1, 바뀌면 같이 바꾼다)
        public int Next(int index, ref int step)
        {
            if (Count < 2)
                return 0;
            if (m_Loop)
                return (index + 1) % Count;
            if (index + step < 0 || index + step >= Count)
                step = -step;
            return index + step;
        }

#if UNITY_EDITOR
        // 위에서 볼 때 점·길·보는 방향이 보이게 — 자리 지킴은 큰 점, 순찰은 점을 잇는 선
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.4f, 1f, 0.9f);
            for (int i = 0; i < Count; i++)
            {
                Vector3 p = Point(i).position;
                Gizmos.DrawSphere(p + Vector3.up * 0.2f, Count == 1 ? 0.45f : IsPass(i) ? 0.15f : 0.3f);
                Vector3 look = Vector3.ProjectOnPlane(Point(i).forward, Vector3.up).normalized;
                Gizmos.DrawLine(p + Vector3.up * 0.2f, p + Vector3.up * 0.2f + look * 2f);
                if (i + 1 < Count)
                    Gizmos.DrawLine(p + Vector3.up * 0.2f, Point(i + 1).position + Vector3.up * 0.2f);
                else if (m_Loop && Count > 2)
                    Gizmos.DrawLine(p + Vector3.up * 0.2f, Point(0).position + Vector3.up * 0.2f);
            }
            if (Count > 0)
                UnityEditor.Handles.Label(Point(0).position + Vector3.up * 2.2f, name);
        }
#endif
    }
}
