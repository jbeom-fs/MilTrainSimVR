using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MilTrainSimVR.B.TrainingField
{
    // Training UI sounds of one training field scene, in SceneSystems. Both are 2D (spatial blend 0): heard the same wherever the
    // head turns or the station stands.
    // - Click: once per Button.onClick of the listed training UI buttons (start, step / final check, stop, continue, abort,
    //   retry, end). onClick only fires for an active, interactable button that was really pressed, so hover, a ray that only
    //   touches it, a drag of a scroll body and a blocked button never click. The sound plays on this object, so a button hidden by
    //   its own click handling does not cut it. Device hotspots, the hub exit door knob and other helpers are not listed.
    // - Completion: once per run, on TrainingFieldCoordinator.CourseCompleted (the course's own Completed edge).
    // It only listens: no button, course, result or field state is changed, and a missing clip or source only silences it.
    // Listeners are added in OnEnable and removed in OnDisable, once per button.
    [DisallowMultipleComponent]
    public sealed class TrainingFieldSfx : MonoBehaviour
    {
        [SerializeField] TrainingFieldCoordinator coordinator;
        [Tooltip("2D source for the one-shot sounds: spatial blend 0, play on awake off, loop off, no clip of its own.")]
        [SerializeField] AudioSource source;
        [SerializeField] AudioClip clickClip;
        [SerializeField, Range(0f, 1f)] float clickVolume = 1f;
        [SerializeField] AudioClip completeClip;
        [SerializeField, Range(0f, 1f)] float completeVolume = 1f;
        [Tooltip("Training UI buttons of this field's courses and stations that click (each once).")]
        [SerializeField] Button[] clickButtons = Array.Empty<Button>();

        readonly List<Button> subscribed = new List<Button>();
        UnityAction click;
        bool listening;

        public TrainingFieldCoordinator Coordinator => coordinator;
        public AudioSource Source => source;
        public AudioClip ClickClip => clickClip;
        public AudioClip CompleteClip => completeClip;
        public float ClickVolume => clickVolume;
        public float CompleteVolume => completeVolume;
        public IReadOnlyList<Button> ClickButtons => clickButtons;

        void OnEnable()
        {
            if (click == null)
                click = PlayClick;
            foreach (var button in clickButtons)
            {
                if (button == null || subscribed.Contains(button))
                    continue;
                button.onClick.AddListener(click);
                subscribed.Add(button);
            }
            if (coordinator != null)
            {
                coordinator.CourseCompleted += OnCourseCompleted;
                listening = true;
            }
        }

        void OnDisable()
        {
            foreach (var button in subscribed)
            {
                if (button != null)
                    button.onClick.RemoveListener(click);
            }
            subscribed.Clear();
            if (listening)
            {
                coordinator.CourseCompleted -= OnCourseCompleted;
                listening = false;
            }
            if (source != null)
                source.Stop();
        }

        void PlayClick() => Play(clickClip, clickVolume);

        void OnCourseCompleted(TrainingFieldStation station) => Play(completeClip, completeVolume);

        void Play(AudioClip clip, float volume)
        {
            if (clip == null || source == null || !source.isActiveAndEnabled)
                return;
            source.PlayOneShot(clip, volume);
        }
    }
}
