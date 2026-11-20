using MilTrainSimVR.A.Common;
using MilTrainSimVR.A.Rifle;
using UnityEngine;

namespace MilTrainSimVR.A.Range
{
    // 사격장의 리그와 총을 고른다 (11/17).
    //   · 사로 과정(A_Range): C 리그(C_XR Origin 인스턴스) + C 총(C_K2C1 인스턴스 + CRangeRifle) — 늘
    //   · 그 밖(분해·결합 내무반): A 리그 (내무반 기능 점검 총은 따로)
    // 11/17 오후 A · C 결정 (사용자 동의): 총은 C 총 하나, 허브에서 고르는 사격 방식은 '조준 방식'만 바꾼다 —
    //   A 를 고르면 C 총의 A 확대 조준(SightAim)이 켜진다 (C 총 프리팹의 C RifleSightZoom 이 RifleStyle 을 읽어 켠다).
    //   그 전엔 A 방식 = A 리그 + A 총(K2C1_Range)이었다 — 그 자리(m_RigA · m_RifleA)는 내무반 리그로만 쓰고 사격장 A 총은 꺼 둔다.
    // 사로 절차 · 안전 감시 · 약실 확인 · 진행이 다른 오브젝트보다 먼저 이 총을 받게 가장 먼저 깨어난다 (그들은 OnEnable 에서 총 이벤트를 듣는다).
    [DefaultExecutionOrder(-10000)]
    public class RifleStyleSwitch : MonoBehaviour
    {
        [Tooltip("이 과정으로 들어왔을 때 C 리그 · C 총을 쓴다")]
        [SerializeField] string m_CourseId = "A_Range";

        [Header("A 리그 (내무반) · 전에 쓰던 사격장 A 총")]
        [SerializeField] GameObject m_RigA;
        [SerializeField] RangeRifle m_RifleA;

        [Header("사격장 — C 리그 · C 총")]
        [SerializeField] GameObject m_RigC;
        [SerializeField] RangeRifle m_RifleC;

        [Header("총을 쓰는 것")]
        [SerializeField] RangeTrainingFlow m_Flow;
        [SerializeField] RangeProcedure m_Procedure;
        [SerializeField] RangeSafetyMonitor m_Safety;
        [SerializeField] ChamberCheck m_ChamberCheck;

        public bool UsingC { get; private set; }

        void Awake()
        {
            UsingC = CourseRoute.Current == m_CourseId && m_RigC != null && m_RifleC != null;

            // 쓰지 않는 쪽을 먼저 끈다 — 리그 두 개가 같이 켜지는 순간이 없게
            if (UsingC)
            {
                if (m_RigA != null) m_RigA.SetActive(false);
                if (m_RifleA != null) m_RifleA.gameObject.SetActive(false);
                m_RigC.SetActive(true);
                m_RifleC.gameObject.SetActive(true);
            }
            else
            {
                if (m_RigC != null) m_RigC.SetActive(false);
                if (m_RifleC != null) m_RifleC.gameObject.SetActive(false);
                if (m_RigA != null) m_RigA.SetActive(true);
                if (m_RifleA != null) m_RifleA.gameObject.SetActive(true);
            }

            RangeRifle rifle = UsingC ? m_RifleC : m_RifleA;
            if (rifle == null)
                return;
            if (m_Flow != null) m_Flow.UseRifle(rifle);
            if (m_Procedure != null) m_Procedure.UseRifle(rifle);
            if (m_Safety != null) m_Safety.UseRifle(rifle);
            if (m_ChamberCheck != null) m_ChamberCheck.UseRifle(rifle);
            Debug.Log($"[Range] {(UsingC ? "사격장 C 리그 · C 총" : "A 리그")} — 총 {rifle.name}, 조준 방식 {RifleStyle.Current} (A = 확대 조준)");
        }
    }
}
