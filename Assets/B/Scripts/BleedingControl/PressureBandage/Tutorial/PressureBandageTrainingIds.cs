using System;

namespace MilTrainSimVR.B.BleedingControl.PressureBandage.Tutorial
{
    // Requirements of the pressure bandage course, in course order. Each is a current fact or a record of this run kept by
    // PressureBandageTrainingLink (see there for which is which and when it is withdrawn).
    public enum PressureRequirement
    {
        Situation = 0,
        Dressing = 1,
        Contact = 2,
        PreCheck = 3,
        Wrap = 4,
        Fix = 5,
        PostCheck = 6
    }

    // Stable IDs of the course (training data, scene targets and states). Display text lives in TrainingSubtitles.
    public static class PressureBandageTrainingIds
    {
        public const string TrainingId = "pb-pressure-bandage-virtual-handling";

        // Current equipment states.
        public const string EquipmentTarget = "pb-equipment";
        public const string DressingPlaced = "dressing-placed";
        public const string WrapComplete = "wrap-complete";
        public const string ApplicationFixed = "application-fixed";

        // Records and inputs of this run.
        public const string TraineeTarget = "pb-trainee";
        public const string SituationConfirmed = "situation-confirmed";
        public const string ContactPracticed = "contact-practiced";
        public const string PreCheckConfirmed = "pre-check-confirmed";
        public const string PostCheckConfirmed = "post-check-confirmed";

        public const string SituationStep = "pb-situation";
        public const string DressingStep = "pb-dressing";
        public const string ContactStep = "pb-contact";
        public const string PreCheckStep = "pb-pre-check";
        public const string WrapStep = "pb-wrap";
        public const string FixStep = "pb-fix";
        public const string PostCheckStep = "pb-post-check";

        // Check items per check (one input per item).
        public const int SituationItems = 1;
        public const int PreCheckItems = 3;
        public const int PostCheckItems = 4;

        public static readonly PressureRequirement[] AllRequirements = (PressureRequirement[])Enum.GetValues(typeof(PressureRequirement));

        // Training data: each step and the requirements that must hold (current states and records), all as State conditions so
        // nothing depends on an action reported in the frame a step is entered. Cumulative where an earlier result must stay
        // valid. The last step (post-wrap check) needs every requirement at once, so the runner completes only when the last
        // post-wrap item is recorded and the situation, dressing placement, contact practice, pre-wrap check, wrap, fixing and
        // post-wrap check all hold together; there is no separate final input.
        public static readonly (string step, PressureRequirement[] needs)[] Steps =
        {
            (SituationStep, new[] { PressureRequirement.Situation }),
            (DressingStep, new[] { PressureRequirement.Situation, PressureRequirement.Dressing }),
            (ContactStep, new[] { PressureRequirement.Situation, PressureRequirement.Dressing, PressureRequirement.Contact }),
            (PreCheckStep, new[] { PressureRequirement.Situation, PressureRequirement.Dressing, PressureRequirement.Contact, PressureRequirement.PreCheck }),
            (WrapStep, new[] { PressureRequirement.Situation, PressureRequirement.Dressing, PressureRequirement.Contact, PressureRequirement.PreCheck,
                               PressureRequirement.Wrap }),
            (FixStep, new[] { PressureRequirement.Situation, PressureRequirement.Dressing, PressureRequirement.Contact, PressureRequirement.PreCheck,
                              PressureRequirement.Wrap, PressureRequirement.Fix }),
            (PostCheckStep, AllRequirements)
        };

        public static (string target, string signal) SignalOf(PressureRequirement requirement) => requirement switch
        {
            PressureRequirement.Situation => (TraineeTarget, SituationConfirmed),
            PressureRequirement.Dressing => (EquipmentTarget, DressingPlaced),
            PressureRequirement.Contact => (TraineeTarget, ContactPracticed),
            PressureRequirement.PreCheck => (TraineeTarget, PreCheckConfirmed),
            PressureRequirement.Wrap => (EquipmentTarget, WrapComplete),
            PressureRequirement.Fix => (EquipmentTarget, ApplicationFixed),
            _ => (TraineeTarget, PostCheckConfirmed)
        };

        public static readonly string[] EquipmentSignals = { DressingPlaced, WrapComplete, ApplicationFixed };
        public static readonly string[] TraineeSignals = { SituationConfirmed, ContactPracticed, PreCheckConfirmed, PostCheckConfirmed };

        // Position of a step in the course (0-based), -1 for an unknown ID.
        public static int StepIndex(string stepId) => Array.FindIndex(Steps, s => s.step == stepId);

        // The step whose action brings a requirement about.
        public static string StepOf(PressureRequirement requirement) => requirement switch
        {
            PressureRequirement.Situation => SituationStep,
            PressureRequirement.Dressing => DressingStep,
            PressureRequirement.Contact => ContactStep,
            PressureRequirement.PreCheck => PreCheckStep,
            PressureRequirement.Wrap => WrapStep,
            PressureRequirement.Fix => FixStep,
            _ => PostCheckStep
        };

        // Short step name (step row line); see TrainingSubtitles.StepLineFormat.
        public static string StepName(string stepId) => stepId switch
        {
            SituationStep => TrainingSubtitles.StepSituation,
            DressingStep => TrainingSubtitles.StepDressing,
            ContactStep => TrainingSubtitles.StepContact,
            PreCheckStep => TrainingSubtitles.StepPreCheck,
            WrapStep => TrainingSubtitles.StepWrap,
            FixStep => TrainingSubtitles.StepFix,
            PostCheckStep => TrainingSubtitles.StepPostCheck,
            _ => string.Empty
        };

        // Purpose line of a step (TrainingSubtitles), shown above the action on the course board.
        public static string StepPurpose(string stepId) => stepId switch
        {
            SituationStep => TrainingSubtitles.PurposeSituation,
            DressingStep => TrainingSubtitles.PurposeDressing,
            ContactStep => TrainingSubtitles.PurposeContact,
            PreCheckStep => TrainingSubtitles.PurposePreCheck,
            WrapStep => TrainingSubtitles.PurposeWrap,
            FixStep => TrainingSubtitles.PurposeFix,
            PostCheckStep => TrainingSubtitles.PurposePostCheck,
            _ => string.Empty
        };

        public static string RequirementName(PressureRequirement requirement) => requirement switch
        {
            PressureRequirement.Situation => TrainingSubtitles.ItemSituation,
            PressureRequirement.Dressing => TrainingSubtitles.ItemDressing,
            PressureRequirement.Contact => TrainingSubtitles.ItemContact,
            PressureRequirement.PreCheck => TrainingSubtitles.ItemPreCheck,
            PressureRequirement.Wrap => TrainingSubtitles.ItemWrap,
            PressureRequirement.Fix => TrainingSubtitles.ItemFix,
            _ => TrainingSubtitles.ItemPostCheck
        };
    }
}
