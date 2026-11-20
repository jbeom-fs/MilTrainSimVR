using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MilTrainSimVR.B.AedTutorial
{
    // AED power state and its indicator only; tutorial progression listens to PowerChanged.
    // The power button is subscribed in code only; keep its Inspector OnClick list empty.
    // Only turning on is supported here; power off/toggle is a separate requirement.
    public sealed class AedPowerDevice : MonoBehaviour
    {
        [SerializeField] Button powerButton;
        [SerializeField] Graphic powerLamp;
        [SerializeField] TMP_Text powerLabel;
        [SerializeField] Color offColor = new Color(0.32f, 0.34f, 0.37f, 1f);
        [SerializeField] Color onColor = new Color(0.25f, 0.95f, 0.35f, 1f);
        Button subscribedButton;
        bool inputEnabled;

        public bool IsOn { get; private set; }
        public Button PowerButton => powerButton;
        public event Action<bool> PowerChanged;

        void Awake() => ApplyVisuals();

        void OnEnable()
        {
            if (subscribedButton != null || powerButton == null)
                return;

            subscribedButton = powerButton;
            subscribedButton.onClick.AddListener(OnPowerButtonPressed);
        }

        void OnDisable()
        {
            if (subscribedButton != null)
                subscribedButton.onClick.RemoveListener(OnPowerButtonPressed);
            subscribedButton = null;
        }

        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
            ApplyVisuals();
        }

        // Power off with input locked for a new run. Silent: no PowerChanged, so no progression reacts to the reset.
        public void ResetForNewRun()
        {
            IsOn = false;
            inputEnabled = false;
            ApplyVisuals();
        }

        public bool TryValidate(out string error)
        {
            var missing = new List<string>();
            if (powerButton == null)
                missing.Add(nameof(powerButton));
            if (powerLamp == null)
                missing.Add(nameof(powerLamp));
            if (powerLabel == null)
                missing.Add(nameof(powerLabel));

            error = missing.Count > 0 ? $"AedPowerDevice '{name}' missing references: {string.Join(", ", missing)}." : null;
            return error == null;
        }

        void OnPowerButtonPressed()
        {
            if (!inputEnabled || IsOn || !TryValidate(out _))
                return;

            IsOn = true;
            ApplyVisuals();
            PowerChanged?.Invoke(true);
        }

        void ApplyVisuals()
        {
            if (powerButton != null)
                powerButton.interactable = inputEnabled && !IsOn;
            if (powerLamp != null)
                powerLamp.color = IsOn ? onColor : offColor;
            if (powerLabel != null)
                powerLabel.text = IsOn ? TrainingSubtitles.PowerOnLabel : TrainingSubtitles.PowerOffLabel;
        }
    }
}
