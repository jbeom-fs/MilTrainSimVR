using UnityEngine;
using UnityEngine.SceneManagement;

namespace MilTrainSimVR.A.Common
{
    // 허브(A_Hub) ↔ 과정 씬 오가기 (11/09). 씬을 바꿔도 남는 정적 값 두 개만 들고 있다.
    //   · Current — 허브에서 고른 과정 ID (예: A_Assembly · A_Range · B_FirstAid · B_CBRN · C_Urban). 과정 씬은 이 값으로 시작 자리를 고른다.
    //     과정 씬을 에디터에서 바로 열어 실행하면 null 이다.
    //   · Last — 마지막으로 들어갔던 과정. 허브로 돌아오면 그 과정 입구 앞에 선다 (HubStart).
    // 과정 씬에서 허브로 돌아갈 때는 ReturnToHub() — B · C 씬은 이 코드를 몰라도 허브 씬(HubScenePath)을 불러오기만 하면 된다.
    public static class CourseRoute
    {
        public const string HubScenePath = "Assets/A/Scenes/A_Hub.unity";

        public static string Current { get; private set; }
        public static string Last { get; private set; }

        public static void Enter(string courseId, string scenePath)
        {
            Current = courseId;
            Last = courseId;
            Load(scenePath);
        }

        public static void ReturnToHub()
        {
            Current = null;
            Load(HubScenePath);
        }

        // 빌드 목록에 있는지. 에디터에서는 목록에 없어도 경로만 맞으면 불러온다 (공용 빌드 목록을 안 바꾸고 시험하려고)
        public static bool CanLoad(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath))
                return false;
            if (SceneUtility.GetBuildIndexByScenePath(scenePath) >= 0)
                return true;
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(scenePath) != null;
#else
            return false;
#endif
        }

        static void Load(string scenePath)
        {
            Debug.Log($"[Course] {scenePath} 불러오기 (과정 {Current ?? "-"})");
#if UNITY_EDITOR
            if (SceneUtility.GetBuildIndexByScenePath(scenePath) < 0)
            {
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scenePath, new LoadSceneParameters(LoadSceneMode.Single));
                return;
            }
#endif
            SceneManager.LoadScene(scenePath, LoadSceneMode.Single);
        }
    }
}
