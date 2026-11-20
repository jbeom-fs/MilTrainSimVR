using MilTrainSimVR.B.AedTutorial;
using UnityEngine;

namespace MilTrainSimVR.B.TrainingField
{
    // AED course for a training field. The AED tutorial has its own progression (no TrainingRunner): state from
    // AedTutorialStartController, hold through its SetPaused (no phase progresses, power and shock buttons locked, a loose pad
    // cannot be grabbed, a pad in hand stays in hand and a release meanwhile never attaches; a rhythm analysis under way restarts
    // from zero), stop through its TryAbort (the retry's reset).
    [DisallowMultipleComponent]
    public sealed class AedFieldCourse : TrainingFieldCourse
    {
        [SerializeField] AedTutorialStartController controller;

        public override TrainingFieldCourseId Course => TrainingFieldCourseId.Aed;
        public AedTutorialStartController Controller => controller;

        public override TrainingFieldCourseState State
        {
            get
            {
                if (controller == null)
                    return TrainingFieldCourseState.Faulted;
                return controller.State switch
                {
                    AedTutorialStartState.WaitingForStart => TrainingFieldCourseState.Waiting,
                    AedTutorialStartState.NotInitialized => TrainingFieldCourseState.Busy,
                    AedTutorialStartState.Faulted => TrainingFieldCourseState.Faulted,
                    AedTutorialStartState.ShockDeliveredAwaitingNext => TrainingFieldCourseState.Completed,
                    // Includes InterruptedReloadRequired: the field's stop resets it like the retry.
                    _ => TrainingFieldCourseState.Running
                };
            }
        }

        public override void SetHeld(bool held)
        {
            if (controller != null)
                controller.SetPaused(held);
        }

        public override bool TryReturnToStart() => controller.TryAbort();

        public override bool TryValidate(out string error)
        {
            error = controller == null ? $"AedFieldCourse '{name}': AedTutorialStartController missing." : null;
            return error == null;
        }
    }
}
