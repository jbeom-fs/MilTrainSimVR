using Unity.XR.CoreUtils;
using UnityEngine;

namespace MilTrainSimVR.UrbanCombat
{
    // VR 플레이어의 몸 판정 (10/26). 헤드셋은 머리와 두 손 위치만 안다 →
    //   · 머리: 눈(카메라)에서 조금 뒤 · 위
    //   · 몸통(가슴·배)·다리: 목 바로 아래로 쌓고, 고개를 돌린 쪽(수평)을 몸 앞으로 본다. 앉으면 머리와 같이 내려가고 다리는 짧아진다.
    //     목 = 눈에서 머리 기준 아래·뒤 (10/29) — 고개를 숙이면 눈은 목을 축으로 아래·앞으로 돈다. 전처럼 눈 높이에서 바로 재면
    //     내려다볼 때 몸통이 눈을 따라 10cm 넘게 내려가고 앞으로 나와서, 가슴에 붙은 방탄복이 따라 움직였다. 똑바로 서 있을 땐 전과 같다
    //   · 팔: 어깨(가슴 위 양쪽 모서리) → 팔꿈치(손에서 컨트롤러 뒤쪽으로 0.28m) → 손
    //   · 심장: 가슴 상자 안 왼쪽 앞의 작은 구 (가슴을 뚫고 지나가면 심장 — HitZone 의 안쪽 부위)
    // 상자들은 '트리거'라 아무것도 막지 않는다 — 걸어 다닐 때 벽·적에 걸리지 않고, 내 총(트리거를 무시)에도 안 맞는다.
    // 적의 총은 트리거까지 보는 광선으로 이 상자(HitZone)를 맞힌다 → 부위가 몸 상태(CombatantHealth)로 간다
    [RequireComponent(typeof(XROrigin), typeof(CombatantHealth))]
    public class PlayerHitboxes : MonoBehaviour
    {
        [Tooltip("왼손 — 비우면 'Left Controller'. 이 오브젝트의 앞(+Z)이 손가락 쪽, 뒤로 팔뚝이 이어진다고 본다")]
        [SerializeField] Transform m_LeftHand;

        [Tooltip("오른손 — 비우면 'Right Controller'")]
        [SerializeField] Transform m_RightHand;

        // 몸 치수(m) — 키 175cm 안팎 기준. 눈 높이에서 아래로 잰다
        const float k_HeadRadius = 0.11f;
        const float k_HeadBack = 0.08f;       // 눈에서 머리 가운데까지 (뒤로)
        // 목(고개를 숙이는 축) → 눈, 머리 기준. 숙일 땐 목뼈 전체가 굽어 머리가 앞으로 많이 나온다 → 축을 눈 12cm 아래로 잡았다
        // (7.5cm 로 하면 내려다볼 때 눈이 앞판 겉면보다 뒤에 있어 앞판 윗단이 주머니 탄창을 가렸다 — 10/29 렌더)
        static readonly Vector3 k_NeckToEyes = new Vector3(0f, 0.12f, 0.08f);
        const float k_NeckToShoulder = 0.08f;   // 목 → 어깨선 (똑바로 서면 눈 → 어깨선 0.20 아래 · 0.08 뒤)
        static readonly Vector3 k_ChestSize = new Vector3(0.40f, 0.32f, 0.24f);
        static readonly Vector3 k_AbdomenSize = new Vector3(0.36f, 0.30f, 0.22f);
        const float k_LegWidth = 0.34f;
        const float k_LegDepth = 0.22f;
        static readonly Vector3 k_HeartOffset = new Vector3(-0.05f, 0.03f, 0.05f);   // 가슴 가운데에서 왼쪽·위·앞
        const float k_HeartRadius = 0.06f;
        const float k_ShoulderHalf = 0.21f;
        const float k_ForearmLength = 0.28f;
        const float k_ForearmRadius = 0.045f;
        const float k_UpperArmRadius = 0.05f;

        XROrigin m_Origin;
        Transform m_Root;
        Transform m_Head, m_Chest, m_Abdomen, m_Legs;
        BoxCollider m_LegsBox;
        Transform m_UpperL, m_ForeL, m_UpperR, m_ForeR;
        CapsuleCollider m_UpperLCap, m_ForeLCap, m_UpperRCap, m_ForeRCap;
        Vector3 m_Facing = Vector3.forward;

        // 시험·적 AI 가 읽는 부위 위치
        public Transform Head => m_Head;
        public Transform Chest => m_Chest;

        void Awake()
        {
            m_Origin = GetComponent<XROrigin>();
            if (m_LeftHand == null)
                m_LeftHand = FindChild(transform, "Left Controller");
            if (m_RightHand == null)
                m_RightHand = FindChild(transform, "Right Controller");
            Build();
        }

        void Build()
        {
            m_Root = new GameObject("C_PlayerHitboxes").transform;
            m_Root.SetParent(transform, false);
            // 움직이는 트리거 묶음 — 고정 물체로 두면 물리 엔진이 매 프레임 다시 계산한다. 움직이는 물체(키네마틱)라고 알려 준다
            var body = m_Root.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            m_Head = Part("Head", HitZone.Part.Head, out SphereCollider head).transform;
            head.radius = k_HeadRadius;

            m_Chest = Part("Chest", HitZone.Part.Chest, out BoxCollider chest).transform;
            chest.size = k_ChestSize;
            var heart = new GameObject("Heart").transform;
            heart.SetParent(m_Chest, false);
            heart.localPosition = k_HeartOffset;
            m_Chest.GetComponent<HitZone>().Setup(HitZone.Part.Chest, heart, k_HeartRadius);

            m_Abdomen = Part("Abdomen", HitZone.Part.Abdomen, out BoxCollider abdomen).transform;
            abdomen.size = k_AbdomenSize;

            m_Legs = Part("Legs", HitZone.Part.Leg, out m_LegsBox).transform;

            m_UpperL = Part("UpperArm_L", HitZone.Part.Arm, out m_UpperLCap).transform;
            m_ForeL = Part("Forearm_L", HitZone.Part.Arm, out m_ForeLCap).transform;
            m_UpperR = Part("UpperArm_R", HitZone.Part.Arm, out m_UpperRCap).transform;
            m_ForeR = Part("Forearm_R", HitZone.Part.Arm, out m_ForeRCap).transform;
        }

        GameObject Part<T>(string partName, HitZone.Part part, out T collider) where T : Collider
        {
            var go = new GameObject(partName);
            go.transform.SetParent(m_Root, false);
            collider = go.AddComponent<T>();
            collider.isTrigger = true;
            go.AddComponent<HitZone>().Setup(part);
            return go;
        }

        // 카메라·손이 이번 프레임 자리로 움직인 뒤에 몸을 맞춘다
        void LateUpdate()
        {
            Camera cam = m_Origin.Camera;
            if (cam == null)
                return;
            Transform eyes = cam.transform;
            Vector3 up = Vector3.up;

            // 몸이 보는 방향 = 고개가 향한 수평 방향. 많이 숙이거나 들면 앞 방향이 거의 수직이라 흔들리니, 그만큼 머리 위쪽을 섞는다
            // (숙이면 머리 위쪽이 앞을 가리킨다) — 내려다보며 탄창을 꺼낼 때 방탄복이 돌지 않게 (10/29). 그래도 없으면 그 전 방향 그대로
            Vector3 flat = Vector3.ProjectOnPlane(eyes.forward, up) - Vector3.ProjectOnPlane(eyes.up, up) * eyes.forward.y;
            if (flat.sqrMagnitude > 0.01f)
                m_Facing = flat.normalized;
            Quaternion torso = Quaternion.LookRotation(m_Facing, up);

            Vector3 headCenter = eyes.position - eyes.forward * k_HeadBack + up * 0.02f;
            m_Head.SetPositionAndRotation(headCenter, eyes.rotation);

            // 몸통은 목 바로 아래로
            float floorY = transform.position.y;
            Vector3 neck = eyes.position - eyes.rotation * k_NeckToEyes;
            Vector3 shoulder = neck - up * k_NeckToShoulder;
            m_Chest.SetPositionAndRotation(shoulder - up * (k_ChestSize.y * 0.5f), torso);
            m_Abdomen.SetPositionAndRotation(shoulder - up * (k_ChestSize.y + k_AbdomenSize.y * 0.5f), torso);

            float hipY = shoulder.y - k_ChestSize.y - k_AbdomenSize.y;
            float legHeight = Mathf.Max(0.2f, hipY - floorY);
            m_LegsBox.size = new Vector3(k_LegWidth, legHeight, k_LegDepth);
            m_Legs.SetPositionAndRotation(new Vector3(shoulder.x, floorY + legHeight * 0.5f, shoulder.z), torso);

            Vector3 right = torso * Vector3.right;
            Vector3 shoulderLine = shoulder - up * 0.03f;
            Arm(m_LeftHand, shoulderLine - right * k_ShoulderHalf, m_UpperL, m_UpperLCap, m_ForeL, m_ForeLCap);
            Arm(m_RightHand, shoulderLine + right * k_ShoulderHalf, m_UpperR, m_UpperRCap, m_ForeR, m_ForeRCap);
        }

        void Arm(Transform hand, Vector3 shoulder, Transform upper, CapsuleCollider upperCap, Transform fore, CapsuleCollider foreCap)
        {
            if (hand == null)
                return;
            Vector3 wrist = hand.position;
            Vector3 elbow = wrist - hand.forward * k_ForearmLength;
            Segment(fore, foreCap, wrist, elbow, k_ForearmRadius);
            Segment(upper, upperCap, elbow, shoulder, k_UpperArmRadius);
        }

        // 두 점 사이를 잇는 캡슐 (캡슐의 긴 축 = 자기 Y 축)
        static void Segment(Transform t, CapsuleCollider cap, Vector3 a, Vector3 b, float radius)
        {
            Vector3 d = b - a;
            float length = d.magnitude;
            t.position = (a + b) * 0.5f;
            if (length > 0.001f)
                t.rotation = Quaternion.FromToRotation(Vector3.up, d / length);
            cap.direction = 1;
            cap.radius = radius;
            cap.height = length + radius * 2f;
        }

        static Transform FindChild(Transform parent, string childName)
        {
            foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == childName)
                    return t;
            }
            return null;
        }
    }
}
