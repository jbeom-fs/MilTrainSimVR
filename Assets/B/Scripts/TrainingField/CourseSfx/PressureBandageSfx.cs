using MilTrainSimVR.B.BleedingControl.PressureBandage;
using UnityEngine;

namespace MilTrainSimVR.B.TrainingField
{
    // Direct pressure / pressure bandage sounds (see StationSfx), next to BandageWrapSfx (the wrap loop, unchanged):
    // - dressing contact: PressureDressing.IsPlacementValid false -> true (the dressing covers the wound validly; near or wrong
    //   face is not valid). Re-armed for sound only after the placement has stayed invalid for rearmSeconds, so a jitter at the
    //   edge does not repeat it; lifting it off and placing it again sounds again.
    // - end attach: PressureBandageApplication.Stage EndPlacement -> Taping (the end attached by distance).
    // - tape: Stage Taping -> Fixed (the tape attach confirmed).
    [DefaultExecutionOrder(320)]
    [DisallowMultipleComponent]
    public sealed class PressureBandageSfx : StationSfx
    {
        [SerializeField] AudioClip dressingContactClip;
        [SerializeField, Range(0f, 1f)] float dressingContactVolume = 1f;
        [Tooltip("The placement must stay invalid this long (s) before the next contact sounds (sound only).")]
        [SerializeField, Min(0f)] float rearmSeconds = 0.4f;
        [SerializeField] AudioClip endAttachClip;
        [SerializeField, Range(0f, 1f)] float endAttachVolume = 1f;
        [SerializeField] AudioClip tapeClip;
        [SerializeField, Range(0f, 1f)] float tapeVolume = 1f;

        PressureDressing dressing;
        PressureBandageApplication application;
        bool lastValid;
        bool armed;
        float invalidSince = -1f;
        PressureBandageStage lastStage;

        public override TrainingFieldCourseId Course => TrainingFieldCourseId.PressureBandage;
        public PressureDressing Dressing => dressing;
        public PressureBandageApplication BandageApplication => application;

        protected override bool Resolve()
        {
            var start = station.CourseLink is PressureBandageFieldCourse course ? course.TrainingStart : null;
            var link = start != null ? start.Link : null;
            dressing = link != null ? link.Dressing : null;
            application = link != null ? link.BandageApplication : null;
            return dressing != null && application != null;
        }

        protected override void Sample(bool first, bool live)
        {
            var play = live && !first;
            // A baseline sample (enable, after a hold or focus loss) takes a valid placement as already made; an invalid one re-arms
            // only after rearmSeconds, as always.
            var valid = dressing.IsPlacementValid;
            if (!valid)
            {
                if (invalidSince < 0f)
                    invalidSince = Time.unscaledTime;
                if (Time.unscaledTime - invalidSince >= rearmSeconds)
                    armed = true;
            }
            else
            {
                invalidSince = -1f;
                if (first)
                    armed = false;
                else if (!lastValid && armed)
                {
                    armed = false;
                    if (play)
                        Play(dressingContactClip, dressingContactVolume, dressing.transform.position);
                }
            }
            lastValid = valid;

            var stage = application.Stage;
            if (!first && play && stage != lastStage)
            {
                if (lastStage == PressureBandageStage.EndPlacement && stage == PressureBandageStage.Taping)
                    Play(endAttachClip, endAttachVolume, dressing.transform.position);
                else if (lastStage == PressureBandageStage.Taping && stage == PressureBandageStage.Fixed)
                    Play(tapeClip, tapeVolume, dressing.transform.position);
            }
            lastStage = stage;
        }

        protected override void Silence()
        {
        }
    }
}
