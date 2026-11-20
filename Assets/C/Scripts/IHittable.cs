using UnityEngine;

namespace MilTrainSimVR.UrbanCombat
{
    // 총에 맞을 수 있는 것(연습 표적·적 병사·드론)이 공통으로 지키는 약속.
    // 총은 "맞은 물체가 IHittable 이면 OnHit 을 부른다"만 알면 되고,
    // 맞은 뒤 어떻게 반응할지는 각 물체가 스스로 정한다 — 그래서 적·드론을 나중에 추가해도 총 코드는 그대로다.
    // direction 은 총알이 날아간 방향(길이 1). 가슴을 뚫고 심장까지 갔는지처럼 '몸 안 어디를 지나갔는지'를 볼 때 쓴다 (10/26)
    public interface IHittable
    {
        void OnHit(RaycastHit hit, Vector3 direction);
    }
}
