namespace MilTrainSimVR.A.Rifle
{
    // 사격용 총 문구. 문구는 여기에만 둔다.
    public static class RifleTexts
    {
        public const string Safe = "안전";
        public const string Semi = "단발";
        public const string Burst = "점사";
        public const string Auto = "연발";

        public static string ModeName(K2C1Rifle.FireMode mode) => mode switch
        {
            K2C1Rifle.FireMode.Safe => Safe,
            K2C1Rifle.FireMode.Semi => Semi,
            K2C1Rifle.FireMode.Burst => Burst,
            _ => Auto,
        };

        // 패널 머리줄 (11/03) — 예: "조정간 단발"
        public static string SelectorLine(K2C1Rifle.FireMode mode) => "조정간 " + ModeName(mode);
    }
}
