using UnityEngine;
using UnityEngine.UI;

namespace MilTrainSimVR.A.Common
{
    // 작업대 패널 — 월드 공간 uGUI. 머리줄(모드 · 진행 · 타이머), 제목, 안내 문구를 보여 준다.
    // 안내 칸 하나에 두 가지를 보여 준다 (11/19): 단계 안내(SetGuide — 흰 글자, 바꿀 때까지) · 알림(ShowMessage — 노란 글자).
    // 알림이 있으면 알림을, 알림이 사라지면 단계 안내를 다시 보여 준다. 색은 B UI 스타일 가이드(1117)의 Text · Notice.
    // 무엇을 보여 줄지는 교육 흐름이 정한다. 버튼도 흐름이 직접 연결하고 켜고 끈다 — 패널은 버튼이 무엇을 하는지 모른다.
    // 글꼴은 OS 글꼴(맑은 고딕)을 실행 중에 만들어 쓴다 (PC VR 기준). Play 전 에디터에서는 한글이 안 보일 수 있다.
    // 머리줄 글자는 보이는 값이 바뀔 때만 새로 만든다 — 매 프레임 문자열을 만들면 GC 가 쌓여 VR 에서 멈칫한다.
    public class TrainingPanel : MonoBehaviour
    {
        [SerializeField] Text m_Header;
        [SerializeField] Text m_Title;
        [SerializeField] Text m_Message;

        [Tooltip("단계 안내 글자 색 (B 스타일 가이드 Text)")]
        [SerializeField] Color m_GuideColor = new Color(0.94f, 0.95f, 0.96f);

        [Tooltip("알림 글자 색 (B 스타일 가이드 Notice)")]
        [SerializeField] Color m_NoticeColor = new Color(1.00f, 0.78f, 0.35f);

        [Tooltip("한글 표시용 OS 글꼴")]
        [SerializeField] string[] m_OsFontNames = { "Malgun Gothic", "맑은 고딕" };

        const string k_Separator = "  ·  ";

        string m_Mode = "";
        int m_Step = -1;
        int m_StepCount;
        int m_TimerSeconds = -1;
        string m_Selector = "";
        string m_Guide = "";
        string m_Notice = "";
        float m_MessageHideTime;
        float m_TitleRight;     // 제목·안내 칸의 처음 오른쪽 끝 — 오른쪽에 그림을 둘 때 줄였다가 되돌린다
        float m_MessageRight;
        RectTransform m_Buttons;   // 버튼 줄 (프리팹의 Buttons) — 없으면 그대로 둔다
        float m_ButtonsRight;
        float m_TitleTop;          // 제목 칸의 처음 위쪽 끝 — 단계 칸 줄을 보이면 그 아래로 내린다
        Font m_Font;

        // 단계 칸 줄 (11/19, B 교육 보드처럼) — 머리줄 아래 번호 칸. 완료 초록 · 지금 파랑 · 다음 회색 (B 스타일 가이드 StepDone · StepCurrent · StepAhead)
        // 프리팹·씬을 바꾸지 않으려고 처음 부를 때 만든다
        static readonly Color k_StepDone = new Color(0.25f, 0.62f, 0.36f);
        static readonly Color k_StepCurrent = new Color(0.16f, 0.42f, 0.70f);
        static readonly Color k_StepAhead = new Color(0.22f, 0.25f, 0.28f);
        const float k_StepRowTop = 88f;      // 패널 위에서 (머리줄 아래)
        const float k_StepRowHeight = 40f;
        const float k_StepGap = 6f;
        const float k_StepGroupGap = 24f;    // 묶음 사이 (분해 | 결합)
        const float k_StepTitleGap = 8f;     // 칸 줄과 제목 사이
        RectTransform m_StepRow;
        readonly System.Collections.Generic.List<Image> m_StepBoxes = new System.Collections.Generic.List<Image>();
        int m_StepRowCount = -1;
        int m_StepRowBreak;

        void Awake()
        {
            var font = Font.CreateDynamicFontFromOSFont(m_OsFontNames, 48);
            if (font != null)
            {
                // 버튼 글자까지 패널 아래 모든 글자에 같은 글꼴
                foreach (var text in GetComponentsInChildren<Text>(true))
                    text.font = font;
            }
            m_Font = font != null ? font : m_Title.font;
            RefreshMessage();
            m_TitleTop = m_Title.rectTransform.offsetMax.y;
            m_TitleRight = m_Title.rectTransform.offsetMax.x;
            m_MessageRight = m_Message.rectTransform.offsetMax.x;
            m_Buttons = transform.Find("Buttons") as RectTransform;
            if (m_Buttons != null)
                m_ButtonsRight = m_Buttons.offsetMax.x;
            RefreshHeader();
        }

        // 제목·안내 칸과 버튼 줄의 오른쪽을 inset 만큼 비운다 (패널 단위, 1 = 1 mm). 0 이면 처음대로 — 사로 결과의 표적지 그림 자리
        public void SetRightInset(float inset)
        {
            SetRight(m_Title.rectTransform, m_TitleRight - inset);
            SetRight(m_Message.rectTransform, m_MessageRight - inset);
            if (m_Buttons != null)
                SetRight(m_Buttons, m_ButtonsRight - inset);
        }

        static void SetRight(RectTransform rect, float right)
        {
            Vector2 max = rect.offsetMax;
            max.x = right;
            rect.offsetMax = max;
        }

        // 단계 칸 줄 — count 칸에 1부터 번호, current(0부터) 앞은 완료 · current 는 지금 · 뒤는 다음.
        // groupBreak > 0 이면 그 번호 칸 앞을 조금 더 띄운다 (예: 6 → 1~6 | 7~12). count <= 0 이면 숨기고 제목을 처음 자리로
        public void SetSteps(int count, int current, int groupBreak = 0)
        {
            if (count <= 0)
            {
                if (m_StepRow != null)
                    m_StepRow.gameObject.SetActive(false);
                SetTitleTop(m_TitleTop);
                return;
            }
            if (m_StepRow == null)
                CreateStepRow();
            if (count != m_StepRowCount || groupBreak != m_StepRowBreak)
                LayoutSteps(count, groupBreak);
            for (int i = 0; i < m_StepBoxes.Count; i++)
                m_StepBoxes[i].color = i < current ? k_StepDone : i == current ? k_StepCurrent : k_StepAhead;
            m_StepRow.gameObject.SetActive(true);
            SetTitleTop(-(k_StepRowTop + k_StepRowHeight + k_StepTitleGap));
        }

        void SetTitleTop(float top)
        {
            Vector2 max = m_Title.rectTransform.offsetMax;
            max.y = top;
            m_Title.rectTransform.offsetMax = max;
        }

        void CreateStepRow()
        {
            var go = new GameObject("StepRow", typeof(RectTransform));
            m_StepRow = (RectTransform)go.transform;
            m_StepRow.SetParent(transform, false);
            m_StepRow.SetSiblingIndex(m_Header.transform.GetSiblingIndex() + 1);
            // 가로는 머리줄과 같은 여백
            RectTransform header = m_Header.rectTransform;
            m_StepRow.anchorMin = new Vector2(0f, 1f);
            m_StepRow.anchorMax = new Vector2(1f, 1f);
            m_StepRow.pivot = new Vector2(0.5f, 1f);
            m_StepRow.offsetMin = new Vector2(header.offsetMin.x, -(k_StepRowTop + k_StepRowHeight));
            m_StepRow.offsetMax = new Vector2(header.offsetMax.x, -k_StepRowTop);
        }

        void LayoutSteps(int count, int groupBreak)
        {
            m_StepRowCount = count;
            m_StepRowBreak = groupBreak;
            while (m_StepBoxes.Count < count)
                m_StepBoxes.Add(CreateStepBox(m_StepBoxes.Count));
            for (int i = 0; i < m_StepBoxes.Count; i++)
                m_StepBoxes[i].gameObject.SetActive(i < count);

            bool grouped = groupBreak > 0 && groupBreak < count;
            float width = m_StepRow.rect.width;
            float gaps = (count - 1) * k_StepGap + (grouped ? k_StepGroupGap - k_StepGap : 0f);
            float box = (width - gaps) / count;
            float x = 0f;
            for (int i = 0; i < count; i++)
            {
                if (grouped && i == groupBreak)
                    x += k_StepGroupGap - k_StepGap;
                var rect = m_StepBoxes[i].rectTransform;
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.offsetMin = new Vector2(x, 0f);
                rect.offsetMax = new Vector2(x + box, 0f);
                x += box + k_StepGap;
            }
        }

        Image CreateStepBox(int index)
        {
            var go = new GameObject($"Step_{index + 1}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(m_StepRow, false);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(go.transform, false);
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            var label = labelObject.GetComponent<Text>();
            label.font = m_Font;
            label.fontSize = 24;
            label.color = m_GuideColor;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            label.text = (index + 1).ToString();
            return image;
        }

        void Update()
        {
            if (m_MessageHideTime > 0f && Time.time >= m_MessageHideTime)
            {
                m_Notice = "";
                m_MessageHideTime = 0f;
                RefreshMessage();
            }
        }

        public void SetTitle(string text) => m_Title.text = text;

        // 단계 안내 — 바꿀 때까지 보인다. 알림이 떠 있으면 알림이 사라진 뒤에 보인다
        public void SetGuide(string text)
        {
            m_Guide = text ?? "";
            RefreshMessage();
        }

        // 알림. seconds <= 0 이면 계속 표시. 빈 문자열이면 알림을 지우고 단계 안내로 돌아간다
        public void ShowMessage(string text, float seconds)
        {
            m_Notice = text ?? "";
            m_MessageHideTime = m_Notice.Length > 0 && seconds > 0f ? Time.time + seconds : 0f;
            RefreshMessage();
        }

        void RefreshMessage()
        {
            bool notice = m_Notice.Length > 0;
            m_Message.text = notice ? m_Notice : m_Guide;
            m_Message.color = notice ? m_NoticeColor : m_GuideColor;
        }

        // 빈 문자열이면 머리줄에서 뺀다
        public void SetMode(string mode)
        {
            if (m_Mode == mode) return;
            m_Mode = mode ?? "";
            RefreshHeader();
        }

        // step 은 1부터. step < 1 이면 머리줄에서 뺀다
        public void SetProgress(int step, int count)
        {
            if (m_Step == step && m_StepCount == count) return;
            m_Step = step;
            m_StepCount = count;
            RefreshHeader();
        }

        // 지금 조정간 상태 (예: "조정간 단발") — 총을 다루는 구간에 머리줄 끝에 늘 보여 준다 (11/03 헤드셋: 단발에서 한 번 더 눌러 연발이 된 걸 몰랐다).
        // 빈 문자열이면 뺀다
        public void SetSelector(string text)
        {
            if (m_Selector == text) return;
            m_Selector = text ?? "";
            RefreshHeader();
        }

        // seconds < 0 이면 머리줄에서 뺀다. 초 단위가 바뀔 때만 다시 그린다
        public void SetTimer(float seconds)
        {
            int whole = seconds < 0f ? -1 : (int)seconds;
            if (m_TimerSeconds == whole) return;
            m_TimerSeconds = whole;
            RefreshHeader();
        }

        void RefreshHeader()
        {
            string header = m_Mode;
            if (m_Step >= 1)
                header = Join(header, $"{m_Step} / {m_StepCount}");
            if (m_TimerSeconds >= 0)
                header = Join(header, $"{m_TimerSeconds / 60:00}:{m_TimerSeconds % 60:00}");
            if (m_Selector.Length > 0)
                header = Join(header, m_Selector);
            m_Header.text = header;
        }

        static string Join(string a, string b) => a.Length == 0 ? b : a + k_Separator + b;
    }
}
