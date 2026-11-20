using System;
using System.Collections.Generic;
using MilTrainSimVR.B.Training;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MilTrainSimVR.B.BleedingControl.Tourniquet.Tutorial
{
    // Final check panel of the status board: the trainee checks the device and selects the confirm button. Writes only this
    // panel's texts and owns only its button; which panel is shown is decided by TourniquetStartScreen. The item lines
    // (충족/미충족) are the link's current device states, the program's check; the trainee's own check is only the button input,
    // and nothing here measures gaze or a visual check. A click goes to TourniquetTrainingLink.TryConfirm, which reads the device
    // again and accepts it only in the final check step of this run with all six states valid; the runner then completes and the
    // board switches to the completion panel only when the runner reports Completed. A refused click keeps nothing: the missing
    // items and a recovery action are shown, and a new click is required. The button is subscribed in code only; keep its
    // Inspector OnClick list empty.
    [DefaultExecutionOrder(110)]
    [DisallowMultipleComponent]
    public sealed class TourniquetFinalCheck : MonoBehaviour
    {
        [SerializeField] TourniquetTrainingLink link;
        [SerializeField] TMP_FontAsset koreanFont;
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text descriptionText;
        [Tooltip("Items 1-3 (attach, band, turn).")]
        [SerializeField] TMP_Text itemsLeftText;
        [Tooltip("Items 4-6 (windlass secured, time record, strap).")]
        [SerializeField] TMP_Text itemsRightText;
        [SerializeField] TMP_Text resultText;
        [SerializeField] Button confirmButton;
        [SerializeField] TMP_Text confirmButtonText;

        Button subscribedButton;
        bool valid;
        bool refused;
        TrainingStepToken refusedToken;
        string shownLeft;
        string shownRight;
        string shownResult;

        public TourniquetTrainingLink Link => link;
        public GameObject Panel => panel;
        public Button ConfirmButton => confirmButton;

        // Item name of device state i of TourniquetTrainingIds.Steps.
        public static string ItemName(int index) => TourniquetTrainingIds.Steps[index].signal switch
        {
            TourniquetTrainingIds.Attached => TrainingSubtitles.FinalItemAttached,
            TourniquetTrainingIds.BandPrepared => TrainingSubtitles.FinalItemBand,
            TourniquetTrainingIds.TurnReached => TrainingSubtitles.FinalItemTurn,
            TourniquetTrainingIds.WindlassSecured => TrainingSubtitles.FinalItemSecured,
            TourniquetTrainingIds.TimeRecorded => TrainingSubtitles.FinalItemRecorded,
            TourniquetTrainingIds.StrapFastened => TrainingSubtitles.FinalItemFastened,
            _ => string.Empty
        };

        void Awake()
        {
            valid = TryValidate(out var error);
            if (!valid)
            {
                Debug.LogError($"[TourniquetTraining] {error}", this);
                if (confirmButton != null)
                    confirmButton.interactable = false;
                return;
            }

            ApplyText(titleText, TrainingSubtitles.FinalCheckTitle);
            ApplyText(descriptionText, TrainingSubtitles.FinalCheckDescription);
            ApplyText(confirmButtonText, TrainingSubtitles.FinalCheckButton);
            confirmButton.interactable = false;
        }

        void OnEnable()
        {
            if (subscribedButton == null && confirmButton != null)
            {
                subscribedButton = confirmButton;
                subscribedButton.onClick.AddListener(OnConfirmClicked);
            }
        }

        void OnDisable()
        {
            if (subscribedButton != null)
                subscribedButton.onClick.RemoveListener(OnConfirmClicked);
            subscribedButton = null;
        }

        // Retry entry point (TourniquetTrainingReset): button off, refused-input record and text caches cleared.
        public void ClearForReset()
        {
            if (confirmButton != null)
                confirmButton.interactable = false;
            refused = false;
            refusedToken = default;
            shownLeft = shownRight = shownResult = null;
        }

        void LateUpdate()
        {
            if (!valid)
                return;

            var open = link.IsFinalCheckStep;
            confirmButton.interactable = open && !link.ConfirmPending;
            if (open)
                Refresh();
        }

        // Only reachable through the button of the shown panel. A second click or hand in the same frame finds the button off;
        // the link also ignores an input while one is pending or outside the final check step.
        void OnConfirmClicked()
        {
            if (!valid || !confirmButton.interactable)
                return;

            confirmButton.interactable = false;
            var result = link.TryConfirm();
            refused = result == TourniquetConfirmResult.Unmet;
            if (refused)
                refusedToken = link.Runner.CurrentToken;
            if (result != TourniquetConfirmResult.Accepted)
                confirmButton.interactable = link.IsFinalCheckStep;
            Refresh();
        }

        void Refresh()
        {
            var firstMissing = -1;
            for (var i = 0; i < TourniquetTrainingIds.Steps.Length && firstMissing < 0; i++)
            {
                if (!link.IsValidNow(i))
                    firstMissing = i;
            }
            var (left, right) = ItemColumns(link.IsValidNow);
            var token = link.Runner.CurrentToken;
            var refusedHere = refused && refusedToken.RunId == token.RunId && refusedToken.StepEntry == token.StepEntry;
            Show(itemsLeftText, left, ref shownLeft);
            Show(itemsRightText, right, ref shownRight);
            Show(resultText, ResultLine(firstMissing, refusedHere, link.ConfirmPending, link.ConfirmCancelled), ref shownResult);
        }

        // Item columns (충족/미충족 per device state, items 1-3 left and 4-6 right).
        public static (string left, string right) ItemColumns(Func<int, bool> met)
        {
            var left = new List<string>();
            var right = new List<string>();
            var count = TourniquetTrainingIds.Steps.Length;
            for (var i = 0; i < count; i++)
                (i < (count + 1) / 2 ? left : right).Add(string.Format(met(i) ? TrainingSubtitles.FinalItemMetFormat : TrainingSubtitles.FinalItemUnmetFormat, ItemName(i)));
            return (string.Join("\n", left), string.Join("\n", right));
        }

        // Result line: with a missing item (firstMissing >= 0) the unmet or refused line, a line break and that item's recovery action;
        // otherwise the pending, cancelled, retry or ready line.
        public static string ResultLine(int firstMissing, bool refusedHere, bool pending, bool cancelled)
        {
            if (firstMissing >= 0)
                return (refusedHere ? TrainingSubtitles.FinalCheckBlocked : TrainingSubtitles.FinalCheckUnmet) + "\n" +
                       TourniquetTrainingGuide.RecoveryAction(TourniquetTrainingIds.Steps[firstMissing].step);
            if (pending)
                return TrainingSubtitles.FinalCheckAccepted;
            if (cancelled)
                return TrainingSubtitles.FinalCheckCancelled;
            return refusedHere ? TrainingSubtitles.FinalCheckRetry : TrainingSubtitles.FinalCheckReady;
        }

        void Show(TMP_Text target, string value, ref string shown)
        {
            if (value == shown)
                return;
            shown = value;
            ApplyText(target, value);
        }

        void ApplyText(TMP_Text target, string value)
        {
            target.font = koreanFont;
            target.text = value;
        }

        public IEnumerable<TMP_Text> Texts => new[] { titleText, descriptionText, itemsLeftText, itemsRightText, resultText, confirmButtonText };

        public bool TryValidate(out string error)
        {
            var missing = new List<string>();
            if (link == null)
                missing.Add(nameof(link));
            if (koreanFont == null)
                missing.Add(nameof(koreanFont));
            if (panel == null)
                missing.Add(nameof(panel));
            if (titleText == null)
                missing.Add(nameof(titleText));
            if (descriptionText == null)
                missing.Add(nameof(descriptionText));
            if (itemsLeftText == null)
                missing.Add(nameof(itemsLeftText));
            if (itemsRightText == null)
                missing.Add(nameof(itemsRightText));
            if (resultText == null)
                missing.Add(nameof(resultText));
            if (confirmButton == null)
                missing.Add(nameof(confirmButton));
            if (confirmButtonText == null)
                missing.Add(nameof(confirmButtonText));

            error = missing.Count > 0 ? $"TourniquetFinalCheck '{name}' missing references: {string.Join(", ", missing)}." : null;
            if (error == null)
            {
                var texts = new List<TMP_Text>(Texts);
                if (new HashSet<TMP_Text>(texts).Count != texts.Count)
                    error = $"TourniquetFinalCheck '{name}': each text must be a different object.";
                else if (!confirmButton.transform.IsChildOf(panel.transform) || texts.Exists(t => !t.transform.IsChildOf(panel.transform)))
                    error = $"TourniquetFinalCheck '{name}': texts and the confirm button must be inside its panel.";
            }
            return error == null;
        }
    }
}
