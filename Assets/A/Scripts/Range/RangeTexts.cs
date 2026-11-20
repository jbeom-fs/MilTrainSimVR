namespace MilTrainSimVR.A.Range
{
    // 사로 사격 화면 문구. 문구는 여기에만 둔다.
    public static class RangeTexts
    {
        public const string IntroTitle = "사로 사격 · 25 m 영점사격";
        // {0} 조 수, {1} 조당 발 수
        public const string IntroGuide = "통제관 지시에 따라 25 m 영점사격을 합니다.\n① 사격 전 준비 → ② 영점사격 ({0}조 × {1}발) → ③ 사격 후 안전 검사\n· 학습 모드: 단계마다 방법을 자세히 알려 줍니다\n· 평가 모드: 안내 없이 진행하고, 탄착과 실수로 등급을 매깁니다\n버튼은 레이로 가리키고 트리거로 누릅니다.";

        // 평가 모드 안내 — 준비·안전 검사에서 무엇을 할지는 알려 주지 않는다
        public const string EvaluationGuide = "평가 모드입니다. 단계 안내 없이 스스로 진행하세요.\n절차 실수와 안전 위반은 기록되고, 끝나면 등급이 나옵니다.";

        public const string LearningMode = "학습 모드";
        public const string EvaluationMode = "평가 모드";
        public const string RetryButton = "다시 하기";
        public const string HomeButton = "처음으로";

        // 사격 전 준비 (기획 2-1절) — 제목 칸 (짧게. 자세한 방법은 PrepareGuides, 11/19)
        public static readonly string[] PrepareSteps =
        {
            "사격 전 준비 1 · 총 들기",
            "사격 전 준비 2 · 조정간 안전 확인",
            "사격 전 준비 3 · 탄창 결합",
            "사격 전 준비 4 · 장전",
            "사격 전 준비 5 · 사격 개시 — 조정간 단발",
        };

        // 학습 모드 안내 — PrepareSteps 와 같은 순서
        public static readonly string[] PrepareGuides =
        {
            "받침대 위의 총을 권총손잡이 쪽으로 잡아 드세요 (그립 버튼).\n이때부터 총구는 항상 표적 쪽(앞)을 향하게 유지합니다.",
            "조정간이 안전인지 확인합니다.\n총을 잡은 손의 트리거로 방아쇠를 당겨 보세요.\n격발되지 않으면 정상이고 다음 단계로 넘어갑니다.",
            "받침대 아래 선반에서 다른 손으로 탄창을 집으세요.\n총 아래쪽 탄창 구멍에 대고 위로 밀어 넣으세요.\n조정간은 안전인 채로 결합해야 합니다.",
            "다른 손으로 장전 손잡이를 잡으세요.\n뒤로 끝까지 당겼다가 놓으면 첫 탄이 약실에 들어갑니다.",
            "통제관: \"사격 개시!\"\n총을 잡은 손의 B 또는 Y 버튼을 한 번 눌러 조정간을 단발로 돌리세요.\n위쪽 머리줄 끝에 \"단발\"이 보이면 됩니다.",
        };

        // 영점사격 (기획 2-2절, 임시 5발 × 2조 — 11/02)
        public const string FiringTitle = "영점사격 · {0}조 / {1}조 ({2}발씩)";
        // 영점사격 안내 — 학습·평가 모두. {0} 조당 발 수
        public const string FiringGuide = "표적이 25 m에 멈추면 사격합니다.\n두 손으로 총을 잡고 가늠자 뒤로 눈을 가져가 조준하세요.\n표적지 가운데 조준점(×)을 겨누고 한 발씩 {0}발을 쏘세요.";

        // 표적 이송 — 조를 시작하면 표적이 25 m 로 가고, 멈추면 사격
        public const string TargetGoingFar = "표적이 25 m로 가는 중입니다.\n멈출 때까지 쏘지 마세요 — 지시 전 격발은 안전 위반입니다.";
        public const string TargetReady = "표적 정지 — 사격 개시!";
        // 조 사이 — 표적이 돌아오는 중 · 돌아온 뒤
        public const string TargetComingBack = "이 조 사격이 끝났습니다. 총구는 표적 쪽을 유지하세요.\n표적지가 사수 앞으로 돌아오는 중입니다. 잠시 기다리세요.";
        public const string NextGroupButton = "다음 조 시작";
        // 영점사격 중 탄이 떨어짐 (11/03)
        public const string OutOfAmmo = "탄이 떨어졌습니다 — 쏜 발까지로 이 조를 마칩니다";
        public const string NextGroupGuide = "표적지가 돌아왔습니다. 오른쪽 그림에서 탄착을 확인하세요.\n준비되면 아래 \"다음 조 시작\"을 누르세요.";

        // 사격 후 안전 검사 (기획 2-3절) — 제목 칸
        public static readonly string[] SafetyCheckSteps =
        {
            "사격 후 안전 검사 1 · 조정간 안전",
            "사격 후 안전 검사 2 · 탄창 분리",
            "사격 후 안전 검사 3 · 약실 확인",
            "사격 후 안전 검사 4 · 노리쇠 전진",
            "사격 후 안전 검사 5 · 공격발",
            "사격 후 안전 검사 6 · 조정간 안전",
        };

        // 학습 모드 안내 — SafetyCheckSteps 와 같은 순서
        public static readonly string[] SafetyCheckGuides =
        {
            "사격이 끝났습니다. 총구는 표적 쪽을 향한 채로 둡니다.\nB 또는 Y 버튼을 눌러 조정간을 안전으로 돌리세요.\n단발 → 연발 → 안전 순서로 바뀝니다.",
            "총을 잡은 손의 A 또는 X 버튼(탄창 멈치)을 눌러 탄창을 빼세요.\n빠진 탄창은 받침대나 선반에 내려놓으세요.",
            "장전 손잡이를 뒤로 끝까지 당긴 채 잡고 계세요.\n그대로 총 오른쪽 배출구를 가까이 들여다보면 약실 확인이 진행됩니다.\n약실이 비어 있는지 확인하세요.",
            "약실이 빈 것을 확인했으면 장전 손잡이를 놓으세요.\n노리쇠가 앞으로 돌아갑니다.",
            "B 또는 Y 버튼으로 조정간을 단발로 돌리세요.\n총구를 표적 쪽으로 향한 채 방아쇠를 당깁니다.\n탄이 없으므로 손에 약한 진동만 옵니다.",
            "마지막으로 B 또는 Y 버튼을 눌러 조정간을 안전으로 돌리세요.\n(단발 → 연발 → 안전) 이것으로 사로 사격이 끝납니다.",
        };

        // 조 결과 (기획 4절) — 색 점, 조 번호, 명중, 발 수, 탄착군 크기, 중심 위치. 점 색은 결과 화면 표적지 그림과 같다
        public const string GroupResult = "<color=#{5}>●</color> {0}조 {1}/{2}발 · 탄착군 {3:0.0} cm · {4}";
        public const string GroupNoHit = "<color=#{2}>●</color> {0}조 0/{1}발 · 표적지에 맞지 않음";

        // 조 색 — 1조 빨강 · 2조 파랑 · 3조 초록, 더 있으면 반복
        static readonly UnityEngine.Color[] s_GroupColors =
        {
            new UnityEngine.Color(0.90f, 0.20f, 0.20f),
            new UnityEngine.Color(0.20f, 0.45f, 0.95f),
            new UnityEngine.Color(0.15f, 0.70f, 0.30f),
        };

        public static UnityEngine.Color GroupColor(int index) => s_GroupColors[index % s_GroupColors.Length];

        public static string GroupColorHex(int index) => UnityEngine.ColorUtility.ToHtmlStringRGB(GroupColor(index));

        // 조준점에서 탄착군 중심까지 — 예: "중심 위 2.1 · 왼쪽 0.8 cm". 0.5 cm 안이면 그 방향은 빼고, 둘 다 안이면 "중심 조준점"
        public static string Offset(UnityEngine.Vector2 cm)
        {
            string v = UnityEngine.Mathf.Abs(cm.y) < 0.5f ? "" : $"{(cm.y > 0f ? "위" : "아래")} {UnityEngine.Mathf.Abs(cm.y):0.0}";
            string h = UnityEngine.Mathf.Abs(cm.x) < 0.5f ? "" : $"{(cm.x > 0f ? "오른쪽" : "왼쪽")} {UnityEngine.Mathf.Abs(cm.x):0.0}";
            if (v.Length == 0 && h.Length == 0)
                return "중심 조준점";
            return $"중심 {(v.Length == 0 ? h : h.Length == 0 ? v : $"{v} · {h}")} cm";
        }

        public static string Step(string[] steps, int index) => index >= 0 && index < steps.Length ? steps[index] : "";

        // 순서 실수 (RangeProcedure)
        public const string WrongOrder = "지금 할 순서가 아닙니다";
        public const string NowDoHead = "지금 할 일: ";

        public static string MistakeText(RangeMistake mistake) => mistake switch
        {
            RangeMistake.DryFireOutOfOrder => "아직 공격발할 순서가 아닙니다",
            RangeMistake.NotAimedDownrange => "총구를 표적 쪽으로 향하고 공격발해야 합니다.\n장전 손잡이를 다시 당긴 뒤 다시 하세요",
            RangeMistake.HammerDown => "공이치기가 떨어져 있습니다. 장전 손잡이를 다시 끝까지 당겼다 놓으세요",
            RangeMistake.NotSemi => "조정간이 단발이 아닙니다 (지금 연발).\n단발로 돌리고 장전 손잡이를 다시 당긴 뒤 다시 하세요",
            RangeMistake.AutoInZeroing => "영점사격은 단발로 쏩니다. 조정간을 단발로 돌리세요",
            RangeMistake.NotOnSafe => "조정간을 안전으로 돌린 뒤 방아쇠를 당겨 격발되지 않는지 확인하세요",
            _ => "탄창을 먼저 분리한 뒤 약실을 확인하세요",
        };

        // 안전 위반 (RangeSafetyMonitor) — 학습·평가 모두 알려 준다
        public const string ViolationHead = "안전 위반 · {0}";

        public static string ViolationText(RangeViolation violation) => violation switch
        {
            RangeViolation.PrematureFire => "지시 전 격발 — 표적이 멈추고 사격 개시 지시가 있을 때만 쏘세요",
            RangeViolation.MuzzleDirection => "총구 방향 — 총구를 표적 쪽으로 유지하세요",
            RangeViolation.InsertNotSafe => "조정간이 안전이 아닌 채 탄창 결합",
            _ => "안전 검사 전에 탄창 분리",
        };

        public static string ViolationName(RangeViolation violation) => violation switch
        {
            RangeViolation.PrematureFire => "지시 전 격발",
            RangeViolation.MuzzleDirection => "총구 방향",
            RangeViolation.InsertNotSafe => "안전 아닌 채 탄창 결합",
            _ => "안전 검사 전 탄창 분리",
        };

        // 평가 결과 — 위반 한 줄: 위반 (그때 단계)
        public const string ViolationLine = "위반: {0} ({1})";

        // 약실 확인 진행 (학습 모드)
        public const string ChamberChecking = "약실 확인 중 {0}%";

        public const string CompletedTitle = "사로 사격 완료";

        // 평가 결과 (RangeGrading)
        public const string EvaluationResultTitle = "사로 사격 평가 · {0}";
        // 표적지 그림 아래 작은 글씨
        public const string GradeCriteriaLine = "기준(임시)\n조 합격: {0}/{0}발, 탄착군 {1:0.#} cm 이하\n우수: {2}조 합격, 실수 {3}회 이하\n합격: {4}조 합격, 실수 {5}회 이하\n안전 위반 시 불합격";

        public static string GradeName(RangeGrade grade) => grade switch
        {
            RangeGrade.Excellent => "우수",
            RangeGrade.Pass => "합격",
            _ => "불합격",
        };
        // {0} 모드는 머리줄에 이미 있어 쓰지 않는다
        public const string ResultSummary = "{1:00}:{2:00} · 절차 실수 {3}회 · 안전 위반 {4}회";
    }
}
