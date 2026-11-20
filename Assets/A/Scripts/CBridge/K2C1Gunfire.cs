using MilTrainSimVR.A.Rifle;
using MilTrainSimVR.UrbanCombat;
using UnityEngine;

namespace MilTrainSimVR.A.CBridge
{
    // A 총(K2C1Rifle)이 쏜 한 발을 C 시가지에 알린다 (11/17 총기 통합 4단계 — Docs/A/1117_총기통합_5_총소리알림.md).
    // C 총(TempRifle.Shoot)이 하는 것과 같은 순서 · 같은 함수:
    //   맞은 것이 C 표적(C IHittable)이면 — 사람 몸 부위(HitZone)면 Gunfire.RaiseBodyHit 먼저 → OnHit
    //   → 맞은 곳 Gunfire.RaiseSurfaceHit (불꽃 · 탄흔) → 총소리 Gunfire.RaiseShotFired (적이 듣고 이쪽을 봄, CombatAudio 사격음)
    //   → 총알 길 Gunfire.RaiseBulletPassed (총구가 벽 속이면 안 냄 — 스친 적이 대응사격)
    // A 표적(A IHittable — 사로 표적지)은 A 총이 이미 명중 처리했으므로 C OnHit 을 다시 부르지 않는다
    //   (표적지의 CHitRelay 가 C → A 로 넘겨 두 번 맞게 된다).
    // 이 폴더는 asmdef 가 없어(Assembly-CSharp) C 코드를 볼 수 있다. A 총에 붙인다 (메뉴 A/만들기/사격용 K2C1 C 연동 (손 · 탄창 · 총소리)).
    // A 씬엔 Gunfire 를 듣는 것이 없어서 아무 일도 없다 (사격장 실전 사격의 C 리그는 C 총을 쓴다).
    [RequireComponent(typeof(K2C1Rifle))]
    public class K2C1Gunfire : MonoBehaviour
    {
        K2C1Rifle m_Rifle;

        void Awake() => m_Rifle = GetComponent<K2C1Rifle>();
        void OnEnable() => m_Rifle.ShotReported += OnShot;
        void OnDisable() => m_Rifle.ShotReported -= OnShot;

        void OnShot(K2C1Rifle.ShotReport shot)
        {
            if (shot.Struck)
            {
                if (!shot.HandledByA)
                {
                    var target = shot.Hit.collider.GetComponentInParent<MilTrainSimVR.UrbanCombat.IHittable>();
                    if (target != null)
                    {
                        // 사람 몸이면 누가 맞혔는지 먼저 알린다 — 경기 기록(명중 · 처치)
                        if (target is HitZone zone)
                            Gunfire.RaiseBodyHit(m_Rifle, zone);
                        target.OnHit(shot.Hit, shot.Direction);
                    }
                }
                Gunfire.RaiseSurfaceHit(shot.Hit);
            }
            Gunfire.RaiseShotFired(shot.Origin, m_Rifle);
            if (!shot.Blocked)
                Gunfire.RaiseBulletPassed(shot.Origin, shot.End, m_Rifle);
        }
    }
}
