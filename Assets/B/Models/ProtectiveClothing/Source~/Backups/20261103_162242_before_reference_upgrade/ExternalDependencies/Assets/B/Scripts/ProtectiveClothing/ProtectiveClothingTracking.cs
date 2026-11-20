using UnityEngine.InputSystem.XR;
using UnityEngine.XR;

namespace MilTrainSimVR.B.ProtectiveClothing
{
    public enum ProtectiveClothingHandTracking
    {
        // No pose driver for this hand (missing reference or object): nothing to track.
        Missing = 0,
        // The driver exists, but position and rotation are not both reported as tracked. Its Transform keeps the last pose.
        NotTracked = 1,
        Tracked = 2
    }

    // Hand tracking from the controller's TrackedPoseDriver (XR Origin Starter Assets), read at the moment of the call. A Transform
    // or a selection alone is not tracking: the driver must be active, its tracking state action enabled and bound, and both
    // position and rotation reported tracked. Same rule as the pressure bandage equipment (its helper is internal to that
    // assembly).
    internal static class ProtectiveClothingTracking
    {
        public static ProtectiveClothingHandTracking Of(TrackedPoseDriver driver)
        {
            if (driver == null)
                return ProtectiveClothingHandTracking.Missing;
            if (!driver.isActiveAndEnabled)
                return ProtectiveClothingHandTracking.NotTracked;

            var tracking = driver.trackingStateInput.action;
            if (tracking == null || !tracking.enabled || tracking.controls.Count == 0)
                return ProtectiveClothingHandTracking.NotTracked;
            const InputTrackingState required = InputTrackingState.Position | InputTrackingState.Rotation;
            if (((InputTrackingState)tracking.ReadValue<int>() & required) != required)
                return ProtectiveClothingHandTracking.NotTracked;

            var position = driver.positionInput.action;
            return position != null && position.enabled && position.controls.Count > 0
                ? ProtectiveClothingHandTracking.Tracked
                : ProtectiveClothingHandTracking.NotTracked;
        }
    }
}
