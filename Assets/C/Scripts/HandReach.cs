using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MilTrainSimVR.UrbanCombat
{
    // 손이 어떤 자리에 와서 그립을 쥐었는지 본다 (10/28) — 가슴 탄입대 · 꽂힌 탄창 · 장전 손잡이가 같이 쓴다.
    // 그 자리들엔 충돌체를 두지 않는다: XRI 손은 트리거가 아닌 충돌체만 잡는데, 그런 충돌체를 몸 앞이나 총에 두면
    // 적 총알을 막아 주거나(가슴 앞 탄입대) 총몸과 부딪혀 든 총이 떨린다.
    // 대신 손의 잡기 점(XRI 가 잡을 물건을 찾을 때 쓰는 반지름 10cm 구의 가운데)이 그 자리에서 얼마나 떨어졌는지로 본다.
    // 잡기 점은 C 리그(C_XR Origin)에서 손 모양의 주먹 가운데(C_PalmPoint)로 옮겨 두었다 — 원래는 컨트롤러 앞쪽이라 손보다 10cm 앞이었다 (10/28 헤드셋)
    public static class HandReach
    {
        // 손의 잡기 점 — XRI 근접 캐스터의 원점. 없으면 인터랙터 위치
        public static Vector3 GrabPoint(NearFarInteractor hand)
        {
            return GrabFrame(hand).position;
        }

        // 잡기 점의 방향 — 주먹 축(앞)과 손가락 쪽(아래) 기준으로 손에 쥐여 줄 물건의 자세를 정할 때 쓴다
        public static Quaternion GrabRotation(NearFarInteractor hand)
        {
            return GrabFrame(hand).rotation;
        }

        // 그 손의 손 모양(ControllerHandPose) — 잡기 점(C_PalmPoint)이 손 모양 오브젝트 아래에 있다. 없으면 null
        public static ControllerHandPose HandVisual(NearFarInteractor hand)
        {
            return GrabFrame(hand).GetComponentInParent<ControllerHandPose>();
        }

        static Transform GrabFrame(NearFarInteractor hand)
        {
            Transform origin = hand.nearInteractionCaster != null ? hand.nearInteractionCaster.effectiveCastOrigin : null;
            return origin != null ? origin : hand.transform;
        }

        public static bool IsNear(NearFarInteractor hand, Vector3 point, float radius)
        {
            return (GrabPoint(hand) - point).sqrMagnitude <= radius * radius;
        }

        // 이번 프레임에 빈손으로 그립을 막 쥐었는지 — 자리는 쓰는 쪽이 본다. 총에선 장전 손잡이 → 레일 → 탄창 순서로 먼저 차지한다
        public static bool IsFreshGrip(NearFarInteractor hand)
        {
            return hand != null && hand.isActiveAndEnabled && !hand.hasSelection && hand.selectInput.ReadWasPerformedThisFrame();
        }

        // 손의 잡기 점이 frame 기준 상자 안인지 — 총의 레일·탄창 자리, 가슴 탄입대처럼 모양이 길쭉한 자리를 점 대신 상자로 본다
        public static bool InBox(NearFarInteractor hand, Transform frame, Bounds box)
        {
            return frame != null && box.Contains(frame.InverseTransformPoint(GrabPoint(hand)));
        }

        // 두 묶음의 충돌체가 서로 부딪히지 않게 — 총에 꽂히기 직전의 탄창과 총몸처럼 일부러 겹치는 것들
        public static void IgnorePairs(Collider[] a, Collider[] b)
        {
            foreach (Collider x in a)
            {
                foreach (Collider y in b)
                {
                    if (x != null && y != null && x != y)
                        Physics.IgnoreCollision(x, y);
                }
            }
        }
    }
}
