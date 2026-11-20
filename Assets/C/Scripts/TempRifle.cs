using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MilTrainSimVR.UrbanCombat
{
    // 플레이어 소총의 쏘기 — 처음엔 A 의 K2 모델이 오기 전 상자 총(C_TempRifle)용으로 만들었고, 10/26 부터 K2C1(C_K2C1, A 프리팹의 Variant)에도 그대로 쓴다.
    // 잡은 손의 트리거를 당긴 정도(0~1)를 직접 읽어, m_FireAt 까지 당기면 방아쇠를 당기고(PullTrigger) m_ResetAt 아래로 풀면 놓는다(ReleaseTrigger).
    // 그 전까지는 손 모양의 검지가 방아쇠 쪽으로 다가가기만 한다(RifleHands, 10/28). 방아쇠 부품은 없다(A: 격발 = 컨트롤러 입력).
    // 실제로 탄이 나갈지는 조정간(안전·단발·점사·연발)과 약실에 탄이 있는지로 정한다.
    // 탄 (10/28): 약실 한 발 + 꽂힌 탄창(MagazineWell). 쏘면 탄창에서 다음 탄이 약실로 들어온다.
    //   탄창이 비어 약실까지 비면 새 탄창을 끼우고 장전 손잡이(RifleChargingHandle)를 당겨야 다시 쏜다. 탄이 남은 채 바꾸면 바로 쏜다.
    //   삽입구가 없는 총(C_TempRifle)은 탄이 떨어지지 않는다.
    //   한 발마다 장전 손잡이(노리쇠에 붙어 있음)가 뒤 끝까지 갔다 앞으로 닫히고, 안쪽 노리쇠 뭉치도 1 cm 오간다 — RifleChargingHandle.CycleBolt (11/17)
    // 버튼은 손잡이를 잡은 손 것만 듣는다: A/X = 탄창멈치(꽂힌 탄창을 떨어뜨림), B/Y = 조정간.
    // VR 에선 총구나 손이 벽을 뚫고 들어갈 수 있다 → 쏠 때 머리→손잡이→총구 길이 막혔으면 막힌 곳에 맞힌다 (벽 너머 사격 방지).
    // 탄 방향 (10/29): 영점·탄도(RifleBallistics)가 있으면 탄은 총구에서 '영점 맞춘 총열' 방향으로 나가 중력을 받는다 — 가늠자·가늠쇠로 겨눈 곳에 맞는다(25m 영점).
    //   없으면 전처럼 총구 앞으로 곧게 (상자 총 C_TempRifle)
    // 탄이 0발(약실이 빔)일 때 방아쇠를 당기면 소리는 나지 않고 손에 약한 진동만 온다 (사용자 11/17 — 빈 격발 소리 · 공이치기는 넣지 않는다).
    //   A 사로가 C 총을 쓰도록 이벤트를 연다 (11/17): Fired · DryFired(약실이 빈 채 당김) · TriggerBlocked(안전에서 당김) · FireModeChanged ·
    //   ChargingHandlePulled · ChargingHandleReturned — A 의 RangeRifle 과 같은 이름
    [RequireComponent(typeof(XRGrabInteractable))]
    public class TempRifle : MonoBehaviour
    {
        public enum FireMode { Safe, Semi, Burst, Auto }   // 안전 · 단발 · 점사 · 연발

        [Tooltip("총구. 영점·탄도(아래)가 없으면 이 오브젝트의 앞(+Z) 방향으로 곧게 쏜다")]
        [SerializeField] Transform m_Muzzle;

        [Tooltip("영점·탄도 — 있으면 탄이 총구에서 가늠자·가늠쇠 조준선과 영점 거리에서 만나는 방향으로 나가 중력을 받는다(K2C1). 비우면 총구 앞으로 곧게")]
        [SerializeField] RifleBallistics m_Ballistics;

        [Tooltip("총몸 기준점 — 머리 → 이 점 → 총구 사이가 막혔으면 막힌 곳에 맞는다. 비우면 잡는 손잡이 기준점")]
        [SerializeField] Transform m_Breech;

        [Tooltip("사거리(m)")]
        [SerializeField] float m_Range = 100f;

        [Tooltip("맞을 수 있는 레이어. 지금은 전부")]
        [SerializeField] LayerMask m_HitMask = ~0;

        [Tooltip("탄도를 잠깐 보여 주는 선 — 헤드셋 없이 시험할 때 어디로 쐈는지 보려고")]
        [SerializeField] LineRenderer m_Tracer;

        [SerializeField] float m_TracerSeconds = 0.06f;

        [Header("탄 · 조정간")]
        [Tooltip("탄창 삽입구 — 비우면 탄이 떨어지지 않는다 (C_TempRifle)")]
        [SerializeField] MagazineWell m_Well;

        [Tooltip("장전 손잡이 — 약실이 비었을 때 당겨서 탄창의 탄을 약실로 넣는다")]
        [SerializeField] RifleChargingHandle m_ChargingHandle;

        [Tooltip("탄창 하나에 든 탄 수 — 사격장 판에 보여 줄 때 쓴다")]
        [SerializeField] int m_MagazineSize = 30;

        [Tooltip("트리거를 이만큼(0~1) 당기면 쏜다 — 그 전까지는 검지가 방아쇠로 다가가기만 한다")]
        [SerializeField] float m_FireAt = 0.9f;

        [Tooltip("쏜 뒤 트리거를 이만큼 아래로 풀어야 놓은 것 — 단발은 그래야 다음 탄이 나간다")]
        [SerializeField] float m_ResetAt = 0.6f;

        [Tooltip("처음 조정간 위치")]
        [SerializeField] FireMode m_FireMode = FireMode.Semi;

        [Tooltip("분당 발사 수 — 점사·연발에서 다음 탄까지의 간격")]
        [SerializeField] float m_RoundsPerMinute = 750f;

        [Tooltip("점사 한 번에 나가는 탄 수")]
        [SerializeField] int m_BurstCount = 3;

        [Tooltip("조정간 부품 (K2C1 의 Part_Selector) — 조정간을 바꾸면 자기 X축(총 좌우)으로 돌린다. 비우면 돌리지 않는다 (상자 총 C_TempRifle)")]
        [SerializeField] Transform m_Selector;

        [Tooltip("조정간 각도(°) — 안전·단발·점사·연발 순서. 모델 자세 = 안전(0°), +는 앞쪽 끝이 위로 가는 방향. " +
                 "A 의 K2C1Rifle 과 같은 값: 왼쪽 면을 총구가 왼쪽에 오게 볼 때 9시 안전 · 12시 단발 · 3시 연발 (A 10/27). 점사 135° 는 A 의 임시값")]
        [SerializeField] float[] m_SelectorAngles = { 0f, 90f, 135f, 180f };

        [Header("효과")]
        [SerializeField] MuzzleFlash m_MuzzleFlash;

        [Tooltip("반동 — XRGrabInteractable 의 잡기 변환기 목록에도 들어 있어야 움직인다")]
        [SerializeField] RifleRecoil m_Recoil;

        // 사격음 · 탄피는 여기서 내지 않는다 — 리그의 CombatAudio 가 Gunfire.ShotFired 를 듣고 쏜 자리 · 실내외에 맞춰 낸다 (11/16)
        [Header("조작 소리 — 파일을 넣으면 난다 (총에 붙은 3D 소리)")]
        [SerializeField] AudioSource m_Audio;

        [Tooltip("약실이 비었는데 방아쇠를 당겼을 때 — 비워 두면 소리 없음 (지금 비어 있음, 사용자 11/17: 0발에서 소리 안 나게)")]
        [SerializeField] AudioClip m_EmptySound;

        [Tooltip("탄창을 끼울 때 — 여러 개면 번갈아")]
        [SerializeField] AudioClip[] m_MagazineInSounds;

        [Tooltip("탄창을 뺄 때 (탄창멈치 · 손으로 뽑기)")]
        [SerializeField] AudioClip m_MagazineOutSound;

        [Tooltip("장전 손잡이를 끝까지 당겼을 때")]
        [SerializeField] AudioClip m_ChargeBackSound;

        [Tooltip("장전 손잡이가 앞으로 돌아갈 때")]
        [SerializeField] AudioClip m_ChargeSound;

        [SerializeField] AudioClip m_SelectorSound;

        [Header("개머리판으로 유리 깨기")]
        [Tooltip("개머리판 받침판 가운데 — 총 기준 자리(m). K2C1 모델의 뒤 끝: z -0.25, 높이 0 ~ 0.10")]
        [SerializeField] Vector3 m_StockButt = new Vector3(0f, 0.05f, -0.25f);

        [Tooltip("받침판 높이의 절반(m) — 위 · 가운데 · 아래 세 점이 지나간 길로 본다")]
        [SerializeField] float m_StockButtHalfHeight = 0.045f;

        [Tooltip("이 빠르기(m/s)보다 세게 쳐야 깨진다. 몸(리그) 기준 빠르기 — 걷기 · 끊어 돌기 · 순간이동으로 총이 움직인 건 친 게 아니다")]
        [SerializeField] float m_StockStrikeSpeed = 1.2f;

        XRGrabInteractable m_Grab;
        RifleHands m_Hands;
        XROrigin m_StockHolder;
        readonly Vector3[] m_StockLast = new Vector3[3];
        Vector3 m_StockLastInRig;
        float m_TracerOffTime;
        RifleBallistics.SegmentCast m_Cast;                     // 탄도가 마디마다 쓰는 광선 = CastSkippingSelf (쏠 때마다 새로 만들지 않게)
        readonly List<Vector3> m_Path = new List<Vector3>();    // 방금 탄이 지난 점들 — 탄도선

        bool m_Chambered;
        int m_ShotsFired;
        bool m_TriggerHeld;
        bool m_InputPulled;   // 손의 트리거로 당겼는지 — 테스트가 PullTrigger 로 당긴 건 손을 풀어도 놓지 않는다
        int m_BurstLeft;
        int m_MagazineInCount;
        float m_NextShotTime;
        Quaternion m_SelectorRest;   // 조정간 부품의 모델 자세 = 안전

        InputAction m_MagazineButton;
        InputAction m_SelectorButton;

        // 총알이 어딘가에 맞을 때마다 — 탄착 효과(ImpactEffects)가 듣는다. 총은 어떤 효과가 나는지 몰라도 된다
        public event System.Action<RaycastHit> HitSurface;

        // 사로(A)가 듣는 것 — A 의 RangeRifle 과 같은 이름 · 같은 뜻 (11/17)
        public event System.Action Fired;                    // 실탄 한 발 (명중 처리가 끝난 뒤)
        public event System.Action DryFired;                 // 약실이 빈 채 당김 (소리 없음)
        public event System.Action TriggerBlocked;           // 안전에서 방아쇠를 당김 — 당겨지지 않는다
        public event System.Action<FireMode> FireModeChanged;
        public event System.Action ChargingHandlePulled;     // 장전 손잡이를 끝까지 당김
        public event System.Action ChargingHandleReturned;   // 장전 손잡이가 앞으로 돌아옴

        // 남은 탄 = 약실 + 꽂힌 탄창 — 사격장 판이 보여 준다
        public int Rounds => (m_Chambered ? 1 : 0) + (m_Well != null ? m_Well.Rounds : 0);
        public int MagazineSize => m_MagazineSize;
        public FireMode Mode => m_FireMode;
        public float FireAt => m_FireAt;
        public bool Chambered => m_Chambered;
        public bool HasMagazine => m_Well == null || m_Well.HasMagazine;

        // 지금까지 실제로 나간 탄 수 — 사격장 판이 명중률을 계산할 때 쓴다
        public int ShotsFired => m_ShotsFired;

        public static string ModeName(FireMode mode) => mode switch
        {
            FireMode.Safe => "안전",
            FireMode.Semi => "단발",
            FireMode.Burst => "점사",
            _ => "연발",
        };

        void Awake()
        {
            m_Grab = GetComponent<XRGrabInteractable>();
            m_Hands = GetComponent<RifleHands>();
            m_Cast = CastSkippingSelf;
            m_Grab.selectExited.AddListener(OnSelectExited);
            if (m_Well != null)
            {
                m_Well.Inserted += OnMagazineInserted;
                m_Well.Removed += OnMagazineRemoved;
            }
            if (m_ChargingHandle != null)
            {
                m_ChargingHandle.PulledBack += OnHandlePulledBack;
                m_ChargingHandle.ReturnedForward += OnHandleReturned;
            }
            if (m_Tracer != null)
                m_Tracer.enabled = false;
            // 조정간 부품을 처음 위치(기본 단발)로 — 모델은 안전 자세로 놓여 있다
            if (m_Selector != null)
                m_SelectorRest = m_Selector.localRotation;
            ApplySelector();
        }

        void Start()
        {
            // 장전된 채로 시작한다 — 꽂힌 탄창에서 한 발을 약실로 (탄창 29 + 약실 1).
            // Awake 가 아니라 여기서: 삽입구(MagazineWell)가 Awake 에서 탄창을 채운 뒤여야 한다
            m_Chambered = m_Well == null || m_Well.TakeRound();
        }

        void OnDestroy()
        {
            if (m_Grab != null)
                m_Grab.selectExited.RemoveListener(OnSelectExited);
            if (m_Well != null)
            {
                m_Well.Inserted -= OnMagazineInserted;
                m_Well.Removed -= OnMagazineRemoved;
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
            m_MagazineButton = new InputAction("Rifle Magazine Release", InputActionType.Button, "<XRController>{LeftHand}/{PrimaryButton}");
            m_MagazineButton.AddBinding("<XRController>{RightHand}/{PrimaryButton}");
            m_SelectorButton = new InputAction("Rifle Selector", InputActionType.Button, "<XRController>{LeftHand}/{SecondaryButton}");
            m_SelectorButton.AddBinding("<XRController>{RightHand}/{SecondaryButton}");

            m_MagazineButton.performed += OnMagazineButton;
            m_SelectorButton.performed += OnSelectorButton;
            m_MagazineButton.Enable();
            m_SelectorButton.Enable();
        }

        void OnDisable()
        {
            m_MagazineButton?.Dispose();
            m_SelectorButton?.Dispose();
            ReleaseTrigger();
        }

        void OnSelectExited(SelectExitEventArgs args)
        {
            // 손을 놓으면 방아쇠도 놓은 것으로 — 연발이 혼자 계속 나가지 않게
            m_InputPulled = false;
            ReleaseTrigger();
        }

        // 손잡이를 잡은 손의 트리거를 당긴 정도 (0~1). 아무도 안 들었으면 -1
        float TriggerPull()
        {
            if (m_Grab.interactorsSelecting.Count > 0 && m_Grab.interactorsSelecting[0] is XRBaseInputInteractor hand)
                return hand.activateInput.ReadValue();
            return -1f;
        }

        // 방아쇠 당기기 — 손(트리거 값, Update)과 테스트(Unity CLI)가 같은 길을 쓴다
        public void PullTrigger()
        {
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

        public void ReleaseTrigger()
        {
            m_TriggerHeld = false;
            m_BurstLeft = 0;
        }

        // 조정간 한 칸 — 안전 → 단발 → 점사 → 연발 → 안전
        public void CycleFireMode()
        {
            SetFireMode((FireMode)(((int)m_FireMode + 1) % 4));
        }

        // 조정간을 바로 그 위치로 — 소리 · 진동 한 번 (A 사로가 원하는 위치로 돌릴 때. CycleFireMode 를 여러 번 부르면 소리가 겹친다)
        public void SetFireMode(FireMode mode)
        {
            m_FireMode = mode;
            ReleaseTrigger();
            ApplySelector();
            Play(m_SelectorSound);
            Buzz(0.2f, 0.03f);
            Debug.Log($"[C] 조정간: {ModeName(m_FireMode)}");
            FireModeChanged?.Invoke(m_FireMode);
        }

        // 처음 상태로 — 손에서 놓게 하고 탄창(떨어뜨리지 않고 없앰) · 약실을 비우고 조정간을 안전으로 (소리 없음).
        // A 사로의 "처음으로 · 다시 하기"용 — A K2C1Rifle.ResetState 와 같은 뜻 (장전 손잡이는 손을 놓으면 저절로 앞으로 간다)
        public void ResetState()
        {
            if (m_Grab.isSelected && m_Grab.interactionManager != null)
                m_Grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)m_Grab);
            m_InputPulled = false;
            ReleaseTrigger();
            if (m_Well != null)
                m_Well.Clear();
            m_Chambered = false;
            m_FireMode = FireMode.Safe;
            ApplySelector();
            FireModeChanged?.Invoke(m_FireMode);
        }

        // 시험용 (손 없이) — 장전 손잡이를 끝까지 당겼다 놓은 것과 같다
        public void TestCycleChargingHandle()
        {
            OnHandlePulledBack();
            OnHandleReturned();
        }

        // 조정간 부품을 지금 위치의 각도로 돌린다 — A 의 K2C1Rifle.ApplySelector 와 같은 식.
        // 앞쪽 끝(+Z)이 위(+Y)로 가는 쪽이 + (Unity 의 X축 회전은 반대 부호). 회전 중심 = Part_Selector 원점(레버판 가운데, A 10/27)
        void ApplySelector()
        {
            if (m_Selector == null || m_SelectorAngles == null || (int)m_FireMode >= m_SelectorAngles.Length)
                return;
            m_Selector.localRotation = m_SelectorRest * Quaternion.AngleAxis(-m_SelectorAngles[(int)m_FireMode], Vector3.right);
        }

        // 탄창멈치 — 꽂힌 탄창을 떨어뜨린다. 약실의 탄은 그대로 남는다 (손 버튼과 테스트(Unity CLI)가 같은 길을 쓴다)
        public void ReleaseMagazine()
        {
            if (m_Well != null)
                m_Well.Drop();
        }

        void OnMagazineInserted()
        {
            if (m_MagazineInSounds != null && m_MagazineInSounds.Length > 0)
                Play(m_MagazineInSounds[m_MagazineInCount++ % m_MagazineInSounds.Length]);
            Buzz(0.3f, 0.03f);
        }

        void OnMagazineRemoved()
        {
            Play(m_MagazineOutSound);
            Buzz(0.3f, 0.03f);
        }

        // 장전 손잡이를 끝까지 당김 — 약실에 탄이 있었으면 배출구로 빠져나간다 (그 탄은 버려진다, 바닥에 떨어지는 소리)
        void OnHandlePulledBack()
        {
            ReleaseTrigger();
            Play(m_ChargeBackSound);
            if (m_Chambered)
            {
                m_Chambered = false;
                CombatAudio.PlayEjectedRound(transform);
                Debug.Log("[C] 장전 손잡이 — 약실의 탄이 빠짐");
            }
            ChargingHandlePulled?.Invoke();
        }

        // 장전 손잡이가 앞으로 돌아옴 — 노리쇠가 탄창의 탄을 약실로 밀어 넣는다
        void OnHandleReturned()
        {
            m_Chambered = m_Well == null || m_Well.TakeRound();
            Play(m_ChargeSound);
            Buzz(0.35f, 0.04f);
            Debug.Log(m_Chambered ? $"[C] 장전 (탄창 {(m_Well != null ? m_Well.Rounds : 0)}발 + 약실 1)" : "[C] 장전 손잡이 — 탄창이 비어 약실도 비었다");
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

            StockStrike();
        }

        // 개머리판으로 창 유리를 깬다 (사용자 11/18: "점프로 깨는 게 아닌, 총기 개머리판으로 깰 수 있게 — 그래야 유리창이 갑작스럽게 깨지지 않을 것 같아서").
        // 든 총의 받침판(위 · 가운데 · 아래 세 점)이 이번 프레임에 지나간 길이 창 유리를 가로지르고, 그때 빠르기가 m_StockStrikeSpeed 를 넘으면 그 판이 깨진다.
        // 빠르기는 든 사람의 리그 기준으로 잰다 — 걷기 · 끊어 돌기 · 순간이동으로 총이 유리를 지난 건 친 게 아니다 (천천히 댄 것도 안 깨진다)
        void StockStrike()
        {
            XROrigin holder = m_Grab.isSelected ? Holder() : null;
            if (holder == null)
            {
                m_StockHolder = null;
                return;
            }
            Vector3 butt = transform.TransformPoint(m_StockButt);
            Vector3 up = transform.up * m_StockButtHalfHeight;
            Vector3 inRig = holder.transform.InverseTransformPoint(butt);
            // 한 프레임에 k_StockMaxStep 넘게 튄 건 손 추적이 튄 것 — 친 게 아니다 (세게 쳐도 한 프레임에 10 cm 안팎)
            float moved = (inRig - m_StockLastInRig).magnitude;
            if (m_StockHolder == holder && Time.deltaTime > 0f && moved >= m_StockStrikeSpeed * Time.deltaTime && moved <= k_StockMaxStep)
            {
                bool broke = false;
                for (int i = 0; i < m_StockLast.Length; i++)
                    broke |= BreakableGlass.StockPath(m_StockLast[i], butt + up * (i - 1));
                if (broke)
                    Buzz(0.8f, 0.08f);
            }
            for (int i = 0; i < m_StockLast.Length; i++)
                m_StockLast[i] = butt + up * (i - 1);
            m_StockLastInRig = inRig;
            m_StockHolder = holder;
        }

        void TryShoot()
        {
            if (Time.time < m_NextShotTime)
                return;

            // 총을 든 사람이 전사했으면 쏘지 않는다 — 결과판을 보는 동안 총은 잡히고 탄창 · 조정간도 되지만 방아쇠는 안 된다 (사용자 11/17)
            if (HolderDead())
            {
                ReleaseTrigger();
                return;
            }

            // 장전 손잡이를 당기고 있는 동안은 노리쇠가 열려 있어 격발되지 않는다
            if (m_ChargingHandle != null && m_ChargingHandle.Offset > 0.01f)
            {
                ReleaseTrigger();
                return;
            }

            if (!m_Chambered)
            {
                DryFire();
                return;
            }

            // 쏘고 나면 노리쇠가 다음 탄을 탄창에서 약실로 넣는다 — 탄창이 비었거나 빠졌으면 약실이 빈 채로 남는다
            m_Chambered = m_Well == null || m_Well.TakeRound();
            m_ShotsFired++;
            // 다음 탄 시각 — 이어서 쏘는 중이면 앞 탄의 예정 시각에서 이어 센다 (지금 시각부터 세면 프레임만큼씩 밀려 느려진다)
            float interval = 60f / m_RoundsPerMinute;
            m_NextShotTime = Time.time - m_NextShotTime < interval ? m_NextShotTime + interval : Time.time + interval;
            if (m_FireMode == FireMode.Burst)
                m_BurstLeft--;
            // 노리쇠가 뒤로 갔다 앞으로 닫히는 모습 — K2 는 장전 손잡이가 노리쇠에 붙어 같이 오간다. 다음 탄 전에 끝나게 간격을 준다 (11/17)
            if (m_ChargingHandle != null)
                m_ChargingHandle.CycleBolt(interval);
            Shoot();
            Fired?.Invoke();
        }

        // 약실이 비었다 — 손에 약한 진동 한 번 (소리 칸이 비어 있어 소리는 없다). 연발이어도 당길 때마다 한 번만
        void DryFire()
        {
            ReleaseTrigger();
            Play(m_EmptySound);
            Buzz(0.15f, 0.02f);
            DryFired?.Invoke();
        }

        const float k_GlassBehindMuzzle = 0.6f;   // 유리 판정을 총구에서 이만큼 뒤부터 (총열 · 총몸 길이)
        const float k_StockMaxStep = 0.5f;        // 개머리판이 한 프레임에 이보다 많이 움직였으면 타격으로 안 본다 (추적이 튐)

        // 한 발. 총알을 날리지 않고 쏜 순간 광선으로 바로 판정한다(히트스캔) — 실내 근거리 교전엔 이걸로 충분하다.
        // 영점·탄도가 있으면 중력으로 휜 길을 짧은 광선 여러 개로 본다 (RifleBallistics)
        void Shoot()
        {
            if (m_Muzzle == null)
                return;

            Vector3 origin = m_Muzzle.position;
            Vector3 dir = m_Muzzle.forward;
            bool ballistic = m_Ballistics != null && m_Ballistics.TryGetShotLine(out origin, out dir);
            Vector3 end = origin + dir * m_Range;
            m_Path.Clear();

            // 총구가 벽 속에 있으면(또는 손을 벽 너머로 넣었으면) 그 벽 표면에 맞는다 — 총구에서 쏘면 벽 너머가 맞는다
            bool blocked = BarrelBlocked(out RaycastHit hit);
            Vector3 hitDir = dir;
            bool struck = blocked;
            if (!blocked)
                struck = ballistic
                    ? m_Ballistics.Fly(origin, dir, m_Range, m_Cast, out hit, out hitDir, m_Path)
                    : CastSkippingSelf(origin, dir, m_Range, out hit);
            if (ballistic && !struck && m_Path.Count > 0)
                end = m_Path[m_Path.Count - 1];
            if (struck)
            {
                end = hit.point;
                // 맞은 콜라이더가 자식이어도 찾도록 부모 쪽까지 본다
                IHittable target = hit.collider.GetComponentInParent<IHittable>();
                if (target != null)
                {
                    Debug.Log($"[C] 명중: {hit.collider.name} ({hit.distance:0.0}m)");
                    // 사람 몸이면 누가 맞혔는지 먼저 알린다 — 경기 기록(명중률·처치)
                    if (target is HitZone zone)
                        Gunfire.RaiseBodyHit(this, zone);
                    target.OnHit(hit, hitDir);
                }
                HitSurface?.Invoke(hit);
                Gunfire.RaiseSurfaceHit(hit);
            }
            // 총소리 — 가까운 적이 듣고 이쪽을 본다
            Gunfire.RaiseShotFired(origin, this);
            // 총알 길 — 빗나가도 옆을 스친 적은 쏜 쪽을 알고 대응사격한다 (11/16). 총구가 벽 속이면 총알이 안 나간 것으로 본다
            if (!blocked)
            {
                Gunfire.RaiseBulletPassed(origin, end, this);
                // 지나간 길의 유리(에스컬레이터 난간 · 창)를 깬다 — 총알은 유리를 지나간다 (11/18).
                // 총열 길이만큼 뒤에서부터 본다: 총구를 유리 너머로 내밀고 쏴도 그 유리가 깨지게 (안 그러면 총이 유리에 꽂힌 채 멀쩡하다)
                BreakableGlass.BulletPath(origin - dir * k_GlassBehindMuzzle, end);
            }

            // 막혔을 땐 탄도선·총구 불빛을 안 낸다 — 벽 속 총구에서 나오면 벽 건너편이 번쩍인다
            if (!blocked)
            {
                ShowTracer(origin, end);
                if (m_MuzzleFlash != null)
                    m_MuzzleFlash.Play();
            }
            if (m_Recoil != null)
                m_Recoil.Kick(m_Hands != null && m_Hands.IsSupported);
            // VR 에서 "쐈다"는 느낌의 절반은 손에서 온다
            Buzz(0.6f, 0.05f);
        }

        // 머리 → 손잡이 → 총구 순서로 막힌 데가 있는지. 머리는 총을 든 사람(XR Origin)의 카메라 — 아무도 안 들었으면 손잡이부터
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
            return length > 0.001f && CastSkippingSelf(from, d / length, length, out hit);
        }

        // 처음 맞는 곳 — 총 자신과 총을 든 사람(리그·손)의 콜라이더는 건너뛰고 그 뒤에서 다시 본다
        bool CastSkippingSelf(Vector3 from, Vector3 dir, float distance, out RaycastHit hit)
        {
            XROrigin origin = Holder();
            Transform holder = origin != null ? origin.transform : null;
            for (int i = 0; i < 4 && distance > 0f; i++)
            {
                // 맵 끝 보이지 않는 벽('Ignore Raycast' 층)은 늘 건너뛴다 — 벽 너머 배경 건물에 맞게
                if (!Physics.Raycast(from, dir, out hit, distance, m_HitMask & Gunfire.ShotMask, QueryTriggerInteraction.Ignore))
                    return false;
                Transform t = hit.collider.transform;
                if (!t.IsChildOf(transform) && (holder == null || !t.IsChildOf(holder)))
                    return true;
                // 시작점이 콜라이더 안이면 광선이 그 콜라이더를 못 본다 → 자기 몸 바로 안쪽에서 다시 쏘면 그 뒤만 본다
                float step = hit.distance + 0.01f;
                from += dir * step;
                distance -= step;
            }
            hit = default;
            return false;
        }

        XROrigin Holder()
        {
            return m_Grab.interactorsSelecting.Count > 0 ? m_Grab.interactorsSelecting[0].transform.GetComponentInParent<XROrigin>() : null;
        }

        // 총을 든 사람(리그의 몸 상태)이 전사했나 — 아무도 안 들었거나 몸 상태가 없는 리그(A 사격장 등)면 아니다
        bool HolderDead()
        {
            XROrigin holder = Holder();
            return holder != null && holder.TryGetComponent(out CombatantHealth health) && !health.IsAlive;
        }

        void ShowTracer(Vector3 from, Vector3 to)
        {
            if (m_Tracer == null)
                return;
            // 휜 탄도면 탄이 지난 점들을 다 잇는다 (눈으로는 거의 직선 — 100m 에서 12cm 휜다)
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

        // 손잡이 손엔 그대로, 받친 손엔 절반 세기로 진동 — 받친 손은 XRI 로 잡지 않으니 RifleHands 에게 묻는다
        void Buzz(float amplitude, float duration)
        {
            if (m_Grab.interactorsSelecting.Count > 0 && m_Grab.interactorsSelecting[0] is XRBaseInputInteractor hand)
                hand.SendHapticImpulse(amplitude, duration);
            if (m_Hands != null && m_Hands.SupportHand != null)
                m_Hands.SupportHand.SendHapticImpulse(amplitude * 0.5f, duration);
        }

        // 공통 일시정지 메뉴(B, 왼손 메뉴 버튼)가 떠 있는 동안에는 두 버튼을 듣지 않는다 (11/19).
        // 메뉴는 트리거(XRI 의 activate 입력)만 막아 준다 — 이 두 버튼은 총이 직접 듣는 것이라 여기서 막는다
        void OnMagazineButton(InputAction.CallbackContext context)
        {
            if (!MilTrainSimVR.B.CommonMenu.CommonPauseMenu.IsPaused && IsGripHandDevice(context.control.device))
                ReleaseMagazine();
        }

        void OnSelectorButton(InputAction.CallbackContext context)
        {
            if (!MilTrainSimVR.B.CommonMenu.CommonPauseMenu.IsPaused && IsGripHandDevice(context.control.device))
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
