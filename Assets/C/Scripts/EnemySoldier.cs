using UnityEngine;
using UnityEngine.AI;

namespace MilTrainSimVR.UrbanCombat
{
    // 적 병사의 겉모습 (11/03) — Tripo 로 만든 병사 모델 + Mixamo 애니메이션. 회색 박스 더미(EnemyDummy)를 바꿨다.
    // 보기·쏘기는 EnemyShooter, 걷기는 EnemyMover 가 그대로 맡고, 여기서는 그 상태를 보고 몸을 움직인다:
    //   · 서 있음 = 소총 든 대기. 경계하며 서 있음 = 조준 — 상체(허리 → 가슴)를 굽혀 총구가 겨눌 곳(EnemyShooter.LastKnown)을 본다
    //   · 걷기·뛰기 = 길찾기가 실제로 가는 빠르기에 맞춰 재생 빠르기를 바꿔 발이 미끄러지지 않게 한다.
    //     뛰기는 한 번도 안 맞았을 때만 나온다 (EnemyMover 가 맞은 적은 걷게 한다). 맞아도 다친 모습은 없다 (11/03 사용자)
    //   · 전사 = 머리에 맞았으면 헤드샷 쓰러짐 둘 중 하나, 그 밖(심장·두 번째 총상·출혈)은 보통 쓰러짐 둘 중 하나.
    //     어느 쪽에서 맞았는지는 보지 않고 무작위로 고른다 (11/03 사용자). 쓰러질 때 총을 놓친다.
    //     11/12: 쓰러질 자리가 벽 · 차 · 엄폐물로 막혔으면 다른 동작을 고르거나 빈 쪽으로 몸을 틀며 쓰러진다 (ChooseFall)
    //   · 총은 가슴 뼈(UpperChest)에 붙어 있다 — 조준 자세(가늠자가 오른눈 바로 앞)와 낮춘 자세(Mixamo 대기 자세의 두 손목 자리)
    //     사이를 오간다. 낮춘 자세의 기울기는 몸 방향 기준이라 뛸 때 상체가 앞으로 숙어도 총구가 땅을 곧장 보지 않는다.
    //   · 손은 애니메이션에서 따오지 않고, 총에 맞춰 정한 자리 · 방향 · 손가락 굽힘으로 붙인다 (EnemySoldierBuilder 가 K2C1 메시와
    //     손 뼈대에서 잰다): 오른손 = 손바닥이 권총손잡이 오른쪽을 감싸고 엄지 쪽이 위, 손가락이 앞을 감아 쥔다 /
    //     왼손 = 손바닥이 총열덮개 밑에서 위를 보고 손가락이 오른쪽 옆을 감는다.
    //     이 모델은 Mixamo 자동 리깅이 집게손가락 첫 마디를 손바닥 뿌리(손목에서 3cm)에 두어서, 애니메이션 손가락을 그대로 쓰면
    //     손바닥이 접혀 손이 늘어나 보였다 (11/03 사용자) — 첫 마디는 굽히지 않는다
    //   · 팔은 두 마디 IK. 팔꿈치는 손등 줄을 따라(손목이 꺾이지 않게) 조금 아래 · 바깥으로 두고, 손목 비틀림은 아래팔과 나눈다
    // 부위 판정 상자(HitZone)는 뼈마다 붙어 있어서 몸을 따라 움직이고 같이 쓰러진다 (EnemySoldierBuilder 가 메시에서 크기를 잰다)
    //   · 11/16 엄폐 애니메이션 (사용자가 Mixamo 에서 받음):
    //     숨어 있음 = 높은 엄폐는 웅크려 섬(Taking Cover Idle), 낮은 엄폐는 무릎 쏴 자세(Crouch Idle) — 총은 낮춘 자세 그대로
    //     옆걸음 = 플레이어 쪽을 겨눈 채 왼쪽 · 오른쪽 (Strafe 둘). 앞으로 가면 걷기에 조준을 얹는다
    //     재장전 = 서서(Reloading) · 앉아서(SitReload). 이때만 총이 애니메이션의 오른손을 따라가고(조준 첫 자세에서 잰 손 ↔ 총 관계)
    //     왼손은 애니메이션 그대로 탄창을 빼고 넣는다. 탄창(Part_Magazine)은 그동안 왼손을 따라간다
    [RequireComponent(typeof(CombatantHealth))]
    public class EnemySoldier : MonoBehaviour
    {
        static readonly int k_Pose = Animator.StringToHash("Pose");
        static readonly int k_MoveSpeed = Animator.StringToHash("MoveSpeed");
        static readonly int k_ActionSpeed = Animator.StringToHash("ActionSpeed");
        static readonly int k_Dead = Animator.StringToHash("Dead");
        static readonly int k_DeathClip = Animator.StringToHash("DeathClip");

        // 애니메이터의 Pose 값 · DeathClip 값 (보통 0·1, 헤드샷 2·3) — EnemySoldierBuilder 와 같게
        public const int PoseIdle = 0, PoseAim = 1, PoseWalk = 2, PoseRun = 3,
            PoseCover = 4, PoseCrouch = 5, PoseStrafeLeft = 6, PoseStrafeRight = 7, PoseReload = 8, PoseCrouchReload = 9;
        public const int PoseCount = 10;
        public const int HeadshotDeathClips = 2;

        // 쥔 모양을 덮어쓰는 손가락 (m_FingerRotations 와 같은 순서)
        public static readonly HumanBodyBones[] Fingers =
        {
            HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal,
            HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal,
            HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal,
            HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal,
            HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal,
            HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal,
            HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal,
            HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal,
            HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal,
            HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal,
        };

        [SerializeField] Animator m_Animator;

        [Tooltip("가슴 뼈에 붙은 총 (K2C1) — 쓰러질 때 놓친다")]
        [SerializeField] Transform m_Rifle;

        [Tooltip("총구 — 조준할 때 이 앞(+Z)이 겨눌 곳을 보게 상체를 굽힌다")]
        [SerializeField] Transform m_Muzzle;

        [Tooltip("총 드는 모양 (손 자리 · 손가락 · 조준 / 낮춘 자세) — Play 중에 바꾸면 바로 보인다")]
        [SerializeField] EnemyHoldSettings m_Hold;

        [Header("뼈대에서 잰 값 (EnemySoldierBuilder 가 넣는다)")]
        [Tooltip("조준 애니메이션 첫 자세의 오른눈 자리 (가슴 뼈 기준)")]
        [SerializeField] Vector3 m_AimEye;
        [Tooltip("조준 애니메이션 첫 자세의 몸 방향 (가슴 뼈 기준)")]
        [SerializeField] Quaternion m_AimBasis = Quaternion.identity;
        [Tooltip("대기 애니메이션 첫 자세의 가슴 뼈 (몸 기준)")]
        [SerializeField] Vector3 m_IdleChestPosition;
        [SerializeField] Quaternion m_IdleChestRotation = Quaternion.identity;
        [Tooltip("손 뼈의 T 자세 회전 (아래팔 기준) — 오른손 · 왼손. 손목 비틀림을 아래팔과 나눌 때 기준")]
        [SerializeField] Quaternion[] m_HandRest = { Quaternion.identity, Quaternion.identity };
        [Tooltip("손가락 뼈의 T 자세 회전 (Fingers 순서, 부모 기준)")]
        [SerializeField] Quaternion[] m_FingerRest = new Quaternion[0];
        [Tooltip("조준 애니메이션 첫 자세의 오른손 뼈 (가슴 뼈 기준) — 재장전할 때 총이 애니메이션의 오른손을 따라가게 하는 기준")]
        [SerializeField] Vector3 m_AimRightHandPosition;
        [SerializeField] Quaternion m_AimRightHandRotation = Quaternion.identity;

        [Header("걸음")]
        [Tooltip("걷기 · 뛰기 애니메이션이 원래 나아가는 빠르기(m/초) — 이 빠르기로 갈 때 1배로 재생한다")]
        [SerializeField] float m_WalkClipSpeed = 1.2f;
        [SerializeField] float m_RunClipSpeed = 3.1f;
        [Tooltip("옆걸음 애니메이션(왼쪽 · 오른쪽)이 원래 옆으로 가는 빠르기(m/초)")]
        [SerializeField] Vector2 m_StrafeClipSpeed = new Vector2(1.24f, 1.68f);

        [Header("재장전 (11/16)")]
        [Tooltip("재장전 애니메이션 길이(초, 잘라 쓴 부분) — 서서 · 앉아서. EnemyShooter 의 갈아 끼우는 시간에 맞춰 빠르게 튼다")]
        [SerializeField] Vector2 m_ReloadClipSeconds = new Vector2(2.05f, 2.3f);
        [Tooltip("총의 탄창 — 재장전 중 왼손을 따라간다")]
        [SerializeField] Transform m_Magazine;
        [Tooltip("탄창이 왼손을 따라가는 구간 (재장전의 0~1 시간) — 서서. 왼손이 탄창을 잡는 때 ~ 새 탄창을 끼우는 때 (Blender 로 잰 손 움직임)")]
        [SerializeField] Vector2 m_MagazineInHandStand = new Vector2(0.07f, 0.52f);
        [Tooltip("앉아서 재장전할 때")]
        [SerializeField] Vector2 m_MagazineInHandCrouch = new Vector2(0.03f, 0.65f);
        [Tooltip("재장전 동작으로 들어가고 나오는 시간(초)")]
        [SerializeField] float m_ReloadBlendSeconds = 0.25f;

        [Tooltip("길찾기 빠르기가 이것(m/초)보다 빠르면 뛴다 — EnemyMover 의 걷기 1.4 · 뛰기 3.2 사이")]
        [SerializeField] float m_RunAbove = 2.2f;

        [Header("조준")]
        [Tooltip("조준 자세로 들어가고 나오는 시간(초)")]
        [SerializeField] float m_AimBlendSeconds = 0.3f;

        [Tooltip("상체를 굽히는 한계(°)")]
        [SerializeField] float m_MaxAimBend = 70f;

        [Header("벽 앞에서 총 돌리기 (11/18)")]
        [Tooltip("벽 · 엄폐물에 총이 걸리면 벽에 안 닿게 돌린다 — 끄면 전처럼 총이 벽을 뚫는다")]
        [SerializeField] bool m_WallAvoid = true;

        [Tooltip("총구가 벽에서 이만큼(m)은 떨어져 있게")]
        [SerializeField] float m_WallMargin = 0.05f;

        [Tooltip("총을 돌리고 되돌리는 빠르기(°/초)")]
        [SerializeField] float m_WallTiltSpeed = 300f;

        [Header("쓰러짐")]
        [Tooltip("쓰러지는 동작마다 끝에 머리가 가 있는 자리 — 몸 기준 (x 오른쪽 · y 앞, m). DeathClip 순서(보통 둘 · 헤드샷 둘).\n" +
                 "EnemySoldierBuilder 가 동작에서 잰다 — 지금 동작은 셋이 앞으로 1.4~1.5m, 하나(앞에서 맞은 헤드샷)가 뒤로 0.7m")]
        [SerializeField] Vector2[] m_DeathReach = { new Vector2(-0.06f, 1.43f), new Vector2(-0.37f, 1.54f), new Vector2(0.42f, -0.74f), new Vector2(-0.06f, 1.42f) };

        [Tooltip("쓰러질 자리가 비었는지 볼 때 몸의 반지름(m)")]
        [SerializeField] float m_FallRadius = 0.3f;

        [Tooltip("빈 쪽으로 몸을 트는 빠르기(°/초) — 쓰러지기 시작해 아직 서 있는 동안(0.5초쯤) 다 돈다")]
        [SerializeField] float m_FallTurnSpeed = 300f;

        CombatantHealth m_Health;
        EnemyShooter m_Shooter;
        EnemyMover m_Mover;
        NavMeshAgent m_Agent;
        float m_ReloadWeight;
        bool m_Reloading, m_ReloadCrouch;
        float m_ReloadStart;
        Vector3 m_RifleInHandPosition;   // 재장전 중 총 자리 (애니메이션 오른손 뼈 기준)
        Quaternion m_RifleInHandRotation = Quaternion.identity;
        Transform m_MagazineParent;
        Vector3 m_MagazineLocalPosition;
        Quaternion m_MagazineLocalRotation;
        bool m_MagazineInHand;
        Transform[] m_Spine;          // 허리 → 가슴 (아래부터)
        Transform m_RightUpper, m_RightLower, m_RightHand;
        Transform m_LeftUpper, m_LeftLower, m_LeftHand;
        Vector3 m_RightFingerAxis, m_LeftFingerAxis;   // 손 뼈 기준 손가락 쪽 (손목 → 가운뎃손가락 뿌리)
        Transform[] m_FingerBones;
        // m_Hold 로 계산한 값 — 총 자리(조준: 가슴 뼈 기준 / 낮춤: 자리는 가슴 뼈, 방향은 몸 기준), 두 손 자리(총 기준), 손가락 회전
        Vector3 m_AimPosition, m_LowPosition;
        Quaternion m_AimRotation = Quaternion.identity, m_LowRotation = Quaternion.identity;
        Vector3 m_RightHandPosition, m_LeftHandPosition;
        Quaternion m_RightHandRotation = Quaternion.identity, m_LeftHandRotation = Quaternion.identity;
        Quaternion[] m_FingerRotations;
        int m_HoldVersion = -1;
        float m_AimWeight;
        bool m_Dead;
        int m_ShowPose = -1;          // 보기용으로 시킨 자세 (없으면 -1)
        Vector3 m_ShowAim;
        Transform m_RifleParent;
        Bounds m_RifleBounds;         // 총 모양 (총 기준) — 떨어뜨릴 때 충돌 상자
        float m_FallTurn;             // 쓰러지며 더 돌아야 할 각도 (°, 위에서 볼 때 시계 방향 +)
        // 벽 앞에서 총을 피하는 방식 · 각 (AvoidWall): 옆으로 돌림(°, + 오른쪽) / 위로 세움 / 아래로 숙임
        const int k_TurnNone = 0, k_TurnSide = 1, k_TurnUp = 2, k_TurnDown = 3;
        int m_WallTurn;
        float m_WallAngle;
        float m_WallBack;             // 벽 앞에서 총을 몸 쪽으로 당긴 거리 (m)
        // 작은 각부터 — 처음 안 걸리는 것을 쓴다. 낮춘 자세(총구가 왼쪽 앞)는 옆으로 조금만 돌려도 벽과 나란해지고,
        // 정면을 겨눈 자세는 위로 세우는 쪽이 먼저 걸린다
        static readonly (int turn, float angle)[] k_WallAngles =
        {
            (k_TurnSide, 15f), (k_TurnDown, 20f), (k_TurnSide, 30f), (k_TurnUp, 30f), (k_TurnDown, 35f), (k_TurnSide, 45f), (k_TurnUp, 50f),
            (k_TurnDown, 50f), (k_TurnSide, 60f), (k_TurnDown, 65f), (k_TurnUp, 70f), (k_TurnSide, 75f), (k_TurnDown, 80f), (k_TurnUp, 85f),
        };
        static readonly RaycastHit[] s_WallHits = new RaycastHit[16];

        // 쓰러질 쪽을 찾을 때 몸을 틀어 보는 각도 — 덜 트는 것부터
        static readonly float[] k_FallTurns = { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 120f, -120f, 150f, -150f, 180f };
        static readonly int[] s_FallOrder = new int[4];
        static readonly Collider[] s_Overlaps = new Collider[64];
        const float k_FallStep = 0.25f;        // 이 높이(m)까지의 턱 · 연석은 넘어 쓰러진다
        const float k_HeadBeyond = 0.2f;       // 머리 뼈(목 위)에서 정수리까지

        void Awake()
        {
            m_Health = GetComponent<CombatantHealth>();
            m_Shooter = GetComponent<EnemyShooter>();
            m_Mover = GetComponent<EnemyMover>();
            m_Agent = GetComponent<NavMeshAgent>();
            FindBones();
            m_Health.StateChanged += OnStateChanged;
        }

        void FindBones()
        {
            m_Spine = new[] { Bone(HumanBodyBones.Spine), Bone(HumanBodyBones.Chest), Bone(HumanBodyBones.UpperChest) };
            m_RightUpper = Bone(HumanBodyBones.RightUpperArm);
            m_RightLower = Bone(HumanBodyBones.RightLowerArm);
            m_RightHand = Bone(HumanBodyBones.RightHand);
            m_LeftUpper = Bone(HumanBodyBones.LeftUpperArm);
            m_LeftLower = Bone(HumanBodyBones.LeftLowerArm);
            m_LeftHand = Bone(HumanBodyBones.LeftHand);
            m_RightFingerAxis = Bone(HumanBodyBones.RightMiddleProximal).localPosition.normalized;
            m_LeftFingerAxis = Bone(HumanBodyBones.LeftMiddleProximal).localPosition.normalized;
            m_FingerBones = new Transform[Fingers.Length];
            for (int i = 0; i < Fingers.Length; i++)
                m_FingerBones[i] = Bone(Fingers[i]);
            if (m_Rifle != null)
            {
                m_RifleParent = m_Rifle.parent;
                m_RifleBounds = MeshBounds(m_Rifle);
            }
            if (m_Magazine != null && m_MagazineParent == null)
            {
                m_MagazineParent = m_Magazine.parent;
                m_MagazineLocalPosition = m_Magazine.localPosition;
                m_MagazineLocalRotation = m_Magazine.localRotation;
            }
        }

        Transform Bone(HumanBodyBones bone) => m_Animator.GetBoneTransform(bone);

        void OnDestroy()
        {
            if (m_Health != null)
                m_Health.StateChanged -= OnStateChanged;
        }

        // 보기용 (적 동작 보기 씬 — EnemyShowcase): 길찾기 · 쏘기 대신 이 자세를 하고(걷기 · 뛰기는 제자리) 이 점을 겨눈다. pose 가 음수면 끈다
        public void Show(int pose, Vector3 aimPoint)
        {
            m_ShowPose = pose;
            m_ShowAim = aimPoint;
        }

        // 편집 모드 미리보기 (손 · 총 맞춤을 그림으로 확인할 때): 지금 뼈 자세(애니메이션을 샘플한 상태) 위에 총 · 손을 실행 중과 같게 맞춘다
        public void PreviewHold(float aimWeight, Vector3 aimPoint)
        {
            if (m_FingerBones == null)
                FindBones();
            m_HoldVersion = -1;
            m_AimWeight = aimWeight;
            Show(aimWeight > 0f ? PoseAim : PoseIdle, aimPoint);
            Hold();
        }

        // m_Hold 값으로 총 자리 · 손 자리 · 손가락 회전을 계산한다 (값이 바뀌었을 때만)
        void ApplyHold()
        {
            if (m_Hold == null || m_Hold.Version == m_HoldVersion)
                return;
            m_HoldVersion = m_Hold.Version;
            m_Hold.AimRifle(m_AimEye, m_AimBasis, out m_AimPosition, out m_AimRotation);
            m_Hold.LowRifle(m_IdleChestPosition, m_IdleChestRotation, out m_LowPosition, out m_LowRotation);
            m_Hold.HandPoses(Bone(HumanBodyBones.RightMiddleProximal).localPosition, Bone(HumanBodyBones.LeftMiddleProximal).localPosition,
                out m_RightHandPosition, out m_RightHandRotation, out m_LeftHandPosition, out m_LeftHandRotation);
            // 조준 첫 자세에서 애니메이션 오른손에 대해 총이 있는 자리 — Mixamo 소총 동작은 같은 총을 쥐고 만들어져 있어서
            // 재장전 동작의 오른손에 이 관계로 총을 붙이면 애니메이션이 총을 다루는 대로 움직인다
            Quaternion handInverse = Quaternion.Inverse(m_AimRightHandRotation);
            m_RifleInHandPosition = handInverse * (m_AimPosition - m_AimRightHandPosition);
            m_RifleInHandRotation = handInverse * m_AimRotation;
            if (m_FingerRest.Length == Fingers.Length)
            {
                m_FingerRotations ??= new Quaternion[Fingers.Length];
                int half = Fingers.Length / 2;
                m_Hold.FingerRotations(false, m_FingerBones, m_FingerRest, 0, m_FingerRotations);
                m_Hold.FingerRotations(true, m_FingerBones, m_FingerRest, half, m_FingerRotations);
            }
        }

        void OnStateChanged(CombatantHealth who, CombatantHealth.State state, HitZone.Part? part)
        {
            if (state == CombatantHealth.State.Dead && !m_Dead)
                Die(part == HitZone.Part.Head);
            else if (state == CombatantHealth.State.Healthy && m_Dead)
                Revive();
        }

        // 애니메이션이 돌기 전에 — 지금 무엇을 하는지 알려 준다
        void Update()
        {
            if (m_Dead)
            {
                // 빈 쪽으로 몸을 튼다 (ChooseFall 이 정한 만큼)
                if (m_FallTurn != 0f)
                {
                    float step = Mathf.MoveTowards(0f, m_FallTurn, m_FallTurnSpeed * Time.deltaTime);
                    transform.Rotate(0f, step, 0f, Space.World);
                    m_FallTurn -= step;
                }
                return;
            }
            float speed = 0f;
            if (m_Agent != null && m_Agent.enabled)
                speed = new Vector2(m_Agent.velocity.x, m_Agent.velocity.z).magnitude;
            // 길찾기가 멈춘 뒤 남은 빠르기(감속)로 걷는 동작이 0.2초 끼던 것 — 걷는 중인지는 EnemyMover 가 정한다
            bool moving = speed > 0.15f && (m_Mover == null || m_Mover.IsMoving);
            bool crouch = m_Mover != null && m_Mover.Crouching;
            bool reloading = m_ShowPose < 0 && !moving && m_Shooter != null && m_Shooter.ReloadingNow;
            if (reloading && !m_Reloading)
            {
                m_ReloadStart = Time.time;
                m_ReloadCrouch = crouch;
            }
            m_Reloading = reloading;
            bool aim = false;
            int pose;
            if (m_ShowPose >= 0)
            {
                pose = m_ShowPose;
                aim = pose == PoseAim;
                m_Animator.SetFloat(k_MoveSpeed, 1f);
            }
            else if (moving && m_Mover != null && m_Mover.Strafing)
            {
                // 옆걸음 — 몸은 플레이어 쪽(EnemyMover), 실제로 가는 쪽이 몸 기준 옆이면 왼쪽 · 오른쪽, 앞이면 걷기. 어느 쪽이든 겨눈다
                Vector3 local = transform.InverseTransformDirection(m_Agent.velocity);
                aim = true;
                if (Mathf.Abs(local.x) >= Mathf.Abs(local.z) * 0.7f || local.z < 0f)
                {
                    bool left = local.x < 0f;
                    pose = left ? PoseStrafeLeft : PoseStrafeRight;
                    m_Animator.SetFloat(k_MoveSpeed, Mathf.Clamp(speed / (left ? m_StrafeClipSpeed.x : m_StrafeClipSpeed.y), 0.5f, 1.8f));
                }
                else
                {
                    pose = PoseWalk;
                    m_Animator.SetFloat(k_MoveSpeed, Mathf.Clamp(speed / m_WalkClipSpeed, 0.5f, 1.6f));
                }
            }
            else if (moving)
            {
                bool run = m_Agent.speed > m_RunAbove;
                pose = run ? PoseRun : PoseWalk;
                m_Animator.SetFloat(k_MoveSpeed, Mathf.Clamp(speed / (run ? m_RunClipSpeed : m_WalkClipSpeed), 0.5f, 1.6f));
            }
            else if (reloading)
            {
                // 갈아 끼우는 시간에 맞춰 재장전 동작을 빠르게 튼다
                pose = m_ReloadCrouch ? PoseCrouchReload : PoseReload;
                float clip = m_ReloadCrouch ? m_ReloadClipSeconds.y : m_ReloadClipSeconds.x;
                m_Animator.SetFloat(k_ActionSpeed, clip / Mathf.Max(0.5f, m_Shooter.ReloadSeconds));
            }
            else if (crouch)
            {
                pose = PoseCrouch;
            }
            else if (m_Mover != null && m_Mover.HidingStanding)
            {
                pose = PoseCover;
            }
            else
            {
                aim = m_Shooter != null && m_Shooter.Aiming;
                pose = aim ? PoseAim : PoseIdle;
            }
            m_Animator.SetInteger(k_Pose, pose);
            m_AimWeight = Mathf.MoveTowards(m_AimWeight, aim ? 1f : 0f, Time.deltaTime / m_AimBlendSeconds);
            m_ReloadWeight = Mathf.MoveTowards(m_ReloadWeight, reloading ? 1f : 0f, Time.deltaTime / m_ReloadBlendSeconds);
        }

        // 애니메이션이 뼈를 놓은 뒤에
        void LateUpdate()
        {
            if (m_Dead || m_Rifle == null)
                return;
            Hold();
        }

        // 총을 자세에 맞게 들고, 상체를 굽혀 겨누고, 두 손을 총에 붙이고, 손가락을 쥔 모양으로
        void Hold()
        {
            ApplyHold();
            // 애니메이션이 놓은 두 손 — 재장전 중엔 총이 오른손을 따라가고 왼손은 이대로 둔다
            Vector3 animRight = m_RightHand.position, animLeft = m_LeftHand.position;
            Quaternion animRightRotation = m_RightHand.rotation, animLeftRotation = m_LeftHand.rotation;
            float w = Mathf.SmoothStep(0f, 1f, m_AimWeight);
            float r = Mathf.SmoothStep(0f, 1f, m_ReloadWeight);
            // 낮춘 자세 방향: 몸 기준과 '가슴에 붙은 듯'(대기 첫 자세의 가슴에 대해 같은 방향) 사이 — 뛸 때 상체가 숙으면 같이 숙는다
            Quaternion low = Quaternion.Slerp(transform.rotation * m_LowRotation,
                m_RifleParent.rotation * Quaternion.Inverse(m_IdleChestRotation) * m_LowRotation, m_Hold != null ? m_Hold.lowFollowChest : 0f);
            Vector3 rifleAt = m_RifleParent.TransformPoint(Vector3.Lerp(m_LowPosition, m_AimPosition, w));
            Quaternion rifleTurn = Quaternion.Slerp(low, m_RifleParent.rotation * m_AimRotation, w);
            if (r > 0f)
            {
                rifleAt = Vector3.Lerp(rifleAt, animRight + animRightRotation * m_RifleInHandPosition, r);
                rifleTurn = Quaternion.Slerp(rifleTurn, animRightRotation * m_RifleInHandRotation, r);
            }
            m_Rifle.SetPositionAndRotation(rifleAt, rifleTurn);
            if (w > 0f && m_Muzzle != null && (m_ShowPose >= 0 || m_Shooter != null))
                AimUpperBody(m_ShowPose >= 0 ? m_ShowAim : m_Shooter.LastKnown, w);
            AvoidWall();
            float twist = m_Hold != null ? m_Hold.twistToForearm : 0.5f;
            Vector3 rightBias = m_Hold != null ? m_Hold.rightElbowBias * 0.01f : Vector3.down * 0.25f;
            Vector3 leftBias = m_Hold != null ? m_Hold.leftElbowBias * 0.01f : Vector3.down * 0.25f;
            Grip(m_RightUpper, m_RightLower, m_RightHand, m_RightHandPosition, m_RightHandRotation, m_HandRest[0], m_RightFingerAxis, rightBias, twist, 0f,
                1f, animRight, animRightRotation);
            Grip(m_LeftUpper, m_LeftLower, m_LeftHand, m_LeftHandPosition, m_LeftHandRotation, m_HandRest[1], m_LeftFingerAxis, leftBias, twist,
                m_Hold != null ? m_Hold.leftSlide * 0.01f : 0f, 1f - r, animLeft, animLeftRotation);
            if (m_FingerRotations != null)
            {
                // 오른손가락은 늘 권총손잡이를 쥔 모양, 왼손가락은 재장전 중 애니메이션 쪽으로
                int half = Fingers.Length / 2;
                for (int i = 0; i < m_FingerBones.Length; i++)
                {
                    if (m_FingerBones[i] != null)
                        m_FingerBones[i].localRotation = i < half && r > 0f
                            ? Quaternion.Slerp(m_FingerRotations[i], m_FingerBones[i].localRotation, r)
                            : m_FingerRotations[i];
                }
            }
            UpdateMagazine();
        }

        // 벽 앞에서 총을 벽에 안 닿게 돌린다 (사용자 11/18: "적의 총구가 벽을 뚫고 조금 보이는 문제").
        // 몸 가운데 → 총구는 수평으로 약 90 cm 인데 적은 벽에서 50 cm 까지 다가선다. 엄폐 · 재장전 중엔 몸이 플레이어 쪽(= 숨은 벽 쪽)을 봐서
        // 총이 벽 속으로 들어가고, 벽 두께가 20 cm 라 끝이 반대편으로 나왔다 (Play 측정: 재장전 78% · 이동 2% 의 프레임에서 평균 20 ~ 30 cm).
        //   · 몸 가운데(손잡이 높이) → 총구 사이에 벽 · 엄폐물이 끼면, 손잡이를 축으로 총을 돌린다: 옆(벽과 나란히) · 위(세움) · 아래(숙임) 가운데
        //     가장 작은 각으로 안 걸리는 것. 처음엔 위아래로만 꺾었는데, 낮춘 자세의 총은 왼쪽 앞으로 비스듬해서 80° 를 숙이거나 50° 를 세워야 했고
        //     앉은 자세에선 세운 총이 몸에 겹쳤다 — 옆으로는 30° 면 된다. 어느 각도 안 되면 벽과 나란히 돌리고 몸 쪽으로 당긴다(25 cm 까지)
        //   · 두 손은 총을 따라간다 (Grip 이 이 뒤에 온다). 각은 m_WallTiltSpeed 로 부드럽게 오가고, 한번 고른 방식은 풀릴 때까지 유지한다
        //   · 플레이어를 보며 겨누는 중엔 하지 않는다 — 쏘는 자세는 그대로 (측정에서 그 상태로 벽을 뚫은 적은 없었다)
        void AvoidWall()
        {
            if (m_Muzzle == null || !Application.isPlaying)
                return;
            Vector3 grip = m_Rifle.TransformPoint(m_RightHandPosition);
            Vector3 toMuzzle = m_Muzzle.position - grip;
            Vector3 flat = Vector3.ProjectOnPlane(toMuzzle, Vector3.up);
            bool shooting = m_ShowPose < 0 && m_Shooter != null && m_Shooter.Aiming && m_Shooter.SeesPlayer;
            Vector3 axis = flat.sqrMagnitude > 1e-4f ? Vector3.Cross(Vector3.up, flat).normalized : m_Rifle.right;   // 이 축으로 + 면 총구가 내려간다
            int wantTurn = k_TurnNone;
            float wantAngle = 0f, wantBack = 0f;
            if (m_WallAvoid && !shooting && flat.sqrMagnitude > 1e-4f)
            {
                Vector3 body = new Vector3(transform.position.x, grip.y, transform.position.z);
                float floor = transform.position.y + 0.06f;
                // 총구가 이미 기운 쪽(몸 기준 왼쪽 · 오른쪽)으로 먼저 돌린다 — 낮춘 자세는 총구가 왼쪽 앞을 본다
                float side = Vector3.Dot(flat, transform.right) > 0.05f * flat.magnitude ? 1f : -1f;
                // 지금 피하고 있으면 더 넉넉히 비어야 푼다 — 벽 가장자리에서 들었다 놨다 하지 않게
                bool turned = m_WallTurn != k_TurnNone;
                if (!WallClear(body, grip + toMuzzle, turned ? m_WallMargin * 3f : m_WallMargin))
                {
                    // 1) 지금 피하는 방식 그대로 되는 가장 작은 각 (방식이 프레임마다 바뀌면 총이 떤다). 지금 각보다 작은 각은 넉넉히 비어야
                    bool found = false;
                    if (turned)
                        for (int i = 0; i < k_WallAngles.Length && !found; i++)
                        {
                            if (k_WallAngles[i].turn != m_WallTurn)
                                continue;
                            float a = k_WallAngles[i].angle;
                            float sign = m_WallTurn == k_TurnSide ? Mathf.Sign(m_WallAngle) : 1f;
                            float margin = a < Mathf.Abs(m_WallAngle) - 1f ? m_WallMargin * 3f : m_WallMargin;
                            if (TurnClear(m_WallTurn, a * sign, grip, toMuzzle, axis, body, floor, margin))
                            {
                                wantTurn = m_WallTurn;
                                wantAngle = a * sign;
                                found = true;
                            }
                        }
                    // 2) 아니면 옆 · 위 · 아래 가운데 가장 작은 각 (옆은 기운 쪽 먼저, 그다음 반대쪽)
                    for (int i = 0; i < k_WallAngles.Length && !found; i++)
                    {
                        int turn = k_WallAngles[i].turn;
                        float a = k_WallAngles[i].angle;
                        if (turn == k_TurnSide)
                        {
                            if (TurnClear(turn, a * side, grip, toMuzzle, axis, body, floor, m_WallMargin)) { wantAngle = a * side; found = true; }
                            else if (TurnClear(turn, -a * side, grip, toMuzzle, axis, body, floor, m_WallMargin)) { wantAngle = -a * side; found = true; }
                        }
                        else if (TurnClear(turn, a, grip, toMuzzle, axis, body, floor, m_WallMargin)) { wantAngle = a; found = true; }
                        if (found)
                            wantTurn = turn;
                    }
                    if (!found)
                    {
                        // 3) 어느 각도 안 된다 — 벽에 아주 바짝. 벽과 나란히 옆으로 돌리고(90°), 그래도 걸리는 만큼 몸 쪽으로 당긴다
                        wantTurn = k_TurnSide;
                        wantAngle = 90f * side;
                        Vector3 reach = grip + Quaternion.AngleAxis(wantAngle, Vector3.up) * toMuzzle - body;
                        float length = reach.magnitude;
                        wantBack = Mathf.Clamp(length + m_WallMargin - WallDistance(body, reach / length, length + m_WallMargin), 0f, 0.25f);
                    }
                }
            }
            // 방식이 바뀌면 먼저 0 으로 돌아왔다가 새 방식으로 간다 (옆으로 돌린 것과 위로 세운 것이 섞이지 않게)
            if (wantTurn != m_WallTurn && Mathf.Abs(m_WallAngle) > 0.01f)
                m_WallAngle = Mathf.MoveTowards(m_WallAngle, 0f, m_WallTiltSpeed * Time.deltaTime);
            else
            {
                m_WallTurn = wantTurn;
                m_WallAngle = Mathf.MoveTowards(m_WallAngle, wantAngle, m_WallTiltSpeed * Time.deltaTime);
            }
            m_WallBack = Mathf.MoveTowards(m_WallBack, wantBack, 1.5f * Time.deltaTime);
            if (Mathf.Abs(m_WallAngle) < 0.01f && m_WallBack < 0.001f)
                return;
            Quaternion turnNow = TurnRotation(m_WallTurn, m_WallAngle, axis);
            Vector3 back = flat.sqrMagnitude > 1e-4f ? flat.normalized * m_WallBack : Vector3.zero;
            m_Rifle.SetPositionAndRotation(grip + turnNow * (m_Rifle.position - grip) - back, turnNow * m_Rifle.rotation);
        }

        // 손잡이를 축으로: 옆 = 세로 축 둘레(+ 오른쪽), 위 = 총구를 세움, 아래 = 총구를 숙임 (axis 는 총 옆 방향, + 면 내려간다)
        static Quaternion TurnRotation(int turn, float angle, Vector3 axis) =>
            turn == k_TurnSide ? Quaternion.AngleAxis(angle, Vector3.up) : Quaternion.AngleAxis(turn == k_TurnUp ? -angle : angle, axis);

        // 그만큼 돌렸을 때 총구가 벽 · 바닥에 안 걸리나
        bool TurnClear(int turn, float angle, Vector3 grip, Vector3 toMuzzle, Vector3 axis, Vector3 body, float floor, float margin)
        {
            Vector3 tip = grip + TurnRotation(turn, angle, axis) * toMuzzle;
            return tip.y >= floor && WallClear(body, tip, margin);
        }

        // a → b (끝에서 margin 더) 사이에 벽 · 엄폐물이 없나 — 제 몸은 빼고, 총알 · 시선이 보는 것만 (Gunfire.ShotMask)
        bool WallClear(Vector3 a, Vector3 b, float margin)
        {
            Vector3 d = b - a;
            float length = d.magnitude;
            return length < 1e-4f || WallDistance(a, d / length, length + margin) >= length + margin;
        }

        // a 에서 dir 쪽으로 max 안에 처음 걸리는 벽까지 거리 (없으면 max)
        float WallDistance(Vector3 a, Vector3 dir, float max)
        {
            int count = Physics.RaycastNonAlloc(a, dir, s_WallHits, max, Gunfire.ShotMask, QueryTriggerInteraction.Ignore);
            float nearest = max;
            for (int i = 0; i < count; i++)
                if (!s_WallHits[i].collider.transform.IsChildOf(transform) && s_WallHits[i].distance < nearest)
                    nearest = s_WallHits[i].distance;
            return nearest;
        }

        // 재장전 중 왼손이 탄창을 잡은 때부터 새 탄창을 끼우는 때까지 탄창이 왼손을 따라간다 (잡을 때 자리 그대로 붙인다)
        void UpdateMagazine()
        {
            if (m_Magazine == null || m_MagazineParent == null)
                return;
            bool inHand = false;
            if (m_Reloading && m_Shooter != null)
            {
                float t = (Time.time - m_ReloadStart) / Mathf.Max(0.5f, m_Shooter.ReloadSeconds);
                Vector2 span = m_ReloadCrouch ? m_MagazineInHandCrouch : m_MagazineInHandStand;
                inHand = t >= span.x && t < span.y;
            }
            if (inHand == m_MagazineInHand)
                return;
            m_MagazineInHand = inHand;
            if (inHand)
            {
                m_Magazine.SetParent(m_LeftHand, true);
            }
            else
            {
                m_Magazine.SetParent(m_MagazineParent, false);
                m_Magazine.SetLocalPositionAndRotation(m_MagazineLocalPosition, m_MagazineLocalRotation);
            }
        }

        // 손(손목 뼈)을 총의 그 자리 · 방향에 놓는다:
        //   1) 팔이 닿지 않으면 총을 따라 slide(m)까지 뒤쪽(총 −Z)을 쥔다 — 팔을 조금 굽힌 길이(97%)에 맞춘다
        //   2) 두 마디 IK 로 손목을 거기에, 3) 팔꿈치를 손등 줄을 따라 아래팔 길이만큼 뒤 + bias 쪽으로 (손목이 꺾이지 않게)
        //   4) 손목 비틀림 일부를 아래팔이 맡고, 5) 손 방향을 맞춘다
        // weight 가 1 보다 작으면 애니메이션이 놓은 손(animPosition · animRotation)과 총 자리 사이로 (재장전 중 왼손), 0 이면 애니메이션 그대로
        void Grip(Transform upper, Transform lower, Transform hand, Vector3 position, Quaternion rotation, Quaternion rest, Vector3 fingerAxis, Vector3 bias, float twist, float slide,
            float weight, Vector3 animPosition, Quaternion animRotation)
        {
            if (hand == null || weight <= 0f)
                return;
            Vector3 target = m_Rifle.TransformPoint(position);
            Quaternion handRotation = m_Rifle.rotation * rotation;
            float forearm = Vector3.Distance(lower.position, hand.position);
            if (slide > 0f)
            {
                float reach = (Vector3.Distance(upper.position, lower.position) + forearm) * 0.97f;
                Vector3 d = target - upper.position;
                Vector3 back = m_Rifle.forward;
                float along = Vector3.Dot(d, back);
                float root = along * along - d.sqrMagnitude + reach * reach;   // |d − back·s| = reach 의 작은 근
                float s = d.sqrMagnitude <= reach * reach ? 0f : root < 0f ? slide : along - Mathf.Sqrt(root);
                target -= back * Mathf.Clamp(s, 0f, slide);
            }
            if (weight < 1f)
            {
                target = Vector3.Lerp(animPosition, target, weight);
                handRotation = Quaternion.Slerp(animRotation, handRotation, weight);
            }
            SolveArm(upper, lower, hand, target);
            SwingElbow(upper, lower, hand, target - handRotation * fingerAxis * forearm + transform.rotation * bias);
            SplitTwist(lower, hand, handRotation, rest, twist);
            hand.rotation = handRotation;
        }

        // 어깨 → 손을 잇는 줄을 축으로 팔을 돌려 팔꿈치를 pole 쪽으로 — 손 자리는 그대로다
        static void SwingElbow(Transform upper, Transform lower, Transform end, Vector3 pole)
        {
            Vector3 a = upper.position;
            Vector3 axis = end.position - a;
            Vector3 elbow = Vector3.ProjectOnPlane(lower.position - a, axis);
            Vector3 want = Vector3.ProjectOnPlane(pole - a, axis);
            if (elbow.sqrMagnitude < 1e-6f || want.sqrMagnitude < 1e-6f)
                return;
            upper.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(elbow, want, axis), axis.normalized) * upper.rotation;
        }

        // 손이 아래팔에 대해 T 자세보다 뼈 축(아래팔 → 손) 둘레로 비틀린 만큼의 share 를 아래팔이 맡는다 — 아래팔을 제 축으로 돌려서 손 자리는 그대로다
        static void SplitTwist(Transform lower, Transform hand, Quaternion handRotation, Quaternion rest, float share)
        {
            if (share <= 0f)
                return;
            Quaternion turn = Quaternion.Inverse(lower.rotation) * handRotation * Quaternion.Inverse(rest);   // T 자세에서 달라진 만큼 (아래팔 기준)
            Vector3 axis = hand.localPosition.normalized;
            Vector3 v = Vector3.Project(new Vector3(turn.x, turn.y, turn.z), axis);
            float m = Mathf.Sqrt(v.sqrMagnitude + turn.w * turn.w);
            if (m < 1e-5f)
                return;
            new Quaternion(v.x / m, v.y / m, v.z / m, turn.w / m).ToAngleAxis(out float angle, out Vector3 twistAxis);
            if (angle > 180f)
                angle -= 360f;
            lower.rotation *= Quaternion.AngleAxis(angle * share, twistAxis);
        }

        // 총구 앞이 겨눌 곳을 보도록 허리 → 가슴 뼈에 나눠 돌린다. 위 뼈일수록 남은 만큼을 더 맡아 마지막 뼈에서 딱 맞는다
        void AimUpperBody(Vector3 target, float weight)
        {
            for (int i = 0; i < m_Spine.Length; i++)
            {
                if (m_Spine[i] == null)
                    continue;
                Vector3 want = target - m_Muzzle.position;
                if (want.sqrMagnitude < 0.01f)
                    return;
                Quaternion fix = Quaternion.FromToRotation(m_Muzzle.forward, want);
                fix.ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180f)
                    angle -= 360f;
                angle = Mathf.Clamp(angle, -m_MaxAimBend, m_MaxAimBend) * weight / (m_Spine.Length - i);
                m_Spine[i].rotation = Quaternion.AngleAxis(angle, axis) * m_Spine[i].rotation;
            }
        }

        // 어깨(upper) · 팔꿈치(lower) · 손(end) 두 마디 IK — 팔꿈치 각을 코사인 법칙으로 맞춘 뒤 어깨를 돌려 손을 target 에 놓는다
        static void SolveArm(Transform upper, Transform lower, Transform end, Vector3 target)
        {
            Vector3 a = upper.position, b = lower.position, c = end.position;
            float ab = Vector3.Distance(a, b), bc = Vector3.Distance(b, c);
            float at = Mathf.Clamp(Vector3.Distance(a, target), Mathf.Abs(ab - bc) + 0.001f, ab + bc - 0.001f);
            float want = Mathf.Acos(Mathf.Clamp((ab * ab + bc * bc - at * at) / (2f * ab * bc), -1f, 1f)) * Mathf.Rad2Deg;
            float now = Vector3.Angle(a - b, c - b);
            Vector3 axis = Vector3.Cross(a - b, c - b);
            if (axis.sqrMagnitude < 1e-8f)
                axis = Vector3.Cross(c - a, Vector3.down);   // 팔이 곧게 펴져 있으면 팔꿈치를 아래로
            lower.rotation = Quaternion.AngleAxis(want - now, axis.normalized) * lower.rotation;
            upper.rotation = Quaternion.FromToRotation(end.position - a, target - a) * upper.rotation;
        }

        void Die(bool headshot)
        {
            m_Dead = true;
            ChooseFall(headshot, out int clip, out m_FallTurn);
            m_Animator.SetInteger(k_DeathClip, clip);
            m_Animator.SetBool(k_Dead, true);
            DropRifle();
        }

        // 쓰러질 동작과 방향 (11/12 — 11/11 헤드셋: "적이 벽이나 물건을 뚫고 죽는다"). 쓰러지는 동작은 몸을 1.4~1.5m(하나는 0.7m) 옮기는데,
        // 죽을 때 길찾기를 끄니 막는 것이 없어서 벽 · 차 · 엄폐물 앞에 선 적이 그 속으로 쓰러졌다. → 몸이 지나갈 자리가 비었는지 보고 고른다:
        //   1) 지금 선 방향 그대로 — 맞은 부위에 맞는 동작 둘(무작위 순서), 그다음 다른 부위의 동작 둘
        //   2) 다 막혔으면 몸을 30° 씩 좌우로 틀어 가며 같은 순서로 — 가장 덜 트는 쪽을 고르고, 쓰러지기 시작할 때 그만큼 돈다
        //   3) 어디도 비지 않으면(아주 좁은 구석) 전처럼 맞은 부위에 맞는 동작을 그 자리에서
        void ChooseFall(bool headshot, out int clip, out float turn)
        {
            int own = (headshot ? HeadshotDeathClips : 0) + Random.Range(0, 2);
            int other = (headshot ? 0 : HeadshotDeathClips) + Random.Range(0, 2);
            s_FallOrder[0] = own;
            s_FallOrder[1] = own ^ 1;      // 같은 부류의 나머지 하나 (0 ↔ 1, 2 ↔ 3)
            s_FallOrder[2] = other;
            s_FallOrder[3] = other ^ 1;
            float mirror = Random.value < 0.5f ? 1f : -1f;   // 왼쪽 · 오른쪽 가운데 어느 쪽부터 볼지
            foreach (float angle in k_FallTurns)
            {
                foreach (int candidate in s_FallOrder)
                {
                    if (candidate >= m_DeathReach.Length || !FallIsClear(m_DeathReach[candidate], angle * mirror))
                        continue;
                    clip = candidate;
                    turn = angle * mirror;
                    return;
                }
            }
            clip = own;
            turn = 0f;
        }

        // 몸을 turn(°)만큼 튼 뒤 그 동작으로 쓰러지면 지나갈 자리가 비었는지 — 선 자리에서 머리가 닿는 곳까지를 캡슐 둘로 본다:
        // 낮은 것(바닥 턱 위 ~ 허리)은 끝까지, 높은 것(허리 ~ 어깨)은 절반까지 (그 뒤로는 상체가 이미 내려와 있다)
        bool FallIsClear(Vector2 reach, float turn)
        {
            Vector3 flat = Quaternion.AngleAxis(turn, Vector3.up) * transform.rotation * new Vector3(reach.x, 0f, reach.y);
            flat.y = 0f;
            float length = flat.magnitude;
            if (length < 0.01f)
                return true;
            Vector3 dir = flat / length;
            // 캡슐은 선 자리 바로 앞에서 시작한다 — 등 뒤에 붙은 벽은 쓰러지는 데 걸리지 않는다
            Vector3 from = transform.position + dir * (m_FallRadius * 0.8f);
            Vector3 to = transform.position + dir * Mathf.Max(m_FallRadius * 0.8f, length + k_HeadBeyond - m_FallRadius);
            Vector3 low = Vector3.up * (k_FallStep + m_FallRadius);
            Vector3 high = Vector3.up * (k_FallStep + m_FallRadius * 3f);
            return !FallBlocked(from + low, to + low) && !FallBlocked(from + high, Vector3.Lerp(from, to, 0.5f) + high);
        }

        // 그 캡슐에 움직이지 않는 것(벽 · 바닥 · 차 · 엄폐물)이 걸리는지. 사람(자기 · 다른 적 · 플레이어)과 굴러다니는 물건(떨어진 총 · 탄창)은 겹쳐도 된다
        bool FallBlocked(Vector3 a, Vector3 b)
        {
            int count = Physics.OverlapCapsuleNonAlloc(a, b, m_FallRadius, s_Overlaps, Gunfire.ShotMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider c = s_Overlaps[i];
                if (c.attachedRigidbody != null || c is CharacterController || c.GetComponentInParent<CombatantHealth>() != null)
                    continue;
                return true;
            }
            // 통이 꽉 찼으면 못 본 것이 있을 수 있다 — 막힌 것으로 친다
            return count >= s_Overlaps.Length;
        }

        // 총을 놓친다 — 몸에서 떼어 물리로 떨어뜨린다. 자기 몸 판정 상자와는 부딪히지 않는다
        void DropRifle()
        {
            if (m_Rifle == null)
                return;
            m_Rifle.SetParent(transform, true);
            var box = m_Rifle.gameObject.AddComponent<BoxCollider>();
            box.center = m_RifleBounds.center;
            box.size = m_RifleBounds.size;
            foreach (HitZone zone in GetComponentsInChildren<HitZone>())
                Physics.IgnoreCollision(box, zone.GetComponent<Collider>());
            var body = m_Rifle.gameObject.AddComponent<Rigidbody>();
            body.mass = 3.5f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        // 다시하기 — 총을 다시 들고 선 자세로
        void Revive()
        {
            m_Dead = false;
            m_FallTurn = 0f;   // 돌아간 몸은 EnemyMover 가 처음 방향으로 되돌린다
            m_Reloading = false;
            m_ReloadWeight = 0f;
            UpdateMagazine();   // 재장전 중 쓰러져 왼손에 남은 탄창을 총으로
            if (m_Rifle != null)
            {
                if (m_Rifle.TryGetComponent(out Rigidbody body))
                {
                    body.isKinematic = true;
                    Destroy(body);
                }
                if (m_Rifle.TryGetComponent(out BoxCollider box))
                    Destroy(box);
                m_Rifle.SetParent(m_RifleParent, false);
                m_Rifle.localPosition = m_LowPosition;
            }
            m_Animator.Rebind();
            m_Animator.Update(0f);
            m_AimWeight = 0f;
        }

        // 메시들의 테두리 상자 (root 기준)
        public static Bounds MeshBounds(Transform root)
        {
            bool any = false;
            var bounds = new Bounds();
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null)
                    continue;
                Bounds b = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = root.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if (any)
                        bounds.Encapsulate(p);
                    else
                        bounds = new Bounds(p, Vector3.zero);
                    any = true;
                }
            }
            return bounds;
        }
    }
}
