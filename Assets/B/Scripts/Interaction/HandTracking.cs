using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MilTrainSimVR.B.Interaction
{
    // Hand pose tracking read from the controller's TrackedPoseDriver now, the same rule as the B equipment readers
    // (TourniquetDevice, PressureBandageTracking, ProtectiveClothingTracking): a selection or a Transform alone is not tracking. The
    // driver must be active, its tracking state action enabled and bound with both position and rotation reported tracked, and its
    // position and rotation actions enabled and bound.
    public static class HandTracking
    {
        public static TrackedPoseDriver DriverOf(IXRInteractor interactor) =>
            interactor != null && interactor.transform != null ? interactor.transform.GetComponentInParent<TrackedPoseDriver>() : null;

        public static bool IsPoseTracked(TrackedPoseDriver driver)
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
            var rotation = driver.rotationInput.action;
            return position != null && position.enabled && position.controls.Count > 0 &&
                   rotation != null && rotation.enabled && rotation.controls.Count > 0;
        }
    }
}
