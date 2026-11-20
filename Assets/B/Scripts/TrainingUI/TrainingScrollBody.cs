using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MilTrainSimVR.B.TrainingUI
{
    // Vertical scroll body of a B course board, built from plain uGUI parts: a ScrollRect (vertical only, clamped, no inertia) on this
    // object, a Viewport with RectMask2D, a Content laid out by its VerticalLayoutGroup and ContentSizeFitter (its height is the real
    // text height, never a fixed reserve) and a vertical Scrollbar set to auto hide. A body that fits stays still and shows no
    // scrollbar; a longer one shows the scrollbar with the part that is left. RectMask2D also keeps rays off the clipped part.
    //
    // This component only chooses where the body starts, so the trainee sees the current guidance:
    // - from the top whenever the body is shown (its page opens: start, practice, final check, completion, after a retry, a stop or a
    //   new visit of the field);
    // - from the top when one of its step texts changes (a new step); a same-step rewrite of the other texts (progress, status) keeps
    //   the trainee's scroll position, and the ScrollRect brings the position back into range when the content height changes;
    // - with a focus part (for example a check card at the end of the body) scrolled into view when that part appears.
    // A body kept outside its page (a board whose panels hold no control of their own) follows the page through its CanvasGroup: shown
    // and hit only while the page is open. Reads and writes no course state, text or button.
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ScrollRect))]
    public sealed class TrainingScrollBody : MonoBehaviour
    {
        [SerializeField] ScrollRect scroll;
        [Tooltip("Page this body follows when it lives outside it; empty when the body is inside its page.")]
        [SerializeField] GameObject page;
        [Tooltip("CanvasGroup of this body, used only with a page.")]
        [SerializeField] CanvasGroup pageGroup;
        [Tooltip("Texts that name the current step; a change shows the body from its top.")]
        [SerializeField] TMP_Text[] stepTexts = Array.Empty<TMP_Text>();
        [Tooltip("Parts of the content scrolled into view when they appear.")]
        [SerializeField] RectTransform[] focusParts = Array.Empty<RectTransform>();

        static readonly Vector3[] Corners = new Vector3[4];

        bool valid;
        bool pageShown;
        bool toTop;
        RectTransform focus;
        string[] shownSteps = Array.Empty<string>();
        bool[] shownParts = Array.Empty<bool>();

        public ScrollRect Scroll => scroll;
        public GameObject Page => page;

        void Awake()
        {
            valid = TryValidate(out var error);
            if (!valid)
            {
                Debug.LogError("[TrainingUI] " + error, this);
                enabled = false;
                return;
            }
            shownSteps = new string[stepTexts.Length];
            shownParts = new bool[focusParts.Length];
            if (page != null)
            {
                pageShown = page.activeInHierarchy;
                ShowPage(pageShown);
            }
        }

        void OnEnable()
        {
            if (!valid)
                return;
            toTop = true;
            for (var i = 0; i < stepTexts.Length; i++)
                shownSteps[i] = stepTexts[i].text;
            for (var i = 0; i < focusParts.Length; i++)
                shownParts[i] = focusParts[i].gameObject.activeInHierarchy;
        }

        // After the courses' Update and LateUpdate of this frame (their texts and panels are final for the frame).
        void LateUpdate()
        {
            if (page != null)
            {
                var shown = page.activeInHierarchy;
                if (shown != pageShown)
                {
                    pageShown = shown;
                    ShowPage(shown);
                    toTop |= shown;
                }
                if (!shown)
                    return;
            }

            for (var i = 0; i < stepTexts.Length; i++)
            {
                var text = stepTexts[i].text;
                if (text != shownSteps[i])
                {
                    shownSteps[i] = text;
                    toTop = true;
                }
            }
            for (var i = 0; i < focusParts.Length; i++)
            {
                var on = focusParts[i].gameObject.activeInHierarchy;
                if (on && !shownParts[i])
                    focus = focusParts[i];
                shownParts[i] = on;
            }
            if (!toTop && focus == null)
                return;

            var content = scroll.content;
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            scroll.StopMovement();
            var range = Mathf.Max(0f, content.rect.height - scroll.viewport.rect.height);
            var offset = focus != null ? FocusOffset(focus, content) : 0f;
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, Mathf.Clamp(offset, 0f, range));
            toTop = false;
            focus = null;
        }

        void ShowPage(bool shown)
        {
            pageGroup.alpha = shown ? 1f : 0f;
            pageGroup.interactable = shown;
            pageGroup.blocksRaycasts = shown;
        }

        // Content offset (from the top) that shows the part whole, its top first when it is taller than the viewport.
        float FocusOffset(RectTransform part, RectTransform content)
        {
            part.GetWorldCorners(Corners);
            var top = float.MinValue;
            var bottom = float.MaxValue;
            foreach (var corner in Corners)
            {
                var y = content.InverseTransformPoint(corner).y;
                top = Mathf.Max(top, y);
                bottom = Mathf.Min(bottom, y);
            }
            var contentTop = content.rect.yMax;
            var fromTop = contentTop - top;
            var toBottom = contentTop - bottom;
            return Mathf.Min(fromTop, toBottom - scroll.viewport.rect.height);
        }

        public bool TryValidate(out string error)
        {
            var problems = new List<string>();
            if (scroll == null || scroll.gameObject != gameObject)
                problems.Add("the ScrollRect of this object");
            else
            {
                if (scroll.content == null || scroll.viewport == null || !scroll.content.IsChildOf(scroll.viewport))
                    problems.Add("a content inside the viewport");
                if (scroll.viewport != null && scroll.viewport.GetComponent<RectMask2D>() == null)
                    problems.Add("RectMask2D on the viewport");
                if (!scroll.vertical || scroll.horizontal || scroll.movementType != ScrollRect.MovementType.Clamped)
                    problems.Add("a vertical-only, clamped scroll");
                if (scroll.verticalScrollbar == null || !scroll.verticalScrollbar.transform.IsChildOf(transform))
                    problems.Add("a vertical scrollbar inside this body");
                if (scroll.content != null && (scroll.content.anchorMin.y != 1f || scroll.content.anchorMax.y != 1f || scroll.content.pivot.y != 1f))
                    problems.Add("a content anchored and pivoted at its top");
            }
            if (page != null && (pageGroup == null || pageGroup.gameObject != gameObject || transform.IsChildOf(page.transform)))
                problems.Add("its own CanvasGroup and a place outside the page it follows");
            if (stepTexts == null || Array.IndexOf(stepTexts, null) >= 0)
                problems.Add("step text entries");
            if (focusParts == null || Array.IndexOf(focusParts, null) >= 0 ||
                (scroll != null && scroll.content != null && Array.Exists(focusParts, p => p != null && !p.IsChildOf(scroll.content))))
                problems.Add("focus parts inside the content");
            error = problems.Count > 0 ? $"TrainingScrollBody '{name}' needs {string.Join(", ", problems)}." : null;
            return error == null;
        }
    }
}
