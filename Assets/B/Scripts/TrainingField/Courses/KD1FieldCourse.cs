using MilTrainSimVR.B.Decontamination.KD1.Tutorial;
using MilTrainSimVR.B.Training;
using UnityEngine;

namespace MilTrainSimVR.B.TrainingField
{
    // KD-1 course for a training field: state from KD1TrainingCourse, stop through its own restart/retry (TryReset: pads off the
    // hands, every piece back), hold through the runner (the course gate allows no grab, opening, fit or area record while held)
    // and KD1Equipment.Suspended (a held pack, packet or pad stays in hand and holds still; a release meanwhile is applied only
    // after the dialog, so a packet or pad being pulled is never put back in its slot by the dialog itself).
    [DisallowMultipleComponent]
    public sealed class KD1FieldCourse : RunnerFieldCourse
    {
        [SerializeField] KD1TrainingCourse course;

        public override TrainingFieldCourseId Course => TrainingFieldCourseId.KD1;
        public KD1TrainingCourse TrainingCourse => course;
        protected override TrainingRunner Runner => course != null ? course.Runner : null;

        public override TrainingFieldCourseState State
        {
            get
            {
                if (course == null)
                    return TrainingFieldCourseState.Faulted;
                return course.State switch
                {
                    KD1CourseState.WaitingForStart => TrainingFieldCourseState.Waiting,
                    KD1CourseState.Running => TrainingFieldCourseState.Running,
                    KD1CourseState.Completed => TrainingFieldCourseState.Completed,
                    KD1CourseState.Resetting => TrainingFieldCourseState.Busy,
                    _ => TrainingFieldCourseState.Faulted
                };
            }
        }

        protected override void SetSuspended(bool suspended)
        {
            if (course != null && course.Equipment != null)
                course.Equipment.Suspended = suspended;
        }

        public override bool TryReturnToStart() => course.TryReset();

        public override bool TryValidate(out string error)
        {
            error = course == null ? $"KD1FieldCourse '{name}': KD1TrainingCourse missing."
                : course.Runner == null ? $"KD1FieldCourse '{name}': the course has no runner."
                : null;
            return error == null;
        }
    }
}
