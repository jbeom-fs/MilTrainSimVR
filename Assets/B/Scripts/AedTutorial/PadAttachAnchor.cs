using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace MilTrainSimVR.B.AedTutorial
{
    public enum PadAttachCheck
    {
        Valid = 0,
        WrongPad = 1,
        Occupied = 2,
        TooFar = 3,
        WrongFacing = 4,
        WrongTwist = 5
    }

    // Pad placement point on the patient: guide marker, attach tolerances and occupancy.
    // Checks compare the pad's AttachPoint (adhesive face) with AttachTarget (skin surface); it never grabs or attaches by itself.
    public sealed class PadAttachAnchor : MonoBehaviour
    {
        [SerializeField] GameObject marker;
        [SerializeField] TMP_Text provisionalLabel;
        [Tooltip("True until the position, rotation and tolerance are confirmed.")]
        [SerializeField] bool provisional = true;

        [Header("Attach")]
        [Tooltip("Only the pad with this ID may attach here.")]
        [SerializeField] string acceptedPadId;
        [Tooltip("Skin-surface pose the pad's adhesive face snaps to. Forward points into the body.")]
        [SerializeField] Transform attachTarget;
        [Tooltip("Max distance in meters between the pad's adhesive-face center and the attach target.")]
        [SerializeField, Min(0f)] float positionToleranceMeters = 0.06f;
        [Tooltip("Max angle in degrees between the adhesive-face direction and the into-body direction.")]
        [SerializeField, Range(0f, 180f)] float facingToleranceDegrees = 45f;
        [Tooltip("Max in-plane rotation in degrees from the target's up axis. 180 disables this check.")]
        [SerializeField, Range(0f, 180f)] float twistToleranceDegrees = 180f;

        [Header("Attached display")]
        [Tooltip("Conforming pad on the patient model, shown only while a pad is attached here.")]
        [SerializeField] GameObject attachedDisplay;

        [Header("Ready display")]
        [SerializeField] GameObject readyIndicator;
        [SerializeField] TMP_Text readyLabel;

        public bool IsProvisional => provisional;
        public GameObject Marker => marker;
        public string AcceptedPadId => acceptedPadId;
        public Transform AttachTarget => attachTarget;
        public float PositionToleranceMeters => positionToleranceMeters;
        public AedPad AttachedPad { get; private set; }
        public GameObject AttachedDisplay => attachedDisplay;

        // Display follows occupancy, so re-enabling never hides an attached pad or shows a free anchor.
        void OnEnable() => ApplyAttachedDisplay();

        public void ShowMarker(bool visible)
        {
            if (provisionalLabel != null)
            {
                provisionalLabel.text = provisional ? TrainingSubtitles.ProvisionalAnchorLabel : string.Empty;
                provisionalLabel.gameObject.SetActive(provisional);
            }

            if (!visible)
                ShowReady(false);
            if (marker != null)
                marker.SetActive(visible);
        }

        public void ShowReady(bool ready)
        {
            if (readyLabel != null)
                readyLabel.text = ready ? TrainingSubtitles.AttachReadyLabel : string.Empty;
            if (readyIndicator != null)
                readyIndicator.SetActive(ready);
        }

        public float DistanceTo(AedPad pad) => Vector3.Distance(pad.AttachPoint.position, attachTarget.position);

        public PadAttachCheck Evaluate(AedPad pad)
        {
            if (pad == null || pad.PadId != acceptedPadId || pad.AttachPoint == null)
                return PadAttachCheck.WrongPad;
            if (AttachedPad != null && AttachedPad != pad)
                return PadAttachCheck.Occupied;
            if (DistanceTo(pad) > positionToleranceMeters)
                return PadAttachCheck.TooFar;

            var face = pad.AttachPoint;
            if (Vector3.Angle(face.forward, attachTarget.forward) > facingToleranceDegrees)
                return PadAttachCheck.WrongFacing;
            if (twistToleranceDegrees < 180f &&
                Vector3.Angle(Vector3.ProjectOnPlane(face.up, attachTarget.forward), attachTarget.up) > twistToleranceDegrees)
                return PadAttachCheck.WrongTwist;
            return PadAttachCheck.Valid;
        }

        // Called by AedPad once its snap and fixing succeeded.
        public void MarkAttached(AedPad pad)
        {
            if (AttachedPad == null)
                AttachedPad = pad;
            ApplyAttachedDisplay();
        }

        // Frees the anchor for a new run: no occupant, patient pad hidden, guide and ready display off.
        public void ResetForNewRun()
        {
            AttachedPad = null;
            ShowMarker(false);
            ApplyAttachedDisplay();
        }

        public void ApplyAttachedDisplay()
        {
            if (attachedDisplay != null)
                attachedDisplay.SetActive(AttachedPad != null);
        }

        public bool TryValidate(out string error)
        {
            var missing = new List<string>();
            if (marker == null)
                missing.Add(nameof(marker));
            if (provisional && provisionalLabel == null)
                missing.Add(nameof(provisionalLabel));
            if (string.IsNullOrWhiteSpace(acceptedPadId))
                missing.Add(nameof(acceptedPadId));
            if (attachTarget == null)
                missing.Add(nameof(attachTarget));
            if (readyIndicator == null)
                missing.Add(nameof(readyIndicator));
            if (readyLabel == null)
                missing.Add(nameof(readyLabel));
            if (attachedDisplay == null)
                missing.Add(nameof(attachedDisplay));

            error = missing.Count > 0 ? $"PadAttachAnchor '{name}' missing references: {string.Join(", ", missing)}." : null;
            return error == null;
        }
    }
}
