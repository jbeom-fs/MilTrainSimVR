using System.Collections.Generic;
using UnityEngine;

namespace MilTrainSimVR.A.Range
{
    public enum RangeGrade { Excellent, Pass, Fail }   // 우수 · 합격 · 불합격

    // 평가 모드 등급 기준 (사로 기획 3·4절). **값은 임시** — 영점사격 합격 기준은 교안을 확인한 뒤 정한다 (10절).
    [System.Serializable]
    public class RangeGradeCriteria
    {
        [Tooltip("조 합격: 탄착군 크기(cm)가 이 값 이하")]
        public float groupMaxCm = 4f;

        [Tooltip("조 합격: 조의 탄이 모두 표적지에 맞아야 한다")]
        public bool groupAllHits = true;

        // 조당 정해진 발 수 — 흐름이 판정 전에 넣는다. 탄이 떨어져 덜 쏜 조(예: 4/4발)는 합격 조가 아니다 (11/03)
        [System.NonSerialized] public int roundsPerGroup;

        [Tooltip("우수: 합격한 조가 이 수 이상")]
        public int excellentGroups = 2;    // 11/02 조 수 3 → 2 에 맞춤

        [Tooltip("우수: 절차 실수가 이 수 이하")]
        public int excellentMaxErrors;

        [Tooltip("합격: 합격한 조가 이 수 이상")]
        public int passGroups = 1;

        [Tooltip("합격: 절차 실수가 이 수 이하")]
        public int passMaxErrors = 2;

        [Tooltip("안전 위반이 하나라도 있으면 불합격")]
        public bool failOnViolation = true;
    }

    // 평가 결과 계산. 화면 글자는 RangeTexts 에서 가져온다.
    public static class RangeGrading
    {
        public static bool GroupPassed(ShotGroup g, RangeGradeCriteria c) =>
            g.HitCount > 0 && (!c.groupAllHits || g.HitCount >= Mathf.Max(g.Shots, c.roundsPerGroup)) && g.SizeCm <= c.groupMaxCm;

        public static int PassedGroups(IReadOnlyList<ShotGroup> groups, RangeGradeCriteria c)
        {
            int n = 0;
            foreach (ShotGroup g in groups)
                if (GroupPassed(g, c))
                    n++;
            return n;
        }

        public static RangeGrade Grade(IReadOnlyList<ShotGroup> groups, int errors, int violations, RangeGradeCriteria c)
        {
            if (c.failOnViolation && violations > 0)
                return RangeGrade.Fail;
            int passed = PassedGroups(groups, c);
            if (passed >= c.excellentGroups && errors <= c.excellentMaxErrors)
                return RangeGrade.Excellent;
            if (passed >= c.passGroups && errors <= c.passMaxErrors)
                return RangeGrade.Pass;
            return RangeGrade.Fail;
        }
    }
}
