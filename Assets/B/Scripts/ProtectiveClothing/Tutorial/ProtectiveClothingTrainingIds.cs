using System;
using System.Collections.Generic;

namespace MilTrainSimVR.B.ProtectiveClothing.Tutorial
{
    // Stable IDs and the step data of the protective clothing wearing course (training data, scene target and states). Display
    // text lives in TrainingSubtitles.
    //
    // ORDER (2026-11-04): trousers, jacket, left overboot, right overboot, left glove, right glove. Basis and its level:
    // - protective suit -> overboots -> (gas mask) -> gloves: an official military public source (Kookbang Ilbo, 2021-04-26,
    //   https://kookbang.dema.mil.kr/newsWeb/20210426/1/BBSMSTR_000000010025/view.do);
    // - trousers before jacket: a public unofficial training summary and the user's recollection agree; the current approved
    //   manual text is not at hand;
    // - left before right: a development choice.
    // This course is the six protective clothing parts only (no gas mask) and is not the whole MOPP procedure or an approved
    // manual. To change the order, edit Order (and the step texts if needed) and the course's training definition to match:
    // step IDs and state IDs belong to the part, not to the position, so they stay stable (ProtectiveClothingPart values are
    // not reordered).
    //
    // Step data (all from Order): step k is the step of Order[k] and needs the current worn state of Order[0..k] (cumulative,
    // State conditions only, no one-off Action), so the last step needs all six parts worn at once and completes the course.
    // Worn means the virtual wear state of ProtectiveClothingEquipment; no fit, closure, contamination or protection is judged.
    public static class ProtectiveClothingTrainingIds
    {
        public const string TrainingId = "pc-protective-clothing-wearing";

        // Current wear state of each part (ProtectiveClothingEquipment.IsWorn), written by ProtectiveClothingTrainingLink.
        public const string EquipmentTarget = "pc-equipment";
        public const string JacketWorn = "jacket-worn";
        public const string TrousersWorn = "trousers-worn";
        public const string GloveLeftWorn = "glove-left-worn";
        public const string GloveRightWorn = "glove-right-worn";
        public const string OverbootLeftWorn = "overboot-left-worn";
        public const string OverbootRightWorn = "overboot-right-worn";

        public const string JacketStep = "pc-jacket";
        public const string TrousersStep = "pc-trousers";
        public const string GloveLeftStep = "pc-glove-left";
        public const string GloveRightStep = "pc-glove-right";
        public const string OverbootLeftStep = "pc-overboot-left";
        public const string OverbootRightStep = "pc-overboot-right";

        // Course order (see the class notes for its basis).
        public static readonly ProtectiveClothingPart[] Order =
        {
            ProtectiveClothingPart.Trousers, ProtectiveClothingPart.Jacket, ProtectiveClothingPart.OverbootLeft, ProtectiveClothingPart.OverbootRight,
            ProtectiveClothingPart.GloveLeft, ProtectiveClothingPart.GloveRight
        };

        public static readonly ProtectiveClothingPart[] AllParts = (ProtectiveClothingPart[])Enum.GetValues(typeof(ProtectiveClothingPart));

        public static int StepCount => Order.Length;

        public static string SignalOf(ProtectiveClothingPart part) => part switch
        {
            ProtectiveClothingPart.Jacket => JacketWorn,
            ProtectiveClothingPart.Trousers => TrousersWorn,
            ProtectiveClothingPart.GloveLeft => GloveLeftWorn,
            ProtectiveClothingPart.GloveRight => GloveRightWorn,
            ProtectiveClothingPart.OverbootLeft => OverbootLeftWorn,
            _ => OverbootRightWorn
        };

        public static string StepOf(ProtectiveClothingPart part) => part switch
        {
            ProtectiveClothingPart.Jacket => JacketStep,
            ProtectiveClothingPart.Trousers => TrousersStep,
            ProtectiveClothingPart.GloveLeft => GloveLeftStep,
            ProtectiveClothingPart.GloveRight => GloveRightStep,
            ProtectiveClothingPart.OverbootLeft => OverbootLeftStep,
            _ => OverbootRightStep
        };

        public static string[] Signals
        {
            get
            {
                var signals = new string[AllParts.Length];
                for (var i = 0; i < AllParts.Length; i++)
                    signals[i] = SignalOf(AllParts[i]);
                return signals;
            }
        }

        // (step ID, parts that must be worn now) in course order.
        public static (string step, ProtectiveClothingPart[] needs)[] Steps
        {
            get
            {
                var steps = new (string, ProtectiveClothingPart[])[Order.Length];
                for (var k = 0; k < Order.Length; k++)
                {
                    var needs = new ProtectiveClothingPart[k + 1];
                    Array.Copy(Order, needs, k + 1);
                    steps[k] = (StepOf(Order[k]), needs);
                }
                return steps;
            }
        }

        // Position of a step in the course (0-based), -1 for an unknown ID.
        public static int StepIndex(string stepId)
        {
            for (var k = 0; k < Order.Length; k++)
            {
                if (StepOf(Order[k]) == stepId)
                    return k;
            }
            return -1;
        }

        // Order holds each of the six parts exactly once (no missing or repeated part).
        public static bool TryValidateOrder(out string error)
        {
            var seen = new HashSet<ProtectiveClothingPart>();
            var repeated = new List<string>();
            foreach (var part in Order)
            {
                if (!Enum.IsDefined(typeof(ProtectiveClothingPart), part) || !seen.Add(part))
                    repeated.Add(part.ToString());
            }
            var missing = new List<string>();
            foreach (var part in AllParts)
            {
                if (!seen.Contains(part))
                    missing.Add(part.ToString());
            }
            error = repeated.Count > 0 || missing.Count > 0 || Order.Length != AllParts.Length
                ? $"Course order must hold each of the {AllParts.Length} parts once (repeated or unknown: {string.Join(", ", repeated)}; missing: {string.Join(", ", missing)})."
                : null;
            return error == null;
        }
    }
}
