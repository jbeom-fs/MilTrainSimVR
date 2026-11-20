using MilTrainSimVR.B.BleedingControl.Tourniquet.Tutorial;
using MilTrainSimVR.B.Training;
using UnityEngine;

namespace MilTrainSimVR.B.TrainingField
{
    // Tourniquet course for a training field: state from its start gate and link, stop through TourniquetTrainingReset.TryAbort,
    // hold through the runner (its start gate refuses new selections while held) and the device and part handling (a held body
    // or part stays held, a part does not move, a release meanwhile never attaches or completes).
    [DisallowMultipleComponent]
    public sealed class TourniquetFieldCourse : RunnerFieldCourse
    {
        [SerializeField] TourniquetTrainingStart trainingStart;
        [SerializeField] TourniquetTrainingReset trainingReset;

        public override TrainingFieldCourseId Course => TrainingFieldCourseId.Tourniquet;
        public TourniquetTrainingStart TrainingStart => trainingStart;
        public TourniquetTrainingReset TrainingReset => trainingReset;
        protected override TrainingRunner Runner => trainingStart != null && trainingStart.Link != null ? trainingStart.Link.Runner : null;

        public override TrainingFieldCourseState State
        {
            get
            {
                if (trainingStart == null || trainingReset == null || trainingStart.Link == null)
                    return TrainingFieldCourseState.Faulted;
                if (trainingReset.IsResetting)
                    return TrainingFieldCourseState.Busy;
                switch (trainingStart.State)
                {
                    case TourniquetTrainingStartState.WaitingForStart:
                        return TrainingFieldCourseState.Waiting;
                    case TourniquetTrainingStartState.Resetting:
                        return TrainingFieldCourseState.Busy;
                    case TourniquetTrainingStartState.Faulted:
                        return TrainingFieldCourseState.Faulted;
                }
                return trainingStart.Link.Summary != null ? TrainingFieldCourseState.Completed : TrainingFieldCourseState.Running;
            }
        }

        protected override void SetSuspended(bool suspended)
        {
            var link = trainingStart != null ? trainingStart.Link : null;
            if (link == null)
                return;
            if (link.Device != null)
                link.Device.Suspended = suspended;
            if (link.Operation != null)
                link.Operation.Suspended = suspended;
        }

        public override bool TryReturnToStart() => trainingReset.TryAbort();

        public override bool TryValidate(out string error)
        {
            error = trainingStart == null || trainingReset == null ? $"TourniquetFieldCourse '{name}': start gate or reset missing."
                : trainingReset.TrainingStart != trainingStart ? $"TourniquetFieldCourse '{name}': the reset does not use this start gate."
                : null;
            return error == null;
        }
    }
}
