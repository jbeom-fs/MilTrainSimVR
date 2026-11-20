using System;
using System.Collections.Generic;
using System.Linq;
using MilTrainSimVR.B.TrainingUI;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace MilTrainSimVR.B.BleedingControl.PressureBandage.Tutorial
{
    // Course guidance of the board: the step row (seven display-only boxes and the step line, shown above the practice panel),
    // the practice panel's one-action instruction, progress line and check card (title, current item, button), and which guide
    // markers show. The top-level panels and the step row's visibility are switched by PressureBandageStartScreen only;
    // PressureBandageStatusBoard writes nothing here.
    //
    // While practicing (the run this course started is running), one action is chosen from the first requirement that
    // does not hold now (course order) and the current equipment state, and the step box, the step line, the instruction, the
    // marker and the check card all follow that same action. Its step is the step that brings the requirement about; when that
    // step lies before the runner's current step (an earlier result was lost: dressing off the wound, wrap cancelled, check
    // withdrawn), the step line carries the recovery form. The runner and the requirements are only read, never changed. A
    // precondition of the action (one hand holding the dressing for the pre-wrap check, tracking back, app focus) is asked for
    // first, with the check card hidden until the check itself can be taken. From the wrap to the tape the dressing need not be
    // held, so no hold is asked for there. The start and completion screens show no step (the run completes by itself on the
    // post-wrap check; there is no final check screen).
    // Step boxes take the course board colours (TrainingBoardStyle): before the guided step done, the guided step current, later
    // steps ahead; on a recovery the guided (earlier) step is the current one. One progress line under the action
    // shows the owner's current ratio while that step is guided: direct pressure (contact requirement) "압박 유지 N%" from
    // PressureDressing.ContactProgress, wrap (wrap requirement) "붕대 감기 N%" from PressureBandageApplication.WrapProgress. Read
    // only, never both at once; it is empty on every other action and screen and cleared by the retry.
    //
    // The check card shows one item of the active check (situation, pre-wrap, post-wrap) at a time; its button is taken only while
    // PressureBandageTrainingLink accepts that input right now, and the link records it as before (one input per item, in order,
    // withdrawn with the application attempt). A refused input keeps nothing; the state-driven action shows the recovery.
    //
    // Wound guide (optional; set by the training field menu): an outline on the skin around the designated wound (the dressing's
    // PressureBandageWound frame itself: its centre is the contact check's centre) and an arrow above it along the skin normal,
    // shown while the guided action is the situation check or putting the dressing on the wound (also again when the dressing has
    // left it), and hidden once the dressing covers it, so wrap, end and tape never sit under it. Display only: a bigger outline
    // than the wound radius never widens the contact check. It follows the wound frame every frame (station moves, seated pose).
    [DefaultExecutionOrder(105)]
    [DisallowMultipleComponent]
    public sealed class PressureBandageTrainingGuide : MonoBehaviour
    {
        const float IssueNoticeSeconds = 2.5f;

        [SerializeField] PressureBandageTrainingStart trainingStart;
        [SerializeField] PressureBandageTrainingLink link;
        [Tooltip("Wrap zone outline, end attach range ring and tape frame of the equipment; shown only while the current action is " +
                 "the wrap, end or tape placing move itself.")]
        [SerializeField] PressureBandageGuide equipmentGuide;
        [SerializeField] PressureBandageCourseMarker marker;
        [SerializeField] TMP_FontAsset koreanFont;
        [Header("Step row (above the practice panel)")]
        [Tooltip("One display-only box per course step, in order. Not buttons.")]
        [SerializeField] Image[] stepBoxes = Array.Empty<Image>();
        [SerializeField] TMP_Text stepText;
        [Header("Practice panel")]
        [Tooltip("Purpose line of the guided step (the step of the step box and step line, also on a recovery). Optional; set by the " +
                 "board UI menu.")]
        [SerializeField] TMP_Text purposeText;
        [SerializeField] TMP_Text instructionText;
        [Tooltip("One line under the action while the direct pressure or the wrap step is guided: the owner's ratio as a percent. " +
                 "Empty otherwise.")]
        [FormerlySerializedAs("contactProgressText")]
        [SerializeField] TMP_Text progressText;
        [Header("Check card (inside the practice panel)")]
        [SerializeField] GameObject checkArea;
        [SerializeField] TMP_Text checkTitleText;
        [Tooltip("The current item's information only.")]
        [SerializeField] TMP_Text checkItemsText;
        [SerializeField] Button checkButton;
        [SerializeField] TMP_Text checkButtonText;
        [Header("Wound guide (training field; optional)")]
        [Tooltip("Root of the wound outline and arrow (no collider), saved hidden. Empty: no wound guide (the course's own scene).")]
        [SerializeField] Transform woundGuide;
        [Tooltip("Outline on the skin (LineRenderer, local space, loop) around the wound centre.")]
        [SerializeField] LineRenderer woundOutline;
        [Tooltip("Outline radius (m), display only (the contact check keeps the wound's own radius).")]
        [SerializeField, Min(0.01f)] float woundOutlineRadius = 0.04f;

        // One action of this frame: its step (index), the line, the marked part, whether the action is a wrap / end / tape placing
        // move shown by the equipment markers, and the check to show (None: card hidden). Line and markers come from this one choice.
        struct Guidance
        {
            public int step;
            public string instruction;
            public Transform point;
            public bool equipmentMarks;
            public PressureCheckKind check;
            public PressureRequirement? requirement;
            public bool wound;
        }

        Button subscribedButton;
        bool valid;
        PressureCheckKind shownCheck;
        (int step, bool recovery) shownRow = (-2, false);
        string shownInstruction;
        string shownTitle;
        string shownItems;
        string shownButton;
        // Format and percent on the progress line; percent -1 hidden (empty), -2 nothing written yet.
        (string format, int percent) shownProgress = (null, -2);

        public PressureBandageTrainingLink Link => link;
        public PressureBandageTrainingStart TrainingStart => trainingStart;
        public GameObject CheckArea => checkArea;
        public Button CheckButton => checkButton;
        public IReadOnlyList<Image> StepBoxes => stepBoxes;
        public TMP_Text ProgressText => progressText;
        public IEnumerable<TMP_Text> Texts => new[] { stepText, instructionText, progressText, checkTitleText, checkItemsText, checkButtonText };

        void Awake()
        {
            valid = TryValidate(out var error);
            if (!valid)
            {
                Debug.LogError($"[PressureBandageTraining] {error}", this);
                if (checkButton != null)
                    checkButton.interactable = false;
                return;
            }
            checkButton.interactable = false;
            ShowRow(-1, false);
            ShowProgress(null, -1);
            ShowWound(false);
        }

        void OnEnable()
        {
            if (subscribedButton == null && checkButton != null)
            {
                subscribedButton = checkButton;
                subscribedButton.onClick.AddListener(OnCheckClicked);
            }
        }

        void OnDisable()
        {
            if (subscribedButton != null)
                subscribedButton.onClick.RemoveListener(OnCheckClicked);
            subscribedButton = null;
        }

        // Practicing: the run this course started is running (after completion the completion panel shows instead).
        public bool IsPracticing => valid && trainingStart.State == PressureTrainingStartState.Started && link.IsRunning;

        void LateUpdate()
        {
            if (!valid)
                return;
            if (!IsPracticing)
            {
                ShowWound(false);
                equipmentGuide.Suppressed = true;
                marker.Show(null);
                SetCheck(PressureCheckKind.None);
                ShowRow(-1, false);
                ShowProgress(null, -1);
                return;
            }

            var g = Current();
            equipmentGuide.Suppressed = !g.equipmentMarks;
            ShowRow(g.step, g.step < PressureBandageTrainingIds.StepIndex(link.Runner.CurrentStepId));
            Show(instructionText, g.instruction, ref shownInstruction);
            if (g.requirement == PressureRequirement.Contact)
                ShowProgress(TrainingSubtitles.ContactProgressFormat, Percent(link.Dressing.ContactProgress));
            else if (g.requirement == PressureRequirement.Wrap)
                ShowProgress(TrainingSubtitles.WrapProgressFormat, Percent(link.BandageApplication.WrapProgress));
            else
                ShowProgress(null, -1);
            marker.Show(g.point);
            // A training field's stop confirm holds the run: the wound guide hides meanwhile.
            ShowWound(g.wound && !link.Runner.Held);
            SetCheck(g.check);
        }

        // ---------- Wound guide ----------

        const int OutlineSegments = 40;
        float outlineRadius = -1f;

        void ShowWound(bool show)
        {
            if (woundGuide == null)
                return;
            if (!show)
            {
                if (woundGuide.gameObject.activeSelf)
                    woundGuide.gameObject.SetActive(false);
                return;
            }
            var wound = link.Dressing.Wound;
            // Local +Y = skin normal (the arrow points down it), local XZ = the skin plane (the outline lies in it).
            woundGuide.SetPositionAndRotation(wound.Center, Quaternion.LookRotation(wound.transform.forward, wound.Normal));
            if (woundOutline != null && !Mathf.Approximately(outlineRadius, woundOutlineRadius))
            {
                var points = new Vector3[OutlineSegments];
                for (var i = 0; i < OutlineSegments; i++)
                {
                    var a = Mathf.PI * 2f * i / OutlineSegments;
                    points[i] = new Vector3(Mathf.Cos(a) * woundOutlineRadius, 0.002f, Mathf.Sin(a) * woundOutlineRadius);
                }
                woundOutline.useWorldSpace = false;
                woundOutline.loop = true;
                woundOutline.positionCount = OutlineSegments;
                woundOutline.SetPositions(points);
                outlineRadius = woundOutlineRadius;
            }
            if (!woundGuide.gameObject.activeSelf)
                woundGuide.gameObject.SetActive(true);
        }

        // ---------- Progress line (direct pressure, wrap) ----------

        // The owner's current ratio, whole percent rounded down; read only, so no time, history or equipment state is touched.
        // Direct pressure: the dressing's continuous contact ratio (0 after a break); its practice record (history) is never shown
        // as progress. Wrap: the application's wrap progress (kept through a pause, 0 after a cancel or the retry). When the owner
        // completes, the link sees it in the same frame and the line moves on with the action, without waiting for a full display.
        static int Percent(float ratio) => Mathf.Clamp(Mathf.FloorToInt(ratio * 100f), 0, 100);

        // format null or percent < 0: hidden (empty line).
        void ShowProgress(string format, int percent)
        {
            if (format == null || percent < 0)
            {
                format = null;
                percent = -1;
            }
            if (shownProgress == (format, percent))
                return;
            shownProgress = (format, percent);
            progressText.font = koreanFont;
            progressText.text = format != null ? string.Format(format, percent) : string.Empty;
        }

        // ---------- One action ----------

        Guidance Current()
        {
            var g = new Guidance { requirement = link.FirstMissing() };
            if (g.requirement == null)
            {
                g.step = PressureBandageTrainingIds.StepIndex(link.Runner.CurrentStepId);
                g.instruction = TrainingSubtitles.WaitingAdvance;
                return g;
            }

            var requirement = g.requirement.Value;
            g.step = PressureBandageTrainingIds.StepIndex(PressureBandageTrainingIds.StepOf(requirement));
            var d = link.Dressing;
            var a = link.BandageApplication;
            var anchors = a.Kit.AnchorTransforms;
            if (!d.InputFocused)
            {
                g.instruction = TrainingSubtitles.ActPaused;
                return g;
            }

            switch (requirement)
            {
                case PressureRequirement.Situation:
                    g.wound = true;
                    g.instruction = TrainingSubtitles.ActCheckSituation;
                    g.check = link.ActiveCheck == PressureCheckKind.Situation ? PressureCheckKind.Situation : PressureCheckKind.None;
                    break;
                case PressureRequirement.Dressing:
                    g.wound = true;
                    g.instruction = DressingAction(d, anchors, out g.point);
                    break;
                case PressureRequirement.Contact:
                    g.instruction = ContactAction(d, anchors, out g.point);
                    break;
                case PressureRequirement.PreCheck:
                    if (d.IsHeld && d.HoldingHandTracking != PressureHandTracking.Tracked)
                    {
                        g.instruction = TrainingSubtitles.ActTracking;
                    }
                    else if (!d.IsHoldValid)
                    {
                        g.instruction = TrainingSubtitles.ActHoldDressing;
                        g.point = anchors.dressingPress;
                    }
                    else if (link.ActiveCheck == PressureCheckKind.PreWrap)
                    {
                        g.instruction = ItemAction(PressureCheckKind.PreWrap, NextItem(PressureCheckKind.PreWrap));
                        g.check = PressureCheckKind.PreWrap;
                    }
                    else
                    {
                        g.instruction = BandageAction(d, a, anchors, out g.point, out g.equipmentMarks);
                    }
                    break;
                case PressureRequirement.Wrap:
                case PressureRequirement.Fix:
                    g.instruction = BandageAction(d, a, anchors, out g.point, out g.equipmentMarks);
                    break;
                default:
                    if (link.ActiveCheck == PressureCheckKind.PostWrap)
                    {
                        g.instruction = ItemAction(PressureCheckKind.PostWrap, NextItem(PressureCheckKind.PostWrap));
                        g.check = PressureCheckKind.PostWrap;
                    }
                    else
                    {
                        g.instruction = TrainingSubtitles.WaitingAdvance;
                    }
                    break;
            }
            return g;
        }

        // Dressing not on the wound now: get it into the hand, then onto the wound with the right face.
        static string DressingAction(PressureDressing d, PressureBandageKit.Anchors anchors, out Transform point)
        {
            point = null;
            if (d.IsHeld && d.HoldingHandTracking != PressureHandTracking.Tracked)
                return TrainingSubtitles.ActTracking;
            if (!d.IsHeld)
            {
                point = anchors.dressingGrab;
                return TrainingSubtitles.ActGrabDressing;
            }
            return d.CurrentCheck == PressureDressingCheck.WrongFace ? TrainingSubtitles.ActFlipDressing : TrainingSubtitles.ActCoverWound;
        }

        // Dressing on the wound, contact practice not yet done: both hands on the dressing until PressureDressing's continuous
        // contact completes (its time and ratio stay owned there).
        static string ContactAction(PressureDressing d, PressureBandageKit.Anchors anchors, out Transform point)
        {
            point = null;
            if (d.IsHeld && d.HoldingHandTracking != PressureHandTracking.Tracked)
                return TrainingSubtitles.ActTracking;
            if (d.IsTwoHandContactValid)
                return TrainingSubtitles.ActKeepHands;
            if (d.LeftTracking != PressureHandTracking.Tracked || d.RightTracking != PressureHandTracking.Tracked)
                return TrainingSubtitles.ActTracking;
            point = anchors.dressingPress;
            return TrainingSubtitles.ActStackHands;
        }

        // Wrap and fixing (dressing on the wound; it need not be held): preconditions first (a free hand for the part, tracking of
        // the hand selecting the roll, end or tape), then the part of the current application stage. Equipment markers (wrap
        // zone outline, end attach range ring, tape frame) are on only for the placing move itself (roll, end or tape in a tracked
        // hand); a grab action marks its grab point only and a precondition shows neither, so one target is emphasised at a time.
        // The wrap needs no move round the arm: the roll is brought into the zone and kept there while the application wraps.
        static string BandageAction(PressureDressing d, PressureBandageApplication a, PressureBandageKit.Anchors anchors, out Transform point,
            out bool equipmentMarks)
        {
            point = null;
            equipmentMarks = false;
            switch (a.Stage)
            {
                case PressureBandageStage.Ready:
                case PressureBandageStage.Wrapping:
                    if (!a.IsRollHeld)
                    {
                        // Dressing in one hand and tape in the other: no hand is free for the roll (one hand, one kit part).
                        if (a.IsTapeHeld && d.IsHeld)
                            return TrainingSubtitles.ActPutTapeDown;
                        point = anchors.rollGrab;
                        return a.Stage == PressureBandageStage.Ready ? TrainingSubtitles.ActGrabRoll : TrainingSubtitles.ActRegrabRoll;
                    }
                    if (a.RollHandTracking != PressureHandTracking.Tracked)
                        return TrainingSubtitles.ActTracking;
                    equipmentMarks = true;
                    return a.Block == PressureWrapBlock.OutOfZone ? TrainingSubtitles.ActMoveRollToZone : TrainingSubtitles.ActHoldRollInZone;
                case PressureBandageStage.EndPlacement:
                    if (!a.IsEndHeld && a.IsTapeHeld && d.IsHeld)
                        return TrainingSubtitles.ActPutTapeDown;
                    if (a.IsEndHeld)
                    {
                        // The end attaches by itself near its mark, but only while its selecting hand is tracked: tracking
                        // comes first (no marks). There is no face, tilt, turn or release line for the end.
                        if (a.EndHandTracking != PressureHandTracking.Tracked)
                            return TrainingSubtitles.ActTracking;
                        equipmentMarks = true;
                        return a.IsEndRearming ? TrainingSubtitles.ActEndAwayFirst : TrainingSubtitles.ActEndToMark;
                    }
                    point = anchors.endGrab;
                    return a.IsRecent(PressureWrapIssue.EndRejected, IssueNoticeSeconds) ? TrainingSubtitles.ActRegrabEnd : TrainingSubtitles.ActGrabEnd;
                case PressureBandageStage.Taping:
                    if (a.IsTapeHeld)
                    {
                        if (a.TapeHandTracking != PressureHandTracking.Tracked)
                            return TrainingSubtitles.ActTracking;
                        equipmentMarks = true;
                        return a.IsTapeRearming ? TrainingSubtitles.ActTapeAwayFirst : TrainingSubtitles.ActTapeToMark;
                    }
                    point = anchors.tapeGrab;
                    return a.IsRecent(PressureWrapIssue.TapeRejected, IssueNoticeSeconds) ? TrainingSubtitles.ActRegrabTape : TrainingSubtitles.ActGrabTape;
                default:
                    return TrainingSubtitles.WaitingAdvance;
            }
        }

        // ---------- Step row ----------

        // Current step box in the accent colour, every other box grey; step < 0 clears the row (start, completion, reset).
        void ShowRow(int step, bool recovery)
        {
            if (shownRow == (step, recovery))
                return;
            shownRow = (step, recovery);
            for (var i = 0; i < stepBoxes.Length; i++)
                stepBoxes[i].color = TrainingBoardStyle.StepColor(i, step);
            var steps = PressureBandageTrainingIds.Steps;
            var line = step >= 0 && step < steps.Length
                ? string.Format(recovery ? TrainingSubtitles.StepRecoveryFormat : TrainingSubtitles.StepLineFormat, step + 1, steps.Length,
                    PressureBandageTrainingIds.StepName(steps[step].step))
                : string.Empty;
            stepText.font = koreanFont;
            stepText.text = line;
            if (purposeText != null)
            {
                purposeText.font = koreanFont;
                purposeText.text = step >= 0 && step < steps.Length ? PressureBandageTrainingIds.StepPurpose(steps[step].step) : string.Empty;
            }
        }

        // ---------- Check card ----------

        int NextItem(PressureCheckKind kind) => Mathf.Min(link.ItemsDone(kind), PressureBandageTrainingLink.ItemCount(kind) - 1);

        void SetCheck(PressureCheckKind kind)
        {
            shownCheck = kind;
            var on = kind != PressureCheckKind.None;
            if (checkArea.activeSelf != on)
                checkArea.SetActive(on);
            if (!on)
            {
                checkButton.interactable = false;
                return;
            }

            if (kind == PressureCheckKind.Situation)
            {
                Show(checkTitleText, TrainingSubtitles.CheckTitleSituation, ref shownTitle);
                Show(checkItemsText, TrainingSubtitles.SituationInfo, ref shownItems);
            }
            else
            {
                var index = NextItem(kind);
                Show(checkTitleText, string.Format(TrainingSubtitles.CheckTitleFormat, ItemSource(kind, index), index + 1,
                    PressureBandageTrainingLink.ItemCount(kind)), ref shownTitle);
                Show(checkItemsText, ItemInfo(kind, index), ref shownItems);
            }
            Show(checkButtonText, TrainingSubtitles.CheckButton, ref shownButton);
            checkButton.interactable = link.CanConfirmCheck(kind, out _);
        }

        // Pre-wrap: colour, warmth, sensation. Post-wrap: re-bleeding, colour, warmth, sensation (same order as the link records).
        static string ItemAction(PressureCheckKind kind, int index) => (kind == PressureCheckKind.PostWrap ? index - 1 : index) switch
        {
            -1 => TrainingSubtitles.ActCheckBleeding,
            0 => TrainingSubtitles.ActCheckColor,
            1 => TrainingSubtitles.ActCheckWarmth,
            _ => TrainingSubtitles.ActCheckSensation
        };

        static string ItemSource(PressureCheckKind kind, int index) =>
            index == PressureBandageTrainingLink.ItemCount(kind) - 1 ? TrainingSubtitles.CheckSourceAnswer : TrainingSubtitles.CheckSourceCard;

        static string ItemInfo(PressureCheckKind kind, int index) => kind == PressureCheckKind.PreWrap
            ? index switch
            {
                0 => TrainingSubtitles.PreInfoColor,
                1 => TrainingSubtitles.PreInfoWarmth,
                _ => TrainingSubtitles.PreInfoSensation
            }
            : index switch
            {
                0 => TrainingSubtitles.PostInfoBleeding,
                1 or 2 => TrainingSubtitles.PostInfoSame,
                _ => TrainingSubtitles.PostInfoSensation
            };

        // Only reachable through the shown check button; it goes off at once, so a second click or hand in the same frame is
        // ignored. The link re-checks the conditions now and again on this frame's final equipment state before recording the
        // shown check's next item (the card shows it only once recorded); a refused or dropped input keeps nothing.
        void OnCheckClicked()
        {
            if (!valid || !checkButton.interactable || !IsPracticing || shownCheck == PressureCheckKind.None)
                return;
            checkButton.interactable = false;
            link.TryConfirmCheck(shownCheck);
        }

        // Retry step (PressureBandageTrainingReset): markers off, check card hidden, step row grey and empty, caches cleared.
        public void ClearForReset()
        {
            if (!valid)
                return;
            equipmentGuide.Suppressed = true;
            marker.Show(null);
            ShowWound(false);
            SetCheck(PressureCheckKind.None);
            shownInstruction = shownTitle = shownItems = shownButton = null;
            shownRow = (-2, false);
            ShowRow(-1, false);
            instructionText.text = string.Empty;
            shownProgress = (null, -2);
            ShowProgress(null, -1);
        }

        void Show(TMP_Text target, string value, ref string shown)
        {
            if (value == shown)
                return;
            shown = value;
            target.font = koreanFont;
            target.text = value;
        }

        public bool TryValidate(out string error)
        {
            var missing = new List<string>();
            if (trainingStart == null)
                missing.Add(nameof(trainingStart));
            if (link == null)
                missing.Add(nameof(link));
            if (equipmentGuide == null)
                missing.Add(nameof(equipmentGuide));
            if (marker == null)
                missing.Add(nameof(marker));
            if (koreanFont == null)
                missing.Add(nameof(koreanFont));
            if (stepText == null || instructionText == null || progressText == null || checkTitleText == null || checkItemsText == null ||
                checkButtonText == null)
                missing.Add("a text");
            if (checkArea == null || checkButton == null)
                missing.Add("check area or button");
            if (stepBoxes == null || stepBoxes.Length != PressureBandageTrainingIds.Steps.Length || stepBoxes.Any(b => b == null))
                missing.Add($"{PressureBandageTrainingIds.Steps.Length} step boxes");
            error = missing.Count > 0 ? $"PressureBandageTrainingGuide '{name}' missing references: {string.Join(", ", missing)}." : null;
            if (error == null && (trainingStart.Link != link))
                error = $"PressureBandageTrainingGuide '{name}': the start gate must use the same training link.";
            if (error == null && (!checkButton.transform.IsChildOf(checkArea.transform) || !checkTitleText.transform.IsChildOf(checkArea.transform) ||
                                  !checkItemsText.transform.IsChildOf(checkArea.transform)))
                error = $"PressureBandageTrainingGuide '{name}': the check title, items and button must be inside the check area.";
            if (error == null && (progressText == instructionText || progressText.transform.IsChildOf(checkArea.transform)))
                error = $"PressureBandageTrainingGuide '{name}': the progress line must be its own text outside the check area.";
            if (error == null && new HashSet<Image>(stepBoxes).Count != stepBoxes.Length)
                error = $"PressureBandageTrainingGuide '{name}': each step box must be a different image.";
            return error == null;
        }
    }
}
