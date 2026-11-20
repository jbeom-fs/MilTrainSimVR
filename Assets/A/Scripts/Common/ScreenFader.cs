using System.Collections;
using UnityEngine;

namespace MilTrainSimVR.A.Common
{
    // 화면을 까맣게 덮었다 걷는다 — 방 사이 이동(DoorTransition) 같은 순간이동 때 쓴다.
    // 머리(카메라)에 붙은 작은 구를 안쪽에서 본다. 깊이 검사 없이 맨 위에 그리는 재질(AimOverlay 셰이더)이라 두 눈 모두, 총·손보다 앞에 덮인다.
    // 머리 추적은 그대로 — 까만 동안에도 고개를 돌리면 따라간다 (화면을 멈춘 채 까맣게 하면 오히려 어지럽다).
    public class ScreenFader : MonoBehaviour
    {
        [Tooltip("머리 — 비우면 Main Camera")]
        [SerializeField] Transform m_Head;

        [Tooltip("덮는 재질 — 깊이 검사 없이 맨 위에 그리는 것 (Materials/Common/ScreenFade)")]
        [SerializeField] Material m_Material;

        [Tooltip("머리를 감싸는 구 반지름 (m) — 카메라 가까운 면(near)보다 커야 한다")]
        [SerializeField] float m_Radius = 0.25f;

        [SerializeField] Color m_Color = Color.black;

        Renderer m_Sphere;
        Material m_Instance;
        float m_Alpha;

        public bool IsBusy { get; private set; }
        public float Alpha => m_Alpha;

        void Awake()
        {
            if (m_Head == null && Camera.main != null)
                m_Head = Camera.main.transform;
            if (m_Head == null || m_Material == null)
            {
                Debug.LogWarning("[Fade] 머리 또는 재질이 없다 — 화면을 덮지 않고 바로 넘어간다");
                return;
            }
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "ScreenFade";
            Destroy(sphere.GetComponent<Collider>());
            sphere.transform.SetParent(m_Head, false);
            sphere.transform.localScale = Vector3.one * m_Radius * 2f;
            m_Sphere = sphere.GetComponent<Renderer>();
            m_Sphere.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m_Sphere.receiveShadows = false;
            m_Instance = new Material(m_Material);
            m_Sphere.sharedMaterial = m_Instance;
            SetAlpha(0f);
        }

        void OnDestroy()
        {
            if (m_Instance != null)
                Destroy(m_Instance);
        }

        public void SetAlpha(float alpha)
        {
            m_Alpha = Mathf.Clamp01(alpha);
            if (m_Sphere == null)
                return;
            m_Sphere.enabled = m_Alpha > 0.001f;
            Color c = m_Color;
            c.a = m_Alpha;
            m_Instance.SetColor("_Color", c);
        }

        // 지금 값에서 target 까지 seconds 동안. 시간은 실제 시간(일시 정지와 상관없이)
        public IEnumerator FadeTo(float target, float seconds)
        {
            IsBusy = true;
            float from = m_Alpha;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                SetAlpha(Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t / seconds)));
                yield return null;
            }
            SetAlpha(target);
            IsBusy = false;
        }
    }
}
