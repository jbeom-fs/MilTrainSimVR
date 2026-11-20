using UnityEngine;
using UnityEngine.AI;

namespace MilTrainSimVR.UrbanCombat
{
    // 회색 박스 맵의 적 길찾기 지도(내비메시)를 켠다 (10/27). 맵 생성기가 맵을 만들 때 같이 구워서 여기에 넣어 둔다.
    // NavMeshSurface 를 안 쓰는 까닭: 문 폭 1m 를 지나가려면 적 반지름이 0.5m(기본 Humanoid)보다 작아야 하는데,
    // 그러려면 프로젝트 공용 설정(Navigation 의 Agent 종류)을 바꿔야 한다 → 생성기가 C 적에 맞는 값으로 직접 굽는다.
    // 적(NavMeshAgent)보다 먼저 켜져야 해서 실행 순서를 앞으로 당긴다. 에디터에서도 켜 둬서 Scene 창에서 길을 볼 수 있다
    [ExecuteAlways]
    [DefaultExecutionOrder(-102)]
    public class GreyboxNavMesh : MonoBehaviour
    {
        [Tooltip("맵 생성기가 구운 길찾기 지도 — 직접 넣지 않는다")]
        [SerializeField] NavMeshData m_Data;

        NavMeshDataInstance m_Instance;

        public NavMeshData Data
        {
            get => m_Data;
            set
            {
                m_Data = value;
                if (isActiveAndEnabled)
                {
                    Remove();
                    Add();
                }
            }
        }

        void OnEnable() => Add();

        void OnDisable() => Remove();

        void Add()
        {
            if (m_Data != null && !m_Instance.valid)
                m_Instance = NavMesh.AddNavMeshData(m_Data);
        }

        void Remove()
        {
            m_Instance.Remove();
            m_Instance = new NavMeshDataInstance();
        }
    }
}
