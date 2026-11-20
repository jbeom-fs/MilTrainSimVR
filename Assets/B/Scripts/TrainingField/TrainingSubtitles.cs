namespace MilTrainSimVR.B.TrainingField
{
    // Single source for the on-screen text of the B training fields (first aid and CBRN field scenes): entrance board, the station
    // controls inside each course display, the stop confirm and the board blockers. The field exit (door knob) has no text. Each
    // course's own board keeps its own TrainingSubtitles.
    // Leave TMP text fields empty in the Inspector. Results are in memory only: no score, pass grade or saving.
    public static class TrainingSubtitles
    {
        public const string FirstAidFieldTitle = "구급법 교육장";
        public const string CbrnFieldTitle = "화생방 교육장";
        public const string FirstAidFieldDescription = "AED 사용, 지혈대 사용, 직접 압박·압박붕대 실습 위치가 있습니다.";
        public const string CbrnFieldDescription = "K1 방독면 착용, 보호의 착용, KD-1 휴대장비 제독 실습 위치가 있습니다.";
        public const string FieldGuide = "실습 위치로 이동한 뒤 그 위치의 안내판에서 교육 시작을 선택하세요.\n" +
                                         "한 번에 한 실습만 진행합니다. 순서는 자유롭게 고를 수 있습니다.";
        // Entry being prepared (the headset's tracking not there yet): no course can start until it is.
        public const string EntryPreparingGuide = "입구 준비 중입니다. 헤드셋 위치가 확인되면 실습을 시작할 수 있습니다.";
        public const string BlockedEntryPreparing = "입구 준비 중입니다.\n헤드셋 위치가 확인되면 교육을 시작할 수 있습니다.";
        public const string MoveGuide = "이동: 왼손 스틱 · 방향 전환: 오른손 스틱 좌우 · 순간 이동: 오른손 스틱을 앞으로 밀어 바닥이나 실습 위치 표시를 가리킨 뒤 놓기";

        // Station names (entrance list, blocker line).
        public const string StationAed = "AED 사용";
        public const string StationTourniquet = "지혈대 사용";
        public const string StationPressureBandage = "직접 압박·압박붕대";
        public const string StationK1GasMask = "K1 방독면 착용";
        public const string StationProtectiveClothing = "보호의 착용";
        public const string StationKD1 = "KD-1 휴대장비 제독";

        // Latest result of this station in this visit (memory only; course start screen and entrance list).
        public const string ResultCompleted = "최근 결과: 완료";
        public const string ResultAborted = "최근 결과: 중단";

        public const string StopButton = "교육 중단";
        public const string EndButton = "교육 종료";

        // Stop confirm over the course display (the course is held while it is open).
        public const string ConfirmQuestion = "교육을 중단할까요?";
        public const string ConfirmDetail = "확인하는 동안 이 실습만 멈춰 있습니다.\n중단하면 이 실습의 진행과 장비가 처음 상태로 돌아갑니다.";
        public const string ContinueButton = "계속하기";
        public const string AbortButton = "중단하기";

        // Blocker over a course display: another station is in use, or this course cannot run now (the stop confirm has its own
        // texts above).
        public const string BlockedOtherFormat = "{0} 실습이 진행 중입니다.\n그 위치에서 교육 종료 또는 교육 중단을 선택한 뒤 시작하세요.";
        public const string BlockedFaulted = "이 실습을 지금 진행할 수 없습니다.\n진행 담당자에게 알려 주세요.";
        public const string BlockedResetFailed = "정리에 실패했습니다.\n진행 담당자에게 알려 주세요.";

        public const string All = FirstAidFieldTitle + CbrnFieldTitle + FirstAidFieldDescription + CbrnFieldDescription + FieldGuide + MoveGuide +
            EntryPreparingGuide + BlockedEntryPreparing +
            StationAed + StationTourniquet + StationPressureBandage + StationK1GasMask + StationProtectiveClothing + StationKD1 +
            ResultCompleted + ResultAborted + StopButton + EndButton + ConfirmQuestion + ConfirmDetail + ContinueButton + AbortButton +
            BlockedOtherFormat + BlockedFaulted + BlockedResetFailed;

        public static string StationName(TrainingFieldCourseId course) => course switch
        {
            TrainingFieldCourseId.Aed => StationAed,
            TrainingFieldCourseId.Tourniquet => StationTourniquet,
            TrainingFieldCourseId.PressureBandage => StationPressureBandage,
            TrainingFieldCourseId.K1GasMask => StationK1GasMask,
            TrainingFieldCourseId.ProtectiveClothing => StationProtectiveClothing,
            TrainingFieldCourseId.KD1 => StationKD1,
            _ => string.Empty
        };
    }
}
