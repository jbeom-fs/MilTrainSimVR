using UnityEngine;

namespace MilTrainSimVR.A.Rifle
{
    // 총이 쏘기 직전에 탄이 나갈 직선을 묻는 약속 (조준 확대 모드 설계 4절, C 제안 3절).
    // 줄 직선이 없으면 false — 총은 원래대로 총구 앞 방향으로 쏜다.
    public interface IShotLine
    {
        bool TryGetShotLine(out Vector3 origin, out Vector3 direction);
    }
}
