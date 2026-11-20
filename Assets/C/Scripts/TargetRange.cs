using System.Text;
using TMPro;
using UnityEngine;

namespace MilTrainSimVR.UrbanCombat
{
    // 사격장 판 — 표적별 명중 수 · 합계 · 명중률과 총의 탄약 · 조정간을 보여 주고, 버튼으로 표적을 다시 세운다.
    // 보이는 값이 바뀔 때만 글자를 새로 만든다 — 매 프레임 문자열을 만들면 쓰레기(GC)가 쌓여 VR 에서 멈칫한다.
    public class TargetRange : MonoBehaviour
    {
        [SerializeField] TempRifle m_Rifle;

        [Tooltip("표적들이 들어 있는 부모 — 그 아래 PracticeTarget 을 모두 찾는다")]
        [SerializeField] Transform m_TargetsRoot;

        [Tooltip("다시 세울 때 탄흔도 지운다")]
        [SerializeField] ImpactEffects m_Impacts;

        [SerializeField] TMP_Text m_Board;

        PracticeTarget[] m_Targets;
        int m_ShotsAtReset;
        int m_LastSignature;
        bool m_Dirty = true;
        readonly StringBuilder m_Text = new StringBuilder(256);

        void Awake()
        {
            m_Targets = m_TargetsRoot.GetComponentsInChildren<PracticeTarget>(true);
        }

        public int TotalHits
        {
            get
            {
                int hits = 0;
                foreach (PracticeTarget target in m_Targets)
                    hits += target.HitCount;
                return hits;
            }
        }

        // 마지막으로 다시 세운 뒤에 쏜 탄 수
        public int ShotsSinceReset => m_Rifle != null ? m_Rifle.ShotsFired - m_ShotsAtReset : 0;

        // 판의 '표적 다시 세우기' 버튼이 부른다
        public void ResetAll()
        {
            foreach (PracticeTarget target in m_Targets)
                target.ResetTarget();
            if (m_Impacts != null)
                m_Impacts.ClearHoles();
            m_ShotsAtReset = m_Rifle != null ? m_Rifle.ShotsFired : 0;
            m_Dirty = true;
        }

        void Update()
        {
            int signature = Signature();
            if (!m_Dirty && signature == m_LastSignature)
                return;
            m_LastSignature = signature;
            m_Dirty = false;
            Redraw();
        }

        // 판에 보이는 값들을 숫자 하나로 묶는다 — 이 숫자가 그대로면 다시 그리지 않는다
        int Signature()
        {
            int h = 17;
            foreach (PracticeTarget target in m_Targets)
                h = h * 31 + target.HitCount;
            h = h * 31 + ShotsSinceReset;
            if (m_Rifle != null)
            {
                h = h * 31 + m_Rifle.Rounds;
                h = h * 31 + (int)m_Rifle.Mode;
                h = h * 31 + (m_Rifle.HasMagazine ? 1 : 0) + (m_Rifle.Chambered ? 2 : 0);
            }
            return h;
        }

        void Redraw()
        {
            if (m_Board == null)
                return;

            m_Text.Clear();
            m_Text.Append("<b>사격장</b>\n");
            foreach (PracticeTarget target in m_Targets)
                m_Text.Append(target.Label).Append("  명중 ").Append(target.HitCount).Append('\n');

            int hits = TotalHits;
            int shots = ShotsSinceReset;
            m_Text.Append("합계 ").Append(hits).Append(" / 사격 ").Append(shots);
            if (shots > 0)
                m_Text.Append("  (").Append(Mathf.RoundToInt(100f * hits / shots)).Append("%)");
            m_Text.Append('\n');

            if (m_Rifle != null)
            {
                m_Text.Append("탄약 ").Append(m_Rifle.Rounds).Append(" / ").Append(m_Rifle.MagazineSize)
                    .Append("  ·  ").Append(TempRifle.ModeName(m_Rifle.Mode));
                if (!m_Rifle.HasMagazine)
                    m_Text.Append("  ·  탄창 없음");
                else if (!m_Rifle.Chambered)
                    m_Text.Append("  ·  장전 필요");
            }
            m_Board.SetText(m_Text);
        }
    }
}
