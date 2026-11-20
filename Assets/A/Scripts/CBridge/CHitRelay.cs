using UnityEngine;

namespace MilTrainSimVR.A.CBridge
{
    // C 총(TempRifle)은 C 의 IHittable 만 찾는다 → 맞은 것을 같은 오브젝트의 A IHittable(예: 사로 표적지 ZeroTarget)에 그대로 넘긴다 (11/17).
    // C 방식으로 사로를 쏠 때 표적지에 탄착이 남게 하는 다리. 표적지 오브젝트(충돌체의 부모 쪽)에 붙인다.
    public class CHitRelay : MonoBehaviour, MilTrainSimVR.UrbanCombat.IHittable
    {
        MilTrainSimVR.A.Rifle.IHittable[] m_Targets;

        void Awake() => m_Targets = GetComponents<MilTrainSimVR.A.Rifle.IHittable>();

        public void OnHit(RaycastHit hit, Vector3 direction)
        {
            foreach (var target in m_Targets)
                target.OnHit(hit, direction);
        }
    }
}
