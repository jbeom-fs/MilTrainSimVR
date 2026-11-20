using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace MilTrainSimVR.B.BleedingControl.Tourniquet
{
    // Placement preview: a ghost of the tourniquet at the candidate pose on the limb, coloured placeable or blocked, plus a short
    // label. Shown only while the device is held, tracked and has a candidate; hidden otherwise. Reads TourniquetDevice and never
    // changes it. The ghost has renderers only (no collider, rigidbody or interactable), so it cannot be grabbed, hit or collide.
    [DefaultExecutionOrder(110)]
    [DisallowMultipleComponent]
    public sealed class TourniquetPlacementPreview : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] TourniquetDevice device;
        [Tooltip("Collider-free ghost root; its children mirror the tourniquet visuals relative to the device root.")]
        [SerializeField] Transform ghostRoot;
        [SerializeField] Renderer[] ghostRenderers = new Renderer[0];
        [Tooltip("World-space label root (canvas), placed above the candidate and turned to the view camera.")]
        [SerializeField] Transform label;
        [SerializeField] TMP_Text labelText;
        [SerializeField] Transform viewCamera;
        [SerializeField] Color placeableColor = new Color(0.2f, 0.9f, 0.45f, 0.45f);
        [SerializeField] Color blockedColor = new Color(1f, 0.3f, 0.25f, 0.45f);
        [Tooltip("Label height in metres above the candidate position.")]
        [SerializeField, Min(0f)] float labelHeight = 0.13f;

        MaterialPropertyBlock block;
        bool? shownPlaceable;
        TourniquetPlacementCheck shownCheck;

        void OnEnable() => Hide();

        void OnDisable() => Hide();

        void LateUpdate()
        {
            if (device == null || !device.IsHeld || !device.HasCandidate)
            {
                Hide();
                return;
            }

            var pose = device.CandidateRootPose;
            ghostRoot.SetPositionAndRotation(pose.position, pose.rotation);
            var placeable = device.State == TourniquetState.HeldPlaceable;
            if (shownPlaceable != placeable || shownCheck != device.CurrentCheck)
                Apply(placeable, device.CurrentCheck);
            SetVisible(true);

            label.position = pose.position + Vector3.up * labelHeight;
            if (viewCamera != null)
            {
                var facing = label.position - viewCamera.position;
                if (facing.sqrMagnitude > 1e-6f)
                    label.rotation = Quaternion.LookRotation(facing, Vector3.up);
            }
        }

        void Apply(bool placeable, TourniquetPlacementCheck check)
        {
            shownPlaceable = placeable;
            shownCheck = check;
            var color = placeable ? placeableColor : blockedColor;
            block ??= new MaterialPropertyBlock();
            foreach (var r in ghostRenderers)
            {
                if (r == null)
                    continue;
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, color);
                r.SetPropertyBlock(block);
            }

            labelText.text = placeable ? TrainingSubtitles.PreviewPlaceable : TrainingSubtitles.PreviewBlocked + "\n" + TrainingSubtitles.Reason(check);
            labelText.color = new Color(color.r, color.g, color.b, 1f);
        }

        void Hide()
        {
            SetVisible(false);
            shownPlaceable = null;
        }

        void SetVisible(bool visible)
        {
            if (ghostRoot != null && ghostRoot.gameObject.activeSelf != visible)
                ghostRoot.gameObject.SetActive(visible);
            if (label != null && label.gameObject.activeSelf != visible)
                label.gameObject.SetActive(visible);
        }

        public bool TryValidate(out string error)
        {
            var missing = new List<string>();
            if (device == null)
                missing.Add(nameof(device));
            if (ghostRoot == null)
                missing.Add(nameof(ghostRoot));
            if (ghostRenderers == null || ghostRenderers.Length == 0 || System.Array.Exists(ghostRenderers, r => r == null))
                missing.Add(nameof(ghostRenderers));
            if (label == null)
                missing.Add(nameof(label));
            if (labelText == null)
                missing.Add(nameof(labelText));
            if (viewCamera == null)
                missing.Add(nameof(viewCamera));

            error = missing.Count > 0 ? $"TourniquetPlacementPreview '{name}' missing references: {string.Join(", ", missing)}." : null;
            if (error == null && (ghostRoot.GetComponentInChildren<Collider>(true) != null || label.GetComponentInChildren<Collider>(true) != null))
                error = $"TourniquetPlacementPreview '{name}': the ghost and label must have no collider.";
            return error == null;
        }
    }
}
