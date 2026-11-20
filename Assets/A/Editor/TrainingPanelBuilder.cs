using MilTrainSimVR.A.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace MilTrainSimVR.A.EditorTools
{
    // 작업대 패널 프리팹(Assets/A/Prefabs/TrainingPanel.prefab)을 만든다. 배치·크기를 바꾸려면 여기 값을 고치고 다시 만든다.
    // 월드 공간 캔버스: 1 단위 = 1 mm (스케일 0.001) → 900 × 560 = 0.9 × 0.56 m.
    // 레이(Near-Far Interactor)로 누르도록 TrackedDeviceGraphicRaycaster 를 단다. 씬에는 XRUIInputModule 이 있는 EventSystem 이 있어야 한다.
    // 주의: 다시 만들면 프리팹 안 오브젝트 ID 가 바뀌어 씬의 버튼 연결(AssemblyTrainingFlow)이 끊길 수 있다 — 다시 만든 뒤 연결을 확인한다.
    static class TrainingPanelBuilder
    {
        const string k_Path = "Assets/A/Prefabs/TrainingPanel.prefab";

        static readonly Vector2 k_Size = new Vector2(900f, 560f);
        const float k_Padding = 30f;

        // 색은 B UI 스타일 가이드(Docs/B/1117) 값 — Background · Text · Button (11/19). 안내 칸 글자색은 TrainingPanel 이 실행 중에 정한다 (안내 Text · 알림 Notice)
        static readonly Color k_Background = new Color(0.07f, 0.09f, 0.11f, 1f);
        static readonly Color k_TextColor = new Color(0.94f, 0.95f, 0.96f);
        static readonly Color k_ButtonColor = new Color(0.16f, 0.42f, 0.70f);

        [MenuItem("A/만들기/작업대 패널 프리팹")]
        static void Build()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var root = new GameObject("TrainingPanel", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(TrackedDeviceGraphicRaycaster), typeof(TrainingPanel));
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = k_Size;
            rect.localScale = Vector3.one * 0.001f;
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            root.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;   // 월드 공간 글자가 흐리지 않게

            var background = Child("Background", root.transform, typeof(Image));
            Stretch(background, 0f, 0f, 0f, 0f);
            background.GetComponent<Image>().color = k_Background;

            var header = MakeText("Header", root.transform, font, 30, k_TextColor, TextAnchor.MiddleLeft, FontStyle.Normal);
            Band(header, k_Padding, 50f);
            var title = MakeText("Title", root.transform, font, 52, k_TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            Band(title, k_Padding + 60f, 130f);
            // 긴 제목도 칸 밖으로 잘리지 않게 줄인다 (11/19)
            var titleText = title.GetComponent<Text>();
            titleText.resizeTextForBestFit = true;
            titleText.resizeTextMinSize = 34;
            titleText.resizeTextMaxSize = 52;
            var message = MakeText("Message", root.transform, font, 34, k_TextColor, TextAnchor.UpperLeft, FontStyle.Normal);
            Band(message, k_Padding + 200f, 200f);
            // 결과 화면처럼 줄이 많으면 글자를 줄여 칸 안에 넣는다
            var messageText = message.GetComponent<Text>();
            messageText.resizeTextForBestFit = true;
            messageText.resizeTextMinSize = 22;
            messageText.resizeTextMaxSize = 34;

            var row = Child("Buttons", root.transform, typeof(HorizontalLayoutGroup));
            var rowRect = (RectTransform)row.transform;
            rowRect.anchorMin = new Vector2(0f, 0f);
            rowRect.anchorMax = new Vector2(1f, 0f);
            rowRect.pivot = new Vector2(0.5f, 0f);
            rowRect.offsetMin = new Vector2(k_Padding, k_Padding);
            rowRect.offsetMax = new Vector2(-k_Padding, k_Padding + 90f);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 24f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            // 이름은 흐름 쪽 버튼 칸과 맞춘다. 글자는 흐름이 실행 중에 AssemblyTexts 에서 넣는다
            MakeButton("LearningButton", row.transform, font);
            MakeButton("EvaluationButton", row.transform, font);
            MakeButton("RetryButton", row.transform, font);
            MakeButton("HomeButton", row.transform, font);

            var panel = root.GetComponent<TrainingPanel>();
            var so = new SerializedObject(panel);
            so.FindProperty("m_Header").objectReferenceValue = header.GetComponent<Text>();
            so.FindProperty("m_Title").objectReferenceValue = title.GetComponent<Text>();
            so.FindProperty("m_Message").objectReferenceValue = message.GetComponent<Text>();
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, k_Path);
            Object.DestroyImmediate(root);
            Debug.Log($"[A] 작업대 패널 프리팹 저장: {k_Path}");
        }

        static GameObject Child(string name, Transform parent, params System.Type[] components)
        {
            var go = new GameObject(name, typeof(RectTransform));
            foreach (var type in components)
                go.AddComponent(type);
            go.transform.SetParent(parent, false);
            return go;
        }

        static void Stretch(GameObject go, float left, float right, float top, float bottom)
        {
            var r = (RectTransform)go.transform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(left, bottom);
            r.offsetMax = new Vector2(-right, -top);
        }

        // 위에서 top 만큼 내려와 height 높이의 가로 띠
        static void Band(GameObject go, float top, float height)
        {
            var r = (RectTransform)go.transform;
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.offsetMin = new Vector2(k_Padding, -top - height);
            r.offsetMax = new Vector2(-k_Padding, -top);
        }

        static GameObject MakeText(string name, Transform parent, Font font, int size, Color color, TextAnchor anchor, FontStyle style)
        {
            var go = Child(name, parent, typeof(Text));
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            text.text = name;
            return go;
        }

        static void MakeButton(string name, Transform parent, Font font)
        {
            var go = Child(name, parent, typeof(Image), typeof(Button), typeof(LayoutElement));
            go.GetComponent<Image>().color = k_ButtonColor;
            var element = go.GetComponent<LayoutElement>();
            element.preferredWidth = 250f;
            var label = MakeText("Label", go.transform, font, 34, k_TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(label, 8f, 8f, 4f, 4f);
        }
    }
}
