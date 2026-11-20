using System;
using System.Collections.Generic;
using UnityEngine;

namespace MilTrainSimVR.B.Training
{
    public enum TrainingConditionKind
    {
        Action = 0,
        State = 1,
        HoldState = 2
    }

    [Serializable]
    public sealed class TrainingCondition
    {
        public TrainingConditionKind Kind;
        public string TargetId;
        public string SignalId;
        public bool ExpectedValue = true;
        public float HoldSeconds = 1f;
    }

    [Serializable]
    public sealed class TrainingStep
    {
        public string StepId;
        public TrainingCondition[] Conditions = Array.Empty<TrainingCondition>();
    }

    [CreateAssetMenu(menuName = "B/Training Definition")]
    public sealed class TrainingDefinition : ScriptableObject
    {
        public string TrainingId;
        public TrainingStep[] Steps = Array.Empty<TrainingStep>();

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(TrainingId))
            {
                error = "TrainingId is empty.";
                return false;
            }

            return TryValidateSteps(Steps, out error);
        }

        public static bool TryValidateSteps(TrainingStep[] steps, out string error)
        {
            error = null;
            if (steps == null || steps.Length == 0)
            {
                error = "Training has no steps.";
                return false;
            }

            var stepIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var step in steps)
            {
                if (step == null || string.IsNullOrWhiteSpace(step.StepId) || !stepIds.Add(step.StepId))
                {
                    error = $"Missing or duplicate StepId: '{step?.StepId}'.";
                    return false;
                }

                if (step.Conditions == null || step.Conditions.Length == 0)
                {
                    error = $"Step '{step.StepId}' has no completion conditions.";
                    return false;
                }

                foreach (var condition in step.Conditions)
                {
                    if (condition == null || !Enum.IsDefined(typeof(TrainingConditionKind), condition.Kind) ||
                        string.IsNullOrWhiteSpace(condition.TargetId) || string.IsNullOrWhiteSpace(condition.SignalId))
                    {
                        error = $"Step '{step.StepId}' has an invalid condition: target '{condition?.TargetId}', signal '{condition?.SignalId}'.";
                        return false;
                    }

                    if (condition.Kind == TrainingConditionKind.HoldState &&
                        (condition.HoldSeconds <= 0f || float.IsNaN(condition.HoldSeconds) || float.IsInfinity(condition.HoldSeconds)))
                    {
                        error = $"Step '{step.StepId}', target '{condition.TargetId}': HoldSeconds must be finite and positive.";
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
