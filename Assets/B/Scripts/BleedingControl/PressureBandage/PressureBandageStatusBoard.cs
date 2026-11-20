using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MilTrainSimVR.B.BleedingControl.PressureBandage
{
    // World-space status board of the pressure bandage device check (not a training start or completion screen): one next
    // action, the current dressing state (placement with its reason, hold, two-hand contact, hand tracking), the current bandage
    // application state (stage, roll, progress conditions, end or tape check), the continuous contact ratio, and this run's
    // contact practice history on its own line. Reads PressureDressing and PressureBandageApplication only; no buttons. All text
    // comes from TrainingSubtitles and is rebuilt only when what it shows changes. Colour repeats what the words already say.
    //
    // This board is the only writer of these texts. The next action has one source at a time: input paused first; then the
    // bandage guide while a bandage application is under way (stage past Ready, the roll, end or tape in hand, or a recent
    // cancel); otherwise the chapter 1 contact guide (with a combined line when one hand holds the dressing and the bandage can
    // start). Completion lines follow the current state only: the contact line the current continuous contact, the bandage line
    // the current Fixed stage ("device operation complete", never "training complete"); history never stands in for them.
    //
    // In the training course scene (chapter 3) writesGuidance and writesDeviceLines are off: the course guide shows one action at
    // a time (step row, instruction, check card), so this board writes no text there (no reference to the two guide texts; the
    // notice and device line texts stay referenced but are saved hidden). It stays enabled and valid: the course start checks it,
    // and it keeps the board layout revision. The device next-action rules stay public
    // (ContactInstruction, BandageInstruction) for the device check board.
    [DefaultExecutionOrder(110)]
    [DisallowMultipleComponent]
    public sealed class PressureBandageStatusBoard : MonoBehaviour
    {
        // How long (s) the instruction keeps reporting a broken contact or a rejected bandage input.
        const float BreakNoticeSeconds = 2f;
        const float IssueNoticeSeconds = 2.5f;
        const float CancelNoticeSeconds = 4f;

        [SerializeField] PressureDressing dressing;
        [SerializeField] PressureBandageApplication application;
        [SerializeField] TMP_FontAsset koreanFont;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text noticeText;
        [SerializeField] TMP_Text instructionText;
        [SerializeField] TMP_Text statusText;
        [SerializeField] TMP_Text bandageText;
        [SerializeField] TMP_Text progressText;
        [SerializeField] TMP_Text historyText;
        [SerializeField] Color normalColor = new Color(0.94f, 0.95f, 0.96f, 1f);
        [SerializeField] Color invalidColor = new Color(1f, 0.55f, 0.5f, 1f);
        [SerializeField] Color validColor = new Color(0.45f, 0.95f, 0.6f, 1f);
        [Tooltip("Board layout revision of this board (2: chapter 2 bandage lines; 3: chapter 3 panels).")]
        [SerializeField] int layoutRevision;
        [Tooltip("Writes the title and the next-action instruction. Off when a training course guide owns those two texts.")]
        [SerializeField] bool writesGuidance = true;
        [Tooltip("Writes the notice and the device state lines (placement, hold, hands, bandage, contact ratio, history). Off in the " +
                 "training course scene, where these details are not shown.")]
        [SerializeField] bool writesDeviceLines = true;

        readonly StringBuilder builder = new StringBuilder();
        bool shown;
        (string instruction, PressureDressingCheck check, bool held, InteractorHandedness hand, PressureHandTracking holdTracking, bool hold,
            bool twoHand, PressureHandTracking left, PressureHandTracking right, bool leftIn, bool rightIn, int percent, bool done) shownKey;
        (PressureBandageStage stage, int wrapPercent, PressureWrapBlock block, InteractorHandedness rollHand, PressureHandTracking rollTracking,
            bool endInRange, PressureFixCheck tape, int reset, bool loose) shownBandage;

        public int LayoutRevision => layoutRevision;
        public bool WritesGuidance => writesGuidance;
        public bool WritesDeviceLines => writesDeviceLines;

        void Start()
        {
            if (!TryValidate(out var error))
            {
                Debug.LogError("[PressureBandage] " + error, this);
                enabled = false;
                return;
            }

            if (writesGuidance)
                Apply(titleText, TrainingSubtitles.BoardTitle, normalColor);
            if (writesDeviceLines)
                Apply(noticeText, TrainingSubtitles.Notice, noticeText.color);
        }

        void LateUpdate()
        {
            if (writesGuidance || writesDeviceLines)
                Refresh();
        }

        void Refresh()
        {
            var d = dressing;
            var a = application.isActiveAndEnabled && !application.IsResetting ? application : null;
            var percent = d.IsTwoHandContactValid ? Mathf.FloorToInt(d.ContactProgress * 100f) : -1;
            var key = (writesGuidance ? Instruction(d, a) : null, d.CurrentCheck, d.IsHeld, d.HoldingHand, d.HoldingHandTracking, d.IsHoldValid, d.IsTwoHandContactValid,
                       d.LeftTracking, d.RightTracking, d.LeftInPressZone, d.RightInPressZone, percent, d.HasCompletedContactPractice);
            var bandage = a != null
                ? (a.Stage, WrapPercent(a), a.Block, a.RollHand, a.RollHandTracking, a.IsEndInSnapRange, a.TapeCheck, a.ResetCount, a.LooseValid)
                : (PressureBandageStage.Ready, -1, PressureWrapBlock.None, InteractorHandedness.None, PressureHandTracking.Missing,
                   false, PressureFixCheck.None, -1, false);
            if (shown && key.Equals(shownKey) && bandage.Equals(shownBandage))
                return;
            shown = true;
            shownKey = key;
            shownBandage = bandage;

            if (writesGuidance)
                Apply(instructionText, key.Item1, normalColor);
            if (!writesDeviceLines)
                return;
            Apply(statusText, Status(d, a), d.IsPlacementValid ? validColor : invalidColor);
            Apply(bandageText, a != null ? Bandage(a) : TrainingSubtitles.BandagePrefix + TrainingSubtitles.StageReady,
                a != null && a.Stage == PressureBandageStage.Fixed ? validColor : normalColor);
            Apply(progressText, percent >= 0
                ? TrainingSubtitles.ProgressPrefix + percent + TrainingSubtitles.ProgressPercentSuffix
                : TrainingSubtitles.ProgressPrefix + TrainingSubtitles.ProgressIdle,
                percent >= 0 ? validColor : normalColor);
            Apply(historyText, d.HasCompletedContactPractice ? TrainingSubtitles.HistoryDone : TrainingSubtitles.HistoryNone, normalColor);
        }

        static string Instruction(PressureDressing d, PressureBandageApplication a)
        {
            if (!d.InputFocused)
                return TrainingSubtitles.InputPaused;
            if (a != null && (a.Stage != PressureBandageStage.Ready || a.IsRollHeld || a.IsTapeHeld || RecentCancel(a) != null))
                return BandageInstruction(d, a);
            return ContactInstruction(d, a != null);
        }

        // Chapter 1 next action. The completion line follows the current continuous contact only (its ratio, owned by
        // PressureDressing, reached 1); the practice history is shown on the history line alone.
        public static string ContactInstruction(PressureDressing d, bool bandageAvailable)
        {
            if (d.IsHeld && d.HoldingHandTracking != PressureHandTracking.Tracked)
                return TrainingSubtitles.CheckTracking;
            if (!d.IsPlacementValid)
                return d.IsHeld ? TrainingSubtitles.CoverWound : TrainingSubtitles.GrabDressing;
            if (d.IsTwoHandContactValid)
                return d.ContactProgress >= 1f ? TrainingSubtitles.PracticeDone : TrainingSubtitles.KeepTwoHands;

            var recentBreak = d.LastContactBreakTime >= 0f && Time.time - d.LastContactBreakTime <= BreakNoticeSeconds;
            if (recentBreak && d.LastContactBreak == PressureContactBreak.TrackingLost)
                return TrainingSubtitles.CheckTracking;
            if (recentBreak && d.LastContactBreak == PressureContactBreak.HandLeft)
                return TrainingSubtitles.ContactBroken;
            if (d.LeftTracking != PressureHandTracking.Tracked || d.RightTracking != PressureHandTracking.Tracked)
                return TrainingSubtitles.CheckTracking;
            if (!d.IsHeld && !d.LeftInPressZone && !d.RightInPressZone)
                return TrainingSubtitles.KeepDressing;
            if (bandageAvailable && d.IsHoldValid && !(d.LeftInPressZone && d.RightInPressZone))
                return TrainingSubtitles.StackHandsOrRoll;
            return TrainingSubtitles.StackHands;
        }

        // Chapter 2 next action: the first missing condition of the current stage, then a recent rejected input, then the step.
        // From the wrap on the dressing need not be held (only placed); a hand holding it is still one hand fewer for the parts.
        public static string BandageInstruction(PressureDressing d, PressureBandageApplication a)
        {
            var cancel = RecentCancel(a);
            if (cancel != null)
                return cancel;
            if (a.Stage == PressureBandageStage.Fixed)
                return TrainingSubtitles.ApplicationDone;
            if (!d.IsPlacementValid)
                return d.IsHeld ? TrainingSubtitles.CoverWound : TrainingSubtitles.ReplaceDressing;

            switch (a.Stage)
            {
                case PressureBandageStage.Ready:
                case PressureBandageStage.Wrapping:
                    // Dressing in one hand and tape in the other: no hand is free for the roll (one hand, one kit part).
                    if (!a.IsRollHeld && a.IsTapeHeld && d.IsHeld)
                        return a.Stage == PressureBandageStage.Ready ? TrainingSubtitles.PutTapeDownGrabRoll : TrainingSubtitles.PutTapeDownRegrabRoll;
                    if (!a.IsRollHeld)
                        return a.Stage == PressureBandageStage.Ready ? TrainingSubtitles.GrabRoll : TrainingSubtitles.RegrabRoll;
                    if (a.RollHandTracking != PressureHandTracking.Tracked)
                        return TrainingSubtitles.CheckRollTracking;
                    if (a.Block == PressureWrapBlock.StartLocked)
                        return TrainingSubtitles.WrapNotAllowed;
                    return a.Block == PressureWrapBlock.OutOfZone ? TrainingSubtitles.MoveRollToZone : TrainingSubtitles.HoldRollInZone;
                case PressureBandageStage.EndPlacement:
                    if (a.IsRecent(PressureWrapIssue.EndRejected, IssueNoticeSeconds))
                        return TrainingSubtitles.EndRejected;
                    if (a.IsRecent(PressureWrapIssue.PlacementRequired, IssueNoticeSeconds))
                        return TrainingSubtitles.FixNeedsPlacement;
                    if (!a.IsEndHeld && a.IsTapeHeld && d.IsHeld)
                        return TrainingSubtitles.PutTapeDownGrabEnd;
                    return a.IsEndHeld ? TrainingSubtitles.PlaceEnd : TrainingSubtitles.GrabEnd;
                default:
                    if (a.IsRecent(PressureWrapIssue.TapeRejected, IssueNoticeSeconds))
                        return TrainingSubtitles.TapeRejected;
                    if (a.IsRecent(PressureWrapIssue.PlacementRequired, IssueNoticeSeconds))
                        return TrainingSubtitles.FixNeedsPlacement;
                    return a.IsTapeHeld ? TrainingSubtitles.PlaceTape : TrainingSubtitles.GrabTape;
            }
        }

        static int WrapPercent(PressureBandageApplication a) => Mathf.Clamp(Mathf.FloorToInt(a.WrapProgress * 100f), 0, 100);

        // Cancel notice of the last few seconds (dressing off the wound, or slid / turned under the wrap), else null.
        static string RecentCancel(PressureBandageApplication a)
        {
            if (a.IsRecent(PressureWrapIssue.DressingDisplaced, CancelNoticeSeconds))
                return TrainingSubtitles.DressingDisplaced;
            if (a.IsRecent(PressureWrapIssue.DressingShifted, CancelNoticeSeconds))
                return TrainingSubtitles.DressingShifted;
            return null;
        }

        string Status(PressureDressing d, PressureBandageApplication a)
        {
            builder.Clear();
            builder.Append(d.IsPlacementValid
                ? TrainingSubtitles.PlacementValid
                : TrainingSubtitles.PlacementInvalidPrefix + TrainingSubtitles.Reason(d.CurrentCheck));
            builder.Append('\n').Append(TrainingSubtitles.HoldPrefix);
            // From the end of the wrap the bandage keeps the dressing: no hand is asked for.
            if (a != null && a.IsDressingFixed)
                builder.Append(TrainingSubtitles.HoldFixedByBandage);
            else
                builder.Append(d.IsHoldValid ? TrainingSubtitles.HoldValid : d.IsHeld ? TrainingSubtitles.HoldHeldOnly : TrainingSubtitles.HoldNone);
            if (d.IsHeld)
                builder.Append(TrainingSubtitles.Separator).Append(HandName(d.HoldingHand));
            builder.Append('\n').Append(d.IsTwoHandContactValid ? TrainingSubtitles.TwoHandValid : TrainingSubtitles.TwoHandInvalid);
            builder.Append('\n').Append(TrainingSubtitles.HandsPrefix);
            AppendHand(TrainingSubtitles.LeftHand, d.LeftTracking, d.LeftInPressZone);
            builder.Append(TrainingSubtitles.HandSeparator);
            AppendHand(TrainingSubtitles.RightHand, d.RightTracking, d.RightInPressZone);
            return builder.ToString();
        }

        // Current bandage application: stage line, roll line, then the progress condition (wrapping) or the end / tape check.
        string Bandage(PressureBandageApplication a)
        {
            builder.Clear();
            builder.Append(TrainingSubtitles.BandagePrefix);
            switch (a.Stage)
            {
                case PressureBandageStage.Ready:
                    builder.Append(TrainingSubtitles.StageReady);
                    break;
                case PressureBandageStage.Wrapping:
                    builder.Append(TrainingSubtitles.StageWrapping).Append(WrapPercent(a)).Append(TrainingSubtitles.ProgressPercentSuffix);
                    break;
                case PressureBandageStage.EndPlacement:
                    builder.Append(TrainingSubtitles.StageEnd);
                    break;
                case PressureBandageStage.Taping:
                    builder.Append(TrainingSubtitles.StageTape);
                    break;
                default:
                    builder.Append(TrainingSubtitles.StageFixed);
                    break;
            }

            builder.Append('\n').Append(TrainingSubtitles.RollPrefix);
            if (a.IsRollDetached)
                builder.Append(TrainingSubtitles.RollDetached);
            else if (a.IsRollHeld)
                builder.Append(HandName(a.RollHand)).Append(' ').Append(TrainingSubtitles.Tracking(a.RollHandTracking));
            else
                builder.Append(TrainingSubtitles.RollNotHeld);
            // The loose strip is not drawn for an invalid connection (PressureBandageRibbons display policy).
            if (a.Stage == PressureBandageStage.Wrapping && !a.LooseValid)
                builder.Append(TrainingSubtitles.StripOnHold);

            switch (a.Stage)
            {
                case PressureBandageStage.Ready:
                case PressureBandageStage.Wrapping:
                    builder.Append('\n').Append(TrainingSubtitles.WrapPrefix).Append(TrainingSubtitles.Block(a.Block));
                    break;
                case PressureBandageStage.EndPlacement:
                    // The end has no pose check: only whether it is within the attach distance of its mark.
                    builder.Append('\n').Append(TrainingSubtitles.EndPrefix)
                        .Append(a.IsEndInSnapRange ? TrainingSubtitles.FixValid : TrainingSubtitles.FixInvalidPrefix + TrainingSubtitles.FixAway);
                    break;
                case PressureBandageStage.Taping:
                    builder.Append('\n').Append(TrainingSubtitles.TapePrefix).Append(TrainingSubtitles.Fix(a.TapeCheck));
                    break;
            }
            return builder.ToString();
        }

        void AppendHand(string hand, PressureHandTracking tracking, bool inZone)
        {
            builder.Append(hand).Append(' ').Append(TrainingSubtitles.Tracking(tracking));
            if (inZone)
                builder.Append(TrainingSubtitles.Separator).Append(TrainingSubtitles.HandInZone);
        }

        static string HandName(InteractorHandedness hand) =>
            hand == InteractorHandedness.Left ? TrainingSubtitles.LeftHand
            : hand == InteractorHandedness.Right ? TrainingSubtitles.RightHand
            : string.Empty;

        void Apply(TMP_Text text, string value, Color color)
        {
            text.font = koreanFont;
            text.color = color;
            if (text.text != value)
                text.text = value;
        }

        public bool TryValidate(out string error)
        {
            error = null;
            if (dressing == null || application == null || koreanFont == null || noticeText == null || statusText == null || bandageText == null ||
                progressText == null || historyText == null || (writesGuidance && (titleText == null || instructionText == null)))
                error = $"PressureBandageStatusBoard '{name}' has a missing reference (dressing, application, font or a text).";
            return error == null;
        }
    }
}
