using System.Collections.Generic;

namespace MilTrainSimVR.B.ProtectiveClothing
{
    // On-screen text of the protective clothing equipment scene (one short action line and the development reset button). Leave TMP
    // text fields empty in the Inspector. The wear zones are VR handling areas on the estimated body, not training criteria; the
    // overboot zone sits below that side's knee on the estimated leg (not leg tracking; the worn overboot is still shown on the
    // foot). Nothing here states a
    // product, certification or protection level.
    public static class ProtectiveClothingSubtitles
    {
        public const string GrabAny = "착용할 장비를 잡으세요.";
        public const string BringJacket = "상의를 가슴 앞 착용 영역으로 가져가세요.";
        public const string BringTrousers = "하의를 허리 앞 착용 영역으로 가져가세요.";
        public const string BringGloveLeft = "왼쪽 장갑을 왼손으로 가져가세요.";
        public const string BringGloveRight = "오른쪽 장갑을 오른손으로 가져가세요.";
        public const string BringOverbootLeft = "왼쪽 덮개를 왼쪽 무릎 아래 표시 근처로 가져가세요.";
        public const string BringOverbootRight = "오른쪽 덮개를 오른쪽 무릎 아래 표시 근처로 가져가세요.";
        // A glove held by the hand it is meant for: take it with the other hand.
        public const string SwapGloveLeft = "왼쪽 장갑은 오른손으로 잡아 왼손에 가져가세요.";
        public const string SwapGloveRight = "오른쪽 장갑은 왼손으로 잡아 오른손에 가져가세요.";
        // The guided part (jacket, trousers or a glove; never an overboot) was taken (or taken over) inside its zone: it is worn
        // only after it has been a little outside it.
        public const string MoveAway = "장비를 표시된 영역 밖으로 조금 떼세요.";
        public const string Tracking = "컨트롤러를 시야 안으로 가져오세요.";
        // The headset's tracking is lost or returning: the body and its zones are estimated from it.
        public const string HeadTracking = "헤드셋 추적이 돌아올 때까지 기다려 주세요.";
        public const string Paused = "교육 화면으로 돌아오세요.";
        public const string AllWorn = "모든 보호장비를 착용했습니다.";
        public const string ResetFailed = "초기화하지 못했습니다. 담당자에게 알려 주세요.";
        // Invalid equipment setup (references, user body or wear zones): grabbing is blocked.
        public const string SetupInvalid = "장비 설정 오류로 사용할 수 없습니다. 담당자에게 알려 주세요.";
        // Valid equipment whose component is switched off: nothing can be grabbed or worn until it is on again (no action asked).
        public const string EquipmentPaused = "장비 사용이 일시 중지되었습니다.";
        // The user body is switched off or not running: nothing can be grabbed or worn until it runs again (no action asked).
        public const string BodyPaused = "착용 기능이 일시 중지되었습니다.";
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

        public static string SwapGlove(ProtectiveClothingPart part) => part == ProtectiveClothingPart.GloveLeft ? SwapGloveLeft : SwapGloveRight;

        // Every line (the Korean font must cover their glyphs and each must fit its text area).
        public static IEnumerable<string> All => new[]
        {
            GrabAny, BringJacket, BringTrousers, BringGloveLeft, BringGloveRight, BringOverbootLeft, BringOverbootRight, SwapGloveLeft,
            SwapGloveRight, MoveAway, Tracking, HeadTracking, Paused, AllWorn, ResetFailed, SetupInvalid, EquipmentPaused, BodyPaused, ResetButton
        };

        public static readonly string[] Lines =
        {
            GrabAny, BringJacket, BringTrousers, BringGloveLeft, BringGloveRight, BringOverbootLeft, BringOverbootRight, SwapGloveLeft,
            SwapGloveRight, MoveAway, Tracking, HeadTracking, Paused, AllWorn, ResetFailed, SetupInvalid, EquipmentPaused, BodyPaused
        };
    }
}
