using UnityEngine.InputSystem.XR;
using UnityEngine.XR;

namespace MilTrainSimVR.B.Decontamination.KD1
{
    // Controller tracking from its TrackedPoseDriver (XR Origin Starter Assets), read at the moment of the call. A Transform or a
    // selection alone is not tracking: the driver must be active, its tracking state action enabled and bound, both position and
    // rotation reported tracked, and its position action enabled and bound. Same rule as the protective clothing and pressure
    // bandage equipment (their helpers are internal to those assemblies).
    internal static class KD1Tracking
    {
        public static bool IsTracked(TrackedPoseDriver driver)
        {
            if (driver == null || !driver.isActiveAndEnabled)
                return false;

            var tracking = driver.trackingStateInput.action;
            if (tracking == null || !tracking.enabled || tracking.controls.Count == 0)
                return false;
            const InputTrackingState required = InputTrackingState.Position | InputTrackingState.Rotation;
            if (((InputTrackingState)tracking.ReadValue<int>() & required) != required)
                return false;

            var position = driver.positionInput.action;
            return position != null && position.enabled && position.controls.Count > 0;
        }
    }
}
