using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;

namespace MilTrainSimVR.A.EditorTools
{
    // 플레이어 손 모델 (11/17 사용자: A 리그엔 손이 없고 C 리그에만 있다 → 모두 손이 있게 통일).
    // C 리그(C_XR Origin — C 의 CombatAssetsBuilder.BuildPlayerRig)와 똑같이 맞춘다. 손은 C 프리팹을 그대로 쓴다 (한 벌로, 복사하지 않음):
    //   · Camera Offset 아래에 C_ControllerHand_L · R (컨트롤러를 따라가고, 그립 · 트리거 · 엄지에 맞춰 손가락을 굽힌다 — C ControllerHandPose)
    //   · 컨트롤러 모양(Left/Right Controller Visual)은 끈다
    //   · 손으로 잡는 점(Near-Far Interactor 의 근접 캐스터 원점)을 컨트롤러 앞(손보다 10 cm 앞)에서 손 모양 원점(= 그립 자세, 주먹 가운데)의 C_PalmPoint 로.
    //     멀리 가리키는 광선(원거리 캐스터)은 그대로 컨트롤러 앞에서 나간다
    // 지금 열린 씬의 XR Origin 모두에 맞춘다 — 이미 된 것(C 리그 포함)은 그대로라 다시 눌러도 같다. 씬을 저장한다.
    static class RigHandsBuilder
    {
        const string k_LeftPrefab = "Assets/C/Prefabs/C_ControllerHand_L.prefab";
        const string k_RightPrefab = "Assets/C/Prefabs/C_ControllerHand_R.prefab";
        const string k_PalmName = "C_PalmPoint";   // C 리그와 같은 이름 — C 의 HandReach 가 이 점 위의 손 모양을 찾는다

        [MenuItem("A/만들기/리그에 손 모델 (C 손과 같게)")]
        static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var left = AssetDatabase.LoadAssetAtPath<GameObject>(k_LeftPrefab);
            var right = AssetDatabase.LoadAssetAtPath<GameObject>(k_RightPrefab);
            if (left == null || right == null)
            {
                Debug.LogWarning($"[A] C 손 프리팹이 없다 — {k_LeftPrefab} · {k_RightPrefab}");
                return;
            }

            int rigs = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var origin in root.GetComponentsInChildren<Unity.XR.CoreUtils.XROrigin>(true))
                {
                    Transform offset = origin.transform.Find("Camera Offset");
                    if (offset == null)
                        continue;
                    Transform handL = Hand(left, offset);
                    Transform handR = Hand(right, offset);
                    foreach (NearFarInteractor interactor in origin.GetComponentsInChildren<NearFarInteractor>(true))
                    {
                        Transform hand = interactor.handedness == InteractorHandedness.Left ? handL
                            : interactor.handedness == InteractorHandedness.Right ? handR : null;
                        if (hand != null)
                            SetupController(interactor, hand);
                    }
                    Debug.Log($"[A] 손 모델: {origin.name} ({scene.name})");
                    rigs++;
                }
            }
            if (rigs == 0)
            {
                Debug.Log($"[A] 손 모델: {scene.name} — XR Origin 이 없다");
                return;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // C 리그와 같은 자리 — Camera Offset 바로 아래 (손은 TrackedPoseDriver 로 컨트롤러를 따로 따라간다). 있으면 그것
        static Transform Hand(GameObject prefab, Transform offset)
        {
            Transform hand = offset.Find(prefab.name);
            if (hand == null)
            {
                hand = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, offset)).transform;
                hand.localPosition = Vector3.zero;
                hand.localRotation = Quaternion.identity;
            }
            if (hand.Find(k_PalmName) == null)
            {
                var palm = new GameObject(k_PalmName).transform;
                palm.SetParent(hand, false);
            }
            return hand;
        }

        static void SetupController(NearFarInteractor interactor, Transform hand)
        {
            Transform controller = interactor.transform.parent;
            if (controller != null)
            {
                foreach (Transform child in controller)
                {
                    if (child.name.EndsWith("Controller Visual"))
                        child.gameObject.SetActive(false);
                }
            }
            if (!(interactor.nearInteractionCaster is InteractionCasterBase caster))
            {
                Debug.LogWarning($"[A] {interactor.name} 의 근접 캐스터가 없어 잡는 점을 그대로 둔다");
                return;
            }
            var so = new SerializedObject(caster);
            so.FindProperty("m_CastOrigin").objectReferenceValue = hand.Find(k_PalmName);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
