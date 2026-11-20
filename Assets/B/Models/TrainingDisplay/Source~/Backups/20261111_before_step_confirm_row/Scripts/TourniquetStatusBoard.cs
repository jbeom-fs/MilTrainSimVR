using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace MilTrainSimVR.B.BleedingControl.Tourniquet
{
    // Minimal world-space status board of the prototype: prototype notice, instruction, current placement state with its reason
    // and the last failed release; after attaching, the current part target, its progress, the last part issue and the virtual
    // time record. Reads TourniquetDevice and TourniquetOperation only; no buttons, no training flow. All text comes from
    // TrainingSubtitles. With writesGuidance off (a training guide owns the title and instruction texts), it writes only the
    // notice, status and detail, and its title and instruction references stay empty so no text has two writers.
    [DefaultExecutionOrder(110)]
    [DisallowMultipleComponent]
    public sealed class TourniquetStatusBoard : MonoBehaviour
    {
        [SerializeField] TourniquetDevice device;
        [SerializeField] TourniquetOperation operation;
        [SerializeField] TMP_FontAsset koreanFont;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text noticeText;
        [SerializeField] TMP_Text instructionText;
        [SerializeField] TMP_Text statusText;
        [SerializeField] TMP_Text detailText;
        [SerializeField] Color idleColor = new Color(0.94f, 0.95f, 0.96f, 1f);
        [SerializeField] Color blockedColor = new Color(1f, 0.45f, 0.4f, 1f);
        [SerializeField] Color placeableColor = new Color(0.35f, 0.95f, 0.55f, 1f);
        [SerializeField] Color attachedColor = new Color(0.3f, 0.78f, 1f, 1f);
        [Tooltip("Off when a training guide writes the title and instruction; then titleText and instructionText must be empty.")]
        [SerializeField] bool writesGuidance = true;
        [Tooltip("Board layout revision last applied by the B tourniquet setup menu; it updates the text areas only when its own " +
                 "revision is newer, so later runs keep the user's adjustments. The board's world pose and scale are never changed.")]
        [SerializeField, HideInInspector] int layoutRevision;

        public int LayoutRevision => layoutRevision;
        public bool WritesGuidance => writesGuidance;

        bool shown;
        TourniquetState shownState;
        TourniquetPlacementCheck shownCheck;
        TourniquetPlacementCheck shownFailure;
        // Rounded part values last shown after attaching, so text is rebuilt only when what it shows changes.
        (TourniquetStep step, TourniquetWindlassPhase phase, TourniquetOperationIssue issue, int progress, bool covered, bool canRecord, bool recorded) shownParts;

        void Start()
        {
            if (!TryValidate(out var error))
            {
                Debug.LogError("[Tourniquet] " + error, this);
                enabled = false;
                return;
            }

            ReportMissingGlyphs();
            if (writesGuidance)
            {
                ApplyText(titleText, TrainingSubtitles.BoardTitle);
                ApplyText(instructionText, TrainingSubtitles.Instruction);
            }
            ApplyText(noticeText, TrainingSubtitles.PrototypeNotice);
            Refresh();
        }

        void LateUpdate() => Refresh();

        void Refresh()
        {
            if (device.IsAttached)
            {
                RefreshParts();
                return;
            }

            if (shown && shownState == device.State && shownCheck == device.CurrentCheck && shownFailure == device.LastFailure)
                return;

            if (shownState == TourniquetState.Attached && writesGuidance)
                ApplyText(instructionText, TrainingSubtitles.Instruction);
            shown = true;
            shownState = device.State;
            shownCheck = device.CurrentCheck;
            shownFailure = device.LastFailure;
            switch (device.State)
            {
                case TourniquetState.HeldBlocked:
                    SetStatus(TrainingSubtitles.StatusHeldBlocked, blockedColor, TrainingSubtitles.ReasonPrefix + TrainingSubtitles.Reason(device.CurrentCheck));
                    break;
                case TourniquetState.HeldPlaceable:
                    SetStatus(TrainingSubtitles.StatusHeldPlaceable, placeableColor, TrainingSubtitles.PlaceableDetail);
                    break;
                case TourniquetState.Attached:
                    SetStatus(TrainingSubtitles.StatusAttached, attachedColor, TrainingSubtitles.AttachedDetail);
                    break;
                default:
                    SetStatus(TrainingSubtitles.StatusIdle, idleColor, device.LastFailure == TourniquetPlacementCheck.None ? string.Empty
                        : TrainingSubtitles.FailedPrefix + TrainingSubtitles.Reason(device.LastFailure) + TrainingSubtitles.RetryHint);
                    break;
            }
        }

        void RefreshParts()
        {
            var step = operation.CurrentStep;
            var progress = step switch
            {
                TourniquetStep.PullBand => Mathf.RoundToInt(operation.BandPullProgress * 100f),
                TourniquetStep.TurnWindlass => Mathf.RoundToInt(operation.WindlassTurnDegrees),
                TourniquetStep.SeatWindlass => Mathf.RoundToInt(operation.WindlassSeatDegrees),
                TourniquetStep.RecordTime => 0,
                TourniquetStep.CoverStrap => Mathf.RoundToInt(operation.StrapFoldDegrees / operation.StrapCoverDegrees * 100f),
                _ => 0
            };
            var parts = (step, operation.WindlassPhase, operation.LastIssue, progress, operation.IsStrapCovered, operation.CanRecordTime, operation.IsTimeRecorded);
            // The detail keeps the recorded time on every later step, including Done.
            if (shown && shownState == TourniquetState.Attached && shownParts == parts)
                return;

            shown = true;
            shownState = TourniquetState.Attached;
            shownParts = parts;
            var instruction = step switch
            {
                TourniquetStep.PullBand => TrainingSubtitles.InstructionPullBand,
                TourniquetStep.TurnWindlass => TrainingSubtitles.InstructionTurnWindlass,
                TourniquetStep.SeatWindlass => TrainingSubtitles.InstructionSeatWindlass,
                TourniquetStep.RecordTime => TrainingSubtitles.InstructionRecordTime,
                TourniquetStep.CoverStrap => TrainingSubtitles.InstructionCoverStrap,
                _ => TrainingSubtitles.InstructionPartsDone
            };
            if (writesGuidance)
                ApplyText(instructionText, instruction);

            var status = step switch
            {
                TourniquetStep.PullBand => string.Format(TrainingSubtitles.StatusPullBandFormat, progress),
                TourniquetStep.TurnWindlass => string.Format(TrainingSubtitles.StatusTurnWindlassFormat, progress, Mathf.RoundToInt(operation.RequiredTurnDegrees)),
                TourniquetStep.SeatWindlass => operation.WindlassPhase == TourniquetWindlassPhase.Lowered
                    ? string.Format(TrainingSubtitles.StatusSeatEnteringFormat, progress, Mathf.RoundToInt(operation.WindlassSeatTargetDegrees))
                    : operation.WindlassPhase == TourniquetWindlassPhase.Lowering ? TrainingSubtitles.StatusSeatLowering
                    : TrainingSubtitles.StatusTurnReady,
                TourniquetStep.RecordTime => TrainingSubtitles.StatusRecordTime,
                TourniquetStep.CoverStrap => operation.IsStrapCovered ? TrainingSubtitles.StatusStrapCovered
                    : string.Format(TrainingSubtitles.StatusCoverStrapFormat, progress),
                _ => TrainingSubtitles.StatusPartsDone
            };
            var issue = TrainingSubtitles.Issue(operation.LastIssue);
            var record = operation.IsTimeRecorded
                ? string.Format(TrainingSubtitles.TimeRecordedFormat, Mathf.FloorToInt(operation.RecordedElapsedSeconds) / 60, Mathf.FloorToInt(operation.RecordedElapsedSeconds) % 60)
                : TrainingSubtitles.TimeNotRecorded;
            SetStatus(status, operation.LastIssue != TourniquetOperationIssue.None ? blockedColor : attachedColor,
                string.IsNullOrEmpty(issue) ? record : TrainingSubtitles.ReasonPrefix + issue + "\n" + record);
        }

        void SetStatus(string status, Color color, string detail)
        {
            ApplyText(statusText, status);
            statusText.color = color;
            ApplyText(detailText, detail);
        }

        void ApplyText(TMP_Text target, string value)
        {
            target.font = koreanFont;
            target.text = value;
        }

        // Read-only check against the source font file; does not add glyphs to the font asset.
        void ReportMissingGlyphs()
        {
            var source = koreanFont.sourceFontFile;
            if (source == null)
                return;

            var missing = new HashSet<char>();
            foreach (var c in TrainingSubtitles.All)
            {
                if (!char.IsWhiteSpace(c) && !char.IsControl(c) && !source.HasCharacter(c))
                    missing.Add(c);
            }

            if (missing.Count > 0)
                Debug.LogError($"[Tourniquet] Font '{koreanFont.name}' source is missing glyphs: '{string.Concat(missing)}'.", koreanFont);
        }

        public bool TryValidate(out string error)
        {
            var missing = new List<string>();
            if (device == null)
                missing.Add(nameof(device));
            if (operation == null)
                missing.Add(nameof(operation));
            if (koreanFont == null)
                missing.Add(nameof(koreanFont));
            if (writesGuidance && titleText == null)
                missing.Add(nameof(titleText));
            if (noticeText == null)
                missing.Add(nameof(noticeText));
            if (writesGuidance && instructionText == null)
                missing.Add(nameof(instructionText));
            if (statusText == null)
                missing.Add(nameof(statusText));
            if (detailText == null)
                missing.Add(nameof(detailText));

            error = missing.Count > 0 ? $"TourniquetStatusBoard '{name}' missing references: {string.Join(", ", missing)}." : null;
            if (error == null && !writesGuidance && (titleText != null || instructionText != null))
                error = $"TourniquetStatusBoard '{name}': title and instruction texts must be empty while a training guide writes them.";
            return error == null;
        }
    }
}
