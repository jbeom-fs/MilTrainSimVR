using System;
using UnityEngine;

namespace MilTrainSimVR.B.TrainingField
{
    // Base of the equipment sound observers of one training field station (one per course, on its own object under the station,
    // outside Content). Each subclass only reads its course's public equipment state in LateUpdate, after the equipment of the
    // frame, and plays a 3D one-shot on a real state change (a rising edge) or drives a loop while a real operation goes on. It
    // never calls a judge, writes equipment, course or field state, or changes a Transform, Rigidbody, Collider or grab of the
    // equipment. A missing reference only silences it.
    //
    // Live frames: a change sounds only when the course runs (TrainingFieldCourseState.Running), or in the frame the course
    // turns Completed right after a Running sample (the last operation of a run can complete the course in the same frame).
    // A reset, retry, scene entry or a course at its start screen, busy or faulted only takes new baselines. The first sample
    // after enable is a baseline too, so a state that already holds is never heard as new.
    // Loops: SfxLoop, on only while the subclass marks a real operation (progress rising, part moving), off at once with a
    // short fade when it is not allowed (stop confirm, release, tracking loss, completion, reset, disable).
    public abstract class StationSfx : MonoBehaviour
    {
        [SerializeField] protected TrainingFieldStation station;
        [Tooltip("3D one-shot source on this object (spatial blend 1, loop off, play on awake off, Doppler 0, no clip). It is moved to " +
                 "the part of each sound before it plays.")]
        [SerializeField] AudioSource oneShot;

        TrainingFieldCourseState lastState;
        bool sampled;
        bool ready;

        public TrainingFieldStation Station => station;
        public AudioSource OneShot => oneShot;
        public abstract TrainingFieldCourseId Course { get; }

        // This frame: the course runs and is not held by the stop confirm (loops may sound).
        protected bool Running { get; private set; }

        void Awake()
        {
            ready = station != null && station.CourseLink != null && station.Course == Course && Resolve();
            if (!ready)
                Debug.LogWarning($"[TrainingField] {GetType().Name} '{name}': station or {Course} equipment not found; its sounds stay off.", this);
        }

        void OnEnable() => sampled = false;

        void OnDisable()
        {
            if (oneShot != null)
                oneShot.Stop();
            Silence();
        }

        void LateUpdate()
        {
            if (!ready)
                return;
            var state = station.CourseLink.State;
            var held = station.Owner != null && station.Owner.IsPaused;
            var live = state == TrainingFieldCourseState.Running ||
                       (sampled && lastState == TrainingFieldCourseState.Running && state == TrainingFieldCourseState.Completed);
            Running = state == TrainingFieldCourseState.Running && !held;
            try
            {
                Sample(!sampled, live);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
            lastState = state;
            sampled = true;
        }

        // Finds the course equipment through the station's course link (once, in Awake).
        protected abstract bool Resolve();

        // One frame: compare with the previous sample; play only when live and not the first sample. Always store new values.
        protected abstract void Sample(bool first, bool live);

        // Loops off and baselines dropped (disable).
        protected abstract void Silence();

        protected void Play(AudioClip clip, float volume, Vector3 at)
        {
            if (clip == null || oneShot == null || !oneShot.isActiveAndEnabled)
                return;
            oneShot.transform.position = at;
            oneShot.PlayOneShot(clip, volume);
        }

        protected static bool Rose(bool now, ref bool last)
        {
            var rose = now && !last;
            last = now;
            return rose;
        }
    }

    // One looping sound of a station (its own 3D source, clip on the source). Mark() on every frame with a real operation; Drive()
    // every frame: on while a mark is younger than gap, faded in / out, Play or UnPause once at a start, Pause when silent while
    // still allowed (the next start continues the loop), Stop when not allowed. Longest stop delay: gap + fadeOut.
    [Serializable]
    public sealed class SfxLoop
    {
        [Tooltip("3D looping source (spatial blend 1, loop on, play on awake off, Doppler 0) with its clip.")]
        public AudioSource source;
        [Range(0f, 1f)] public float volume = 1f;
        [Min(0f)] public float fadeIn = 0.04f;
        [Min(0f)] public float fadeOut = 0.08f;
        [Tooltip("A pause in the operation shorter than this (s) keeps the loop on.")]
        [Min(0f)] public float gap = 0.2f;

        float lastMark = -1f;
        float level;
        bool paused;

        public bool IsSounding => source != null && source.isPlaying;

        public void Mark() => lastMark = Time.unscaledTime;

        public void Drive(bool allowed, Vector3 position)
        {
            if (source == null)
                return;
            if (!allowed)
                lastMark = -1f;
            source.transform.position = position;
            var on = lastMark >= 0f && Time.unscaledTime - lastMark <= gap;
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
            if (allowed)
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

        public void Silence()
        {
            lastMark = -1f;
            level = 0f;
            paused = false;
            if (source != null)
            {
                source.Stop();
                source.volume = 0f;
            }
        }
    }

    // Motion of a point in a reference frame, with a dead band against tracking tremor: Moved() is true in a frame where the point
    // has gone farther than deadBand from where it was last counted. A step larger than maxStep (teleport, tracking return,
    // reset jump) only takes a new reference. Sound judgement only.
    [Serializable]
    public sealed class SfxMotion
    {
        [Min(0.001f)] public float deadBand = 0.006f;
        [Min(0.01f)] public float maxStep = 0.15f;

        Vector3 reference;
        Vector3 previous;
        bool has;

        public void Clear() => has = false;

        public bool Moved(Vector3 point)
        {
            if (!has || (point - previous).magnitude > maxStep)
            {
                reference = previous = point;
                has = true;
                return false;
            }
            previous = point;
            if ((point - reference).magnitude <= deadBand)
                return false;
            reference = point;
            return true;
        }
    }
}
