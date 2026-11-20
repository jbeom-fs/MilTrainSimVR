using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace MilTrainSimVR.UrbanCombat
{
    // HeldItemGroundFilter 의 짝 — GravityProvider 가 땅을 본 바로 다음(XRBodyTransformer 가 몸을 옮기기 전)에
    // 손에 든 물건의 레이어를 되돌린다. 그래서 레이어가 바뀌어 있는 동안 도는 건 GravityProvider 의 땅 판정 하나뿐이다.
    // 실행 순서를 따로 주려고 클래스를 나눴다. HeldItemGroundFilter 가 Awake 에서 붙인다 (프리팹에 직접 붙일 필요 없음)
    [DefaultExecutionOrder(XRInteractionUpdateOrder.k_GravityProvider + 1)]
    public class HeldItemGroundFilterRestore : MonoBehaviour
    {
        public HeldItemGroundFilter Filter { get; set; }

        void Update()
        {
            if (Filter != null)
                Filter.Restore();
        }
    }
}
