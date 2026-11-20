using UnityEngine;

namespace MilTrainSimVR.B.TrainingUI
{
    // Colour roles of the B course boards, taken from the KD-1 course board (B_KD1_Tutorial, KD1TrainingBoard), the reference
    // the other courses follow. Runtime painters (step boxes) read these values, so what they paint matches the colours saved
    // on the boards. Nothing here changes a course's progress, equipment, completion or reset.
    public static class TrainingBoardStyle
    {
        public static readonly Color Background = new Color(0.07f, 0.09f, 0.11f, 1f);
        public static readonly Color Text = new Color(0.94f, 0.95f, 0.96f, 1f);
        // Ready line on the start screen and the hint line of the practice.
        public static readonly Color Notice = new Color(1f, 0.78f, 0.35f, 1f);
        public static readonly Color Button = new Color(0.16f, 0.42f, 0.7f, 1f);
        // Card behind a course's own check items (pressure bandage check card).
        public static readonly Color Card = new Color(0.12f, 0.15f, 0.18f, 1f);
        public static readonly Color StepDone = new Color(0.25f, 0.62f, 0.36f, 1f);
        public static readonly Color StepCurrent = new Color(0.16f, 0.42f, 0.7f, 1f);
        public static readonly Color StepAhead = new Color(0.22f, 0.25f, 0.28f, 1f);

        // Box i of a step row while the course shows step current: before it done, it current, after it ahead. current < 0 (start,
        // completion, reset) leaves every box ahead; current at the step count (a course's final check after its last step) every box done.
        public static Color StepColor(int index, int current) => index < current ? StepDone : index == current ? StepCurrent : StepAhead;
    }
}
