using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MilTrainSimVR.UrbanCombat
{
    // 가슴 탄창 (10/28) — 빈손을 가슴 앞 탄창에 대고 그립을 쥐면 새 탄창(가득)이 그 손에 잡힌다. 예비 탄창 수만큼만 나온다.
    // 손을 탄창 둘레(m_GrabZone 상자)에 댔을 때만 나온다 — 처음엔 보이지 않는 자리라 가슴 근처에서 그립을 쥐면 탄창이 튀어나왔고,
    // 다음엔 작은 주머니·좁은 자리(9cm)라 너무 빡빡했다 → 실제 크기 탄창을 꽂고 상자를 넉넉하게 (10/28 헤드셋 피드백 두 번).
    // 10/29: 상자 모양 탄입대는 대강 만든 티가 난다 → VR 게임처럼 방탄복 앞 주머니에 꽂힌 탄창으로 (사용자). 방탄복(m_Visual)은
    // 몸 판정(PlayerHitboxes)의 가슴에 붙어 다니고, 두 개씩 들어가는 주머니 3개에 탄창 칸이 6개(m_Tops)다 — 처음엔 5칸에 꽂혀 있고
    // (빈 칸 = 총에 꽂힌 몫), 꺼낼 때마다 손에 가장 가까운 칸의 탄창이 빠진다.
    // 방탄복엔 충돌체가 없다 (HandReach 설명). 대신 그 자리에서 손이 다른 것(예: 낮게 든 총)을 새로 잡지 못하게 막는다.
    // 11/12: 다시 넣기 — 안 쓴(가득 찬) 탄창을 든 손을 방탄복 자리로 가져가 그립을 놓으면 손에 가장 가까운 빈 칸으로 돌아간다
    // (11/11 헤드셋: "꺼냈다가 안 쓴 탄창은 원하면 다시 넣을 수 있게"). 쓰던 탄창은 받지 않는다 — 칸은 탄 수를 기억하지 않아서,
    // 받으면 다음에 꺼낼 때 가득 찬 탄창이 되어 나온다. 그런 탄창은 전처럼 손에서 떨어진다 (짧은 진동으로 알린다)
    public class MagazinePouch : MonoBehaviour, IXRSelectFilter
    {
        [SerializeField] PlayerHitboxes m_Body;
        [SerializeField] RifleMagazine m_MagazinePrefab;

        [Tooltip("방탄복 모습 — 시작할 때 가슴에 옮겨 붙인다. 원점 = 가슴 가운데(몸 판정의 가슴 상자 가운데)")]
        [SerializeField] Transform m_Visual;

        [Tooltip("탄창 칸 — 탄창이 꽂혀 있으면 켜져 있다. 처음엔 앞에서부터 예비 탄창 수만큼 켠다")]
        [SerializeField] GameObject[] m_Tops;

        [Tooltip("예비 탄창 수 (총에 꽂힌 탄창은 빼고)")]
        [SerializeField] int m_Spares = 5;

        [Tooltip("방탄복 자리 — 가슴 가운데 기준(몸이 보는 방향, m)")]
        [SerializeField] Vector3 m_Offset = Vector3.zero;

        [Tooltip("손의 잡기 점이 이 상자(방탄복 기준) 안에서 그립을 쥐면 꺼낸다 — 주머니와 튀어나온 탄창보다 넉넉하게, 앞·위로 더 넓게")]
        [SerializeField] Bounds m_GrabZone = new Bounds(new Vector3(0f, -0.045f, 0.21f), new Vector3(0.30f, 0.29f, 0.20f));

        // 손에 쥐는 방향 (잡기 점 = 주먹 가운데 기준) — 탄창 윗면이 주먹 축(엄지 쪽으로 빠져나가는 쪽), 탄창 앞(휜 쪽)이 손가락이 감싸는 쪽.
        // 손바닥이 탄창 뒷면에 붙고 손가락이 앞면을 감싼 모양이다. 헤드셋에서 보고 맞춘다
        static readonly Quaternion k_InHand = Quaternion.LookRotation(Vector3.down, Vector3.forward);

        NearFarInteractor[] m_Hands = new NearFarInteractor[0];

        public int Spares => m_Spares;
        public event Action SparesChanged;

        // 방탄복 자리 (월드)
        public Vector3 Point => m_Visual != null ? m_Visual.position
            : m_Body != null && m_Body.Chest != null ? m_Body.Chest.TransformPoint(m_Offset) : transform.position;

        bool IXRSelectFilter.canProcess => isActiveAndEnabled;

        void Awake()
        {
            m_Hands = GetComponentsInChildren<NearFarInteractor>(true);
        }

        void Start()
        {
            // 가슴 판정은 PlayerHitboxes 가 Awake 에서 만든다 — 그 뒤라 여기서 붙인다
            if (m_Visual != null && m_Body != null && m_Body.Chest != null)
            {
                m_Visual.SetParent(m_Body.Chest, false);
                m_Visual.localPosition = m_Offset;
                m_Visual.localRotation = Quaternion.identity;
            }
            if (m_Tops == null)
                return;
            for (int i = 0; i < m_Tops.Length; i++)
            {
                if (m_Tops[i] != null)
                    m_Tops[i].SetActive(i < m_Spares);
            }
        }

        // 손에 가장 가까운 칸 — filled: 탄창이 꽂힌 칸 / 빈 칸 가운데서. 없으면 -1
        int NearestSlot(Vector3 point, bool filled)
        {
            int nearest = -1;
            if (m_Tops == null)
                return nearest;
            float best = float.MaxValue;
            for (int i = 0; i < m_Tops.Length; i++)
            {
                if (m_Tops[i] == null || m_Tops[i].activeSelf != filled)
                    continue;
                float d = (m_Tops[i].transform.position - point).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    nearest = i;
                }
            }
            return nearest;
        }

        // 손에 가장 가까운 꽂힌 탄창을 뺀다
        void TakeNearest(Vector3 point)
        {
            int slot = NearestSlot(point, true);
            if (slot >= 0)
                m_Tops[slot].SetActive(false);
        }

        void OnEnable()
        {
            foreach (NearFarInteractor hand in m_Hands)
            {
                hand.selectFilters.Add(this);
                hand.selectExited.AddListener(OnHandReleased);
            }
        }

        void OnDisable()
        {
            foreach (NearFarInteractor hand in m_Hands)
            {
                if (hand == null)
                    continue;
                hand.selectFilters.Remove(this);
                hand.selectExited.RemoveListener(OnHandReleased);
            }
        }

        // 다시 넣기 — 손이 방탄복 자리에서 탄창을 놓았을 때. 가득 찬 탄창이고 빈 칸이 있으면 그 칸에 꽂고 손의 탄창은 없앤다
        void OnHandReleased(SelectExitEventArgs args)
        {
            // 총에 꽂히면서 손에서 빠진 것(MagazineWell 이 잡기를 취소한다)은 놓은 것이 아니다 — 총을 가슴 앞에서 갈아 끼울 때 여기로 온다
            if (args.isCanceled || !(args.interactorObject is NearFarInteractor hand) || !InZone(hand))
                return;
            if (!(args.interactableObject is Component held) || !held.TryGetComponent(out RifleMagazine mag))
                return;
            Vector3 point = HandReach.GrabPoint(hand);
            int slot = NearestSlot(point, false);
            if (mag.Rounds < mag.Capacity || slot < 0)
            {
                // 쓰던 탄창이거나 빈 칸이 없다 — 받지 않고 떨어지게 둔다
                hand.SendHapticImpulse(0.15f, 0.03f);
                return;
            }
            m_Tops[slot].SetActive(true);
            m_Spares++;
            // 손에서 놓이는 처리(XRI)가 이번 프레임에 끝난 뒤 없어진다
            Destroy(mag.gameObject);
            hand.SendHapticImpulse(0.3f, 0.04f);
            SparesChanged?.Invoke();
            Debug.Log($"[C] 방탄복 주머니에 탄창 다시 넣음 (남은 예비 {m_Spares})");
        }

        void Update()
        {
            NearFarInteractor hand = null;
            foreach (NearFarInteractor h in m_Hands)
            {
                if (HandReach.IsFreshGrip(h) && InZone(h))
                {
                    hand = h;
                    break;
                }
            }
            if (hand == null)
                return;
            if (m_Spares <= 0)
            {
                // 예비 탄창이 다 떨어졌다 — 짧은 진동으로만 알린다
                hand.SendHapticImpulse(0.15f, 0.03f);
                return;
            }
            RifleMagazine mag = Instantiate(m_MagazinePrefab, HandReach.GrabPoint(hand), HandReach.GrabRotation(hand) * k_InHand);
            hand.interactionManager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)mag.Grab);
            m_Spares--;
            TakeNearest(HandReach.GrabPoint(hand));
            hand.SendHapticImpulse(0.3f, 0.04f);
            SparesChanged?.Invoke();
            Debug.Log($"[C] 방탄복 주머니에서 탄창 꺼냄 (남은 예비 {m_Spares})");
        }

        // 탄창 자리에선 손이 다른 것을 새로 잡지 않게 한다 — 그 손엔 탄창을 준다. 탄창 줍기와 이미 쥔 것은 그대로
        bool IXRSelectFilter.Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            if (interactor.IsSelecting(interactable) || (interactable is Component c && c.TryGetComponent(out RifleMagazine _)))
                return true;
            return !(interactor is NearFarInteractor hand && InZone(hand));
        }

        // 손의 잡기 점이 탄창 자리 안인지
        bool InZone(NearFarInteractor hand)
        {
            return m_Visual != null ? HandReach.InBox(hand, m_Visual, m_GrabZone) : HandReach.IsNear(hand, Point, 0.12f);
        }
    }
}
