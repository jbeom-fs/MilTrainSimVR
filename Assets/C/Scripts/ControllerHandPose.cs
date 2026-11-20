using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MilTrainSimVR.UrbanCombat
{
    // 컨트롤러 대신 보여 줄 '손 모델'의 손가락을 컨트롤러 버튼에 맞춰 굽힌다.
    // 그립 → 중지·약지·새끼, 트리거 → 검지, 엄지가 스틱·버튼에 닿으면 → 엄지.
    // 손가락 뼈는 XR Hands 샘플 손의 이름(L_IndexProximal …)으로 찾는다.
    // 이 손의 뼈는 자기 X 축으로 (+)회전하면 손바닥 쪽으로 굽는다 → 처음 회전에 그만큼 더한다.
    // 총을 쥐는 동안엔 총(RifleHands)이 손가락 굽힘을 정한다 (…Override, 10/28) — 손 모델도 총에 옮겨 붙인다
    public class ControllerHandPose : MonoBehaviour
    {
        [Tooltip("왼손이면 켠다 — 어느 컨트롤러의 버튼을 읽을지 정한다")]
        [SerializeField] bool m_LeftHand;

        [Tooltip("손가락 뼈를 찾을 손 모델")]
        [SerializeField] Transform m_HandModel;

        [Tooltip("다 쥐었을 때 더 굽히는 각도(도) — 첫째·둘째·끝 마디")]
        [SerializeField] Vector3 m_FingerCurl = new Vector3(60f, 70f, 40f);

        [Tooltip("엄지를 스틱·버튼에 올렸을 때 더 굽히는 각도(도) — 첫째·끝 마디")]
        [SerializeField] Vector2 m_ThumbCurl = new Vector2(25f, 35f);

        [Tooltip("손가락이 따라오는 빠르기 — 클수록 바로 따라온다")]
        [SerializeField] float m_Speed = 20f;

        class Joint
        {
            public Transform bone;
            public Quaternion rest;   // 모델의 처음(살짝 편) 회전
            public float maxAngle;    // 다 굽혔을 때 더할 각도
        }

        readonly List<Joint> m_Index = new List<Joint>();
        readonly List<Joint> m_Others = new List<Joint>();   // 중지·약지·새끼 — 그립 하나로 같이 굽힌다
        readonly List<Joint> m_Thumb = new List<Joint>();
        bool m_Initialized;

        InputAction m_Grip;
        InputAction m_Trigger;
        InputAction m_TriggerTouch;
        InputAction m_ThumbTouch;

        float m_GripValue;
        float m_IndexValue;
        float m_ThumbValue;

        // 값이 있으면 버튼 대신 이 값으로 굽힌다 (0 = 편 손, 1 = 다 굽힘, 음수 = 살짝 젖힘). null 이면 버튼대로
        public float? GripOverride { get; set; }
        public float? IndexOverride { get; set; }
        public float? ThumbOverride { get; set; }

        // 검지를 마디별 각도(도)로 정한다 — x 첫째 · y 둘째 · z 끝 마디를 손바닥 쪽으로 굽힘(음수 = 젖힘), IndexSpread = 첫째 마디를 옆으로.
        // 값이 있으면 IndexOverride 보다 먼저 쓴다. 총의 방아쇠 손가락용 — 방아쇠 밖에 곧게 뻗었다가 방아쇠로 감는 모양은 굽힘 하나로는 못 만든다
        public Vector3? IndexAnglesOverride { get; set; }
        public float IndexSpread { get; set; }

        // 가운데·약지·새끼를 마디별 각도(도)로 — 총열덮개처럼 큰 것을 겉면을 따라 감쌀 때 (굽힘 하나로는 손가락이 물체를 뚫는다)
        public Vector3? OthersAnglesOverride { get; set; }

        // 가운데·약지·새끼를 손가락마다 따로 마디별 각도(도)로 — [0] 가운데 · [1] 약지 · [2] 새끼. 있으면 OthersAnglesOverride 보다 먼저 쓴다.
        // 권총손잡이처럼 손가락마다 닿는 겉면이 다른 것을 감쌀 때 (11/12 — 셋을 같은 각도로 굽히면 손잡이를 뚫었다)
        public Vector3[] OthersPerFingerOverride { get; set; }

        public Transform HandModel => m_HandModel;
        public bool IsLeft => m_LeftHand;

        // 트리거를 당긴 정도 0~1 — 총을 쥔 손은 이 값만큼 검지를 방아쇠 쪽으로 옮긴다
        public float TriggerValue => m_Trigger != null ? m_Trigger.ReadValue<float>() : 0f;

        void Awake()
        {
            Initialize();
        }

        void OnEnable()
        {
            // 버튼을 직접 읽는 작은 입력들. {Grip} 같은 건 버튼의 '용도' 이름이라 컨트롤러 종류가 달라도 맞는 버튼을 찾는다
            string hand = m_LeftHand ? "<XRController>{LeftHand}" : "<XRController>{RightHand}";
            m_Grip = new InputAction("Hand Grip", InputActionType.Value, hand + "/{Grip}");
            m_Trigger = new InputAction("Hand Trigger", InputActionType.Value, hand + "/{Trigger}");
            m_TriggerTouch = new InputAction("Hand Trigger Touch", InputActionType.Button, hand + "/{TriggerTouch}");
            // 엄지는 스틱·A/B(X/Y)·엄지 받침 중 어디든 닿아 있으면 내린다
            m_ThumbTouch = new InputAction("Hand Thumb Touch", InputActionType.Button, hand + "/{Primary2DAxisTouch}");
            m_ThumbTouch.AddBinding(hand + "/{PrimaryTouch}");
            m_ThumbTouch.AddBinding(hand + "/{SecondaryTouch}");
            m_ThumbTouch.AddBinding(hand + "/thumbrestTouched");

            m_Grip.Enable();
            m_Trigger.Enable();
            m_TriggerTouch.Enable();
            m_ThumbTouch.Enable();
        }

        void OnDisable()
        {
            m_Grip?.Dispose();
            m_Trigger?.Dispose();
            m_TriggerTouch?.Dispose();
            m_ThumbTouch?.Dispose();
        }

        void Update()
        {
            float grip = GripOverride ?? m_Grip.ReadValue<float>();
            // 검지가 방아쇠에 닿아만 있어도 살짝 굽힌다 — 떼면 쭉 펴서 '가리키기'가 된다
            float index = IndexOverride ?? Mathf.Max(m_Trigger.ReadValue<float>(), m_TriggerTouch.IsPressed() ? 0.25f : 0f);
            float thumb = ThumbOverride ?? (m_ThumbTouch.IsPressed() ? 1f : 0f);

            // 버튼은 0/1 로 뚝 바뀌니 몇 프레임에 걸쳐 따라가게 한다 (프레임 수와 상관없이 같은 빠르기)
            float t = 1f - Mathf.Exp(-m_Speed * Time.deltaTime);
            m_GripValue = Mathf.Lerp(m_GripValue, grip, t);
            m_IndexValue = Mathf.Lerp(m_IndexValue, index, t);
            m_ThumbValue = Mathf.Lerp(m_ThumbValue, thumb, t);

            ApplyPose(m_GripValue, m_IndexValue, m_ThumbValue);

            // 검지를 마디별로 정하라고 했으면 굽힘 값 대신 그 각도로 (부드럽게 따라간다)
            if (IndexAnglesOverride.HasValue)
            {
                m_IndexAngles = Vector3.Lerp(m_IndexAngles, IndexAnglesOverride.Value, t);
                m_IndexSpread = Mathf.Lerp(m_IndexSpread, IndexSpread, t);
                ApplyIndexAngles(m_IndexAngles, m_IndexSpread);
            }
            else
            {
                // 다음에 마디별로 바뀔 때 지금 모양에서 출발하게
                m_IndexAngles = new Vector3(m_FingerCurl.x, m_FingerCurl.y, m_FingerCurl.z) * m_IndexValue;
                m_IndexSpread = 0f;
            }

            Vector3[] perFinger = OthersPerFingerOverride;
            if (perFinger != null || OthersAnglesOverride.HasValue)
            {
                for (int f = 0; f < m_OthersAngles.Length; f++)
                {
                    Vector3 target = perFinger != null && f < perFinger.Length ? perFinger[f] : OthersAnglesOverride.GetValueOrDefault();
                    m_OthersAngles[f] = Vector3.Lerp(m_OthersAngles[f], target, t);
                }
                ApplyOthersAngles(m_OthersAngles);
            }
            else
            {
                for (int f = 0; f < m_OthersAngles.Length; f++)
                    m_OthersAngles[f] = new Vector3(m_FingerCurl.x, m_FingerCurl.y, m_FingerCurl.z) * m_GripValue;
            }
        }

        Vector3 m_IndexAngles;
        float m_IndexSpread;
        readonly Vector3[] m_OthersAngles = new Vector3[3];   // 가운데 · 약지 · 새끼

        // 손가락마다 다른 각도로 — [0] 가운데 · [1] 약지 · [2] 새끼 (m_Others 는 손가락마다 첫째·둘째·끝 마디 순서)
        public void ApplyOthersAngles(Vector3[] angles)
        {
            Initialize();
            for (int i = 0; i < m_Others.Count; i++)
            {
                Vector3 finger = angles[Mathf.Min(i / 3, angles.Length - 1)];
                float a = (i % 3) == 0 ? finger.x : (i % 3) == 1 ? finger.y : finger.z;
                m_Others[i].bone.localRotation = m_Others[i].rest * Quaternion.Euler(a, 0f, 0f);
            }
        }

        // 가운데·약지·새끼 세 마디를 각도로 — 손가락마다 첫째·둘째·끝 마디 순서로 들어 있다
        public void ApplyOthersAngles(Vector3 angles)
        {
            Initialize();
            for (int i = 0; i < m_Others.Count; i++)
            {
                float a = (i % 3) == 0 ? angles.x : (i % 3) == 1 ? angles.y : angles.z;
                m_Others[i].bone.localRotation = m_Others[i].rest * Quaternion.Euler(a, 0f, 0f);
            }
        }

        // 검지 세 마디를 각도로 — public 이라 에디터에서 모양을 볼 수 있다
        public void ApplyIndexAngles(Vector3 angles, float spread)
        {
            Initialize();
            if (m_Index.Count < 3)
                return;
            m_Index[0].bone.localRotation = m_Index[0].rest * Quaternion.Euler(angles.x, spread, 0f);
            m_Index[1].bone.localRotation = m_Index[1].rest * Quaternion.Euler(angles.y, 0f, 0f);
            m_Index[2].bone.localRotation = m_Index[2].rest * Quaternion.Euler(angles.z, 0f, 0f);
        }

        // 0 = 편 손, 1 = 다 굽힘. public 이라 헤드셋 없이도 에디터에서 모양을 볼 수 있다
        public void ApplyPose(float grip, float index, float thumb)
        {
            Initialize();
            Bend(m_Others, grip);
            Bend(m_Index, index);
            Bend(m_Thumb, thumb);
        }

        static void Bend(List<Joint> joints, float amount)
        {
            foreach (Joint joint in joints)
                joint.bone.localRotation = joint.rest * Quaternion.Euler(joint.maxAngle * amount, 0f, 0f);
        }

        void Initialize()
        {
            if (m_Initialized || m_HandModel == null)
                return;
            m_Initialized = true;

            AddFinger(m_Index, "Index");
            AddFinger(m_Others, "Middle");
            AddFinger(m_Others, "Ring");
            AddFinger(m_Others, "Little");
            AddJoint(m_Thumb, "ThumbProximal", m_ThumbCurl.x);
            AddJoint(m_Thumb, "ThumbDistal", m_ThumbCurl.y);
        }

        void AddFinger(List<Joint> joints, string finger)
        {
            AddJoint(joints, finger + "Proximal", m_FingerCurl.x);
            AddJoint(joints, finger + "Intermediate", m_FingerCurl.y);
            AddJoint(joints, finger + "Distal", m_FingerCurl.z);
        }

        void AddJoint(List<Joint> joints, string boneName, float maxAngle)
        {
            // 이름 앞의 L_ / R_ 는 빼고 끝부분으로 찾는다
            foreach (Transform bone in m_HandModel.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name.EndsWith("_" + boneName))
                {
                    joints.Add(new Joint { bone = bone, rest = bone.localRotation, maxAngle = maxAngle });
                    return;
                }
            }
            Debug.LogWarning($"[C] 손 뼈를 못 찾음: {boneName}", this);
        }
    }
}
