using Unity.XR.CoreUtils;
using UnityEngine;

namespace MilTrainSimVR.B.Interaction
{
    // Input point of a part operated in place (a lid, a strap point, a windlass, a stored item being pulled). It starts at the
    // part's grab point and then moves only by the real hand's motion inside the XR Origin's tracking space, so:
    // - near and far grabs give the same amount for the same hand motion (no distance gain, no lever from a far grab point);
    // - locomotion (move, turn, teleport) moves the origin, not the hand inside it, so it never moves this point and is never
    //   counted as operation;
    // - CarriedByRig: for a part the player carries (on an item held in the other hand, or on the head), the point rides with
    //   the origin as well, so its relation to that part stays as it would with the real hand.
    // Step(false) (hand not tracked, input held, a dialog open) keeps the point and drops the hand sample, so the jump when
    // tracking returns is never added. Begin (new grab, hand swap) takes the reference at once and adds nothing.
    public sealed class RelativeHand
    {
        Transform hand;
        Transform space;
        bool hasHand;
        Vector3 lastLocal;
        Quaternion lastLocalRotation = Quaternion.identity;
        bool hasSpace;
        Pose lastSpace;

        public bool IsActive => hand != null;
        public Transform Hand => hand;
        public Vector3 Position { get; private set; }
        public Quaternion Rotation { get; private set; } = Quaternion.identity;
        public bool CarriedByRig { get; set; }

        public void Begin(Transform handTransform, Vector3 startPosition) => Begin(handTransform, startPosition, Quaternion.identity);

        public void Begin(Transform handTransform, Vector3 startPosition, Quaternion startRotation)
        {
            hand = handTransform;
            space = TrackingSpaceOf(handTransform);
            Position = startPosition;
            Rotation = startRotation;
            hasHand = false;
            hasSpace = false;
            Step(handTransform != null);
        }

        public void End()
        {
            hand = null;
            space = null;
            hasHand = false;
            hasSpace = false;
        }

        // Next Step only takes the reference (nothing is added for the gap since the last sample).
        public void Rebase() => hasHand = false;

        public void Step(bool handValid)
        {
            if (hand == null)
                return;
            var now = space != null ? new Pose(space.position, space.rotation) : Pose.identity;
            if (CarriedByRig && hasSpace)
            {
                var turn = now.rotation * Quaternion.Inverse(lastSpace.rotation);
                Position = now.position + turn * (Position - lastSpace.position);
                Rotation = turn * Rotation;
            }
            lastSpace = now;
            hasSpace = true;

            if (!handValid)
            {
                hasHand = false;
                return;
            }
            var inverse = Quaternion.Inverse(now.rotation);
            var local = inverse * (hand.position - now.position);
            var localRotation = inverse * hand.rotation;
            if (hasHand)
            {
                Position += now.rotation * (local - lastLocal);
                Rotation = now.rotation * (localRotation * Quaternion.Inverse(lastLocalRotation)) * inverse * Rotation;
            }
            lastLocal = local;
            lastLocalRotation = localRotation;
            hasHand = true;
        }

        // The XR Origin's tracking space (camera floor offset) that holds the controllers; null outside an XR Origin (world).
        static Transform TrackingSpaceOf(Transform t)
        {
            if (t == null)
                return null;
            var origin = t.GetComponentInParent<XROrigin>();
            if (origin == null)
                return null;
            if (origin.CameraFloorOffsetObject != null)
                return origin.CameraFloorOffsetObject.transform;
            return origin.Origin != null ? origin.Origin.transform : origin.transform;
        }
    }
}
