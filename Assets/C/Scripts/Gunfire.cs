using System.Collections.Generic;
using UnityEngine;

namespace MilTrainSimVR.UrbanCombat
{
    // 총소리·탄착을 모두에게 알리는 곳 (10/26) — 총이 여럿(플레이어 K2C1 + 적들)이 되어서 효과와 적이 총 하나하나를 찾아 듣지 않게 한곳에 모은다.
    //   · ShotFired: 어디서 누가 쐈는지 — 적이 총소리를 듣고 그쪽을 본다, 경기 기록이 플레이어 사격 수를 센다
    //   · SurfaceHit: 총알이 맞은 자리 — 불꽃·탄흔(ImpactEffects)
    //   · BodyHit: 누가 쏜 총알이 몸(부위 판정)에 맞았는지 — 맞히기 바로 전에(몸 상태가 바뀌기 전에) 알린다.
    //     몸 상태(CombatantHealth)는 누가 쐈는지 모르니 경기 기록(MatchRecord)이 이걸로 명중·처치한 사람을 안다 (10/27)
    // 적의 총알 길(Trace)도 여기 둔다. 플레이어 몸 판정 상자는 트리거라서(내 총에 안 맞게) 트리거까지 보는 광선이어야 맞는다
    public static class Gunfire
    {
        public static event System.Action<Vector3, Component> ShotFired;
        public static event System.Action<RaycastHit> SurfaceHit;
        public static event System.Action<Component, HitZone> BodyHit;
        // 총알이 날아간 길 (총구 → 맞은 곳 또는 사거리 끝, 직선) — 적이 자기 옆을 스쳐 간 총알을 알아챈다 (11/16)
        public static event System.Action<Vector3, Vector3, Component> BulletPassed;

        public static void RaiseShotFired(Vector3 origin, Component shooter) => ShotFired?.Invoke(origin, shooter);
        public static void RaiseSurfaceHit(RaycastHit hit) => SurfaceHit?.Invoke(hit);
        public static void RaiseBodyHit(Component shooter, HitZone zone) => BodyHit?.Invoke(shooter, zone);
        public static void RaiseBulletPassed(Vector3 from, Vector3 to, Component shooter) => BulletPassed?.Invoke(from, to, shooter);

        // 총알·시선 광선이 보는 층 — 'Ignore Raycast'(맵 끝 보이지 않는 벽) 말고 전부. 벽은 몸만 막고 총알은 지나가게 (10/27)
        public const int ShotMask = Physics.DefaultRaycastLayers;

        // 칸이 모자라면 PhysX 가 아무 것이나 채워 가장 가까운 것이 빠질 수 있다. 소품이 모델 판정(선반 조각마다 하나)이 된 뒤
        // 200 m 광선 30,000개를 재 보니 가장 많을 때 52개 (11/19) — 넉넉히 128
        static readonly RaycastHit[] s_Hits = new RaycastHit[128];
        static readonly IComparer<RaycastHit> s_ByDistance = Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

        // 총알 한 발의 길 — 가까운 것부터 보면서:
        //   · ignore(쏜 사람) 아래 콜라이더는 건너뛴다 (자기 팔·몸)
        //   · 부위 판정(HitZone)이면 거기 맞는다 — 트리거여도(플레이어 몸)
        //   · 그 밖의 트리거와 캐릭터 컨트롤러(플레이어 리그의 몸통 캡슐)는 지나간다
        //   · 나머지(벽·바닥·물건)에서 막힌다
        //   · 맵 끝의 보이지 않는 벽('Ignore Raycast' 층)은 보지 않는다 — ShotMask
        public static bool Trace(Vector3 origin, Vector3 direction, float range, Transform ignore, out RaycastHit hit, out HitZone zone)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, s_Hits, range, ShotMask, QueryTriggerInteraction.Collide);
            System.Array.Sort(s_Hits, 0, count, s_ByDistance);
            for (int i = 0; i < count; i++)
            {
                Collider c = s_Hits[i].collider;
                if (ignore != null && c.transform.IsChildOf(ignore))
                    continue;
                zone = c.GetComponent<HitZone>();
                if (zone == null && (c.isTrigger || c is CharacterController))
                    continue;
                hit = s_Hits[i];
                return true;
            }
            hit = default;
            zone = null;
            return false;
        }
    }
}
