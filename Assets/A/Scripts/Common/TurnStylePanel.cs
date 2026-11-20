using UnityEngine;
using UnityEngine.UI;

namespace MilTrainSimVR.A.Common
{
    // 허브 안내판 아래 판 — 화면 회전 방식 부드럽게 / 끊어 돌기 두 버튼 (11/17). 한쪽을 누르면 그쪽이 켜지고 다른 쪽은 꺼진다 (TurnStyle 에 저장).
    // 아직 안 골랐으면 기본 끊어 돌기가 켜진 모습 (11/18 — 그 전엔 둘 다 꺼진 모습 + "씬마다 원래 설정"). 모양은 사격 방식 판(RifleStylePanel)과 같다.
    // 만드는 곳: 메뉴 A/만들기/허브 사격 방식 안내판 (HubRifleStyleBuilder — 두 판을 같이 짓는다).
    public class TurnStylePanel : MonoBehaviour
    {
        [SerializeField] Button m_SmoothButton;
        [SerializeField] Button m_SnapButton;
        [SerializeField] GameObject m_SmoothCheck;
        [SerializeField] GameObject m_SnapCheck;
        [SerializeField] Text m_Status;

        [SerializeField] Color m_OnColor = new Color(0.22f, 0.55f, 0.32f);
        [SerializeField] Color m_OffColor = new Color(0.22f, 0.24f, 0.27f);

        void Awake()
        {
            m_SmoothButton.onClick.AddListener(() => TurnStyle.Current = TurnStyle.Style.Smooth);
            m_SnapButton.onClick.AddListener(() => TurnStyle.Current = TurnStyle.Style.Snap);
        }

        void OnEnable()
        {
            TurnStyle.Changed += Show;
            Show(TurnStyle.Current);
        }

        void OnDisable() => TurnStyle.Changed -= Show;

        // 씬에 저장되는 모습 — 기본값(끊어 돌기, 11/18)
        public void ShowDefault() => Show(TurnStyle.Style.Snap);

        void Show(TurnStyle.Style style)
        {
            Paint(m_SmoothButton, style == TurnStyle.Style.Smooth);
            Paint(m_SnapButton, style == TurnStyle.Style.Snap);
            if (m_SmoothCheck != null) m_SmoothCheck.SetActive(style == TurnStyle.Style.Smooth);
            if (m_SnapCheck != null) m_SnapCheck.SetActive(style == TurnStyle.Style.Snap);
            if (m_Status != null)
                m_Status.text = style switch
                {
                    TurnStyle.Style.Smooth => "지금: 부드럽게 돌기",
                    TurnStyle.Style.Snap => "지금: 끊어 돌기",
                    _ => "아직 고르지 않음 — 훈련마다 기본 방식을 씁니다",
                };
        }

        void Paint(Button button, bool on)
        {
            var image = button.targetGraphic as Image;
            if (image != null)
                image.color = on ? m_OnColor : m_OffColor;
            var label = button.GetComponentInChildren<Text>();
            if (label != null)
                label.color = on ? Color.white : new Color(0.75f, 0.77f, 0.80f);
        }
    }
}
