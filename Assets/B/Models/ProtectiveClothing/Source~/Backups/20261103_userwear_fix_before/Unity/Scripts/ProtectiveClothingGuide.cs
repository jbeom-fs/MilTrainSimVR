using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MilTrainSimVR.B.ProtectiveClothing
{
    // Guidance of the equipment scene: one short action line, one target marker and the development reset button. Reads
    // ProtectiveClothingEquipment (and through it the user body's zones) only; never wears, hides or moves a part.
    //
    // Line, first that applies: an invalid equipment setup (grabbing blocked); a failed reset; the equipment component switched
    // off (paused: its filter refuses new grabs and wearing is not judged; the wear records are kept and the current state's
    // line returns when it is on again); input focus lost; all six worn; no part held ("grab a part"); the guided part's
    // hand not tracked; a glove held by the hand it is meant for ("take it with the other hand"); the headset not tracked (the
    // body is estimated from it); a glove's target hand not tracked; otherwise "bring <part> to <zone>". With a part in each hand
    // the guided part is the one grabbed last (ProtectiveClothingEquipment.GuidedPiece), and the line and the marker always name
    // that same part.
    //
    // Marker: a thin ring of the zone radius around the guided part's zone on the user body (chest front, waist front, the target
    // hand, the overboot zone in front of the thigh), turned to face the viewer. Shown only with the "bring" line; never all six
    // zones at once, and none while nothing is held.
    //
    // Reset button: asks the equipment to reset (the equipment ignores a second request in the same frame). The button is
    // subscribed in code only; keep its Inspector OnClick list empty.
    [DefaultExecutionOrder(60)]
    [DisallowMultipleComponent]
    public sealed class ProtectiveClothingGuide : MonoBehaviour
    {
        const int RingSegments = 48;

        [SerializeField] ProtectiveClothingEquipment equipment;
        [SerializeField] TMP_FontAsset koreanFont;
        [SerializeField] TMP_Text instructionText;
        [SerializeField] Button resetButton;
        [SerializeField] TMP_Text resetButtonText;
        [Tooltip("Target ring (LineRenderer in local space, loop). Its points are written at runtime from the wear radius.")]
        [SerializeField] LineRenderer marker;
        [Tooltip("The XR camera the ring turns to face.")]
        [SerializeField] Transform viewer;

        readonly Vector3[] ring = new Vector3[RingSegments];
        Button subscribedButton;
        bool valid;
        string shownLine;
        float ringRadius = -1f;

        public ProtectiveClothingEquipment Equipment => equipment;
        public LineRenderer Marker => marker;
        public Button ResetButton => resetButton;
        public IEnumerable<TMP_Text> Texts => new[] { instructionText, resetButtonText };

        void Awake()
        {
            valid = TryValidate(out var error);
            if (!valid)
            {
                Debug.LogError("[ProtectiveClothing] " + error, this);
                if (resetButton != null)
                    resetButton.interactable = false;
                return;
            }
            resetButtonText.font = koreanFont;
            resetButtonText.text = ProtectiveClothingSubtitles.ResetButton;
            marker.gameObject.SetActive(false);
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
            ProtectiveClothingPiece target = null;
            string line;
            if (!equipment.IsValid || !equipment.Body.IsValid)
                line = ProtectiveClothingSubtitles.SetupInvalid;
            else if (equipment.IsFaulted)
                line = ProtectiveClothingSubtitles.ResetFailed;
            else if (!equipment.isActiveAndEnabled)
                line = ProtectiveClothingSubtitles.EquipmentPaused;
            else if (!Application.isFocused)
                line = ProtectiveClothingSubtitles.Paused;
            else if (equipment.AllWorn)
                line = ProtectiveClothingSubtitles.AllWorn;
            else if (equipment.GuidedPiece == null)
                line = ProtectiveClothingSubtitles.GrabAny;
            else if (equipment.HandTracking(equipment.GuidedPiece) != ProtectiveClothingHandTracking.Tracked)
                line = ProtectiveClothingSubtitles.Tracking;
            else if (equipment.IsHeldByWrongHand(equipment.GuidedPiece))
                line = ProtectiveClothingSubtitles.SwapGlove(equipment.GuidedPiece.Part);
            else if (!equipment.Body.HeadTracked)
                line = ProtectiveClothingSubtitles.HeadTracking;
            else if (equipment.ZoneTracking(equipment.GuidedPiece) != ProtectiveClothingHandTracking.Tracked)
                line = ProtectiveClothingSubtitles.Tracking;
            else
            {
                target = equipment.GuidedPiece;
                line = ProtectiveClothingSubtitles.Bring(target.Part);
            }

            if (line != shownLine)
            {
                shownLine = line;
                instructionText.font = koreanFont;
                instructionText.text = line;
            }
            ShowMarker(target);
            // Usable only when a reset can run: a valid, enabled equipment that is not resetting (also to retry a failed reset).
            resetButton.interactable = equipment.IsValid && equipment.isActiveAndEnabled && !equipment.IsResetting;
        }

        void ShowMarker(ProtectiveClothingPiece target)
        {
            if (target == null)
            {
                if (marker.gameObject.activeSelf)
                    marker.gameObject.SetActive(false);
                return;
            }
            var radius = equipment.Body.ZoneRadius(target.Part);
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
            var centre = equipment.Body.ZonePoint(target.Part).position;
            var facing = viewer != null ? centre - viewer.position : Vector3.forward;
            marker.transform.SetPositionAndRotation(centre, facing.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(facing) : Quaternion.identity);
            if (!marker.gameObject.activeSelf)
                marker.gameObject.SetActive(true);
        }

        void OnResetClicked()
        {
            if (!valid || !resetButton.interactable)
                return;
            resetButton.interactable = false;
            equipment.ResetEquipment();
            shownLine = null;
        }

        public bool TryValidate(out string error)
        {
            var missing = new List<string>();
            if (equipment == null)
                missing.Add(nameof(equipment));
            if (koreanFont == null)
                missing.Add(nameof(koreanFont));
            if (instructionText == null || resetButtonText == null)
                missing.Add("a text");
            if (resetButton == null)
                missing.Add(nameof(resetButton));
            if (marker == null)
                missing.Add(nameof(marker));
            error = missing.Count > 0 ? $"ProtectiveClothingGuide '{name}' missing references: {string.Join(", ", missing)}." : null;
            if (error == null && !resetButtonText.transform.IsChildOf(resetButton.transform))
                error = $"ProtectiveClothingGuide '{name}': the reset label must be inside the reset button.";
            return error == null;
        }
    }
}
