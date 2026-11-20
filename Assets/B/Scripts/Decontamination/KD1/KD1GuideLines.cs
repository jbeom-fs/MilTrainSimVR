using System.Collections.Generic;
using UnityEngine;

namespace MilTrainSimVR.B.Decontamination.KD1
{
    // The action and hint lines of the KD-1 handling (see KD1Guide for the order), read from KD1Equipment and the optional surface
    // practice only. Shared by the equipment scene's board and the KD-1 course board, so both say the same thing for the same state
    // and the text stays in TrainingSubtitles.
    public static class KD1GuideLines
    {
        public static bool PracticeShown(KD1SurfacePractice practice) => practice != null && practice.IsValid && practice.isActiveAndEnabled;

        public static bool AnyFitted(KD1Equipment equipment) => equipment.FittedOn(KD1Hand.Left) != null || equipment.FittedOn(KD1Hand.Right) != null;

        // Some hand wearing a pad is tracked now (the other hand's state never matters).
        public static bool FittedHandTracked(KD1Equipment equipment) =>
            equipment.FittedOn(KD1Hand.Left) != null && equipment.IsHandTracked(KD1Hand.Left) ||
            equipment.FittedOn(KD1Hand.Right) != null && equipment.IsHandTracked(KD1Hand.Right);

        public static string Action(KD1Equipment equipment, KD1SurfacePractice practice)
        {
            if (!equipment.IsValid)
                return TrainingSubtitles.SetupInvalid;
            if (equipment.IsFaulted)
                return TrainingSubtitles.ResetFailed;
            if (!equipment.isActiveAndEnabled)
                return TrainingSubtitles.EquipmentPaused;
            if (!Application.isFocused)
                return TrainingSubtitles.Paused;
            var held = equipment.GuidedItem;
            if (held != null)
            {
                if (!equipment.IsHolderTracked(held))
                    return TrainingSubtitles.Tracking;
                return HeldLine(equipment, held);
            }
            // A fitted pad is used without a selection: when no hand wearing one is tracked, no area can be touched and no pad taken off.
            // A tracked wearing hand (or the other hand's state) never blocks; records and contacts are not touched here.
            if (AnyFitted(equipment) && !FittedHandTracked(equipment))
                return TrainingSubtitles.Tracking;
            return IdleLine(equipment, practice);
        }

        // Why the last trigger press did nothing; else, while a tracked hand wears a pad in the practice, how to take it off.
        public static string Hint(KD1Equipment equipment, KD1SurfacePractice practice)
        {
            var usable = equipment.IsValid && !equipment.IsFaulted && equipment.isActiveAndEnabled;
            var notice = usable ? TrainingSubtitles.Of(equipment.Notice) : string.Empty;
            if (usable && string.IsNullOrEmpty(notice) && PracticeShown(practice) && FittedHandTracked(equipment) && Application.isFocused)
                notice = TrainingSubtitles.RemoveHint;
            return notice;
        }

        static string HeldLine(KD1Equipment equipment, KD1Item held)
        {
            switch (held.Kind)
            {
                case KD1ItemKind.OuterPack:
                    if (!held.IsOpen)
                        return TrainingSubtitles.GrabOuter;
                    return AnyIn(equipment.Packets, KD1ItemState.Stored) ? TrainingSubtitles.PutDownOuter : TrainingSubtitles.EmptyOuter;
                case KD1ItemKind.Packet:
                    if (held.State == KD1ItemState.Extracting)
                        return TrainingSubtitles.PullPacket;
                    if (equipment.PadOf(held).State == KD1ItemState.Stored)
                        return TrainingSubtitles.PutDownPacket;
                    return AnyFitted(equipment) ? TrainingSubtitles.EmptyPacketFitted : TrainingSubtitles.EmptyPacket;
                default:
                    if (held.State == KD1ItemState.Extracting)
                        return TrainingSubtitles.PullPad;
                    return equipment.FittedOn(equipment.HolderHand(held)) == null ? TrainingSubtitles.FitPad : TrainingSubtitles.HeldPadHandBusy;
            }
        }

        static string IdleLine(KD1Equipment equipment, KD1SurfacePractice practice)
        {
            if (!equipment.OuterPack.IsOpen)
                return TrainingSubtitles.GrabOuter;
            // Practice done: stays shown (also after taking the pad off) until the reset.
            if (PracticeShown(practice) && practice.IsComplete)
                return TrainingSubtitles.AllAreasDone;
            if (AnyFitted(equipment))
            {
                if (!PracticeShown(practice))
                    return TrainingSubtitles.Fitted;
                return practice.DoneCount == 0 ? TrainingSubtitles.TouchAreas : TrainingSubtitles.TouchRemaining;
            }
            if (AnyIn(equipment.Pads, KD1ItemState.Free))
                return TrainingSubtitles.GrabPad;
            foreach (var packet in equipment.Packets)
            {
                if (packet.IsOpen && equipment.PadOf(packet).State == KD1ItemState.Stored)
                    return TrainingSubtitles.TakePad;
            }
            return TrainingSubtitles.TakePacket;
        }

        static bool AnyIn(IReadOnlyList<KD1Item> items, KD1ItemState state)
        {
            foreach (var item in items)
            {
                if (item.State == state)
                    return true;
            }
            return false;
        }
    }
}
