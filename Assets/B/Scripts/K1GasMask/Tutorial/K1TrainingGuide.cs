using System;
using System.Collections.Generic;
using MilTrainSimVR.B.Training;
using MilTrainSimVR.B.TrainingUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MilTrainSimVR.B.K1GasMask.Tutorial
{
    public enum K1TrainingGuideState
    {
        NotInitialized = 0,
        WaitingForStart = 1,
        Running = 2,
        Completed = 3,
        Faulted = 4
    }

    // One world-space board for the K1 course: start screen, the five practice steps, completion screen with the
    // tightening/check guidance and the retry button. Presentation only: it starts the runner from the start button, shows
    // the runner's current step and completion together with read-only equipment state (preparing, lid closed again, released
    // from the face, lost tracking), and on retry asks K1TrainingLink for the reset and then shows the start screen again.
    // It never changes equipment state or training facts itself, and has no confirm, next, timer or auto close. Retry never
    // starts a run; the start button does. The board stays where the scene puts it; it never follows the camera.
    // Text comes only from TrainingSubtitles. Lives outside the three panels so hiding one never disables it. Both buttons
    // are subscribed in code only; keep their Inspector OnClick lists empty.
    public sealed class K1TrainingGuide : MonoBehaviour
    {
        [SerializeField] TrainingRunner runner;
        [SerializeField] K1TrainingLink link;
        [SerializeField] TMP_FontAsset koreanFont;
        [Tooltip("Training field only: the station that approves the start (ITrainingStartGate; one course at a time). Empty in the tutorial scene.")]
        [SerializeField] UnityEngine.Object startGate;

        [Header("Start screen")]
        [SerializeField] GameObject startPanel;
        [SerializeField] TMP_Text startTitleText;
        [SerializeField] TMP_Text startDescriptionText;
        [Tooltip("Ready line of the start screen (whether the start button can be used now). Optional.")]
        [SerializeField] TMP_Text startReadyText;
        [SerializeField] Button startButton;
        [SerializeField] TMP_Text startButtonText;

        [Header("Progress screen")]
        [SerializeField] GameObject progressPanel;
        [SerializeField] TMP_Text courseText;
        [SerializeField] TMP_Text stepCountText;
        [Tooltip("One display-only segment per practice step, in order. Not buttons.")]
        [SerializeField] Image[] progressSegments = Array.Empty<Image>();
        [SerializeField] TMP_Text stepTitleText;
        [Tooltip("Purpose line of the current step, above the instruction. Optional.")]
        [SerializeField] TMP_Text purposeText;
        [SerializeField] TMP_Text instructionText;
        [SerializeField] TMP_Text hintText;

        [Header("Completion screen")]
        [SerializeField] GameObject completePanel;
        [SerializeField] TMP_Text completeTitleText;
        [SerializeField] TMP_Text completeBodyText;
        [Tooltip("Small source line under the guidance body.")]
        [SerializeField] TMP_Text completeSourceText;
        [Tooltip("Retry: resets the equipment and the run, then shows the start screen. Usable only on the completion screen.")]
        [SerializeField] Button retryButton;
        [SerializeField] TMP_Text retryButtonText;

        [Header("Equipment markers (B guide outlines, not equipment)")]
        [SerializeField] GameObject closureMarker;
        [SerializeField] GameObject flapMarker;
        [Tooltip("Ring at the mask pickup point and the up chevron above it.")]
        [SerializeField] GameObject[] extractMarkers = Array.Empty<GameObject>();
        [Tooltip("Rings at both strap points.")]
        [SerializeField] GameObject[] strapMarkers = Array.Empty<GameObject>();

        [Header("Step segments (course board colours: TrainingBoardStyle)")]
        [Tooltip("Height (canvas units) of the current step's segment; the others keep their own height.")]
        [SerializeField] float currentSegmentHeight = 14f;

        Button subscribedButton;
        Button subscribedRetryButton;
        int shownStep = -1;
        float segmentHeight;

        public K1TrainingGuideState State { get; private set; } = K1TrainingGuideState.NotInitialized;
        public TrainingRunner Runner => runner;
        public K1TrainingLink Link => link;

        void Awake()
        {
            if (!TryValidate(out var error))
            {
                Fault(error);
                return;
            }

            segmentHeight = progressSegments[0].rectTransform.sizeDelta.y;
            ApplyText(startTitleText, TrainingSubtitles.CourseTitle);
            ApplyText(startDescriptionText, TrainingSubtitles.StartDescription);
            if (startReadyText != null)
                ApplyText(startReadyText, string.Empty);
            ApplyText(startButtonText, TrainingSubtitles.StartButton);
            ApplyText(courseText, TrainingSubtitles.CourseTitle);
            ApplyText(stepCountText, string.Empty);
            ApplyText(stepTitleText, string.Empty);
            if (purposeText != null)
                ApplyText(purposeText, string.Empty);
            ApplyText(instructionText, string.Empty);
            ApplyText(hintText, string.Empty);
            ApplyText(completeTitleText, TrainingSubtitles.CompleteTitle);
            ApplyText(completeBodyText, TrainingSubtitles.CompleteBody);
            ApplyText(completeSourceText, TrainingSubtitles.CompleteSource);
            ApplyText(retryButtonText, TrainingSubtitles.RetryButton);
            ReportMissingGlyphs();
            ShowStartScreen();
        }

        // Start screen with the progress display cleared; also the end of a successful retry.
        void ShowStartScreen()
        {
            shownStep = -1;
            SetText(stepCountText, string.Empty);
            SetText(stepTitleText, string.Empty);
            if (purposeText != null)
                SetText(purposeText, string.Empty);
            SetText(instructionText, string.Empty);
            SetText(hintText, string.Empty);
            foreach (var segment in progressSegments)
            {
                segment.color = TrainingBoardStyle.StepAhead;
                var size = segment.rectTransform.sizeDelta;
                segment.rectTransform.sizeDelta = new Vector2(size.x, segmentHeight);
            }

            ShowMarkers(-1, false);
            retryButton.interactable = false;
            completePanel.SetActive(false);
            progressPanel.SetActive(false);
            startPanel.SetActive(true);
            if (startReadyText != null)
                SetText(startReadyText, TrainingSubtitles.StartReady);
            startButton.interactable = true;
            State = K1TrainingGuideState.WaitingForStart;
        }

        void OnEnable()
        {
            if (subscribedButton == null && startButton != null)
            {
                subscribedButton = startButton;
                subscribedButton.onClick.AddListener(OnStartClicked);
            }

            if (subscribedRetryButton == null && retryButton != null)
            {
                subscribedRetryButton = retryButton;
                subscribedRetryButton.onClick.AddListener(OnRetryClicked);
            }
        }

        void OnDisable()
        {
            if (subscribedButton != null)
                subscribedButton.onClick.RemoveListener(OnStartClicked);
            subscribedButton = null;
            if (subscribedRetryButton != null)
                subscribedRetryButton.onClick.RemoveListener(OnRetryClicked);
            subscribedRetryButton = null;
        }

        // Only reachable through the button. Repeated clicks, or a click after the board changed, find another state.
        void OnStartClicked()
        {
            if (State != K1TrainingGuideState.WaitingForStart)
                return;
            // Training field: another course owns the field; nothing has changed here, the start screen stays.
            if (!TrainingStartGate.TryClaim(startGate))
                return;

            runner.StartTraining();
            if (runner.Status != TrainingStatus.Running)
            {
                // The runner logged why; the start screen and the input lock stay.
                TrainingStartGate.Cancel(startGate);
                Debug.LogError("[K1Training] Training did not start; equipment input stays locked.", this);
                return;
            }

            State = K1TrainingGuideState.Running;
            startButton.interactable = false;
            startPanel.SetActive(false);
            progressPanel.SetActive(true);
            Refresh();
        }

        // Only from the completion screen. The state leaves Completed before the reset, so repeated clicks do nothing; the
        // start screen appears only after the link reports a successful reset, and the run starts only from the start button.
        void OnRetryClicked()
        {
            if (State != K1TrainingGuideState.Completed)
                return;

            ResetToStart("Retry");
        }

        // Stop entry point of a training field (its confirm dialog): the same reset as the retry, for a run that is still running
        // or completed. The start button starts the next run.
        public bool TryAbort()
        {
            if (State != K1TrainingGuideState.Running && State != K1TrainingGuideState.Completed)
            {
                Debug.LogWarning($"[K1Training] Stop ignored in state {State}.", this);
                return false;
            }
            return ResetToStart("Stop");
        }

        bool ResetToStart(string reason)
        {
            State = K1TrainingGuideState.NotInitialized;
            retryButton.interactable = false;
            if (!link.TryResetForNewRun(out var error))
            {
                Fault(reason + " reset failed: " + error);
                return false;
            }

            ShowStartScreen();
            return true;
        }

        void Update()
        {
            if (State == K1TrainingGuideState.Running)
                Refresh();
        }

        // Follows the runner's step and status; device state only picks the text for the current step.
        void Refresh()
        {
            if (runner.Status == TrainingStatus.Completed)
            {
                ShowCompletion();
                return;
            }

            if (runner.Status != TrainingStatus.Running)
                return;

            var step = K1TrainingIds.StepIndex(runner.CurrentStepId);
            if (step < 0)
            {
                Fault($"Runner step '{runner.CurrentStepId}' is not a K1 practice step.");
                return;
            }

            if (step != shownStep)
            {
                shownStep = step;
                SetText(stepCountText, $"{step + 1} / {K1TrainingIds.Steps.Length}");
                SetText(stepTitleText, StepTitle(step));
                if (purposeText != null)
                    SetText(purposeText, StepPurpose(step));
                for (var i = 0; i < progressSegments.Length; i++)
                {
                    progressSegments[i].color = TrainingBoardStyle.StepColor(i, step);
                    var size = progressSegments[i].rectTransform.sizeDelta;
                    progressSegments[i].rectTransform.sizeDelta = new Vector2(size.x, i == step ? currentSegmentHeight : segmentHeight);
                }
            }

            var carrier = link.Carrier;
            var extraction = link.Extraction;
            var wear = link.Wear;
            string instruction;
            var hint = string.Empty;
            var strapGuide = false;
            switch (step)
            {
                case 0:
                    instruction = TrainingSubtitles.ReleaseClosureInstruction;
                    break;
                case 1:
                    instruction = TrainingSubtitles.OpenLidInstruction;
                    if (carrier.IsFlapHandUntracked)
                        hint = TrainingSubtitles.TrackingLostHint;
                    break;
                case 2:
                    instruction = TrainingSubtitles.ExtractMaskInstruction;
                    // Whether extraction is allowed stays the equipment's decision; this only explains it.
                    if (!extraction.CanGrab)
                        hint = TrainingSubtitles.ReopenLidHint;
                    else if (extraction.IsHoldingHandUntracked)
                        hint = TrainingSubtitles.TrackingLostHint;
                    break;
                case 3:
                    instruction = TrainingSubtitles.BringToFaceInstruction;
                    if (extraction.IsHoldingHandUntracked || wear.IsWaitingForTracking)
                        hint = TrainingSubtitles.TrackingLostHint;
                    else if (wear.IsSnapRearming)
                        hint = TrainingSubtitles.FaceAwayFirstHint;
                    break;
                default:
                    // Released before Worn: bring it back; the snap returns to the strap guidance without repeating steps.
                    if (!wear.ControlsBody)
                        instruction = TrainingSubtitles.BringBackInstruction;
                    else if (wear.IsPreparing)
                        instruction = TrainingSubtitles.PreparingInstruction;
                    else
                    {
                        instruction = TrainingSubtitles.SeatStrapsInstruction;
                        strapGuide = true;
                    }

                    if (wear.IsWaitingForTracking || (!wear.ControlsBody && extraction.IsHoldingHandUntracked))
                        hint = TrainingSubtitles.TrackingLostHint;
                    else if (wear.IsSnapRearming)
                        hint = TrainingSubtitles.FaceAwayFirstHint;
                    else if (wear.IsStrapRearming)
                        hint = TrainingSubtitles.StrapAgainHint;
                    break;
            }

            SetText(instructionText, instruction);
            SetText(hintText, hint);
            ShowMarkers(step, strapGuide);
        }

        // Shown right when the runner completes and kept; later equipment changes (such as taking the mask off) change nothing.
        void ShowCompletion()
        {
            State = K1TrainingGuideState.Completed;
            progressPanel.SetActive(false);
            completePanel.SetActive(true);
            ShowMarkers(-1, false);
            retryButton.interactable = true;
        }

        static string StepTitle(int step)
        {
            switch (step)
            {
                case 0: return TrainingSubtitles.ReleaseClosureTitle;
                case 1: return TrainingSubtitles.OpenLidTitle;
                case 2: return TrainingSubtitles.ExtractMaskTitle;
                case 3: return TrainingSubtitles.BringToFaceTitle;
                default: return TrainingSubtitles.SeatStrapsTitle;
            }
        }

        static string StepPurpose(int step)
        {
            switch (step)
            {
                case 0: return TrainingSubtitles.ReleaseClosurePurpose;
                case 1: return TrainingSubtitles.OpenLidPurpose;
                case 2: return TrainingSubtitles.ExtractMaskPurpose;
                case 3: return TrainingSubtitles.BringToFacePurpose;
                default: return TrainingSubtitles.SeatStrapsPurpose;
            }
        }

        // Only the current step's markers are on; the face step has none.
        void ShowMarkers(int step, bool strapGuide)
        {
            SetActive(closureMarker, step == 0);
            SetActive(flapMarker, step == 1);
            foreach (var marker in extractMarkers)
                SetActive(marker, step == 2);
            foreach (var marker in strapMarkers)
                SetActive(marker, step == 4 && strapGuide);
        }

        static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
                target.SetActive(active);
        }

        static void SetText(TMP_Text target, string value)
        {
            if (target.text != value)
                target.text = value;
        }

        public bool TryValidate(out string error)
        {
            var missing = new List<string>();
            Require(runner, nameof(runner), missing);
            Require(link, nameof(link), missing);
            Require(koreanFont, nameof(koreanFont), missing);
            Require(startPanel, nameof(startPanel), missing);
            Require(startTitleText, nameof(startTitleText), missing);
            Require(startDescriptionText, nameof(startDescriptionText), missing);
            Require(startButton, nameof(startButton), missing);
            Require(startButtonText, nameof(startButtonText), missing);
            Require(progressPanel, nameof(progressPanel), missing);
            Require(courseText, nameof(courseText), missing);
            Require(stepCountText, nameof(stepCountText), missing);
            Require(stepTitleText, nameof(stepTitleText), missing);
            Require(instructionText, nameof(instructionText), missing);
            Require(hintText, nameof(hintText), missing);
            Require(completePanel, nameof(completePanel), missing);
            Require(completeTitleText, nameof(completeTitleText), missing);
            Require(completeBodyText, nameof(completeBodyText), missing);
            Require(completeSourceText, nameof(completeSourceText), missing);
            Require(retryButton, nameof(retryButton), missing);
            Require(retryButtonText, nameof(retryButtonText), missing);
            Require(closureMarker, nameof(closureMarker), missing);
            Require(flapMarker, nameof(flapMarker), missing);
            if (progressSegments == null || progressSegments.Length != K1TrainingIds.Steps.Length || Array.IndexOf(progressSegments, null) >= 0)
                missing.Add($"{nameof(progressSegments)} (exactly {K1TrainingIds.Steps.Length})");
            if (extractMarkers == null || extractMarkers.Length == 0 || Array.IndexOf(extractMarkers, null) >= 0)
                missing.Add(nameof(extractMarkers));
            if (strapMarkers == null || strapMarkers.Length != 2 || Array.IndexOf(strapMarkers, null) >= 0)
                missing.Add($"{nameof(strapMarkers)} (exactly 2)");

            if (missing.Count > 0)
            {
                error = $"K1TrainingGuide '{name}': missing references: {string.Join(", ", missing)}.";
                return false;
            }

            if (!link.TryValidate(out error))
                return false;

            var panels = new[] { startPanel.transform, progressPanel.transform, completePanel.transform };
            foreach (var panel in panels)
            {
                if (transform.IsChildOf(panel))
                {
                    error = $"K1TrainingGuide must not be inside {panel.name}.";
                    return false;
                }
            }

            if (!startButton.transform.IsChildOf(startPanel.transform) || !startButtonText.transform.IsChildOf(startButton.transform))
            {
                error = "startButton must be inside startPanel and startButtonText inside startButton.";
                return false;
            }

            if (!retryButton.transform.IsChildOf(completePanel.transform) || !retryButtonText.transform.IsChildOf(retryButton.transform))
            {
                error = "retryButton must be inside completePanel and retryButtonText inside retryButton.";
                return false;
            }

            // A fast second click on retry must not land on the start button the retry reveals.
            if (WorldBounds((RectTransform)retryButton.transform).Intersects(WorldBounds((RectTransform)startButton.transform)))
            {
                error = "retryButton must not overlap startButton.";
                return false;
            }

            // The start and retry buttons are the board's only controls; progress segments and texts never take rays.
            foreach (var panel in panels)
            {
                foreach (var graphic in panel.GetComponentsInChildren<Graphic>(true))
                {
                    if (graphic.raycastTarget && graphic.gameObject != startButton.gameObject && graphic.gameObject != retryButton.gameObject)
                    {
                        error = $"Guide graphic '{graphic.name}' must not be a raycast target.";
                        return false;
                    }
                }

                foreach (var selectable in panel.GetComponentsInChildren<Selectable>(true))
                {
                    if (selectable != startButton && selectable != retryButton)
                    {
                        error = $"'{selectable.name}' is a control; the board has only the start and retry buttons.";
                        return false;
                    }
                }
            }

            error = null;
            return true;
        }

        static Bounds WorldBounds(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var bounds = new Bounds(corners[0], Vector3.zero);
            for (var i = 1; i < corners.Length; i++)
                bounds.Encapsulate(corners[i]);
            return bounds;
        }

        static void Require(Object reference, string fieldName, List<string> missing)
        {
            if (reference == null)
                missing.Add(fieldName);
        }

        void ApplyText(TMP_Text target, string value)
        {
            target.font = koreanFont;
            target.text = value;
        }

        void ReportMissingGlyphs()
        {
            if (koreanFont.HasCharacters(TrainingSubtitles.All, out uint[] missing, true, true))
                return;

            var characters = new List<string>();
            if (missing != null)
            {
                foreach (var codePoint in missing)
                {
                    var character = char.ConvertFromUtf32((int)codePoint);
                    if (!char.IsControl(character, 0))
                        characters.Add(character);
                }

                if (missing.Length > 0 && characters.Count == 0)
                    return;
            }

            Debug.LogError($"[K1Training] Font '{koreanFont.name}' is missing glyphs: '{string.Concat(characters)}'.", koreanFont);
        }

        void Fault(string error)
        {
            State = K1TrainingGuideState.Faulted;
            if (startButton != null)
                startButton.interactable = false;
            if (startReadyText != null && koreanFont != null)
                ApplyText(startReadyText, TrainingSubtitles.StartUnavailable);
            if (retryButton != null)
                retryButton.interactable = false;
            ShowMarkers(-1, false);
            Debug.LogError($"[K1Training] Guide blocked: {error}", this);
        }
    }
}
