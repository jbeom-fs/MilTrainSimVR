using System.Collections.Generic;

namespace MilTrainSimVR.B.Decontamination.KD1.Tutorial
{
    // On-screen text of the KD-1 course board (start, step, completion). Leave TMP text fields empty in the Inspector; KD1TrainingBoard
    // fills them from here. The current action and hint lines of the practice come from the KD-1 equipment's TrainingSubtitles
    // (KD1GuideLines), so the same state is described in the same words in both scenes; this class only adds the course's purpose
    // lines, step names, start, completion and buttons. The course is a VR handling experience based on the public description of the
    // KD-1 hand-worn pad (tapped on equipment surfaces); it states no procedure, number of taps, force, time, amount of agent,
    // decontamination effect, removal rate or safety result.
    public static class TrainingSubtitles
    {
        public const string CourseTitle = "KD-1 개인제독 키트 체험";
        public const string CourseDescription = "KD-1의 제독 패드는 손에 끼워 장비 표면에 두드려 사용합니다. 이번 체험은 보호장갑의 표시 부위를 대상으로 합니다.";
        public const string CourseNote = "표시와 진행 수는 체험 수행 기록이며, 실제 제독 효과를 측정하지 않습니다.";
        public const string StartButton = "교육 시작";

        // Start panel: whether the course can start now, or why not.
        public const string ReadyNow = "준비되었습니다. 교육 시작을 누르세요.";
        public const string ReadyHeld = "잡고 있는 물체를 놓은 뒤 시작하세요.";
        public const string ReadyWaiting = "장비를 준비하고 있습니다. 잠시 기다려 주세요.";
        public const string ReadyFocus = "교육 화면으로 돌아오세요.";
        public const string ReadyNotInitial = "장비가 처음 상태가 아닙니다. 담당자에게 알려 주세요.";
        public const string SetupInvalid = "교육 설정 오류로 시작할 수 없습니다. 담당자에게 알려 주세요.";
        public const string ResetFailed = "처음 상태로 되돌리지 못했습니다. 장비가 잠긴 상태이니 담당자에게 알려 주세요.";
        public const string Resetting = "처음 상태로 되돌리는 중입니다.";

        // Steps: short name (step boxes and step line) and purpose (one sentence before the current action line).
        public const string OpenName = "KD-1 열기";
        public const string PrepareName = "제독 패드 준비";
        public const string FitName = "제독 패드 착용";
        public const string TouchName = "표시 부위 체험";
        public const string OpenPurpose = "KD-1을 열어 제독 패드를 준비합니다.";
        public const string PreparePurpose = "제독 패드는 KD-1 안의 개별 봉투에 들어 있습니다.";
        public const string FitPurpose = "제독 패드는 손에 끼워 사용합니다.";
        public const string TouchPurpose = "보호장갑의 표시 부위에서 사용법을 체험합니다.";
        // {0} step number, {1} step count, {2} step name.
        public const string StepFormat = "단계 {0}/{1} · {2}";
        // {0} areas done, {1} area count.
        public const string ProgressFormat = "수행한 부위 {0}/{1}";
        public const string RestartButton = "처음부터";

        public const string CompletionTitle = "KD-1 체험 완료";
        public const string CompletionSummary = "KD-1을 열고 개별 봉투에서 제독 패드를 꺼내 손에 끼운 뒤, 장갑의 표시 부위에 사용해 보았습니다.";
        public const string CompletionNote = "체험 수행 기록이며 실제 제독 효과를 나타내지 않습니다.";
        public const string RetryButton = "다시하기";

        public static string NameOf(string stepId) => stepId switch
        {
            KD1TrainingIds.OpenStep => OpenName,
            KD1TrainingIds.PrepareStep => PrepareName,
            KD1TrainingIds.FitStep => FitName,
            KD1TrainingIds.TouchStep => TouchName,
            _ => string.Empty
        };

        public static string PurposeOf(string stepId) => stepId switch
        {
            KD1TrainingIds.OpenStep => OpenPurpose,
            KD1TrainingIds.PrepareStep => PreparePurpose,
            KD1TrainingIds.FitStep => FitPurpose,
            KD1TrainingIds.TouchStep => TouchPurpose,
            _ => string.Empty
        };

        public static string StepLine(int index, string stepId) => string.Format(StepFormat, index + 1, KD1TrainingIds.StepCount, NameOf(stepId));

        public static string Progress(int done, int total) => string.Format(ProgressFormat, done, total);

        // Every string, the format strings with their digits (the Korean font must cover their glyphs).
        public static IEnumerable<string> All => new[]
        {
            CourseTitle, CourseDescription, CourseNote, StartButton, ReadyNow, ReadyHeld, ReadyWaiting, ReadyFocus, ReadyNotInitial, SetupInvalid,
            ResetFailed, Resetting, OpenName, PrepareName, FitName, TouchName, OpenPurpose, PreparePurpose, FitPurpose, TouchPurpose,
            StepFormat.Replace("{0}", "0123456789").Replace("{1}", "").Replace("{2}", ""), ProgressFormat.Replace("{0}", "0123456789").Replace("{1}", ""),
            RestartButton, CompletionTitle, CompletionSummary, CompletionNote, RetryButton
        };
    }
}
