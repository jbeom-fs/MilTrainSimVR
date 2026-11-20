namespace MilTrainSimVR.B.K1GasMask.Tutorial
{
    // Stable IDs shared by the K1 training data, the scene targets, the equipment link and the guide. Not display text.
    public static class K1TrainingIds
    {
        public const string TrainingId = "k1-gas-mask-donning";

        public const string CarrierTarget = "k1-carrier";
        public const string MaskTarget = "k1-mask";

        public const string ClosureReleased = "closure-released";
        public const string LidFullyOpened = "lid-fully-opened";
        public const string MaskExtracted = "mask-extracted";
        public const string MaskOnFace = "mask-on-face";
        public const string MaskWorn = "mask-worn";

        public const string ReleaseClosureStep = "k1-release-closure";
        public const string OpenLidStep = "k1-open-lid";
        public const string ExtractMaskStep = "k1-extract-mask";
        public const string BringToFaceStep = "k1-bring-to-face";
        public const string SeatStrapsStep = "k1-seat-straps";

        // Practice steps in order, each completed by one fact observed in the current run: (step, target, signal).
        public static readonly (string step, string target, string signal)[] Steps =
        {
            (ReleaseClosureStep, CarrierTarget, ClosureReleased),
            (OpenLidStep, CarrierTarget, LidFullyOpened),
            (ExtractMaskStep, MaskTarget, MaskExtracted),
            (BringToFaceStep, MaskTarget, MaskOnFace),
            (SeatStrapsStep, MaskTarget, MaskWorn),
        };

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
