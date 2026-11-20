using UnityEngine;

namespace MilTrainSimVR.A.Common
{
    // 머리(카메라) 쪽으로 수평으로만 돈다 — 허브 포탈 이름처럼 어느 쪽에서 봐도 글씨가 바로 읽혀야 하는 것 (11/09).
    // 앞(+Z)이 머리를 본다. UI 글씨는 뒷면도 그려져서 판 두 장을 등지게 붙이면 거울 글씨가 겹친다 → 한 장을 돌린다.
    public class FaceHead : MonoBehaviour
    {
        [Tooltip("비우면 Main Camera")]
        [SerializeField] Transform m_Head;

        void LateUpdate()
        {
            if (m_Head == null)
            {
                if (Camera.main == null)
                    return;
                m_Head = Camera.main.transform;
            }
            Vector3 toHead = m_Head.position - transform.position;
            toHead.y = 0f;
            if (toHead.sqrMagnitude > 1e-4f)
                transform.rotation = Quaternion.LookRotation(toHead, Vector3.up);
        }
    }
}
