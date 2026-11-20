using System.Collections.Generic;

namespace MilTrainSimVR.B.ProtectiveClothing
{
    // On-screen text of the protective clothing equipment scene (one short action line and the development reset button). Leave TMP
    // text fields empty in the Inspector. The wear distance is a VR handling value, not a training criterion; nothing here states a
    // product, certification or protection level.
    public static class ProtectiveClothingSubtitles
    {
        public const string GrabAny = "착용할 장비를 잡으세요.";
        public const string BringJacket = "상의를 몸통 표시로 가져가세요.";
        public const string BringTrousers = "하의를 허리 표시로 가져가세요.";
        public const string BringGloveLeft = "왼쪽 장갑을 왼손 표시로 가져가세요.";
        public const string BringGloveRight = "오른쪽 장갑을 오른손 표시로 가져가세요.";
        public const string BringOverbootLeft = "왼쪽 덮개를 왼발 표시로 가져가세요.";
        public const string BringOverbootRight = "오른쪽 덮개를 오른발 표시로 가져가세요.";
        public const string Tracking = "컨트롤러를 시야 안으로 가져오세요.";
        public const string Paused = "교육 화면으로 돌아오세요.";
        public const string AllWorn = "모든 보호장비를 착용했습니다.";
        public const string ResetFailed = "초기화하지 못했습니다. 담당자에게 알려 주세요.";
        // Invalid equipment setup (references or wear radius): grabbing is blocked.
        public const string SetupInvalid = "장비 설정 오류로 사용할 수 없습니다. 담당자에게 알려 주세요.";
        // Valid equipment whose component is switched off: nothing can be grabbed or worn until it is on again (no action asked).
        public const string EquipmentPaused = "장비 사용이 일시 중지되었습니다.";
        public const string ResetButton = "초기화";

        public static string Bring(ProtectiveClothingPart part) => part switch
        {
            ProtectiveClothingPart.Jacket => BringJacket,
            ProtectiveClothingPart.Trousers => BringTrousers,
            ProtectiveClothingPart.GloveLeft => BringGloveLeft,
            ProtectiveClothingPart.GloveRight => BringGloveRight,
            ProtectiveClothingPart.OverbootLeft => BringOverbootLeft,
            _ => BringOverbootRight
        };

        // Every line, for the font glyph and text fit checks of the setup validation.
        public static IEnumerable<string> All => new[]
        {
            GrabAny, BringJacket, BringTrousers, BringGloveLeft, BringGloveRight, BringOverbootLeft, BringOverbootRight, Tracking, Paused,
            AllWorn, ResetFailed, SetupInvalid, EquipmentPaused, ResetButton
        };

        public static readonly string[] Lines =
        {
            GrabAny, BringJacket, BringTrousers, BringGloveLeft, BringGloveRight, BringOverbootLeft, BringOverbootRight, Tracking, Paused,
            AllWorn, ResetFailed, SetupInvalid, EquipmentPaused
        };
    }
}
