namespace MilTrainSimVR.B.BleedingControl.Tourniquet.Tutorial
{
    // Stable IDs shared by the tourniquet training data, the scene targets, the equipment link, the guide and the final check.
    // Not display text.
    public static class TourniquetTrainingIds
    {
        public const string TrainingId = "tq-tourniquet-virtual-handling";

        public const string DeviceTarget = "tq-device";
        // Trainee input target, kept apart from the device states: its one state is the accepted final check input.
        public const string TraineeTarget = "tq-trainee";

        // Current device states (read every frame, never latched into a later run).
        public const string Attached = "attached";
        public const string BandPrepared = "band-prepared";
        public const string TurnReached = "turn-reached";
        public const string WindlassSecured = "windlass-secured";
        public const string TimeRecorded = "time-recorded";
        public const string StrapFastened = "strap-fastened";
        // Trainee state: true only from an accepted final check input until the runner judges it (or the input is cancelled).
        public const string FinalConfirmed = "final-confirmed";

        public const string PlaceStep = "tq-place";
        public const string BandStep = "tq-band";
        public const string TurnStep = "tq-turn";
        public const string SecureStep = "tq-secure";
        public const string RecordStep = "tq-record";
        public const string FastenStep = "tq-fasten";
        // Course step after the six practice steps; not a practice step and not counted in "k / 6".
        public const string FinalCheckStep = "tq-final-check";

        // The six practice steps in order with the device state each one adds. A step's conditions are its own state and every
        // earlier state, all as current values on DeviceTarget: a later step cannot finish while an earlier state is no longer
        // valid, and the last practice step needs all six at once. Step index i and device state index i are the same. The final
        // check step is not in this list; its conditions are all six device states plus FinalConfirmed on TraineeTarget.
        public static readonly (string step, string signal)[] Steps =
        {
            (PlaceStep, Attached),
            (BandStep, BandPrepared),
            (TurnStep, TurnReached),
            (SecureStep, WindlassSecured),
            (RecordStep, TimeRecorded),
            (FastenStep, StrapFastened),
        };

        // Index of a practice step, or -1 (also for the final check step).
        public static int StepIndex(string stepId)
        {
            for (var i = 0; i < Steps.Length; i++)
            {
                if (Steps[i].step == stepId)
                    return i;
            }

            return -1;
        }
    }
}
