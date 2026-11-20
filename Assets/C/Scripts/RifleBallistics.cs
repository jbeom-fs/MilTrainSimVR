using System.Collections.Generic;
using UnityEngine;

namespace MilTrainSimVR.UrbanCombat
{
    // 영점 맞춘 총열 + 중력 탄도 (10/29) — "탄이 어떻게 나가는가"만 맡는다. 어떻게 조준하는지(가늠자로 직접 / A 의 확대 창)와는 상관없다.
    //   · 탄은 총구에서 총열 방향으로 나간다. 조준선(가늠자 구멍 → 가늠쇠 끝)은 총열보다 위에 있고,
    //     실제 총처럼 영점 거리(25m)에서 탄이 조준선을 지나도록 총열 방향을 맞춰 둔다 = 영점사격으로 가늠자를 맞춘 총.
    //     K2C1 모델은 조준선이 모델 총열보다 1.06° 아래로 기울어 있다(A 측정) — 모델의 총구 앞 방향 그대로 쏘면 25m 에서 40cm 위에 맞는다.
    //   · 그래서 25m 안쪽은 조준점보다 조금 낮게(5m 에서 약 4cm), 그 너머는 조금 높게(50m 약 5cm, 100m 약 12cm) 맞는다 — 실제 총과 같다.
    //   · 총알을 날리지 않고 쏜 순간 판정한다. 대신 중력으로 휜 길을 짧은 직선 여러 개로 나눠 광선으로 본다 (25m 까지 0.03초라 차이를 못 느낀다).
    //     공기 저항은 뺐다 — 100m 안에선 0.4cm 차이다 (계산: Advice/1029_실제조준)
    // 나중에 A 와 같이 쓸 수 있게 UnityEngine 말고는 아무것도 부르지 않는다. 광선(총 자신·든 사람 건너뛰기 등)은 쏘는 쪽이 넘겨준다
    public class RifleBallistics : MonoBehaviour
    {
        [Tooltip("가늠자 구멍 가운데 — K2C1 의 Anchor_RearSight")]
        [SerializeField] Transform m_RearSight;

        [Tooltip("가늠쇠 끝 — K2C1 의 Anchor_FrontSight")]
        [SerializeField] Transform m_FrontSight;

        [Tooltip("총구 — 탄이 나가는 곳 (K2C1 의 Anchor_Muzzle)")]
        [SerializeField] Transform m_Muzzle;

        [Tooltip("영점 거리(m) — 가늠자에서 이만큼 앞에서 탄이 조준선을 지난다. 예비군 영점사격은 25m")]
        [SerializeField] float m_ZeroDistance = 25f;

        [Tooltip("탄속(m/s) — K2 5.56mm 약 920")]
        [SerializeField] float m_MuzzleVelocity = 920f;

        [Tooltip("휜 탄도를 이 길이(m)씩 끊어 직선 광선으로 본다 — 10m 면 직선과 휜 길의 차이가 0.2mm 안")]
        [SerializeField] float m_SegmentLength = 10f;

        // 총 기준(이 오브젝트 기준) 영점 맞춘 총열 방향 — 실제 총의 총열처럼 총에 고정된 방향. 처음 한 번 계산한다
        Vector3 m_LocalBore = Vector3.forward;
        bool m_Computed;
        bool m_HasSights;

        // 광선 하나 — 쏘는 쪽이 넘겨준다 (총 자신·든 사람 콜라이더 건너뛰기 등)
        public delegate bool SegmentCast(Vector3 from, Vector3 direction, float distance, out RaycastHit hit);

        public float ZeroDistance => m_ZeroDistance;
        public float MuzzleVelocity => m_MuzzleVelocity;

        void Awake() => ComputeBore();

        // 총이 쏘기 직전에 묻는다 (A 의 IShotLine 과 같은 모양) — 시작점 = 총구, 방향 = 영점 맞춘 총열.
        // 가늠자·가늠쇠가 없으면 false — 총은 원래대로 총구 앞 방향으로 쏜다
        public bool TryGetShotLine(out Vector3 origin, out Vector3 direction)
        {
            if (!m_Computed)
                ComputeBore();
            origin = m_Muzzle != null ? m_Muzzle.position : transform.position;
            direction = transform.TransformDirection(m_LocalBore);
            return m_HasSights;
        }

        // 총구에서 탄을 날려 처음 맞는 곳을 찾는다. path 에는 지난 점들(첫 점 = 시작점, 끝 점 = 맞은 곳 또는 사거리 끝)을 넣는다 — 탄도선 그리기용.
        // hitDirection = 맞을 때 탄이 날던 방향 (부위 판정이 심장까지 총알 길을 볼 때 쓴다)
        public bool Fly(Vector3 origin, Vector3 direction, float range, SegmentCast cast, out RaycastHit hit, out Vector3 hitDirection, List<Vector3> path)
        {
            path?.Clear();
            path?.Add(origin);
            Vector3 velocity = direction.normalized * m_MuzzleVelocity;
            Vector3 gravity = Physics.gravity;
            float step = m_SegmentLength / m_MuzzleVelocity;   // 한 마디를 나는 시간
            Vector3 from = origin;
            float travelled = 0f;
            hitDirection = direction.normalized;
            while (travelled < range)
            {
                // 한 마디 동안 탄이 가는 곳 (등가속도) — 그 사이는 직선으로 본다
                Vector3 to = from + velocity * step + 0.5f * step * step * gravity;
                velocity += gravity * step;
                Vector3 d = to - from;
                float length = d.magnitude;
                Vector3 dir = d / length;
                length = Mathf.Min(length, range - travelled);
                if (cast(from, dir, length, out hit))
                {
                    hitDirection = dir;
                    path?.Add(hit.point);
                    return true;
                }
                from += dir * length;
                travelled += length;
                path?.Add(from);
            }
            hit = default;
            return false;
        }

        // 영점 맞추기 — 총을 똑바로 세워(조준선 수평) 영점 거리의 조준점을 겨눴을 때 탄이 그 점을 지나는 총열 방향을 찾는다.
        // 탄이 거기까지 가는 동안 떨어지는 만큼 조금 위를 겨누고 다시 재기를 몇 번 되풀이한다 (25m 에서 0.4cm 쯤이라 몇 번이면 된다).
        // 영점사격처럼 한 번 맞춰 두면 총에 붙은 방향이다 — 총을 기울이면 실제 총처럼 영점이 어긋난다
        void ComputeBore()
        {
            m_Computed = true;
            m_HasSights = false;
            if (m_Muzzle == null)
                return;
            m_LocalBore = transform.InverseTransformDirection(m_Muzzle.forward);   // 조준선을 모르면 모델 총구 앞 방향 그대로
            if (m_RearSight == null || m_FrontSight == null)
                return;

            Vector3 rear = transform.InverseTransformPoint(m_RearSight.position);
            Vector3 front = transform.InverseTransformPoint(m_FrontSight.position);
            Vector3 muzzle = transform.InverseTransformPoint(m_Muzzle.position);
            Vector3 sight = (front - rear).normalized;
            // 똑바로 세운 총의 '위' — 총의 위(+Y)에서 조준선 방향 성분을 뺀 것. 중력은 그 반대로 당긴다
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, sight).normalized;
            float g = Physics.gravity.magnitude;

            Vector3 aim = rear + sight * m_ZeroDistance;   // 영점 거리의 조준점
            Vector3 point = aim;
            for (int i = 0; i < 4; i++)
            {
                float t = (point - muzzle).magnitude / m_MuzzleVelocity;
                point = aim + up * (0.5f * g * t * t);
            }
            m_LocalBore = (point - muzzle).normalized;
            m_HasSights = true;
        }
    }
}
