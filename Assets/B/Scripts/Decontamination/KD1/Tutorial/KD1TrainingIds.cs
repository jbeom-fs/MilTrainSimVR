namespace MilTrainSimVR.B.Decontamination.KD1.Tutorial
{
    // Stable IDs and the step data of the KD-1 course (training data, scene target and states). Display text lives in
    // TrainingSubtitles, looked up by step ID.
    //
    // Four steps, each needing one fact of this run (State conditions; the runner only moves forward, so a step once passed never
    // comes back when the pad is taken off for a moment):
    //   kd1-open          <- outer-opened : the outer pack has been open in this run;
    //   kd1-prepare-pad   <- pad-out      : a pad has been fully out of its packet (free or fitted) in this run;
    //   kd1-fit-pad       <- pad-fitted   : a pad has been fitted on a hand in this run (either hand; one pad is enough);
    //   kd1-touch-areas   <- areas-done   : every marked area of the surface practice is recorded now (any order).
    // The first three are kept once seen in the run (KD1TrainingCourse), so a fact reached between two runner checks is not lost.
    public static class KD1TrainingIds
    {
        public const string TrainingId = "kd1-decontamination-practice";
        public const string CourseTarget = "kd1-course";

        public const string OuterOpened = "outer-opened";
        public const string PadOut = "pad-out";
        public const string PadFitted = "pad-fitted";
        public const string AreasDone = "areas-done";

        public const string OpenStep = "kd1-open";
        public const string PrepareStep = "kd1-prepare-pad";
        public const string FitStep = "kd1-fit-pad";
        public const string TouchStep = "kd1-touch-areas";

        // (step ID, state it needs) in course order.
        public static readonly (string step, string signal)[] Steps =
        {
            (OpenStep, OuterOpened), (PrepareStep, PadOut), (FitStep, PadFitted), (TouchStep, AreasDone)
        };

        public static readonly string[] Signals = { OuterOpened, PadOut, PadFitted, AreasDone };

        public static int StepCount => Steps.Length;

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
