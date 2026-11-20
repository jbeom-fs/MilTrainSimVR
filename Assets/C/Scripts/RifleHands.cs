using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace MilTrainSimVR.UrbanCombat
{
    // 소총을 쥔 두 손 (10/28 헤드셋 피드백 — "손이 기준이 아니라 총에 손이 달라붙게", "손은 언제든 뗄 수 있게"):
    //   · 방아쇠 손: 총을 그 손의 실제 위치(손 모양 뿌리 = 컨트롤러 그립 자세)에 붙인다 — 권총손잡이 자리(m_TriggerHandPose)가 손에 온다.
    //     손 모델도 그 자리로 옮겨 붙이니, 보이는 손 = 실제 손 = 권총손잡이를 쥔 손이 된다.
    //     검지는 트리거를 안 당기면 방아쇠울 밖 총몸 옆에 곧게 두고, 당기는 만큼 방아쇠로 감는다. 쏘는 건 끝까지(TempRifle.FireAt) 당겼을 때.
    //     자리·검지 각도는 오른손 기준 — 왼손으로 쥐면 좌우를 뒤집어 쓴다 (11/12)
    //   · 받치는 손: 다른 손으로 레일 자리(m_SupportZone 상자)에서 그립을 쥐면 손 모델이 레일 밑을 받치는 자리(m_SupportHandPose)에 붙고
    //     총은 '방아쇠 손 → 받치는 손' 방향을 본다(두 손 조준). 그립을 놓으면 바로 떨어진다
    // 손 자리·검지 각도는 손 모델의 쥔 손가락 모양을 재고 렌더로 확인해 정했다 (CombatAssetsBuilder 상수).
    // 잡기 판정은 손 모양 쪽 잡기 점(C_PalmPoint, 실제 손)으로 하니 탄창·장전 손잡이·탄입대는 그대로다.
    // XRI 의 두 손 잡기는 쓰지 않는다: 총은 한 손만 잡게 막고(선택 필터), 위치·두 손 조준은 이 잡기 변환기가 정한다
    // (변환기 순서: XRGeneralGrabTransformer → 이것 → 반동 RifleRecoil)
    [RequireComponent(typeof(XRGrabInteractable))]
    public class RifleHands : XRBaseGrabTransformer, IXRSelectFilter
    {
        [Tooltip("방아쇠 손의 손 모양 뿌리(그립 자세)가 오는 자리 — 오른손 기준. 왼손이 쥐면 좌우를 뒤집어 쓴다. 총 바로 아래 자식이어야 한다")]
        [SerializeField] Transform m_TriggerHandPose;

        [Tooltip("받치는 손의 손 모양 뿌리가 오는 자리 — 왼손 기준. 오른손이 받치면 좌우를 뒤집어 쓴다. 총 바로 아래 자식이어야 한다")]
        [SerializeField] Transform m_SupportHandPose;

        [Tooltip("받치는 손으로 그립을 쥐면 붙는 자리 — 총 기준 상자. 레일(총열덮개) 전체와 그 아래, 뒤끝까지.\n" +
                 "장전 손잡이 자리는 빼고, 탄창 자리보다는 먼저다 — 레일을 잡다가 탄창이 빠지지 않게 (10/28 헤드셋 피드백)")]
        [SerializeField] Bounds m_SupportZone = new Bounds(new Vector3(0f, 0.08f, 0.37f), new Vector3(0.18f, 0.18f, 0.32f));

        [Header("검지 마디 각도 — x 첫째 · y 둘째 · z 끝 마디 굽힘, w 첫째 마디 벌림 (도, 오른손 기준 — 왼손은 벌림을 뒤집는다)")]
        // 11/12: 손 모델 자리를 손잡이에 맞춰 옮기며 다시 맞췄다 — 마디마다 10/28 에 맞춘 검지 끝 자리(방아쇠 날)에서 4~7mm 안, 총에 1mm(방아쇠엔 1.5mm) 넘게 파고들지 않게
        [Tooltip("트리거를 안 당겼을 때 — 방아쇠울 밖, 총몸 오른쪽에 곧게")]
        [SerializeField] Vector4 m_IndexOff = new Vector4(-11.5f, -20f, -20f, -45f);

        [Tooltip("쏘기 직전 — 끝마디가 방아쇠 날 앞에 닿는다")]
        [SerializeField] Vector4 m_IndexOnTrigger = new Vector4(-17.25f, 30.25f, 48.75f, -38.5f);

        [Tooltip("끝까지 당겼을 때")]
        [SerializeField] Vector4 m_IndexPulled = new Vector4(-15.75f, 50.5f, 38f, -39.25f);

        [Header("방아쇠 손 모델 — 권총손잡이 겉면에 맞춘 자리 · 손가락 (11/12, 오른손 기준 — 왼손은 거울상)")]
        [Tooltip("보이는 손 모델만 방아쇠 손 자리에서 이만큼 옮긴다 (m, 총 기준). 총이 실제 손(컨트롤러)에 붙는 자리(m_TriggerHandPose)는 그대로라 조준은 안 바뀐다.\n" +
                 "손바닥이 손잡이 옆면에 닿고 손가락이 앞을 감싸게 — 전에는 손바닥이 손잡이 속 1.2cm 에 있어 손가락이 손잡이를 뚫었다")]
        [SerializeField] Vector3 m_TriggerModelOffset = new Vector3(0.028f, -0.0167f, 0.0089f);

        [Tooltip("보이는 손 모델을 돌리는 각도 (도, 총 기준 오일러) — 손을 손잡이 축으로 22° 감아 손바닥은 옆면, 손가락 뿌리는 앞 모서리에 온다")]
        [SerializeField] Vector3 m_TriggerModelRotation = new Vector3(-3.77f, -18.56f, -10.82f);

        [Tooltip("방아쇠 손 가운데 손가락 마디 각도 (도, 첫째·둘째·끝) — 마디마다 손잡이 겉면에 닿고 1mm 넘게 파고들지 않게, 손끝은 왼쪽 면에 오게 손가락마다 따로 맞췄다")]
        [SerializeField] Vector3 m_GripMiddle = new Vector3(42.5f, 12.5f, 87.5f);

        [Tooltip("방아쇠 손 약지 마디 각도 (도, 첫째·둘째·끝)")]
        [SerializeField] Vector3 m_GripRing = new Vector3(40f, 20f, 50f);

        [Tooltip("방아쇠 손 새끼 마디 각도 (도, 첫째·둘째·끝) — 손잡이 아래쪽(속이 빈 밑뚜껑 부분)을 감싼다")]
        [SerializeField] Vector3 m_GripLittle = new Vector3(22.5f, 15f, 25f);

        [Tooltip("방아쇠 손 엄지 굽힘 (0~1, 음수 = 젖힘) — 총몸 왼쪽에 가장 덜 파고드는 값 (2mm)")]
        [SerializeField] float m_GripThumb = 0.1f;

        [Tooltip("받치는 손 가운데·약지·새끼의 마디 각도(도, 첫째·둘째·끝) — 레일 오른쪽 아래 모서리를 감아 둘째·끝 마디가 옆면에 닿는다.\n" +
                 "끝 마디 음수 = 손끝을 옆면에 눌러 살짝 젖힘. 굽힘 하나로 쥐면 손가락이 레일을 뚫었다 (10/28)")]
        [SerializeField] Vector3 m_SupportFingers = new Vector3(12f, 52f, -10f);

        [Tooltip("받치는 손 검지의 마디 각도(도, 첫째·둘째·끝) — 다른 손가락보다 길어서 따로 맞췄다")]
        [SerializeField] Vector3 m_SupportIndex = new Vector3(2f, 42f, 0f);

        [Tooltip("받치는 손 엄지 굽힘 (0~1, 음수 = 젖힘) — 레일 왼쪽 아래 모서리를 따라 앞으로 곧게 뻗는다")]
        [SerializeField] float m_SupportThumb = -0.9f;

        // 총에 붙인 손 모델 하나 — 떼면 원래 부모·자리로 돌려놓는다. 장전 손잡이(RifleChargingHandle)도 같은 방식으로 쓴다
        internal class Snap
        {
            public ControllerHandPose hand;
            public Transform model;
            public Transform home;
            public Vector3 homePosition;
            public Quaternion homeRotation;
        }

        XRGrabInteractable m_Grab;
        TempRifle m_Rifle;
        RifleChargingHandle m_ChargingHandle;
        Vector3 m_TriggerPosition;        // 방아쇠 손 자리의 원래(오른손) 위치
        Quaternion m_TriggerRotation;     // 방아쇠 손 자리의 원래(오른손) 방향
        Transform m_TriggerModelPose;     // 보이는 방아쇠 손 모델이 붙는 자리 = 방아쇠 손 자리 + 보정 (실행 중에 만든다)
        readonly Vector3[] m_GripFingers = new Vector3[3];
        Vector3 m_SupportPosition;        // 받치는 손 자리의 원래(왼손) 위치
        Quaternion m_SupportRotation;     // 받치는 손 자리의 원래(왼손) 방향
        Snap m_Trigger;
        Snap m_Support;
        NearFarInteractor m_Primary;      // 총을 잡은 손
        ControllerHandPose m_PrimaryHand; // 그 손의 손 모양 (뿌리 = 실제 손 그립 자세)
        NearFarInteractor m_Other;        // 그 사람의 다른 손 — 받칠 수 있는 손

        // 지금 총열덮개를 받치고 있는 손 (없으면 null) — 반동 줄이기·진동·손목 표시가 본다
        public NearFarInteractor SupportHand => m_Support != null ? m_Other : null;
        public bool IsSupported => m_Support != null;

        // 손의 잡기 점이 레일(받치는 손) 자리 안인지 — 탄창(MagazineWell)이 이 자리를 빼고 본다
        public bool InSupportZone(NearFarInteractor hand)
        {
            return HandReach.InBox(hand, transform, m_SupportZone);
        }

        bool IXRSelectFilter.canProcess => isActiveAndEnabled;

        void Awake()
        {
            m_Grab = GetComponent<XRGrabInteractable>();
            m_Rifle = GetComponent<TempRifle>();
            m_ChargingHandle = GetComponent<RifleChargingHandle>();
            if (m_TriggerHandPose != null)
            {
                m_TriggerPosition = m_TriggerHandPose.localPosition;
                m_TriggerRotation = m_TriggerHandPose.localRotation;
                m_TriggerModelPose = new GameObject("Pose_TriggerHandModel").transform;
                m_TriggerModelPose.SetParent(m_TriggerHandPose.parent, false);
            }
            if (m_SupportHandPose != null)
            {
                m_SupportPosition = m_SupportHandPose.localPosition;
                m_SupportRotation = m_SupportHandPose.localRotation;
            }
        }

        void OnEnable()
        {
            m_Grab.selectFilters.Add(this);
        }

        void OnDisable()
        {
            m_Grab.selectFilters.Remove(this);
            Detach(ref m_Trigger);
            Detach(ref m_Support);
            m_Primary = null;
            m_PrimaryHand = null;
            m_Other = null;
        }

        void Update()
        {
            UpdateHands();
            UpdateTriggerHand();
            UpdateSupportHand();
        }

        // 총을 잡은 손과 그 사람의 다른 손 — 잡은 손이 바뀌면 붙어 있던 손을 다 뗀다
        void UpdateHands()
        {
            NearFarInteractor primary = m_Grab.interactorsSelecting.Count > 0 ? m_Grab.interactorsSelecting[0] as NearFarInteractor : null;
            if (primary == m_Primary)
                return;
            Detach(ref m_Trigger);
            Detach(ref m_Support);
            m_Primary = primary;
            m_PrimaryHand = primary != null ? HandReach.HandVisual(primary) : null;
            // 자리는 오른손 기준으로 만들어 두었다 — 왼손이 권총손잡이를 쥐면 좌우를 뒤집는다 (11/12 헤드셋: 왼손으로 쥐면 손이 어긋났다).
            // 손 모델은 좌우 거울상이고(뼈·메시를 재 보니 0.5mm 안) 총도 좌우 대칭이라 뒤집은 자리에 놓으면 오른손 모습의 거울상이 된다.
            // 여기서 정해야 같은 프레임의 Process(총 위치)가 바뀐 자리를 쓴다
            if (m_TriggerHandPose != null && m_PrimaryHand != null)
            {
                bool left = m_PrimaryHand.IsLeft;
                m_TriggerHandPose.localPosition = left ? Mirror(m_TriggerPosition) : m_TriggerPosition;
                m_TriggerHandPose.localRotation = left ? Mirror(m_TriggerRotation) : m_TriggerRotation;
                // 보이는 손 모델 자리 — 오른손 기준 보정을 더한 뒤 왼손이면 통째로 뒤집는다
                Vector3 modelPosition = m_TriggerPosition + m_TriggerModelOffset;
                Quaternion modelRotation = Quaternion.Euler(m_TriggerModelRotation) * m_TriggerRotation;
                m_TriggerModelPose.localPosition = left ? Mirror(modelPosition) : modelPosition;
                m_TriggerModelPose.localRotation = left ? Mirror(modelRotation) : modelRotation;
            }
            m_Other = null;
            XROrigin rig = primary != null ? primary.GetComponentInParent<XROrigin>() : null;
            if (rig != null)
            {
                foreach (NearFarInteractor hand in rig.GetComponentsInChildren<NearFarInteractor>(true))
                {
                    if (hand != primary && hand.handedness != primary.handedness)
                        m_Other = hand;
                }
            }
        }

        void UpdateTriggerHand()
        {
            if (m_PrimaryHand == null || m_TriggerHandPose == null)
                return;
            if (m_Trigger == null)
                m_Trigger = Attach(m_PrimaryHand, m_TriggerModelPose);
            // 손잡이 겉면을 따라 감싼 세 손가락 · 엄지 + 트리거를 당긴 만큼의 검지
            m_Trigger.hand.GripOverride = 1f;
            m_GripFingers[0] = m_GripMiddle;
            m_GripFingers[1] = m_GripRing;
            m_GripFingers[2] = m_GripLittle;
            m_Trigger.hand.OthersPerFingerOverride = m_GripFingers;
            m_Trigger.hand.ThumbOverride = m_GripThumb;
            Vector4 index = IndexFor(m_Trigger.hand.TriggerValue);
            m_Trigger.hand.IndexAnglesOverride = new Vector3(index.x, index.y, index.z);
            // 벌림(옆으로 돌리기)은 거울에 비치면 방향이 바뀐다 — 그대로 쓰면 왼손 검지가 총몸 옆이 아니라 방아쇠울 아래로 비스듬히 빠졌다
            m_Trigger.hand.IndexSpread = m_Trigger.hand.IsLeft ? -index.w : index.w;
        }

        // 총의 좌우(X)를 뒤집은 자리·방향 — 왼손·오른손 자리를 서로 바꿀 때
        static Vector3 Mirror(Vector3 p)
        {
            return new Vector3(-p.x, p.y, p.z);
        }

        static Quaternion Mirror(Quaternion q)
        {
            return new Quaternion(q.x, -q.y, -q.z, q.w);
        }

        // 트리거를 당긴 만큼 검지를 방아쇠 쪽으로 — 쏘는 기준까지는 방아쇠로 다가가기, 그 뒤로는 당기기
        Vector4 IndexFor(float pull)
        {
            float fireAt = m_Rifle != null ? m_Rifle.FireAt : 0.9f;
            if (pull < fireAt)
                return Vector4.Lerp(m_IndexOff, m_IndexOnTrigger, pull / fireAt);
            return Vector4.Lerp(m_IndexOnTrigger, m_IndexPulled, (pull - fireAt) / Mathf.Max(0.01f, 1f - fireAt));
        }

        void UpdateSupportHand()
        {
            if (m_Other == null || m_SupportHandPose == null)
                return;
            // 그 손으로 다른 걸 잡고 있거나(탄창) 장전 손잡이를 당기는 중이면 받치지 않는다
            bool busy = m_Other.hasSelection || (m_ChargingHandle != null && m_ChargingHandle.Hand == m_Other);
            if (m_Support != null)
            {
                // 그립을 놓으면 바로 뗀다 — 손은 언제든 뗄 수 있다
                if (busy || !m_Other.selectInput.ReadIsPerformed())
                    Detach(ref m_Support);
                return;
            }
            if (busy)
                return;
            // 그립을 쥔 순간 레일 자리 안이면 붙인다 — 장전 손잡이 자리는 그쪽 몫
            if (!HandReach.IsFreshGrip(m_Other) || !InSupportZone(m_Other))
                return;
            if (m_ChargingHandle != null && m_ChargingHandle.InGrabZone(m_Other))
                return;
            ControllerHandPose visual = HandReach.HandVisual(m_Other);
            if (visual == null)
                return;
            // 자리는 왼손 기준으로 만들어 두었다 — 오른손이 받치면 좌우(X)를 뒤집은 자리·방향으로 (손 모델도 좌우 거울상이다)
            m_SupportHandPose.localPosition = visual.IsLeft ? m_SupportPosition : Mirror(m_SupportPosition);
            m_SupportHandPose.localRotation = visual.IsLeft ? m_SupportRotation : Mirror(m_SupportRotation);
            m_Support = Attach(visual, m_SupportHandPose);
            // 네 손가락은 레일 모서리를 감아 옆면에, 엄지는 왼쪽 아래 모서리를 따라 앞으로
            m_Support.hand.IndexAnglesOverride = m_SupportIndex;
            m_Support.hand.IndexSpread = 0f;
            m_Support.hand.OthersAnglesOverride = m_SupportFingers;
            m_Support.hand.ThumbOverride = m_SupportThumb;
            m_Other.SendHapticImpulse(0.2f, 0.03f);
        }

        // 손 모델을 총의 자리 아래로 옮긴다 — 손 모양 뿌리(그립 자세) 밑에 있던 자리 그대로 두면 뿌리가 그 자리에 온 것처럼 보인다
        internal static Snap Attach(ControllerHandPose hand, Transform pose)
        {
            Transform model = hand.HandModel;
            var snap = new Snap { hand = hand, model = model, home = model.parent, homePosition = model.localPosition, homeRotation = model.localRotation };
            model.SetParent(pose, false);
            model.localPosition = snap.homePosition;
            model.localRotation = snap.homeRotation;
            return snap;
        }

        internal static void Detach(ref Snap snap)
        {
            if (snap == null)
                return;
            if (snap.model != null && snap.home != null)
            {
                snap.model.SetParent(snap.home, false);
                snap.model.localPosition = snap.homePosition;
                snap.model.localRotation = snap.homeRotation;
            }
            if (snap.hand != null)
            {
                snap.hand.GripOverride = null;
                snap.hand.IndexOverride = null;
                snap.hand.ThumbOverride = null;
                snap.hand.IndexAnglesOverride = null;
                snap.hand.IndexSpread = 0f;
                snap.hand.OthersAnglesOverride = null;
                snap.hand.OthersPerFingerOverride = null;
            }
            snap = null;
        }

        public override void Process(XRGrabInteractable grabInteractable, XRInteractionUpdateOrder.UpdatePhase updatePhase, ref Pose targetPose, ref Vector3 localScale)
        {
            if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic && updatePhase != XRInteractionUpdateOrder.UpdatePhase.OnBeforeRender)
                return;
            if (m_PrimaryHand == null || m_TriggerHandPose == null)
                return;

            // 1) 총을 방아쇠 손에 붙인다 — 손 모양 뿌리(실제 손의 그립 자세)에 권총손잡이 자리가 오게
            Transform grip = m_PrimaryHand.transform;
            targetPose.rotation = grip.rotation * Quaternion.Inverse(m_TriggerHandPose.localRotation);
            targetPose.position = grip.position - targetPose.rotation * m_TriggerHandPose.localPosition;

            // 2) 두 손 조준 — 받치는 손이 붙어 있으면 방아쇠 손을 축으로 총을 돌려 '방아쇠 손 → 받치는 손' 방향을 보게 한다
            // 받치는 손 모양 뿌리가 붙는 자리(m_SupportHandPose)가 실제 받치는 손 쪽에 오게 돈다 — 보이는 손이 실제 손 방향에 있게
            if (m_Support == null || m_Other == null)
                return;
            Vector3 pivot = grip.position;
            Vector3 along = m_SupportHandPose.localPosition - m_TriggerHandPose.localPosition;
            Vector3 wanted = HandReach.GrabPoint(m_Other) - pivot;
            // 받치는 손이 방아쇠 손에 너무 가까우면 방향이 흔들린다 — 그땐 한 손 그대로
            if (wanted.sqrMagnitude < 0.08f * 0.08f)
                return;
            targetPose.rotation = Quaternion.FromToRotation(targetPose.rotation * along, wanted) * targetPose.rotation;
            targetPose.position = pivot - targetPose.rotation * m_TriggerHandPose.localPosition;
        }

        // 총은 한 손만 XRI 로 잡는다 — 다른 손은 이 스크립트가 붙인다(UpdateSupportHand)
        bool IXRSelectFilter.Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            return interactable.interactorsSelecting.Count == 0 || interactor.IsSelecting(interactable);
        }
    }
}
