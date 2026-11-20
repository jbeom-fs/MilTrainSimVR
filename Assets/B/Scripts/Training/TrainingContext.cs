using System;

namespace MilTrainSimVR.B.Training
{
    public enum TrainingStatus
    {
        Idle = 0,
        Running = 1,
        Completed = 2,
        Stopped = 3
    }

    public readonly struct TrainingStepToken
    {
        public readonly int RunId;
        public readonly int StepEntry;

        public TrainingStepToken(int runId, int stepEntry)
        {
            RunId = runId;
            StepEntry = stepEntry;
        }
    }

    public delegate bool TrainingStateReader(string targetId, string signalId, out bool value);

    // Plain runtime state: no scene mutation, XR input, subtitles, or ScriptableObject writes.
    public sealed class TrainingContext
    {
        readonly TrainingStep[] steps;
        readonly TrainingStateReader readState;
        bool[] actions;
        float[] heldSeconds;
        int enteredFrame;
        int lastTickFrame;

        public TrainingStatus Status { get; private set; } = TrainingStatus.Idle;
        public int StepIndex { get; private set; } = -1;
        public TrainingStepToken Token { get; private set; }
        public string CurrentStepId => StepIndex >= 0 && StepIndex < steps.Length ? steps[StepIndex].StepId : null;

        public TrainingContext(TrainingStep[] steps, TrainingStateReader readState)
        {
            if (!TrainingDefinition.TryValidateSteps(steps, out var error))
                throw new ArgumentException(error, nameof(steps));

            this.steps = steps;
            this.readState = readState ?? throw new ArgumentNullException(nameof(readState));
        }

        public void Begin(int runId, int frame)
        {
            Status = TrainingStatus.Running;
            StepIndex = 0;
            Token = new TrainingStepToken(runId, 0);
            lastTickFrame = frame;
            EnterStep(frame);
        }

        public void Stop()
        {
            Status = TrainingStatus.Stopped;
        }

        public bool ReportAction(string targetId, string signalId, TrainingStepToken token, int frame)
        {
            if (Status != TrainingStatus.Running || token.RunId != Token.RunId ||
                token.StepEntry != Token.StepEntry || frame <= enteredFrame)
                return false;

            var matched = false;
            var conditions = steps[StepIndex].Conditions;
            for (var index = 0; index < conditions.Length; index++)
            {
                var condition = conditions[index];
                if (condition.Kind == TrainingConditionKind.Action &&
                    condition.TargetId == targetId && condition.SignalId == signalId)
                {
                    actions[index] = true;
                    matched = true;
                }
            }

            return matched;
        }

        // Evaluate once per frame after interaction callbacks; never advance recursively inside an event.
        public bool Tick(float deltaTime, int frame)
        {
            if (Status != TrainingStatus.Running || frame <= enteredFrame || frame <= lastTickFrame)
                return false;

            if (deltaTime < 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
                throw new ArgumentOutOfRangeException(nameof(deltaTime));

            lastTickFrame = frame;
            var complete = true;
            var conditions = steps[StepIndex].Conditions;
            for (var index = 0; index < conditions.Length; index++)
            {
                var condition = conditions[index];
                if (condition.Kind == TrainingConditionKind.Action)
                {
                    complete &= actions[index];
                    continue;
                }

                var valid = readState(condition.TargetId, condition.SignalId, out var value) &&
                    value == condition.ExpectedValue;
                if (condition.Kind == TrainingConditionKind.HoldState)
                {
                    heldSeconds[index] = valid ? heldSeconds[index] + deltaTime : 0f;
                    valid &= heldSeconds[index] >= condition.HoldSeconds;
                }

                complete &= valid;
            }

            if (!complete)
                return false;

            StepIndex++;
            if (StepIndex == steps.Length)
                Status = TrainingStatus.Completed;
            else
                EnterStep(frame);

            return true;
        }

        void EnterStep(int frame)
        {
            Token = new TrainingStepToken(Token.RunId, Token.StepEntry + 1);
            enteredFrame = frame;
            actions = new bool[steps[StepIndex].Conditions.Length];
            heldSeconds = new float[actions.Length];
        }
    }
}
