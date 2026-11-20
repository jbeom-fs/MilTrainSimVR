using UnityEngine.InputSystem.XR;
using UnityEngine.XR;

namespace MilTrainSimVR.B.BleedingControl.PressureBandage
{
    public enum PressureHandTracking
    {
        // No pose driver for this hand (missing reference or object): nothing to track.
        Missing = 0,
        // The driver exists, but position and rotation are not both reported as tracked. Its Transform keeps the last pose.
        NotTracked = 1,
        Tracked = 2
    }

    // Hand tracking from the controller's TrackedPoseDriver (XR Origin Starter Assets). A Transform alone is not tracking: the
    // driver must be active, its tracking state action enabled and bound, and both position and rotation reported tracked.
    internal static class PressureBandageTracking
    {
        public static PressureHandTracking Of(TrackedPoseDriver driver)
        {
            if (driver == null)
                return PressureHandTracking.Missing;
            if (!driver.isActiveAndEnabled)
                return PressureHandTracking.NotTracked;

            var tracking = driver.trackingStateInput.action;
            if (tracking == null || !tracking.enabled || tracking.controls.Count == 0)
                return PressureHandTracking.NotTracked;
            const InputTrackingState required = InputTrackingState.Position | InputTrackingState.Rotation;
            if (((InputTrackingState)tracking.ReadValue<int>() & required) != required)
                return PressureHandTracking.NotTracked;

            var position = driver.positionInput.action;
            return position != null && position.enabled && position.controls.Count > 0
                ? PressureHandTracking.Tracked
                : PressureHandTracking.NotTracked;
        }
    }
}
