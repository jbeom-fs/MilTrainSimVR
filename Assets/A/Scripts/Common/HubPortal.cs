using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MilTrainSimVR.A.Common
{
    // 허브 과정 포탈 (11/09) — 연병장에 선 차원 포탈. 걸어 들어가면(머리가 렌즈에 닿으면) 또는 레이로 선택하면
    // 화면이 까매지고 과정 씬을 불러온다.
    // 과정을 늘릴 때는 포탈 하나(Portal_*)를 복제해서 이 값 네 개만 바꾸면 된다: 과정 ID · 이름 · 씬 경로 · (돌아오면 설) 자리.
    // 렌즈 그림은 렌즈 재질(Materials/Hub/Portals/Hub_PortalLens_<과정 ID>)의 Base Map 에 넣는다.
    // 씬 경로가 비었거나 없는 씬이면 '준비 중' — 들어가도 넘어가지 않는다.
    // 원점 = 렌즈 아래 바닥, 앞(+Z) = 사람이 다가오는 쪽.
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class HubPortal : MonoBehaviour
    {
        [Tooltip("과정 ID — 과정 씬이 CourseRoute.Current 로 받는다 (예: A_Assembly)")]
        [SerializeField] string m_CourseId;

        [Tooltip("포탈 위에 뜨는 이름 (예: 분해·결합)")]
        [SerializeField] string m_Label;

        [Tooltip("불러올 씬 경로 (예: Assets/A/Scenes/A_Sandbox.unity) — 비우면 준비 중")]
        [SerializeField] string m_ScenePath;

        [Tooltip("이 과정에서 허브로 돌아오면 설 자리 — 바닥 위 점, 앞(+Z)이 바라볼 방향")]
        [SerializeField] Transform m_ReturnPoint;

        [Tooltip("이름 글자 · 준비 중 표시")]
        [SerializeField] Text[] m_Signs;
        [SerializeField] Text[] m_SignSubs;

        [Tooltip("머리가 이 상자(포탈 기준) 안에 들어오면 넘어간다")]
        [SerializeField] Vector3 m_EnterCenter = new Vector3(0f, 1.7f, 0f);
        [SerializeField] Vector3 m_EnterSize = new Vector3(1.5f, 2.4f, 0.9f);

        [Tooltip("비우면 씬에서 찾는다")]
        [SerializeField] ScreenFader m_Fader;
        [SerializeField] Transform m_Head;

        [SerializeField] float m_FadeOutSeconds = 0.5f;

        XRSimpleInteractable m_Interactable;
        bool m_Armed;   // 머리가 한 번 밖에 있어야 들어갈 수 있다 (포탈 안에서 시작해도 바로 넘어가지 않게)
        static bool s_Moving;

        public string CourseId => m_CourseId;
        public Transform ReturnPoint => m_ReturnPoint;
        public bool Ready => CourseRoute.CanLoad(m_ScenePath);

        public event System.Action NotReadyTouched;

        void Awake()
        {
            s_Moving = false;
            m_Interactable = GetComponent<XRSimpleInteractable>();
            m_Interactable.selectEntered.AddListener(OnSelected);
            if (m_Fader == null)
                m_Fader = FindFirstObjectByType<ScreenFader>();
            if (m_Head == null && Camera.main != null)
                m_Head = Camera.main.transform;
            RefreshSign();
        }

        void OnDestroy()
        {
            if (m_Interactable != null)
                m_Interactable.selectEntered.RemoveListener(OnSelected);
        }

        void OnValidate() => RefreshSign();

        void RefreshSign()
        {
            if (m_Signs != null)
                foreach (var t in m_Signs)
                    if (t != null)
                        t.text = m_Label;
            if (m_SignSubs != null)
                foreach (var t in m_SignSubs)
                    if (t != null)
                        t.text = Ready ? "" : "준비 중";
        }

        void Update()
        {
            if (m_Head == null || s_Moving)
                return;
            Vector3 p = transform.InverseTransformPoint(m_Head.position) - m_EnterCenter;
            Vector3 h = m_EnterSize * 0.5f;
            bool inside = Mathf.Abs(p.x) < h.x && Mathf.Abs(p.y) < h.y && Mathf.Abs(p.z) < h.z;
            if (!inside)
                m_Armed = true;
            else if (m_Armed)
            {
                m_Armed = false;
                Go();
            }
        }

        void OnSelected(SelectEnterEventArgs args) => Go();

        // 시험용으로도 부른다 (손 없이)
        public void Go()
        {
            if (s_Moving)
                return;
            if (!Ready)
            {
                Debug.Log($"[Hub] {m_CourseId} 준비 중 — 씬 '{m_ScenePath}' 없음");
                NotReadyTouched?.Invoke();
                return;
            }
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            s_Moving = true;
            Debug.Log($"[Hub] {m_CourseId} ({m_Label}) → {m_ScenePath}");
            if (m_Fader != null)
                yield return m_Fader.FadeTo(1f, m_FadeOutSeconds);
            CourseRoute.Enter(m_CourseId, m_ScenePath);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.5f);
            Gizmos.DrawWireCube(m_EnterCenter, m_EnterSize);
        }
    }
}
