using UnityEngine;
using UnityEngine.UI;

namespace MilTrainSimVR.A.Common
{
    // 허브 안내판 — 조준 방식 A(확대 조준) / C(가늠자 조준) 두 버튼 (11/17). 한쪽을 누르면 그쪽이 켜지고 다른 쪽은 꺼진다 (RifleStyle 에 저장).
    // 켜진 버튼 = 밝은 색 + "✓ 선택됨", 꺼진 버튼 = 어두운 색. 레이로 누른다 (씬에 XRUIInputModule EventSystem, 캔버스에 TrackedDeviceGraphicRaycaster).
    // 만드는 곳: 메뉴 A/만들기/허브 사격 방식 안내판 (HubRifleStyleBuilder).
    public class RifleStylePanel : MonoBehaviour
    {
        [SerializeField] Button m_ButtonA;
        [SerializeField] Button m_ButtonC;

        [Tooltip("버튼 아래 '선택됨' 표시 — 켜진 쪽만 보인다")]
        [SerializeField] GameObject m_CheckA;
        [SerializeField] GameObject m_CheckC;

        [Tooltip("지금 고른 방식을 적는 줄")]
        [SerializeField] Text m_Status;

        [SerializeField] Color m_OnColor = new Color(0.22f, 0.55f, 0.32f);
        [SerializeField] Color m_OffColor = new Color(0.22f, 0.24f, 0.27f);

        void Awake()
        {
            m_ButtonA.onClick.AddListener(() => RifleStyle.Current = RifleStyle.Style.A);
            m_ButtonC.onClick.AddListener(() => RifleStyle.Current = RifleStyle.Style.C);
        }

        void OnEnable()
        {
            RifleStyle.Changed += Show;
            Show(RifleStyle.Current);
        }

        void OnDisable() => RifleStyle.Changed -= Show;

        // 씬에 저장되는 모습 — 만들기 메뉴가 기본값(C, 11/18)으로 칠해 둔다 (Play 전에도 켜진 쪽이 보이게)
        public void ShowDefault() => Show(RifleStyle.Style.C);

        void Show(RifleStyle.Style style)
        {
            bool a = style == RifleStyle.Style.A;
            Paint(m_ButtonA, a);
            Paint(m_ButtonC, !a);
            if (m_CheckA != null) m_CheckA.SetActive(a);
            if (m_CheckC != null) m_CheckC.SetActive(!a);
            if (m_Status != null)
                m_Status.text = a ? "지금: 확대 조준" : "지금: 가늠자 조준";
        }

        void Paint(Button button, bool on)
        {
            var image = button.targetGraphic as Image;
            if (image != null)
                image.color = on ? m_OnColor : m_OffColor;
            // 눌림 · 겨눔 색이 바탕색 위에 곱해진다 — 꺼진 쪽도 겨누면 밝아지게 그대로 둔다
            var label = button.GetComponentInChildren<Text>();
            if (label != null)
                label.color = on ? Color.white : new Color(0.75f, 0.77f, 0.80f);
        }
    }
}
