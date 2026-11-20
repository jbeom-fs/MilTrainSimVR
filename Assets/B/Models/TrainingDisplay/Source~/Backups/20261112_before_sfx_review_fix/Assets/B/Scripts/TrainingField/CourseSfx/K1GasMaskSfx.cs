using MilTrainSimVR.B.K1GasMask;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MilTrainSimVR.B.TrainingField
{
    // K1 gas mask sounds (see StationSfx), four different signals:
    // - button release: K1CarrierLid.IsUnlocked false -> true (Unlocked).
    // - flap open: after the unlock, Openness rising through flapSoundOpenness. Openness only moves with the hand on the flap
    //   handle (reset closes it without a sound); re-armed when the flap is back below flapRearmOpenness.
    // - mask extract: K1MaskExtraction.IsExtracted false -> true (Extracted); a grab alone or a reset move is silent.
    // - strap pull loop: a strap point (K1MaskWear.StrapLeft / StrapRight) selected by a tracked hand while the straps are
    //   driven by it (Extracted, Unfolding, Ready, FaceHeld, Strapping; not while preparing, waiting for tracking, suspended or
    //   worn), and in the same short time both the strap point moved in the mask's frame (the harness really follows the hand)
    //   and the hand moved in the XR Origin's frame (the user moves it). Moving the whole mask, turning the head, the automatic
    //   unfold, the snap and the automatic seat alone are silent. Sound judgement only; the wear rules are unchanged.
    [DefaultExecutionOrder(320)]
    [DisallowMultipleComponent]
    public sealed class K1GasMaskSfx : StationSfx
    {
        [SerializeField] AudioClip buttonClip;
        [SerializeField, Range(0f, 1f)] float buttonVolume = 1f;
        [SerializeField] AudioClip flapClip;
        [SerializeField, Range(0f, 1f)] float flapVolume = 1f;
        [SerializeField, Range(0.05f, 0.95f)] float flapSoundOpenness = 0.3f;
        [SerializeField, Range(0f, 0.5f)] float flapRearmOpenness = 0.05f;
        [SerializeField] AudioClip extractClip;
        [SerializeField, Range(0f, 1f)] float extractVolume = 1f;
        [SerializeField] SfxLoop strapLoop = new SfxLoop();
        [Tooltip("Strap point motion in the mask's frame (m).")]
        [SerializeField] SfxMotion strapMotion = new SfxMotion();
        [Tooltip("Strap hand motion in the XR Origin's frame (m).")]
        [SerializeField] SfxMotion handMotion = new SfxMotion();

        K1CarrierLid carrier;
        K1MaskExtraction extraction;
        K1MaskWear wear;
        bool lastUnlocked;
        float lastOpenness;
        bool flapArmed;
        bool lastExtracted;
        XRSimpleInteractable lastStrap;
        float strapMovedAt = -1f;
        float handMovedAt = -1f;

        public override TrainingFieldCourseId Course => TrainingFieldCourseId.K1GasMask;
        public K1CarrierLid Carrier => carrier;
        public K1MaskExtraction Extraction => extraction;
        public K1MaskWear Wear => wear;
        public SfxLoop StrapLoop => strapLoop;

        protected override bool Resolve()
        {
            var guide = station.CourseLink is K1GasMaskFieldCourse course ? course.TrainingGuide : null;
            var link = guide != null ? guide.Link : null;
            carrier = link != null ? link.Carrier : null;
            extraction = link != null ? link.Extraction : null;
            wear = link != null ? link.Wear : null;
            return carrier != null && extraction != null && wear != null && wear.StrapLeft != null && wear.StrapRight != null;
        }

        protected override void Sample(bool first, bool live)
        {
            var play = live && !first;
            if (Rose(carrier.IsUnlocked, ref lastUnlocked) && play)
                Play(buttonClip, buttonVolume, carrier.transform.position);

            var openness = carrier.Openness;
            if (first || openness <= flapRearmOpenness)
                flapArmed = openness <= flapRearmOpenness;
            else if (flapArmed && carrier.IsUnlocked && lastOpenness < flapSoundOpenness && openness >= flapSoundOpenness)
            {
                flapArmed = false;
                if (play)
                    Play(flapClip, flapVolume, carrier.transform.position);
            }
            lastOpenness = openness;

            if (Rose(extraction.IsExtracted, ref lastExtracted) && play)
                Play(extractClip, extractVolume, extraction.transform.position);

            DriveStrap(first);
        }

        void DriveStrap(bool first)
        {
            var strap = wear.StrapLeft.isSelected ? wear.StrapLeft : wear.StrapRight.isSelected ? wear.StrapRight : null;
            var state = wear.State;
            var allowed = Running && strap != null && !wear.Suspended && !wear.IsPreparing && !wear.IsWaitingForTracking &&
                          (state == K1MaskWearState.Extracted || state == K1MaskWearState.Unfolding || state == K1MaskWearState.Ready ||
                           state == K1MaskWearState.FaceHeld || state == K1MaskWearState.Strapping);
            if (!allowed || first || strap != lastStrap)
            {
                strapMotion.Clear();
                handMotion.Clear();
                strapMovedAt = handMovedAt = -1f;
            }
            lastStrap = allowed ? strap : null;
            var position = strap != null ? strap.transform.position : wear.transform.position;
            if (allowed)
            {
                var now = Time.unscaledTime;
                var hand = strap.firstInteractorSelecting.transform.position;
                var origin = station.Owner != null && station.Owner.Origin != null ? station.Owner.Origin.transform : null;
                if (strapMotion.Moved(wear.transform.InverseTransformPoint(position)))
                    strapMovedAt = now;
                if (handMotion.Moved(origin != null ? origin.InverseTransformPoint(hand) : hand))
                    handMovedAt = now;
                if (strapMovedAt >= 0f && handMovedAt >= 0f && now - strapMovedAt <= strapLoop.gap && now - handMovedAt <= strapLoop.gap)
                    strapLoop.Mark();
            }
            strapLoop.Drive(allowed, position);
        }

        protected override void Silence()
        {
            strapLoop.Silence();
            strapMotion.Clear();
            handMotion.Clear();
            lastStrap = null;
        }
    }
}
