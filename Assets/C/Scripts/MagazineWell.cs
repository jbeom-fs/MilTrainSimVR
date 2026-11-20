using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MilTrainSimVR.UrbanCombat
{
    // 소총의 탄창 삽입구 (10/28) — 탄창이 꽂혀 있는지와 그 탄 수를 맡는다. 약실은 총(TempRifle)이 맡는다.
    //   · 꽂힌 모습: A 의 K2C1 에 들어 있는 탄창 부품(Part_Magazine)을 켜고 끈다 (A 파일은 안 건드린다)
    //   · 끼우기: 손에 든 탄창(RifleMagazine)의 윗면을 삽입구 가까이, 위·앞 방향을 총과 맞춰 가져가면 꽂힌다 — 손은 저절로 놓인다
    //   · 손으로 빼기: 총을 든 채 다른 손을 꽂힌 탄창에 대고 그립을 쥐면 그 손에 빠져나온다
    //   · 탄창멈치: 총 든 손의 A/X 를 누르면 아래로 떨어진다 (TempRifle 이 Drop 을 부른다)
    // 탄창 자리엔 충돌체를 두지 않는다 (HandReach 설명). 대신 거기서 다른 손이 총을 새로 잡지 못하게 막는다
    // — 안 그러면 XRI 가 그 손에 탄창 대신 총(두 손 잡기)을 쥐여 준다
    [RequireComponent(typeof(XRGrabInteractable))]
    public class MagazineWell : MonoBehaviour, IXRSelectFilter
    {
        [Tooltip("꽂힌 탄창의 모습 — K2C1 의 Part_Magazine. 원점이 탄창 가운데다")]
        [SerializeField] GameObject m_Seated;

        [Tooltip("꽂혔을 때 탄창 윗면(급탄구) 가운데가 오는 자리")]
        [SerializeField] Transform m_FeedPoint;

        [Tooltip("빼거나 떨어뜨릴 때 새로 만드는 탄창")]
        [SerializeField] RifleMagazine m_MagazinePrefab;

        [Tooltip("처음 꽂혀 있는 탄창의 탄 수. 0 보다 작으면 탄창 없이 시작")]
        [SerializeField] int m_StartRounds = 30;

        [Header("손")]
        [Tooltip("끼우기 — 탄창 윗면이 이 거리(m) 안에 오면 꽂힌다")]
        [SerializeField] float m_InsertDistance = 0.06f;

        [Tooltip("끼우기 — 탄창의 위·앞 방향이 총과 이 각도(°) 안이어야 꽂힌다")]
        [SerializeField] float m_InsertAngle = 35f;

        [Tooltip("손으로 빼기 — 손의 잡기 점이 이 상자(총 기준) 안에서 그립을 쥐면 빠진다. 탄창 몸통 둘레, 레일 높이보다 아래.\n" +
                 "장전 손잡이·레일(받치는 손) 자리가 먼저다 — 레일 뒤끝을 잡다가 탄창이 빠지지 않게 (10/28 헤드셋 피드백)")]
        [SerializeField] Bounds m_PullZone = new Bounds(new Vector3(0f, -0.045f, 0.18f), new Vector3(0.14f, 0.15f, 0.14f));

        // 방금 뺀 탄창이 다시 꽂히려면 삽입구에서 이만큼(m) 멀어졌다 와야 한다 — 끼우는 거리(m_InsertDistance)보다 커야 한다
        const float k_RearmDistance = 0.10f;

        // 지금 씬에 있는 삽입구 전부 — 새로 생긴 탄창이 이 총들과 부딪히지 않게 할 때 본다
        static readonly List<MagazineWell> s_All = new List<MagazineWell>();
        public static IReadOnlyList<MagazineWell> All => s_All;

        XRGrabInteractable m_Rifle;
        RifleHands m_Hands;
        // 방금 뺀 탄창 — 손으로 뺀 순간엔 꽂혀 있던 자리 그대로라, 막지 않으면 다음 프레임에 바로 다시 꽂힌다 (10/28 시험에서 확인)
        RifleMagazine m_JustRemoved;
        RifleChargingHandle m_ChargingHandle;
        Collider[] m_RifleColliders;
        XROrigin m_HolderRig;
        NearFarInteractor[] m_HolderHands = new NearFarInteractor[0];

        public bool HasMagazine { get; private set; }
        public int Rounds { get; private set; }
        public Collider[] RifleColliders => m_RifleColliders;
        public Vector3 SeatCenter => m_Seated.transform.position;

        public event System.Action Inserted;
        public event System.Action Removed;

        bool IXRSelectFilter.canProcess => isActiveAndEnabled;

        void Awake()
        {
            m_Rifle = GetComponent<XRGrabInteractable>();
            m_ChargingHandle = GetComponent<RifleChargingHandle>();
            m_Hands = GetComponent<RifleHands>();
            m_RifleColliders = GetComponentsInChildren<Collider>(true);
            SetSeated(m_StartRounds >= 0, m_StartRounds);
        }

        void OnEnable()
        {
            s_All.Add(this);
            m_Rifle.selectFilters.Add(this);
            foreach (RifleMagazine mag in RifleMagazine.All)
                HandReach.IgnorePairs(mag.Colliders, m_RifleColliders);
        }

        void OnDisable()
        {
            s_All.Remove(this);
            m_Rifle.selectFilters.Remove(this);
        }

        // 약실로 한 발 보낸다 — 꽂힌 탄창에 탄이 있으면 하나 빼고 true
        public bool TakeRound()
        {
            if (!HasMagazine || Rounds <= 0)
                return false;
            Rounds--;
            return true;
        }

        // 탄창멈치 — 꽂힌 탄창이 아래로 떨어진다
        public void Drop()
        {
            if (!HasMagazine)
                return;
            RifleMagazine mag = TakeOut();
            // 아래로 툭 — 손이나 총에 걸려 있지 않게 바로 멀어진다
            if (mag.TryGetComponent(out Rigidbody body))
                body.linearVelocity = -transform.up * 0.8f;
            Debug.Log($"[C] 탄창 떨어뜨림 (남은 탄 {mag.Rounds})");
        }

        // 꽂힌 탄창을 떨어뜨리지 않고 없앤다 — 소리 · Removed 없음 (A 사로의 "처음으로", TempRifle.ResetState)
        public void Clear()
        {
            SetSeated(false, 0);
        }

        void Update()
        {
            UpdateHolder();
            // 아무도 총을 안 들었으면(탁자 위) 끼우기·빼기를 하지 않는다
            if (m_HolderRig == null)
                return;
            if (HasMagazine)
                TryPullOut();
            else
                TryInsert();
        }

        // 총을 든 사람(먼저 잡은 손의 XR Origin)과 그 사람의 두 손
        void UpdateHolder()
        {
            XROrigin rig = m_Rifle.interactorsSelecting.Count > 0 ? m_Rifle.interactorsSelecting[0].transform.GetComponentInParent<XROrigin>() : null;
            if (rig == m_HolderRig)
                return;
            m_HolderRig = rig;
            m_HolderHands = rig != null ? rig.GetComponentsInChildren<NearFarInteractor>(true) : new NearFarInteractor[0];
        }

        void TryPullOut()
        {
            NearFarInteractor hand = null;
            foreach (NearFarInteractor h in m_HolderHands)
            {
                if (!HandReach.IsFreshGrip(h) || !HandReach.InBox(h, transform, m_PullZone))
                    continue;
                // 장전 손잡이·레일(받치는 손) 자리가 먼저다
                if ((m_ChargingHandle != null && m_ChargingHandle.InGrabZone(h)) || (m_Hands != null && m_Hands.InSupportZone(h)))
                    continue;
                hand = h;
                break;
            }
            if (hand == null)
                return;
            RifleMagazine mag = TakeOut();
            // 꽂혀 있던 자리 그대로 그 손에 쥐여 준다 (탄창 잡기는 잡은 자리를 기억하는 방식이라 손으로 튀지 않는다)
            hand.interactionManager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)mag.Grab);
            hand.SendHapticImpulse(0.3f, 0.04f);
            Debug.Log($"[C] 탄창 뺌 (남은 탄 {mag.Rounds})");
        }

        void TryInsert()
        {
            // 방금 뺀 탄창이 충분히 멀어졌으면 다시 꽂을 수 있게 풀어 준다
            if (m_JustRemoved != null && (m_JustRemoved.FeedPoint.position - m_FeedPoint.position).sqrMagnitude > k_RearmDistance * k_RearmDistance)
                m_JustRemoved = null;

            foreach (RifleMagazine mag in RifleMagazine.All)
            {
                if (!mag.IsHeld || mag.FeedPoint == null || mag == m_JustRemoved)
                    continue;
                if ((mag.FeedPoint.position - m_FeedPoint.position).sqrMagnitude > m_InsertDistance * m_InsertDistance)
                    continue;
                // 거꾸로 들었거나 앞뒤가 바뀐 탄창은 안 들어간다
                if (Vector3.Angle(mag.transform.up, transform.up) > m_InsertAngle || Vector3.Angle(mag.transform.forward, transform.forward) > m_InsertAngle)
                    continue;
                Insert(mag);
                return;
            }
        }

        void Insert(RifleMagazine mag)
        {
            var hand = mag.Grab.interactorsSelecting.Count > 0 ? mag.Grab.interactorsSelecting[0] as XRBaseInputInteractor : null;
            // 손에서 놓고 그 탄창은 없앤다 — 꽂힌 모습은 총의 탄창 부품이 맡는다
            mag.Grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)mag.Grab);
            SetSeated(true, mag.Rounds);
            Destroy(mag.gameObject);
            if (hand != null)
                hand.SendHapticImpulse(0.5f, 0.05f);
            Debug.Log($"[C] 탄창 끼움 ({Rounds}발)");
            Inserted?.Invoke();
        }

        // 꽂힌 탄창을 같은 자리·같은 탄 수의 탄창 오브젝트로 바꾼다
        RifleMagazine TakeOut()
        {
            Transform seat = m_Seated.transform;
            RifleMagazine mag = Instantiate(m_MagazinePrefab, seat.position, seat.rotation);
            mag.Rounds = Rounds;
            m_JustRemoved = mag;
            SetSeated(false, 0);
            Removed?.Invoke();
            return mag;
        }

        void SetSeated(bool seated, int rounds)
        {
            HasMagazine = seated;
            Rounds = seated ? Mathf.Max(0, rounds) : 0;
            if (m_Seated != null)
                m_Seated.SetActive(seated);
        }

        // 총을 이미 든 사람의 다른 손이 꽂힌 탄창 자리에서 총을 새로 잡지 못하게 한다 — 그 손엔 탄창을 준다(TryPullOut).
        // 이미 잡고 있는 손(앞손)은 그대로 둔다 — 잡은 채 그 자리를 지나가도 놓치지 않게
        bool IXRSelectFilter.Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            if (!HasMagazine || interactable.interactorsSelecting.Count == 0 || interactor.IsSelecting(interactable))
                return true;
            return !(interactor is NearFarInteractor hand && HandReach.InBox(hand, transform, m_PullZone));
        }
    }
}
