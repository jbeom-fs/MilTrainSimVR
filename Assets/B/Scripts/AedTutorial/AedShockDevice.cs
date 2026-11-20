using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MilTrainSimVR.B.AedTutorial
{
    public enum AedShockState
    {
        Idle = 0,
        Charging = 1,
        Charged = 2,
        Discharged = 3,
        Interrupted = 4
    }

    // Shock side of the training AED: charge timer, shock button input, blink and status label. No energy or medical effect is modeled.
    // A press counts only if it started while input was enabled, so presses held through charging or contact never shock.
    // ShockPressed reports such a press; progression re-checks the scene and calls TryDischarge to commit exactly one shock.
    // Trigger callbacks are subscribed in code only; keep the Button OnClick and EventTrigger lists empty in the Inspector.
    public sealed class AedShockDevice : MonoBehaviour
    {
        const float BlinkIntervalSeconds = 0.4f;

        [SerializeField] Button shockButton;
        [Tooltip("EventTrigger on the shock button object; tells which pointer started a press.")]
        [SerializeField] EventTrigger pressTrigger;
        [Tooltip("Blinking ring; toggled by Graphic.enabled so disabling the device never changes object activation.")]
        [SerializeField] Graphic highlight;
        [SerializeField] TMP_Text statusLabel;
        [Tooltip("Training value, not a device or medical standard: seconds from charge start to charged.")]
        [SerializeField] float chargeSeconds = 3f;
        readonly HashSet<int> armedPointers = new HashSet<int>();
        EventTrigger subscribedTrigger;
        EventTrigger.Entry downEntry;
        EventTrigger.Entry clickEntry;
        bool inputEnabled;
        float blinkStartedAt;

        public AedShockState State { get; private set; } = AedShockState.Idle;
        public float ChargeSeconds => chargeSeconds;
        public float ChargeElapsedSeconds { get; private set; }
        public bool InputEnabled => inputEnabled;
        // Set by the tutorial controller while a training field holds the course (its stop confirm): a charge under way keeps
        // its progress and does not advance. Not saved; false by default.
        public bool Paused { get; set; }
        public Button ShockButton => shockButton;
        public Graphic Highlight => highlight;
        public event Action ShockPressed;

        void Awake() => ApplyVisuals();

        void OnEnable()
        {
            if (subscribedTrigger != null || pressTrigger == null)
                return;

            subscribedTrigger = pressTrigger;
            downEntry = NewEntry(EventTriggerType.PointerDown, OnPointerDown);
            clickEntry = NewEntry(EventTriggerType.PointerClick, OnPointerClick);
            subscribedTrigger.triggers.Add(downEntry);
            subscribedTrigger.triggers.Add(clickEntry);
        }

        void OnDisable()
        {
            // A charge or armed press never resumes after a disable.
            Interrupt();
            if (subscribedTrigger != null)
            {
                subscribedTrigger.triggers.Remove(downEntry);
                subscribedTrigger.triggers.Remove(clickEntry);
            }

            subscribedTrigger = null;
        }

        void Update()
        {
            if (State == AedShockState.Charging)
            {
                if (Paused)
                    return;
                ChargeElapsedSeconds += Time.deltaTime;
                if (ChargeElapsedSeconds < chargeSeconds)
                    return;

                State = AedShockState.Charged;
                ApplyVisuals();
            }
            else if (State == AedShockState.Charged && inputEnabled)
            {
                highlight.enabled = Mathf.Repeat(Time.time - blinkStartedAt, BlinkIntervalSeconds * 2f) < BlinkIntervalSeconds;
            }
        }

        public bool BeginCharge()
        {
            if (State != AedShockState.Idle || !isActiveAndEnabled || !TryValidate(out _))
                return false;

            ChargeElapsedSeconds = 0f;
            State = AedShockState.Charging;
            ApplyVisuals();
            return true;
        }

        // Only while charged. Every change forgets presses already started, so a new press is always required.
        public void SetInputEnabled(bool enabled)
        {
            enabled &= State == AedShockState.Charged;
            if (enabled == inputEnabled)
                return;

            inputEnabled = enabled;
            armedPointers.Clear();
            blinkStartedAt = Time.time;
            ApplyVisuals();
        }

        // Commits one shock: input locked and discharge recorded before anything else can react.
        public bool TryDischarge()
        {
            if (State != AedShockState.Charged || !inputEnabled)
                return false;

            inputEnabled = false;
            armedPointers.Clear();
            State = AedShockState.Discharged;
            ApplyVisuals();
            return true;
        }

        // Locks input; an unfinished charge or unused charge is dropped for good.
        public void Interrupt()
        {
            inputEnabled = false;
            armedPointers.Clear();
            if (State == AedShockState.Charging || State == AedShockState.Charged)
                State = AedShockState.Interrupted;
            ApplyVisuals();
        }

        // Back to Idle for a new run: no charge or discharge history, input locked, presses forgotten, blink and label off.
        public void ResetForNewRun()
        {
            inputEnabled = false;
            armedPointers.Clear();
            State = AedShockState.Idle;
            ChargeElapsedSeconds = 0f;
            ApplyVisuals();
        }

        public bool TryValidate(out string error)
        {
            var missing = new List<string>();
            if (shockButton == null)
                missing.Add(nameof(shockButton));
            if (pressTrigger == null)
                missing.Add(nameof(pressTrigger));
            if (highlight == null)
                missing.Add(nameof(highlight));
            if (statusLabel == null)
                missing.Add(nameof(statusLabel));

            error = missing.Count > 0 ? $"AedShockDevice '{name}' missing references: {string.Join(", ", missing)}." : null;
            if (error == null && (float.IsNaN(chargeSeconds) || float.IsInfinity(chargeSeconds) || chargeSeconds <= 0f))
                error = $"AedShockDevice '{name}': {nameof(chargeSeconds)} must be a positive finite value, not {chargeSeconds}.";
            if (error == null && pressTrigger.gameObject != shockButton.gameObject)
                error = $"AedShockDevice '{name}': {nameof(pressTrigger)} must be on the shock button object.";
            if (error == null && shockButton.transform.IsChildOf(highlight.transform))
                error = $"AedShockDevice '{name}': the blinking highlight must not contain the shock button.";
            if (error == null && statusLabel.raycastTarget)
                error = $"AedShockDevice '{name}': status label must not be a raycast target.";
            return error == null;
        }

        void OnPointerDown(BaseEventData data)
        {
            if (!(data is PointerEventData pointer))
                return;

            if (inputEnabled && State == AedShockState.Charged)
                armedPointers.Add(pointer.pointerId);
            else
                armedPointers.Remove(pointer.pointerId);
        }

        void OnPointerClick(BaseEventData data)
        {
            if (data is PointerEventData pointer && armedPointers.Remove(pointer.pointerId) &&
                inputEnabled && State == AedShockState.Charged)
                ShockPressed?.Invoke();
        }

        void ApplyVisuals()
        {
            var ready = State == AedShockState.Charged && inputEnabled;
            if (shockButton != null)
                shockButton.interactable = ready;
            if (highlight != null)
                highlight.enabled = ready;
            if (statusLabel != null)
                statusLabel.text = State switch
                {
                    AedShockState.Charging => TrainingSubtitles.ShockChargingLabel,
                    AedShockState.Charged => TrainingSubtitles.ShockChargedLabel,
                    AedShockState.Discharged => TrainingSubtitles.ShockDeliveredLabel,
                    _ => string.Empty
                };
        }

        static EventTrigger.Entry NewEntry(EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> callback)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(callback);
            return entry;
        }
    }
}
