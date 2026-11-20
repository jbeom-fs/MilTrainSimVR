using System;
using MilTrainSimVR.B.AedTutorial;
using UnityEngine;

namespace MilTrainSimVR.B.TrainingField
{
    // AED sounds (see StationSfx). Signals, each a rising edge in a live frame:
    // - power on: AedPowerDevice.IsOn false -> true (PowerChanged reports the same change).
    // - pad attach: AedPad.IsAttached false -> true, each pad on its own (highlight or nearness is not attachment).
    // - analysis / charge alert: AedTutorialStartController.State entering AnalyzingRhythm (a new valid analysis after an
    //   interruption is a new entry) and AedShockDevice.State entering Charged. No repeat while analysing or charging.
    // - shock: AedShockDevice.State entering Discharged, which only TryDischarge commits (ShockPressed is only a request; a
    //   refused press, a press before the charge or with patient contact changes nothing). The device, pads and controller
    //   are the ones the course controller uses (same scene references).
    [DefaultExecutionOrder(320)]
    [DisallowMultipleComponent]
    public sealed class AedSfx : StationSfx
    {
        [SerializeField] AedPowerDevice powerDevice;
        [SerializeField] AedPad[] pads = Array.Empty<AedPad>();
        [SerializeField] AedShockDevice shockDevice;
        [SerializeField] AudioClip powerOnClip;
        [SerializeField, Range(0f, 1f)] float powerOnVolume = 1f;
        [SerializeField] AudioClip padAttachClip;
        [SerializeField, Range(0f, 1f)] float padAttachVolume = 1f;
        [SerializeField] AudioClip alertClip;
        [SerializeField, Range(0f, 1f)] float alertVolume = 1f;
        [Tooltip("Shortest time (s) between two alerts (sound only; the analysis and charge times are the course's).")]
        [SerializeField, Min(0f)] float alertMinInterval = 0.5f;
        [SerializeField] AudioClip shockClip;
        [SerializeField, Range(0f, 1f)] float shockVolume = 1f;

        AedTutorialStartController controller;
        bool lastOn;
        bool[] lastAttached = Array.Empty<bool>();
        bool lastAnalysing;
        bool lastCharged;
        bool lastDischarged;
        float lastAlert = -1f;

        public override TrainingFieldCourseId Course => TrainingFieldCourseId.Aed;
        public AedTutorialStartController Controller => controller;

        protected override bool Resolve()
        {
            controller = station.CourseLink is AedFieldCourse course ? course.Controller : null;
            lastAttached = new bool[pads.Length];
            return controller != null && powerDevice != null && shockDevice != null && pads.Length > 0 && Array.IndexOf(pads, null) < 0;
        }

        protected override void Sample(bool first, bool live)
        {
            var play = live && !first;
            if (Rose(powerDevice.IsOn, ref lastOn) && play)
                Play(powerOnClip, powerOnVolume, powerDevice.transform.position);
            for (var i = 0; i < pads.Length; i++)
            {
                if (Rose(pads[i].IsAttached, ref lastAttached[i]) && play)
                    Play(padAttachClip, padAttachVolume, pads[i].transform.position);
            }
            var analysing = Rose(controller.State == AedTutorialStartState.AnalyzingRhythm, ref lastAnalysing);
            var charged = Rose(shockDevice.State == AedShockState.Charged, ref lastCharged);
            if ((analysing || charged) && play && (lastAlert < 0f || Time.unscaledTime - lastAlert >= alertMinInterval))
            {
                lastAlert = Time.unscaledTime;
                Play(alertClip, alertVolume, powerDevice.transform.position);
            }
            if (Rose(shockDevice.State == AedShockState.Discharged, ref lastDischarged) && play)
                Play(shockClip, shockVolume, shockDevice.transform.position);
        }

        protected override void Silence()
        {
        }
    }
}
