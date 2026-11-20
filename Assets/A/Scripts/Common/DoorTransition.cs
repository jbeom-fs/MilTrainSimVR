using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MilTrainSimVR.A.Common
{
    // 닫힌 문 손잡이 — 만지면(레이 트리거·손 그립 모두 '선택') 화면이 까매졌다가 다른 방(m_Destination)에서 밝아진다.
    // 문은 열리지 않는다. 걸어서 지나가는 것보다 멀미가 적고, 나중에 방을 씬으로 나눠도 까만 동안 불러오면 된다.
    // 도착: 머리(카메라)가 m_Destination 자리에, 몸이 m_Destination 앞(+Z)을 보게 — 플레이 공간 안의 서 있는 자리·키는 그대로 둔다.
    // 잡고 있던 물건(총 등)은 손을 따라온다 (XRI 잡기).
    // m_ToHub 를 켜면 도착 자리 대신 까매진 채 허브 씬을 불러온다 (CourseRoute.ReturnToHub, 11/10 — 사격장 나가는 문).
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class DoorTransition : MonoBehaviour
    {
        [Tooltip("도착 자리 — 바닥 위 점, 앞(+Z)이 바라볼 방향 (예: Points/Point_RangeEntry)")]
        [SerializeField] Transform m_Destination;

        [Tooltip("켜면 도착 자리 대신 허브로 나간다 — 까매진 채 허브 씬을 불러오고, 허브가 화면을 밝힌다")]
        [SerializeField] bool m_ToHub;

        [Tooltip("비우면 씬에서 찾는다")]
        [SerializeField] ScreenFader m_Fader;

        [Tooltip("잠기면 만져도 넘어가지 않는다 (예: 분해·결합 교육을 마치기 전) — 지금은 늘 열림")]
        [SerializeField] bool m_Locked;

        [SerializeField] float m_FadeOutSeconds = 0.4f;
        [SerializeField] float m_HoldSeconds = 0.2f;
        [SerializeField] float m_FadeInSeconds = 0.4f;

        XRSimpleInteractable m_Interactable;
        static bool s_Moving;   // 문 여러 개가 한꺼번에 움직이지 않게

        public bool Locked { get => m_Locked; set => m_Locked = value; }
        public Transform Destination => m_Destination;

        public event System.Action Arrived;        // 다른 방에서 밝아지기 시작할 때
        public event System.Action LockedTouched;  // 잠긴 채 만짐 — 안내 문구는 듣는 쪽이

        void Awake()
        {
            m_Interactable = GetComponent<XRSimpleInteractable>();
            m_Interactable.selectEntered.AddListener(OnSelected);
            if (m_Fader == null)
                m_Fader = FindFirstObjectByType<ScreenFader>();
        }

        void OnDestroy()
        {
            if (m_Interactable != null)
                m_Interactable.selectEntered.RemoveListener(OnSelected);
        }

        void OnSelected(SelectEnterEventArgs args)
        {
            if (m_Locked)
            {
                Debug.Log($"[Door] {name} 잠김");
                LockedTouched?.Invoke();
                return;
            }
            XROrigin origin = args.interactorObject.transform.GetComponentInParent<XROrigin>();
            Go(origin);
        }

        // 시험용으로도 부른다 (손 없이). origin 이 없으면 씬의 XR Origin
        public void Go(XROrigin origin = null)
        {
            if (s_Moving)
                return;
            if (m_ToHub)
            {
                StartCoroutine(RunToHub());
                return;
            }
            if (m_Destination == null)
                return;
            if (origin == null)
                origin = FindFirstObjectByType<XROrigin>();
            if (origin == null || origin.Camera == null)
                return;
            StartCoroutine(Run(origin));
        }

        IEnumerator Run(XROrigin origin)
        {
            s_Moving = true;
            Debug.Log($"[Door] {name} → {m_Destination.name}");
            if (m_Fader != null)
                yield return m_Fader.FadeTo(1f, m_FadeOutSeconds);

            Teleport(origin, m_Destination);
            yield return new WaitForSecondsRealtime(m_HoldSeconds);

            Arrived?.Invoke();
            if (m_Fader != null)
                yield return m_Fader.FadeTo(0f, m_FadeInSeconds);
            s_Moving = false;
        }

        IEnumerator RunToHub()
        {
            s_Moving = true;
            Debug.Log($"[Door] {name} → 허브");
            if (m_Fader != null)
                yield return m_Fader.FadeTo(1f, m_FadeOutSeconds);
            s_Moving = false;   // 정적 값 — 허브에서 돌아와 다시 쓸 때 막히지 않게
            CourseRoute.ReturnToHub();
        }

        // 머리가 도착 자리 위(키 그대로)에, 바라보는 방향(수평)이 도착 자리 앞이 되게 리그를 옮긴다
        public static void Teleport(XROrigin origin, Transform destination)
        {
            Transform head = origin.Camera.transform;
            Vector3 headForward = Vector3.ProjectOnPlane(head.forward, origin.transform.up);
            Vector3 wantForward = Vector3.ProjectOnPlane(destination.forward, origin.transform.up);
            if (headForward.sqrMagnitude > 1e-4f && wantForward.sqrMagnitude > 1e-4f)
                origin.RotateAroundCameraUsingOriginUp(Vector3.SignedAngle(headForward, wantForward, origin.transform.up));

            float headHeight = Vector3.Dot(head.position - origin.transform.position, origin.transform.up);
            origin.MoveCameraToWorldLocation(destination.position + origin.transform.up * headHeight);
        }
    }
}
