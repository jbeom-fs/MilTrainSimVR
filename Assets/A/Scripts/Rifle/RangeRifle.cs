using UnityEngine;

namespace MilTrainSimVR.A.Rifle
{
    // 사로(A_Range)가 총에서 읽고 시키는 것만 모은 약속 (11/17). 사로 절차·안전 감시·약실 확인·채점은 이것만 본다.
    //   · A 방식 = K2C1Rifle (A 가 복사해 고친 총, 조준 확대 · 장전 손잡이 · 공이치기)
    //   · C 방식 = CRangeRifle (Assets/A/Scripts/CBridge — C 의 TempRifle 을 감싸 이 약속에 맞춘다)
    // 어느 쪽을 쓸지는 허브에서 고른 RifleStyle 로 RifleStyleSwitch 가 정한다.
    public abstract class RangeRifle : MonoBehaviour
    {
        public enum FireMode { Safe, Semi, Burst, Auto }   // 안전 · 단발 · 점사 · 연발

        public abstract event System.Action Fired;                  // 실탄 한 발
        public abstract event System.Action DryFired;               // 빈 격발 — 공이치기만 떨어짐 (소리 없이 진동만, 11/17)
        public abstract event System.Action TriggerBlocked;         // 안전에서 방아쇠를 당김 — 당겨지지 않는다
        public abstract event System.Action HammerDown;             // 공이치기가 떨어진 채 방아쇠를 당김 — 아무 일 없다
        public abstract event System.Action<FireMode> FireModeChanged;
        public abstract event System.Action ChargingHandlePulled;   // 장전 손잡이를 끝까지 당김
        public abstract event System.Action ChargingHandleReturned; // 장전 손잡이가 앞으로 돌아옴
        public abstract event System.Action MagazineInserted;
        public abstract event System.Action MagazineRemoved;

        public abstract FireMode Mode { get; }
        public abstract bool HasMagazine { get; }
        public abstract int MagazineRounds { get; }   // 꽂힌 탄창에 남은 탄 (약실 탄은 빼고)
        public abstract bool Chambered { get; }
        public abstract bool HammerCocked { get; }
        public abstract bool IsHeld { get; }
        public abstract Vector3 MuzzleForward { get; }

        // 장전 손잡이가 뒤로 간 거리(m)와 끝까지의 거리 — 약실 확인(손잡이를 끝까지 당긴 채 보기)이 쓴다. 손잡이가 없으면 둘 다 0
        public abstract float ChargingOffset { get; }
        public abstract float ChargingStroke { get; }

        public abstract void PullTrigger();
        public abstract void ReleaseTrigger();
        public abstract void SetFireMode(FireMode mode);

        // 처음 상태로 — 손에서 놓게 하고 탄창 없음 · 약실 빔 · 공이치기 떨어짐 · 조정간 안전
        public abstract void ResetState();

        // 사로 탄창을 새로 놓는다 (전에 있던 탄창은 없앤다). 이 총에 맞는 탄창을 놓았으면 true
        public virtual bool ResetMagazines(Vector3 position, Quaternion rotation, int rounds) => false;

        // 시험용 (손 없이 단계 넘기기) — 못 하는 총은 아무것도 안 한다
        public virtual void TestInsertMagazine() { }
        public virtual void TestRemoveMagazine() { }
        public virtual void TestCycleChargingHandle() { }
    }
}
