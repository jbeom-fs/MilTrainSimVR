using UnityEngine;

namespace MilTrainSimVR.B.Interaction
{
    // Held pose of a free item whose grab transformer writes the pose itself (K1 mask after extraction, KD-1 pieces). The item
    // keeps the rotation it had when the hold began and turns only with the hand's own rotation since then: no snapping to the
    // hand's default angle, no turn toward the camera, no stick rotation. A far grab brings it to the hand smoothly: over
    // bringSeconds the item point nearest to the hand at the grab (its grip point) moves to the hand; a grip point already within
    // nearReach of the hand is not moved (a near grab holds the item where it is). While IsBringing, callers do not complete
    // anything with the item (attach, wear, contact). Rebase (tracking return, input hold over, hand pose made valid again) keeps
    // the current pose and continues any remaining bring from there, so nothing jumps; Begin is for a new grab or a hand swap.
    public sealed class FreeHold
    {
        public const float DefaultBringSeconds = 0.3f;
        public const float DefaultNearReach = 0.1f;

        Vector3 segmentStart;
        Vector3 segmentEnd;
        Quaternion rotationOffset = Quaternion.identity;
        Vector3 gripLocal;
        bool bringing;
        float bringSeconds;
        float segmentSeconds;
        float progress;
        int advancedFrame = -1;

        public bool IsActive { get; private set; }
        public bool IsBringing => IsActive && bringing && progress < 1f;

        // hand: the holding controller pose now. item: the item pose now. gripWorld: the item point to bring to the hand (the
        // closest point of its grab colliders to the hand).
        public void Begin(Pose hand, Pose item, Vector3 gripWorld, float seconds = DefaultBringSeconds, float nearReach = DefaultNearReach)
        {
            IsActive = true;
            Capture(hand, item);
            gripLocal = Quaternion.Inverse(item.rotation) * (gripWorld - item.position);
            bringSeconds = Mathf.Max(0f, seconds);
            bringing = bringSeconds > 0f && Vector3.Distance(gripWorld, hand.position) > nearReach;
            segmentSeconds = bringSeconds;
            progress = bringing ? 0f : 1f;
            segmentEnd = bringing ? -(rotationOffset * gripLocal) : segmentStart;
            advancedFrame = Time.frameCount;
        }

        // Keeps the item where it is now relative to the hand; a bring under way continues from here for its remaining part.
        public void Rebase(Pose hand, Pose item)
        {
            if (!IsActive)
            {
                Begin(hand, item, hand.position, 0f);
                return;
            }
            var remaining = bringing ? 1f - progress : 0f;
            Capture(hand, item);
            if (remaining > 0f)
            {
                segmentEnd = -(rotationOffset * gripLocal);
                segmentSeconds = bringSeconds * remaining;
                progress = 0f;
            }
            else
            {
                bringing = false;
                progress = 1f;
                segmentEnd = segmentStart;
            }
            advancedFrame = Time.frameCount;
        }

        public void End()
        {
            IsActive = false;
            bringing = false;
        }

        // Item pose for this hand pose; the bring advances once per frame.
        public Pose Evaluate(Pose hand)
        {
            if (bringing && progress < 1f && advancedFrame != Time.frameCount)
            {
                advancedFrame = Time.frameCount;
                progress = segmentSeconds > 0f ? Mathf.Min(1f, progress + Time.deltaTime / segmentSeconds) : 1f;
            }
            var offset = bringing ? Vector3.Lerp(segmentStart, segmentEnd, Mathf.SmoothStep(0f, 1f, progress)) : segmentStart;
            return new Pose(hand.position + hand.rotation * offset, hand.rotation * rotationOffset);
        }

        void Capture(Pose hand, Pose item)
        {
            var inverse = Quaternion.Inverse(hand.rotation);
            segmentStart = inverse * (item.position - hand.position);
            rotationOffset = inverse * item.rotation;
        }
    }
}
