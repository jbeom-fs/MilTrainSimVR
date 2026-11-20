using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace MilTrainSimVR.A.EditorTools
{
    // 총기 통합 — A 사격용 K2C1(K2C1_Rifle.prefab)에 C 기능 · C 부품을 붙인다 (11/17).
    //   · 1단계 두 손 파지(K2C1RifleHands): 방아쇠 손 자리 Pose_TriggerHand · 받치는 손 자리 Pose_SupportHand — C_K2C1 의 같은 이름 자리와 같은 값
    //     (A · C 총은 같은 K2C1.prefab 모델). 잡기 변환기 순서: XRGeneralGrabTransformer → 두 손 파지 → 반동 RifleRecoil (C_K2C1 과 같다)
    //   · 2단계 장전 손잡이 부품에 K2C1ChargingHandleHand — 잡으면 손이 손잡이를 감싼다
    //   · 4단계 총에 K2C1Gunfire — C 시가지에 총소리 · 탄착 · 명중을 알린다
    //   · 5단계 탄창 = C 것: C MagazineWell(C_K2C1 과 같은 값, 탄창 없이 시작) + 다리 CMagazineFeed, A 총의 탄창 삽입구 칸 = 다리
    //     레일(받치는 손) 자리를 C 탄창 빼는 자리와 안 겹치게 앞으로 줄임 — C 삽입구가 A 손(K2C1RifleHands)의 레일 자리를 모르기 때문 (임시, C 답 대기)
    // C 쪽 · 손 컴포넌트는 asmdef 없는 폴더(Assembly-CSharp)라 이 편집기 코드에서 이름으로 붙인다. 다시 눌러도 같다.
    // 프리팹을 고치므로 사로 총(K2C1_Range) · 기능 점검 총(K2C1_Rifle) 모두에 들어간다.
    static class RifleHandsBuilder
    {
        const string k_Path = "Assets/A/Prefabs/K2C1_Rifle.prefab";
        const string k_HandsType = "MilTrainSimVR.A.Hands.K2C1RifleHands, Assembly-CSharp";
        const string k_HandleHandType = "MilTrainSimVR.A.Hands.K2C1ChargingHandleHand, Assembly-CSharp";
        const string k_GunfireType = "MilTrainSimVR.A.CBridge.K2C1Gunfire, Assembly-CSharp";
        const string k_FeedType = "MilTrainSimVR.A.CBridge.CMagazineFeed, Assembly-CSharp";
        const string k_CWellType = "MilTrainSimVR.UrbanCombat.MagazineWell, Assembly-CSharp";
        const string k_CMagazineType = "MilTrainSimVR.UrbanCombat.RifleMagazine, Assembly-CSharp";
        const string k_OldWellType = "MilTrainSimVR.A.Rifle.MagazineWell";   // 11/17 전 A 복사본 — 있으면 뺀다
        const string k_CMagazinePath = "Assets/C/Prefabs/C_Magazine.prefab";
        const string k_HandlePath = "HingePivot/Part_Upper/Part_ChargingHandle";   // K2C1RifleBuilder 와 같은 자리

        // C_K2C1 (11/17 HEAD) 의 자리 그대로 — 총 기준
        static readonly Vector3 k_TriggerPosition = new Vector3(-0.002f, 0.019f, -0.007f);
        static readonly Quaternion k_TriggerRotation = new Quaternion(-0.5f, 0f, 0f, 0.866025f);
        static readonly Vector3 k_SupportPosition = new Vector3(-0.028f, 0.065f, 0.333f);
        static readonly Quaternion k_SupportRotation = new Quaternion(-0.161291f, -0.214031f, 0.642735f, 0.717682f);

        // 레일 자리 — C 는 z 0.21 ~ 0.53 인데 C 탄창 빼는 자리(z 0.11 ~ 0.25)와 앞뒤 4 cm 겹친다 → 뒤끝을 0.25 로 (임시)
        static readonly Bounds k_SupportZone = new Bounds(new Vector3(0f, 0.08f, 0.39f), new Vector3(0.18f, 0.18f, 0.28f));

        [MenuItem("A/만들기/사격용 K2C1 C 연동 (손 · 탄창 · 총소리)")]
        static void Build()
        {
            var handsType = System.Type.GetType(k_HandsType);
            var handleHandType = System.Type.GetType(k_HandleHandType);
            var gunfireType = System.Type.GetType(k_GunfireType);
            var feedType = System.Type.GetType(k_FeedType);
            var cWellType = System.Type.GetType(k_CWellType);
            var cMagazineType = System.Type.GetType(k_CMagazineType);
            if (handsType == null || handleHandType == null || gunfireType == null || feedType == null || cWellType == null || cMagazineType == null)
            {
                Debug.LogWarning("[A] 손 · C 연동 컴포넌트를 못 찾았다 (컴파일 확인)");
                return;
            }
            var cMagazinePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_CMagazinePath);
            Component cMagazine = cMagazinePrefab != null ? cMagazinePrefab.GetComponent(cMagazineType) : null;
            if (cMagazine == null)
            {
                Debug.LogWarning($"[A] C 탄창 프리팹이 없다 — {k_CMagazinePath}");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(k_Path);
            try
            {
                Transform trigger = Pose(root.transform, "Pose_TriggerHand", k_TriggerPosition, k_TriggerRotation);
                Transform support = Pose(root.transform, "Pose_SupportHand", k_SupportPosition, k_SupportRotation);

                var hands = root.GetComponent(handsType) as XRBaseGrabTransformer;
                if (hands == null)
                    hands = root.AddComponent(handsType) as XRBaseGrabTransformer;
                var so = new SerializedObject(hands);
                so.FindProperty("m_TriggerHandPose").objectReferenceValue = trigger;
                so.FindProperty("m_SupportHandPose").objectReferenceValue = support;
                so.FindProperty("m_SupportZone").boundsValue = k_SupportZone;
                so.ApplyModifiedPropertiesWithoutUndo();

                var grab = root.GetComponent<XRGrabInteractable>();
                var general = root.GetComponent<XRGeneralGrabTransformer>();
                var single = new List<XRBaseGrabTransformer>();
                if (general != null) single.Add(general);
                single.Add(hands);
                foreach (var t in root.GetComponents<XRBaseGrabTransformer>())
                {
                    if (t != general && t != hands)
                        single.Add(t);   // 반동 (RifleRecoil) — 마지막
                }
                grab.startingSingleGrabTransformers = single;

                Transform handle = root.transform.Find(k_HandlePath);
                if (handle == null)
                    Debug.LogWarning($"[A] 장전 손잡이를 못 찾았다 — {k_HandlePath}");
                else if (handle.GetComponent(handleHandType) == null)
                    handle.gameObject.AddComponent(handleHandType);

                if (root.GetComponent(gunfireType) == null)
                    root.AddComponent(gunfireType);

                SetupMagazine(root, cWellType, feedType, cMagazine);

                PrefabUtility.SaveAsPrefabAsset(root, k_Path);
                Debug.Log($"[A] 사격용 K2C1 C 연동: {k_Path} — 변환기 {string.Join(" → ", single.ConvertAll(t => t.GetType().Name))}, 탄창 C MagazineWell + CMagazineFeed");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // 5단계 — 탄창 삽입구를 C 것으로. A 복사본이 있으면 빼고, C 삽입구 값은 C_K2C1 과 같게 (탄창 없이 시작만 다르다)
        static void SetupMagazine(GameObject root, System.Type cWellType, System.Type feedType, Component cMagazine)
        {
            foreach (var c in root.GetComponents<MonoBehaviour>())
            {
                if (c != null && c.GetType().FullName == k_OldWellType)
                    Object.DestroyImmediate(c, true);
            }
            var well = root.GetComponent(cWellType);
            if (well == null)
                well = root.AddComponent(cWellType);
            var wso = new SerializedObject(well);
            wso.FindProperty("m_Seated").objectReferenceValue = root.transform.Find("Part_Magazine")?.gameObject;
            wso.FindProperty("m_FeedPoint").objectReferenceValue = root.transform.Find("Attach_MagFeed");
            wso.FindProperty("m_MagazinePrefab").objectReferenceValue = cMagazine;
            wso.FindProperty("m_StartRounds").intValue = -1;   // 탄창 없이 시작 — 사로 · 기능 점검은 탄창을 받아서 끼운다
            wso.ApplyModifiedPropertiesWithoutUndo();
            if (wso.FindProperty("m_Seated").objectReferenceValue == null || wso.FindProperty("m_FeedPoint").objectReferenceValue == null)
                Debug.LogWarning("[A] Part_Magazine · Attach_MagFeed 를 못 찾았다 — C 탄창 삽입구 칸을 확인");

            var feed = root.GetComponent(feedType);
            if (feed == null)
                feed = root.AddComponent(feedType);
            var fso = new SerializedObject(feed);
            fso.FindProperty("m_MagazinePrefab").objectReferenceValue = cMagazine;
            fso.ApplyModifiedPropertiesWithoutUndo();

            var rifle = root.GetComponent<MilTrainSimVR.A.Rifle.K2C1Rifle>();
            var rso = new SerializedObject(rifle);
            rso.FindProperty("m_Well").objectReferenceValue = feed;
            rso.ApplyModifiedPropertiesWithoutUndo();
        }

        static Transform Pose(Transform root, string name, Vector3 position, Quaternion rotation)
        {
            Transform pose = root.Find(name);
            if (pose == null)
            {
                pose = new GameObject(name).transform;
                pose.SetParent(root, false);
            }
            pose.localPosition = position;
            pose.localRotation = rotation;
            return pose;
        }
    }
}
