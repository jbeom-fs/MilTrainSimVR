using MilTrainSimVR.B.BleedingControl.PressureBandage;
using UnityEngine;

namespace MilTrainSimVR.B.TrainingField
{
    // Wrap friction loop of the pressure bandage station: a 3D looping source on this object, which follows the bandage roll
    // (this object is not part of the equipment; the roll's parent, physics and grab stay as they are).
    //
    // It only reads PressureBandageApplication after its LateUpdate (order 102) of the same frame and never recomputes the wrap
    // rules: the loop sounds while the wrap progress really rises, i.e. this frame's WrapProgress is above the previous sample
    // while the course runs (TrainingFieldCourseState.Running), the application is not held (Suspended, stop confirm) or
    // resetting, the stage is Wrapping in both samples and nothing blocks the wrap (Block None: roll held by a tracked hand in
    // the zone, dressing placed, input focused, start allowed). A held roll that does not add progress is silent.
    // The previous sample is taken again (no sound from that change) on the first sample after enable, when ResetCount or
    // CancelCount changes and when the stage was not Wrapping in the previous sample, so a reset, cancel, the wrap start (0) and
    // the last frame (EndPlacement, progress shown as 1) are never heard as wrapping.
    // A short gap (progressGap) of frames without progress keeps the loop; any blocked or held state stops it at once with a
    // short fade (fadeOut). Start fades in (fadeIn). Play / UnPause only when it starts, Pause or Stop when silent: no call per
    // frame. Nothing of the bandage (wrap amount, time, stage, transforms, checks) is written.
    [DefaultExecutionOrder(310)]
    [DisallowMultipleComponent]
    public sealed class BandageWrapSfx : MonoBehaviour
    {
        const float Rise = 1e-6f;

        [SerializeField] TrainingFieldStation station;
        [SerializeField] PressureBandageApplication application;
        [Tooltip("3D source with the wrap loop clip: spatial blend 1, loop on, play on awake off, Doppler 0.")]
        [SerializeField] AudioSource source;
        [SerializeField, Range(0f, 1f)] float volume = 1f;
        [Tooltip("Fade in at a start (s).")]
        [SerializeField, Min(0f)] float fadeIn = 0.04f;
        [Tooltip("Fade out at a stop (s).")]
        [SerializeField, Min(0f)] float fadeOut = 0.08f;
        [Tooltip("Frames without progress shorter than this (s) keep the loop on; longer stops it.")]
        [SerializeField, Min(0f)] float progressGap = 0.1f;

        bool sampled;
        float lastProgress;
        PressureBandageStage lastStage;
        int lastResets;
        int lastCancels;
        float lastRise = -1f;
        float level;
        bool paused;

        public TrainingFieldStation Station => station;
        public PressureBandageApplication BandageApplication => application;
        public AudioSource Source => source;
        public float Volume => volume;
        // The loop is heard (or fading out) now.
        public bool IsSounding => source != null && source.isPlaying;

        void OnEnable() => Silence();

        void OnDisable() => Silence();

        void LateUpdate()
        {
            if (station == null || application == null || source == null)
                return;
            var roll = application.RollGrab;
            if (roll != null && roll.gameObject.activeInHierarchy)
                transform.position = roll.transform.position;

            var stage = application.Stage;
            var progress = application.WrapProgress;
            var link = station.CourseLink;
            var allowed = link != null && link.State == TrainingFieldCourseState.Running && !application.Suspended && !application.IsResetting &&
                          stage == PressureBandageStage.Wrapping && application.Block == PressureWrapBlock.None;
            var rebase = !sampled || application.ResetCount != lastResets || application.CancelCount != lastCancels ||
                         lastStage != PressureBandageStage.Wrapping;
            if (!allowed || rebase)
                lastRise = -1f;
            else if (progress > lastProgress + Rise)
                lastRise = Time.unscaledTime;
            sampled = true;
            lastProgress = progress;
            lastStage = stage;
            lastResets = application.ResetCount;
            lastCancels = application.CancelCount;

            var on = lastRise >= 0f && Time.unscaledTime - lastRise <= progressGap;
            Ramp(on, allowed);
        }

        // Fade toward the target; start once, then Pause (still wrapping, only paused) or Stop when silent.
        void Ramp(bool on, bool keepPosition)
        {
            var target = on ? volume : 0f;
            var time = on ? fadeIn : fadeOut;
            level = time > 0f ? Mathf.MoveTowards(level, target, Mathf.Max(volume, 0.01f) * Time.unscaledDeltaTime / time) : target;
            if (on && !source.isPlaying)
            {
                if (paused)
                    source.UnPause();
                else
                    source.Play();
                paused = false;
            }
            source.volume = level;
            if (on || level > 0f)
                return;
            if (keepPosition)
            {
                if (source.isPlaying)
                {
                    source.Pause();
                    paused = true;
                }
            }
            else if (source.isPlaying || paused)
            {
                source.Stop();
                paused = false;
            }
        }

        // Enable, disable (scene unload included): no sound, next sample is a new baseline.
        void Silence()
        {
            sampled = false;
            lastRise = -1f;
            level = 0f;
            paused = false;
            if (source != null)
            {
                source.Stop();
                source.volume = 0f;
            }
        }
    }
}
