using MilTrainSimVR.A.Assembly;
using UnityEditor;
using UnityEngine;

namespace MilTrainSimVR.A.EditorTools
{
    // 헤드셋 없이 분해·결합 흐름을 시험하는 메뉴 (Play 중에만). 손 대신 지금 단계를 끝난 상태로 만든다.
    // 분해·결합 다음 기능 점검까지 이어서 진행한다.
    static class AssemblyTestMenu
    {
        const string k_Root = "A/시험/";
        const double k_AutoInterval = 0.3;   // 자동 진행 간격(초) — 단계가 넘어가는 것을 눈으로 볼 수 있게

        static double s_NextAutoTime;

        [MenuItem(k_Root + "학습 모드 시작")]
        static void StartLearning() => Flow()?.StartLearning();

        [MenuItem(k_Root + "평가 모드 시작")]
        static void StartEvaluation() => Flow()?.StartEvaluation();

        [MenuItem(k_Root + "다시 하기")]
        static void Retry() => Flow()?.Retry();

        [MenuItem(k_Root + "처음으로")]
        static void ReturnToIntro() => Flow()?.ReturnToIntro();

        [MenuItem(k_Root + "지금 단계 완료")]
        static void CompleteCurrent()
        {
            string done = CompleteRunning();
            Debug.Log(done != null ? $"[A 시험] 완료 처리: {done}" : "[A 시험] 진행 중인 단계 없음");
        }

        // 진행 중인 쪽(분해·결합 또는 기능 점검)의 지금 단계를 끝낸다. 없으면 null
        static string CompleteRunning()
        {
            var sequence = Object.FindFirstObjectByType<AssemblySequence>();
            if (sequence != null && sequence.Running)
                return sequence.TestCompleteCurrent();
            var check = Object.FindFirstObjectByType<FunctionCheckSequence>();
            if (check != null && check.Running)
                return check.TestCompleteCurrent();
            return null;
        }

        [MenuItem(k_Root + "끝까지 자동 진행")]
        static void AutoRun()
        {
            if (Sequence() == null)
                return;
            s_NextAutoTime = 0;
            EditorApplication.update -= AutoStep;
            EditorApplication.update += AutoStep;
        }

        [MenuItem(k_Root + "학습 모드 시작", true)]
        [MenuItem(k_Root + "평가 모드 시작", true)]
        [MenuItem(k_Root + "다시 하기", true)]
        [MenuItem(k_Root + "처음으로", true)]
        [MenuItem(k_Root + "지금 단계 완료", true)]
        [MenuItem(k_Root + "끝까지 자동 진행", true)]
        static bool IsPlaying() => EditorApplication.isPlaying;

        static void AutoStep()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.update -= AutoStep;
                return;
            }
            if (EditorApplication.timeSinceStartup < s_NextAutoTime)
                return;
            s_NextAutoTime = EditorApplication.timeSinceStartup + k_AutoInterval;
            string done = CompleteRunning();
            if (done == null)
            {
                EditorApplication.update -= AutoStep;
                return;
            }
            Debug.Log($"[A 시험] 완료 처리: {done}");
        }

        static AssemblyTrainingFlow Flow() => Find<AssemblyTrainingFlow>();

        static AssemblySequence Sequence() => Find<AssemblySequence>();

        static T Find<T>() where T : Object
        {
            var found = Object.FindFirstObjectByType<T>();
            if (found == null)
                Debug.LogWarning($"[A 시험] 씬에 {typeof(T).Name} 이 없다");
            return found;
        }
    }
}
