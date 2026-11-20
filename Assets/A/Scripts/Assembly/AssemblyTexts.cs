namespace MilTrainSimVR.A.Assembly
{
    // 분해·결합 화면 문구. 문구는 여기에만 둔다.
    public static class AssemblyTexts
    {
        // AssemblySequence 의 단계 순서와 같은 순서 — 제목 칸 (짧게. 자세한 방법은 StepGuides, 11/19)
        // 번호는 패널 위 단계 칸 번호(1~12)와 같다. 분해 · 결합은 머리줄에 (Phase)
        public static readonly string[] Steps =
        {
            "1. 가스 조절기 분리",
            "2. 총기 본체 열기",
            "3. 밀대 뭉치 분리",
            "4. 장전 손잡이 분리",
            "5. 가스 활대 분리",
            "6. 노리쇠 뭉치 분리",
            "7. 노리쇠 뭉치 결합",
            "8. 가스 활대 삽입",
            "9. 장전 손잡이 삽입",
            "10. 밀대 뭉치 삽입",
            "11. 총기 본체 닫기",
            "12. 가스 조절기 결합",
        };

        public const string DisassemblyPhase = "분해";
        public const string AssemblyPhase = "결합";

        // 학습 모드 단계 안내 — 안내 칸 (11/19). Steps 와 같은 순서
        public static readonly string[] StepGuides =
        {
            "총열 앞쪽의 반짝이는 가스 조절기를 그립 버튼으로 잡으세요.\n화살표 방향으로 끝까지 당기면 빠집니다.\n뺀 부품은 작업대 매트 위에 내려놓으세요.",
            "상부 본체를 그립 버튼으로 잡으세요.\n화살표 방향으로 돌리면 경첩을 축으로 본체가 열립니다.\n끝까지 열면 다음 단계로 넘어갑니다.",
            "반짝이는 밀대 뭉치를 그립 버튼으로 잡으세요.\n화살표 방향으로 끝까지 당겨 빼고 매트 위에 내려놓으세요.",
            "반짝이는 장전 손잡이를 잡으세요.\n화살표 방향으로 끝까지 당기면 홈에서 빠집니다.",
            "가스 활대를 잡고 화살표 방향으로 끝까지 빼세요.\n노리쇠 뭉치는 가스 활대에 붙은 채로 함께 나옵니다.",
            "빼낸 가스 활대에서 노리쇠 뭉치를 분리합니다.\n반짝이는 노리쇠 뭉치를 잡고 화살표 방향으로 빼세요.\n이것으로 분해가 끝납니다.",
            "이제 분해의 반대 순서로 결합합니다.\n노리쇠 뭉치를 들어 가스 활대의 반투명 자리 표시에 대세요.\n축에 물리면 화살표 방향으로 끝까지 밀어 끼우세요.",
            "노리쇠 뭉치를 끼운 가스 활대를 들어 반투명 자리 표시에 대세요.\n축에 물리면 화살표 방향으로 끝까지 밀어 넣으세요.",
            "장전 손잡이를 들어 반투명 자리 표시에 대세요.\n축에 물리면 화살표 방향으로 끝까지 밀어 넣으세요.",
            "밀대 뭉치를 들어 반투명 자리 표시에 대세요.\n축에 물리면 화살표 방향으로 끝까지 밀어 넣으세요.",
            "상부 본체를 잡고 화살표 방향으로 돌려 닫으세요.\n끝까지 닫히면 다음 단계로 넘어갑니다.",
            "가스 조절기를 들어 총열 앞쪽의 반투명 자리 표시에 대세요.\n끝까지 밀어 끼우면 결합이 끝나고 기능 점검으로 넘어갑니다.",
        };

        public static string StepText(int index) => index >= 0 && index < Steps.Length ? Steps[index] : "";

        public static string StepGuide(int index) => index >= 0 && index < StepGuides.Length ? StepGuides[index] : "";

        // 평가 모드 안내 — 무엇을 할지는 알려 주지 않는다
        public const string EvaluationGuide = "평가 모드입니다. 부품 강조와 단계 안내 없이 스스로 진행하세요.\n순서가 틀리면 오류로 기록되고, 걸린 시간과 오류 수로 등급이 정해집니다.";

        public const string WrongOrderRemove = "지금은 그 부품을 움직일 순서가 아닙니다";
        public const string WrongOrderInsert = "아직 그 부품을 결합할 순서가 아닙니다";
        public const string NowDoHead = "지금 할 일: ";
        public const string DisassemblyDone = "분해 완료! 이번에는 반대 순서로 결합합니다";
        public const string CompletedTitle = "분해·결합 완료";

        // 교육 흐름 (AssemblyTrainingFlow)
        public const string IntroTitle = "총기 분해·결합";
        public const string IntroGuide = "K2C1 소총을 분해한 뒤 다시 결합하고, 마지막에 기능 점검을 합니다.\n· 학습 모드: 움직일 부품이 반짝이고 화살표가 방향을 알려 줍니다\n· 평가 모드: 안내 없이 진행하고, 시간과 오류 수로 등급을 매깁니다\n버튼은 레이로 가리키고 트리거로 누르며, 부품은 그립 버튼으로 잡습니다.";
        public const string FunctionCheckTitle = "기능 점검";
        public const string FunctionCheckIntro = "결합 완료! 이제 총이 제대로 작동하는지 기능 점검을 합니다";

        // FunctionCheckSequence 의 단계 순서와 같은 순서 (FunctionCheckStep) — 제목 칸. 번호는 단계 칸 번호(1~6)와 같다
        public static readonly string[] FunctionCheckSteps =
        {
            "기능 점검 1. 총 들기",
            "기능 점검 2. 조정간 안전 확인",
            "기능 점검 3. 장전 손잡이 당겼다 놓기",
            "기능 점검 4. 조정간 단발",
            "기능 점검 5. 빈 격발",
            "기능 점검 6. 조정간 안전",
        };

        // 학습 모드 기능 점검 안내 — FunctionCheckSteps 와 같은 순서
        public static readonly string[] FunctionCheckGuides =
        {
            "작업대 위의 총을 권총손잡이 쪽으로 잡으세요 (그립 버튼).\n점검하는 동안 총구는 사람이 없는 쪽을 향하게 합니다.",
            "조정간이 안전인지 확인합니다.\n총을 잡은 손의 트리거로 방아쇠를 당겨 보세요.\n격발되지 않으면 정상이고 다음 단계로 넘어갑니다.",
            "다른 손으로 장전 손잡이를 잡으세요.\n뒤로 끝까지 당겼다가 놓으면 노리쇠가 앞으로 돌아갑니다.",
            "총을 잡은 손의 B 또는 Y 버튼을 한 번 눌러 조정간을 단발로 돌리세요.\n위쪽 머리줄 끝에 지금 조정간이 보입니다.\n한 번 더 누르면 연발이 되니 주의하세요.",
            "총구를 위(하늘 쪽)로 향하고 방아쇠를 당기세요.\n탄이 없으므로 소리 없이 손에 약한 진동만 옵니다.",
            "B 또는 Y 버튼을 눌러 조정간을 다시 안전으로 돌리세요.\n단발 → 연발 → 안전 순서로 바뀝니다. 머리줄에서 \"안전\"을 확인하세요.",
        };

        public static string FunctionCheckStepText(int index) => index >= 0 && index < FunctionCheckSteps.Length ? FunctionCheckSteps[index] : "";

        public static string FunctionCheckGuide(int index) => index >= 0 && index < FunctionCheckGuides.Length ? FunctionCheckGuides[index] : "";

        public static string MistakeText(FunctionCheckMistake mistake) => mistake switch
        {
            FunctionCheckMistake.TriggerOnSafe => "조정간이 안전이라 방아쇠가 당겨지지 않습니다",
            FunctionCheckMistake.FiredOutOfOrder => "아직 격발할 순서가 아닙니다",
            FunctionCheckMistake.HammerDown => "공이치기가 떨어져 있습니다. 장전 손잡이를 다시 끝까지 당겼다 놓으세요",
            FunctionCheckMistake.NotSemi => "조정간이 단발이 아닙니다 (지금 연발).\n단발로 돌리고 장전 손잡이를 다시 당긴 뒤, 위를 향해 다시 당기세요",
            FunctionCheckMistake.NotAimedUp => "총구를 위로 향하고 격발해야 합니다.\n장전 손잡이를 다시 당긴 뒤, 위를 향해 다시 당기세요",
            _ => "조정간을 안전으로 돌린 뒤 방아쇠를 당기세요",
        };
        public const string ResultSummary = "{0} · 소요 시간 {1:00}:{2:00} · 오류 {3}회";
        public const string LearningMode = "학습 모드";
        public const string EvaluationMode = "평가 모드";
        // 평가 결과 (AssemblyGrading)
        public const string EvaluationResultTitle = "평가 결과 · {0}";
        public const string EvaluationSummary = "소요 시간 {0:00}:{1:00} · 오류 {2}회";
        public const string StepErrorsHead = "오류 단계: ";
        public const string NoStepErrors = "오류 난 단계 없음";
        public const string GradeCriteriaLine = "기준 · 우수 {0:00}:{1:00} 안 오류 {2}회 이하 · 합격 {3:00}:{4:00} 안 오류 {5}회 이하";

        public static string GradeName(AssemblyGrade grade) => grade switch
        {
            AssemblyGrade.Excellent => "우수",
            AssemblyGrade.Pass => "합격",
            _ => "불합격",
        };

        public const string RetryButton = "다시 하기";
        public const string HomeButton = "처음으로";
    }
}
