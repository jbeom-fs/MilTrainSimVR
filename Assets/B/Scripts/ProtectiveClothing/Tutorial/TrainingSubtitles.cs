using System.Collections.Generic;

namespace MilTrainSimVR.B.ProtectiveClothing.Tutorial
{
    // On-screen text of the protective clothing wearing course (start, practice and completion screens). Leave TMP text fields
    // empty in the Inspector. Lines the equipment already has (bring to the zone, take a glove with the other hand, take it a
    // little away, tracking, focus, pauses, setup and reset errors) are not repeated here: the course guide uses
    // ProtectiveClothingSubtitles for them. The step order and its basis are in ProtectiveClothingTrainingIds (not an approved
    // manual, no gas mask, not the whole MOPP procedure), so no line calls it an official procedure; worn means the virtual wear
    // state only, never fit, closure or protection.
    public static class TrainingSubtitles
    {
        // ---------- Start screen ----------
        public const string CourseTitle = "보호의 착용 교육";
        public const string CourseDescription = "하의, 상의, 양쪽 덮개, 양쪽 장갑 순서로 단계마다 하나씩 착용합니다.\n방독면을 제외한 보호의 6종 연습이며 전체 보호태세(MOPP) 절차가 아닙니다.";
        public const string CourseControls = "장비를 잡아 몸의 표시된 착용 영역으로 가져가면 자동으로 착용됩니다.";
        public const string StartReady = "준비되었습니다. 시작 버튼을 누르세요.";
        public const string StartButton = "시작";
        // Start refused or not possible yet (the reason line).
        public const string StartUnavailable = "지금은 시작할 수 없습니다. 담당자에게 알려 주세요.";
        public const string StartPutDown = "잡고 있는 장비를 내려놓은 뒤 시작하세요.";
        public const string StartNotInitial = "장비가 처음 상태가 아닙니다. 담당자에게 알려 주세요.";
        public const string StartBodyWait = "착용 기능이 준비될 때까지 기다려 주세요.";

        // ---------- Practice: step row ----------
        // {0} step number, {1} step count, {2} step name.
        public const string StepLineFormat = "{0}/{1} 단계: {2}";
        public const string StepJacket = "상의 착용";
        public const string StepTrousers = "하의 착용";
        public const string StepGloveLeft = "왼쪽 장갑 착용";
        public const string StepGloveRight = "오른쪽 장갑 착용";
        public const string StepOverbootLeft = "왼쪽 덮개 착용";
        public const string StepOverbootRight = "오른쪽 덮개 착용";

        // ---------- Practice: purpose line of the step (above the action) ----------
        // From the course description and controls lines and the equipment's bring / glove lines (ProtectiveClothingSubtitles): which
        // part this step wears and how the virtual wearing works. Worn means the virtual wear state only (no fit, closure or protection).
        public const string PurposeTrousers = "보호의 하의를 입습니다. 허리 앞 착용 영역에 가져가면 가상으로 착용됩니다.";
        public const string PurposeJacket = "보호의 상의를 입습니다. 가슴 앞 착용 영역에 가져가면 가상으로 착용됩니다.";
        public const string PurposeOverbootLeft = "왼발에 덮개를 착용합니다. 왼쪽 무릎 아래 표시 근처에 가져가면 가상으로 착용됩니다.";
        public const string PurposeOverbootRight = "오른발에 덮개를 착용합니다. 오른쪽 무릎 아래 표시 근처에 가져가면 가상으로 착용됩니다.";
        public const string PurposeGloveLeft = "왼손에 장갑을 착용합니다. 장갑은 반대쪽 손으로 잡아 착용할 손으로 가져갑니다.";
        public const string PurposeGloveRight = "오른손에 장갑을 착용합니다. 장갑은 반대쪽 손으로 잡아 착용할 손으로 가져갑니다.";

        // ---------- Practice: one action (before the part is held; bringing it reuses ProtectiveClothingSubtitles.Bring) ----------
        public const string GrabJacket = "작업대에서 상의를 잡으세요.";
        public const string GrabTrousers = "작업대에서 하의를 잡으세요.";
        public const string GrabGloveLeft = "작업대에서 왼쪽 장갑을 오른손으로 잡으세요.";
        public const string GrabGloveRight = "작업대에서 오른쪽 장갑을 왼손으로 잡으세요.";
        public const string GrabOverbootLeft = "작업대에서 왼쪽 덮개를 잡으세요.";
        public const string GrabOverbootRight = "작업대에서 오른쪽 덮개를 잡으세요.";
        // Name line over the part to grab now (training field pickup marker).
        public const string PickupJacket = "상의를 잡으세요";
        public const string PickupTrousers = "하의를 잡으세요";
        public const string PickupGloveLeft = "왼쪽 장갑을 잡으세요";
        public const string PickupGloveRight = "오른쪽 장갑을 잡으세요";
        public const string PickupOverbootLeft = "왼쪽 덮개를 잡으세요";
        public const string PickupOverbootRight = "오른쪽 덮개를 잡으세요";
        // The part of this step was just worn; the runner moves to the next step on the next frame.
        public const string WaitingAdvance = "다음 단계로 넘어갑니다.";
        // The run was stopped from outside (runner or flow switched off) or no summary could be made.
        public const string TrainingStopped = "교육이 중지되었습니다. 담당자에게 알려 주세요.";

        // ---------- Practice: status line ----------
        // {0} worn count, {1} part count, {2} the part of this step.
        public const string StatusFormat = "착용 {0}/{1}. 이 단계에서는 {2}만 잡을 수 있습니다.";

        // ---------- Part names ----------
        public const string NameJacket = "상의";
        public const string NameTrousers = "하의";
        public const string NameGloveLeft = "왼쪽 장갑";
        public const string NameGloveRight = "오른쪽 장갑";
        public const string NameOverbootLeft = "왼쪽 덮개";
        public const string NameOverbootRight = "오른쪽 덮개";

        // ---------- Completion ----------
        public const string CompletionTitle = "보호의 착용 교육을 마쳤습니다";
        // {0} step count.
        public const string CompletionCourseFormat = "교육 과정: {0}단계 모두 완료";
        // {0} part name.
        public const string CompletionItemDoneFormat = "{0}: 가상 착용됨";
        public const string CompletionItemNotDoneFormat = "{0}: 착용 안 됨";
        public const string CompletionNote = "VR에서 착용 동작을 마친 기록입니다. 실제 밀착, 여밈, 보호 성능은 확인하지 않았습니다.";
        public const string RetryButton = "다시하기";
        public const string RetryFailed = "다시하기를 준비하지 못했습니다. 담당자에게 알려 주세요.";

        public static string StepName(ProtectiveClothingPart part) => part switch
        {
            ProtectiveClothingPart.Jacket => StepJacket,
            ProtectiveClothingPart.Trousers => StepTrousers,
            ProtectiveClothingPart.GloveLeft => StepGloveLeft,
            ProtectiveClothingPart.GloveRight => StepGloveRight,
            ProtectiveClothingPart.OverbootLeft => StepOverbootLeft,
            _ => StepOverbootRight
        };

        public static string Purpose(ProtectiveClothingPart part) => part switch
        {
            ProtectiveClothingPart.Jacket => PurposeJacket,
            ProtectiveClothingPart.Trousers => PurposeTrousers,
            ProtectiveClothingPart.GloveLeft => PurposeGloveLeft,
            ProtectiveClothingPart.GloveRight => PurposeGloveRight,
            ProtectiveClothingPart.OverbootLeft => PurposeOverbootLeft,
            _ => PurposeOverbootRight
        };

        public static string Grab(ProtectiveClothingPart part) => part switch
        {
            ProtectiveClothingPart.Jacket => GrabJacket,
            ProtectiveClothingPart.Trousers => GrabTrousers,
            ProtectiveClothingPart.GloveLeft => GrabGloveLeft,
            ProtectiveClothingPart.GloveRight => GrabGloveRight,
            ProtectiveClothingPart.OverbootLeft => GrabOverbootLeft,
            _ => GrabOverbootRight
        };

        public static string Pickup(ProtectiveClothingPart part) => part switch
        {
            ProtectiveClothingPart.Jacket => PickupJacket,
            ProtectiveClothingPart.Trousers => PickupTrousers,
            ProtectiveClothingPart.GloveLeft => PickupGloveLeft,
            ProtectiveClothingPart.GloveRight => PickupGloveRight,
            ProtectiveClothingPart.OverbootLeft => PickupOverbootLeft,
            _ => PickupOverbootRight
        };

        public static string Name(ProtectiveClothingPart part) => part switch
        {
            ProtectiveClothingPart.Jacket => NameJacket,
            ProtectiveClothingPart.Trousers => NameTrousers,
            ProtectiveClothingPart.GloveLeft => NameGloveLeft,
            ProtectiveClothingPart.GloveRight => NameGloveRight,
            ProtectiveClothingPart.OverbootLeft => NameOverbootLeft,
            _ => NameOverbootRight
        };

        // Every line of this course, format strings with their placeholders (the Korean font must cover their glyphs).
        public static IEnumerable<string> All => new[]
        {
            CourseTitle, CourseDescription, CourseControls, StartReady, StartButton, StartUnavailable, StartPutDown, StartNotInitial, StartBodyWait,
            StepLineFormat, StepJacket, StepTrousers, StepGloveLeft, StepGloveRight, StepOverbootLeft, StepOverbootRight,
            PurposeTrousers, PurposeJacket, PurposeOverbootLeft, PurposeOverbootRight, PurposeGloveLeft, PurposeGloveRight,
            GrabJacket, GrabTrousers, GrabGloveLeft, GrabGloveRight, GrabOverbootLeft, GrabOverbootRight, WaitingAdvance, TrainingStopped,
            PickupJacket, PickupTrousers, PickupGloveLeft, PickupGloveRight, PickupOverbootLeft, PickupOverbootRight,
            StatusFormat, NameJacket, NameTrousers, NameGloveLeft, NameGloveRight, NameOverbootLeft, NameOverbootRight,
            CompletionTitle, CompletionCourseFormat, CompletionItemDoneFormat, CompletionItemNotDoneFormat, CompletionNote, RetryButton, RetryFailed
        };

        // One-line practice instructions (fit check of the instruction text).
        public static readonly string[] Actions =
        {
            GrabJacket, GrabTrousers, GrabGloveLeft, GrabGloveRight, GrabOverbootLeft, GrabOverbootRight, WaitingAdvance, TrainingStopped
        };
    }
}
