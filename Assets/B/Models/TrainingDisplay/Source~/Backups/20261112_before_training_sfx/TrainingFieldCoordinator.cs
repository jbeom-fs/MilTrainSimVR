using System;
using System.Collections.Generic;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace MilTrainSimVR.B.TrainingField
{
    // Operation of one training field scene (first aid or CBRN): one course at a time, the stop confirm, the end after completion,
    // the latest results of this visit and the entry pose. Lives in SceneSystems; the stations, their courses and the one XR
    // Origin are in the same scene. It never starts a course (only a course board's start button does) and never moves a station.
    // The field's exit is A's hub portal (HubPortal_ToHub instance under ExitAnchor): it is always on and goes back to the hub;
    // this coordinator neither locks nor watches it.
    //
    // Ownership (one course at a time): each course's start entry point asks its station (ITrainingStartGate) right before it
    // changes anything, and the station asks TryClaimStart here, in the same call. The first claim of a frame wins; any other
    // station's claim is refused before its runner or equipment changes, so two start presses in one frame never start two
    // courses. A claim whose start fails is returned at once (CancelStart). The owner keeps the field while its course runs, shows
    // its completion summary with the equipment still out, resets, or has faulted after it started (equipment may be left out);
    // it gives the field up only when its course is back at its start screen (stop, end after completion, the course's own retry)
    // and the last field reset did not fail. Blockers over the other stations' boards show this and keep their buttons from
    // being pressed, but the claim is what decides. A course started without the field's approval (missing gate link) is
    // adopted as owner and reported.
    //
    // Stop: "교육 중단" on a running station opens its confirm. While it is open the course is held (TrainingFieldCourse.SetHeld:
    // its input gate refuses new selections, no step is judged, its equipment changes nothing, its clocks (charge, placement
    // watch, time record) do not run) and the free rigid bodies of that station only are held still (TrainingFieldStation
    // .HoldPhysics). Global time is never changed: the simulator, locomotion, tracking, the UI and every other station keep
    // running. Every XR selection is kept: a part in hand stays in hand, never dropped, put back or attached by the dialog. "계속하기" releases the hold: the confirmed progress and the equipment stay; held parts take a new
    // hand baseline, so nothing jumps; a continuous contact or hands-off time starts again from zero. "중단하기" releases it, then
    // the course's own reset returns it to its start screen at this station; the result is Aborted. "교육 종료" after a
    // completion runs the same reset and keeps Completed.
    //
    // Entry, two states. Preparing (from the scene start): the XR Origin stands provisionally on the EntryAnchor (position,
    // horizontal facing), the rig's locomotion providers are off (no walk, turn or teleport that a later alignment would undo;
    // the head view and the boards stay live), every course start is refused and the boards say the entry is being prepared.
    // It waits for the head's position and rotation tracking as long as it takes (a headset connected late included; no time
    // limit ends it). Entered: on the first frame with that tracking, the origin is turned and moved once with the XROrigin API
    // so the tracked head (its real offset in the tracking space) stands over the anchor, looking along its forward, on the
    // anchor's height; locomotion comes back and starts are approved. It never writes the camera, never aligns again in this
    // scene visit: later tracking loss and return, the stop, end and retry of a course never move the user.
    //
    // Teleport while preparing: a teleport request of XRI is made only by a teleportation interactable (TeleportationArea,
    // TeleportationAnchor) on its own select or activate event, and an activate needs a selection; the provider stores a
    // request even while it is disabled. So from Start this coordinator is a select filter on every teleportation interactable
    // of its scene: while preparing none can be selected, no request is made, and none is stored for the moment the provider
    // comes back. When the entry is done, an interactor of the XR Origin whose select input is still pressed (held during the
    // wait or pressed on that frame) still cannot select one until that input has been seen released, so letting go of an
    // aim made while preparing never teleports; the next new press teleports as before. A selection in progress always passes
    // (none can start while preparing), so the filter never ends one itself. The locomotion providers that were on are off while
    // preparing as well (move and turn; a second layer for teleport); nothing that was off is switched on, and no interactor,
    // teleport target or input action is switched on or off here.
    //
    // Observer views: a station's mirror cameras do not draw the user bodies of the other stations (the K1 avatar and the
    // protective clothing body follow the same HMD), set per camera right before it renders and restored after.
    [DefaultExecutionOrder(300)]
    [DisallowMultipleComponent]
    public sealed class TrainingFieldCoordinator : MonoBehaviour, IXRSelectFilter
    {
        // After this long (unscaled s) still preparing, the wait is reported once in the log (the entry still waits).
        const float EntryWaitNotice = 3f;

        [SerializeField] TrainingFieldKind field;
        [SerializeField] TrainingFieldStation[] stations = Array.Empty<TrainingFieldStation>();
        [SerializeField] TMP_FontAsset koreanFont;

        [Header("Entry (the user's start pose in this field)")]
        [Tooltip("EntryAnchor root: the user's head is placed over it, facing its forward, when the scene starts.")]
        [SerializeField] Transform entryAnchor;
        [Tooltip("The one XR Origin of this field scene.")]
        [SerializeField] XROrigin xrOrigin;

        [Header("Entrance board")]
        [SerializeField] TMP_Text entranceTitleText;
        [SerializeField] TMP_Text entranceDescriptionText;
        [SerializeField] TMP_Text entranceGuideText;
        [SerializeField] TMP_Text entranceStationsText;
        [SerializeField] TMP_Text entranceMoveText;

        readonly Dictionary<Camera, List<Renderer>> hiddenPerCamera = new Dictionary<Camera, List<Renderer>>();
        readonly List<(Renderer renderer, bool off)> restore = new List<(Renderer, bool)>();
        TrainingFieldStation paused;
        bool valid;
        bool cameraHooks;
        string shownStations;
        // Start ownership: the station that owns the field, the frame of its claim and whether its course was seen in use since.
        TrainingFieldStation owner;
        int claimFrame = -1;
        bool ownerInUse;
        TrackedPoseDriver headDriver;
        bool entryPlaced;
        bool entryWaitLogged;
        float entryWaitStart;
        // Locomotion providers switched off while preparing the entry (switched on again once entered).
        readonly List<LocomotionProvider> heldLocomotion = new List<LocomotionProvider>();
        // Teleportation interactables of this scene with this filter, and the interactors that must let go of their select
        // input before they may select one (entry done with the input pressed).
        readonly List<BaseTeleportationInteractable> filteredTeleports = new List<BaseTeleportationInteractable>();
        readonly HashSet<IXRSelectInteractor> teleportWaitRelease = new HashSet<IXRSelectInteractor>();

        public TrainingFieldKind Field => field;
        public IReadOnlyList<TrainingFieldStation> Stations => stations;
        public Transform EntryAnchor => entryAnchor;
        public XROrigin Origin => xrOrigin;
        // The station that owns the field (its course is starting, running, completed, resetting or left faulted), or null.
        public TrainingFieldStation Active => owner;
        public bool IsPaused => paused != null;
        // The user stands at the entry by the tracked head (false while preparing: starts are refused).
        public bool IsEntered => entryPlaced;

        void Awake()
        {
            valid = TryValidate(out var error);
            if (!valid)
            {
                Debug.LogError($"[TrainingField] {error} The field cannot coordinate its stations; no course can start.", this);
                return;
            }
            Apply(entranceTitleText, field == TrainingFieldKind.Cbrn ? TrainingSubtitles.CbrnFieldTitle : TrainingSubtitles.FirstAidFieldTitle);
            Apply(entranceDescriptionText, field == TrainingFieldKind.Cbrn ? TrainingSubtitles.CbrnFieldDescription : TrainingSubtitles.FirstAidFieldDescription);
            Apply(entranceGuideText, TrainingSubtitles.EntryPreparingGuide);
            Apply(entranceMoveText, TrainingSubtitles.MoveGuide);
            headDriver = xrOrigin.Camera.GetComponent<TrackedPoseDriver>();
        }

        void OnEnable()
        {
            if (!valid)
                return;
            foreach (var station in stations)
            {
                station.Owner = this;
                station.StopRequested += OnStop;
                station.ContinueRequested += OnContinue;
                station.AbortRequested += OnAbort;
                station.EndRequested += OnEnd;
            }
        }

        // After every course component's OnEnable, so the per-camera hide runs after their own per-camera display rules.
        void Start()
        {
            if (!valid)
                return;
            BuildObserverExclusions();
            if (hiddenPerCamera.Count > 0)
            {
                RenderPipelineManager.beginCameraRendering += OnBeginCamera;
                RenderPipelineManager.endCameraRendering += OnEndCamera;
                cameraHooks = true;
            }

            // Provisional only (not the entry): the saved origin pose is never the start pose; the head offset is applied once
            // tracked, in Update. Locomotion waits for it.
            xrOrigin.transform.SetPositionAndRotation(entryAnchor.position, Quaternion.Euler(0f, entryAnchor.eulerAngles.y, 0f));
            Physics.SyncTransforms();
            entryWaitStart = Time.unscaledTime;
            FilterTeleports();
            HoldLocomotion();
        }

        void OnDisable()
        {
            if (!valid)
                return;
            foreach (var station in stations)
            {
                if (station.Owner == this)
                    station.Owner = null;
                station.StopRequested -= OnStop;
                station.ContinueRequested -= OnContinue;
                station.AbortRequested -= OnAbort;
                station.EndRequested -= OnEnd;
            }
            Resume();
            ReleaseLocomotion();
            UnfilterTeleports();
            if (cameraHooks)
            {
                RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
                RenderPipelineManager.endCameraRendering -= OnEndCamera;
                cameraHooks = false;
            }
        }

        void Update()
        {
            if (!valid)
                return;
            if (entryPlaced)
            {
                if (teleportWaitRelease.Count > 0 && teleportWaitRelease.RemoveWhere(IsSelectReleased) > 0 && teleportWaitRelease.Count == 0)
                    Debug.Log("[TrainingField] Teleport input released after the entry; a new teleport press works from now on.", this);
                return;
            }
            if (IsHeadTracked(headDriver))
            {
                PlaceAtEntry();
                return;
            }
            if (!entryWaitLogged && Time.unscaledTime - entryWaitStart >= EntryWaitNotice)
            {
                entryWaitLogged = true;
                Debug.Log($"[TrainingField] Entry still preparing after {EntryWaitNotice:F0} s: waiting for the headset's position and rotation tracking " +
                          "(starts refused, locomotion off until then).", this);
            }
        }

        // After the courses, their boards and start screens: this frame's states.
        void LateUpdate()
        {
            if (!valid)
                return;

            foreach (var station in stations)
            {
                var state = station.CourseLink.State;
                if (state == TrainingFieldCourseState.Completed && station.SeenState != TrainingFieldCourseState.Completed)
                {
                    station.LastResult = TrainingFieldResult.Completed;
                    Debug.Log($"[TrainingField] {station.Course}: completed.", station);
                }
                if (state == TrainingFieldCourseState.Waiting)
                    station.ResetFailed = false;
                if (InUse(state) && station != owner && !InUse(station.SeenState))
                    AdoptUnclaimed(station);
                station.SeenState = state;
            }
            UpdateOwner();

            // A held course that left Running by itself (fault, reset) ends the confirm; otherwise a part let go during the dialog
            // is held still too.
            if (paused != null && paused.CourseLink.State != TrainingFieldCourseState.Running)
                Resume();
            else if (paused != null)
                paused.HoldPhysics();

            foreach (var station in stations)
                ShowStation(station);
            ShowEntranceStations();
        }

        static bool InUse(TrainingFieldCourseState state) =>
            state == TrainingFieldCourseState.Running || state == TrainingFieldCourseState.Completed || state == TrainingFieldCourseState.Busy;

        // ---------- Start ownership ----------

        // Asked by a station's start gate, inside its course's start call, before the course changes anything.
        internal bool TryClaimStart(TrainingFieldStation station)
        {
            if (!valid || station == null || Array.IndexOf(stations, station) < 0)
                return false;
            if (!entryPlaced)
            {
                Debug.Log($"[TrainingField] {station.Course}: start refused, the entry is still being prepared (headset tracking).", station);
                return false;
            }
            if (paused != null || (owner != null && owner != station))
            {
                Debug.Log($"[TrainingField] {station.Course}: start refused, {(paused != null ? "a stop confirm is open" : owner.Course + " owns the field")}.", station);
                return false;
            }
            owner = station;
            claimFrame = Time.frameCount;
            ownerInUse = false;
            Debug.Log($"[TrainingField] {station.Course}: start approved; the field belongs to this station until it is back at its start screen.", station);
            return true;
        }

        // The approved start did not happen (the course's own checks or runner start failed): the claim is returned now.
        internal void CancelStart(TrainingFieldStation station)
        {
            if (station == null || owner != station || InUse(station.CourseLink.State))
                return;
            Debug.Log($"[TrainingField] {station.Course}: start failed after approval; the field is free again.", station);
            ReleaseOwner();
        }

        void UpdateOwner()
        {
            if (owner == null)
                return;
            var state = owner.SeenState;
            if (InUse(state))
            {
                ownerInUse = true;
                return;
            }
            if (!ownerInUse)
            {
                // Approved but never started (a start path that neither started nor cancelled): free after its frame.
                if (Time.frameCount > claimFrame || state == TrainingFieldCourseState.Faulted)
                {
                    Debug.LogWarning($"[TrainingField] {owner.Course}: approved start did not happen; the field is free again.", owner);
                    ReleaseOwner();
                }
                return;
            }
            // After its run: free only at its start screen with no failed field reset. A course that faulted after it started
            // keeps the field (its equipment may still be out).
            if (state == TrainingFieldCourseState.Waiting && !owner.ResetFailed)
            {
                Debug.Log($"[TrainingField] {owner.Course}: back at its start screen; the field is free.", owner);
                ReleaseOwner();
            }
        }

        // A course in use without a claim (its start gate is not linked): it owns the field from now on, so no other one starts.
        void AdoptUnclaimed(TrainingFieldStation station)
        {
            if (owner == null)
            {
                owner = station;
                claimFrame = Time.frameCount;
                ownerInUse = true;
                Debug.LogWarning($"[TrainingField] {station.Course} started without the field's approval (its start gate link is missing); " +
                                 "it owns the field now. Run the field build menu.", station);
                return;
            }
            Debug.LogError($"[TrainingField] {station.Course} is in use while {owner.Course} owns the field: its start gate link is missing. " +
                           "Only one course may run; run the field build menu.", station);
        }

        void ReleaseOwner()
        {
            owner = null;
            claimFrame = -1;
            ownerInUse = false;
        }

        // ---------- Display ----------

        // Inside the course display: stop while running, end after completion, the latest result on the start screen, the confirm
        // over the display while it is open, and a blocker message while this course cannot be used (entry preparing, another
        // station in use, or this course faulted or left by a failed reset; a running course keeps its stop for another try).
        void ShowStation(TrainingFieldStation station)
        {
            var state = station.SeenState;
            var confirm = paused == station;
            string blocker = null;
            if (!entryPlaced)
                blocker = TrainingSubtitles.BlockedEntryPreparing;
            else if (confirm)
                blocker = null;
            else if (owner != null && owner != station)
                blocker = string.Format(TrainingSubtitles.BlockedOtherFormat, owner.DisplayName);
            else if (station.ResetFailed && state != TrainingFieldCourseState.Running)
                blocker = TrainingSubtitles.BlockedResetFailed;
            else if (state == TrainingFieldCourseState.Faulted)
                blocker = TrainingSubtitles.BlockedFaulted;
            station.Show(state == TrainingFieldCourseState.Running, state == TrainingFieldCourseState.Completed,
                state == TrainingFieldCourseState.Waiting, confirm, blocker);
        }

        void ShowEntranceStations()
        {
            if (entranceStationsText == null)
                return;
            var lines = new List<string>();
            foreach (var station in stations)
            {
                var result = station.LastResult == TrainingFieldResult.Completed ? " · " + TrainingSubtitles.ResultCompleted
                    : station.LastResult == TrainingFieldResult.Aborted ? " · " + TrainingSubtitles.ResultAborted
                    : string.Empty;
                lines.Add($"{lines.Count + 1}. {station.DisplayName}{result}");
            }
            var text = string.Join("\n", lines);
            if (text == shownStations)
                return;
            shownStations = text;
            Apply(entranceStationsText, text);
        }

        // ---------- Stop confirm, stop and end ----------

        void OnStop(TrainingFieldStation station)
        {
            if (paused != null || station.CourseLink.State != TrainingFieldCourseState.Running)
                return;
            paused = station;
            // Hold first: from here the course and its equipment change nothing, while every XR selection stays as it is.
            station.CourseLink.SetHeld(true);
            station.HoldPhysics();
            Debug.Log($"[TrainingField] {station.Course}: stop confirm open; course, equipment and its free bodies held (selections kept; global time unchanged).", station);
            ShowStation(station);
        }

        void OnContinue(TrainingFieldStation station)
        {
            if (paused != station)
                return;
            Resume();
            Debug.Log($"[TrainingField] {station.Course}: continued after the stop confirm.", station);
            ShowStation(station);
        }

        void OnAbort(TrainingFieldStation station)
        {
            if (paused != station)
                return;
            Resume();
            ReturnToStart(station, TrainingFieldResult.Aborted, "stopped");
        }

        void OnEnd(TrainingFieldStation station)
        {
            if (paused != null || station.CourseLink.State != TrainingFieldCourseState.Completed)
                return;
            // The completion stays the result of this run; the end only cleans the station up.
            ReturnToStart(station, TrainingFieldResult.Completed, "ended after completion");
        }

        void ReturnToStart(TrainingFieldStation station, TrainingFieldResult result, string reason)
        {
            var ok = station.CourseLink.TryReturnToStart();
            station.ResetFailed = !ok;
            if (ok)
            {
                station.LastResult = result;
                Debug.Log($"[TrainingField] {station.Course}: {reason}; back to its start screen at this station.", station);
            }
            else
            {
                Debug.LogError($"[TrainingField] {station.Course}: {reason}, but the course reset failed (see the course's error); " +
                               "the field stays with this station, so no other course starts over its equipment.", station);
            }
            station.SeenState = station.CourseLink.State;
            UpdateOwner();
            ShowStation(station);
        }

        void Resume()
        {
            if (paused == null)
                return;
            var station = paused;
            paused = null;
            station.ReleasePhysics();
            station.CourseLink.SetHeld(false);
        }

        // ---------- Entry ----------

        // Once per scene visit, on the first frame the head is tracked: head over the anchor (its real offset in the tracking
        // space included), facing the anchor's forward, origin on the anchor's height. Only the XR Origin moves (XROrigin API);
        // the camera stays the tracked pose's. Then the entry is done: locomotion back, starts approved, boards normal.
        void PlaceAtEntry()
        {
            var forward = Vector3.ProjectOnPlane(entryAnchor.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-6f)
                forward = Vector3.forward;
            xrOrigin.MatchOriginUpCameraForward(Vector3.up, forward.normalized);
            xrOrigin.MoveCameraToWorldLocation(entryAnchor.position + Vector3.up * xrOrigin.CameraInOriginSpaceHeight);
            Physics.SyncTransforms();
            entryPlaced = true;
            HoldPressedTeleportInput();
            ReleaseLocomotion();
            Apply(entranceGuideText, TrainingSubtitles.FieldGuide);
            Debug.Log($"[TrainingField] Entry done after {Time.unscaledTime - entryWaitStart:F1} s: the tracked head is placed at the EntryAnchor " +
                      $"{entryAnchor.position} facing {forward.normalized}; starts are approved from now on.", this);
        }

        // ---------- Teleport requests while preparing (see the class notes) ----------

        bool IXRSelectFilter.canProcess => isActiveAndEnabled;

        bool IXRSelectFilter.Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            if (interactor == null)
                return false;
            if (interactor.IsSelecting(interactable))
                return true;
            return entryPlaced && !teleportWaitRelease.Contains(interactor);
        }

        void FilterTeleports()
        {
            filteredTeleports.Clear();
            foreach (var teleport in FindObjectsByType<BaseTeleportationInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (teleport.gameObject.scene != gameObject.scene)
                    continue;
                teleport.selectFilters.Add(this);
                filteredTeleports.Add(teleport);
            }
            Debug.Log($"[TrainingField] Entry preparing: {filteredTeleports.Count} teleport target(s) take no teleport until the entry is done.", this);
        }

        void UnfilterTeleports()
        {
            foreach (var teleport in filteredTeleports)
            {
                if (teleport != null)
                    teleport.selectFilters.Remove(this);
            }
            filteredTeleports.Clear();
            teleportWaitRelease.Clear();
        }

        // Entry done: the XR Origin's interactors whose select input is pressed now wait for its release (see the class notes).
        void HoldPressedTeleportInput()
        {
            teleportWaitRelease.Clear();
            foreach (var interactor in xrOrigin.GetComponentsInChildren<XRBaseInputInteractor>(true))
            {
                if (!IsSelectReleased(interactor))
                    teleportWaitRelease.Add(interactor);
            }
            if (teleportWaitRelease.Count > 0)
                Debug.Log($"[TrainingField] Entry done with {teleportWaitRelease.Count} select input(s) pressed: no teleport from them until released.", this);
        }

        // Released: gone, switched off (a teleport ray is off while its input is not held) or its select input not pressed.
        static bool IsSelectReleased(IXRSelectInteractor interactor) =>
            !(interactor is XRBaseInputInteractor input) || input == null || !input.isActiveAndEnabled ||
            !(input.isSelectActive || input.selectInput.ReadIsPerformed());

        // Preparing the entry: the rig's enabled locomotion providers (move, turn, teleport) are switched off.
        void HoldLocomotion()
        {
            heldLocomotion.Clear();
            foreach (var provider in xrOrigin.GetComponentsInChildren<LocomotionProvider>(true))
            {
                if (!provider.enabled)
                    continue;
                provider.enabled = false;
                heldLocomotion.Add(provider);
            }
        }

        // Exactly the providers switched off by HoldLocomotion come back on.
        void ReleaseLocomotion()
        {
            foreach (var provider in heldLocomotion)
            {
                if (provider != null)
                    provider.enabled = true;
            }
            heldLocomotion.Clear();
        }

        // Position and rotation of the head both tracked by its live pose driver.
        static bool IsHeadTracked(TrackedPoseDriver driver)
        {
            if (driver == null || !driver.isActiveAndEnabled)
                return false;
            var tracking = driver.trackingStateInput.action;
            if (tracking == null || !tracking.enabled || tracking.controls.Count == 0)
                return false;
            const InputTrackingState required = InputTrackingState.Position | InputTrackingState.Rotation;
            return ((InputTrackingState)tracking.ReadValue<int>() & required) == required;
        }

        // ---------- Observer views ----------

        void BuildObserverExclusions()
        {
            hiddenPerCamera.Clear();
            foreach (var viewer in stations)
            {
                foreach (var camera in viewer.ObserverCameras)
                {
                    foreach (var other in stations)
                    {
                        if (other == viewer || other.UserBodyRenderers.Count == 0)
                            continue;
                        if (!hiddenPerCamera.TryGetValue(camera, out var list))
                            hiddenPerCamera[camera] = list = new List<Renderer>();
                        list.AddRange(other.UserBodyRenderers);
                    }
                }
            }
        }

        void OnBeginCamera(ScriptableRenderContext _, Camera camera)
        {
            restore.Clear();
            if (!hiddenPerCamera.TryGetValue(camera, out var list))
                return;
            foreach (var r in list)
            {
                if (r == null)
                    continue;
                restore.Add((r, r.forceRenderingOff));
                r.forceRenderingOff = true;
            }
        }

        void OnEndCamera(ScriptableRenderContext _, Camera camera)
        {
            foreach (var (r, off) in restore)
            {
                if (r != null)
                    r.forceRenderingOff = off;
            }
            restore.Clear();
        }

        void Apply(TMP_Text text, string value)
        {
            if (text == null)
                return;
            text.font = koreanFont;
            text.text = value;
        }

        public bool TryValidate(out string error)
        {
            var problems = new List<string>();
            if (koreanFont == null)
                problems.Add("korean font missing");
            if (entryAnchor == null)
                problems.Add("EntryAnchor missing");
            if (xrOrigin == null || xrOrigin.Camera == null)
                problems.Add("XR Origin (with its camera) missing");
            else if (entryAnchor != null && (xrOrigin.transform.IsChildOf(entryAnchor) || entryAnchor.IsChildOf(xrOrigin.transform)))
                problems.Add("EntryAnchor and the XR Origin must not contain each other");
            if (stations == null || stations.Length != 3 || Array.IndexOf(stations, null) >= 0)
                problems.Add("three stations required");
            else
            {
                var seen = new HashSet<TrainingFieldCourseId>();
                foreach (var station in stations)
                {
                    if (!seen.Add(station.Course))
                        problems.Add($"course {station.Course} twice");
                    if (TrainingFieldIds.FieldOf(station.Course) != field)
                        problems.Add($"{station.Course} is not a {field} course");
                    if (!station.TryValidate(out var stationError))
                        problems.Add(stationError);
                    if (xrOrigin != null && station.transform.IsChildOf(xrOrigin.transform))
                        problems.Add($"{station.name} is under the XR Origin");
                }
            }
            error = problems.Count > 0 ? $"TrainingFieldCoordinator '{name}': {string.Join(", ", problems)}." : null;
            return error == null;
        }
    }
}
