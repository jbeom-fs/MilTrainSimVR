using UnityEngine;

namespace MilTrainSimVR.UrbanCombat
{
    // 사격 연습용 표적. 총(TempRifle)이 맞히면 OnHit 이 불린다.
    // 맞았을 때 어떻게 반응할지 — 사수에게 "맞혔다"를 어떻게 알려 줄지 — 는 OnHit 안에서 정한다.
    // ResetTarget 은 처음 모습(자리·색·고정)으로 되돌린다 — 사격장 판의 '표적 다시 세우기' 버튼이 부른다.
    public class PracticeTarget : MonoBehaviour, IHittable
    {
        [Tooltip("사격장 판에 보일 이름 (비우면 오브젝트 이름)")]
        [SerializeField] string m_Label;

        [Tooltip("맞았을 때 바꿀 색")]
        [SerializeField] Color m_HitColor = new Color(1f, 0.35f, 0.1f);

        [Tooltip("맞은 지점에서 밀어내는 힘 (Rigidbody 가 있을 때만)")]
        [SerializeField] float m_PushForce = 3f;

        Renderer m_Renderer;
        Rigidbody m_Body;
        int m_HitCount;

        // 다시 세울 때 돌아갈 처음 모습
        Vector3 m_StartPosition;
        Quaternion m_StartRotation;
        Color m_StartColor;

        void Awake()
        {
            m_Renderer = GetComponent<Renderer>();
            m_Body = GetComponent<Rigidbody>(); // 없으면 null — 움직이지 않는 고정 표적
            m_StartPosition = transform.position;
            m_StartRotation = transform.rotation;
            if (m_Renderer != null)
                m_StartColor = m_Renderer.sharedMaterial.color;
        }

        public string Label => string.IsNullOrEmpty(m_Label) ? name : m_Label;

        // 맞은 횟수 — 사격장 판·결과 화면에서 읽는다
        public int HitCount => m_HitCount;

        public void OnHit(RaycastHit hit, Vector3 direction)
        {
            m_HitCount++;

            // 색은 모든 표적에서 바꾼다 — 25m 처럼 멀리 있어도 맞았는지 보이게
            if (m_Renderer != null)
                m_Renderer.material.color = m_HitColor;

            // 받침대 위 표적(Rigidbody 있음)은 고정을 풀고 사수 반대쪽으로 밀어 쓰러뜨린다
            if (m_Body != null)
            {
                m_Body.isKinematic = false;
                m_Body.AddForceAtPosition(-hit.normal * m_PushForce, hit.point, ForceMode.Impulse);
            }
        }

        public void ResetTarget()
        {
            m_HitCount = 0;
            if (m_Renderer != null)
                m_Renderer.material.color = m_StartColor;

            if (m_Body != null)
            {
                // 쓰러지며 굴러가던 속도를 먼저 지우고 다시 고정한다 — 고정된 물체에 속도를 넣으면 경고가 뜬다
                if (!m_Body.isKinematic)
                {
                    m_Body.linearVelocity = Vector3.zero;
                    m_Body.angularVelocity = Vector3.zero;
                }
                m_Body.isKinematic = true;
            }
            transform.SetPositionAndRotation(m_StartPosition, m_StartRotation);
        }
    }
}
