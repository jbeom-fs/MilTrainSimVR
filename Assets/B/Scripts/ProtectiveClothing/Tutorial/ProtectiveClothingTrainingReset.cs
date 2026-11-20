using System;
using System.Collections.Generic;
using UnityEngine;

namespace MilTrainSimVR.B.ProtectiveClothing.Tutorial
{
    // The one coordinator of the retry after a completed run (the completion panel's button) and of a training field's stop
    // (TryAbort: a started run, running or completed; the course board itself has no stop, and the equipment guide's development
    // reset is not active in the course scene). Runs in one
    // call, in this order:
    //  1. start gate locked (Resetting): no new grab, no wearing, no start; guide ring off, step row and practice texts cleared;
    //  2. runner stopped and the link forgets the run (worn states, summary), so the equipment reset and any late state of the
    //     old run never reach the next run;
    //  3. ProtectiveClothingEquipment.ResetEquipment (the equipment's own reset: selections cancelled, wear records cleared, worn
    //     meshes hidden, pickups back to the saved workbench pose, select order and arming cleared; its result is checked
    //     there and false is a failure);
    //  4. the equipment checked again: nothing worn, every pickup back, nothing selected, not faulted;
    //  5. completion cache and retry button cleared; the base hand models come back by themselves (the user body hides a
    //     base hand only while the HMD draws that hand's worn glove);
    //  6. start gate back to WaitingForStart and the start panel shown (ProtectiveClothingStartScreen). No run starts by itself.
    // No scene reload; the XR Rig, the user's place and view, the mirror, the board and the wear zones keep the user's values.
    // On any failure the gate faults (equipment locked) and the start panel shows the failure line with the start button off.
    [DisallowMultipleComponent]
    public sealed class ProtectiveClothingTrainingReset : MonoBehaviour
    {
        [SerializeField] ProtectiveClothingTrainingStart trainingStart;
        [SerializeField] ProtectiveClothingTrainingLink link;
        [SerializeField] ProtectiveClothingTrainingGuide trainingGuide;
        [SerializeField] ProtectiveClothingCompletionPanel completionPanel;
        [SerializeField] ProtectiveClothingStartScreen startScreen;

        bool valid;

        public bool IsResetting { get; private set; }
        public ProtectiveClothingTrainingLink Link => link;
        public ProtectiveClothingTrainingStart TrainingStart => trainingStart;

        void Awake()
        {
            valid = TryValidate(out var error);
            if (!valid)
                Debug.LogError($"[ProtectiveClothingTraining] {error} Retry unavailable.", this);
        }

        public bool TryRetry()
        {
            if (IsResetting)
                return false;
            if (!valid)
            {
                Debug.LogError("[ProtectiveClothingTraining] Retry refused: ProtectiveClothingTrainingReset references are invalid (see its earlier error).", this);
                return false;
            }
            var summary = link.Summary;
            if (trainingStart.State != ProtectiveClothingStartState.Started || summary == null)
            {
                Debug.LogWarning($"[ProtectiveClothingTraining] Retry ignored: no completed run of this course (gate {trainingStart.State}).", this);
                return false;
            }
            return ResetRun();
        }

        // Stop entry point of a training field (its confirm dialog): the same reset as the retry, for a started run that is still
        // running or completed. Worn parts come off through the equipment reset. The start button starts the next run.
        public bool TryAbort()
        {
            if (IsResetting || !valid || trainingStart.State != ProtectiveClothingStartState.Started)
            {
                Debug.LogWarning($"[ProtectiveClothingTraining] Stop ignored (gate {(trainingStart != null ? trainingStart.State.ToString() : "missing")}, resetting {IsResetting}, valid {valid}).", this);
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
                var equipment = link.Equipment;
                if (!equipment.ResetEquipment())
                {
                    Fail("ProtectiveClothingEquipment.ResetEquipment failed or was ignored" +
                         (equipment.IsFaulted ? ": " + equipment.FaultReason : " (see its log)."));
                    return false;
                }
                if (equipment.IsFaulted || equipment.WornCount != 0)
                {
                    Fail($"equipment not back after the reset (faulted {equipment.IsFaulted}, worn {equipment.WornCount}).");
                    return false;
                }
                foreach (var piece in equipment.Pieces)
                {
                    if (piece.Worn.activeSelf || !piece.gameObject.activeSelf || piece.Grab.isSelected)
                    {
                        Fail($"{piece.Part} not back after the reset (worn mesh shown, pickup hidden or still selected).");
                        return false;
                    }
                }
                completionPanel.ClearForReset();
                if (!trainingStart.TryReturnToWaiting(out var error))
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
            Debug.LogError($"[ProtectiveClothingTraining] Retry failed: {error} Equipment input stays locked.", this);
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
            error = missing.Count > 0 ? $"ProtectiveClothingTrainingReset '{name}' missing references: {string.Join(", ", missing)}." : null;
            if (error == null && (trainingStart.Link != link || trainingGuide.Link != link || completionPanel.Link != link ||
                                  startScreen.TrainingStart != trainingStart || completionPanel.TrainingReset != this))
                error = $"ProtectiveClothingTrainingReset '{name}': gate, guide, completion panel and start screen must share this course.";
            if (error == null && link.Equipment == null)
                error = $"ProtectiveClothingTrainingReset '{name}': the link has no equipment.";
            return error == null;
        }
    }
}
