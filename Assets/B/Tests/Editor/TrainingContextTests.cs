using System.Collections.Generic;
using MilTrainSimVR.B.Training;
using NUnit.Framework;

namespace MilTrainSimVR.B.Tests
{
    public sealed class TrainingContextTests
    {
        readonly Dictionary<string, bool> states = new Dictionary<string, bool>();

        bool ReadState(string targetId, string signalId, out bool value)
        {
            return states.TryGetValue(targetId + "/" + signalId, out value);
        }

        TrainingContext Create(params TrainingStep[] steps)
        {
            states.Clear();
            var context = new TrainingContext(steps, ReadState);
            context.Begin(1, 0);
            return context;
        }

        static TrainingStep Step(string id, params TrainingCondition[] conditions)
        {
            return new TrainingStep { StepId = id, Conditions = conditions };
        }

        static TrainingCondition Condition(TrainingConditionKind kind, string signal = "grab", string target = "cube")
        {
            return new TrainingCondition { Kind = kind, TargetId = target, SignalId = signal, HoldSeconds = 1f };
        }

        [Test]
        public void RepeatedActionAdvancesOnlyOnceAndCannotLeakIntoNextStepInSameFrame()
        {
            var context = Create(Step("one", Condition(TrainingConditionKind.Action)),
                Step("two", Condition(TrainingConditionKind.Action)));
            var token = context.Token;
            Assert.That(context.ReportAction("cube", "grab", token, 1), Is.True);
            context.ReportAction("cube", "grab", token, 1);
            Assert.That(context.StepIndex, Is.EqualTo(0));
            Assert.That(context.Tick(0.1f, 1), Is.True);
            Assert.That(context.StepIndex, Is.EqualTo(1));
            Assert.That(context.ReportAction("cube", "grab", context.Token, 1), Is.False);
            Assert.That(context.Tick(0.1f, 1), Is.False);
            Assert.That(context.Tick(0.1f, 2), Is.False);
        }

        [Test]
        public void WrongTargetAndWrongSignalCannotCompleteStep()
        {
            var context = Create(Step("one", Condition(TrainingConditionKind.Action)));
            Assert.That(context.ReportAction("other", "grab", context.Token, 1), Is.False);
            Assert.That(context.ReportAction("cube", "release", context.Token, 1), Is.False);
            Assert.That(context.Tick(1f, 1), Is.False);
        }

        [Test]
        public void DetachedPadInvalidatesCurrentStateBeforeEvaluation()
        {
            var context = Create(Step("pads", Condition(TrainingConditionKind.State, "attached", "left"),
                Condition(TrainingConditionKind.State, "attached", "right")));
            states["left/attached"] = true;
            states["right/attached"] = true;
            states["left/attached"] = false;
            Assert.That(context.Tick(0.1f, 1), Is.False);
            states["left/attached"] = true;
            Assert.That(context.Tick(0.1f, 2), Is.True);
            Assert.That(context.Status, Is.EqualTo(TrainingStatus.Completed));
        }

        [Test]
        public void MissingOrDisabledStateDoesNotSatisfyExpectedFalse()
        {
            var condition = Condition(TrainingConditionKind.State, "attached");
            condition.ExpectedValue = false;
            var context = Create(Step("one", condition));
            Assert.That(context.Tick(0.1f, 1), Is.False);
            states["cube/attached"] = false;
            Assert.That(context.Tick(0.1f, 2), Is.True);
        }

        [Test]
        public void HoldResetsOnReleaseAndDoesNotAccumulateTwicePerFrame()
        {
            var context = Create(Step("hold", Condition(TrainingConditionKind.HoldState, "held")));
            states["cube/held"] = true;
            Assert.That(context.Tick(0.6f, 1), Is.False);
            Assert.That(context.Tick(0.6f, 1), Is.False);
            states["cube/held"] = false;
            Assert.That(context.Tick(0.1f, 2), Is.False);
            states["cube/held"] = true;
            Assert.That(context.Tick(0.6f, 3), Is.False);
            Assert.That(context.Tick(0.5f, 4), Is.True);
        }

        [Test]
        public void AllConditionsAreRequiredAndActionIsLatchedOnlyInCurrentStep()
        {
            var context = Create(Step("one", Condition(TrainingConditionKind.Action),
                Condition(TrainingConditionKind.State, "ready")));
            context.ReportAction("cube", "grab", context.Token, 1);
            Assert.That(context.Tick(0.1f, 1), Is.False);
            states["cube/ready"] = true;
            Assert.That(context.Tick(0.1f, 2), Is.True);
        }

        [Test]
        public void EarlierStepTokenIsRejected()
        {
            var context = Create(Step("one", Condition(TrainingConditionKind.Action)),
                Step("two", Condition(TrainingConditionKind.Action)));
            var oldToken = context.Token;
            context.ReportAction("cube", "grab", oldToken, 1);
            context.Tick(0.1f, 1);
            Assert.That(context.ReportAction("cube", "grab", oldToken, 2), Is.False);
            Assert.That(context.Tick(0.1f, 2), Is.False);
        }

        [Test]
        public void StopAndNewRunRejectEarlierCallbacks()
        {
            var context = Create(Step("one", Condition(TrainingConditionKind.Action)));
            var oldToken = context.Token;
            context.Stop();
            Assert.That(context.ReportAction("cube", "grab", oldToken, 1), Is.False);
            Assert.That(context.Tick(1f, 1), Is.False);
            context.Begin(2, 2);
            Assert.That(context.ReportAction("cube", "grab", oldToken, 3), Is.False);
            Assert.That(context.Tick(1f, 3), Is.False);
            context.ReportAction("cube", "grab", context.Token, 4);
            Assert.That(context.Tick(0.1f, 4), Is.True);
            Assert.That(context.Tick(0.1f, 5), Is.False);
        }

        [Test]
        public void StateOnlyStepsAdvanceAtMostOncePerFrame()
        {
            var context = Create(Step("one", Condition(TrainingConditionKind.State, "ready")),
                Step("two", Condition(TrainingConditionKind.State, "ready")));
            states["cube/ready"] = true;
            Assert.That(context.Tick(0.1f, 0), Is.False);
            Assert.That(context.Tick(0.1f, 1), Is.True);
            Assert.That(context.StepIndex, Is.EqualTo(1));
            Assert.That(context.Tick(0.1f, 1), Is.False);
            Assert.That(context.Tick(0.1f, 2), Is.True);
        }

        [Test]
        public void DuplicateAndEmptyStepIdsAreRejected()
        {
            Assert.That(TrainingDefinition.TryValidateSteps(new[] {
                Step("same", Condition(TrainingConditionKind.Action)),
                Step("same", Condition(TrainingConditionKind.Action)) }, out var error), Is.False);
            StringAssert.Contains("same", error);
            Assert.That(TrainingDefinition.TryValidateSteps(new[] {
                Step("", Condition(TrainingConditionKind.Action)) }, out _), Is.False);
        }

        [Test]
        public void MissingConditionsAndInvalidSignalsAreRejected()
        {
            Assert.That(TrainingDefinition.TryValidateSteps(new[] { Step("empty") }, out _), Is.False);
            Assert.That(TrainingDefinition.TryValidateSteps(new[] {
                Step("bad", Condition(TrainingConditionKind.Action, "")) }, out _), Is.False);
            Assert.That(TrainingDefinition.TryValidateSteps(new[] {
                Step("bad", Condition((TrainingConditionKind)99)) }, out _), Is.False);
        }

        [Test]
        public void NonPositiveAndNonFiniteHoldTimesAreRejected()
        {
            foreach (var seconds in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            {
                var condition = Condition(TrainingConditionKind.HoldState);
                condition.HoldSeconds = seconds;
                Assert.That(TrainingDefinition.TryValidateSteps(new[] { Step("hold", condition) }, out _), Is.False);
            }
        }
    }
}
