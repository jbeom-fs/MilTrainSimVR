using System;
using System.Collections.Generic;
using UnityEngine;

namespace MilTrainSimVR.B.Training
{
    [Serializable]
    public sealed class TrainingStateValue
    {
        public string SignalId;
        public bool InitialValue;
    }

    // XR/gear UnityEvents call this component. It reports facts without advancing training.
    public sealed class TrainingTarget : MonoBehaviour
    {
        [SerializeField] string targetId;
        [SerializeField] TrainingStateValue[] states = Array.Empty<TrainingStateValue>();
        readonly Dictionary<string, bool> values = new Dictionary<string, bool>(StringComparer.Ordinal);
        bool initialized;

        public string TargetId => targetId;
        public event Action<TrainingTarget, string> ActionReported;

        public bool TryValidate(out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(targetId))
            {
                error = $"Target '{name}' has an empty TargetId.";
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (states == null)
            {
                error = $"Target '{targetId}' has a null state list.";
                return false;
            }

            foreach (var state in states)
            {
                if (state == null || string.IsNullOrWhiteSpace(state.SignalId) || !ids.Add(state.SignalId))
                {
                    error = $"Target '{targetId}' has a missing or duplicate state ID: '{state?.SignalId}'.";
                    return false;
                }
            }

            return true;
        }

        public bool HasState(string signalId)
        {
            return Array.Exists(states, state => state != null && state.SignalId == signalId);
        }

        public bool TryGetState(string signalId, out bool value)
        {
            value = false;
            if (!isActiveAndEnabled)
                return false;

            EnsureInitialized();
            return values.TryGetValue(signalId, out value);
        }

        public void ReportAction(string signalId)
        {
            if (isActiveAndEnabled && !string.IsNullOrWhiteSpace(signalId))
                ActionReported?.Invoke(this, signalId);
        }

        public void SetStateTrue(string signalId) => SetState(signalId, true);
        public void SetStateFalse(string signalId) => SetState(signalId, false);

        void SetState(string signalId, bool value)
        {
            if (!isActiveAndEnabled)
                return;

            EnsureInitialized();
            if (!values.ContainsKey(signalId))
            {
                Debug.LogError($"[Training] Target '{targetId}' has no state '{signalId}'.", this);
                return;
            }

            values[signalId] = value;
        }

        void EnsureInitialized()
        {
            if (initialized)
                return;

            if (!TryValidate(out var error))
            {
                Debug.LogError($"[Training] {error}", this);
                return;
            }

            foreach (var state in states)
                values.Add(state.SignalId, state.InitialValue);
            initialized = true;
        }

        void OnDisable()
        {
            values.Clear();
            initialized = false;
        }
    }
}
