using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MilTrainSimVR.B.Interaction
{
    // Far grab bring of an item moved by XRGrabInteractable's own transformer (dynamic attach with the item's rotation kept, attach
    // ease-in). A grab from a distance eases the item's attach point to the hand over the grab's attach ease-in time; until it
    // arrives the item is being brought, and callers do not complete anything with it (attach, wear, seat, wrap). Read it after
    // XRI's update of the frame (LateUpdate): an Instantaneous held item that is not easing then has its attach point on the hand's
    // attach point. Locomotion is applied before XRI's update (XRBodyTransformer runs first), so walking never reads as bringing.
    public static class GrabBring
    {
        // Distance (m) between the item's attach point and the hand's under which the item has arrived.
        public const float ArrivedDistance = 0.01f;

        public static bool IsBringing(XRGrabInteractable grab)
        {
            if (grab == null || !grab.isSelected || grab.attachEaseInTime <= 0f)
                return false;
            var interactor = grab.firstInteractorSelecting;
            if (interactor == null)
                return false;
            var itemAttach = grab.GetAttachTransform(interactor);
            var handAttach = interactor.GetAttachTransform(grab);
            return itemAttach != null && handAttach != null &&
                   (itemAttach.position - handAttach.position).sqrMagnitude > ArrivedDistance * ArrivedDistance;
        }
    }
}
