using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Jump;

namespace MilTrainSimVR.UrbanCombat
{
    // 점프 버튼 (11/12) — 뛰는 것 자체는 XRI 의 JumpProvider 가 그대로 하고(높이 1.25m, 오래 누르면 더 높이), 여기서는 '어느 버튼이 점프인지'만 정한다.
    // JumpProvider 의 입력을 이 컴포넌트로 돌려놓는다 (CombatAssetsBuilder.TunePlayerControls).
    // XRI 기본은 오른손 A 인데, 오른손으로 총을 들면 A 는 탄창멈치(TempRifle)라서 뛸 때마다 탄창이 빠졌다 (11/11 헤드셋).
    // 그래서 처음엔 점프를 껐는데, 창턱(0.9m · 상가 창 0.5m)을 넘어 건물에 드나들려면 점프가 있어야 한다 (11/12 사용자) → 버튼을 옮긴다:
    //   · 총을 들지 않은 손의 A/X — 총 든 손의 A/X 는 그대로 탄창멈치. 총이 없으면 두 손 다 점프 (11/12 사용자: "왼손으로 뛰어넘게")
    //   · 오른 스틱(회전 스틱) 누르기 — 왼손 X 는 이동 스틱과 같은 엄지라, 앞으로 가면서 뛰려면 스틱에서 엄지를 떼야 한다.
    //     스틱 누르기는 왼 엄지가 이동 스틱을 민 채로 뛸 수 있다. 헤드셋에서 써 보고 하나만 남겨도 된다 (아래 두 칸)
    // 공중에서 새로 누른 것은 쓰지 않는다 (11/12 사용자: "점프를 할 때 더블점프가 된다") — 공중 점프 횟수는 리그에서 0 으로 했고
    // (CombatAssetsBuilder.TunePlayerControls), 여기서는 공중에서 누른 채 내려오다 땅에 닿는 순간 한 번 더 뛰는 것을 막는다: 뗄 때까지 안 누른 것으로 본다
    public class JumpButton : MonoBehaviour, IXRInputButtonReader
    {
        [Tooltip("총을 들지 않은 손의 A/X 로 뛴다 (총 든 손의 A/X 는 탄창멈치)")]
        [SerializeField] bool m_FreeHandPrimaryButton = true;

        [Tooltip("오른 스틱(회전 스틱)을 눌러서 뛴다 — 이동 스틱에서 엄지를 떼지 않아도 된다")]
        [SerializeField] bool m_RightStickClick = true;

        InputAction m_LeftPrimary;
        InputAction m_RightPrimary;
        InputAction m_RightStick;
        NearFarInteractor m_LeftHand;
        NearFarInteractor m_RightHand;
        JumpProvider m_Jump;
        int m_Frame = -1;
        bool m_Held;
        bool m_WasHeld;
        bool m_RawHeld;       // 버튼이 실제로 눌려 있는지 (공중에서 누른 것 포함)
        bool m_PressSpent;    // 지금 누르고 있는 건 공중에서 누른 것이라 쓰지 않는다

        // 테스트(Unity CLI)가 누르고 있는 것 — 손 버튼과 같은 길로 JumpProvider 에 간다
        public bool ManualHeld { get; set; }

        void Awake()
        {
            m_Jump = GetComponent<JumpProvider>();
            XROrigin rig = GetComponentInParent<XROrigin>();
            if (rig == null)
                return;
            foreach (NearFarInteractor hand in rig.GetComponentsInChildren<NearFarInteractor>(true))
            {
                if (hand.handedness == InteractorHandedness.Left)
                    m_LeftHand = hand;
                else if (hand.handedness == InteractorHandedness.Right)
                    m_RightHand = hand;
            }
        }

        void OnEnable()
        {
            m_LeftPrimary = new InputAction("Jump Left Primary", InputActionType.Button, "<XRController>{LeftHand}/{PrimaryButton}");
            m_RightPrimary = new InputAction("Jump Right Primary", InputActionType.Button, "<XRController>{RightHand}/{PrimaryButton}");
            m_RightStick = new InputAction("Jump Right Stick Click", InputActionType.Button, "<XRController>{RightHand}/{Primary2DAxisClick}");
            m_LeftPrimary.Enable();
            m_RightPrimary.Enable();
            m_RightStick.Enable();
        }

        void OnDisable()
        {
            m_LeftPrimary?.Dispose();
            m_RightPrimary?.Dispose();
            m_RightStick?.Dispose();
            m_LeftPrimary = m_RightPrimary = m_RightStick = null;
            m_Held = m_WasHeld = m_RawHeld = m_PressSpent = false;
        }

        void Update()
        {
            Sample();
        }

        // 이번 프레임에 점프 버튼이 눌려 있는지 — 한 프레임에 한 번만 읽어서, 누가 먼저 묻든 '방금 눌렀다 · 방금 뗐다'가 같게 나온다
        void Sample()
        {
            if (m_Frame == Time.frameCount)
                return;
            m_Frame = Time.frameCount;
            m_WasHeld = m_Held;
            bool held = ManualHeld;
            if (m_LeftPrimary != null)
            {
                if (m_FreeHandPrimaryButton)
                    held |= (m_LeftPrimary.IsPressed() && !HoldsRifle(m_LeftHand)) || (m_RightPrimary.IsPressed() && !HoldsRifle(m_RightHand));
                if (m_RightStickClick)
                    held |= m_RightStick.IsPressed();
            }
            // 막 누른 순간에 뛸 수 없는 상태(공중)면 이번 누름은 뗄 때까지 버린다. 땅 · 발 뗀 직후(코요테 타임)는 그대로 뛴다
            if (held && !m_RawHeld)
                m_PressSpent = m_Jump != null && m_Jump.isActiveAndEnabled && m_Jump.gravityProvider != null && !m_Jump.CanJump();
            if (!held)
                m_PressSpent = false;
            // 공통 일시정지 메뉴(B)가 떠 있는 동안 누른 것은 쓰지 않는다 — 누른 채 메뉴를 닫아도 뗄 때까지 버린다
            if (held && MilTrainSimVR.B.CommonMenu.CommonPauseMenu.IsPaused)
                m_PressSpent = true;
            m_RawHeld = held;
            m_Held = held && !m_PressSpent;
        }

        // 그 손이 총의 손잡이를 쥐고 있는지 — 그 손의 A/X · B/Y 는 총이 쓴다 (TempRifle.IsGripHandDevice 와 같은 기준)
        static bool HoldsRifle(NearFarInteractor hand)
        {
            return hand != null && hand.hasSelection && hand.firstInteractableSelected.transform.GetComponent<TempRifle>() != null;
        }

        public bool ReadIsPerformed()
        {
            Sample();
            return m_Held;
        }

        public bool ReadWasPerformedThisFrame()
        {
            Sample();
            return m_Held && !m_WasHeld;
        }

        public bool ReadWasCompletedThisFrame()
        {
            Sample();
            return !m_Held && m_WasHeld;
        }

        public float ReadValue()
        {
            return ReadIsPerformed() ? 1f : 0f;
        }

        public bool TryReadValue(out float value)
        {
            value = ReadValue();
            return true;
        }
    }
}
