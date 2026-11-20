using MilTrainSimVR.B.BleedingControl.PressureBandage.Tutorial;
using MilTrainSimVR.B.Training;
using UnityEngine;

namespace MilTrainSimVR.B.TrainingField
{
    // Direct pressure and pressure bandage course for a training field: state from its start gate and link, stop through
    // PressureBandageTrainingReset.TryAbort, hold through the runner (its start gate refuses new selections) and the equipment:
    // the dressing's InputSuspended (the two-hand contact ends, so its continuous time starts again after the dialog; a release
    // is not seated) and the application's Suspended (no wrap time, end or tape attach, placement watch or cancel).
    [DisallowMultipleComponent]
    public sealed class PressureBandageFieldCourse : RunnerFieldCourse
    {
        [SerializeField] PressureBandageTrainingStart trainingStart;
        [SerializeField] PressureBandageTrainingReset trainingReset;

        public override TrainingFieldCourseId Course => TrainingFieldCourseId.PressureBandage;
        public PressureBandageTrainingStart TrainingStart => trainingStart;
        public PressureBandageTrainingReset TrainingReset => trainingReset;
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
                    case PressureTrainingStartState.WaitingForStart:
                        return TrainingFieldCourseState.Waiting;
                    case PressureTrainingStartState.Resetting:
                        return TrainingFieldCourseState.Busy;
                    case PressureTrainingStartState.Faulted:
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
            if (link.Dressing != null)
                link.Dressing.InputSuspended = suspended;
            if (link.BandageApplication != null)
                link.BandageApplication.Suspended = suspended;
        }

        public override bool TryReturnToStart() => trainingReset.TryAbort();

        public override bool TryValidate(out string error)
        {
            error = trainingStart == null || trainingReset == null ? $"PressureBandageFieldCourse '{name}': start gate or reset missing."
                : trainingReset.TrainingStart != trainingStart ? $"PressureBandageFieldCourse '{name}': the reset does not use this start gate."
                : null;
            return error == null;
        }
    }
}
