using System;
using System.Collections.Generic;
using MilTrainSimVR.B.Training;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MilTrainSimVR.B.TrainingField
{
    // One practice station of a training field, on its StationRoot (the only transform the field layout moves: position and
    // horizontal rotation in edit mode, scale 1). Everything of the course is under Content: practice table, patient or target,
    // equipment, judge volumes, the course board, markers and its runner. The course keeps its own start, steps, completion and
    // retry; it reads no field state. Equipment that a hand or the user's body takes keeps its own initial parent inside Content
    // (each course stores its start pose in its parent's frame when it wakes), so a reset puts it back at this station wherever
    // the station stands.
    //
    // The station's controls live inside the course display. On the course board canvas, outside every scroll body, in its fixed
    // bottom row: "교육 중단" while the course runs, "교육 종료" after it completed (beside the course's retry) and the latest
    // result of this visit on the start screen. Blockers are field-owned canvases just in front of the course board: shown while
    // the entry is being prepared, another station is in use (this course cannot start), this course cannot run (fault, failed
    // reset) or this course is held by the stop confirm, so no board or device button is pressed meanwhile. The stop confirm is the
    // last part of the first blocker (the one over the course display): it covers the whole display above the shade and is the
    // only part that takes rays while it is open. These parts only show what the coordinator (TrainingFieldCoordinator) decides;
    // their buttons only report to it.
    //
    // Start gate (ITrainingStartGate): the course's start entry point (startGate field, linked to this station) asks this station
    // right before it changes anything; the station asks its coordinator in the same call. Without a coordinator (not linked,
    // invalid or disabled) every start is refused, so a broken field never runs two courses.
    //
    // FloorAnchor: the floor of this station (its world height is the floor the course's user body stands on). Part of the
    // station, moved with it; never taken from the background model.
    [DisallowMultipleComponent]
    public sealed class TrainingFieldStation : MonoBehaviour, ITrainingStartGate
    {
        [SerializeField] TrainingFieldCourseId course;
        [SerializeField] TrainingFieldCourse courseLink;
        [Tooltip("Every course object of this station (moved here from the course's tutorial scene).")]
        [SerializeField] Transform content;
        [Tooltip("Where the trainee stands to practise, facing +Z of this anchor (station frame).")]
        [SerializeField] Transform practiceAnchor;
        [Tooltip("Floor of this station (world height used by the course's user body: K1 avatar, protective clothing body).")]
        [SerializeField] Transform floorAnchor;
        [Tooltip("Practice range in this station's frame (centre and size, m).")]
        [SerializeField] Vector3 areaCenter = new Vector3(0f, 1f, 0.6f);
        [SerializeField] Vector3 areaSize = new Vector3(2.4f, 2f, 2.4f);
        [SerializeField] TMP_FontAsset koreanFont;

        [Header("Display controls (course board: bottom row; stop confirm: inside the first blocker)")]
        [SerializeField] TMP_Text resultText;
        [SerializeField] Button stopButton;
        [SerializeField] TMP_Text stopButtonText;
        [SerializeField] Button endButton;
        [SerializeField] TMP_Text endButtonText;
        [SerializeField] GameObject confirmPanel;
        [SerializeField] TMP_Text confirmQuestionText;
        [SerializeField] TMP_Text confirmDetailText;
        [SerializeField] Button continueButton;
        [SerializeField] TMP_Text continueButtonText;
        [SerializeField] Button abortButton;
        [SerializeField] TMP_Text abortButtonText;

        [Header("Course board blockers (one per course board canvas)")]
        [SerializeField] GameObject[] blockers = Array.Empty<GameObject>();
        [SerializeField] TMP_Text[] blockerTexts = Array.Empty<TMP_Text>();

        [Header("Observer views (mirrors) and the user's body of this course")]
        [Tooltip("Cameras of this station that show the user's body (mirror, rear view).")]
        [SerializeField] Camera[] observerCameras = Array.Empty<Camera>();
        [Tooltip("Renderers of this course's user body or avatar; other stations' observer cameras do not draw them.")]
        [SerializeField] Renderer[] userBodyRenderers = Array.Empty<Renderer>();

        // Free rigid bodies of this station held still by the stop confirm, with the motion they had.
        Rigidbody[] bodies = Array.Empty<Rigidbody>();
        readonly Dictionary<Rigidbody, (Vector3 linear, Vector3 angular)> heldBodies = new Dictionary<Rigidbody, (Vector3, Vector3)>();
        string shownResult;
        string shownBlocker;
        bool valid;

        public event Action<TrainingFieldStation> StopRequested;
        public event Action<TrainingFieldStation> ContinueRequested;
        public event Action<TrainingFieldStation> AbortRequested;
        public event Action<TrainingFieldStation> EndRequested;

        public TrainingFieldCourseId Course => course;
        public string DisplayName => TrainingSubtitles.StationName(course);
        public TrainingFieldCourse CourseLink => courseLink;
        public Transform Content => content;
        public Transform PracticeAnchor => practiceAnchor;
        public Transform FloorAnchor => floorAnchor;
        public Bounds LocalArea => new Bounds(areaCenter, areaSize);
        public IReadOnlyList<GameObject> Blockers => blockers;
        public IReadOnlyList<Camera> ObserverCameras => observerCameras;
        public IReadOnlyList<Renderer> UserBodyRenderers => userBodyRenderers;
        public bool IsValid => valid;

        // Latest result in this visit (memory only). A completion is not overwritten by the end that follows it.
        public TrainingFieldResult LastResult { get; set; }
        // Course state seen by the coordinator in its previous frame (completion edge).
        public TrainingFieldCourseState SeenState { get; set; } = TrainingFieldCourseState.Waiting;
        public bool ResetFailed { get; set; }
        // The coordinator that approves this station's starts (set while it is enabled).
        internal TrainingFieldCoordinator Owner { get; set; }

        bool ITrainingStartGate.TryClaimStart() => valid && Owner != null && Owner.isActiveAndEnabled && Owner.TryClaimStart(this);

        void ITrainingStartGate.CancelStart()
        {
            if (Owner != null)
                Owner.CancelStart(this);
        }

        void Awake()
        {
            valid = TryValidate(out var error);
            if (!valid)
            {
                Debug.LogError($"[TrainingField] {error}", this);
                return;
            }
            bodies = content.GetComponentsInChildren<Rigidbody>(true);
            resultText.gameObject.SetActive(false);
            stopButton.gameObject.SetActive(false);
            endButton.gameObject.SetActive(false);
            Apply(stopButtonText, TrainingSubtitles.StopButton);
            Apply(endButtonText, TrainingSubtitles.EndButton);
            Apply(confirmQuestionText, TrainingSubtitles.ConfirmQuestion);
            Apply(confirmDetailText, TrainingSubtitles.ConfirmDetail);
            Apply(continueButtonText, TrainingSubtitles.ContinueButton);
            Apply(abortButtonText, TrainingSubtitles.AbortButton);
            confirmPanel.SetActive(false);
            foreach (var blocker in blockers)
                blocker.SetActive(false);
        }

        void OnEnable()
        {
            if (!valid)
                return;
            stopButton.onClick.AddListener(OnStop);
            endButton.onClick.AddListener(OnEnd);
            continueButton.onClick.AddListener(OnContinue);
            abortButton.onClick.AddListener(OnAbort);
        }

        void OnDisable()
        {
            if (!valid)
                return;
            stopButton.onClick.RemoveListener(OnStop);
            endButton.onClick.RemoveListener(OnEnd);
            continueButton.onClick.RemoveListener(OnContinue);
            abortButton.onClick.RemoveListener(OnAbort);
        }

        void OnStop() => StopRequested?.Invoke(this);
        void OnEnd() => EndRequested?.Invoke(this);
        void OnContinue() => ContinueRequested?.Invoke(this);
        void OnAbort() => AbortRequested?.Invoke(this);

        // Stop confirm (called every frame while it is open): every free (non-kinematic) rigid body of this station that no hand
        // holds is made kinematic with its motion kept, so nothing of this course falls, slides or settles meanwhile; a part let go
        // during the dialog is caught on the next call. Bodies held by XRI stay XRI's. Only this station; global time unchanged.
        public void HoldPhysics()
        {
            foreach (var body in bodies)
            {
                if (body == null || body.isKinematic || heldBodies.ContainsKey(body) || !body.gameObject.activeInHierarchy || IsHeldByHand(body))
                    continue;
                heldBodies[body] = (body.linearVelocity, body.angularVelocity);
                body.isKinematic = true;
            }
        }

        // Confirm closed: each body made kinematic here is free again with its motion, unless something else has taken it over
        // since (its own course set it, or a hand holds it).
        public void ReleasePhysics()
        {
            foreach (var pair in heldBodies)
            {
                var body = pair.Key;
                if (body == null || !body.isKinematic || IsHeldByHand(body))
                    continue;
                body.isKinematic = false;
                body.linearVelocity = pair.Value.linear;
                body.angularVelocity = pair.Value.angular;
            }
            heldBodies.Clear();
        }

        static bool IsHeldByHand(Rigidbody body) => body.TryGetComponent<XRGrabInteractable>(out var grab) && grab.isSelected;

        // What the display shows now (decided by the coordinator). The blockers are up while the confirm is open or a blocker message
        // is given; under the confirm their message is left empty (the confirm covers the display).
        public void Show(bool stopShown, bool endShown, bool resultShown, bool confirmShown, string blocker)
        {
            if (!valid)
                return;
            var result = !resultShown ? string.Empty
                : LastResult == TrainingFieldResult.Completed ? TrainingSubtitles.ResultCompleted
                : LastResult == TrainingFieldResult.Aborted ? TrainingSubtitles.ResultAborted
                : string.Empty;
            if (result != shownResult)
            {
                shownResult = result;
                Apply(resultText, result);
            }
            SetActive(resultText.gameObject, result.Length > 0);
            SetActive(stopButton.gameObject, stopShown && !confirmShown);
            SetActive(endButton.gameObject, endShown && !confirmShown);
            SetActive(confirmPanel, confirmShown);

            var message = confirmShown ? string.Empty : blocker ?? string.Empty;
            var blocked = confirmShown || message.Length > 0;
            if (blocked && message != shownBlocker)
            {
                foreach (var text in blockerTexts)
                    Apply(text, message);
            }
            shownBlocker = blocked ? message : null;
            foreach (var b in blockers)
                SetActive(b, blocked);
        }

        static void SetActive(GameObject go, bool active)
        {
            if (go.activeSelf != active)
                go.SetActive(active);
        }

        void Apply(TMP_Text text, string value)
        {
            text.font = koreanFont;
            text.text = value;
        }

        public bool TryValidate(out string error)
        {
            var missing = new List<string>();
            if (courseLink == null)
                missing.Add(nameof(courseLink));
            if (content == null)
                missing.Add(nameof(content));
            if (practiceAnchor == null)
                missing.Add(nameof(practiceAnchor));
            if (floorAnchor == null)
                missing.Add(nameof(floorAnchor));
            if (koreanFont == null)
                missing.Add(nameof(koreanFont));
            if (resultText == null)
                missing.Add(nameof(resultText));
            if (stopButton == null || stopButtonText == null || endButton == null || endButtonText == null)
                missing.Add("stop or end button");
            if (confirmPanel == null || confirmQuestionText == null || confirmDetailText == null || continueButton == null ||
                continueButtonText == null || abortButton == null || abortButtonText == null)
                missing.Add("stop confirm");
            if (blockers == null || blockerTexts == null || blockers.Length == 0 || blockers.Length != blockerTexts.Length ||
                Array.IndexOf(blockers, null) >= 0 || Array.IndexOf(blockerTexts, null) >= 0)
                missing.Add("board blockers (one text each)");
            if (observerCameras == null || Array.IndexOf(observerCameras, null) >= 0 || userBodyRenderers == null ||
                Array.IndexOf(userBodyRenderers, null) >= 0)
                missing.Add("observer camera or user body renderer entries");

            error = missing.Count > 0 ? $"TrainingFieldStation '{name}' missing references: {string.Join(", ", missing)}." : null;
            if (error == null && courseLink.Course != course)
                error = $"TrainingFieldStation '{name}': the course link is {courseLink.Course}, not {course}.";
            if (error == null && !courseLink.TryValidate(out error))
                return false;
            if (error == null && (!courseLink.transform.IsChildOf(transform) || !content.IsChildOf(transform) || !practiceAnchor.IsChildOf(transform) ||
                                  !floorAnchor.IsChildOf(transform) || floorAnchor.IsChildOf(content)))
                error = $"TrainingFieldStation '{name}': the course link, content, practice anchor and floor anchor must be under this station (the floor anchor not inside Content).";
            // The confirm sits over the display inside the first blocker (above its shade, the only part taking rays while open); the
            // stop, end and result parts are on the course board, under the blockers, so a blocker keeps them from being pressed.
            if (error == null && (!confirmPanel.transform.IsChildOf(blockers[0].transform) || confirmPanel == blockers[0] ||
                                  Array.Exists(blockers, b => stopButton.transform.IsChildOf(b.transform) || endButton.transform.IsChildOf(b.transform) ||
                                                              resultText.transform.IsChildOf(b.transform))))
                error = $"TrainingFieldStation '{name}': the stop confirm must be inside the first blocker, the stop, end and result parts outside every blocker.";
            var scale = transform.lossyScale;
            if (error == null && (Mathf.Abs(scale.x - 1f) > 1e-3f || Mathf.Abs(scale.y - 1f) > 1e-3f || Mathf.Abs(scale.z - 1f) > 1e-3f))
                error = $"TrainingFieldStation '{name}': the station scale must be 1 (now {scale}).";
            return error == null;
        }
    }
}
