using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MilTrainSimVR.B.TrainingUI
{
    // Step row of a course board in the KD-1 board style: one display-only box per course step with its number on it. The step name
    // and count are the course's own step line, so the boxes carry only the number and never cut a long name. Writes the numbers
    // (digits, not subtitles) the first time it is used. Show paints the boxes (done, current, ahead) for a course whose own guide has
    // no step boxes (its board adapter calls it); a course guide that paints its own boxes uses TrainingBoardStyle.StepColor and never
    // calls Show, so every box has one painter. Reads and changes no course state. No raycast target, no button.
    [DisallowMultipleComponent]
    public sealed class TrainingStepRow : MonoBehaviour
    {
        [SerializeField] TMP_FontAsset koreanFont;
        [Tooltip("One display-only box per course step, in order.")]
        [SerializeField] Image[] boxes = new Image[0];
        [Tooltip("The number text inside each box, same order.")]
        [SerializeField] TMP_Text[] labels = new TMP_Text[0];

        // 0 not checked yet, 1 valid, -1 invalid (logged once).
        int state;
        int shown = int.MinValue;

        public IReadOnlyList<Image> Boxes => boxes;
        public IReadOnlyList<TMP_Text> Labels => labels;
        public int Count => boxes != null ? boxes.Length : 0;

        void Awake() => Ready();

        // A row inside an inactive panel can be painted before its Awake; the first use of either writes the numbers.
        bool Ready()
        {
            if (state != 0)
                return state > 0;
            if (!TryValidate(out var error))
            {
                state = -1;
                Debug.LogError("[TrainingUI] " + error, this);
                return false;
            }
            state = 1;
            for (var i = 0; i < labels.Length; i++)
            {
                labels[i].font = koreanFont;
                labels[i].text = (i + 1).ToString(CultureInfo.InvariantCulture);
            }
            return true;
        }

        // current: the step the course shows now (TrainingBoardStyle.StepColor). Written only on a change.
        public void Show(int current)
        {
            if (!Ready() || current == shown)
                return;
            shown = current;
            for (var i = 0; i < boxes.Length; i++)
                boxes[i].color = TrainingBoardStyle.StepColor(i, current);
        }

        public bool TryValidate(out string error)
        {
            error = null;
            if (koreanFont == null)
                error = $"TrainingStepRow '{name}': korean font missing.";
            else if (boxes == null || labels == null || boxes.Length == 0 || boxes.Length != labels.Length)
                error = $"TrainingStepRow '{name}': needs one number label per step box ({boxes?.Length ?? 0} boxes, {labels?.Length ?? 0} labels).";
            else
            {
                for (var i = 0; i < boxes.Length && error == null; i++)
                {
                    if (boxes[i] == null || labels[i] == null)
                        error = $"TrainingStepRow '{name}': step box or label {i + 1} missing.";
                    else if (!labels[i].transform.IsChildOf(boxes[i].transform))
                        error = $"TrainingStepRow '{name}': label {i + 1} must be inside its box.";
                    else if (boxes[i].raycastTarget || labels[i].raycastTarget)
                        error = $"TrainingStepRow '{name}': step box {i + 1} must not take rays.";
                }
                if (error == null && (new HashSet<Image>(boxes).Count != boxes.Length || new HashSet<TMP_Text>(labels).Count != labels.Length))
                    error = $"TrainingStepRow '{name}': each box and label must be a different object.";
            }
            return error == null;
        }
    }
}
