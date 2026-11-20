namespace MilTrainSimVR.B.AedTutorial
{
    // Single source for on-screen tutorial text. Leave TMP text fields empty in the Inspector.
    public static class TrainingSubtitles
    {
        public const string StartTitle = "AED 장비 조작 교육";
        public const string StartGoal = "AED 전원을 켜고 패드를 부착한 뒤 리듬 분석, 충전, 충격 버튼 조작까지 연습합니다.";
        public const string StartButton = "시작";
        public const string PowerButtonInstruction = "AED 전원 버튼을 누르세요.";
        public const string PadPickupInstruction = "패드 한 개를 집으세요.";
        public const string PowerOffLabel = "꺼짐";
        public const string PowerOnLabel = "켜짐";
        public const string PadPlacementInstruction = "패드의 표시를 확인하고 안내된 부위로 가져가세요.";
        public const string PadRepickInstruction = "패드를 다시 집으세요.";
        public const string ProvisionalAnchorLabel = "예시 위치";
        public const string AttachReadyInstruction = "놓으면 부착됩니다.";
        public const string AttachReadyLabel = "놓으면 부착";
        public const string AttachPositionInstruction = "표시된 위치가 아닙니다. 패드를 다시 집어 표시된 곳에 놓으세요.";
        public const string AttachFacingInstruction = "패드 부착면이 몸을 향하지 않았습니다. 패드를 다시 집으세요.";
        public const string AttachTwistInstruction = "패드 방향이 표시와 맞지 않습니다. 패드를 다시 집으세요.";
        public const string AttachWrongAnchorInstruction = "다른 패드의 위치입니다. 패드를 다시 집어 표시된 곳에 놓으세요.";
        public const string AttachOccupiedInstruction = "이미 패드가 부착된 위치입니다. 패드를 다시 집으세요.";
        public const string RemainingPadInstruction = "남은 패드 한 개를 집으세요.";
        public const string HandsOffInstruction = "심장 리듬을 분석합니다. 환자에게서 손을 떼세요.";
        public const string RhythmAnalyzingInstruction = "심장 리듬 분석 중입니다. 환자에게 손대지 마세요.";
        public const string AnalysisRestartInstruction = "분석 중 환자에게 손이 닿았습니다. 손을 떼면 분석을 처음부터 다시 시작합니다.";
        public const string ContactUnavailableInstruction = "손 위치를 확인할 수 없어 분석을 진행하지 않습니다. 양손 컨트롤러가 추적되는지 확인하세요.";
        public const string SceneReloadRequiredInstruction = "진행이 중단되었습니다. 씬을 다시 불러와 처음부터 진행하세요.";
        public const string ShockAdvisedHandsOffInstruction = "충격이 필요합니다. 충전을 시작하도록 환자에게서 손을 떼세요.";
        public const string ShockChargingInstruction = "충격이 필요합니다. 충전 중이니 기다리세요. 충격 전에는 환자에게서 손을 떼야 합니다.";
        public const string ShockReadyInstruction = "충전이 끝났습니다. 주변 사람이 모두 환자에게서 떨어졌는지 확인하고 깜박이는 충격 버튼을 누르세요.";
        public const string ShockBlockedContactInstruction = "환자에게 손이 닿아 충격 버튼을 잠갔습니다. 손을 뗀 뒤 버튼을 새로 누르세요.";
        public const string ShockTrackingUnavailableInstruction = "손 위치를 확인할 수 없어 충격 버튼을 잠갔습니다. 양손 컨트롤러가 추적되는지 확인하세요.";
        public const string ShockContactCheckUnavailableInstruction = "환자 접촉 판정을 사용할 수 없어 충격 버튼을 잠갔습니다. 씬을 다시 불러오세요.";
        // Explains real follow-up care only; the program runs no CPR or repeat cycle. The completion screen of the board
        // (AedTrainingBoard) shows CompletionTitle and CompletionFollowUp; the instruction line keeps the whole text as before.
        public const string CompletionFollowUp =
            "실제 상황에서는 충격 직후 즉시 심폐소생술을 재개하고,\n패드는 부착한 상태로 유지합니다.\n\n" +
            "약 2분 후 AED의 재분석 안내가 나오면 환자에게서 손을 떼고,\n기기 안내에 따라 필요한 충격과 심폐소생술을 반복합니다.\n" +
            "119 구급대가 인계받을 때까지 안내에 따라 처치를 이어갑니다.";
        public const string ShockDeliveredInstruction = "AED 조작 체험을 완료했습니다.\n\n" + CompletionFollowUp;
        public const string ShockChargingLabel = "충전 중";
        public const string ShockChargedLabel = "충전 완료";
        public const string ShockDeliveredLabel = "충격 시행";
        public const string RetryButton = "다시하기";

        // ---------- Course board (AedTrainingBoard) ----------
        // Ready line of the start screen.
        public const string StartReady = "준비되었습니다. 시작 버튼을 누르세요.";
        public const string StartUnavailable = "지금은 시작할 수 없습니다. 담당자에게 알려 주세요.";
        // Display grouping of the tutorial's existing states into four board steps (no new procedure or criterion):
        // power button; pad pickup and placement; hands off and rhythm analysis; charge and shock button.
        public const string StepPower = "전원 켜기";
        public const string StepPads = "패드 부착";
        public const string StepAnalysis = "리듬 분석";
        public const string StepShock = "충전과 충격";
        // Purpose line of each board step (above the instruction), from the start goal and the existing instructions of that step
        // (hands off during the analysis, everyone clear before the shock); no new procedure, time or energy criterion.
        public const string PurposePower = "AED 사용은 전원을 켜는 것으로 시작합니다.";
        public const string PurposePads = "패드 두 장을 표시된 위치에 하나씩 부착합니다.";
        public const string PurposeAnalysis = "AED가 심장 리듬을 분석하는 동안에는 환자에게 손대지 않습니다.";
        public const string PurposeShock = "충격이 필요하면 충전이 끝난 뒤 모두 환자에게서 떨어진 상태에서 충격 버튼을 누릅니다.";
        // {0} step number, {1} step count, {2} step name.
        public const string StepLineFormat = "단계 {0}/{1} · {2}";
        public const string CompletionTitle = "AED 조작 체험 완료";
        // Step counter characters; layout only, not a subtitle.
        public const string CounterCharacters = "0123456789/·";

        public const string All = StartTitle + StartGoal + StartButton + PowerButtonInstruction +
            PadPickupInstruction + PowerOffLabel + PowerOnLabel +
            PadPlacementInstruction + PadRepickInstruction + ProvisionalAnchorLabel +
            AttachReadyInstruction + AttachReadyLabel + AttachPositionInstruction + AttachFacingInstruction +
            AttachTwistInstruction + AttachWrongAnchorInstruction + AttachOccupiedInstruction +
            RemainingPadInstruction + HandsOffInstruction + RhythmAnalyzingInstruction +
            AnalysisRestartInstruction + ContactUnavailableInstruction + SceneReloadRequiredInstruction +
            ShockAdvisedHandsOffInstruction + ShockChargingInstruction + ShockReadyInstruction +
            ShockBlockedContactInstruction + ShockTrackingUnavailableInstruction +
            ShockContactCheckUnavailableInstruction + ShockDeliveredInstruction +
            ShockChargingLabel + ShockChargedLabel + ShockDeliveredLabel + RetryButton +
            StartReady + StartUnavailable + StepPower + StepPads + StepAnalysis + StepShock + StepLineFormat + CompletionTitle + CounterCharacters +
            PurposePower + PurposePads + PurposeAnalysis + PurposeShock;
    }
}
