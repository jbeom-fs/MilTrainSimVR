using System.Collections.Generic;
using UnityEngine;

namespace MilTrainSimVR.A.Range
{
    // 한 조의 사격 결과 — 쏜 발 수와 표적지에 맞은 자리(조준점 기준 cm, 오른쪽 +x · 위 +y) (기획 4절).
    //   · 탄착군 크기: 맞은 탄 중 가장 먼 두 탄착 사이 거리 (cm). 1발 이하면 0
    //   · 중심: 맞은 탄의 평균 위치 — 조준점에서 상하·좌우로 몇 cm인지
    public class ShotGroup
    {
        readonly List<Vector2> m_Hits = new List<Vector2>();

        public int Shots { get; set; }
        public IReadOnlyList<Vector2> Hits => m_Hits;
        public int HitCount => m_Hits.Count;
        public float SizeCm { get; private set; }
        public Vector2 CenterCm { get; private set; }

        public void AddHit(Vector2 cm)
        {
            m_Hits.Add(cm);
            Recalculate();
        }

        void Recalculate()
        {
            Vector2 sum = Vector2.zero;
            float size = 0f;
            for (int i = 0; i < m_Hits.Count; i++)
            {
                sum += m_Hits[i];
                for (int j = i + 1; j < m_Hits.Count; j++)
                    size = Mathf.Max(size, Vector2.Distance(m_Hits[i], m_Hits[j]));
            }
            SizeCm = size;
            CenterCm = m_Hits.Count > 0 ? sum / m_Hits.Count : Vector2.zero;
        }
    }
}
