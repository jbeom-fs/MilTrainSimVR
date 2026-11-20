using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MilTrainSimVR.A.Range
{
    // 결과 화면의 표적지 그림 (사로 기획 3절) — 25 m 앞 표적은 VR에서 탄착이 안 보이므로 패널에 그려 보여 준다.
    // 사로 패널(TrainingPanel 캔버스)에 붙인다. 실행 중에 패널 오른쪽 위에 표적지 그림(RawImage)과 탄착 점을 만든다.
    // 점 색은 조마다 다르다 (RangeTexts.GroupColors). 표적지 밖(빗나감)은 그리지 않는다. 그림 아래에 작은 글씨(평가 기준)를 둘 수 있다.
    public class TargetMap : MonoBehaviour
    {
        [Tooltip("표적지 그림 — 씬의 표적지와 같은 그림")]
        [SerializeField] Texture m_Sheet;

        [Tooltip("그림이 나타내는 표적지 크기 (가로, 세로 cm) — 씬의 표적지 크기와 같게")]
        [SerializeField] Vector2 m_SheetSizeCm = new Vector2(30f, 30f);

        [Tooltip("그림 속 조준점 자리 (0 ~ 1, 왼쪽 아래 = 0,0) — 씬 표적지의 조준점(ZeroTarget.m_AimPoint)과 같게")]
        [SerializeField] Vector2 m_AimUv = new Vector2(0.5f, 0.5f);

        [Tooltip("패널 위 그림 폭 (패널 단위, 1 = 1 mm) — 높이는 표적지 비율대로")]
        [SerializeField] float m_Size = 240f;

        [Tooltip("패널 오른쪽·위 끝에서 떨어진 거리 (패널 단위)")]
        [SerializeField] Vector2 m_Margin = new Vector2(30f, 100f);

        [SerializeField] float m_DotSize = 12f;

        [Tooltip("그림 아래 작은 글씨 칸 높이 (패널 단위)")]
        [SerializeField] float m_CaptionHeight = 110f;

        RectTransform m_Root;
        Text m_Caption;
        Sprite m_Dot;
        readonly List<GameObject> m_Dots = new List<GameObject>();

        // 표적지 그림이 차지하는 폭 + 여백 — 패널 글자 칸을 이만큼 줄인다
        public float Inset => m_Size + m_Margin.x;

        void Awake()
        {
            var go = new GameObject("TargetMap", typeof(RectTransform), typeof(RawImage));
            m_Root = (RectTransform)go.transform;
            m_Root.SetParent(transform, false);
            m_Root.anchorMin = m_Root.anchorMax = m_Root.pivot = Vector2.one;
            m_Root.anchoredPosition = -m_Margin;
            m_Root.sizeDelta = new Vector2(m_Size, m_Size * m_SheetSizeCm.y / m_SheetSizeCm.x);
            var image = go.GetComponent<RawImage>();
            image.texture = m_Sheet;
            image.raycastTarget = false;
            m_Dot = MakeDot();

            var caption = new GameObject("Caption", typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)caption.transform;
            rect.SetParent(m_Root, false);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -8f);
            rect.sizeDelta = new Vector2(40f, m_CaptionHeight);   // 그림보다 양옆 20 씩 넓게
            m_Caption = caption.GetComponent<Text>();
            m_Caption.fontSize = 17;
            m_Caption.color = new Color(0.70f, 0.75f, 0.80f);
            m_Caption.alignment = TextAnchor.UpperLeft;
            m_Caption.raycastTarget = false;
            go.SetActive(false);
        }

        // caption: 그림 아래 작은 글씨 (빈 문자열이면 없음)
        public void Show(IReadOnlyList<ShotGroup> groups, string caption = "")
        {
            Clear();
            // 패널이 실행 중에 바꾼 한글 글꼴을 같이 쓴다
            if (m_Caption.font == null || m_Caption.font.name == "LegacyRuntime")
                foreach (Text text in GetComponentsInChildren<Text>(true))
                    if (text != m_Caption && text.font != null) { m_Caption.font = text.font; break; }
            m_Caption.text = caption;
            float perCm = m_Size / m_SheetSizeCm.x;
            Vector2 min = -Vector2.Scale(m_AimUv, m_SheetSizeCm);                       // 조준점 기준 표적지 왼쪽 아래 (cm)
            Vector2 max = Vector2.Scale(Vector2.one - m_AimUv, m_SheetSizeCm);          // 오른쪽 위
            for (int g = 0; g < groups.Count; g++)
            {
                Color color = RangeTexts.GroupColor(g);
                foreach (Vector2 cm in groups[g].Hits)
                {
                    if (cm.x < min.x || cm.x > max.x || cm.y < min.y || cm.y > max.y)
                        continue;
                    var dot = new GameObject("Hit", typeof(RectTransform), typeof(Image));
                    var rect = (RectTransform)dot.transform;
                    rect.SetParent(m_Root, false);
                    rect.anchorMin = rect.anchorMax = m_AimUv;   // 조준점
                    rect.sizeDelta = new Vector2(m_DotSize, m_DotSize);
                    rect.anchoredPosition = cm * perCm;
                    var image = dot.GetComponent<Image>();
                    image.sprite = m_Dot;
                    image.color = color;
                    image.raycastTarget = false;
                    m_Dots.Add(dot);
                }
            }
            m_Root.gameObject.SetActive(true);
        }

        public void Hide()
        {
            Clear();
            if (m_Root != null)
                m_Root.gameObject.SetActive(false);
        }

        void Clear()
        {
            foreach (GameObject dot in m_Dots)
                Destroy(dot);
            m_Dots.Clear();
        }

        // 동그라미 점 — 기본 UI 스프라이트는 실행 중에 불러올 수 없어 코드로 만든다
        static Sprite MakeDot()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size / 2f - 1f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = new Vector2(x - size / 2f + 0.5f, y - size / 2f + 0.5f).magnitude;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - d + 0.5f)));
                }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
