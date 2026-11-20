namespace MilTrainSimVR.B.K1GasMask.Tutorial
{
    // Single source for on-screen K1 training text. Leave TMP text fields empty in the Inspector.
    public static class TrainingSubtitles
    {
        public const string CourseTitle = "K1 방독면 착용 교육";
        public const string StartDescription = "주머니를 열고 방독면을 꺼내 착용하는 조작을 연습합니다.";
        public const string StartButton = "교육 시작";
        // Ready line of the start screen: the start button can be used now, or the guide is blocked.
        public const string StartReady = "준비되었습니다. 교육 시작을 누르세요.";
        public const string StartUnavailable = "지금은 시작할 수 없습니다. 담당자에게 알려 주세요.";

        public const string ReleaseClosureTitle = "고정장치 해제";
        public const string ReleaseClosureInstruction = "주머니의 고정장치를 잡아 해제하세요.";
        public const string OpenLidTitle = "덮개 열기";
        public const string OpenLidInstruction = "잡기를 놓은 뒤 덮개 손잡이를 잡아 끝까지 여세요.";
        public const string ExtractMaskTitle = "방독면 꺼내기";
        public const string ExtractMaskInstruction = "방독면 본체를 잡고 위로 완전히 꺼내세요.";
        public const string BringToFaceTitle = "얼굴에 가져오기";
        public const string BringToFaceInstruction = "방독면을 얼굴 가까이 가져오세요.";
        public const string SeatStrapsTitle = "끈을 뒤로 옮기기";
        public const string SeatStrapsInstruction = "끈을 손으로 잡아 머리 뒤로 옮기세요.";

        // Purpose line of each step (course board, above the instruction), from the course description (open the carrier, take the
        // mask out, wear it) and the step table of Docs/B 1028_K1_교육단계_UI_설계; the strap adjustment and seal check stay on the
        // completion screen (no new procedure, count or time).
        public const string ReleaseClosurePurpose = "주머니를 열기 위해 먼저 고정장치를 풉니다.";
        public const string OpenLidPurpose = "덮개를 끝까지 열어 방독면을 꺼낼 수 있게 합니다.";
        public const string ExtractMaskPurpose = "열린 주머니에서 방독면 본체를 위로 꺼냅니다.";
        public const string BringToFacePurpose = "꺼낸 방독면을 얼굴에 맞춰 착용을 시작합니다.";
        public const string SeatStrapsPurpose = "끈을 머리 뒤로 옮겨 착용을 마칩니다. 끈 조절과 밀착 확인은 완료 화면에서 안내합니다.";

        public const string PreparingInstruction = "착용 준비 중입니다.";
        public const string ReopenLidHint = "덮개를 다시 끝까지 여세요.";
        public const string BringBackInstruction = "방독면을 다시 얼굴에 가져오세요.";
        public const string TrackingLostHint = "위치 추적이 끊겨 잠시 멈췄습니다. 추적이 돌아오면 이어서 진행합니다.";
        // After the training field's stop confirm: a pose made during the dialog is not taken as done.
        public const string FaceAwayFirstHint = "방독면을 얼굴에서 조금 뗐다가 다시 가져오세요.";
        public const string StrapAgainHint = "끈을 잡은 손을 머리 앞으로 가져왔다가 다시 뒤로 옮기세요.";

        public const string CompleteTitle = "K1 방독면 착용 체험 완료";
        // Summary of the official article below, not a quotation, field manual or current revised doctrine. Explanation only:
        // nothing in the program tightens, detects breathing or judges the seal.
        public const string CompleteBody = "머리끈은 윗끈·아랫머리끈·중간끈 순으로 번갈아 당겨 조절합니다.\n\n" +
            "조절 후 충분히 숨을 내쉬고, 정화통 흡입구를 손바닥으로 가린 채 숨을 들이쉬어 밀착 상태를 확인합니다.";
        // https://kookbang.dema.mil.kr/newsWeb/20201021/1/BBSMSTR_000000100128/view.do (checked 2026-10-28)
        public const string CompleteSource = "출처:\n국방홍보원 국방일보 「방독면 원리와 착용」, 2020.10.21.";

        public const string RetryButton = "다시하기";

        // Label of the rear observation screen (mirror and avatar module); no training step depends on it.
        public const string RearViewLabel = "후면 보기";

        // Step counter characters ("3 / 5"); layout only, not a subtitle.
        public const string CounterCharacters = "0123456789 /";

        public const string All = CourseTitle + StartDescription + StartButton + StartReady + StartUnavailable +
            ReleaseClosureTitle + ReleaseClosureInstruction + OpenLidTitle + OpenLidInstruction +
            ExtractMaskTitle + ExtractMaskInstruction + BringToFaceTitle + BringToFaceInstruction +
            SeatStrapsTitle + SeatStrapsInstruction + PreparingInstruction + ReopenLidHint +
            ReleaseClosurePurpose + OpenLidPurpose + ExtractMaskPurpose + BringToFacePurpose + SeatStrapsPurpose +
            BringBackInstruction + TrackingLostHint + FaceAwayFirstHint + StrapAgainHint + CompleteTitle + CompleteBody + CompleteSource + RetryButton + RearViewLabel + CounterCharacters;
    }
}
