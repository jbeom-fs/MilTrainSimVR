using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace MilTrainSimVR.B.CommonMenu
{
    // The prefab owns the font/material settings. Its three-button view is built once; no content scene references are stored.
    internal sealed class CommonMenuPanel
    {
        internal const int UILayer = 5; // Unity's built-in UI layer; shared with the menu ray's graphic mask.
        internal const int SortingOrder = 31000;
        readonly GameObject root;
        readonly Canvas canvas;
        readonly Button resume;
        readonly Button main;
        readonly Button quit;
        readonly TMP_Text notice;
        readonly Material textMaterial;
        static readonly Color TextColor = new Color(0.94f, 0.95f, 0.96f);

        public CommonMenuPanel(Transform parent, TMP_FontAsset font, Material surface, Material fontOverlay, Action resume, Action goMain, Action quit)
        {
            root = new GameObject("PausePanel", typeof(RectTransform));
            root.layer = UILayer;
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(720f, 440f);
            rect.localScale = Vector3.one * 0.001f;
            canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;
            var raycaster = root.AddComponent<TrackedDeviceGraphicRaycaster>();
            raycaster.checkFor2DOcclusion = false;
            raycaster.checkFor3DOcclusion = false;
            raycaster.ignoreReversedGraphics = true;
            var background = root.AddComponent<Image>();
            background.color = new Color(0.07f, 0.09f, 0.11f);
            background.material = surface;
            background.raycastTarget = true;
            textMaterial = fontOverlay != null ? new Material(fontOverlay) : new Material(font.material);
            Label("Title", TrainingSubtitles.MenuTitle, new Vector2(0f, 170f), new Vector2(660f, 60f), 34, font);
            this.resume = Button("Continue", TrainingSubtitles.Continue, 70f, new Color(0.16f, 0.42f, 0.7f), resume, font, surface);
            main = Button("Main", TrainingSubtitles.GoMain, -20f, new Color(0.16f, 0.42f, 0.7f), goMain, font, surface);
            this.quit = Button("Quit", TrainingSubtitles.Quit, -110f, new Color(0.62f, 0.2f, 0.2f), quit, font, surface);
            notice = Label("Notice", string.Empty, new Vector2(0f, -185f), new Vector2(660f, 40f), 22, font);
            notice.color = new Color(1f, 0.78f, 0.35f);
        }

        TMP_Text Label(string name, string value, Vector2 position, Vector2 size, int fontSize, TMP_FontAsset font, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = UILayer;
            go.transform.SetParent(parent != null ? parent : root.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSharedMaterial = textMaterial;
            text.fontSize = fontSize;
            text.enableAutoSizing = false;
            text.alignment = TextAlignmentOptions.Center;
            text.color = TextColor;
            text.raycastTarget = false;
            text.text = value;
            return text;
        }

        Button Button(string name, string text, float y, Color color, Action clicked, TMP_FontAsset font, Material surface)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = UILayer;
            go.transform.SetParent(root.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(360f, 64f);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.material = surface;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = colors.selectedColor = new Color(0.9607843f, 0.9607843f, 0.9607843f, 1f);
            colors.pressedColor = new Color(0.78431374f, 0.78431374f, 0.78431374f, 1f);
            colors.disabledColor = new Color(0.78431374f, 0.78431374f, 0.78431374f, 0.5019608f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.1f;
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => clicked());
            Label("Label", text, Vector2.zero, rect.sizeDelta, 26, font, go.transform);
            return button;
        }

        public void Show(Camera head, float distance, bool mainEnabled)
        {
            var forward = Vector3.ProjectOnPlane(head.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.ProjectOnPlane(head.transform.up, Vector3.up);
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;
            root.transform.SetPositionAndRotation(head.transform.position + forward.normalized * distance, Quaternion.LookRotation(forward.normalized, Vector3.up));
            canvas.worldCamera = head;
            resume.interactable = quit.interactable = true;
            main.interactable = mainEnabled;
            notice.text = string.Empty;
            root.SetActive(true);
        }

        public void Notice(string message) => notice.text = message;
        public void WaitForRelease()
        {
            resume.interactable = main.interactable = quit.interactable = false;
            Notice(TrainingSubtitles.ReleaseToResume);
        }
        public void Hide()
        {
            if (root != null)
                root.SetActive(false);
        }
        public void Dispose()
        {
            Hide();
            UnityEngine.Object.Destroy(root);
            UnityEngine.Object.Destroy(textMaterial);
        }
    }
}
