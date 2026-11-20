using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MilTrainSimVR.B.Decontamination.KD1
{
    // Board of the KD-1 equipment scene: the action that can be done now (one line), why the last trigger press did nothing (when
    // there is a reason, for a few seconds) and the equipment reset button. Reads KD1Equipment only; never changes an item. Text
    // comes from TrainingSubtitles.
    //
    // Lines (KD1GuideLines, shared with the KD-1 course board): action line (one-hand flow), first that applies: invalid setup; failed reset (with what to do); equipment switched off;
    // focus lost; the held item grabbed last (KD1Equipment.GuidedItem): its hand not tracked, or what to do with it (put the open
    // outer pack or packet down and take what is inside, pull it out, fit the pad, or put it down first when that hand already
    // wears a pad); with nothing held: grab the outer pack; once a pad is fitted, how to take it off (no push to a next pad or the
    // other hand); otherwise the next thing on the way (a free pad, a pad in an open packet, a packet in the open outer pack).
    // With the surface practice in the scene (optional reference), a fitted pad leads to touching the marked areas (first, remaining,
    // all done) instead of the bare removal line; the removal hint then shows in the reason field when there is no reason, and the
    // progress field shows "연습 부위 n/3". Practice lines never hide the setup, reset, pause, focus or tracking lines above them.
    //
    // Reset button: asks the equipment to reset (one reset per frame there). Subscribed in code only; keep its OnClick list empty.
    [DefaultExecutionOrder(60)]
    [DisallowMultipleComponent]
    public sealed class KD1Guide : MonoBehaviour
    {
        [SerializeField] KD1Equipment equipment;
        [SerializeField] TMP_FontAsset koreanFont;
        [SerializeField] TMP_Text instructionText;
        [SerializeField] TMP_Text noticeText;
        [SerializeField] Button resetButton;
        [SerializeField] TMP_Text resetButtonText;
        [Tooltip("Optional: the surface contact practice of this scene. Needs the progress text when set.")]
        [SerializeField] KD1SurfacePractice practice;
        [SerializeField] TMP_Text progressText;

        Button subscribedButton;
        bool valid;
        string shownLine;
        string shownNotice;
        string shownProgress;

        public KD1Equipment Equipment => equipment;
        public Button ResetButton => resetButton;
        public IEnumerable<TMP_Text> Texts => new[] { instructionText, noticeText, resetButtonText };
        public TMP_Text InstructionText => instructionText;
        public TMP_Text NoticeText => noticeText;
        public TMP_Text ProgressText => progressText;
        public KD1SurfacePractice Practice => practice;

        void Awake()
        {
            valid = TryValidate(out var error);
            if (!valid)
            {
                Debug.LogError("[KD1] " + error, this);
                if (resetButton != null)
                    resetButton.interactable = false;
                return;
            }
            resetButtonText.font = koreanFont;
            resetButtonText.text = TrainingSubtitles.ResetButton;
        }

        void OnEnable()
        {
            if (subscribedButton == null && resetButton != null)
            {
                subscribedButton = resetButton;
                subscribedButton.onClick.AddListener(OnResetClicked);
            }
        }

        void OnDisable()
        {
            if (subscribedButton != null)
                subscribedButton.onClick.RemoveListener(OnResetClicked);
            subscribedButton = null;
        }

        void LateUpdate()
        {
            if (!valid)
                return;
            Show(instructionText, KD1GuideLines.Action(equipment, practice), ref shownLine);
            Show(noticeText, KD1GuideLines.Hint(equipment, practice), ref shownNotice);
            if (progressText != null)
            {
                var usable = equipment.IsValid && !equipment.IsFaulted && equipment.isActiveAndEnabled;
                Show(progressText, usable && KD1GuideLines.PracticeShown(practice) ? TrainingSubtitles.Progress(practice.DoneCount, practice.ZoneCount) : string.Empty, ref shownProgress);
            }
            resetButton.interactable = equipment.IsValid && equipment.isActiveAndEnabled && !equipment.IsResetting;
        }

        void Show(TMP_Text text, string line, ref string shown)
        {
            if (line == shown)
                return;
            shown = line;
            text.font = koreanFont;
            text.text = line;
        }

        void OnResetClicked()
        {
            if (!valid || !resetButton.interactable)
                return;
            resetButton.interactable = false;
            equipment.ResetEquipment();
            shownLine = null;
            shownNotice = null;
            shownProgress = null;
        }

        public bool TryValidate(out string error)
        {
            var missing = new List<string>();
            if (equipment == null)
                missing.Add(nameof(equipment));
            if (koreanFont == null)
                missing.Add(nameof(koreanFont));
            if (instructionText == null || noticeText == null || resetButtonText == null || instructionText == noticeText)
                missing.Add("a text");
            if (resetButton == null)
                missing.Add(nameof(resetButton));
            if (practice != null && (progressText == null || progressText == instructionText || progressText == noticeText))
                missing.Add(nameof(progressText) + " (needed with the practice)");
            error = missing.Count > 0 ? $"KD1Guide '{name}' missing references: {string.Join(", ", missing)}." : null;
            if (error == null && !resetButtonText.transform.IsChildOf(resetButton.transform))
                error = $"KD1Guide '{name}': the reset label must be inside the reset button.";
            return error == null;
        }
    }
}
