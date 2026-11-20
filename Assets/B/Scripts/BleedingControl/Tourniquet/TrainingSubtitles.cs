namespace MilTrainSimVR.B.BleedingControl.Tourniquet
{
    // Single source for on-screen text of the tourniquet training start screen, step guide, final check, completion screen and the placement and part handling prototype. Leave TMP text fields empty in the
    // Inspector. Zones, tolerances, pull and turn amounts are development settings of a technical prototype, not a product
    // reproduction or a medical criterion. The time record is a virtual training record, not a real treatment time.
    public static class TrainingSubtitles
    {
        public const string BoardTitle = "지혈대 배치·부품 조작 기술 시제품";
        // The trainee is shown the green placement area only (no colour legend; the other areas still refuse a placement).
        public const string PrototypeNotice = "특정 제품의 재현이나 의료 기준이 아닌 조작 확인용 시제품입니다. 당김·회전량은 게임 조작 설정입니다.";
        public const string Instruction = "지혈대를 잡아 초록색으로 표시된 부위에 가져간 뒤 놓으세요.";

        // Training start screen (in the course's own scene shown with PrototypeNotice before the start button is selected; the
        // training field display has no notice line).
        public const string StartTitle = "지혈대 가상 조작 교육";
        public const string StartDescription = "지혈대를 표시된 부위에 배치하고, 밴드·조임막대·시간 기록·마감 스트랩 조작을 체험합니다.";
        public const string StartReady = "교육 시작을 선택한 뒤 안내에 따라 지혈대를 조작하세요.";
        public const string StartButton = "교육 시작";
        public const string StartUnavailable = "교육을 시작할 수 없습니다. 장비 준비 상태를 확인하지 못했습니다.";

        // Six-step training guide (step line and one action). Virtual handling steps, not a medical procedure or a pass result.
        // Each step's action reuses the part handling instruction above.
        public const string StepLineFormat = "{0} / {1} · {2}";
        // Step line while an earlier state reached in this run is restored (the guide's recovery target, not the runner's step):
        // a display of what to restore now, not a step going back.
        public const string StepRecoveryFormat = "보완 {0}/{1} · {2}";
        public const string StepPlace = "지혈대 배치";
        public const string StepBand = "밴드 준비";
        public const string StepTurn = "막대 회전";
        public const string StepSecure = "막대 고정";
        public const string StepRecord = "시간 기록";
        public const string StepFasten = "마감";
        public const string RecoverTurn = "막대 회전량이 다시 부족합니다. 처음 돌린 방향으로 막대를 더 돌린 뒤 고정하세요.";
        // Purpose line of each step on the course board (above the action), from the course description, the prototype notice
        // and the step actions above; it explains the handling of this prototype only (no medical criterion, pressure or time limit).
        public const string PurposePlace = "지혈대를 초록색으로 표시된 부위에 배치합니다. 미리보기로 위치와 방향을 맞춥니다.";
        public const string PurposeBand = "밴드를 끝까지 당겨 조임막대를 돌릴 수 있게 준비합니다.";
        public const string PurposeTurn = "조임막대를 돌려 조이는 조작입니다. 필요한 회전량은 게임 조작 설정입니다.";
        public const string PurposeSecure = "돌린 조임막대가 되돌아가지 않도록 고정부에 끼웁니다.";
        public const string PurposeRecord = "부착 후 경과 시각을 표기면에 기록하는 조작입니다. (가상 기록)";
        public const string PurposeFasten = "마감 스트랩으로 고정부를 덮어 조작을 마무리합니다.";

        // Final check after the six steps. 충족/미충족 is the program's check of the device states; the trainee's own check is
        // only the button input (no gaze or visual check is measured).
        public const string FinalCheckTitle = "최종 확인";
        public const string FinalCheckDescription = "부착 위치, 조임막대 고정, 마감 스트랩, 시간 기록을 직접 확인한 뒤 '확인 완료'를 선택하세요.";
        public const string FinalItemAttached = "지정 대상에 부착";
        public const string FinalItemBand = "밴드 준비";
        public const string FinalItemTurn = "필요 회전량 충족";
        public const string FinalItemSecured = "조임막대 고정";
        public const string FinalItemRecorded = "시간 기록";
        public const string FinalItemFastened = "마감 스트랩 고정";
        public const string FinalItemMetFormat = "충족 · {0}";
        public const string FinalItemUnmetFormat = "미충족 · {0}";
        public const string FinalCheckButton = "확인 완료";
        public const string FinalCheckReady = "충족·미충족은 장비 상태 검사 결과입니다. 확인했다면 '확인 완료'를 선택하세요.";
        public const string FinalCheckUnmet = "미충족 항목이 있습니다. 조치한 뒤 '확인 완료'를 선택하세요.";
        public const string FinalCheckBlocked = "미충족 항목이 있어 완료하지 않았습니다.";
        public const string FinalCheckRetry = "항목이 모두 충족되었습니다. '확인 완료'를 다시 선택하세요.";
        public const string FinalCheckCancelled = "장비 상태가 바뀌어 확인이 취소되었습니다. '확인 완료'를 다시 선택하세요.";
        public const string FinalCheckAccepted = "확인 입력을 받았습니다. 장비 상태를 다시 검사합니다.";

        // Completion screen: a summary of the virtual handling performed, not a treatment result, protection effect or pass.
        public const string CompletionTitle = "지혈대 가상 조작 체험 완료";
        public const string CompletionItemDoneFormat = "{0} · 수행";
        public const string CompletionItemNotDoneFormat = "{0} · 확인 안 됨";
        public const string CompletionNote = "가상 조작 수행 요약입니다. 실제 지혈 성공이나 의료적 합격을 뜻하지 않습니다.\n" +
            "장비 상태와 시간 기록은 다시하기 전까지 유지됩니다.";
        // Retry after completion only (the completion panel's button). Restores the device and the training to the start screen.
        public const string RetryButton = "다시하기";
        public const string RetryFailed = "다시하기 준비에 실패했습니다. 진행 담당자에게 알려 주세요.";

        public const string TrainingStoppedTitle = "교육 진행 중단";
        public const string TrainingStopped = "교육 진행이 중단되었습니다. 진행 담당자에게 알려 주세요.";

        public const string StatusIdle = "미부착 · 잡지 않음";
        public const string StatusHeldBlocked = "잡고 있음 · 배치 불가";
        public const string StatusHeldPlaceable = "잡고 있음 · 배치 가능";
        public const string StatusAttached = "부착됨";

        public const string ReasonPrefix = "이유: ";
        public const string PlaceableDetail = "지금 놓으면 부착됩니다.";
        public const string FailedPrefix = "부착되지 않음 - ";
        public const string RetryHint = "\n다시 잡아 시도하세요.";
        public const string AttachedDetail = "지혈대가 표시된 사지에 부착되었습니다.\n이 시제품에서는 부착 후 다시 옮길 수 없습니다.";

        // Part handling after attaching (chapter 2). Virtual prototype order, not an approved medical procedure:
        // band -> windlass turn and seat -> time record -> securing strap.
        public const string InstructionPullBand = "빨간 밴드 끝을 잡아 바깥쪽으로 끝까지 당긴 뒤 놓으세요.";
        public const string InstructionTurnWindlass = "조임막대의 끝 쪽을 잡고 막대 축을 중심으로 돌리세요.";
        public const string InstructionSeatWindlass = "막대를 고정부 옆에 맞춰 아래로 내린 뒤, 고정부 쪽으로 돌려 끝까지 끼우세요.";
        public const string InstructionRecordTime = "시간 표기면을 잡거나 레이로 선택해 훈련 경과 시각을 기록하세요. (가상 기록)";
        public const string InstructionCoverStrap = "마감 스트랩 끝을 잡아 고정부 위로 끝까지 덮은 뒤 놓으세요.";
        public const string InstructionPartsDone = "막대 고정과 마감 스트랩 고정이 끝났습니다. (가상 조작)";

        public const string StatusPullBandFormat = "밴드 끝 당김 {0}%";
        public const string StatusTurnWindlassFormat = "막대 회전 {0}° / {1}°";
        public const string StatusTurnReady = "회전 충분 · 고정부 옆에서 아래로 내리세요";
        public const string StatusSeatLowering = "막대 내리는 중";
        public const string StatusSeatEnteringFormat = "고정부 쪽 회전 {0}° / {1}°";
        public const string StatusRecordTime = "막대 고정 완료 · 시간 기록 차례";
        public const string StatusCoverStrapFormat = "마감 스트랩 덮기 {0}%";
        public const string StatusStrapCovered = "덮임 · 놓으면 고정됩니다";
        public const string StatusPartsDone = "막대 고정 · 마감 고정 완료";

        public const string TimeNotRecorded = "시간 기록: 없음";
        public const string TimeRecordedFormat = "시간 기록: 훈련 경과 {0:00}:{1:00} (가상 기록)";
        // Text written on the time label surface: elapsed training minutes and seconds.
        public const string TimeLabelFormat = "{0:00}:{1:00}";

        public const string IssueTrackingLost = "손 추적 끊김 - 끊긴 동안의 움직임은 진행에 넣지 않았습니다";
        public const string IssueSelectionCanceled = "잡기 취소됨";
        public const string IssuePullIncomplete = "당김 부족 - 다시 잡아 끝까지 당긴 뒤 놓으세요";
        public const string IssueTurnIncomplete = "회전량 부족 - 아직 고정부에 넣을 수 없습니다";
        public const string IssueSeatIncomplete = "고정되지 않음 - 고정부 쪽으로 끝까지 돌려 끼우세요";
        public const string IssueCoverIncomplete = "덮기 부족 - 고정부 위로 끝까지 덮은 뒤 놓으세요";
        public const string IssuePartsUnavailable = "부품 조작 준비 실패 - 구성 메뉴와 모델 설정을 확인하세요";
        public const string IssueReleaseInputUnavailable = "놓은 순간의 손 움직임 확인 불가 - 다시 잡아 놓으세요";

        public const string PreviewPlaceable = "배치 가능";
        public const string PreviewBlocked = "배치 불가";

        public const string ReasonOutOfRange = "표시된 영역에서 멂";
        public const string ReasonOutsideAllowedZone = "위치 - 초록색 부위 밖";
        public const string ReasonOffLimbCenter = "위치 - 사지 중심에서 벗어남";
        public const string ReasonWrongAxis = "방향 - 사지 방향과 어긋남";
        public const string ReasonWrongRoll = "방향 - 본체 면을 미리보기와 맞추세요";
        public const string ReasonForbiddenZone = "위치 - 놓을 수 없는 곳에 걸침";
        public const string ReasonOtherTarget = "위치 - 초록색으로 표시된 부위가 아님";
        public const string ReasonTrackingLost = "손 추적 끊김";
        public const string ReasonSelectionCanceled = "잡기 취소됨";
        public const string ReasonReleasePoseUnavailable = "놓은 자세 확인 불가";

        public static string Reason(TourniquetPlacementCheck check) => check switch
        {
            TourniquetPlacementCheck.OutOfRange => ReasonOutOfRange,
            TourniquetPlacementCheck.OutsideAllowedZone => ReasonOutsideAllowedZone,
            TourniquetPlacementCheck.OffLimbCenter => ReasonOffLimbCenter,
            TourniquetPlacementCheck.WrongAxis => ReasonWrongAxis,
            TourniquetPlacementCheck.WrongRoll => ReasonWrongRoll,
            TourniquetPlacementCheck.ForbiddenZone => ReasonForbiddenZone,
            TourniquetPlacementCheck.OtherTarget => ReasonOtherTarget,
            TourniquetPlacementCheck.TrackingLost => ReasonTrackingLost,
            TourniquetPlacementCheck.SelectionCanceled => ReasonSelectionCanceled,
            TourniquetPlacementCheck.ReleasePoseUnavailable => ReasonReleasePoseUnavailable,
            _ => string.Empty
        };

        public static string Issue(TourniquetOperationIssue issue) => issue switch
        {
            TourniquetOperationIssue.TrackingLost => IssueTrackingLost,
            TourniquetOperationIssue.SelectionCanceled => IssueSelectionCanceled,
            TourniquetOperationIssue.PullIncomplete => IssuePullIncomplete,
            TourniquetOperationIssue.TurnIncomplete => IssueTurnIncomplete,
            TourniquetOperationIssue.SeatIncomplete => IssueSeatIncomplete,
            TourniquetOperationIssue.CoverIncomplete => IssueCoverIncomplete,
            TourniquetOperationIssue.PartsUnavailable => IssuePartsUnavailable,
            TourniquetOperationIssue.ReleaseInputUnavailable => IssueReleaseInputUnavailable,
            _ => string.Empty
        };

        public const string All = BoardTitle + PrototypeNotice + Instruction +
            StartTitle + StartDescription + StartReady + StartButton + StartUnavailable +
            StepLineFormat + StepRecoveryFormat + StepPlace +StepBand + StepTurn + StepSecure + StepRecord + StepFasten + RecoverTurn +
            PurposePlace + PurposeBand + PurposeTurn + PurposeSecure + PurposeRecord + PurposeFasten +
            TrainingStoppedTitle + TrainingStopped +
            FinalCheckTitle + FinalCheckDescription + FinalItemAttached + FinalItemBand + FinalItemTurn + FinalItemSecured +
            FinalItemRecorded + FinalItemFastened + FinalItemMetFormat + FinalItemUnmetFormat + FinalCheckButton + FinalCheckReady +
            FinalCheckUnmet + FinalCheckBlocked + FinalCheckRetry + FinalCheckCancelled + FinalCheckAccepted +
            CompletionTitle + CompletionItemDoneFormat + CompletionItemNotDoneFormat + CompletionNote + RetryButton + RetryFailed +
            StatusIdle + StatusHeldBlocked + StatusHeldPlaceable + StatusAttached +
            ReasonPrefix + PlaceableDetail + FailedPrefix + RetryHint + AttachedDetail + PreviewPlaceable + PreviewBlocked +
            ReasonOutOfRange + ReasonOutsideAllowedZone + ReasonOffLimbCenter + ReasonWrongAxis + ReasonWrongRoll +
            ReasonForbiddenZone + ReasonOtherTarget + ReasonTrackingLost + ReasonSelectionCanceled + ReasonReleasePoseUnavailable +
            InstructionPullBand + InstructionTurnWindlass + InstructionSeatWindlass + InstructionRecordTime + InstructionCoverStrap +
            InstructionPartsDone + StatusPullBandFormat + StatusTurnWindlassFormat + StatusTurnReady + StatusSeatLowering +
            StatusSeatEnteringFormat + StatusRecordTime + StatusCoverStrapFormat + StatusStrapCovered + StatusPartsDone + TimeNotRecorded +
            TimeRecordedFormat + TimeLabelFormat + "0123456789" + IssueTrackingLost + IssueSelectionCanceled + IssuePullIncomplete +
            IssueTurnIncomplete + IssueSeatIncomplete + IssueCoverIncomplete + IssuePartsUnavailable + IssueReleaseInputUnavailable;
    }
}
