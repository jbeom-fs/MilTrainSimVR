using MilTrainSimVR.A.Common;
using MilTrainSimVR.A.Rifle;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MilTrainSimVR.UrbanCombat
{
    // 확대 조준을 허브(연병장)에서 고른 사격 방식에 따라 켜고 끈다 (사용자 11/17).
    //   · A 방식: A 의 확대 조준(SightAim — 총을 든 채 눈을 가늠자 뒤로 가져가면 4배 창)을 C 총에서도 쓴다
    //   · C 방식: 끈 채 둔다 — 지금 C 그대로
    //   리그 · 총 · 조작 키는 어느 쪽이든 C 그대로다 ("조준점 확대되는 방식을 이야기 한거지 조작 키까지 다 바꾸겠다는 소리가 아님")
    // SightAim 은 같은 총에 꺼진 채(enabled = false) 붙어 있고, 씬 시작 때 여기서 한 번 켠다 (사격 방식은 허브에서만 바뀐다).
    //   A 사격장의 C 방식도 이 총(C_K2C1)을 쓰지만 그땐 사격 방식이 C 라 꺼진 채다 — A 사격장은 그대로.
    // C 총에 맞춘 SightAim 값은 프리팹에 넣는다 (A 파일은 그대로, CombatAssetsBuilder):
    //   · 흔들림 0 · 따라가기 0 — 창 = 실제 총의 조준선. 흔들림 · 숨 참기는 총 자체를 움직이는 RifleBreath 가 맡고, 탄도 총을 따라 나간다
    // 확대 카메라의 가까운 면 (11/17 검증): A 값 1 m 면 벽 바로 앞에서 조준할 때 창에 그 벽이 안 찍혀 벽 너머가 보였다.
    //   줄이면 총구(가늠자에서 0.71 m, 조준선 조금 아래)가 창 아래에 찍힌다 → 평소엔 총구 바로 앞(m_Near 0.75 m)에 두고,
    //   확대 중 가늠자 앞 그 거리 안에 벽 · 사람이 있으면 그 바로 앞까지 당긴다 — 창에 벽이 찍히고 벽 너머는 안 보인다
    // A 의 숨 막대(창 아래)는 숨긴다 — SightAim 은 C 의 앞손을 몰라 늘 '가득'으로 보인다. C 의 숨은 RifleBreath(진동으로 알림)
    public class RifleSightZoom : MonoBehaviour
    {
        // SightAim 이 만드는 자식 이름 (SightAim.BuildView)
        const string k_BreathBar = "AimBreathBar";
        const string k_AimCamera = "AimCamera";

        [Tooltip("확대 카메라의 평소 가까운 면(m) — 총구(가늠자에서 0.71 m)가 창에 안 찍히게")]
        [SerializeField] float m_Near = 0.75f;

        [Tooltip("가까운 면을 당길 때 벽 표면에서 이만큼(m) 앞에 둔다")]
        [SerializeField] float m_WallMargin = 0.02f;

        SightAim m_Aim;
        Camera m_AimCamera;
        XRGrabInteractable m_Grab;
        Transform m_RearSight;
        Transform m_FrontSight;
        readonly RaycastHit[] m_Hits = new RaycastHit[8];

        public bool ZoomEnabled { get; private set; }

        void Start()
        {
            // Start = 모든 Awake 뒤 — SightAim 이 창 · 카메라 · 숨 막대를 만든 다음
            m_Aim = GetComponent<SightAim>();
            if (m_Aim == null)
            {
                Debug.LogWarning("[C] 확대 조준 — 같은 총에 SightAim 이 없다 (CombatAssetsBuilder 로 총을 다시 만들 것)");
                return;
            }
            ZoomEnabled = RifleStyle.Current == RifleStyle.Style.A;
            m_Aim.enabled = ZoomEnabled;
            Transform bar = transform.Find(k_BreathBar);
            if (bar != null && bar.TryGetComponent(out Renderer renderer))
                renderer.enabled = false;   // 창을 켜고 끌 때 SightAim 은 오브젝트를 켜고 끈다 — 그림만 꺼 두면 계속 숨겨진다
            Transform cam = transform.Find(k_AimCamera);
            m_AimCamera = cam != null ? cam.GetComponent<Camera>() : null;
            m_Grab = GetComponent<XRGrabInteractable>();
            // 조준선 두 점 — RifleBallistics 와 같은 부품 (K2C1 모델)
            m_RearSight = FindChild(transform, "Anchor_RearSight");
            m_FrontSight = FindChild(transform, "Anchor_FrontSight");
            Debug.Log($"[C] 사격 방식 {RifleStyle.Current} — 확대 조준 {(ZoomEnabled ? "켬" : "끔")}");
        }

        void LateUpdate()
        {
            if (!ZoomEnabled || m_AimCamera == null || m_RearSight == null || m_FrontSight == null || !m_Aim.IsAiming)
                return;
            m_AimCamera.nearClipPlane = NearPlane();
        }

        // 가늠자 → 조준선 방향으로 m_Near 안에 처음 걸리는 것 (총 자신 · 든 사람 · 보이지 않는 맵 끝 벽 빼고) 바로 앞. 없으면 m_Near
        float NearPlane()
        {
            Vector3 from = m_RearSight.position;
            Vector3 dir = (m_FrontSight.position - from).normalized;
            float reach = m_Near + m_WallMargin;
            Transform holder = Holder();
            int count = Physics.RaycastNonAlloc(from, dir, m_Hits, reach, Gunfire.ShotMask, QueryTriggerInteraction.Ignore);
            float nearest = reach;
            for (int i = 0; i < count; i++)
            {
                Transform t = m_Hits[i].collider.transform;
                if (t.IsChildOf(transform) || (holder != null && t.IsChildOf(holder)))
                    continue;
                nearest = Mathf.Min(nearest, m_Hits[i].distance);
            }
            return nearest >= reach ? m_Near : Mathf.Max(0.02f, nearest - m_WallMargin);
        }

        Transform Holder()
        {
            if (m_Grab == null || m_Grab.interactorsSelecting.Count == 0)
                return null;
            XROrigin origin = m_Grab.interactorsSelecting[0].transform.GetComponentInParent<XROrigin>();
            return origin != null ? origin.transform : null;
        }

        static Transform FindChild(Transform parent, string name)
        {
            foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
                if (t.name == name)
                    return t;
            return null;
        }
    }
}
