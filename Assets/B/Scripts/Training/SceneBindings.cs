using System;
using System.Collections.Generic;
using UnityEngine;

namespace MilTrainSimVR.B.Training
{
    public sealed class SceneBindings : MonoBehaviour
    {
        [SerializeField] TrainingTarget[] targets = Array.Empty<TrainingTarget>();
        readonly Dictionary<string, TrainingTarget> lookup = new Dictionary<string, TrainingTarget>(StringComparer.Ordinal);

        public IEnumerable<TrainingTarget> Targets => lookup.Values;

        public bool TryInitialize(out string error)
        {
            lookup.Clear();
            error = null;
            if (targets == null)
            {
                error = "SceneBindings target list is null.";
                return false;
            }

            foreach (var target in targets)
            {
                if (target == null)
                {
                    error = "SceneBindings contains a missing target reference.";
                    return false;
                }

                if (!target.TryValidate(out error))
                    return false;

                if (lookup.ContainsKey(target.TargetId))
                {
                    error = $"SceneBindings has duplicate target '{target.TargetId}'.";
                    return false;
                }

                lookup.Add(target.TargetId, target);
            }

            return true;
        }

        public bool ContainsTarget(string targetId) => lookup.ContainsKey(targetId);

        public bool HasState(string targetId, string signalId)
        {
            return lookup.TryGetValue(targetId, out var target) && target != null && target.HasState(signalId);
        }

        public bool TryGetState(string targetId, string signalId, out bool value)
        {
            value = false;
            return isActiveAndEnabled && lookup.TryGetValue(targetId, out var target) && target != null &&
                target.TryGetState(signalId, out value);
        }
    }
}
