using UnityEngine;

namespace MilTrainSimVR.A.Rifle
{
    // 총의 탄창 삽입구 — A 총(K2C1Rifle)이 탄창에서 읽고 시키는 것만 모은 약속 (11/17 총기 통합 5단계).
    // 탄창은 C 것(C MagazineWell · C_Magazine)을 그대로 쓴다 — 탄창 한 벌. A 총 코드(이 asmdef)는 C 를 볼 수 없어서
    // C 연결 코드(Assets/A/Scripts/CBridge/CMagazineFeed)가 C 삽입구를 이 약속에 맞춘다. 같은 총에 붙어 있어야 한다.
    public interface IMagazineFeed
    {
        bool HasMagazine { get; }
        int Rounds { get; }                 // 꽂힌 탄창에 남은 탄

        event System.Action Inserted;
        event System.Action Removed;

        bool TakeRound();                   // 약실로 한 발 — 꽂힌 탄창에 탄이 있으면 하나 빼고 true
        void Drop();                        // 탄창멈치 — 꽂힌 탄창이 아래로 떨어진다
        void Clear();                       // 처음 상태로 — 꽂힌 탄창을 탄창 오브젝트 없이 없앤다 (알림 없음)

        // 사로 탄창을 새로 놓는다 (전에 있던 탄창은 없앤다)
        void ResetMagazines(Vector3 position, Quaternion rotation, int rounds);

        // 시험용 (손 없이) — 놓인 탄창 하나를 꽂는다 · 꽂힌 탄창을 뺀다
        void TestInsert();
        void TestRemove();
    }
}
