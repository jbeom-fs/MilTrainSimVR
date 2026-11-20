using MilTrainSimVR.B.ProtectiveClothing.Tutorial;
using MilTrainSimVR.B.Training;
using UnityEngine;

namespace MilTrainSimVR.B.TrainingField
{
    // Protective clothing course for a training field: state from its start gate and link, stop through
    // ProtectiveClothingTrainingReset.TryAbort (worn parts come off through the equipment reset), hold through the runner (its
    // start gate refuses new grabs and wearing while held: a held part stays in hand, is disarmed and has to leave its zone again
    // before it can be worn, so nothing is worn at the moment the dialog closes).
    [DisallowMultipleComponent]
    public sealed class ProtectiveClothingFieldCourse : RunnerFieldCourse
    {
        [SerializeField] ProtectiveClothingTrainingStart trainingStart;
        [SerializeField] ProtectiveClothingTrainingReset trainingReset;

        public override TrainingFieldCourseId Course => TrainingFieldCourseId.ProtectiveClothing;
        public ProtectiveClothingTrainingStart TrainingStart => trainingStart;
        public ProtectiveClothingTrainingReset TrainingReset => trainingReset;
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
                    case ProtectiveClothingStartState.WaitingForStart:
                        return TrainingFieldCourseState.Waiting;
                    case ProtectiveClothingStartState.Resetting:
                        return TrainingFieldCourseState.Busy;
                    case ProtectiveClothingStartState.Faulted:
                        return TrainingFieldCourseState.Faulted;
                }
                return trainingStart.Link.Summary != null ? TrainingFieldCourseState.Completed : TrainingFieldCourseState.Running;
            }
        }

        public override bool TryReturnToStart() => trainingReset.TryAbort();

        public override bool TryValidate(out string error)
        {
            error = trainingStart == null || trainingReset == null ? $"ProtectiveClothingFieldCourse '{name}': start gate or reset missing."
                : trainingReset.TrainingStart != trainingStart ? $"ProtectiveClothingFieldCourse '{name}': the reset does not use this start gate."
                : null;
            return error == null;
        }
    }
}
