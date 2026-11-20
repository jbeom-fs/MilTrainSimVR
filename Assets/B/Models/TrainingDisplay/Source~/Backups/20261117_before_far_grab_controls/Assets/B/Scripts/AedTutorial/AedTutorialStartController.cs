using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MilTrainSimVR.B.AedTutorial
{
    public enum AedTutorialStartState
    {
        NotInitialized = 0,
        WaitingForStart = 1,
        AwaitingPowerButton = 2,
        Faulted = 3,
        AwaitingPadPickup = 4,
        GuidingPadPlacement = 5,
        ModuleCompleted = 6,
        AwaitingHandsOff = 7,
        AnalyzingRhythm = 8,
        AnalysisCompleteAwaitingNext = 9,
        InterruptedReloadRequired = 10,
        ChargingShock = 11,
        AwaitingShockButton = 12,
        ShockDeliveredAwaitingNext = 13
    }

    // Which anchor a pad attaches to. The anchor's acceptedPadId must match the pad's ID.
    [Serializable]
    public struct PadGuide
    {
        public AedPad pad;
        public PadAttachAnchor anchor;
    }

    // Lives outside both panels so hiding StartPanel never disables it.
    // Owns tutorial progression; AedPowerDevice owns the power state, AedPad the grab/attach state, PadAttachAnchor the attach check,
    // PatientContactMonitor the hands-off check, AedShockDevice the charge and shock button.
    // Buttons and device events are subscribed in code only; keep Inspector OnClick lists empty.
    public sealed class AedTutorialStartController : MonoBehaviour
    {
        [SerializeField] GameObject startPanel;
        [SerializeField] GameObject guidePanel;
        [SerializeField] Button startButton;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text goalText;
        [SerializeField] TMP_Text startButtonText;
        [SerializeField] TMP_Text instructionText;
        [SerializeField] TMP_FontAsset koreanFont;
        [SerializeField] AedPowerDevice powerDevice;
        [SerializeField] GameObject powerButtonHighlight;
        [SerializeField] PadGuide[] padGuides = Array.Empty<PadGuide>();
        [SerializeField] PatientContactMonitor contactMonitor;
        [Tooltip("Training value, not a device or medical standard: seconds of continuous hands-off needed to finish rhythm analysis.")]
        [SerializeField] float rhythmAnalysisSeconds = 5f;
        [SerializeField] AedShockDevice shockDevice;
        [Tooltip("Shown only after the shock; resets the equipment in this scene and returns to the start screen.")]
        [SerializeField] Button retryButton;
        [SerializeField] TMP_Text retryButtonText;
        [Tooltip("Training field only: the station that approves the start (ITrainingStartGate; one course at a time). Empty in the tutorial scene.")]
        [SerializeField] Object startGate;
        Button subscribedButton;
        Button subscribedRetryButton;
        AedPowerDevice subscribedDevice;
        AedShockDevice subscribedShockDevice;
        readonly List<AedPad> subscribedPads = new List<AedPad>();
        bool attachReady;
        bool analysisTouchedPatient;
        bool disabledMidRun;
        const int RequiredPads = 2;

        public AedTutorialStartState State { get; private set; } = AedTutorialStartState.NotInitialized;
        public AedPad CurrentPad { get; private set; }
        public string CurrentPadId => CurrentPad != null ? CurrentPad.PadId : null;
        // Refreshed every frame after pad placement; display and logs only. Decisions re-evaluate the monitor at that moment.
        public PatientContactState PatientContact { get; private set; } = PatientContactState.Unavailable;
        public float AnalysisElapsedSeconds { get; private set; }

        void Awake()
        {
            if (!TryValidate(out var error))
            {
                Fault(error);
                return;
            }

            ApplyText(titleText, TrainingSubtitles.StartTitle);
            ApplyText(goalText, TrainingSubtitles.StartGoal);
            ApplyText(startButtonText, TrainingSubtitles.StartButton);
            ApplyText(instructionText, TrainingSubtitles.PowerButtonInstruction);
            ApplyText(retryButtonText, TrainingSubtitles.RetryButton);
            ReportMissingGlyphs();
            ShowStartScreen();
        }

        // Start screen with every equipment input locked; also the end of a retry.
        void ShowStartScreen()
        {
            instructionText.text = TrainingSubtitles.PowerButtonInstruction;
            guidePanel.SetActive(false);
            SetRetryVisible(false);
            powerButtonHighlight.SetActive(false);
            powerDevice.SetInputEnabled(false);
            SetPadsSelectable(false);
            foreach (var guide in padGuides)
            {
                guide.anchor.ShowMarker(false);
                guide.anchor.ApplyAttachedDisplay();
            }

            startPanel.SetActive(true);
            startButton.interactable = true;
            State = AedTutorialStartState.WaitingForStart;
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

            if (subscribedDevice == null && powerDevice != null)
            {
                subscribedDevice = powerDevice;
                subscribedDevice.PowerChanged += OnPowerChanged;
            }

            if (subscribedPads.Count == 0 && padGuides != null)
            {
                foreach (var guide in padGuides)
                {
                    if (guide.pad == null || subscribedPads.Contains(guide.pad))
                        continue;

                    guide.pad.Grabbed += OnPadGrabbed;
                    guide.pad.Released += OnPadReleased;
                    subscribedPads.Add(guide.pad);
                }
            }

            if (subscribedShockDevice == null && shockDevice != null)
            {
                subscribedShockDevice = shockDevice;
                subscribedShockDevice.ShockPressed += OnShockPressed;
            }

            // Analysis, charge and shock never resume after a mid-run disable; only a scene reload starts over.
            if (disabledMidRun)
            {
                RequireSceneReload();
                return;
            }

            // Catch changes that happened while this controller was disabled.
            TryAdvanceAfterPowerOn();
            ReconcilePads();
        }

        // Analysis, charge and shock progress, or the attach preview; attachment itself happens on a real release.
        void Update()
        {
            if (Paused)
            {
                LockLoosePadsWhilePaused();
                return;
            }

            if (IsMonitoredPhase(State))
            {
                UpdateMonitoredPhase(Time.deltaTime);
                return;
            }

            if (State != AedTutorialStartState.GuidingPadPlacement || CurrentPad == null || !CurrentPad.IsGrabbed)
                return;

            SetAttachReady(AnchorFor(CurrentPad).Evaluate(CurrentPad) == PadAttachCheck.Valid);
        }

        void OnDisable()
        {
            // Analysis runs only in Update and the charge is dropped here, so no pending completion or shock remains.
            if (IsMonitoredPhase(State))
            {
                disabledMidRun = true;
                AnalysisElapsedSeconds = 0f;
                if (shockDevice != null)
                    shockDevice.Interrupt();
            }

            if (subscribedShockDevice != null)
                subscribedShockDevice.ShockPressed -= OnShockPressed;
            subscribedShockDevice = null;

            if (subscribedButton != null)
                subscribedButton.onClick.RemoveListener(OnStartClicked);
            subscribedButton = null;

            if (subscribedRetryButton != null)
                subscribedRetryButton.onClick.RemoveListener(OnRetryClicked);
            subscribedRetryButton = null;

            if (subscribedDevice != null)
                subscribedDevice.PowerChanged -= OnPowerChanged;
            subscribedDevice = null;

            foreach (var pad in subscribedPads)
            {
                if (pad == null)
                    continue;

                pad.Grabbed -= OnPadGrabbed;
                pad.Released -= OnPadReleased;
            }

            subscribedPads.Clear();
        }

        // Only reachable through the button; repeated or faulted clicks are ignored.
        void OnStartClicked()
        {
            if (State != AedTutorialStartState.WaitingForStart)
                return;

            // Training field: another course owns the field; nothing has changed here, the start screen stays.
            if (!MilTrainSimVR.B.Training.TrainingStartGate.TryClaim(startGate))
                return;

            if (!TryValidate(out var error))
            {
                MilTrainSimVR.B.Training.TrainingStartGate.Cancel(startGate);
                Fault(error);
                return;
            }

            State = AedTutorialStartState.AwaitingPowerButton;
            startButton.interactable = false;
            startPanel.SetActive(false);
            guidePanel.SetActive(true);
            powerButtonHighlight.SetActive(true);
            powerDevice.SetInputEnabled(true);
        }

        void OnPowerChanged(bool isOn) => TryAdvanceAfterPowerOn();

        void TryAdvanceAfterPowerOn()
        {
            if (State != AedTutorialStartState.AwaitingPowerButton || powerDevice == null || !powerDevice.IsOn)
                return;

            if (!TryValidate(out var error))
            {
                Fault(error);
                return;
            }

            powerDevice.SetInputEnabled(false);
            powerButtonHighlight.SetActive(false);
            EnterPadPickup(TrainingSubtitles.PadPickupInstruction);
            SetPadsSelectable(true);
        }

        void OnPadGrabbed(AedPad pad)
        {
            // One guided pad at a time; a second pad grabbed meanwhile is ignored until the current one is released. While paused
            // nothing changes; SetPaused(false) picks a pad still in hand up again.
            if (Paused || State != AedTutorialStartState.AwaitingPadPickup || !pad.IsGrabbed || pad.IsAttached)
                return;

            if (!TryValidate(out var error))
            {
                Fault(error);
                return;
            }

            CurrentPad = pad;
            foreach (var guide in padGuides)
                guide.pad.SetHighlight(false);
            attachReady = false;
            AnchorFor(pad).ShowMarker(true);
            instructionText.text = TrainingSubtitles.PadPlacementInstruction;
            State = AedTutorialStartState.GuidingPadPlacement;
        }

        void OnPadReleased(AedPad pad, bool userRelease)
        {
            if (State != AedTutorialStartState.GuidingPadPlacement || pad != CurrentPad)
                return;

            var anchor = AnchorFor(pad);
            anchor.ShowMarker(false);
            attachReady = false;
            CurrentPad = null;

            // Re-check at the moment of a real release; earlier previews never decide attachment. A release while paused (stop
            // confirm open) is never judged: no attachment, the pad is picked up again after the dialog.
            var judged = userRelease && !Paused;
            var check = judged ? anchor.Evaluate(pad) : PadAttachCheck.TooFar;
            if (judged && check == PadAttachCheck.Valid && pad.TryAttach(anchor))
            {
                OnPadAttached();
                return;
            }

            EnterPadPickup(judged ? FailureInstruction(pad, anchor, check) : TrainingSubtitles.PadRepickInstruction);
            if (Paused)
                return;

            // Another pad may already be in hand.
            ReconcilePads();
        }

        string FailureInstruction(AedPad pad, PadAttachAnchor anchor, PadAttachCheck check)
        {
            foreach (var guide in padGuides)
            {
                if (guide.anchor != anchor && guide.anchor.DistanceTo(pad) <= guide.anchor.PositionToleranceMeters)
                    return TrainingSubtitles.AttachWrongAnchorInstruction;
            }

            switch (check)
            {
                case PadAttachCheck.WrongFacing:
                    return TrainingSubtitles.AttachFacingInstruction;
                case PadAttachCheck.WrongTwist:
                    return TrainingSubtitles.AttachTwistInstruction;
                case PadAttachCheck.Occupied:
                    return TrainingSubtitles.AttachOccupiedInstruction;
                case PadAttachCheck.TooFar when anchor.DistanceTo(pad) <= anchor.PositionToleranceMeters * 3f:
                    return TrainingSubtitles.AttachPositionInstruction;
                default:
                    return TrainingSubtitles.PadRepickInstruction;
            }
        }

        void OnPadAttached()
        {
            if (AllPadsAttached())
            {
                EnterHandsOff();
                return;
            }

            EnterPadPickup(TrainingSubtitles.RemainingPadInstruction);
            // The remaining pad may already be held in the other hand.
            ReconcilePads();
        }

        // Completion uses the current device and pad states, not event counts.
        bool AllPadsAttached()
        {
            if (!powerDevice.IsOn)
                return false;

            foreach (var guide in padGuides)
            {
                if (!guide.pad.IsAttached || guide.pad.AttachedAnchor != guide.anchor || guide.anchor.AttachedPad != guide.pad)
                    return false;
            }

            return true;
        }

        void EnterHandsOff()
        {
            foreach (var guide in padGuides)
            {
                guide.pad.SetHighlight(false);
                guide.pad.SetGrabEnabled(false);
                guide.anchor.ShowMarker(false);
            }

            AnalysisElapsedSeconds = 0f;
            analysisTouchedPatient = false;
            PatientContact = PatientContactState.Unavailable;
            instructionText.text = TrainingSubtitles.HandsOffInstruction;
            State = AedTutorialStartState.AwaitingHandsOff;
        }

        // Every state after both pads are attached: device, pads and contact are re-read each frame.
        static bool IsMonitoredPhase(AedTutorialStartState state) =>
            state == AedTutorialStartState.AwaitingHandsOff || state == AedTutorialStartState.AnalyzingRhythm ||
            IsShockPhase(state);

        // States that rely on the shock device; it must stay enabled and uninterrupted.
        static bool IsShockPhase(AedTutorialStartState state) =>
            state == AedTutorialStartState.AnalysisCompleteAwaitingNext || state == AedTutorialStartState.ChargingShock ||
            state == AedTutorialStartState.AwaitingShockButton || state == AedTutorialStartState.ShockDeliveredAwaitingNext;

        // Each state sets only its own instruction, so an earlier phase never overwrites a later one.
        void UpdateMonitoredPhase(float deltaTime)
        {
            if (!AllPadsAttached())
            {
                Fault("AED power or pad attachment is no longer valid after pad placement.");
                return;
            }

            if (IsShockPhase(State) && (!shockDevice.isActiveAndEnabled || shockDevice.State == AedShockState.Interrupted))
            {
                RequireSceneReload();
                return;
            }

            var contact = EvaluateContact();
            switch (State)
            {
                case AedTutorialStartState.AwaitingHandsOff:
                    if (contact == PatientContactState.Clear)
                        StartRhythmAnalysis();
                    else
                        SetInstruction(contact == PatientContactState.Unavailable ? TrainingSubtitles.ContactUnavailableInstruction
                            : analysisTouchedPatient ? TrainingSubtitles.AnalysisRestartInstruction : TrainingSubtitles.HandsOffInstruction);
                    break;

                case AedTutorialStartState.AnalyzingRhythm:
                    if (contact != PatientContactState.Clear)
                    {
                        ResetRhythmAnalysis(contact);
                        break;
                    }

                    AnalysisElapsedSeconds += deltaTime;
                    if (AnalysisElapsedSeconds >= rhythmAnalysisSeconds)
                        CompleteRhythmAnalysis(contact);
                    break;

                case AedTutorialStartState.AnalysisCompleteAwaitingNext:
                    TryStartCharging(contact);
                    break;

                // Contact or lost tracking does not reset the charge; the button stays locked until charged and clear.
                case AedTutorialStartState.ChargingShock:
                    if (shockDevice.State == AedShockState.Charged)
                    {
                        State = AedTutorialStartState.AwaitingShockButton;
                        UpdateShockButton(contact);
                    }
                    else
                        SetInstruction(contact == PatientContactState.Unavailable
                            ? ContactUnavailableShockInstruction()
                            : TrainingSubtitles.ShockChargingInstruction);
                    break;

                case AedTutorialStartState.AwaitingShockButton:
                    UpdateShockButton(contact);
                    break;
            }
        }

        PatientContactState EvaluateContact()
        {
            var contact = contactMonitor.Evaluate();
            PatientContact = contact;
            return contact;
        }

        void StartRhythmAnalysis()
        {
            AnalysisElapsedSeconds = 0f;
            SetInstruction(TrainingSubtitles.RhythmAnalyzingInstruction);
            State = AedTutorialStartState.AnalyzingRhythm;
        }

        // Any touch or lost check discards the run; the next hands-off starts a full analysis from zero.
        void ResetRhythmAnalysis(PatientContactState contact)
        {
            AnalysisElapsedSeconds = 0f;
            if (contact == PatientContactState.Touching)
                analysisTouchedPatient = true;
            SetInstruction(contact == PatientContactState.Touching
                ? TrainingSubtitles.AnalysisRestartInstruction
                : TrainingSubtitles.ContactUnavailableInstruction);
            State = AedTutorialStartState.AwaitingHandsOff;
        }

        // The training scenario is fixed to shock advised; no real rhythm is analyzed.
        void CompleteRhythmAnalysis(PatientContactState contact)
        {
            State = AedTutorialStartState.AnalysisCompleteAwaitingNext;
            TryStartCharging(contact);
        }

        // Entered once from the analysis-complete state; waits there while the hands are not validly clear.
        void TryStartCharging(PatientContactState contact)
        {
            if (contact != PatientContactState.Clear)
            {
                SetInstruction(contact == PatientContactState.Touching
                    ? TrainingSubtitles.ShockAdvisedHandsOffInstruction
                    : ContactUnavailableShockInstruction());
                return;
            }

            if (!shockDevice.BeginCharge())
            {
                Fault("Shock device could not start charging.");
                return;
            }

            SetInstruction(TrainingSubtitles.ShockChargingInstruction);
            State = AedTutorialStartState.ChargingShock;
        }

        // Input is enabled only while charged and validly clear; any change forgets presses already started.
        void UpdateShockButton(PatientContactState contact)
        {
            shockDevice.SetInputEnabled(contact == PatientContactState.Clear);
            SetInstruction(contact == PatientContactState.Clear ? TrainingSubtitles.ShockReadyInstruction
                : contact == PatientContactState.Touching ? TrainingSubtitles.ShockBlockedContactInstruction
                : ContactUnavailableShockInstruction());
        }

        // Decided on the state at the press itself, never on the cached contact.
        void OnShockPressed()
        {
            if (State != AedTutorialStartState.AwaitingShockButton || shockDevice.State != AedShockState.Charged)
                return;

            if (!AllPadsAttached())
            {
                Fault("AED power or pad attachment is no longer valid at the shock press.");
                return;
            }

            var contact = EvaluateContact();
            if (contact != PatientContactState.Clear)
            {
                UpdateShockButton(contact);
                return;
            }

            if (!shockDevice.TryDischarge())
                return;

            SetInstruction(TrainingSubtitles.ShockDeliveredInstruction);
            State = AedTutorialStartState.ShockDeliveredAwaitingNext;
            SetRetryVisible(true);
        }

        // A separate new run, not a repeated care cycle. Only from the final state; repeated clicks find another state and do nothing.
        void OnRetryClicked()
        {
            if (State != AedTutorialStartState.ShockDeliveredAwaitingNext)
                return;

            ResetToStart();
        }

        // ---------- Training field: stop confirm and stop ----------

        // While a training field asks whether to stop: no phase progresses (Update stops), the power button and shock button take
        // no input, a pad lying loose cannot be grabbed and their prompts stay. A pad in hand stays in hand: its grab is never
        // switched off here (that would end the XR selection and drop it), its placement preview stops, and a release meanwhile
        // never attaches (OnPadReleased). Resuming gives each phase its own input back; a rhythm analysis under way starts again
        // from zero (its continuous hands-off time never adds the time before the dialog), a charge keeps its progress.
        public bool Paused { get; private set; }

        public void SetPaused(bool paused)
        {
            if (paused == Paused)
                return;
            Paused = paused;
            shockDevice.Paused = paused;
            if (paused)
            {
                powerDevice.SetInputEnabled(false);
                LockLoosePadsWhilePaused();
                shockDevice.SetInputEnabled(false);
                return;
            }

            switch (State)
            {
                case AedTutorialStartState.AwaitingPowerButton:
                    powerDevice.SetInputEnabled(true);
                    break;
                case AedTutorialStartState.AwaitingPadPickup:
                case AedTutorialStartState.GuidingPadPlacement:
                    SetPadsSelectable(true);
                    if (State == AedTutorialStartState.AwaitingPadPickup)
                    {
                        foreach (var guide in padGuides)
                            guide.pad.SetHighlight(true);
                    }
                    break;
                case AedTutorialStartState.AnalyzingRhythm:
                    // Back to waiting for hands off: the next clear frame starts a full analysis from zero.
                    AnalysisElapsedSeconds = 0f;
                    SetInstruction(TrainingSubtitles.HandsOffInstruction);
                    State = AedTutorialStartState.AwaitingHandsOff;
                    break;
            }
            ReconcilePads();
        }

        // Pads not in a hand take no new grab while paused; a held one keeps its grab (and its XR selection). Also run from
        // Update, so a pad let go during the dialog is locked on the next frame, outside the XRI event that released it.
        void LockLoosePadsWhilePaused()
        {
            if (padGuides == null)
                return;
            foreach (var guide in padGuides)
            {
                if (guide.pad == null || guide.pad.IsGrabbed)
                    continue;
                guide.pad.SetGrabEnabled(false);
                guide.pad.SetHighlight(false);
            }
        }

        // The run is still under way or finished (not the start screen, not resetting, not faulted).
        public bool IsRunInProgress => State != AedTutorialStartState.NotInitialized && State != AedTutorialStartState.WaitingForStart &&
                                       State != AedTutorialStartState.Faulted;

        // Stop entry point of a training field (its confirm dialog): the same reset as the retry, from any state of a started run.
        public bool TryAbort()
        {
            if (!IsRunInProgress)
            {
                Debug.LogWarning($"[AedTutorial] Stop ignored in state {State}.", this);
                return false;
            }
            Paused = false;
            shockDevice.Paused = false;
            ResetToStart();
            return true;
        }

        void ResetToStart()
        {
            // NotInitialized matches no handler or Update phase, so the old run is not evaluated and reset-time events
            // (such as a canceled pad release) are not treated as new training actions.
            State = AedTutorialStartState.NotInitialized;
            SetRetryVisible(false);

            shockDevice.ResetForNewRun();
            foreach (var guide in padGuides)
            {
                guide.pad.ResetForNewRun();
                guide.anchor.ResetForNewRun();
            }

            powerDevice.ResetForNewRun();

            CurrentPad = null;
            attachReady = false;
            analysisTouchedPatient = false;
            disabledMidRun = false;
            PatientContact = PatientContactState.Unavailable;
            AnalysisElapsedSeconds = 0f;

            // The new run starts only from a separate press of the start button.
            ShowStartScreen();
        }

        void SetRetryVisible(bool visible)
        {
            if (retryButton == null)
                return;

            retryButton.interactable = visible;
            retryButton.gameObject.SetActive(visible);
        }

        string ContactUnavailableShockInstruction() =>
            contactMonitor.IssueKind == ContactCheckIssue.HandTracking
                ? TrainingSubtitles.ShockTrackingUnavailableInstruction
                : TrainingSubtitles.ShockContactCheckUnavailableInstruction;

        void RequireSceneReload()
        {
            State = AedTutorialStartState.InterruptedReloadRequired;
            AnalysisElapsedSeconds = 0f;
            powerDevice.SetInputEnabled(false);
            SetPadsSelectable(false);
            shockDevice.Interrupt();
            SetRetryVisible(false);
            SetInstruction(TrainingSubtitles.SceneReloadRequiredInstruction);
            Debug.LogWarning("[AedTutorial] Interrupted after pad placement. Input locked; reload the scene to start over.", this);
        }

        void SetInstruction(string value)
        {
            if (instructionText.text != value)
                instructionText.text = value;
        }

        void SetAttachReady(bool ready)
        {
            if (ready == attachReady)
                return;

            attachReady = ready;
            AnchorFor(CurrentPad).ShowReady(ready);
            instructionText.text = ready ? TrainingSubtitles.AttachReadyInstruction : TrainingSubtitles.PadPlacementInstruction;
        }

        void EnterPadPickup(string instruction)
        {
            foreach (var guide in padGuides)
                guide.pad.SetHighlight(true);
            instructionText.text = instruction;
            State = AedTutorialStartState.AwaitingPadPickup;
        }

        void ReconcilePads()
        {
            // A release missed while disabled cannot be re-checked, so it never attaches.
            if (State == AedTutorialStartState.GuidingPadPlacement && !CurrentPad.IsGrabbed)
            {
                OnPadReleased(CurrentPad, false);
                return;
            }

            if (State != AedTutorialStartState.AwaitingPadPickup)
                return;

            foreach (var guide in padGuides)
            {
                if (guide.pad != null && guide.pad.IsGrabbed && !guide.pad.IsAttached)
                {
                    OnPadGrabbed(guide.pad);
                    return;
                }
            }
        }

        PadAttachAnchor AnchorFor(AedPad pad)
        {
            foreach (var guide in padGuides)
            {
                if (guide.pad == pad)
                    return guide.anchor;
            }

            return null;
        }

        void SetPadsSelectable(bool selectable)
        {
            if (padGuides == null)
                return;

            foreach (var guide in padGuides)
            {
                if (guide.pad == null)
                    continue;

                guide.pad.SetGrabEnabled(selectable);
                if (!selectable)
                    guide.pad.SetHighlight(false);
            }
        }

        bool TryValidate(out string error)
        {
            var missing = new List<string>();
            Require(startPanel, nameof(startPanel), missing);
            Require(guidePanel, nameof(guidePanel), missing);
            Require(startButton, nameof(startButton), missing);
            Require(titleText, nameof(titleText), missing);
            Require(goalText, nameof(goalText), missing);
            Require(startButtonText, nameof(startButtonText), missing);
            Require(instructionText, nameof(instructionText), missing);
            Require(koreanFont, nameof(koreanFont), missing);
            Require(powerDevice, nameof(powerDevice), missing);
            Require(powerButtonHighlight, nameof(powerButtonHighlight), missing);
            Require(contactMonitor, nameof(contactMonitor), missing);
            Require(shockDevice, nameof(shockDevice), missing);
            Require(retryButton, nameof(retryButton), missing);
            Require(retryButtonText, nameof(retryButtonText), missing);
            if (padGuides == null || padGuides.Length != RequiredPads)
                missing.Add($"{nameof(padGuides)} (exactly {RequiredPads})");
            else
            {
                for (var i = 0; i < padGuides.Length; i++)
                {
                    Require(padGuides[i].pad, $"{nameof(padGuides)}[{i}].pad", missing);
                    Require(padGuides[i].anchor, $"{nameof(padGuides)}[{i}].anchor", missing);
                }
            }

            if (missing.Count > 0)
            {
                error = $"Missing references: {string.Join(", ", missing)}.";
                return false;
            }

            if (!powerDevice.TryValidate(out error) || !contactMonitor.TryValidate(out error) || !shockDevice.TryValidate(out error))
                return false;

            if (float.IsNaN(rhythmAnalysisSeconds) || float.IsInfinity(rhythmAnalysisSeconds) || rhythmAnalysisSeconds <= 0f)
            {
                error = $"{nameof(rhythmAnalysisSeconds)} must be a positive finite value, not {rhythmAnalysisSeconds}.";
                return false;
            }

            for (var i = 0; i < padGuides.Length; i++)
            {
                if (!padGuides[i].pad.TryValidate(out error) || !padGuides[i].anchor.TryValidate(out error))
                    return false;

                for (var j = 0; j < i; j++)
                {
                    if (padGuides[j].pad == padGuides[i].pad || padGuides[j].anchor == padGuides[i].anchor ||
                        padGuides[j].pad.PadId == padGuides[i].pad.PadId)
                    {
                        error = $"{nameof(padGuides)}[{j}] and [{i}] share a pad, pad ID or anchor.";
                        return false;
                    }
                }

                if (padGuides[i].anchor.AcceptedPadId != padGuides[i].pad.PadId)
                {
                    error = $"{nameof(padGuides)}[{i}]: anchor '{padGuides[i].anchor.name}' accepts '{padGuides[i].anchor.AcceptedPadId}', not '{padGuides[i].pad.PadId}'.";
                    return false;
                }

                if (HasRaycastTarget(padGuides[i].anchor.Marker, out error) || !IsDisplayOnly(padGuides[i].anchor.AttachedDisplay, out error))
                    return false;

                for (var j = 0; j < i; j++)
                {
                    if (padGuides[j].anchor.AttachedDisplay == padGuides[i].anchor.AttachedDisplay ||
                        padGuides[j].pad.Visual == padGuides[i].pad.Visual)
                    {
                        error = $"{nameof(padGuides)}[{j}] and [{i}] share an attached display or pad visual.";
                        return false;
                    }
                }
            }

            if (HasRaycastTarget(powerButtonHighlight, out error) || HasRaycastTarget(shockDevice.Highlight.gameObject, out error))
                return false;

            // One press must reach exactly one of the two device buttons.
            var powerHotspot = (RectTransform)powerDevice.PowerButton.transform;
            var shockHotspot = (RectTransform)shockDevice.ShockButton.transform;
            if (powerHotspot == shockHotspot || powerHotspot.IsChildOf(shockHotspot) || shockHotspot.IsChildOf(powerHotspot) ||
                WorldBounds(powerHotspot).Intersects(WorldBounds(shockHotspot)))
            {
                error = "Power and shock buttons must be separate, non-overlapping hotspots.";
                return false;
            }

            var start = startPanel.transform;
            var guide = guidePanel.transform;
            if (start.IsChildOf(guide) || guide.IsChildOf(start))
                error = "startPanel and guidePanel must not contain each other.";
            else if (transform.IsChildOf(start) || transform.IsChildOf(guide))
                error = "Controller must not be inside startPanel or guidePanel.";
            else if (!startButton.transform.IsChildOf(start) || !titleText.transform.IsChildOf(start) ||
                !goalText.transform.IsChildOf(start))
                error = "startButton, titleText and goalText must be inside startPanel.";
            else if (!startButtonText.transform.IsChildOf(startButton.transform))
                error = "startButtonText must be inside startButton.";
            else if (!instructionText.transform.IsChildOf(guide))
                error = "instructionText must be inside guidePanel.";
            else if (!retryButtonText.transform.IsChildOf(retryButton.transform))
                error = "retryButtonText must be inside retryButton.";
            // A fast second retry click must not land on the start button that the retry reveals.
            else if (retryButton.transform.IsChildOf(start) ||
                WorldBounds((RectTransform)retryButton.transform).Intersects(WorldBounds((RectTransform)startButton.transform)))
                error = "retryButton must be outside startPanel and must not overlap startButton.";
            else
                error = null;

            return error == null;
        }

        // The patient's attached pad is a fixed visual: no grab, physics or collider of its own.
        static bool IsDisplayOnly(GameObject display, out string error)
        {
            error = display.GetComponentInChildren<Renderer>(true) == null
                ? $"Attached display '{display.name}' has no renderer."
                : display.GetComponentInParent<AedPad>(true) != null || display.GetComponentInChildren<Rigidbody>(true) != null ||
                  display.GetComponentInChildren<Collider>(true) != null
                    ? $"Attached display '{display.name}' must not have a pad, rigidbody or collider."
                    : null;
            return error == null;
        }

        // Guide visuals must never block XR rays.
        static bool HasRaycastTarget(GameObject visual, out string error)
        {
            foreach (var graphic in visual.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic.raycastTarget)
                {
                    error = $"Guide graphic '{graphic.name}' under '{visual.name}' must not be a raycast target.";
                    return true;
                }
            }

            error = null;
            return false;
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
                    // Line breaks are layout, not glyphs.
                    var character = char.ConvertFromUtf32((int)codePoint);
                    if (!char.IsControl(character, 0))
                        characters.Add(character);
                }

                if (missing.Length > 0 && characters.Count == 0)
                    return;
            }

            Debug.LogError($"[AedTutorial] Font '{koreanFont.name}' is missing glyphs: '{string.Concat(characters)}'.", koreanFont);
        }

        void Fault(string error)
        {
            State = AedTutorialStartState.Faulted;
            CurrentPad = null;
            attachReady = false;
            if (startButton != null)
                startButton.interactable = false;
            if (guidePanel != null)
                guidePanel.SetActive(false);
            if (powerButtonHighlight != null)
                powerButtonHighlight.SetActive(false);
            if (powerDevice != null)
                powerDevice.SetInputEnabled(false);
            if (shockDevice != null)
                shockDevice.Interrupt();
            SetRetryVisible(false);
            SetPadsSelectable(false);
            if (padGuides != null)
            {
                foreach (var guide in padGuides)
                {
                    if (guide.anchor != null)
                        guide.anchor.ShowMarker(false);
                }
            }

            Debug.LogError($"[AedTutorial] Start blocked: {error}", this);
        }
    }
}
