using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MilTrainSimVR.A.Rifle
{
    // 총을 쥔 두 손 (11/17 총기 통합 1단계 — 두 손 파지). 총에 붙은 손 컴포넌트(Assets/A/Scripts/Hands/K2C1RifleHands)가 이 약속을 지킨다.
    // A 총 코드(이 asmdef)는 C 손(ControllerHandPose)을 볼 수 없어서, 손이 알려 주는 것만 이걸로 받는다:
    //   · 받치는 손 — 조준 확대 모드의 숨 참기가 그 손의 트리거를 본다 (SightAim)
    //   · 레일(받치는 손) 자리 — 꽂힌 탄창 빼기가 이 자리는 양보한다 (MagazineWell)
    public interface IRifleSupport
    {
        // 지금 총열덮개를 받치고 있는 손 (없으면 null)
        NearFarInteractor SupportHand { get; }

        // 손의 잡기 점이 레일(받치는 손) 자리 안인지
        bool InSupportZone(NearFarInteractor hand);
    }
}
