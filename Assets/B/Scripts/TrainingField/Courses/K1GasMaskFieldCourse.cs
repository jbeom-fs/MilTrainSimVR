using MilTrainSimVR.B.K1GasMask.Tutorial;
using MilTrainSimVR.B.Training;
using UnityEngine;

namespace MilTrainSimVR.B.TrainingField
{
    // K1 gas mask course for a training field: state from K1TrainingGuide, stop through K1TrainingGuide.TryAbort (the retry's
    // reset: mask off the face, carrier closed), hold through the runner (K1TrainingLink refuses new selections while held) and
    // the carrier, extraction and wearing (held points stay held and hold still; no face snap, strapping or face release).
    [DisallowMultipleComponent]
    public sealed class K1GasMaskFieldCourse : RunnerFieldCourse
    {
        [SerializeField] K1TrainingGuide trainingGuide;

        public override TrainingFieldCourseId Course => TrainingFieldCourseId.K1GasMask;
        public K1TrainingGuide TrainingGuide => trainingGuide;
        protected override TrainingRunner Runner => trainingGuide != null ? trainingGuide.Runner : null;

        public override TrainingFieldCourseState State
        {
            get
            {
                if (trainingGuide == null)
                    return TrainingFieldCourseState.Faulted;
                return trainingGuide.State switch
                {
                    K1TrainingGuideState.WaitingForStart => TrainingFieldCourseState.Waiting,
                    K1TrainingGuideState.Running => TrainingFieldCourseState.Running,
                    K1TrainingGuideState.Completed => TrainingFieldCourseState.Completed,
                    K1TrainingGuideState.NotInitialized => TrainingFieldCourseState.Busy,
                    _ => TrainingFieldCourseState.Faulted
                };
            }
        }

        protected override void SetSuspended(bool suspended)
        {
            var link = trainingGuide != null ? trainingGuide.Link : null;
            if (link == null)
                return;
            if (link.Carrier != null)
                link.Carrier.Suspended = suspended;
            if (link.Extraction != null)
                link.Extraction.Suspended = suspended;
            if (link.Wear != null)
                link.Wear.Suspended = suspended;
        }

        public override bool TryReturnToStart() => trainingGuide.TryAbort();

        public override bool TryValidate(out string error)
        {
            error = trainingGuide == null ? $"K1GasMaskFieldCourse '{name}': training guide missing."
                : trainingGuide.Runner == null || trainingGuide.Link == null ? $"K1GasMaskFieldCourse '{name}': the guide has no runner or link."
                : null;
            return error == null;
        }
    }
}
