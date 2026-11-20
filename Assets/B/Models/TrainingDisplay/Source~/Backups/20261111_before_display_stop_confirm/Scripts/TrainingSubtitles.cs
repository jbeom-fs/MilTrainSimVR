namespace MilTrainSimVR.B.TrainingField
{
    // Single source for the on-screen text of the B training fields (first aid and CBRN field scenes): entrance board, station
    // panels, the stop confirm, the board blockers and the field exit. Each course's own board keeps its own TrainingSubtitles.
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
        public const string StationEntryPreparing = "입구 준비 중입니다. 잠시 기다리세요.";
        public const string BlockedEntryPreparing = "입구 준비 중입니다.\n헤드셋 위치가 확인되면 교육을 시작할 수 있습니다.";
        public const string MoveGuide = "이동: 왼손 스틱 · 방향 전환: 오른손 스틱 좌우 · 순간 이동: 오른손 스틱을 앞으로 밀어 바닥이나 실습 위치 표시를 가리킨 뒤 놓기";

        // Station names (field panel title, entrance list, blocker line).
        public const string StationAed = "AED 사용";
        public const string StationTourniquet = "지혈대 사용";
        public const string StationPressureBandage = "직접 압박·압박붕대";
        public const string StationK1GasMask = "K1 방독면 착용";
        public const string StationProtectiveClothing = "보호의 착용";
        public const string StationKD1 = "KD-1 휴대장비 제독";

        // Station panel status line, by the course's state.
        public const string StationWaiting = "옆 안내판의 교육 시작을 선택하면 이 위치의 실습이 시작됩니다.";
        public const string StationRunning = "실습 중입니다. 그만하려면 교육 중단을 선택하세요.";
        public const string StationCompleted = "실습을 마쳤습니다. 요약을 확인한 뒤 다시하기 또는 교육 종료를 선택하세요.";
        public const string StationBusy = "정리 중입니다. 잠시 기다리세요.";
        public const string StationFaulted = "이 실습을 지금 진행할 수 없습니다. 진행 담당자에게 알려 주세요.";
        public const string StationOtherActiveFormat = "{0} 실습이 진행 중입니다. 그 위치에서 교육 종료 또는 교육 중단을 선택한 뒤 시작하세요.";
        public const string StationResetFailed = "정리에 실패했습니다. 진행 담당자에게 알려 주세요.";

        // Latest result of this station in this visit (memory only).
        public const string ResultCompleted = "최근 결과: 완료";
        public const string ResultAborted = "최근 결과: 중단";

        public const string StopButton = "교육 중단";
        public const string EndButton = "교육 종료";

        // Stop confirm (the course is held while it is open).
        public const string ConfirmQuestion = "교육을 중단할까요?";
        public const string ConfirmDetail = "확인하는 동안 이 실습만 멈춰 있습니다.\n중단하면 이 실습의 진행과 장비가 처음 상태로 돌아갑니다.";
        public const string ContinueButton = "계속하기";
        public const string AbortButton = "중단하기";

        // Blocker over a course board: another station is in use, or this course is held by the stop confirm.
        // Short: the station panel beside the board already says what to do (StationOtherActiveFormat).
        public const string BlockedOtherFormat = "{0} 실습이 진행 중입니다.";
        public const string BlockedPaused = "교육 중단 확인 중입니다.\n현장 안내판에서 계속하기 또는 중단하기를 선택하세요.";

        // Field exit.
        public const string ExitToCbrnButton = "화생방 교육장으로 이동";
        public const string ExitToFirstAidButton = "구급법 교육장으로 이동";
        public const string ExitReady = "교육장 출구입니다. 다른 분야 교육장으로 이동할 수 있습니다.";
        public const string ExitBlocked = "진행 중인 실습이 있습니다. 교육 종료 또는 교육 중단 후 이동하세요.";
        public const string ExitUnavailable = "이동할 교육장이 빌드 씬 목록에 등록되지 않아 이동할 수 없습니다. 진행 담당자에게 알려 주세요.";
        public const string ExitLoading = "이동 중입니다.";

        public const string All = FirstAidFieldTitle + CbrnFieldTitle + FirstAidFieldDescription + CbrnFieldDescription + FieldGuide + MoveGuide +
            EntryPreparingGuide + StationEntryPreparing + BlockedEntryPreparing +
            StationAed + StationTourniquet + StationPressureBandage + StationK1GasMask + StationProtectiveClothing + StationKD1 +
            StationWaiting + StationRunning + StationCompleted + StationBusy + StationFaulted + StationOtherActiveFormat + StationResetFailed +
            ResultCompleted + ResultAborted + StopButton + EndButton + ConfirmQuestion + ConfirmDetail + ContinueButton + AbortButton +
            BlockedOtherFormat + BlockedPaused + ExitToCbrnButton + ExitToFirstAidButton + ExitReady + ExitBlocked + ExitUnavailable + ExitLoading;

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
