// 원본: Assets/C/Scripts/TempRifle.cs (커밋 a1a51ac, C 이주석). A 가 복사해 고쳤다 — Docs/A/1027_C사격코드_재사용검토.md
// 바꾼 것: 공이치기·약실 상태, 장전 손잡이(ChargingHandleSlide) 연동, 빈 격발(11/17부터 소리 없이 진동만), 탄 없이 시작,
//          Gunfire(시가지 전투의 총소리·탄착 알림) 호출 제거, 문구는 RifleTexts, 사격 상태 이벤트 추가.
// 10/28: 버튼 재장전을 없애고 탄을 꽂힌 탄창(MagazineWell — C 커밋 c798f18 복사)에서 받는다.
// 11/17 총기 통합 5단계: 탄창은 C 것(C MagazineWell · C_Magazine)을 그대로 쓴다 — IMagazineFeed 로만 본다 (A 복사본은 지움).
// 10/29: 탄 방향을 C 의 영점·탄도(RifleBallistics 복사)로 — 총구에서 25m 영점 맞춘 총열 + 중력 (C TempRifle.Shoot 과 같은 방식).
// 11/17 총기 통합 2단계: 방아쇠를 XRI Activate(트리거 절반쯤) 대신 잡은 손의 트리거 값으로 — m_FireAt 까지 당기면 쏜다 (C TempRifle 과 같은 방식 · 값).
using System.Collections.Generic;
using MilTrainSimVR.A.Common;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MilTrainSimVR.A.Rifle
{
    // A 의 사격용 K2C1 — 분해·결합의 기능 점검(공격발)과 사로 사격이 같이 쓴다.
    // 잡은 손의 트리거를 당긴 정도(0~1)를 직접 읽어 m_FireAt 까지 당기면 방아쇠를 당기고(PullTrigger) m_ResetAt 아래로 풀면 놓는다(ReleaseTrigger).
    // 그 전까지는 손 모양의 검지가 방아쇠 쪽으로 다가가기만 한다 (두 손 파지 K2C1RifleHands). 앞손 트리거는 숨 참기(SightAim)라 보지 않는다.
    // 탄이 나가는 조건: 조정간이 안전이 아니고, 공이치기가 젖혀져 있고, 약실에 탄이 있을 때.
    //   · 공이치기가 젖혀졌는데 약실이 비었으면 공이치기만 떨어진다 → 빈 격발 (소리 없이 손에 약한 진동). 다시 하려면 장전 손잡이를 당겨야 한다.
    //   · 쏘면 노리쇠가 저절로 뒤로 갔다 오며 공이치기를 다시 젖히고 꽂힌 탄창에서 다음 탄을 넣는다.
    //   · 장전 손잡이를 끝까지 당기면 공이치기가 젖혀지고 약실의 탄이 빠진다. 앞으로 돌아오면 꽂힌 탄창에서 탄을 넣는다.
    // 탄창은 손으로 끼우고 뺀다 (C MagazineWell — IMagazineFeed). 탄창이 없으면 장전되지 않는다.
    // 탄창멈치 (11/17 총기 통합 3단계 — C TempRifle 과 같게): 손잡이를 잡은 손의 A/X 를 누르면 꽂힌 탄창이 아래로 떨어진다. 약실의 탄은 그대로.
    //   XRI 기본 리그는 오른손 A 가 점프라 겹친다 → 총을 든 동안 그 리그의 점프(JumpProvider)를 끄고, 놓으면 되돌린다 (A 씬엔 넘을 곳이 없다).
    // 조정간은 잡은 손의 B/Y 버튼으로 돌리고, 총의 조정간 부품(Part_Selector)도 그 위치 각도로 돌려 보여 준다 (손으로 돌리기는 나중에).
    // VR 에선 총구나 손이 벽을 뚫고 들어갈 수 있다 → 쏠 때 머리→손잡이→총구 길이 막혔으면 막힌 곳에 맞힌다 (벽 너머 사격 방지).
    [RequireComponent(typeof(XRGrabInteractable))]
    // 사로는 RangeRifle 약속으로만 본다 (11/17 — 허브에서 C 방식을 고르면 C 총으로 바뀐다). 조정간 종류(FireMode)도 거기 있다.
    public class K2C1Rifle : RangeRifle
    {
        [Tooltip("총구. 이 오브젝트의 앞(+Z) 방향으로 쏜다")]
        [SerializeField] Transform m_Muzzle;

        [Tooltip("총몸 기준점 — 머리 → 이 점 → 총구 사이가 막혔으면 막힌 곳에 맞는다. 비우면 잡는 손잡이 기준점")]
        [SerializeField] Transform m_Breech;

        [SerializeField] ChargingHandleSlide m_ChargingHandle;

        [Tooltip("탄 방향을 정해 주는 것 (IShotLine, 예: 조준 확대 모드 SightAim). 비우거나 직선을 주지 않으면 총구 앞 방향")]
        [SerializeField] MonoBehaviour m_ShotLine;

        [Tooltip("영점·탄도 (RifleBallistics — C 것 복사). 있으면 탄은 총구에서 25m 영점 맞춘 총열 방향으로 나가 중력을 받는다. 비우면 총구 앞 방향 직선")]
        [SerializeField] RifleBallistics m_Ballistics;

        [Tooltip("사거리(m)")]
        [SerializeField] float m_Range = 100f;

        [Tooltip("맞을 수 있는 레이어. 지금은 전부")]
        [SerializeField] LayerMask m_HitMask = ~0;

        [Header("방아쇠")]
        [Tooltip("트리거를 이만큼(0~1) 당기면 쏜다 — 그 전까지는 검지가 방아쇠로 다가가기만 한다 (C TempRifle 과 같은 값)")]
        [SerializeField] float m_FireAt = 0.9f;

        [Tooltip("쏜 뒤 트리거를 이만큼 아래로 풀어야 놓은 것 — 단발은 그래야 다음 탄이 나간다")]
        [SerializeField] float m_ResetAt = 0.6f;

        [Header("탄창 · 조정간")]
        [Tooltip("탄창 삽입구 (IMagazineFeed — C 탄창을 쓰는 CMagazineFeed, 11/17 총기 통합 5단계). 비우면 탄창이 없는 총 (장전되지 않는다)")]
        [SerializeField] MonoBehaviour m_Well;

        [Tooltip("처음 조정간 위치")]
        [SerializeField] FireMode m_FireMode = FireMode.Safe;

        [Tooltip("조정간 부품 (Part_Selector) — 조정간을 바꾸면 이 부품을 자기 X축(총 좌우)으로 돌린다. 비우면 돌리지 않는다")]
        [SerializeField] Transform m_Selector;

        [Tooltip("조정간 각도(°) — 안전·단발·점사·연발 순서. 모델 자세 = 안전(0°), +각도는 앞쪽 끝이 위로 가는 방향. " +
                 "K2C1 (사용자 10/27): 왼쪽 면을 총구가 왼쪽에 오게 볼 때 9시 안전 · 12시 단발 · 3시 연발. 점사 없음 (점사 값은 쓰지 않는다)")]
        [SerializeField] float[] m_SelectorAngles = { 0f, 90f, 135f, 180f };

        [Tooltip("점사 위치가 있는 총인지. K2C1 은 없다 — 조정간이 안전 → 단발 → 연발 로 돈다")]
        [SerializeField] bool m_HasBurst;

        [Tooltip("분당 발사 수 — 점사·연발에서 다음 탄까지의 간격")]
        [SerializeField] float m_RoundsPerMinute = 750f;

        [Tooltip("점사 한 번에 나가는 탄 수")]
        [SerializeField] int m_BurstCount = 3;

        [Header("효과")]
        [SerializeField] MuzzleFlash m_MuzzleFlash;

        [Tooltip("반동 — XRGrabInteractable 의 잡기 변환기 목록에도 들어 있어야 움직인다")]
        [SerializeField] RifleRecoil m_Recoil;

        [Tooltip("탄도를 잠깐 보여 주는 선 — 헤드셋 없이 시험할 때 어디로 쐈는지 보려고")]
        [SerializeField] LineRenderer m_Tracer;

        [SerializeField] float m_TracerSeconds = 0.06f;

        [Header("소리 (비우면 임시 소리)")]
        [SerializeField] AudioSource m_Audio;
        [SerializeField] AudioClip m_ShotSound;

        [SerializeField] AudioClip m_SelectorSound;

        [Tooltip("장전 손잡이를 끝까지 당겼을 때")]
        [SerializeField] AudioClip m_HandleBackSound;

        [Tooltip("장전 손잡이가 앞으로 돌아와 노리쇠가 닫힐 때")]
        [SerializeField] AudioClip m_HandleForwardSound;

        XRGrabInteractable m_Grab;
        Quaternion m_SelectorRest;
        float m_TracerOffTime;
        RifleBallistics.SegmentCast m_Cast;                     // 탄도가 마디마다 쓰는 광선 = CastSkippingSelf (쏠 때마다 새로 만들지 않게)
        readonly List<Vector3> m_Path = new List<Vector3>();    // 방금 탄이 지난 점들 — 탄도선

        bool m_Chambered;      // 약실에 탄이 있는지
        bool m_HammerCocked;   // 공이치기가 젖혀져 있는지
        int m_ShotsFired;
        bool m_TriggerHeld;
        bool m_InputPulled;   // 손의 트리거로 당겼는지 — 시험이 PullTrigger 로 당긴 건 손을 풀어도 놓지 않는다
        int m_BurstLeft;
        float m_NextShotTime;

        InputAction m_SelectorButton;
        InputAction m_MagazineButton;
        readonly List<UnityEngine.XR.Interaction.Toolkit.Locomotion.Jump.JumpProvider> m_PausedJumps = new List<UnityEngine.XR.Interaction.Toolkit.Locomotion.Jump.JumpProvider>();   // 총을 든 동안 끈 점프

        // 총알이 어딘가에 맞을 때마다 — 탄착 효과가 듣는다
        public event System.Action<RaycastHit> HitSurface;

        // 한 발이 어떻게 날아갔는지 (11/17 총기 통합 4단계) — 명중 처리 뒤, Fired 앞에 알린다.
        // C 시가지의 총소리 · 탄착 알림(Gunfire)과 적 명중은 C 쪽 다리(Assets/A/Scripts/CBridge/K2C1Gunfire)가 이걸 듣고 낸다 — A 총 코드는 C 를 모른다
        public struct ShotReport
        {
            public Vector3 Origin;      // 탄이 나간 곳 (총구, 조준 확대 모드면 조준선에 맞춰 돌린 총구)
            public Vector3 End;         // 맞은 곳 또는 사거리 끝
            public Vector3 Direction;   // 맞을 때 탄이 날아간 방향 (길이 1)
            public bool Blocked;        // 총구가 벽 속이라 막힌 곳에 맞음 — 탄이 나가지 않은 것으로 본다
            public bool Struck;         // 무언가에 맞았는지 (Hit 이 유효)
            public RaycastHit Hit;
            public bool HandledByA;     // 맞은 것이 A 표적(A IHittable)이라 A 가 이미 명중 처리함
        }

        public event System.Action<ShotReport> ShotReported;

        public override event System.Action Fired;                  // 실탄 한 발
        public override event System.Action DryFired;               // 빈 격발 — 소리 없이 진동만 (11/17)
        public override event System.Action TriggerBlocked;         // 안전에서 방아쇠를 당김 — 당겨지지 않는다
        public override event System.Action HammerDown;             // 공이치기가 떨어진 채 방아쇠를 당김 — 아무 일 없다
        public override event System.Action<FireMode> FireModeChanged;
        public override event System.Action ChargingHandlePulled;   // 장전 손잡이를 끝까지 당김
        public override event System.Action ChargingHandleReturned; // 장전 손잡이가 앞으로 돌아옴

        // 꽂힌 탄창에 남은 탄 (약실 탄은 빼고)
        public int Rounds => Well != null ? Well.Rounds : 0;
        public override int MagazineRounds => Rounds;
        public override bool HasMagazine => Well != null && Well.HasMagazine;
        public IMagazineFeed Well => m_Feed ??= m_Well as IMagazineFeed;
        IMagazineFeed m_Feed;
        public override bool Chambered => m_Chambered;
        public override bool HammerCocked => m_HammerCocked;
        public override FireMode Mode => m_FireMode;
        public override bool IsHeld => m_Grab != null && m_Grab.isSelected;
        public float FireAt => m_FireAt;   // 두 손 파지의 검지가 '방아쇠에 닿음' 기준으로 쓴다

        // 두 손으로 들었는지 — 두 손 파지(IRifleSupport)면 레일을 받친 손이 있을 때. 없으면 XRI 로 두 손이 잡았을 때 (반동이 덜 튄다)
        bool TwoHanded => m_Support != null ? m_Support.SupportHand != null : m_Grab.interactorsSelecting.Count > 1;
        IRifleSupport m_Support;
        public ChargingHandleSlide ChargingHandle => m_ChargingHandle;
        public override float ChargingOffset => m_ChargingHandle != null ? m_ChargingHandle.Offset : 0f;
        public override float ChargingStroke => m_ChargingHandle != null ? m_ChargingHandle.Stroke : 0f;

        public override event System.Action MagazineInserted
        {
            add { if (Well != null) Well.Inserted += value; }
            remove { if (Well != null) Well.Inserted -= value; }
        }

        public override event System.Action MagazineRemoved
        {
            add { if (Well != null) Well.Removed += value; }
            remove { if (Well != null) Well.Removed -= value; }
        }

        // 총구가 향한 방향 (월드) — 기능 점검의 "총구를 위로" 판정이 쓴다
        public override Vector3 MuzzleForward => m_Muzzle != null ? m_Muzzle.forward : transform.forward;

        // 지금까지 실제로 나간 탄 수 — 사로 결과(명중률)에 쓴다
        public int ShotsFired => m_ShotsFired;

        void Awake()
        {
            m_Grab = GetComponent<XRGrabInteractable>();
            m_Support = GetComponent<IRifleSupport>();
            m_Cast = (Vector3 from, Vector3 dir, float distance, out RaycastHit hit) => CastSkippingSelf(from, dir, distance, out hit);
            m_Grab.selectEntered.AddListener(OnSelectEntered);
            m_Grab.selectExited.AddListener(OnSelectExited);
            if (m_ChargingHandle != null)
            {
                m_ChargingHandle.PulledBack += OnHandlePulledBack;
                m_ChargingHandle.ReturnedForward += OnHandleReturned;
            }
            if (m_Selector != null)
                m_SelectorRest = m_Selector.localRotation;
            ApplySelector();
            if (m_Tracer != null)
                m_Tracer.enabled = false;

            if (m_ShotSound == null) m_ShotSound = TempSound.Clack("Temp_Shot", 0.25f, 120f, 18f, 0.3f, 1f, 10);
            if (m_SelectorSound == null) m_SelectorSound = TempSound.Clack("Temp_Selector", 0.03f, 3000f, 200f, 0.5f, 0.5f, 12);
            if (m_HandleBackSound == null) m_HandleBackSound = TempSound.Clack("Temp_HandleBack", 0.12f, 600f, 35f, 0.3f, 0.7f, 13);
            if (m_HandleForwardSound == null) m_HandleForwardSound = TempSound.Clack("Temp_HandleForward", 0.09f, 900f, 60f, 0.4f, 0.9f, 14);
        }

        void OnDestroy()
        {
            if (m_Grab != null)
            {
                m_Grab.selectEntered.RemoveListener(OnSelectEntered);
                m_Grab.selectExited.RemoveListener(OnSelectExited);
            }
            if (m_ChargingHandle != null)
            {
                m_ChargingHandle.PulledBack -= OnHandlePulledBack;
                m_ChargingHandle.ReturnedForward -= OnHandleReturned;
            }
        }

        void OnEnable()
        {
            // 양손 버튼을 다 듣고, 누른 손이 손잡이를 잡은 손일 때만 반응한다 (시뮬레이터: 1 = A/X, 2 = B/Y)
            m_SelectorButton = new InputAction("Rifle Selector", InputActionType.Button, "<XRController>{LeftHand}/{SecondaryButton}");
            m_SelectorButton.AddBinding("<XRController>{RightHand}/{SecondaryButton}");
            m_SelectorButton.performed += OnSelectorButton;
            m_SelectorButton.Enable();
            m_MagazineButton = new InputAction("Rifle Magazine Release", InputActionType.Button, "<XRController>{LeftHand}/{PrimaryButton}");
            m_MagazineButton.AddBinding("<XRController>{RightHand}/{PrimaryButton}");
            m_MagazineButton.performed += OnMagazineButton;
            m_MagazineButton.Enable();
        }

        void OnDisable()
        {
            m_SelectorButton?.Dispose();
            m_SelectorButton = null;
            m_MagazineButton?.Dispose();
            m_MagazineButton = null;
            ResumeJumps();
            ReleaseTrigger();
        }

        // 총을 들면 그 사람(리그)의 점프를 끈다 — 손잡이 손의 A 가 탄창멈치라 점프와 겹친다
        void OnSelectEntered(SelectEnterEventArgs args)
        {
            XROrigin rig = args.interactorObject.transform.GetComponentInParent<XROrigin>();
            if (rig == null)
                return;
            foreach (var jump in rig.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.Jump.JumpProvider>())
            {
                if (jump.enabled && !m_PausedJumps.Contains(jump))
                {
                    jump.enabled = false;
                    m_PausedJumps.Add(jump);
                }
            }
        }

        void ResumeJumps()
        {
            foreach (var jump in m_PausedJumps)
            {
                if (jump != null)
                    jump.enabled = true;
            }
            m_PausedJumps.Clear();
        }

        void OnSelectExited(SelectExitEventArgs args)
        {
            // 손을 놓으면 방아쇠도 놓은 것으로 — 연발이 혼자 계속 나가지 않게
            m_InputPulled = false;
            ReleaseTrigger();
            if (!m_Grab.isSelected)
                ResumeJumps();
        }

        // 손잡이를 잡은 손(먼저 잡은 손)의 트리거를 당긴 정도 (0~1). 아무도 안 들었으면 -1
        float TriggerPull()
        {
            if (m_Grab.interactorsSelecting.Count > 0 && m_Grab.interactorsSelecting[0] is XRBaseInputInteractor hand)
                return hand.activateInput.ReadValue();
            return -1f;
        }

        // 방아쇠 당기기 — 손(트리거 값, Update)과 시험이 같은 길을 쓴다
        public override void PullTrigger()
        {
            if (m_TriggerHeld)
                return;
            m_TriggerHeld = true;
            if (m_FireMode == FireMode.Safe)
            {
                // 안전: 방아쇠가 당겨지지 않는다
                TriggerBlocked?.Invoke();
                return;
            }
            if (m_FireMode == FireMode.Burst)
                m_BurstLeft = m_BurstCount;
            TryShoot();
        }

        public override void ReleaseTrigger()
        {
            m_TriggerHeld = false;
            m_BurstLeft = 0;
        }

        // 탄창멈치 — 꽂힌 탄창을 떨어뜨린다. 약실의 탄은 그대로 남는다 (손 버튼과 시험이 같은 길을 쓴다)
        public void ReleaseMagazine()
        {
            if (Well == null || !Well.HasMagazine)
                return;
            Well.Drop();
            Buzz(0.3f, 0.03f);
        }

        // 조정간 한 칸 — 안전 → 단발 → (점사) → 연발 → 안전. K2C1 은 점사가 없다 (m_HasBurst)
        public void CycleFireMode()
        {
            var next = (FireMode)(((int)m_FireMode + 1) % 4);
            if (next == FireMode.Burst && !m_HasBurst)
                next = FireMode.Auto;
            SetFireMode(next);
        }

        public override void SetFireMode(FireMode mode)
        {
            m_FireMode = mode;
            ReleaseTrigger();
            Play(m_SelectorSound);
            Buzz(0.2f, 0.03f);
            Debug.Log($"[Rifle] 조정간: {RifleTexts.ModeName(m_FireMode)}");
            ApplySelector();
            FireModeChanged?.Invoke(m_FireMode);
        }

        // 처음 상태로 — 손에서 놓게 하고 탄창(빼서 없앰)·약실·공이치기·조정간·장전 손잡이를 되돌린다 (기능 점검·사로를 다시 할 때)
        public override void ResetState()
        {
            if (m_Grab.isSelected && m_Grab.interactionManager != null)
                m_Grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)m_Grab);
            ReleaseTrigger();
            if (Well != null)
                Well.Clear();
            m_Chambered = false;
            m_HammerCocked = false;
            m_FireMode = FireMode.Safe;
            ApplySelector();
            if (m_ChargingHandle != null)
                m_ChargingHandle.ResetForward();
        }

        // 사로 탄창 놓기 — 이 총에 맞는 탄창(C_Magazine)은 탄창 삽입구가 만든다
        public override bool ResetMagazines(Vector3 position, Quaternion rotation, int rounds)
        {
            if (Well == null)
                return false;
            Well.ResetMagazines(position, rotation, rounds);
            return true;
        }

        // 시험용 — 놓인 탄창을 하나 꽂는다 · 뺀다 · 장전 손잡이를 끝까지 당겼다 놓는다
        public override void TestInsertMagazine() => Well?.TestInsert();

        public override void TestRemoveMagazine() => Well?.TestRemove();

        public override void TestCycleChargingHandle()
        {
            if (m_ChargingHandle == null)
                return;
            m_ChargingHandle.SetOffset(m_ChargingHandle.Stroke);
            m_ChargingHandle.SetOffset(0f);
        }

        // 조정간 부품을 지금 위치의 각도로 돌린다 — 앞쪽 끝(+Z)이 위(+Y)로 가는 쪽이 + (Unity 의 X축 회전은 반대 부호)
        void ApplySelector()
        {
            if (m_Selector == null || m_SelectorAngles == null || (int)m_FireMode >= m_SelectorAngles.Length)
                return;
            m_Selector.localRotation = m_SelectorRest * Quaternion.AngleAxis(-m_SelectorAngles[(int)m_FireMode], Vector3.right);
        }

        void OnHandlePulledBack()
        {
            // 노리쇠가 뒤로 가며 공이치기를 젖히고, 약실에 있던 탄을 빼낸다
            m_HammerCocked = true;
            if (m_Chambered)
            {
                m_Chambered = false;
                Debug.Log("[Rifle] 약실의 탄 배출");
            }
            Play(m_HandleBackSound);
            ChargingHandlePulled?.Invoke();
        }

        void OnHandleReturned()
        {
            // 노리쇠가 앞으로 닫히며 꽂힌 탄창 맨 위 탄을 약실에 넣는다
            if (!m_Chambered && Well != null && Well.TakeRound())
                m_Chambered = true;
            Play(m_HandleForwardSound);
            Buzz(0.3f, 0.04f);
            ChargingHandleReturned?.Invoke();
        }

        void Update()
        {
            // 방아쇠 — 트리거를 m_FireAt 까지 당기면 당긴 것, m_ResetAt 아래로 풀면 놓은 것 (둘 사이를 두어 경계에서 떨지 않게)
            float pull = TriggerPull();
            if (pull >= 0f)
            {
                if (!m_InputPulled && pull >= m_FireAt)
                {
                    m_InputPulled = true;
                    PullTrigger();
                }
                else if (m_InputPulled && pull <= m_ResetAt)
                {
                    m_InputPulled = false;
                    ReleaseTrigger();
                }
            }

            // 점사·연발: 방아쇠를 당기고 있는 동안 발사 간격마다 다음 탄이 나간다
            if (m_TriggerHeld && (m_FireMode == FireMode.Auto || (m_FireMode == FireMode.Burst && m_BurstLeft > 0)))
                TryShoot();

            if (m_Tracer != null && m_Tracer.enabled && Time.time >= m_TracerOffTime)
                m_Tracer.enabled = false;
        }

        void TryShoot()
        {
            if (Time.time < m_NextShotTime)
                return;

            // 공이치기가 떨어져 있으면 방아쇠만 헐겁게 당겨진다
            if (!m_HammerCocked)
            {
                ReleaseTrigger();
                HammerDown?.Invoke();
                return;
            }

            if (!m_Chambered)
            {
                DryFire();
                return;
            }

            // 한 발 — 노리쇠가 저절로 오가며 공이치기를 다시 젖히고 다음 탄을 넣는다
            m_Chambered = Well != null && Well.TakeRound();
            m_ShotsFired++;
            // 다음 탄 시각 — 이어서 쏘는 중이면 앞 탄의 예정 시각에서 이어 센다 (지금 시각부터 세면 프레임만큼씩 밀려 느려진다)
            float interval = 60f / m_RoundsPerMinute;
            m_NextShotTime = Time.time - m_NextShotTime < interval ? m_NextShotTime + interval : Time.time + interval;
            if (m_FireMode == FireMode.Burst)
                m_BurstLeft--;
            Shoot();
        }

        // 빈 격발: 공이치기만 떨어진다 — 연발이어도 당길 때마다 한 번만
        void DryFire()
        {
            m_HammerCocked = false;
            ReleaseTrigger();
            // 소리 없이 손에 약한 진동만 — 사용자 11/17: 빈 격발 소리는 A · C 모두 뺀다 (C TempRifle 과 같게)
            Buzz(0.15f, 0.02f);
            Debug.Log("[Rifle] 빈 격발");
            DryFired?.Invoke();
        }

        // 한 발. 총알을 날리지 않고 쏜 순간 광선으로 바로 판정한다(히트스캔).
        // 영점·탄도(RifleBallistics)가 있으면 탄은 총구에서 영점 맞춘 총열 방향으로 나가 중력을 받는다 — 휜 길을 짧은 광선 여러 개로 본다
        void Shoot()
        {
            if (m_Muzzle == null)
                return;

            Vector3 origin = m_Muzzle.position;
            Vector3 dir = m_Muzzle.forward;
            bool ballistic = m_Ballistics != null && m_Ballistics.TryGetShotLine(out origin, out dir);
            // 조준 확대 모드 등이 조준선을 주면 그 조준선으로 판정한다 (탄도선은 보기 좋게 총구에서 그린다)
            Vector3 castOrigin = origin;
            if (m_ShotLine is IShotLine line && line.TryGetShotLine(out Vector3 lineOrigin, out Vector3 lineDir))
            {
                if (ballistic && m_Ballistics.TryGetSightLine(out Vector3 rear, out Vector3 sight))
                {
                    // 총을 그 조준선으로 돌려 놓은 것처럼 총구·총열을 가늠자 기준으로 같은 만큼 돌린다 — 25m 에서 가늠쇠 끝 = 탄착
                    Quaternion turn = Quaternion.FromToRotation(sight, lineDir);
                    origin = castOrigin = rear + turn * (origin - rear);
                    dir = turn * dir;
                }
                else
                {
                    castOrigin = lineOrigin;
                    dir = lineDir;
                }
            }
            Vector3 end = origin + dir * m_Range;
            m_Path.Clear();

            // 총구가 벽 속에 있으면(또는 손을 벽 너머로 넣었으면) 그 벽 표면에 맞는다 — 총구에서 쏘면 벽 너머가 맞는다
            bool blocked = BarrelBlocked(out RaycastHit hit);
            Vector3 hitDir = dir;
            bool struck = blocked;
            bool handledByA = false;
            if (!blocked)
                struck = ballistic
                    ? m_Ballistics.Fly(castOrigin, dir, m_Range, m_Cast, out hit, out hitDir, m_Path)
                    : CastSkippingSelf(castOrigin, dir, m_Range, out hit);
            if (ballistic && !struck && m_Path.Count > 0)
                end = m_Path[m_Path.Count - 1];
            if (struck)
            {
                end = hit.point;
                // 맞은 콜라이더가 자식이어도 찾도록 부모 쪽까지 본다
                IHittable target = hit.collider.GetComponentInParent<IHittable>();
                handledByA = target != null;
                if (target != null)
                {
                    Debug.Log($"[Rifle] 명중: {hit.collider.name} ({hit.distance:0.0}m)");
                    target.OnHit(hit, hitDir);
                }
                else   // 11/10: 표적 밖에 맞은 탄도 어디인지 남긴다 — 사로 2조가 헤드셋에서만 0발인 문제를 찾으려고
                    Debug.Log($"[Rifle] 탄: {PathOf(hit.collider.transform)} ({hit.distance:0.0}m{(blocked ? ", 총구 막힘" : "")}) · 시작 {castOrigin} · 방향 {dir} · 든 사람 {Holder() != null}");
                HitSurface?.Invoke(hit);
            }
            else
                Debug.Log($"[Rifle] 탄: 아무 데도 안 맞음 · 시작 {castOrigin} · 방향 {dir}");

            // 막혔을 땐 탄도선·총구 불빛을 안 낸다 — 벽 속 총구에서 나오면 벽 건너편이 번쩍인다
            if (!blocked)
            {
                ShowTracer(origin, end);
                if (m_MuzzleFlash != null)
                    m_MuzzleFlash.Play();
            }
            if (m_Recoil != null)
                m_Recoil.Kick(TwoHanded);
            Play(m_ShotSound);
            // VR 에서 "쐈다"는 느낌의 절반은 손에서 온다
            Buzz(0.6f, 0.05f);
            ShotReported?.Invoke(new ShotReport { Origin = castOrigin, End = end, Direction = hitDir, Blocked = blocked, Struck = struck, Hit = hit, HandledByA = handledByA });
            Fired?.Invoke();
        }

        // 머리 → 손잡이 → 총구 순서로 막힌 데가 있는지. 머리는 총을 든 사람(XR Origin)의 카메라 — 아무도 안 들었으면 손잡이부터.
        // 총을 걸치는 면(RifleRestSurface — 사로 책상)은 막힘으로 치지 않는다 (11/10: 책상에 걸치면 손잡이가 상판 아래로 내려가 탄이 안 나갔다)
        bool BarrelBlocked(out RaycastHit block)
        {
            Vector3 breech = m_Breech != null ? m_Breech.position
                : m_Grab.attachTransform != null ? m_Grab.attachTransform.position : transform.position;
            XROrigin holder = Holder();
            if (holder != null && holder.Camera != null && CastBetween(holder.Camera.transform.position, breech, out block))
                return true;
            return CastBetween(breech, m_Muzzle.position, out block);
        }

        bool CastBetween(Vector3 from, Vector3 to, out RaycastHit hit)
        {
            Vector3 d = to - from;
            float length = d.magnitude;
            hit = default;
            return length > 0.001f && CastSkippingSelf(from, d / length, length, out hit, skipRestSurfaces: true);
        }

        // 처음 맞는 곳 — 총 자신과 총을 든 사람(리그·손)의 콜라이더는 건너뛰고 그 뒤에서 다시 본다. skipRestSurfaces 면 총을 걸치는 면도 건너뛴다
        bool CastSkippingSelf(Vector3 from, Vector3 dir, float distance, out RaycastHit hit, bool skipRestSurfaces = false)
        {
            XROrigin origin = Holder();
            Transform holder = origin != null ? origin.transform : null;
            for (int i = 0; i < 6 && distance > 0f; i++)   // 총 · 손 · 책상 몸통 · 상판을 건너뛸 수 있게
            {
                if (!Physics.Raycast(from, dir, out hit, distance, m_HitMask, QueryTriggerInteraction.Ignore))
                    return false;
                Transform t = hit.collider.transform;
                bool rest = skipRestSurfaces && hit.collider.GetComponentInParent<RifleRestSurface>() != null;
                if (!rest && !t.IsChildOf(transform) && (holder == null || !t.IsChildOf(holder)))
                    return true;
                // 시작점이 콜라이더 안이면 광선이 그 콜라이더를 못 본다 → 자기 몸 바로 안쪽에서 다시 쏘면 그 뒤만 본다
                float step = hit.distance + 0.01f;
                from += dir * step;
                distance -= step;
            }
            hit = default;
            return false;
        }

        static string PathOf(Transform t)
        {
            string path = t.name;
            for (Transform p = t.parent; p != null; p = p.parent)
                path = p.name + "/" + path;
            return path;
        }

        XROrigin Holder()
        {
            return m_Grab.interactorsSelecting.Count > 0 ? m_Grab.interactorsSelecting[0].transform.GetComponentInParent<XROrigin>() : null;
        }

        void ShowTracer(Vector3 from, Vector3 to)
        {
            if (m_Tracer == null)
                return;
            // 휜 탄도면 지난 점들을 잇는다
            if (m_Path.Count >= 2)
            {
                m_Tracer.positionCount = m_Path.Count;
                for (int i = 0; i < m_Path.Count; i++)
                    m_Tracer.SetPosition(i, m_Path[i]);
            }
            else
            {
                m_Tracer.positionCount = 2;
                m_Tracer.SetPosition(0, from);
                m_Tracer.SetPosition(1, to);
            }
            m_Tracer.enabled = true;
            m_TracerOffTime = Time.time + m_TracerSeconds;
        }

        void Play(AudioClip clip)
        {
            if (m_Audio != null && clip != null)
                m_Audio.PlayOneShot(clip);
        }

        // 손잡이 손엔 그대로, 받친 손엔 절반 세기로 진동
        void Buzz(float amplitude, float duration)
        {
            for (int i = 0; i < m_Grab.interactorsSelecting.Count; i++)
            {
                if (m_Grab.interactorsSelecting[i] is XRBaseInputInteractor hand)
                    hand.SendHapticImpulse(i == 0 ? amplitude : amplitude * 0.5f, duration);
            }
        }

        void OnMagazineButton(InputAction.CallbackContext context)
        {
            if (IsGripHandDevice(context.control.device))
                ReleaseMagazine();
        }

        void OnSelectorButton(InputAction.CallbackContext context)
        {
            if (IsGripHandDevice(context.control.device))
                CycleFireMode();
        }

        // 버튼을 누른 컨트롤러가 손잡이를 잡은 손인지 — 총을 안 들고 누른 버튼, 앞손 버튼은 무시한다
        bool IsGripHandDevice(InputDevice device)
        {
            if (m_Grab.interactorsSelecting.Count == 0)
                return false;
            InteractorHandedness hand = m_Grab.interactorsSelecting[0].handedness;
            if (hand == InteractorHandedness.Left)
                return device.usages.Contains(UnityEngine.InputSystem.CommonUsages.LeftHand);
            if (hand == InteractorHandedness.Right)
                return device.usages.Contains(UnityEngine.InputSystem.CommonUsages.RightHand);
            return false;
        }
    }
}
