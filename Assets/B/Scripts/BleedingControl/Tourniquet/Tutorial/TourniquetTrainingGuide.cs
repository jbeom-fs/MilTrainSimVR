using System.Collections.Generic;
using MilTrainSimVR.B.Training;
using TMPro;
using UnityEngine;

namespace MilTrainSimVR.B.BleedingControl.Tourniquet.Tutorial
{
    // Training part of the operation panel: the step line ("3 / 6 · 막대 회전") and the one action to do now. Reads the runner's
    // current step and the link's device states; never changes either. TourniquetStatusBoard keeps the equipment part of the
    // same panel (progress, issue, time record) and is configured not to write these two texts, so each text has one writer.
    // While the runner is on step k, the action is step k's, unless an earlier state that was reached in this run is no longer
    // valid: then the device's recovery action is shown and the runner holds (the data's cumulative conditions), so the guide
    // never asks for a step the device cannot take now. A runner one or two frames behind a fast user only catches up through
    // the steps already done. The step line counts the six practice steps only; the final check and the completion have their
    // own panels (TourniquetFinalCheck, TourniquetCompletionPanel), shown by TourniquetStartScreen instead of this panel.
    //
    // Operation markers: the same guide target as the action text (step k, or the broken earlier state whose recovery action is
    // shown) picks the rings around the part to handle now, and only those are on: grab point, band end, windlass end, windlass
    // end and clip, time label, strap end. They are off when the target's device state is already valid (a runner still catching
    // up never highlights a part whose handle is already locked), when the device is held for placing (the placement preview
    // takes over), and outside a running practice step of this course's run (start, final check, completion, stopped, reset).
    //
    // Guide target: decided here only, once per frame (after the runner (0) and the link (106)), and shared read-only through
    // GuideStepIndex / IsRecovering. The step line, purpose, action and markers all name it; TourniquetBoardSteps (115) paints its
    // current box from it. While recovering, the step line reads "보완 k/6 · name" for the restored step k; the runner's step and
    // its records are untouched, and once the state is valid again the target is the runner's step again.
    //
    // Allowed zone (the green placement area of the device's limb target): this guide is its only writer during the training. On
    // only while the guide target is the place step and the device is not attached (TourniquetDevice.IsAttached), held or not;
    // off before the start, from the frame of the attachment (even if the runner is still on the place step), on later steps,
    // the final check, completion, stop and reset. Unlike the place marker it stays on while the device is held for placing.
    [DefaultExecutionOrder(110)]
    [DisallowMultipleComponent]
    public sealed class TourniquetTrainingGuide : MonoBehaviour
    {
        [SerializeField] TrainingRunner runner;
        [SerializeField] TourniquetTrainingLink link;
        [SerializeField] TMP_FontAsset koreanFont;
        [Tooltip("Operation panel title: the step line.")]
        [SerializeField] TMP_Text stepText;
        [Tooltip("Operation panel instruction: the current action.")]
        [SerializeField] TMP_Text instructionText;
        [Tooltip("Operation panel purpose line of the step whose action is shown (the recovery step while one is shown). Optional.")]
        [SerializeField] TMP_Text purposeText;

        [Header("Operation markers (outside the device; only this guide turns them on)")]
        [SerializeField] TourniquetGuideMarker placeMarker;
        [SerializeField] TourniquetGuideMarker bandMarker;
        [Tooltip("Windlass end: turn and seat steps.")]
        [SerializeField] TourniquetGuideMarker windlassMarker;
        [Tooltip("Clip entry: seat step, with the windlass end.")]
        [SerializeField] TourniquetGuideMarker clipMarker;
        [SerializeField] TourniquetGuideMarker timeLabelMarker;
        [SerializeField] TourniquetGuideMarker strapMarker;

        bool valid;
        string shownStep;
        string shownInstruction;
        string shownPurpose;
        string shownMarkerStep = string.Empty;
        // Last written allowed zone state; null: not written yet, the next write always applies.
        bool? shownZone;

        public TrainingRunner Runner => runner;
        public TourniquetTrainingLink Link => link;
        public TMP_Text StepText => stepText;
        public TMP_Text InstructionText => instructionText;
        public TourniquetGuideMarker[] Markers => new[] { placeMarker, bandMarker, windlassMarker, clipMarker, timeLabelMarker, strapMarker };

        // This frame's guide target: the practice step index whose step line, purpose, action and markers are shown; -1 when no
        // practice step is guided (not started, final check, completion, stopped, reset, invalid guide).
        public int GuideStepIndex { get; private set; } = -1;
        // True while the target is an earlier state of this run that is no longer valid (its recovery is shown), false otherwise.
        public bool IsRecovering { get; private set; }

        void Awake()
        {
            valid = TryValidate(out var error);
            if (!valid)
                Debug.LogError($"[TourniquetTraining] {error}", this);
            ShowMarkers(null);
            shownZone = null;
            ShowAllowedZone(false);
        }

        // Renderers only, so this is safe while the hierarchy is being deactivated; the next enable writes the state again.
        void OnDisable()
        {
            shownZone = null;
            ShowAllowedZone(false);
        }

        // Retry entry point (TourniquetTrainingReset): markers off and the text cache cleared, so a new run writes its texts again.
        public void ClearForReset()
        {
            ShowMarkers(null);
            shownZone = null;
            ShowAllowedZone(false);
            SetTarget(-1, false);
            shownStep = null;
            shownInstruction = null;
            shownPurpose = null;
            if (valid && purposeText != null)
                ApplyText(purposeText, string.Empty);
        }

        void LateUpdate()
        {
            if (valid)
                Refresh();
        }

        void Refresh()
        {
            if (!link.HasStartedRun)
            {
                SetTarget(-1, false);
                ShowMarkers(null);
                ShowAllowedZone(false);
                return;
            }

            string step, instruction;
            var purpose = string.Empty;
            string markerStep = null;
            var target = -1;
            var recovering = false;
            var index = TourniquetTrainingIds.StepIndex(runner.CurrentStepId);
            // The operation panel is hidden on the final check and completion screens; its texts still name that screen.
            if (link.IsCurrentRun && runner.Status == TrainingStatus.Completed)
            {
                step = TrainingSubtitles.CompletionTitle;
                instruction = TrainingSubtitles.CompletionNote;
            }
            else if (link.IsFinalCheckStep)
            {
                step = TrainingSubtitles.FinalCheckTitle;
                instruction = TrainingSubtitles.FinalCheckDescription;
            }
            else if (link.IsCurrentRun && runner.Status == TrainingStatus.Running && index >= 0)
            {
                var broken = -1;
                for (var i = 0; i < index && broken < 0; i++)
                {
                    if (link.WasReached(i) && !link.IsValidNow(i))
                        broken = i;
                }
                recovering = broken >= 0;
                target = recovering ? broken : index;
                step = StepLine(target, recovering);
                instruction = recovering ? RecoveryAction(TourniquetTrainingIds.Steps[target].step)
                    : StepAction(TourniquetTrainingIds.Steps[target].step);
                purpose = StepPurpose(TourniquetTrainingIds.Steps[target].step);
                if (!link.IsValidNow(target) && link.RunActive && link.Operation.IsReady && !(target == 0 && link.Device.IsHeld))
                    markerStep = TourniquetTrainingIds.Steps[target].step;
            }
            else
            {
                step = TrainingSubtitles.TrainingStoppedTitle;
                instruction = TrainingSubtitles.TrainingStopped;
            }

            SetTarget(target, recovering);
            ShowMarkers(markerStep);
            // target 0 exists only on a running place step of this course's run; the attachment itself turns the zone off.
            ShowAllowedZone(target == 0 && !link.Device.IsAttached);
            if (purposeText != null && purpose != shownPurpose)
            {
                shownPurpose = purpose;
                ApplyText(purposeText, purpose);
            }
            if (step == shownStep && instruction == shownInstruction)
                return;
            shownStep = step;
            shownInstruction = instruction;
            ApplyText(stepText, step);
            ApplyText(instructionText, instruction);
        }

        void SetTarget(int index, bool recovering)
        {
            GuideStepIndex = index;
            IsRecovering = index >= 0 && recovering;
        }

        // Step line of the guide target (practice step index 0..5): "k / 6 · name", or "보완 k/6 · name" while it is recovered.
        public static string StepLine(int index, bool recovering) =>
            string.Format(recovering ? TrainingSubtitles.StepRecoveryFormat : TrainingSubtitles.StepLineFormat, index + 1,
                TourniquetTrainingIds.Steps.Length, StepName(TourniquetTrainingIds.Steps[index].step));

        // Purpose line of a practice step (TrainingSubtitles), shown above its action.
        public static string StepPurpose(string stepId) => stepId switch
        {
            TourniquetTrainingIds.PlaceStep => TrainingSubtitles.PurposePlace,
            TourniquetTrainingIds.BandStep => TrainingSubtitles.PurposeBand,
            TourniquetTrainingIds.TurnStep => TrainingSubtitles.PurposeTurn,
            TourniquetTrainingIds.SecureStep => TrainingSubtitles.PurposeSecure,
            TourniquetTrainingIds.RecordStep => TrainingSubtitles.PurposeRecord,
            TourniquetTrainingIds.FastenStep => TrainingSubtitles.PurposeFasten,
            _ => string.Empty
        };

        // Markers of one practice step (null: none). Written only on a change.
        void ShowMarkers(string stepId)
        {
            if (stepId == shownMarkerStep)
                return;
            shownMarkerStep = stepId;
            foreach (var marker in Markers)
            {
                if (marker != null)
                    marker.gameObject.SetActive(stepId != null && MarkerOf(stepId, marker));
            }
        }

        // Allowed zone of the device's limb target. Written only on a change.
        void ShowAllowedZone(bool visible)
        {
            if (shownZone == visible)
                return;
            var zone = link != null && link.Device != null ? link.Device.Target : null;
            if (zone == null)
                return;
            shownZone = visible;
            zone.SetAllowedZoneVisible(visible);
        }

        bool MarkerOf(string stepId, TourniquetGuideMarker marker) => stepId switch
        {
            TourniquetTrainingIds.PlaceStep => marker == placeMarker,
            TourniquetTrainingIds.BandStep => marker == bandMarker,
            TourniquetTrainingIds.TurnStep => marker == windlassMarker,
            TourniquetTrainingIds.SecureStep => marker == windlassMarker || marker == clipMarker,
            TourniquetTrainingIds.RecordStep => marker == timeLabelMarker,
            TourniquetTrainingIds.FastenStep => marker == strapMarker,
            _ => false
        };

        public static string StepName(string stepId) => stepId switch
        {
            TourniquetTrainingIds.PlaceStep => TrainingSubtitles.StepPlace,
            TourniquetTrainingIds.BandStep => TrainingSubtitles.StepBand,
            TourniquetTrainingIds.TurnStep => TrainingSubtitles.StepTurn,
            TourniquetTrainingIds.SecureStep => TrainingSubtitles.StepSecure,
            TourniquetTrainingIds.RecordStep => TrainingSubtitles.StepRecord,
            TourniquetTrainingIds.FastenStep => TrainingSubtitles.StepFasten,
            _ => string.Empty
        };

        public static string StepAction(string stepId) => stepId switch
        {
            TourniquetTrainingIds.PlaceStep => TrainingSubtitles.Instruction,
            TourniquetTrainingIds.BandStep => TrainingSubtitles.InstructionPullBand,
            TourniquetTrainingIds.TurnStep => TrainingSubtitles.InstructionTurnWindlass,
            TourniquetTrainingIds.SecureStep => TrainingSubtitles.InstructionSeatWindlass,
            TourniquetTrainingIds.RecordStep => TrainingSubtitles.InstructionRecordTime,
            TourniquetTrainingIds.FastenStep => TrainingSubtitles.InstructionCoverStrap,
            _ => string.Empty
        };

        // Action that restores an earlier state. Only the turn amount can drop on this device (turning back before seating);
        // the other states hold once reached, so their own action is shown.
        public static string RecoveryAction(string stepId) =>
            stepId == TourniquetTrainingIds.TurnStep ? TrainingSubtitles.RecoverTurn : StepAction(stepId);

        void ApplyText(TMP_Text target, string value)
        {
            target.font = koreanFont;
            target.text = value;
        }

        public bool TryValidate(out string error)
        {
            var missing = new List<string>();
            if (runner == null)
                missing.Add(nameof(runner));
            if (link == null)
                missing.Add(nameof(link));
            if (koreanFont == null)
                missing.Add(nameof(koreanFont));
            if (stepText == null)
                missing.Add(nameof(stepText));
            if (instructionText == null)
                missing.Add(nameof(instructionText));
            if (System.Array.IndexOf(Markers, null) >= 0)
                missing.Add("operation markers");

            error = missing.Count > 0 ? $"TourniquetTrainingGuide '{name}' missing references: {string.Join(", ", missing)}." : null;
            if (error == null && link.Runner != runner)
                error = $"TourniquetTrainingGuide '{name}': runner is not the link's runner.";
            if (error == null && stepText == instructionText)
                error = $"TourniquetTrainingGuide '{name}': step and instruction texts must differ.";
            if (error == null && purposeText != null && (purposeText == stepText || purposeText == instructionText))
                error = $"TourniquetTrainingGuide '{name}': the purpose text must differ from the step and instruction texts.";
            if (error == null && new HashSet<TourniquetGuideMarker>(Markers).Count != Markers.Length)
                error = $"TourniquetTrainingGuide '{name}': each operation marker must be a different object.";
            return error == null;
        }
    }
}
