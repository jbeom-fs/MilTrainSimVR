using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Utilities;

namespace MilTrainSimVR.A.Assembly
{
    // 잡기 우선순위 — 부품 잡기 상자가 겹치는 곳(상부 본체 뒤쪽: 밀대뭉치·가스 활대·노리쇠 뭉치)에서 지금 단계 부품이 먼저 잡히게 한다.
    // 손에서 잡기 상자 표면까지 거리(안이면 0)에, 지금 단계가 아니면 벌점을 더한다. 지금 단계가 아닌 부품만 닿으면 그 부품이 잡혀
    // 순서 오류로 센다 (막지 않는다).
    // 이 거리는 손(Near-Far Interactor)의 정렬 방식이 InteractableBased 일 때만 쓰인다 — 기본값 SquareDistance 는 부품 중심점까지
    // 거리로 골라서, 막대 끝을 잡으면 다른 부품 중심이 더 가까워 그쪽이 잡혔다 (10/27 헤드셋). A 씬 리그는 InteractableBased 로 둔다.
    public static class AssemblyGrabPriority
    {
        // 거리 제곱(m²)에 더하는 벌점 — 손이 닿는 범위(수 cm)보다 훨씬 크게
        const float k_NotCurrentPenalty = 1f;

        public static void Apply(XRGrabInteractable grab, System.Func<bool> isCurrent)
        {
            grab.getDistanceOverride = (interactable, position) =>
            {
                XRInteractableUtility.TryGetClosestPointOnCollider(interactable, position, out DistanceInfo info);
                if (!isCurrent())
                    info.distanceSqr += k_NotCurrentPenalty;
                return info;
            };
        }
    }
}
