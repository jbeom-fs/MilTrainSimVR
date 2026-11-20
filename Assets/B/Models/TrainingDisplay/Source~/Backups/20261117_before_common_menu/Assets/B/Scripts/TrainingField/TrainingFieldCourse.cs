using MilTrainSimVR.B.Training;
using UnityEngine;

namespace MilTrainSimVR.B.TrainingField
{
    // Link from a training field station to one existing course. Reads the course's own state and calls only the course's own
    // entry points (its stop/retry reset, its hold); it never starts a run, moves equipment or judges a step. Each course keeps its
    // start screen, steps, checks, completion summary and retry. One small subclass per course, because the six courses have
    // different start gates and reset paths.
    public abstract class TrainingFieldCourse : MonoBehaviour
    {
        public abstract TrainingFieldCourseId Course { get; }
        public abstract TrainingFieldCourseState State { get; }

        // Stop confirm open (true) or closed (false). While held the course takes no new input, judges nothing and changes no
        // equipment state, and every XR selection stays as it is: a part in hand stays in hand (it holds still or only follows the
        // hand, as each equipment does for a lost hand), a release meanwhile is never a judged release. Closing it gives every
        // part a new hand baseline, so nothing jumps; a continuous contact or hands-off time starts again from zero.
        public abstract void SetHeld(bool held);

        // Back to the course's start screen through its own reset (stop confirmed, or the field's end after completion).
        public abstract bool TryReturnToStart();

        public abstract bool TryValidate(out string error);
    }

    // A course on the common TrainingRunner: held through TrainingRunner.Held (step judging off; the course's own input gate
    // also reads it and refuses new selections) and the hold of its equipment (SetSuspended).
    public abstract class RunnerFieldCourse : TrainingFieldCourse
    {
        protected abstract TrainingRunner Runner { get; }

        public override void SetHeld(bool held)
        {
            var runner = Runner;
            var on = held && runner != null && runner.Status == TrainingStatus.Running;
            if (runner != null)
                runner.Held = on;
            SetSuspended(on);
        }

        // The equipment's own hold flags (each equipment's Suspended); nothing for a course whose input gate is enough.
        protected virtual void SetSuspended(bool suspended)
        {
        }
    }
}
