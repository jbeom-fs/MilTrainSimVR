using System.Collections.Generic;

namespace MilTrainSimVR.B.Decontamination.KD1
{
    // On-screen text of the KD-1 equipment scene: one action line, an optional reason line and the reset button. Leave TMP text
    // fields empty in the Inspector; KD1Guide fills them from here. Screen terms: outer pack = KD-1, packet = 개별 봉투,
    // pad = 제독 패드. Grabbing is described as 잡기/놓기/꺼내기; the trigger is named only where it is needed (fitting and taking
    // off a pad); touching the areas needs no button. The automatic opening, the trigger fitting and the touch areas are a simplified VR
    // practice of this virtual set, not the product's procedure or an evaluation standard; nothing here states a decontamination
    // effect, a removal rate, a safety result or a product specification.
    public static class TrainingSubtitles
    {
        // Action lines: what can be done now.
        public const string GrabOuter = "KD-1을 잡으세요.";
        public const string PutDownOuter = "KD-1을 내려놓고 안에 있는 개별 봉투를 잡으세요.";
        public const string EmptyOuter = "내용물을 모두 꺼냈습니다. KD-1을 내려놓으세요.";
        public const string TakePacket = "KD-1 안에 있는 개별 봉투를 잡으세요.";
        public const string PullPacket = "개별 봉투를 입구 쪽으로 끝까지 꺼내세요.";
        public const string PutDownPacket = "봉투를 내려놓고 안에 있는 제독 패드를 잡으세요.";
        public const string EmptyPacket = "빈 봉투입니다. 내려놓고 제독 패드를 잡으세요.";
        public const string EmptyPacketFitted = "빈 봉투입니다. 내려놓으세요.";
        public const string TakePad = "개별 봉투 안에 있는 제독 패드를 잡으세요.";
        public const string PullPad = "제독 패드를 봉투 밖으로 끝까지 꺼내세요.";
        public const string GrabPad = "꺼낸 제독 패드를 잡으세요.";
        public const string FitPad = "트리거를 눌러 제독 패드를 손에 끼우세요.";
        public const string Fitted = "제독 패드를 끼웠습니다. 벗기려면 이 손의 트리거를 누르세요.";
        public const string HeldPadHandBusy = "잡은 패드를 내려놓고, 착용한 패드를 먼저 벗으세요.";
        // Surface practice (when the scene has it; otherwise Fitted is shown after fitting).
        // First line after fitting: what the pad is for and what to do now (the touch of the pad's underside counts).
        public const string TouchAreas = "제독 패드는 장비 표면에 두드려 사용합니다. 장갑의 민트색 부위를 두드려 보세요.";
        public const string TouchRemaining = "남은 민트색 표시 부위를 체험하세요.";
        public const string AllAreasDone = "표시된 부위를 모두 체험했습니다.";
        // Second line while a pad is fitted and the practice is shown (in the reason field when no reason is shown).
        public const string RemoveHint = "패드를 벗기려면 착용한 손의 트리거를 누르세요.";
        // Progress field: {0} areas done of {1}.
        public const string ProgressFormat = "연습 부위 {0}/{1}";

        // Reason lines: why the last trigger press did nothing.
        public const string PadNotOut = "제독 패드를 봉투 밖으로 끝까지 꺼낸 뒤 트리거를 누르세요.";
        public const string HandHasPad = "이 손에는 이미 제독 패드를 끼웠습니다.";
        public const string Tracking = "컨트롤러를 시야 안으로 가져오세요.";
        public const string Paused = "교육 화면으로 돌아오세요.";

        // Status lines.
        public const string ResetFailed = "초기화하지 못했습니다. 다시 눌러 보고, 계속되면 담당자에게 알려 주세요.";
        public const string SetupInvalid = "장비 설정 오류로 사용할 수 없습니다. 담당자에게 알려 주세요.";
        public const string EquipmentPaused = "장비 사용이 일시 중지되었습니다.";
        public const string ResetButton = "장비 초기화";

        public static string Of(KD1Notice notice) => notice switch
        {
            KD1Notice.PadNotOut => PadNotOut,
            KD1Notice.HandHasPad => HandHasPad,
            KD1Notice.Tracking => Tracking,
            KD1Notice.Paused => Paused,
            _ => string.Empty
        };

        // Lines shown in the instruction or reason field (each must fit it).
        public static readonly string[] Lines =
        {
            GrabOuter, PutDownOuter, EmptyOuter, TakePacket, PullPacket, PutDownPacket, EmptyPacket, EmptyPacketFitted, TakePad, PullPad,
            GrabPad, FitPad, Fitted, HeldPadHandBusy, TouchAreas, TouchRemaining, AllAreasDone, RemoveHint, PadNotOut, HandHasPad, Tracking, Paused,
            ResetFailed, SetupInvalid, EquipmentPaused
        };

        public static string Progress(int done, int total) => string.Format(ProgressFormat, done, total);

        // Every string (the Korean font must cover their glyphs).
        public static IEnumerable<string> All
        {
            get
            {
                foreach (var line in Lines)
                    yield return line;
                yield return ProgressFormat.Replace("{0}", "0123456789").Replace("{1}", "");
                yield return ResetButton;
            }
        }
    }
}
