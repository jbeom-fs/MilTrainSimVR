namespace MilTrainSimVR.B.Training
{
    // Optional start approval of a scene that holds several courses (a B training field: only one course may run at a time).
    // A course start entry point asks TryClaimStart after its own "waiting for the start" check and before it changes anything
    // (runner, equipment, start state); false means another course owns the field, and the start is refused with nothing changed.
    // When a granted start does not happen after all (the course's own readiness or runner start failed), the course calls
    // CancelStart in the same call, so the claim never outlives a failed start. The owner gives the claim up by itself once the
    // course is back at its start screen after a run (stop, end after completion, retry). Empty in a course's own tutorial
    // scene, where every course starts as before.
    public interface ITrainingStartGate
    {
        bool TryClaimStart();
        void CancelStart();
    }

    // The courses' serialized gate field is a plain Object reference (linked to the station in a training field scene, empty elsewhere).
    public static class TrainingStartGate
    {
        // No gate: the course's own scene, the start goes on. A set gate decides; a set object that is not a gate refuses, so a
        // wrong link never lets a second course start.
        public static bool TryClaim(UnityEngine.Object gate) => gate == null || gate is ITrainingStartGate g && g.TryClaimStart();

        public static void Cancel(UnityEngine.Object gate)
        {
            if (gate != null && gate is ITrainingStartGate g)
                g.CancelStart();
        }
    }
}
