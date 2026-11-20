using System;
using System.Collections.Generic;
using UnityEngine;

namespace MilTrainSimVR.B.BleedingControl.PressureBandage.Tutorial
{
    // The one coordinator of the retry after a completed run (the completion panel's button) and of a training field's stop
    // (TryAbort: a started run, running or completed; the course board itself has no stop). Runs in one call, in this order:
    //  1. start gate locked (Resetting): no new equipment selection, no start, no wrap start; guide markers off, check area,
    //     step row and progress line cleared;
    //  2. runner stopped and the link forgets the run (records, check inputs waiting for their frame, summary), so equipment
    //     events of the reset and any late input of the old run never reach the next run;
    //  3. PressureBandageApplication.ResetEquipment: every kit selection cancelled (a cancel is never handled as a release), parts
    //     back to the saved tray layout and visibility, cloth work meshes, wrap progress, end attach and lock, end and tape
    //     releases waiting, tape state, dressing contact time and its practice record cleared (its own checks; false is a
    //     failure);
    //  4. the equipment checked ready again (initial, unselected, no practice record);
    //  5. completion cache and retry button cleared;
    //  6. start gate back to WaitingForStart and the start panel shown (PressureBandageStartScreen). No run starts by itself.
    // No scene reload; the XR Rig, the user's place and view, the board pose and the user's layout and wound adjustments stay. On
    // any failure the gate faults (equipment locked) and the start panel shows the failure line with the start button off.
    [DisallowMultipleComponent]
    public sealed class PressureBandageTrainingReset : MonoBehaviour
    {
        [SerializeField] PressureBandageTrainingStart trainingStart;
        [SerializeField] PressureBandageTrainingLink link;
        [SerializeField] PressureBandageTrainingGuide trainingGuide;
        [SerializeField] PressureBandageCompletionPanel completionPanel;
        [SerializeField] PressureBandageStartScreen startScreen;

        bool valid;

        public bool IsResetting { get; private set; }
        public PressureBandageTrainingLink Link => link;
        public PressureBandageTrainingStart TrainingStart => trainingStart;

        void Awake()
        {
            valid = TryValidate(out var error);
            if (!valid)
                Debug.LogError($"[PressureBandageTraining] {error} Retry unavailable.", this);
        }

        public bool TryRetry()
        {
            if (IsResetting)
                return false;
            if (!valid)
            {
                Debug.LogError("[PressureBandageTraining] Retry refused: PressureBandageTrainingReset references are invalid (see its earlier error).", this);
                return false;
            }
            var summary = link.Summary;
            if (trainingStart.State != PressureTrainingStartState.Started || summary == null)
            {
                Debug.LogWarning($"[PressureBandageTraining] Retry ignored: no completed run of this course (gate {trainingStart.State}).", this);
                return false;
            }
            return ResetRun();
        }

        // Stop entry point of a training field (its confirm dialog): the same reset as the retry, for a started run that is still
        // running or completed. The start button starts the next run.
        public bool TryAbort()
        {
            if (IsResetting || !valid || trainingStart.State != PressureTrainingStartState.Started)
            {
                Debug.LogWarning($"[PressureBandageTraining] Stop ignored (gate {(trainingStart != null ? trainingStart.State.ToString() : "missing")}, resetting {IsResetting}, valid {valid}).", this);
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
                if (!link.BandageApplication.ResetEquipment())
                {
                    Fail("PressureBandageApplication.ResetEquipment failed (see its error).");
                    return false;
                }
                if (!trainingStart.TryCheckReady(out var error))
                {
                    Fail("equipment not ready after the reset: " + error);
                    return false;
                }
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

        void Fail(string error)
        {
            trainingStart.FaultReset();
            trainingGuide.ClearForReset();
            startScreen.ShowResetFailed();
            Debug.LogError($"[PressureBandageTraining] Retry failed: {error} Equipment input stays locked.", this);
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
            if (completionPanel == null)
                missing.Add(nameof(completionPanel));
            if (startScreen == null)
                missing.Add(nameof(startScreen));
            error = missing.Count > 0 ? $"PressureBandageTrainingReset '{name}' missing references: {string.Join(", ", missing)}." : null;
            if (error == null && (trainingStart.Link != link || trainingGuide.Link != link || completionPanel.Link != link ||
                                  startScreen.TrainingStart != trainingStart || completionPanel.TrainingReset != this))
                error = $"PressureBandageTrainingReset '{name}': gate, guide, completion panel and start screen must share this course.";
            if (error == null && link.BandageApplication == null)
                error = $"PressureBandageTrainingReset '{name}': the link has no bandage application.";
            return error == null;
        }
    }
}
