using System.Collections.Generic;
using MilTrainSimVR.A.Rifle;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace MilTrainSimVR.A.EditorTools
{
    // 사격용 K2C1 프리팹(Assets/A/Prefabs/K2C1_Rifle.prefab)을 K2C1.prefab 의 Variant 로 만든다. 설정을 바꾸려면 여기를 고치고 다시 만든다.
    //   · 총 전체: 양손 잡기 (손잡이 Attach_Grip, 앞손 Attach_Foregrip), 반동, K2C1Rifle
    //   · 장전 손잡이(Part_ChargingHandle): 따로 잡혀 뒤로만 당겨지는 ChargingHandleSlide (장전 손잡이만 움직인다)
    // 총 잡기 설정은 C 의 C_K2C1(시가지 전투)과 같은 방식이다 — 손잡이 위치 값도 같게 했다.
    static class K2C1RifleBuilder
    {
        const string k_BasePath = "Assets/A/Prefabs/K2C1.prefab";
        const string k_Path = "Assets/A/Prefabs/K2C1_Rifle.prefab";
        const string k_HandlePath = "HingePivot/Part_Upper/Part_ChargingHandle";
        const float k_MinHandleSize = 0.03f;   // 장전 손잡이 잡기 상자 최소 크기 — 작은 부품도 손에 걸리게

        [MenuItem("A/만들기/사격용 K2C1 프리팹")]
        static void Build()
        {
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_BasePath);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            root.name = "K2C1_Rifle";
            var handle = root.transform.Find(k_HandlePath);

            var grip = NewChild(root.transform, "Attach_Grip", new Vector3(0f, -0.03f, 0f));
            var foregrip = NewChild(root.transform, "Attach_Foregrip", new Vector3(0f, 0.07f, 0.33f));

            // 총 몸 잡기 상자 — 장전 손잡이를 뺀 모든 메시를 감싼다
            var body = root.AddComponent<BoxCollider>();
            FitBox(body, root.transform, r => !r.transform.IsChildOf(handle));

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 3.3f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;

            var general = root.AddComponent<XRGeneralGrabTransformer>();
            var recoil = root.AddComponent<RifleRecoil>();
            var grab = root.AddComponent<XRGrabInteractable>();
            grab.selectMode = InteractableSelectMode.Multiple;
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.attachTransform = grip;
            grab.secondaryAttachTransform = foregrip;
            grab.useDynamicAttach = false;
            grab.distanceCalculationMode = XRBaseInteractable.DistanceCalculationMode.ColliderPosition;
            grab.colliders.Clear();
            grab.colliders.Add(body);   // 장전 손잡이 상자는 넣지 않는다 — 그건 장전 손잡이 잡기 것
            // 손 위치를 먼저 정하고(General) 그 위에 반동을 더한다
            grab.startingSingleGrabTransformers = new List<XRBaseGrabTransformer> { general, recoil };
            grab.startingMultipleGrabTransformers = new List<XRBaseGrabTransformer> { general, recoil };

            // 장전 손잡이
            var handleBox = handle.gameObject.AddComponent<BoxCollider>();
            FitBox(handleBox, handle, r => r.transform.IsChildOf(handle));
            handleBox.size = Vector3.Max(handleBox.size, Vector3.one * k_MinHandleSize);
            var handleBody = handle.gameObject.AddComponent<Rigidbody>();
            handleBody.isKinematic = true;
            handleBody.useGravity = false;
            var handleGrab = handle.gameObject.AddComponent<XRGrabInteractable>();
            // 손이 상자 안에 있으면 거리 0 → 총 몸(중심까지 거리)보다 먼저 잡힌다
            handleGrab.distanceCalculationMode = XRBaseInteractable.DistanceCalculationMode.ColliderVolume;
            handleGrab.colliders.Clear();
            handleGrab.colliders.Add(handleBox);
            var slide = handle.gameObject.AddComponent<ChargingHandleSlide>();
            var slideSo = new SerializedObject(slide);
            slideSo.FindProperty("m_Frame").objectReferenceValue = root.transform;
            // 가스 활대·노리쇠 뭉치는 같이 움직이지 않는다 — 모델에서 가스 활대 뒤끝과 상부 본체 뒤끝 사이가 2 cm 뿐이라
            // 9 cm 당기면 본체 밖으로 튀어나온다 (10/27 헤드셋)
            slideSo.FindProperty("m_MoveWith").arraySize = 0;
            slideSo.ApplyModifiedPropertiesWithoutUndo();

            // 영점·탄도 (10/29, C RifleBallistics 복사) — 가늠자·가늠쇠·총구 기준점으로 25m 영점 총열을 계산한다
            var ballistics = root.AddComponent<RifleBallistics>();
            var ballisticsSo = new SerializedObject(ballistics);
            ballisticsSo.FindProperty("m_RearSight").objectReferenceValue = FindDeep(root.transform, "Anchor_RearSight");
            ballisticsSo.FindProperty("m_FrontSight").objectReferenceValue = FindDeep(root.transform, "Anchor_FrontSight");
            ballisticsSo.FindProperty("m_Muzzle").objectReferenceValue = root.transform.Find("Anchor_Muzzle");
            ballisticsSo.ApplyModifiedPropertiesWithoutUndo();

            var rifle = root.AddComponent<K2C1Rifle>();
            var rifleSo = new SerializedObject(rifle);
            rifleSo.FindProperty("m_Ballistics").objectReferenceValue = ballistics;
            rifleSo.FindProperty("m_Muzzle").objectReferenceValue = root.transform.Find("Anchor_Muzzle");
            rifleSo.FindProperty("m_ChargingHandle").objectReferenceValue = slide;
            rifleSo.FindProperty("m_Recoil").objectReferenceValue = recoil;
            rifleSo.FindProperty("m_Audio").objectReferenceValue = audio;
            rifleSo.FindProperty("m_Selector").objectReferenceValue = root.transform.Find("Part_Selector");
            rifleSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, k_Path);
            Debug.Log($"[A] 사격용 K2C1 프리팹 저장: {k_Path} (몸 상자 {body.center:F3} / {body.size:F3}, 장전 손잡이 상자 {handleBox.center:F3} / {handleBox.size:F3})");
            Object.DestroyImmediate(root);
        }

        static Transform FindDeep(Transform parent, string name)
        {
            foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
                if (t.name == name)
                    return t;
            return null;
        }

        static Transform NewChild(Transform parent, string name, Vector3 localPosition)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            return t;
        }

        // 고른 메시들을 box 의 로컬 공간에서 감싸도록 상자를 맞춘다
        static void FitBox(BoxCollider box, Transform space, System.Func<Renderer, bool> include)
        {
            var bounds = new Bounds();
            bool any = false;
            foreach (var filter in space.root.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<Renderer>();
                if (renderer == null || filter.sharedMesh == null || !include(renderer))
                    continue;
                var b = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = space.InverseTransformPoint(filter.transform.TransformPoint(corner));
                    if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
                    else bounds.Encapsulate(p);
                }
            }
            box.center = bounds.center;
            box.size = bounds.size;
        }
    }
}
