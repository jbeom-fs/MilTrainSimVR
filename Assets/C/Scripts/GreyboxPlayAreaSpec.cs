using System.Collections.Generic;
using UnityEngine;

namespace MilTrainSimVR.UrbanCombat
{
    // 회색 박스 맵 '배치'의 놀 수 있는 범위 — 맵 생성기가 이 테두리에 보이지 않는 벽을 세운다 (10/26, S 구역을 빼며).
    // 자식(꼭짓점)이 셋 이상이면 자식 순서대로 이은 다각형이 테두리다 (10/27 — 바깥 도로 연석선이 비스듬해서 사각형으로는 못 따라간다).
    // 자식이 없으면 예전처럼 이 상자의 위치·가로·세로(회전은 안 봄). 놀 곳 자체가 없으면 생성기가 배치 전체보다 20m 넓게 막는다.
    // 범위 밖(바깥 도로·맵 끝 배경 건물)은 보이기만 하고 갈 수 없다
    public class GreyboxPlayAreaSpec : MonoBehaviour
    {
        // 테두리 꼭짓점 (맵 x·z, y = 0) — 둘레를 한 바퀴 도는 순서
        public List<Vector3> Corners()
        {
            var corners = new List<Vector3>();
            if (transform.childCount >= 3)
            {
                foreach (Transform child in transform)
                    corners.Add(new Vector3(child.position.x, 0f, child.position.z));
                return corners;
            }
            Vector3 c = transform.position;
            float w = Mathf.Abs(transform.lossyScale.x) * 0.5f, d = Mathf.Abs(transform.lossyScale.z) * 0.5f;
            corners.Add(new Vector3(c.x - w, 0f, c.z - d));
            corners.Add(new Vector3(c.x + w, 0f, c.z - d));
            corners.Add(new Vector3(c.x + w, 0f, c.z + d));
            corners.Add(new Vector3(c.x - w, 0f, c.z + d));
            return corners;
        }

#if UNITY_EDITOR
        // 위에서 내려다볼 때 테두리가 빨간 선으로 보이게 — 속을 칠하면 다른 배치 상자를 가린다
        void OnDrawGizmos()
        {
            List<Vector3> corners = Corners();
            Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.9f);
            for (int i = 0; i < corners.Count; i++)
            {
                Vector3 a = corners[i], b = corners[(i + 1) % corners.Count];
                Gizmos.DrawLine(a + Vector3.up * 0.3f, b + Vector3.up * 0.3f);
                Gizmos.DrawLine(a + Vector3.up * 10f, b + Vector3.up * 10f);
                Gizmos.DrawLine(a, a + Vector3.up * 10f);
            }
            Vector3 south = corners[0];
            foreach (Vector3 c in corners)
            {
                if (c.z < south.z)
                    south = c;
            }
            UnityEditor.Handles.Label(south + new Vector3(4f, 3f, 4f), "놀 곳 — 보이지 않는 벽");
        }
#endif
    }
}
