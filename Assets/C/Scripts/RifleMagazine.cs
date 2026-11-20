using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MilTrainSimVR.UrbanCombat
{
    // 손에 들었거나 바닥에 떨어진 탄창 하나 (10/28) — 남은 탄 수를 가진다.
    // 총에 꽂으면 이 오브젝트는 없어지고 총의 삽입구(MagazineWell)가 탄 수를 넘겨받는다. 다시 빼면 그 탄 수로 새로 만든다.
    // 떨어뜨린 탄창은 보통 물건처럼 다시 주울 수 있다 (탄 수 그대로).
    // 11/12: 윗면에 남은 탄이 보인다 (모양은 MagazineRoundsBuilder 가 만든다) — 탄은 두 줄로 엇갈려 쌓여서 한 발 줄 때마다
    // 맨 위 탄이 좌우로 바뀐다. 장전 손잡이를 당긴 뒤 탄창을 빼 보면 좌상탄 ↔ 우상탄이 바뀌어 있어 약실에 탄이 들어갔는지 알 수 있다.
    //   · 두 발 이상: 맨 위 탄 + 반대쪽에 반쯤 잠긴 그 아래 탄      · 한 발: 그 탄 + 반대쪽에 받침판의 턱
    //   · 빈 탄창: 받침판만 끝까지 올라와 있다
    [RequireComponent(typeof(XRGrabInteractable))]
    public class RifleMagazine : MonoBehaviour
    {
        [Tooltip("탄창 윗면(급탄구) 가운데 — 총의 삽입구에 맞추는 점")]
        [SerializeField] Transform m_FeedPoint;

        [Tooltip("가득 찼을 때 탄 수")]
        [SerializeField] int m_Capacity = 30;

        [Header("윗면에 보이는 탄 — 자리는 MagazineRoundsBuilder 가 넣는다")]
        [Tooltip("맨 위 탄 — 한 발이라도 남았으면 보인다")]
        [SerializeField] Transform m_TopRound;

        [Tooltip("그 아래 탄 — 맨 위 탄의 반대쪽에 반쯤 잠겨 보인다 (두 발부터)")]
        [SerializeField] Transform m_NextRound;

        [Tooltip("받침판 — 한 발 남으면 탄 옆에 턱이 보이고, 비면 끝까지 올라온다. 턱을 +X 쪽에 만들어 두고 좌우를 뒤집어 쓴다")]
        [SerializeField] Transform m_Follower;

        [Tooltip("탄 두 줄이 가운데에서 좌우로 떨어진 거리(m)")]
        [SerializeField] float m_ColumnOffset = 0.005f;

        [Tooltip("한 발마다 내려가는 높이(m) — 두 줄이 엇갈려 쌓인 간격")]
        [SerializeField] float m_Stagger = 0.0046f;

        [Tooltip("마지막 한 발이 오른쪽에 놓이는지 (탄두를 앞으로 두고 위에서 볼 때).\n" +
                 "끄면 왼쪽 — 30발들이 탄창이 보통 이렇다: 가득(30발)이면 우상탄, 한 발 빠지면(29발) 좌상탄")]
        [SerializeField] bool m_LastRoundOnRight;

        // 지금 씬에 있는 탄창 전부 — 삽입구가 끼울 탄창을 찾을 때 본다
        static readonly List<RifleMagazine> s_All = new List<RifleMagazine>();
        public static IReadOnlyList<RifleMagazine> All => s_All;

        XRGrabInteractable m_Grab;
        Collider[] m_Colliders;
        int m_Rounds;
        float m_LastHitSound = -1f;

        // 만든 쪽(탄입대·삽입구)이 만든 직후에 정한다 — 윗면의 탄도 거기에 맞춘다
        public int Rounds
        {
            get => m_Rounds;
            set
            {
                m_Rounds = value;
                ShowRounds();
            }
        }

        public int Capacity => m_Capacity;
        public Transform FeedPoint => m_FeedPoint;
        public XRGrabInteractable Grab => m_Grab;
        public Collider[] Colliders => m_Colliders;
        public bool IsHeld => m_Grab.isSelected;

        // 맨 위 탄이 오른쪽인지 (우상탄) — 탄이 없으면 뜻이 없다
        public bool TopRoundOnRight => (m_Rounds % 2 == 1) == m_LastRoundOnRight;

        void Awake()
        {
            m_Grab = GetComponent<XRGrabInteractable>();
            m_Colliders = GetComponentsInChildren<Collider>(true);
            Rounds = m_Capacity;
        }

        void OnEnable()
        {
            s_All.Add(this);
            // 꽂기 직전·뺀 직후엔 총몸과 겹친다 — 서로 밀면 든 총이 떨리니 총 충돌체와는 부딪히지 않게 한다
            foreach (MagazineWell well in MagazineWell.All)
                HandReach.IgnorePairs(m_Colliders, well.RifleColliders);
        }

        void OnDisable()
        {
            s_All.Remove(this);
        }

        // 떨어뜨린 탄창이 바닥 · 벽에 부딪히는 소리 (11/16, CombatAudio) — 손에 든 동안은 안 낸다.
        // 세게 부딪힐수록 크게, 튀면서 여러 번 닿아도 0.15초에 한 번
        void OnCollisionEnter(Collision collision)
        {
            if (IsHeld || Time.time - m_LastHitSound < 0.15f)
                return;
            float speed = collision.relativeVelocity.magnitude;
            if (speed < 0.8f)
                return;
            m_LastHitSound = Time.time;
            CombatAudio.PlayMagazineDrop(collision.contactCount > 0 ? collision.GetContact(0).point : transform.position, speed);
        }

        // 남은 탄 수대로 윗면을 맞춘다. 세 물체는 같은 부모 아래에 있고, 부모의 원점이 '맨 위 탄'의 축 높이 · 좌우 가운데다
        void ShowRounds()
        {
            if (m_TopRound == null || m_NextRound == null || m_Follower == null)
                return;
            int rounds = Mathf.Max(0, m_Rounds);
            float last = m_LastRoundOnRight ? 1f : -1f;      // 마지막 한 발이 놓이는 쪽 (+1 = 오른쪽)
            float top = TopRoundOnRight ? 1f : -1f;          // 지금 맨 위 탄이 놓인 쪽
            m_TopRound.gameObject.SetActive(rounds >= 1);
            m_NextRound.gameObject.SetActive(rounds >= 2);
            m_Follower.gameObject.SetActive(rounds <= 1);
            m_TopRound.localPosition = new Vector3(top * m_ColumnOffset, 0f, m_TopRound.localPosition.z);
            m_NextRound.localPosition = new Vector3(-top * m_ColumnOffset, -m_Stagger, m_NextRound.localPosition.z);
            // 받침판의 턱은 마지막 한 발의 반대쪽 — 한 발 남았으면 '그 아래 탄' 높이, 비었으면 '맨 위 탄' 높이까지 올라온다
            m_Follower.localPosition = new Vector3(0f, rounds == 0 ? 0f : -m_Stagger, m_Follower.localPosition.z);
            m_Follower.localScale = new Vector3(-last, 1f, 1f);
        }
    }
}
