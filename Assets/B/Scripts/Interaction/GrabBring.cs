using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MilTrainSimVR.B.Interaction
{
    // Far grab bring of an item moved by XRGrabInteractable. With a TrackedGrabHold installed (the B free items), the bring is the
    // hold's own progress: IsBringing is exactly that progress below 1 (it stops while the hand is not tracked). Without one, the
    // item is eased by XRI's attach ease-in and counts as being brought while its attach point is farther than ArrivedDistance from
    // the hand's (read after XRI's update of the frame, in LateUpdate). Callers complete nothing with an item (attach, wear, seat,
    // wrap) while BlocksCompletion: being brought, or held still by its hold (hand not tracked, no valid sample yet, or the
    // recovery sample of this frame).
    public static class GrabBring
    {
        // Distance (m) between the item's attach point and the hand's under which the item has arrived (no bring needed).
        public const float ArrivedDistance = 0.01f;

        public static bool IsBringing(XRGrabInteractable grab)
        {
            if (grab == null || !grab.isSelected)
                return false;
            var hold = TrackedGrabHold.Of(grab);
            if (hold != null)
                return hold.IsBringing;
            if (grab.attachEaseInTime <= 0f)
                return false;
            var interactor = grab.firstInteractorSelecting;
            if (interactor == null)
                return false;
            var itemAttach = grab.GetAttachTransform(interactor);
            var handAttach = interactor.GetAttachTransform(grab);
            return itemAttach != null && handAttach != null &&
                   (itemAttach.position - handAttach.position).sqrMagnitude > ArrivedDistance * ArrivedDistance;
        }

        // Held, but not following the hand this frame (TrackedGrabHold.IsHoldingStill); false without a hold.
        public static bool IsHoldingStill(XRGrabInteractable grab)
        {
            var hold = grab != null && grab.isSelected ? TrackedGrabHold.Of(grab) : null;
            return hold != null && hold.IsHoldingStill;
        }

        public static bool BlocksCompletion(XRGrabInteractable grab) => IsBringing(grab) || IsHoldingStill(grab);
    }
}
