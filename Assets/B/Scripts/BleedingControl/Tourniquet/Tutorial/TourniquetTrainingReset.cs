using System;
using System.Collections.Generic;
using UnityEngine;

namespace MilTrainSimVR.B.BleedingControl.Tourniquet.Tutorial
{
    // The one coordinator of the retry after a completed run (the completion panel's button) and of a training field's stop
    // (TryAbort: a started run, running or completed; the course board itself has no stop). Runs in one call, in this order,
    // through small entry points of each part:
    //  1. start gate locked (Resetting): no new equipment selection and no start accepted;
    //  2. operation markers off (guide);
    //  3. runner stopped and the link forgets the run (device states, input, summary), so device events of the reset are ignored;
    //  4-5. TourniquetOperation.ResetToInitial: real XRI selections of the part handles and the body grab canceled (a canceled
    //     release is never judged), then placement, body grab settings, part poses, work-copy meshes, progress, time record and
    //     its clock, issue, preview candidate and hand samples restored;
    //  6. final check and completion caches and buttons cleared;
    //  7. start gate back to WaitingForStart only when the device is ready in its initial state;
    //  8. start panel shown by TourniquetStartScreen (the sole panel switcher). No new run starts: only the start button does.
    // The runner's run id is not touched; the next StartTraining makes a new one. No scene reload; the XR Rig, the user's place
    // and view, the board's pose and the user's layout adjustments stay. On any failure the gate faults (equipment locked) and the
    // start panel shows the failure line with the start button off.
    [DisallowMultipleComponent]
    public sealed class TourniquetTrainingReset : MonoBehaviour
    {
        [SerializeField] TourniquetTrainingStart trainingStart;
        [SerializeField] TourniquetTrainingLink link;
        [SerializeField] TourniquetTrainingGuide trainingGuide;
        [SerializeField] TourniquetFinalCheck finalCheck;
        [SerializeField] TourniquetCompletionPanel completionPanel;
        [SerializeField] TourniquetStartScreen startScreen;

        bool valid;

        public bool IsResetting { get; private set; }
        public TourniquetTrainingStart TrainingStart => trainingStart;
        public TourniquetTrainingLink Link => link;
        public TourniquetTrainingGuide TrainingGuide => trainingGuide;
        public TourniquetFinalCheck FinalCheck => finalCheck;
        public TourniquetCompletionPanel CompletionPanel => completionPanel;
        public TourniquetStartScreen StartScreen => startScreen;

        void Awake()
        {
            valid = TryValidate(out var error);
            if (!valid)
                Debug.LogError($"[Tourniquet] {error} Retry unavailable.", this);
        }

        // Accepted once per completed run of this course: the gate must be Started and the link must hold that run's summary.
        public bool TryRetry()
        {
            if (IsResetting)
                return false;
            if (!valid)
            {
                Debug.LogError("[Tourniquet] Retry refused: TourniquetTrainingReset references are invalid (see its earlier error).", this);
                return false;
            }
            var summary = link.Summary;
            if (trainingStart.State != TourniquetTrainingStartState.Started || summary == null)
            {
                Debug.LogWarning($"[Tourniquet] Retry ignored: no completed run of this course (gate {trainingStart.State}).", this);
                return false;
            }
            return ResetRun();
        }

        // Stop entry point of a training field (its confirm dialog): the same reset as the retry, for a started run that is still
        // running or completed. Nothing else differs; the start button starts the next run.
        public bool TryAbort()
        {
            if (IsResetting || !valid || trainingStart.State != TourniquetTrainingStartState.Started)
            {
                Debug.LogWarning($"[Tourniquet] Stop ignored (gate {(trainingStart != null ? trainingStart.State.ToString() : "missing")}, resetting {IsResetting}, valid {valid}).", this);
                return false;
            }
            return ResetRun();
        }

        bool ResetRun()
        {
            IsResetting = true;
            try
            {
                trainingStart.LockForReset();
                trainingGuide.ClearForReset();
                link.EndRunForReset();
                link.Operation.ResetToInitial();
                if (!TryCheckDeviceReset(out var error))
                {
                    Fail(error);
                    return false;
                }
                finalCheck.ClearForReset();
                completionPanel.ClearForReset();
                if (!trainingStart.TryReturnToWaiting(out error))
                {
                    Fail(error);
                    return false;
                }
                startScreen.ReturnToStart();
                return true;
            }
            catch (Exception e)
            {
                Fail(e.Message);
                Debug.LogException(e, this);
                return false;
            }
            finally
            {
                IsResetting = false;
            }
        }

        // The device's own results after ResetToInitial; nothing is recomputed here.
        bool TryCheckDeviceReset(out string error)
        {
            var device = link.Device;
            var operation = link.Operation;
            error = device.IsAttached || device.IsHeld ? $"device not back in its initial state ({device.State})."
                : operation.IsBandPrepared || operation.WindlassTurnDegrees != 0f || operation.IsWindlassSecured || operation.IsTimeRecorded ||
                  operation.IsStrapFastened ? "part progress not cleared."
                : null;
            return error == null;
        }

        void Fail(string error)
        {
            trainingStart.FaultReset();
            trainingGuide.ClearForReset();
            startScreen.ShowResetFailed();
            Debug.LogError($"[Tourniquet] Retry failed: {error} Equipment input stays locked.", this);
        }

        public bool TryValidate(out string error)
        {
            var missing = new List<string>();
            if (trainingStart == null)
                missing.Add(nameof(trainingStart));
            if (link == null)
                missing.Add(nameof(link));
            if (trainingGuide == null)
                missing.Add(nameof(trainingGuide));
            if (finalCheck == null)
                missing.Add(nameof(finalCheck));
            if (completionPanel == null)
                missing.Add(nameof(completionPanel));
            if (startScreen == null)
                missing.Add(nameof(startScreen));

            error = missing.Count > 0 ? $"TourniquetTrainingReset '{name}' missing references: {string.Join(", ", missing)}." : null;
            if (error == null && (trainingStart.Link != link || trainingGuide.Link != link || finalCheck.Link != link || completionPanel.Link != link ||
                                  startScreen.TrainingStart != trainingStart || completionPanel.TrainingReset != this))
                error = $"TourniquetTrainingReset '{name}': start gate, guide, final check, completion panel and start screen must share this course's link and gate.";
            if (error == null && (link.Device == null || link.Operation == null))
                error = $"TourniquetTrainingReset '{name}': the link has no device or operation.";
            return error == null;
        }
    }
}
