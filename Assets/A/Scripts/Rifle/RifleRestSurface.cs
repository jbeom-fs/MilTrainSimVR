using UnityEngine;

namespace MilTrainSimVR.A.Rifle
{
    // 총을 올려놓거나 걸치는 면 (사로 책상 등, 11/10) — 총구 막힘 판정(K2C1Rifle.BarrelBlocked)에서 뺀다.
    // VR 에는 실제 책상이 없어 총을 책상에 걸치면 손잡이 · 총 몸통이 상판 아래로 내려간다. 그러면 머리 → 손잡이 → 총구 사이에
    // 상판이 끼어 "총구가 벽 속"으로 판정돼 탄이 나가지 않았다 (헤드셋에서 사로 1 · 2조 모두 Table_Top 에 막힘).
    // 날아가는 탄은 그대로 이 면에 맞는다 — 책상 아래로 쏘면 책상에 맞는다.
    public class RifleRestSurface : MonoBehaviour
    {
    }
}
