namespace MilTrainSimVR.B.TrainingField
{
    public enum TrainingFieldCourseId
    {
        Aed = 0,
        Tourniquet = 1,
        PressureBandage = 2,
        K1GasMask = 3,
        ProtectiveClothing = 4,
        KD1 = 5
    }

    public enum TrainingFieldKind
    {
        FirstAid = 0,
        Cbrn = 1
    }

    // State of one station's course as the field sees it (read from the course's own state, never written).
    public enum TrainingFieldCourseState
    {
        // Start screen: free to start; another station may start meanwhile.
        Waiting = 0,
        // A run started from this station's board and not yet completed.
        Running = 1,
        // Completed: the course shows its summary; equipment and guidance are kept until retry or the field's end.
        Completed = 2,
        // The course's own reset is under way.
        Busy = 3,
        // References invalid or the course faulted: nothing can be started here.
        Faulted = 4
    }

    // Latest result of a station in this visit of the field scene (memory only; no score, pass grade or saving).
    public enum TrainingFieldResult
    {
        None = 0,
        Completed = 1,
        Aborted = 2
    }

    public static class TrainingFieldIds
    {
        public const string FirstAidScenePath = "Assets/B/Scene/B_FirstAid.unity";
        public const string CbrnScenePath = "Assets/B/Scene/B_CBRN.unity";

        public static string ScenePath(TrainingFieldKind field) => field == TrainingFieldKind.Cbrn ? CbrnScenePath : FirstAidScenePath;
        public static TrainingFieldKind Other(TrainingFieldKind field) => field == TrainingFieldKind.Cbrn ? TrainingFieldKind.FirstAid : TrainingFieldKind.Cbrn;

        public static TrainingFieldKind FieldOf(TrainingFieldCourseId course) =>
            course == TrainingFieldCourseId.K1GasMask || course == TrainingFieldCourseId.ProtectiveClothing || course == TrainingFieldCourseId.KD1
                ? TrainingFieldKind.Cbrn
                : TrainingFieldKind.FirstAid;
    }
}
