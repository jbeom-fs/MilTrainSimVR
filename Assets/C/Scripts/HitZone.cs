using UnityEngine;

namespace MilTrainSimVR.UrbanCombat
{
    // 몸의 한 부위(머리·심장·가슴·배·팔·다리) 판정 상자. 총알(광선)이 이 콜라이더에 맞으면 부위를 몸 주인(CombatantHealth)에게 알린다.
    // 부위마다 결과가 다르다 — 머리·심장 전사, 가슴·배 중상, 팔·다리 부상 (C 범위 문서 4번, 10/26).
    // 심장처럼 다른 부위 '안쪽'에 있는 부위는 따로 상자를 두면 바깥 상자에 먼저 맞아 영영 안 맞는다 →
    // 바깥 부위(가슴)에 안쪽 구(m_Inner)를 넣어 두고, 총알이 가슴을 뚫고 지나가는 길이 그 구를 지나면 안쪽 부위(심장)에 맞은 것으로 본다
    [RequireComponent(typeof(Collider))]
    public class HitZone : MonoBehaviour, IHittable
    {
        public enum Part { Head, Heart, Chest, Abdomen, Arm, Leg }   // 머리 · 심장 · 가슴 · 배 · 팔 · 다리

        [SerializeField] Part m_Part = Part.Chest;

        [Tooltip("이 부위 안쪽에 있는 더 중요한 부위의 가운데 (가슴 안 심장) — 비우면 없음")]
        [SerializeField] Transform m_Inner;

        [Tooltip("안쪽 부위의 반지름(m)")]
        [SerializeField] float m_InnerRadius = 0.06f;

        [SerializeField] Part m_InnerPart = Part.Heart;

        [Tooltip("맞은 점에서 총알 방향으로 몸 안을 이만큼(m) 지나가며 안쪽 부위를 본다")]
        [SerializeField] float m_Depth = 0.5f;

        CombatantHealth m_Owner;

        public Part BodyPart => m_Part;

        public CombatantHealth Owner => m_Owner != null ? m_Owner : m_Owner = GetComponentInParent<CombatantHealth>();

        // 코드로 몸을 만들 때(적 더미 만들기·플레이어 몸 판정) 부위와 안쪽 부위를 정한다
        public void Setup(Part part, Transform inner = null, float innerRadius = 0.06f, Part innerPart = Part.Heart)
        {
            m_Part = part;
            m_Inner = inner;
            m_InnerRadius = innerRadius;
            m_InnerPart = innerPart;
        }

        public void OnHit(RaycastHit hit, Vector3 direction)
        {
            if (Owner != null)
                Owner.TakeHit(ResolvePart(hit.point, direction), hit.point);
        }

        // 맞은 점에서 총알이 날아간 방향으로 몸 깊이만큼 그은 선분이 안쪽 구에 닿으면 안쪽 부위
        public Part ResolvePart(Vector3 point, Vector3 direction)
        {
            if (m_Inner == null || direction.sqrMagnitude < 1e-6f)
                return m_Part;
            Vector3 d = direction.normalized;
            Vector3 center = m_Inner.position;
            // 선분 위에서 구 가운데와 가장 가까운 점
            float t = Mathf.Clamp(Vector3.Dot(center - point, d), 0f, m_Depth);
            Vector3 closest = point + d * t;
            return (closest - center).sqrMagnitude <= m_InnerRadius * m_InnerRadius ? m_InnerPart : m_Part;
        }
    }
}
