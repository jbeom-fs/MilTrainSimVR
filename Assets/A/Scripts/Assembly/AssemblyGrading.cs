using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MilTrainSimVR.A.Assembly
{
    public enum AssemblyGrade { Excellent, Pass, Fail }   // 우수 · 합격 · 불합격

    // 평가 모드 등급 기준. 값은 임시 — 헤드셋으로 몇 번 해 본 뒤 정한다 (기획 4절).
    [System.Serializable]
    public class GradeCriteria
    {
        [Tooltip("우수: 이 시간(초) 안에")]
        public float excellentSeconds = 180f;

        [Tooltip("우수: 오류가 이 수 이하")]
        public int excellentMaxErrors;

        [Tooltip("합격: 이 시간(초) 안에")]
        public float passSeconds = 300f;

        [Tooltip("합격: 오류가 이 수 이하")]
        public int passMaxErrors = 2;
    }

    // 평가 결과 계산 — 등급과 '오류가 난 단계' 줄. 화면 글자는 AssemblyTexts 에서 가져온다.
    public static class AssemblyGrading
    {
        public static AssemblyGrade Grade(float seconds, int errors, GradeCriteria c)
        {
            if (seconds <= c.excellentSeconds && errors <= c.excellentMaxErrors)
                return AssemblyGrade.Excellent;
            if (seconds <= c.passSeconds && errors <= c.passMaxErrors)
                return AssemblyGrade.Pass;
            return AssemblyGrade.Fail;
        }

        // "분해 2 (1) · 결합 3 (2) · 기능 점검 2 (1)" — 단계 문구의 앞 이름(" · " 앞)으로 묶는다. 분해 5 처럼 두 줄로 나뉜 단계는 합친다
        public static string StepErrorSummary(IReadOnlyList<int> assemblyErrors, IReadOnlyList<int> checkErrors)
        {
            var labels = new List<string>();
            var counts = new List<int>();
            Collect(assemblyErrors, AssemblyTexts.StepText, labels, counts);
            if (checkErrors != null)
                Collect(checkErrors, AssemblyTexts.FunctionCheckStepText, labels, counts);
            if (labels.Count == 0)
                return AssemblyTexts.NoStepErrors;

            var sb = new StringBuilder(AssemblyTexts.StepErrorsHead);
            for (int i = 0; i < labels.Count; i++)
            {
                if (i > 0) sb.Append(" · ");
                sb.Append(labels[i]).Append(" (").Append(counts[i]).Append(')');
            }
            return sb.ToString();
        }

        static void Collect(IReadOnlyList<int> errors, System.Func<int, string> text, List<string> labels, List<int> counts)
        {
            for (int i = 0; i < errors.Count; i++)
            {
                if (errors[i] <= 0) continue;
                string label = ShortName(text(i));
                int at = labels.IndexOf(label);
                if (at >= 0)
                {
                    counts[at] += errors[i];
                    continue;
                }
                labels.Add(label);
                counts.Add(errors[i]);
            }
        }

        static string ShortName(string stepText)
        {
            int cut = stepText.IndexOf(" · ", System.StringComparison.Ordinal);
            return cut > 0 ? stepText.Substring(0, cut) : stepText;
        }
    }
}
