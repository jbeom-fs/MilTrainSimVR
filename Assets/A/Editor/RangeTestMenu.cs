using MilTrainSimVR.A.Range;
using MilTrainSimVR.A.Rifle;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;

namespace MilTrainSimVR.A.EditorTools
{
    // 헤드셋 없이 사로 사격 흐름을 시험하는 메뉴 (Play 중에만). 판정이 붙기 전이라 단계는 여기서 넘긴다.
    static class RangeTestMenu
    {
        const string k_Root = "A/시험/사로/";
        const string k_FiringPosition = "Points/Point_FiringPosition";

        [MenuItem(k_Root + "사선으로 이동")]
        static void MoveToFiringPosition()
        {
            var origin = Object.FindFirstObjectByType<XROrigin>();
            var point = GameObject.Find(k_FiringPosition);
            if (origin == null || point == null)
            {
                Debug.LogWarning($"[A 시험] XR Origin 또는 {k_FiringPosition} 이 없다");
                return;
            }
            origin.transform.SetPositionAndRotation(point.transform.position, point.transform.rotation);
        }

        [MenuItem(k_Root + "학습 모드 시작")]
        static void StartLearning() => Flow()?.StartLearning();

        [MenuItem(k_Root + "평가 모드 시작")]
        static void StartEvaluation() => Flow()?.StartEvaluation();

        [MenuItem(k_Root + "지금 단계 완료")]
        static void CompleteCurrent()
        {
            string done = Flow()?.CompleteStep();
            Debug.Log(done != null ? $"[A 시험] 사로 완료 처리: {done}" : "[A 시험] 사로 진행 중인 단계 없음");
        }

        // 손 없이 — 사로 총(흐름에 연결된 총 — 사격 방식 A 또는 C)에 놓인 탄창을 끼우고 뺀다
        [MenuItem(k_Root + "탄창 끼우기")]
        static void InsertMagazine()
        {
            var rifle = RangeGun();
            if (rifle == null)
            {
                Debug.LogWarning("[A 시험] 사로 총이 없다");
                return;
            }
            rifle.TestInsertMagazine();
        }

        [MenuItem(k_Root + "탄창 빼기")]
        static void RemoveMagazine() => RangeGun()?.TestRemoveMagazine();

        static RangeRifle RangeGun()
        {
            var flow = Flow();
            if (flow == null)
                return null;
            return new SerializedObject(flow).FindProperty("m_Rifle").objectReferenceValue as RangeRifle;
        }

        [MenuItem(k_Root + "다음 조 시작")]
        static void NextGroup()
        {
            var flow = Flow();
            if (flow != null && !flow.StartNextGroup())
                Debug.Log("[A 시험] 아직 다음 조를 시작할 수 없다 (조 사이가 아니거나 표적이 오는 중)");
        }

        [MenuItem(k_Root + "다시 하기")]
        static void Retry() => Flow()?.Retry();

        [MenuItem(k_Root + "처음으로")]
        static void ReturnToIntro() => Flow()?.ReturnToIntro();

        // 표적 이송 (교육 흐름과는 아직 안 이음)
        [MenuItem(k_Root + "표적 가까이")]
        static void TargetNear() => Carrier()?.MoveNear();

        [MenuItem(k_Root + "표적 멀리 (25 m)")]
        static void TargetFar() => Carrier()?.MoveFar();

        [MenuItem(k_Root + "표적 교체 (왔다가 25 m)")]
        static void TargetExchange() => Carrier()?.Exchange();

        static TargetCarrier Carrier()
        {
            var found = Object.FindFirstObjectByType<TargetCarrier>();
            if (found == null)
                Debug.LogWarning("[A 시험] 씬에 TargetCarrier 가 없다 — A/만들기/표적 레일에 매달기");
            return found;
        }

        [MenuItem(k_Root + "표적 가까이", true)]
        [MenuItem(k_Root + "표적 멀리 (25 m)", true)]
        [MenuItem(k_Root + "표적 교체 (왔다가 25 m)", true)]
        [MenuItem(k_Root + "사선으로 이동", true)]
        [MenuItem(k_Root + "학습 모드 시작", true)]
        [MenuItem(k_Root + "평가 모드 시작", true)]
        [MenuItem(k_Root + "지금 단계 완료", true)]
        [MenuItem(k_Root + "탄창 끼우기", true)]
        [MenuItem(k_Root + "탄창 빼기", true)]
        [MenuItem(k_Root + "다음 조 시작", true)]
        [MenuItem(k_Root + "다시 하기", true)]
        [MenuItem(k_Root + "처음으로", true)]
        static bool IsPlaying() => EditorApplication.isPlaying;

        static RangeTrainingFlow Flow()
        {
            var found = Object.FindFirstObjectByType<RangeTrainingFlow>();
            if (found == null)
                Debug.LogWarning("[A 시험] 씬에 RangeTrainingFlow 가 없다");
            return found;
        }
    }
}
