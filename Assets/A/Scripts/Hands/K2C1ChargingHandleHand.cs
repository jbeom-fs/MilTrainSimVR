// 원본: Assets/C/Scripts/RifleChargingHandle.cs 의 손 붙이기 부분(Grab · Release, 11/17 HEAD ac6d798, C 이주석) — 총기 통합 (Docs/A/1117_총기통합_2_방아쇠_장전손잡이손.md).
// C 장전 손잡이는 손 자리로 잡기를 직접 보지만, A 장전 손잡이(ChargingHandleSlide)는 따로 잡히는 XRI 부품이라 잡힘 · 놓음 이벤트에 붙인다.
using MilTrainSimVR.UrbanCombat;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using CHandReach = MilTrainSimVR.UrbanCombat.HandReach;

namespace MilTrainSimVR.A.Hands
{
    // 장전 손잡이를 잡은 손 — 당기는 동안 보이는 손 모델을 손잡이에 붙여 주먹으로 감싼다 (C 와 같은 모양).
    // 손 방향은 그대로, 주먹 가운데(잡기 점)가 손잡이에 오게 붙이고, 손잡이를 따라 같이 움직인다. 놓으면 손으로 돌아간다.
    // 손 모델이 이미 다른 데(레일 — 두 손 파지)에 붙어 있으면 건드리지 않는다.
    // A 장전 손잡이 부품(Part_ChargingHandle — XRGrabInteractable + ChargingHandleSlide)에 붙인다.
    [RequireComponent(typeof(XRGrabInteractable))]
    public class K2C1ChargingHandleHand : MonoBehaviour
    {
        [Tooltip("감싼 손가락 — 그립(가운데·약지·새끼) · 검지 · 엄지 굽힘 (0~1, C 와 같은 값)")]
        [SerializeField] Vector3 m_Fist = new Vector3(1f, 0.8f, 0.8f);

        XRGrabInteractable m_Grab;
        Transform m_HandPose;   // 손 모델이 붙는 자리 — 손잡이 아래라 손잡이를 따라 움직인다
        K2C1RifleHands.Snap m_Snap;

        void Awake()
        {
            m_Grab = GetComponent<XRGrabInteractable>();
            m_HandPose = new GameObject("Pose_ChargingHand").transform;
            m_HandPose.SetParent(transform, false);
        }

        void OnEnable()
        {
            m_Grab.selectEntered.AddListener(OnGrab);
            m_Grab.selectExited.AddListener(OnRelease);
        }

        void OnDisable()
        {
            m_Grab.selectEntered.RemoveListener(OnGrab);
            m_Grab.selectExited.RemoveListener(OnRelease);
            K2C1RifleHands.Detach(ref m_Snap);
        }

        void OnGrab(SelectEnterEventArgs args)
        {
            if (!(args.interactorObject is NearFarInteractor hand))
                return;
            ControllerHandPose visual = CHandReach.HandVisual(hand);
            if (visual == null || visual.HandModel == null || visual.HandModel.parent != visual.transform)
                return;
            Transform root = visual.transform;
            m_HandPose.SetPositionAndRotation(root.position + (transform.position - CHandReach.GrabPoint(hand)), root.rotation);
            K2C1RifleHands.Detach(ref m_Snap);
            m_Snap = K2C1RifleHands.Attach(visual, m_HandPose);
            m_Snap.hand.GripOverride = m_Fist.x;
            m_Snap.hand.IndexOverride = m_Fist.y;
            m_Snap.hand.ThumbOverride = m_Fist.z;
        }

        void OnRelease(SelectExitEventArgs args) => K2C1RifleHands.Detach(ref m_Snap);
    }
}
