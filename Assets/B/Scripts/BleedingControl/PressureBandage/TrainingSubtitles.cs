using System.Collections.Generic;
using System.Linq;

namespace MilTrainSimVR.B.BleedingControl.PressureBandage
{
    // Single source for on-screen text of the pressure bandage device check board (chapter 1: dressing handling and direct
    // pressure contact practice; chapter 2: bandage wrapping, end placement and taping). Leave TMP text fields empty in the
    // Inspector. Zones, paths, tolerances, the contact check time and the wrap time are development VR handling settings, not
    // medical criteria, a pressure or tension value, a bleeding control result, a treatment time or an approved wrapping method.
    public static class TrainingSubtitles
    {
        public const string BoardTitle = "직접 압박·압박붕대 · 장비 동작 확인";
        public const string Notice = "드레싱 위치·손 접촉·붕대 경로·고정 위치만 확인하는 가상 조작 연습입니다. 실제 압박력·장력·출혈·혈류는 판단하지 않습니다.";

        // Instruction: one next action from the current state.
        public const string GrabDressing = "트레이의 드레싱을 잡으세요.";
        public const string CoverWound = "드레싱의 접촉면이 상처를 향하게 하여 올바른 면으로 상처를 덮으세요.";
        public const string KeepDressing = "드레싱을 잡아 상처 위에 유지하세요.";
        public const string StackHands = "두 손을 드레싱 위에 겹쳐 대고 두 손의 접촉 동작을 유지하세요.";
        public const string KeepTwoHands = "두 손의 접촉 동작을 유지하세요.";
        public const string ContactBroken = "접촉이 끊겼습니다. 두 손을 드레싱 위에 다시 모으세요.";
        public const string CheckTracking = "손 추적을 확인하세요. 컨트롤러를 시야 안으로 가져오세요.";
        public const string InputPaused = "입력이 일시 중단되었습니다. 앱으로 돌아오면 다시 확인합니다.";
        public const string PracticeDone = "직접 압박 동작 연습 완료. 두 손의 접촉 동작을 계속 연습할 수 있습니다.";
        // One hand holds the dressing validly before any bandage step: both next actions are offered.
        public const string StackHandsOrRoll = "두 손을 드레싱 위에 겹쳐 접촉을 연습하거나, 롤붕대를 잡아 감기 영역에서 감기를 시작하세요.";

        // Instruction, bandage (chapter 2): the first missing condition of the current stage. The dressing need not be held
        // from the wrap on; it only has to cover the wound.
        public const string ReplaceDressing = "드레싱을 상처 위에 다시 올바르게 덮으세요.";
        public const string GrabRoll = "트레이의 롤붕대를 잡으세요.";
        public const string PutTapeDownGrabRoll = "롤붕대를 잡을 손에 테이프가 있습니다. 테이프를 먼저 내려놓고 그 손으로 롤붕대를 잡으세요.";
        public const string PutTapeDownRegrabRoll = "롤붕대를 잡을 손에 테이프가 있습니다. 테이프를 먼저 내려놓고 롤붕대를 다시 잡으세요. 감긴 부분은 그대로 유지됩니다.";
        public const string PutTapeDownGrabEnd = "감기 완료. 붕대 끝을 잡을 손에 테이프가 있습니다. 테이프를 먼저 내려놓고 붕대 끝을 잡으세요.";
        public const string RegrabRoll ="롤붕대를 다시 잡으세요. 감긴 부분은 그대로 유지됩니다.";
        public const string CheckRollTracking = "롤붕대를 잡은 손의 추적을 확인하세요. 추적이 돌아오면 감긴 만큼에서 이어 갑니다.";
        public const string MoveRollToZone = "롤붕대를 팔 위에 표시된 감기 영역으로 가져가세요.";
        public const string HoldRollInZone = "감기 영역 안에서 롤붕대를 유지하세요. 붕대가 팔에 감깁니다.";
        public const string WrapNotAllowed = "아직 감기를 시작할 순서가 아닙니다.";
        public const string DressingShifted = "감긴 붕대 아래에서 드레싱이 크게 밀리거나 돌아가 이번 감김과 고정이 취소되었습니다. 드레싱을 다시 덮고 붕대를 처음부터 감으세요.";
        public const string DressingDisplaced ="드레싱이 상처에서 벗어나 이번 감김과 고정이 취소되었습니다. 드레싱을 다시 덮고 붕대를 처음부터 감으세요.";
        public const string GrabEnd = "감기 완료. 롤에서 분리된 붕대 끝을 잡으세요.";
        public const string PlaceEnd = "붕대 끝을 표시 가까이 가져가세요. 가까워지면 자동으로 붙습니다.";
        public const string EndRejected = "붕대 끝이 표시에서 먼 곳에 놓였습니다. 다시 잡아 표시 가까이 가져가세요.";
        public const string GrabTape = "트레이의 테이프를 잡으세요.";
        public const string PlaceTape = "테이프를 붕대 끝의 표시 가까이 가져가세요. 가까워지면 자동으로 붙습니다.";
        public const string TapeRejected = "테이프가 표시에서 먼 곳에 놓였습니다. 다시 잡아 표시 가까이 가져가세요.";
        public const string FixNeedsPlacement = "드레싱이 상처를 덮은 상태에서 다시 맞춰 놓으세요.";
        public const string ApplicationDone = "장비 적용 조작 완료. 손을 놓아도 감김과 고정이 유지됩니다. 교육 전체 완료가 아닌 장비 동작 확인입니다.";

        // Status lines: current state only.
        public const string PlacementValid = "드레싱 배치: 유효 · 상처를 덮음";
        public const string PlacementInvalidPrefix = "드레싱 배치: 무효 · ";
        public const string ReasonNone = "판정할 상처가 없음";
        public const string ReasonAway = "상처 위치가 아님";
        public const string ReasonWrongFace = "반대 면이 상처를 향함";
        public const string ReasonTilted = "드레싱이 기울어짐";
        public const string ReasonLifted = "상처 표면에서 떨어짐";
        public const string ReasonTooDeep = "팔 안쪽으로 너무 들어감";
        public const string ReasonNotCovering = "상처를 다 덮지 못함";
        public const string HoldPrefix = "드레싱 유지: ";
        public const string HoldValid = "유지 중";
        public const string HoldHeldOnly = "잡고 있음 · 유지 조건 미충족";
        public const string HoldNone = "유지하는 손 없음";
        // From the end of the wrap: the dressing stays under the bandage by itself (no hand needed).
        public const string HoldFixedByBandage = "붕대로 고정됨";
        public const string TwoHandValid = "두 손 접촉: 유효";
        public const string TwoHandInvalid = "두 손 접촉: 무효";
        public const string HandsPrefix = "손 상태: ";
        public const string LeftHand = "왼손";
        public const string RightHand = "오른손";
        public const string HandTracked = "추적됨";
        public const string HandNotTracked = "추적 안 됨";
        public const string HandMissing = "연결 없음";
        public const string HandInZone = "드레싱 위";
        public const string Separator = " · ";
        public const string HandSeparator = " / ";

        // Bandage status lines: current application state only.
        public const string BandagePrefix = "붕대 적용: ";
        public const string StageReady = "시작 전";
        public const string StageWrapping = "감기 진행 ";
        public const string StageEnd = "감기 완료 · 붕대 끝 배치 대기";
        public const string StageTape = "붕대 끝 배치 완료 · 테이프 고정 대기";
        public const string StageFixed = "테이프 고정 완료 · 장비 적용 조작 완료";
        public const string RollPrefix = "롤붕대: ";
        public const string RollNotHeld = "잡지 않음";
        public const string RollDetached = "롤에서 분리됨";
        public const string StripOnHold = " · 연결 띠 표시 보류(팔에 너무 가까움)";
        public const string WrapPrefix = "진행 조건: ";
        public const string WrapAllowed = "충족";
        public const string BlockFocus = "입력 중단";
        public const string BlockPlacement = "드레싱 배치 무효";
        public const string BlockRoll = "롤붕대를 잡지 않음";
        public const string BlockRollTracking = "롤붕대 잡은 손 추적 안 됨";
        public const string BlockZone = "롤붕대가 감기 영역 밖";
        public const string BlockLocked = "교육 순서상 시작 전";
        public const string EndPrefix = "붕대 끝: ";
        public const string TapePrefix = "테이프: ";
        public const string FixValid = "표시 위치에 맞음";
        public const string FixInvalidPrefix = "맞지 않음 · ";
        public const string FixAway = "표시 위치가 아님";

        // Progress of the continuous two-hand contact (a ratio, never shown as seconds).
        public const string ProgressPrefix = "연속 접촉 확인: ";
        public const string ProgressPercentSuffix = "%";
        public const string ProgressIdle = "두 손 접촉이 유효할 때만 진행";

        // History of this run, shown apart from the current state.
        public const string HistoryDone = "이번 회차 기록: 직접 압박 동작 연습 완료";
        public const string HistoryNone = "이번 회차 기록: 아직 없음";

        // ---------- Training course (chapter 3): start, step row (7 steps), one action at a time, checks, completion ----------

        public const string CourseTitle = "직접 압박·압박붕대 가상 조작 체험";
        public const string CourseDescription =
            "상처를 드레싱으로 덮고 직접 압박한 뒤 붕대로 감아 고정하는 순서를 7단계로 연습합니다.\n" +
            "위치와 순서만 확인하는 기본형 가상 연습입니다. 감기 구간은 출혈이 조절된 상태를 가정하며, 실제 압박력·장력·지혈·혈류는 판단하지 않습니다.";
        public const string StartReady = "준비되면 교육 시작 버튼을 누르세요.";
        public const string StartButton = "교육 시작";
        public const string StartUnavailable = "지금은 교육을 시작할 수 없습니다. 담당자에게 알려 주세요.";
        public const string RetryFailed = "장비를 처음 상태로 되돌리지 못했습니다. 담당자에게 알려 주세요.";

        // Step row line: number, count and short step name. The recovery form is used when an earlier step's action must be
        // redone while the course already waits on a later step.
        public const string StepLineFormat = "{0} / {1} · {2}";
        public const string StepRecoveryFormat = "{0} / {1} · {2} · 복구";
        public const string StepSituation = "상황·도구 확인";
        public const string StepDressing = "드레싱 배치";
        public const string StepContact = "직접 압박";
        public const string StepPreCheck = "감기 전 확인";
        public const string StepWrap = "붕대 감기";
        public const string StepFix = "끝·테이프 고정";
        public const string StepPostCheck = "처치 후 확인";

        // Purpose line of each step (course board, above the action), from the step table of the course design (Docs/B
        // 1102_압박붕대_교육과정_설계, section of the course steps) and the course description; virtual handling only (no force, time,
        // bleeding control or circulation judgement).
        public const string PurposeSituation = "상처 위치와 드레싱·붕대의 역할을 확인합니다.";
        public const string PurposeDressing = "드레싱의 올바른 면이 상처를 향하게 하여 상처 위에 둡니다.";
        public const string PurposeContact = "드레싱 위에 두 손을 겹쳐 대는 직접 압박 동작을 연습합니다. 실제 압박력은 판단하지 않습니다.";
        public const string PurposePreCheck = "한 손으로 드레싱을 유지하며 감기 전에 손 쪽 상태 정보를 확인합니다.";
        public const string PurposeWrap = "롤붕대를 감기 영역에 유지해 드레싱 위로 팔 주위를 감습니다.";
        public const string PurposeFix = "감기가 끝난 붕대 끝을 지정 위치에 두고 테이프로 고정합니다.";
        public const string PurposePostCheck = "고정한 뒤 재출혈 표시와 손 쪽 상태 정보를 확인합니다.";
        public static readonly string[] CoursePurposes =
        {
            PurposeSituation, PurposeDressing, PurposeContact, PurposePreCheck, PurposeWrap, PurposeFix, PurposePostCheck
        };

        // Item names (completion summary).
        public const string ItemSituation = "상황·도구 확인";
        public const string ItemDressing = "드레싱 배치";
        public const string ItemContact = "직접 압박 접촉 동작 연습";
        public const string ItemPreCheck = "감기 전 상태 확인";
        public const string ItemWrap = "붕대 감기";
        public const string ItemFix = "붕대 끝·테이프 고정";
        public const string ItemPostCheck = "처치 후 상태 확인";

        // Course next action (practice screen): one short action at a time, never a choice of two.
        public const string ActPaused = "앱 화면으로 돌아오세요.";
        public const string ActTracking = "컨트롤러를 시야 안으로 가져오세요.";
        public const string ActCheckSituation = "상황과 도구를 확인하세요.";
        public const string ActGrabDressing = "드레싱을 잡으세요.";
        public const string ActCoverWound = "드레싱으로 상처를 덮으세요.";
        public const string ActFlipDressing = "드레싱을 뒤집어 접촉면으로 상처를 덮으세요.";
        public const string ActStackHands = "두 손을 드레싱 위에 모아 유지하세요.";
        public const string ActKeepHands = "두 손을 그대로 유지하세요.";
        // Direct pressure progress line under the action (whole percent of PressureDressing.ContactProgress).
        public const string ContactProgressFormat = "압박 유지 {0}%";
        public const string ActHoldDressing = "한 손으로 드레싱을 잡아 유지하세요.";
        public const string ActPutTapeDown = "테이프를 먼저 내려놓으세요.";
        // Wrap (hold to wrap): grab the roll, bring it into the shown zone, keep it there.
        public const string ActGrabRoll = "롤붕대를 잡으세요.";
        public const string ActRegrabRoll = "롤붕대를 다시 잡으세요.";
        public const string ActMoveRollToZone = "롤붕대를 표시된 영역으로 가져가세요.";
        public const string ActHoldRollInZone = "영역 안에서 롤붕대를 유지하세요.";
        // Wrap progress line under the action (whole percent of PressureBandageApplication.WrapProgress).
        public const string WrapProgressFormat = "붕대 감기 {0}%";
        // End: it attaches by itself when brought near its mark (distance only), so there is no face, tilt, turn or release line.
        public const string ActGrabEnd = "붕대 끝을 잡으세요.";
        public const string ActRegrabEnd = "붕대 끝을 다시 잡으세요.";
        public const string ActEndToMark = "붕대 끝을 표시 가까이 가져가세요.";
        // After the training field's stop confirm with the end already at its mark: it attaches after a new approach.
        public const string ActEndAwayFirst = "붕대 끝을 표시에서 뗐다가 다시 가져가세요.";
        // Tape: the device fixes it only when released in a valid pose, so while held the line follows the current pose check
        // (position, face, tilt, direction) and asks to release only when it is valid.
        public const string ActGrabTape = "테이프를 잡으세요.";
        public const string ActRegrabTape = "테이프를 다시 잡으세요.";
        public const string ActTapeToMark = "테이프를 표시 가까이 가져가세요.";
        // After the training field's stop confirm with the tape already at its mark: it attaches after a new approach.
        public const string ActTapeAwayFirst = "테이프를 표시에서 뗐다가 다시 가져가세요.";
        public const string ActCheckColor = "손 쪽 색 정보를 확인하세요.";
        public const string ActCheckWarmth = "손 쪽 온도 정보를 확인하세요.";
        public const string ActCheckSensation = "손가락 감각 응답을 확인하세요.";
        public const string ActCheckBleeding = "재출혈 표시를 확인하세요.";
        public const string WaitingAdvance = "다음 단계로 넘어가는 중입니다.";

        // Check card (practice screen): the current item only. Hand-side information is a virtual casualty status card or the
        // casualty's answer, never a controller measurement.
        public const string CheckTitleSituation = "상황·도구";
        public const string CheckTitleFormat = "가상 {0} · {1}/{2}";
        public const string CheckSourceCard = "상태 카드";
        public const string CheckSourceAnswer = "부상자 응답";
        public const string SituationInfo = "안전한 훈련 공간 · 왼쪽 아래팔 윗면 상처(가상)\n드레싱: 상처 덮기 · 롤붕대: 드레싱 고정\n테이프: 붕대 끝 고정";
        public const string PreInfoColor = "평소 피부색과 같음";
        public const string PreInfoWarmth = "반대쪽 손과 비슷하게 따뜻함";
        public const string PreInfoSensation = "저리거나 둔한 느낌이 없음";
        public const string PostInfoBleeding = "붕대 위로 새로 번지는 출혈 표시 없음";
        public const string PostInfoSame = "감기 전과 같음";
        public const string PostInfoSensation = "저림이나 조이는 통증이 없음";
        public const string CheckButton = "확인";
        // Refusal lines of PressureBandageTrainingLink.CanConfirmCheck (API only; the screen shows the recovery action instead).
        public const string CheckRefusedHold = "드레싱 유지가 끊겨 확인을 받지 않았습니다. 드레싱을 유지한 뒤 다시 누르세요.";
        public const string CheckRefusedFixed = "붕대가 고정된 상태가 아니어서 확인을 받지 않았습니다.";
        public const string CheckRefusedFocus = "입력이 일시 중단되어 확인을 받지 않았습니다.";


        // Completion panel.
        public const string CompletionTitle = "직접 압박·압박붕대 가상 조작 체험 완료";
        public const string CompletionItemDoneFormat = "[수행] {0}";
        public const string CompletionItemNotDoneFormat = "[미수행] {0}";
        public const string CompletionNote = "가상 조작 연습의 수행 여부만 요약합니다. 실제 압박력·출혈량·혈류·합격 여부는 판단하지 않습니다. " +
                                             "실제 상황에서 출혈이 계속되거나 다시 시작되면 재평가가 필요합니다.";
        public const string RetryButton = "다시하기";

        // Every course action line and check information line.
        public static readonly string[] CourseActions =
        {
            ActPaused, ActTracking, ActCheckSituation, ActGrabDressing, ActCoverWound, ActFlipDressing, ActStackHands, ActKeepHands,
            ActHoldDressing, ActPutTapeDown, ActGrabRoll, ActRegrabRoll, ActMoveRollToZone, ActHoldRollInZone, ActGrabEnd, ActRegrabEnd,
            ActEndToMark, ActEndAwayFirst, ActGrabTape, ActRegrabTape, ActTapeToMark, ActTapeAwayFirst,
            ActCheckColor, ActCheckWarmth, ActCheckSensation, ActCheckBleeding, WaitingAdvance
        };
        public static readonly string[] CheckInfos =
        {
            SituationInfo, PreInfoColor, PreInfoWarmth, PreInfoSensation, PostInfoBleeding, PostInfoSame, PostInfoSensation
        };

        public static string Reason(PressureDressingCheck check)
        {
            switch (check)
            {
                case PressureDressingCheck.Away: return ReasonAway;
                case PressureDressingCheck.WrongFace: return ReasonWrongFace;
                case PressureDressingCheck.Tilted: return ReasonTilted;
                case PressureDressingCheck.Lifted: return ReasonLifted;
                case PressureDressingCheck.TooDeep: return ReasonTooDeep;
                case PressureDressingCheck.NotCovering: return ReasonNotCovering;
                default: return ReasonNone;
            }
        }

        public static string Block(PressureWrapBlock block)
        {
            switch (block)
            {
                case PressureWrapBlock.None: return WrapAllowed;
                case PressureWrapBlock.FocusLost: return BlockFocus;
                case PressureWrapBlock.DressingNotPlaced: return BlockPlacement;
                case PressureWrapBlock.RollNotHeld: return BlockRoll;
                case PressureWrapBlock.RollHandNotTracked: return BlockRollTracking;
                case PressureWrapBlock.StartLocked: return BlockLocked;
                default: return BlockZone;
            }
        }

        public static string Fix(PressureFixCheck check)
        {
            switch (check)
            {
                case PressureFixCheck.Valid: return FixValid;
                default: return FixInvalidPrefix + FixAway;
            }
        }

        public static string Tracking(PressureHandTracking tracking)
        {
            switch (tracking)
            {
                case PressureHandTracking.Tracked: return HandTracked;
                case PressureHandTracking.NotTracked: return HandNotTracked;
                default: return HandMissing;
            }
        }

        // Every string above (the Korean font must cover their glyphs).
        public static IEnumerable<string> All => new[]
        {
            BoardTitle, Notice, GrabDressing, CoverWound, KeepDressing, StackHands, KeepTwoHands, ContactBroken, CheckTracking,
            InputPaused, PracticeDone, PlacementValid, PlacementInvalidPrefix, ReasonNone, ReasonAway, ReasonWrongFace, ReasonTilted,
            ReasonLifted, ReasonTooDeep, ReasonNotCovering, HoldPrefix, HoldValid, HoldHeldOnly, HoldNone, HoldFixedByBandage, TwoHandValid,
            TwoHandInvalid, HandsPrefix, LeftHand, RightHand, HandTracked, HandNotTracked, HandMissing, HandInZone, Separator,
            HandSeparator, ProgressPrefix, ProgressPercentSuffix, ProgressIdle, HistoryDone, HistoryNone, StackHandsOrRoll,
            ReplaceDressing, GrabRoll, RegrabRoll, CheckRollTracking, MoveRollToZone, HoldRollInZone, WrapNotAllowed, DressingDisplaced,
            GrabEnd, PlaceEnd, EndRejected, GrabTape, PlaceTape, TapeRejected, FixNeedsPlacement, ApplicationDone, BandagePrefix,
            StageReady, StageWrapping, StageEnd, StageTape, StageFixed, RollPrefix, RollNotHeld, RollDetached, WrapPrefix, WrapAllowed,
            BlockFocus, BlockPlacement, BlockRoll, BlockRollTracking, BlockZone, BlockLocked, EndPrefix,
            TapePrefix, FixValid, FixInvalidPrefix, FixAway, DressingShifted,
            StripOnHold, PutTapeDownGrabRoll, PutTapeDownRegrabRoll, PutTapeDownGrabEnd, CourseTitle, CourseDescription, StartReady,
            StartButton, StartUnavailable, RetryFailed, StepSituation, StepDressing, StepContact, StepPreCheck, StepWrap, StepFix,
            StepPostCheck, StepLineFormat, StepRecoveryFormat, ItemSituation, ItemDressing, ItemContact, ItemPreCheck, ItemWrap,
            ItemFix, ItemPostCheck, CheckTitleSituation, CheckTitleFormat, CheckSourceCard, CheckSourceAnswer, CheckButton,
            CheckRefusedHold, CheckRefusedFixed, CheckRefusedFocus, CompletionTitle, CompletionItemDoneFormat,
            CompletionItemNotDoneFormat, CompletionNote, RetryButton, ContactProgressFormat, WrapProgressFormat, "0123456789"
        }.Concat(CourseActions).Concat(CheckInfos).Concat(CoursePurposes);
    }
}
