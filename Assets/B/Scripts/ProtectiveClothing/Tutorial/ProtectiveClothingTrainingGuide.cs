using System;
using System.Collections.Generic;
using System.Linq;
using MilTrainSimVR.B.TrainingUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MilTrainSimVR.B.ProtectiveClothing.Tutorial
{
    // Course guidance of the course board while practicing: the step row (six display-only boxes in the course board colours:
    // done, current, ahead; and the step line), the practice panel's one-action instruction and one status line, and the one
    // target ring of the current part's zone. The top-level panels and the step row's visibility are switched by
    // ProtectiveClothingStartScreen only. In the course scene this is the only writer of guidance and the ring: the equipment
    // guide (PC_Board with its development reset button) is saved inactive and the start gate refuses to start while it runs.
    //
    // One part per frame, the course's CurrentPart (ProtectiveClothingTrainingLink: the runner's step), and the step box, the
    // step line, the instruction and the ring all name that same part. Instruction, first that applies: setup invalid; reset
    // failed; equipment paused; user body paused; focus lost; headset not tracked; the part's holding hand not tracked; a
    // glove in the hand it is meant for (take it with the other hand); the glove's target hand not tracked; the part held
    // inside its zone before arming (take it a little away; never an overboot, which needs no arming); the part held (bring it
    // to its zone); otherwise grab it (gloves: which hand grabs and which hand it goes to; overboots: the zone below that side's
    // knee). The equipment
    // lines are ProtectiveClothingSubtitles (not repeated). The ring shows only with the "take it away" and "bring" lines: never
    // while a tracking line asks to wait, never two at once.
    //
    // Pickup marker (optional; linked in the training field scenes): with the "grab it" line, an arrow and the part's name over that
    // same part's pickup on the workbench (equipment.Piece(CurrentPart), the piece the step allows), so the part to take is
    // found by sight. It hides as soon as the part is held (the ring at the body takes over) and comes back when it is let go
    // unworn. Never over a worn part or another step's part; hidden outside practice and while a training field holds the run.
    // Display only: no collider, no UI raycast target.
    [DefaultExecutionOrder(105)]
    [DisallowMultipleComponent]
    public sealed class ProtectiveClothingTrainingGuide : MonoBehaviour
    {
        const int RingSegments = 48;

        [SerializeField] ProtectiveClothingTrainingStart trainingStart;
        [SerializeField] ProtectiveClothingTrainingLink link;
        [SerializeField] TMP_FontAsset koreanFont;
        [Header("Step row (above the practice panel)")]
        [Tooltip("One display-only box per course step, in order. Not buttons.")]
        [SerializeField] Image[] stepBoxes = Array.Empty<Image>();
        [SerializeField] TMP_Text stepText;
        [Header("Practice panel")]
        [Tooltip("Purpose line of the current step's part, above the action. Optional.")]
        [SerializeField] TMP_Text purposeText;
        [SerializeField] TMP_Text instructionText;
        [Tooltip("One line: worn count and the only part this step allows.")]
        [SerializeField] TMP_Text statusText;
        [Header("Target ring")]
        [Tooltip("Course ring (LineRenderer in local space, loop), saved hidden; its points are written at runtime from the zone radius.")]
        [SerializeField] LineRenderer marker;
        [Tooltip("The XR camera the ring turns to face.")]
        [SerializeField] Transform viewer;
        [Header("Pickup marker (training field; optional)")]
        [Tooltip("Arrow over the part to grab now (no collider), saved hidden. Empty: no pickup marker (the course's own scene).")]
        [SerializeField] Transform pickupMarker;
        [Tooltip("Name line of the pickup marker (3D text, no raycast).")]
        [SerializeField] TMP_Text pickupLabel;

        readonly Vector3[] ring = new Vector3[RingSegments];
        bool valid;
        int shownStep = -2;
        string shownInstruction;
        string shownStatus;
        string shownPickup;
        float ringRadius = -1f;

        public ProtectiveClothingTrainingLink Link => link;
        public ProtectiveClothingTrainingStart TrainingStart => trainingStart;
        public IReadOnlyList<Image> StepBoxes => stepBoxes;
        public LineRenderer Marker => marker;
        public IEnumerable<TMP_Text> Texts => new[] { stepText, instructionText, statusText };

        void Awake()
        {
            valid = TryValidate(out var error);
            if (!valid)
            {
                Debug.LogError($"[ProtectiveClothingTraining] {error}", this);
                return;
            }
            marker.gameObject.SetActive(false);
            if (pickupMarker != null)
                pickupMarker.gameObject.SetActive(false);
            ShowRow(-1);
        }

        // Practicing: the gate started the run and this run has not ended with a summary (the completion panel shows then).
        public bool IsPracticing => valid && trainingStart.State == ProtectiveClothingStartState.Started && link.IsCurrentRun && link.Summary == null;

        // After the runner (0), the equipment (50) and the link (55): this frame's wear state and step.
        void LateUpdate()
        {
            if (!valid)
                return;
            if (!IsPracticing)
            {
                ShowMarker(null);
                ShowPickup(null);
                ShowRow(-1);
                return;
            }

            var part = link.CurrentPart;
            ShowRow(link.CurrentStep);
            ProtectiveClothingPiece target = null;
            ProtectiveClothingPiece pickup = null;
            Show(instructionText, Instruction(part, ref target, ref pickup), ref shownInstruction);
            var equipment = link.Equipment;
            Show(statusText, part.HasValue
                ? string.Format(TrainingSubtitles.StatusFormat, equipment.WornCount, ProtectiveClothingTrainingIds.AllParts.Length, TrainingSubtitles.Name(part.Value))
                : string.Empty, ref shownStatus);
            // A training field's stop confirm holds the run: no marker meanwhile.
            var held = link.Runner != null && link.Runner.Held;
            ShowMarker(held ? null : target);
            ShowPickup(held ? null : pickup);
        }

        string Instruction(ProtectiveClothingPart? part, ref ProtectiveClothingPiece target, ref ProtectiveClothingPiece pickup)
        {
            var equipment = link.Equipment;
            var body = equipment.Body;
            if (!equipment.IsValid || !body.IsValid)
                return ProtectiveClothingSubtitles.SetupInvalid;
            if (equipment.IsFaulted)
                return ProtectiveClothingSubtitles.ResetFailed;
            if (!equipment.isActiveAndEnabled)
                return ProtectiveClothingSubtitles.EquipmentPaused;
            if (!body.IsRunning)
                return ProtectiveClothingSubtitles.BodyPaused;
            if (!link.IsRunning)
                return TrainingSubtitles.TrainingStopped;
            if (!Application.isFocused)
                return ProtectiveClothingSubtitles.Paused;
            if (!part.HasValue)
                return TrainingSubtitles.WaitingAdvance;
            if (!body.HeadTracked)
                return ProtectiveClothingSubtitles.HeadTracking;

            var piece = equipment.Piece(part.Value);
            if (!piece.Grab.isSelected)
            {
                if (!equipment.IsWorn(part.Value))
                    pickup = piece;
                return TrainingSubtitles.Grab(part.Value);
            }
            if (equipment.HandTracking(piece) != ProtectiveClothingHandTracking.Tracked)
                return ProtectiveClothingSubtitles.Tracking;
            if (equipment.IsHeldByWrongHand(piece))
                return ProtectiveClothingSubtitles.SwapGlove(part.Value);
            if (equipment.ZoneTracking(piece) != ProtectiveClothingHandTracking.Tracked)
                return ProtectiveClothingSubtitles.Tracking;
            target = piece;
            return equipment.IsWaitingToClearZone(piece) ? ProtectiveClothingSubtitles.MoveAway : ProtectiveClothingSubtitles.Bring(part.Value);
        }

        // ---------- Step row ----------

        // Course board step colours (TrainingBoardStyle: done, current, ahead); step < 0 clears the row (start, completion, reset).
        void ShowRow(int step)
        {
            if (shownStep == step)
                return;
            shownStep = step;
            for (var i = 0; i < stepBoxes.Length; i++)
                stepBoxes[i].color = TrainingBoardStyle.StepColor(i, step);
            var order = ProtectiveClothingTrainingIds.Order;
            stepText.font = koreanFont;
            stepText.text = step >= 0 && step < order.Length
                ? string.Format(TrainingSubtitles.StepLineFormat, step + 1, order.Length, TrainingSubtitles.StepName(order[step]))
                : string.Empty;
            if (purposeText != null)
            {
                purposeText.font = koreanFont;
                purposeText.text = step >= 0 && step < order.Length ? TrainingSubtitles.Purpose(order[step]) : string.Empty;
            }
        }

        // ---------- Ring ----------

        void ShowMarker(ProtectiveClothingPiece target)
        {
            if (target == null)
            {
                if (marker.gameObject.activeSelf)
                    marker.gameObject.SetActive(false);
                return;
            }
            var body = link.Equipment.Body;
            var radius = body.ZoneRadius(target.Part);
            if (!Mathf.Approximately(radius, ringRadius))
            {
                for (var i = 0; i < RingSegments; i++)
                {
                    var a = Mathf.PI * 2f * i / RingSegments;
                    ring[i] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius;
                }
                marker.loop = true;
                marker.useWorldSpace = false;
                marker.positionCount = RingSegments;
                marker.SetPositions(ring);
                ringRadius = radius;
            }
            var centre = body.ZonePoint(target.Part).position;
            var facing = viewer != null ? centre - viewer.position : Vector3.forward;
            marker.transform.SetPositionAndRotation(centre, facing.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(facing) : Quaternion.identity);
            if (!marker.gameObject.activeSelf)
                marker.gameObject.SetActive(true);
        }

        // ---------- Pickup marker ----------

        // Over the top of the part's grab collider (world up), turned about the vertical to face the viewer.
        void ShowPickup(ProtectiveClothingPiece piece)
        {
            if (pickupMarker == null)
                return;
            if (piece == null || piece.GrabCollider == null)
            {
                if (pickupMarker.gameObject.activeSelf)
                    pickupMarker.gameObject.SetActive(false);
                return;
            }
            var bounds = piece.GrabCollider.bounds;
            var top = new Vector3(bounds.center.x, bounds.max.y + 0.02f, bounds.center.z);
            var facing = viewer != null ? Vector3.ProjectOnPlane(top - viewer.position, Vector3.up) : Vector3.forward;
            pickupMarker.SetPositionAndRotation(top, facing.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(facing, Vector3.up) : Quaternion.identity);
            var label = TrainingSubtitles.Pickup(piece.Part);
            if (pickupLabel != null && label != shownPickup)
            {
                shownPickup = label;
                pickupLabel.font = koreanFont;
                pickupLabel.text = label;
            }
            if (!pickupMarker.gameObject.activeSelf)
                pickupMarker.gameObject.SetActive(true);
        }

        // Retry step (ProtectiveClothingTrainingReset): ring off, step row grey and empty, texts and caches cleared.
        public void ClearForReset()
        {
            if (!valid)
                return;
            ShowMarker(null);
            ShowPickup(null);
            shownStep = -2;
            ShowRow(-1);
            shownInstruction = shownStatus = null;
            instructionText.text = string.Empty;
            statusText.text = string.Empty;
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
            if (koreanFont == null)
                missing.Add(nameof(koreanFont));
            if (stepText == null || instructionText == null || statusText == null)
                missing.Add("a text");
            if (marker == null)
                missing.Add(nameof(marker));
            if (stepBoxes == null || stepBoxes.Length != ProtectiveClothingTrainingIds.StepCount || stepBoxes.Any(b => b == null))
                missing.Add($"{ProtectiveClothingTrainingIds.StepCount} step boxes");
            error = missing.Count > 0 ? $"ProtectiveClothingTrainingGuide '{name}' missing references: {string.Join(", ", missing)}." : null;
            if (error == null && trainingStart.Link != link)
                error = $"ProtectiveClothingTrainingGuide '{name}': the start gate must use the same training link.";
            if (error == null && new HashSet<Image>(stepBoxes).Count != stepBoxes.Length)
                error = $"ProtectiveClothingTrainingGuide '{name}': each step box must be a different image.";
            if (error == null && new HashSet<TMP_Text>(Texts).Count != 3)
                error = $"ProtectiveClothingTrainingGuide '{name}': step, instruction and status must be different texts.";
            if (error == null && purposeText != null && Texts.Contains(purposeText))
                error = $"ProtectiveClothingTrainingGuide '{name}': the purpose text must differ from the step, instruction and status texts.";
            return error == null;
        }
    }
}
