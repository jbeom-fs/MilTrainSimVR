using System.Collections.Generic;
using MilTrainSimVR.A.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Jump;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace MilTrainSimVR.UrbanCombat.EditorTools
{
    // 전투에 쓰는 자산을 만든다 — 메뉴 MilTrainSimVR → C → 전투 자산 만들기.
    //   · C_K2C1.prefab: 플레이어 소총. A 의 K2C1.prefab 의 Variant(A 가 모델을 고치면 따라온다, Assets/A 는 안 건드림)에
    //     잡기(두 손)·쏘기(TempRifle)·반동·물리·충돌체·총구 불빛·탄도선을 붙인다 — 설정은 임시 소총(C_TempRifle)에서 그대로 옮긴다 (10/26)
    //     + 탄창 삽입구(MagazineWell)·장전 손잡이(RifleChargingHandle) (10/28) + 영점·탄도(RifleBallistics) (10/29)
    //   · C_Magazine.prefab: 손에 드는 탄창 — K2C1 의 탄창 부품과 같은 모델 (10/28)
    //     + 윗면에 남은 탄이 보이는 윗부분 (11/12, MagazineRoundsBuilder — 좌상탄 · 우상탄 · 한 발 · 빈 탄창)
    //   · C_XR Origin.prefab: 손의 잡기 점을 손 모양의 주먹 가운데로 옮기고, 가슴 탄창(MagazinePouch)을 붙인다 (10/28)
    //     — 10/29 부터 방탄복(Models/PlateCarrier, 블렌더로 만듦) 앞 주머니 3개에 탄창 6칸
    //     + 조작 설정 (11/12, TunePlayerControls — 스틱은 늘 이동 · 회전, 점프 버튼 옮김, 멀미 비네트 줄임)
    //     + 소리 (11/16, AddCombatAudio — 사격음 · 탄피 · 탄창 떨어짐 · 발소리). 총의 조작 소리는 AssignRifleSounds
    //   · C_EnemyDummy.prefab: 적 병사 — 11/03 부터 EnemySoldierBuilder 가 만든다 (병사 모델 · 애니메이션 · 뼈마다 부위 판정).
    //     플레이어를 보면 쏘고(EnemyShooter), 길찾기(NavMeshAgent)로 자리를 지키거나 순찰한다(EnemyMover) — 경로는 씬에서 적마다 넣는다 (10/27)
    //   · C_WoundVignette.mat: 플레이어가 맞았을 때 시야 가장자리를 붉게 하는 비네트 재질
    // 치수를 바꾸려면 아래 숫자를 고치고 메뉴를 다시 누른다 — 프리팹을 통째로 다시 만든다
    public static class CombatAssetsBuilder
    {
        const string k_K2C1Path = "Assets/A/Prefabs/K2C1.prefab";
        const string k_TempRiflePath = "Assets/C/Prefabs/C_TempRifle.prefab";
        const string k_PlayerRiflePath = "Assets/C/Prefabs/C_K2C1.prefab";
        const string k_DummyPath = "Assets/C/Prefabs/C_EnemyDummy.prefab";
        const string k_MagazinePath = "Assets/C/Prefabs/C_Magazine.prefab";
        const string k_PlayerRigPath = "Assets/C/Prefabs/C_XR Origin.prefab";
        const string k_VignetteMaterialPath = "Assets/C/Materials/C_WoundVignette.mat";

        // 방탄복 — 모델·주머니 자리·천 결은 Models/PlateCarrier/Source~/build_plate_carrier.py(블렌더)가 만든다
        const string k_VestModelPath = "Assets/C/Models/PlateCarrier/C_PlateCarrier.fbx";
        const string k_VestLayoutPath = "Assets/C/Models/PlateCarrier/C_PlateCarrier_Layout.json";
        const string k_VestNormalPath = "Assets/C/Models/PlateCarrier/Textures/C_Cordura_N.png";
        const string k_VestMaterialFolder = "Assets/C/Materials/PlateCarrier";

        // 손 모양(그립 자세 = 주먹 가운데)이 XRI 가 물건을 붙이는 점(조준 자세 = 컨트롤러 앞쪽)에서 떨어진 만큼 — 10/22 헤드셋(VD·Quest)에서 잰 값 (조준 자세 기준, m).
        // 총은 조준 자세에 붙으므로 총의 잡는 점을 이만큼 반대로 옮겨야 손잡이가 주먹 안에 온다. 회전 차이(X -60°)는 그대로 두어 총열은 컨트롤러가 가리키는 쪽을 본다
        static readonly Vector3 k_AimToGrip = new Vector3(0f, -0.030f, -0.095f);

        // 주먹 가운데가 와야 할 자리 — XRI 잡기 점(Attach_Grip·Attach_Foregrip) 계산용. 총 위치는 이제 RifleHands 가 손 모양 자리로 정한다
        static readonly Vector3 k_FistOnGrip = new Vector3(0f, -0.03f, 0f);
        static readonly Vector3 k_FistOnForegrip = new Vector3(0f, -0.03f, 0f);

        // 손 모양 자리 (총 기준, RifleHands 가 손 모델을 붙이고 총을 그 손에 붙인다) — 10/28 에 손 모델을 쥔 모양으로 굽혀
        // 손가락이 감싸는 원(반지름 약 3cm)의 가운데·주먹 축을 재고, 렌더로 보며 맞췄다 (그 전엔 감으로 정해 주먹이 손잡이 아래에 떠 있었다).
        // 손 모양 뿌리 = 그립 자세: 앞(+Z) = 주먹 축(새끼 → 검지), 위(+Y) = 손목 쪽, 오른손 +X = 손등. 원의 가운데는 뿌리에서 3.3cm 아래(−Y)
        // 방아쇠 손: 권총손잡이(높이 6.5cm, 아래가 뒤로 30° 기움)를 가운데·약지·새끼가 감싸고, 엄지는 왼쪽 조정간 옆,
        //   검지는 방아쇠울 위 총몸 옆에 온다 → 뿌리 = Anchor_Grip + 이 값, X −60°
        static readonly Vector3 k_TriggerHandOffset = new Vector3(-0.002f, 0.019f, -0.007f);
        static readonly Quaternion k_TriggerHandRotation = Quaternion.Euler(-60f, 0f, 0f);
        // 받치는 손(왼손 기준): 손바닥 가운데가 레일(총열덮개, 몸통 폭 5.4 · 높이 5.5cm + 위 레일) 밑면에 붙고, 네 손가락이 오른쪽 아래 모서리를 감아
        //   옆면에 둘째·끝 마디가 닿으며, 엄지는 왼쪽 아래 모서리를 따라 앞으로 뻗는다. 팔은 왼쪽 뒤 아래에서 온다 → 이 자리·방향, 손가락 마디는 RifleHands.
        //   손 메시 꼭짓점마다 레일 겉면까지 거리를 재서, 손바닥·손가락 끝 마디가 겉면에 1mm 안으로 닿고 어디도 1.5mm 넘게 파고들지 않는 자세를 찾고
        //   렌더로 확인했다 (10/28 세 번째 — 처음엔 손가락이 레일을 뚫었고, 다음엔 손바닥이 2cm·손가락이 1.5~2cm 떨어져 떠 보였다)
        static readonly Vector3 k_SupportHandOffset = new Vector3(-0.028f, -0.035f, 0.003f);
        static readonly Quaternion k_SupportHandRotation = Quaternion.Euler(2.5f, -31f, 83f);

        // 총 충돌체를 앞뒤로 몇 m 씩 잘라 모양을 따라갈지
        const float k_ColliderSlice = 0.06f;

        [MenuItem("MilTrainSimVR/C/전투 자산 만들기 (K2C1 · 탄창 · 적 더미 · 피격 화면)", false, 50)]
        public static void Build()
        {
            var k2c1 = AssetDatabase.LoadAssetAtPath<GameObject>(k_K2C1Path);
            var temp = AssetDatabase.LoadAssetAtPath<GameObject>(k_TempRiflePath);
            if (k2c1 == null || temp == null)
            {
                Debug.LogWarning($"[C] {k_K2C1Path} 또는 {k_TempRiflePath} 가 없다");
                return;
            }
            RifleMagazine magazine = BuildMagazine(k2c1);
            BuildPlayerRifle(k2c1, temp, magazine);
            BuildPlayerRig(magazine);
            EnemySoldierBuilder.Build(k2c1, temp);
            BuildVignetteMaterial();
            AssetDatabase.SaveAssets();
            Debug.Log($"[C] 전투 자산을 만들었다 — {k_PlayerRiflePath}, {k_MagazinePath}, {k_PlayerRigPath}(탄입대), {k_DummyPath}, {k_VignetteMaterialPath}");
        }

        // ---------------------------------------------------------------- 플레이어 소총 (K2C1 Variant)

        static void BuildPlayerRifle(GameObject k2c1, GameObject temp, RifleMagazine magazine)
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(k2c1);
            root.name = "C_K2C1";
            // 위치 표시(Anchor_)는 이름으로 찾는다 — A 안내(Docs/A/1026_C공지_K2C1_프리팹_사용안내.md): 계층 경로는 바뀔 수 있다
            Transform muzzle = Find(root.transform, "Anchor_Muzzle");

            // 잡는 자리 (10/28 헤드셋에서 고침) — XRI 는 총의 이 점을 손의 조준 자세에 붙인다. 손 모양은 그립 자세(주먹 가운데)에 있으니
            // 주먹 가운데가 권총손잡이·총열덮개를 쥐도록 그 차이(k_AimToGrip)만큼 반대로 옮겨 둔다 — 전에는 손이 권총손잡이 뒤 허공을 쥐었다.
            // 두 손으로 잡으면 총은 '잡는 자리 → 앞손 자리' 방향이 '오른손 → 왼손' 방향을 보게 돈다 → 앞손 자리도 같은 만큼 옮긴다
            Transform grip = Child(root.transform, "Attach_Grip", PointIn(root.transform, "Anchor_Grip") + k_FistOnGrip - k_AimToGrip);
            Transform fore = Child(root.transform, "Attach_Foregrip", PointIn(root.transform, "Anchor_Foregrip") + k_FistOnForegrip - k_AimToGrip);

            // 총구 불빛·탄도선 — 임시 소총 것을 복사
            GameObject flash = Object.Instantiate(temp.transform.Find("Muzzle/MuzzleFlash").gameObject, muzzle, false);
            flash.name = "MuzzleFlash";
            GameObject tracer = Object.Instantiate(temp.transform.Find("Tracer").gameObject, root.transform, false);
            tracer.name = "Tracer";

            // 충돌체 — 잡기(손이 닿는 곳)·탁자에 놓기용. K2C1 모델에는 충돌체가 없다
            // 10/28: 큰 상자 3개 → 총 모양대로 (앞뒤로 k_ColliderSlice 씩 자른 상자, 탄창 빼고)
            AddFittedBoxes(root, k_ColliderSlice);

            // 물리·잡기·쏘기 — 임시 소총의 설정을 그대로 옮기고, 그 안의 '임시 소총 부품' 참조를 새 총의 같은 부품으로 바꾼다
            Copy(temp.GetComponent<Rigidbody>(), root);
            Copy(temp.GetComponent<AudioSource>(), root);
            Copy(temp.GetComponent<XRGeneralGrabTransformer>(), root);
            Copy(temp.GetComponent<RifleRecoil>(), root);
            var grab = (XRGrabInteractable)Copy(temp.GetComponent<XRGrabInteractable>(), root);
            var rifle = (TempRifle)Copy(temp.GetComponent<TempRifle>(), root);
            foreach (Component c in new Component[] { grab, rifle, root.GetComponent<RifleRecoil>(), root.GetComponent<XRGeneralGrabTransformer>() })
                Remap(c, temp, root);
            var so = new SerializedObject(grab);
            so.FindProperty("m_AttachTransform").objectReferenceValue = grip;
            so.FindProperty("m_SecondaryAttachTransform").objectReferenceValue = fore;
            so.FindProperty("m_Colliders").ClearArray();   // 비우면 자식 콜라이더를 다 쓴다
            so.ApplyModifiedPropertiesWithoutUndo();

            // 탄창 삽입구·장전 손잡이 (10/28) — 꽂힌 탄창의 모습은 K2C1 의 Part_Magazine 을 켜고 끈다.
            // 둘 다 충돌체 없이 손 자리로 잡는다(HandReach) — 그래서 위 잡기 충돌체 목록(자식 전부)에 끼어들 것이 없다
            Transform feed = Child(root.transform, "Attach_MagFeed", FeedPointIn(root.transform));
            var well = root.AddComponent<MagazineWell>();
            var wso = new SerializedObject(well);
            wso.FindProperty("m_Seated").objectReferenceValue = Find(root.transform, "Part_Magazine").gameObject;
            wso.FindProperty("m_FeedPoint").objectReferenceValue = feed;
            wso.FindProperty("m_MagazinePrefab").objectReferenceValue = magazine;
            wso.ApplyModifiedPropertiesWithoutUndo();
            var handle = root.AddComponent<RifleChargingHandle>();
            var hso = new SerializedObject(handle);
            hso.FindProperty("m_Handle").objectReferenceValue = Find(root.transform, "Part_ChargingHandle");
            hso.ApplyModifiedPropertiesWithoutUndo();

            // 손 모양 자리 (10/28 헤드셋 피드백: "손이 아니라 총이 기준") — 손 모델을 총의 이 자리에 붙인다(RifleHands). 자리 = 주먹 가운데(그립 자세)
            Transform triggerPose = Child(root.transform, "Pose_TriggerHand", PointIn(root.transform, "Anchor_Grip") + k_TriggerHandOffset);
            triggerPose.localRotation = k_TriggerHandRotation;
            Transform supportPose = Child(root.transform, "Pose_SupportHand", PointIn(root.transform, "Anchor_Foregrip") + k_SupportHandOffset);
            supportPose.localRotation = k_SupportHandRotation;
            var hands = root.AddComponent<RifleHands>();
            var handsSo = new SerializedObject(hands);
            handsSo.FindProperty("m_TriggerHandPose").objectReferenceValue = triggerPose;
            handsSo.FindProperty("m_SupportHandPose").objectReferenceValue = supportPose;
            handsSo.ApplyModifiedPropertiesWithoutUndo();

            // 숨 참기 (11/17 — A SightAim 의 숨 참기를 C 총에): 앞손 트리거로 참으면 손 떨림을 거르고 반동도 줄어든다
            var breath = root.AddComponent<RifleBreath>();

            // 잡기 변환기 순서: 손 위치(General) → 두 손 조준(RifleHands) → 숨 참기(Breath) → 반동(Recoil). 총은 이제 한 손으로만 XRI 에 잡힌다
            // 반동이 숨 참기 뒤라 참는 동안에도 반동은 걸러지지 않고 날카롭게 튄다 (크기는 RifleRecoil 이 숨 배율로 줄인다)
            var transformers = new SerializedObject(grab);
            SerializedProperty single = transformers.FindProperty("m_StartingSingleGrabTransformers");
            single.ClearArray();
            foreach (Object t in new Object[] { root.GetComponent<XRGeneralGrabTransformer>(), hands, breath, root.GetComponent<RifleRecoil>() })
            {
                single.InsertArrayElementAtIndex(single.arraySize);
                single.GetArrayElementAtIndex(single.arraySize - 1).objectReferenceValue = t;
            }
            transformers.ApplyModifiedPropertiesWithoutUndo();

            // 영점·탄도 (10/29) — 탄은 총구에서, 가늠자·가늠쇠 조준선과 25m 에서 만나게 맞춘 방향으로 나가 중력을 받는다.
            // K2C1 의 조준선은 모델 총열보다 1.06° 아래라(A 측정, Docs/A/1028_C제안_사격방향_분리.md) 총구 앞 방향 그대로 쏘면 25m 에서 40cm 위에 맞았다
            var ballistics = root.AddComponent<RifleBallistics>();
            var bso = new SerializedObject(ballistics);
            bso.FindProperty("m_RearSight").objectReferenceValue = Find(root.transform, "Anchor_RearSight");
            bso.FindProperty("m_FrontSight").objectReferenceValue = Find(root.transform, "Anchor_FrontSight");
            bso.FindProperty("m_Muzzle").objectReferenceValue = muzzle;
            bso.ApplyModifiedPropertiesWithoutUndo();

            // 확대 조준 (11/17 — 허브 사격 방식이 A 면 A 의 SightAim 을 켠다, RifleSightZoom). 꺼진 채 붙여 둔다 — A 파일은 그대로, 값만 C 총에 맞춘다
            var sight = root.AddComponent<MilTrainSimVR.A.Rifle.SightAim>();
            sight.enabled = false;
            var sso = new SerializedObject(sight);
            sso.FindProperty("m_RearSight").objectReferenceValue = Find(root.transform, "Anchor_RearSight");
            sso.FindProperty("m_FrontSight").objectReferenceValue = Find(root.transform, "Anchor_FrontSight");
            sso.FindProperty("m_OverlayMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(k_AimOverlayMaterial);
            sso.FindProperty("m_CameraNear").floatValue = 0.75f;   // 총구(0.71 m) 바로 앞 — 벽이 더 가까우면 RifleSightZoom 이 그 앞까지 당긴다 (A 1 m 면 벽 너머가 보였다)
            foreach (string sway in new[] { "m_SwayNormal", "m_SwayHolding", "m_SwayExhausted", "m_FollowNormal", "m_FollowHolding" })
                sso.FindProperty(sway).floatValue = 0f;   // 창 = 실제 총의 조준선 (흔들림 · 숨은 RifleBreath 가 총 자체에)
            sso.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent<RifleSightZoom>();

            var rso = new SerializedObject(rifle);
            rso.FindProperty("m_Muzzle").objectReferenceValue = muzzle;
            rso.FindProperty("m_Ballistics").objectReferenceValue = ballistics;
            rso.FindProperty("m_MuzzleFlash").objectReferenceValue = flash.GetComponent<MuzzleFlash>();
            rso.FindProperty("m_Tracer").objectReferenceValue = tracer.GetComponent<LineRenderer>();
            rso.FindProperty("m_Well").objectReferenceValue = well;
            rso.FindProperty("m_ChargingHandle").objectReferenceValue = handle;
            // 조정간 부품 (11/17) — B/Y 로 조정간을 바꾸면 총 왼쪽의 레버도 돈다 (각도는 A 의 K2C1Rifle 과 같은 기본값)
            rso.FindProperty("m_Selector").objectReferenceValue = Find(root.transform, "Part_Selector");
            rso.ApplyModifiedPropertiesWithoutUndo();
            AssignRifleSounds(root);

            // 씬에 놓인 A 프리팹 인스턴스를 저장하면 그 Variant 가 된다
            PrefabUtility.SaveAsPrefabAsset(root, k_PlayerRiflePath);
            Object.DestroyImmediate(root);
        }

        // ---------------------------------------------------------------- 탄창 (10/28)

        // 손에 드는 탄창 — K2C1 의 탄창 부품(Part_Magazine)과 같은 모델·같은 크기·같은 자세.
        // 원점 = 탄창 가운데(Part_Magazine 원점), 방향 = 꽂혔을 때의 총과 같음 → 꽂힌 자리에 그대로 놓으면 꼭 겹친다
        static RifleMagazine BuildMagazine(GameObject k2c1)
        {
            var rifle = (GameObject)PrefabUtility.InstantiatePrefab(k2c1);
            Transform part = Find(rifle.transform, "Part_Magazine");
            var root = new GameObject("C_Magazine");

            // 탄창 부품 안의 모델(FBX)을 같은 자세로 옮겨 온다 — FBX 와 이어진 채라 A 가 모델·재질을 고치면 따라온다
            foreach (Transform child in part)
            {
                GameObject source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(child.gameObject);
                GameObject copy = source != null && PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject)
                    ? (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform)
                    : Object.Instantiate(child.gameObject, root.transform);
                copy.name = child.name;
                copy.transform.localPosition = child.localPosition;
                copy.transform.localRotation = child.localRotation;
                copy.transform.localScale = child.localScale;
            }

            // 충돌체 — 모델에 꼭 맞춘 상자 하나 (줍기·바닥에 놓이기)
            Bounds bounds = LocalBounds(root.transform);
            AddBox(root, bounds.center, bounds.size);

            // 급탄구 — 꽂혔을 때 총의 급탄구 자리(Attach_MagFeed)와 겹치는 점
            Transform feed = Child(root.transform, "FeedPoint", part.InverseTransformPoint(rifle.transform.TransformPoint(FeedPointIn(rifle.transform))));

            var body = root.AddComponent<Rigidbody>();
            body.mass = 0.45f;   // 30발이 든 탄창 약 0.45kg
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;   // 작은 물건이 떨어지며 바닥을 뚫지 않게

            var grab = root.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.useDynamicAttach = true;   // 잡은 자리 그대로 — 총에서 뺄 때·탄입대에서 꺼낼 때 손으로 튀지 않게
            grab.throwOnDetach = true;

            var mag = root.AddComponent<RifleMagazine>();
            var so = new SerializedObject(mag);
            so.FindProperty("m_FeedPoint").objectReferenceValue = feed;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 윗면에 보이는 탄 (11/12) — 좌상탄 · 우상탄 · 한 발 · 빈 탄창. 충돌체 · 급탄구 자리를 다 정한 뒤에 붙인다 (그 계산에 끼지 않게)
            MagazineRoundsBuilder.Build(root, root.GetComponentInChildren<MeshFilter>(true), mag);

            PrefabUtility.SaveAsPrefabAsset(root, k_MagazinePath);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(rifle);
            return AssetDatabase.LoadAssetAtPath<RifleMagazine>(k_MagazinePath);
        }

        // 꽂힌 탄창 윗면(급탄구) 가운데 — 총 기준. 앞뒤는 삽입구 표시(Anchor_MagWell), 높이는 탄창 모델 맨 위.
        // 총이 원점에 돌지 않고 놓였다고 본다 (생성기가 막 꺼낸 프리팹)
        static Vector3 FeedPointIn(Transform rifle)
        {
            float top = float.MinValue;
            foreach (Renderer r in Find(rifle, "Part_Magazine").GetComponentsInChildren<Renderer>(true))
                top = Mathf.Max(top, rifle.InverseTransformPoint(r.bounds.max).y);
            return new Vector3(0f, top, PointIn(rifle, "Anchor_MagWell").z);
        }

        // 아래 렌더러들을 다 덮는 상자 — root 기준 (root 가 원점에 돌지 않고 놓였다고 본다)
        static Bounds LocalBounds(Transform root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            var b = new Bounds(root.InverseTransformPoint(renderers[0].bounds.center), Vector3.zero);
            foreach (Renderer r in renderers)
            {
                b.Encapsulate(root.InverseTransformPoint(r.bounds.min));
                b.Encapsulate(root.InverseTransformPoint(r.bounds.max));
            }
            return b;
        }

        // ---------------------------------------------------------------- 플레이어 리그: 잡기 점 · 방탄복 탄창 (10/28 · 10/29)

        // 플레이어 리그(C_XR Origin)를 고친다:
        //   · 두 손의 잡기 점(XRI 근접 캐스터 원점)을 손 모양의 주먹 가운데(C_PalmPoint)로 — 원래는 컨트롤러 앞쪽(조준 자세)이라
        //     손 모양보다 10cm 앞에서 잡혔다. 멀리 가리키는 광선(원거리 캐스터)은 그대로 컨트롤러 앞쪽에서 나간다
        //   · 방탄복과 앞 주머니의 탄창 — 몸 판정(PlayerHitboxes)의 가슴을 따라간다
        static void BuildPlayerRig(RifleMagazine magazine)
        {
            GameObject rig = PrefabUtility.LoadPrefabContents(k_PlayerRigPath);

            foreach (NearFarInteractor hand in rig.GetComponentsInChildren<NearFarInteractor>(true))
            {
                string visualName = hand.handedness == InteractorHandedness.Left ? "C_ControllerHand_L"
                    : hand.handedness == InteractorHandedness.Right ? "C_ControllerHand_R" : null;
                Transform visual = visualName != null ? Find(rig.transform, visualName) : null;
                if (visual == null || !(hand.nearInteractionCaster is InteractionCasterBase caster))
                {
                    Debug.LogWarning($"[C] {hand.name} 의 손 모양·근접 캐스터를 못 찾아 잡기 점을 그대로 둔다");
                    continue;
                }
                // 손 모양 오브젝트 = 그립 자세(주먹 가운데)를 따라간다. 그 아래 점을 잡기 점으로
                Transform palm = visual.Find("C_PalmPoint");
                if (palm == null)
                    palm = Child(visual, "C_PalmPoint", Vector3.zero);
                var cso = new SerializedObject(caster);
                cso.FindProperty("m_CastOrigin").objectReferenceValue = palm;
                cso.ApplyModifiedPropertiesWithoutUndo();
            }

            TunePlayerControls(rig);
            AddHubExit(rig);
            AddCombatAudio(rig);

            // 방탄복 (10/29 사용자: "상자 탄입대는 못생기고 대강 만든 거 같다 — 실제 VR 게임처럼 방탄복에 꽂아 두는 탄창으로") —
            // 블렌더로 만든 방탄복(그 밑 몸통 포함) 앞판에 두 개씩 들어가는 주머니 3개, 탄창 6칸에 5개를 거꾸로(바닥판이 위로) 꽂는다.
            // 그 전엔 국방색 상자(21 × 14 × 11cm)에 탄창 5개를 세워 꽂았다(10/28). 충돌체 없이 그림만(적 총알을 막지 않게),
            // 그림자도 없다(다리·팔이 없어 떠 있는 그림자가 진다). 시작할 때 MagazinePouch 가 가슴에 옮겨 붙인다
            Transform oldPouch = rig.transform.Find("C_MagazinePouch");
            if (oldPouch != null)
                Object.DestroyImmediate(oldPouch.gameObject);
            Transform vest = rig.transform.Find("C_PlateCarrier");
            if (vest == null)
            {
                vest = new GameObject("C_PlateCarrier").transform;
                vest.SetParent(rig.transform, false);
            }
            BuildVestModel(vest);
            GameObject[] tops = BuildVestSlots(vest, magazine, out Bounds grabZone);

            var pouch = rig.GetComponent<MagazinePouch>();
            if (pouch == null)
                pouch = rig.AddComponent<MagazinePouch>();
            var so = new SerializedObject(pouch);
            so.FindProperty("m_Body").objectReferenceValue = rig.GetComponent<PlayerHitboxes>();
            so.FindProperty("m_MagazinePrefab").objectReferenceValue = magazine;
            so.FindProperty("m_Visual").objectReferenceValue = vest;
            // 자리·잡는 상자·예비 수는 여기서 정한다 — 컴포넌트에 저장된 옛 값이 남지 않게. 방탄복 원점 = 가슴 가운데
            so.FindProperty("m_Offset").vector3Value = Vector3.zero;
            so.FindProperty("m_GrabZone").boundsValue = grabZone;
            so.FindProperty("m_Spares").intValue = tops.Length - 1;
            SerializedProperty topsProp = so.FindProperty("m_Tops");
            topsProp.arraySize = tops.Length;
            for (int i = 0; i < tops.Length; i++)
                topsProp.GetArrayElementAtIndex(i).objectReferenceValue = tops[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(rig, k_PlayerRigPath);
            PrefabUtility.UnloadPrefabContents(rig);
        }

        // 조작 설정 (11/12 — 11/11 헤드셋에서 나온 것). XRI 기본 리그(Starter Assets)의 값을 C 리그에서만 덮어쓴다:
        //   · 스틱은 늘 이동 · 회전 — 기본 리그는 광선으로 멀리서 잡은 물건을 스틱으로 당기고 돌리게 하느라, 그동안 그 손의 이동 · 회전을
        //     끈다(ControllerInputActionManager). 총을 광선으로 잡으면 총은 RifleHands 가 손에 붙여 놓는데 XRI 는 '멀리 잡은 채'로 알아서,
        //     오른손(회전 스틱)으로 총을 든 내내 회전이 안 됐다. → 스틱으로 물건 조작 끔 + 멀리서 잡은 물건은 손으로 끌려오게(Near)
        //   · 점프 버튼 옮김 — 오른손 A 가 점프라, 오른손으로 총을 들면 A 하나에 점프와 탄창멈치(TempRifle)가 같이 걸렸다.
        //     처음엔(11/12 오전) 점프를 껐는데, 창턱을 넘어 건물에 드나들 때 점프를 쓴다 (11/12 사용자) → 점프는 살리고 버튼만 옮긴다:
        //     총을 들지 않은 손의 A/X + 오른 스틱 누르기 (JumpButton — 높이 · 누르는 시간 같은 점프 자체는 XRI JumpProvider 그대로)
        //   · 멀미 비네트(움직일 때 시야 가장자리가 검어지는 것)를 줄인다 — 구멍을 키우고 가장자리를 더 부드럽게. 끄지는 않는다.
        //     비네트 메시와 셰이더에서 잰 값 (시선 가운데에서 벗어난 각도): 전(0.75 · 0.2)에는 30° 까지만 맑고 38° 부터 완전히 검었다
        //     — 헤드셋 시야가 좌우 50° 안팎이라 바깥 12° 가 검은 띠였다. 지금 값은 38° 까지 맑고 거기서 서서히 어두워져 시야 끝(50°)에서야
        //     다 검어진다. 더 줄이려면 0.85 · 0.3 (43° 까지 맑고, 시야 끝에서도 반쯤만 어둡다)
        const float k_VignetteAperture = 0.82f;     // 맑은 구멍의 크기 — sin(맑은 각도). 1 = 안 가림
        const float k_VignetteFeathering = 0.25f;   // 가장자리가 번지는 폭 — 제곱 × 180° 만큼에 걸쳐 어두워진다

        static void TunePlayerControls(GameObject rig)
        {
            foreach (InteractionAttachController attach in rig.GetComponentsInChildren<InteractionAttachController>(true))
                attach.useManipulationInput = false;
            foreach (NearFarInteractor hand in rig.GetComponentsInChildren<NearFarInteractor>(true))
                hand.farAttachMode = InteractorFarAttachMode.Near;
            foreach (JumpProvider jump in rig.GetComponentsInChildren<JumpProvider>(true))
            {
                jump.gameObject.SetActive(true);
                JumpButton button = jump.GetComponent<JumpButton>();
                if (button == null)
                    button = jump.gameObject.AddComponent<JumpButton>();
                // 점프 입력을 XRI 기본 액션(오른손 A) 대신 JumpButton 에서 읽는다
                jump.jumpInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ObjectReference;
                jump.jumpInput.SetObjectReference(button);
                // 공중에서 한 번 더 뛰기 끔 (11/12 사용자: "점프를 할 때 더블점프가 된다") — XRI 기본(1)이 남아 있어,
                // 뛰고 나서 공중에서 다시 누르면 2.0 m 까지 올라갔다 (보통 점프 1.25 m, Play 모드에서 잼)
                jump.inAirJumpCount = 0;
                jump.unlimitedInAirJumps = false;
            }
            // 손에 든 총 · 탄창을 밟고 설 땅으로 보지 않게 (11/12 사용자: 점프 뒤 착지하지 않고 공중부양) — HeldItemGroundFilter
            foreach (GravityProvider gravity in rig.GetComponentsInChildren<GravityProvider>(true))
            {
                if (gravity.GetComponent<HeldItemGroundFilter>() == null)
                    gravity.gameObject.AddComponent<HeldItemGroundFilter>();
            }
            foreach (TunnelingVignetteController vignette in rig.GetComponentsInChildren<TunnelingVignetteController>(true))
            {
                vignette.defaultParameters.apertureSize = k_VignetteAperture;
                vignette.defaultParameters.featheringEffect = k_VignetteFeathering;
            }
        }

        // 연병장(허브)으로 나가는 길 (11/12 — A 통지 Docs/A/1110_BC통지_허브연결.md, 사용자 결정):
        //   · C_HubPortal.prefab: A 의 HubPortal_ToHub 의 Variant — 과정 ID(C_Urban)만 넣었다. A 프리팹은 그대로 두고 따라간다
        //   · 리그에 MatchExit(성공하면 그 포탈을 세우고, 실패하면 결과판 버튼으로 나간다)와 A 의 ScreenFader(넘어갈 때 화면 가림)를 붙인다
        // A 쪽 자산이 없으면(경로가 바뀌었으면) 경고만 하고 넘어간다 — 그때는 출구가 없다
        const string k_HubPortalSourcePath = "Assets/A/Prefabs/HubPortal_ToHub.prefab";
        const string k_HubPortalPath = "Assets/C/Prefabs/C_HubPortal.prefab";
        const string k_ScreenFadeMaterialPath = "Assets/A/Materials/Common/ScreenFade.mat";

        static void AddHubExit(GameObject rig)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(k_HubPortalSourcePath);
            if (source == null || source.GetComponent<HubPortal>() == null)
            {
                Debug.LogWarning($"[C] {k_HubPortalSourcePath} 가 없어 허브 출구를 붙이지 않는다");
                return;
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            instance.name = "C_HubPortal";
            var portal = new SerializedObject(instance.GetComponent<HubPortal>());
            portal.FindProperty("m_CourseId").stringValue = MatchExit.CourseId;
            portal.ApplyModifiedPropertiesWithoutUndo();
            // 씬에 꺼낸 A 프리팹 인스턴스를 저장하면 그 Variant 가 된다
            GameObject variant = PrefabUtility.SaveAsPrefabAsset(instance, k_HubPortalPath);
            Object.DestroyImmediate(instance);

            var exit = rig.GetComponent<MatchExit>();
            if (exit == null)
                exit = rig.AddComponent<MatchExit>();
            var so = new SerializedObject(exit);
            so.FindProperty("m_PortalPrefab").objectReferenceValue = variant;
            so.ApplyModifiedPropertiesWithoutUndo();

            var material = AssetDatabase.LoadAssetAtPath<Material>(k_ScreenFadeMaterialPath);
            if (material == null)
            {
                Debug.LogWarning($"[C] {k_ScreenFadeMaterialPath} 가 없어 화면 가림 없이 넘어간다");
                return;
            }
            var fader = rig.GetComponent<ScreenFader>();
            if (fader == null)
                fader = rig.AddComponent<ScreenFader>();
            // 머리는 비워 둔다(Main Camera), 반지름 · 색은 A 기본값(0.25m · 검정) — 허브 · B 씬과 같다
            var fso = new SerializedObject(fader);
            fso.FindProperty("m_Material").objectReferenceValue = material;
            fso.ApplyModifiedPropertiesWithoutUndo();
        }

        // 소리 (11/16) — 클립은 임시: 사용자가 가져온 녹음을 잘라 Assets/C/Audio 에 둔 것. 나중에 만든 것 · 산 것을 같은 이름으로 덮어쓰면 된다.
        //   · 총(C_K2C1): 조작 소리(빈 방아쇠 · 탄창 끼움 두 가지 · 뺌 · 장전 손잡이 당김 · 복귀 · 조정간)는 총에 붙은 3D 소리(TempRifle)
        //   · 리그(C_XR Origin): 사격음 · 탄피 · 탄창 떨어짐(CombatAudio — 쏜 자리 · 거리 · 벽 · 실내외)과 발소리(FootstepAudio)
        // 빈 방아쇠 '딸깍'은 아직 클립이 없다 (비워 둠)
        const string k_AudioFolder = "Assets/C/Audio";

        // A 확대 조준의 가늠자 그림 (A 파일 — 읽기만)
        const string k_AimOverlayMaterial = "Assets/A/Materials/Aim/AimSightPicture.mat";

        static AudioClip Clip(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{k_AudioFolder}/{path}.wav");
            if (clip == null)
                Debug.LogWarning($"[C] 소리 {k_AudioFolder}/{path}.wav 가 없다 — 그 소리는 비워 둔다");
            return clip;
        }

        static void SetClips(SerializedObject so, string property, params string[] paths)
        {
            SerializedProperty list = so.FindProperty(property);
            list.arraySize = 0;
            foreach (string path in paths)
            {
                AudioClip clip = Clip(path);
                if (clip == null)
                    continue;
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = clip;
            }
        }

        static void AssignRifleSounds(GameObject rifle)
        {
            var audio = rifle.GetComponent<AudioSource>();
            if (audio == null)
                audio = rifle.AddComponent<AudioSource>();
            // 총에서 나는 3D 소리 — 손에 든 총이 머리에서 0.3 ~ 0.7 m 라 1 m 안은 줄이지 않는다
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            audio.rolloffMode = AudioRolloffMode.Logarithmic;
            audio.minDistance = 1f;
            audio.maxDistance = 20f;
            audio.dopplerLevel = 0f;

            var so = new SerializedObject(rifle.GetComponent<TempRifle>());
            so.FindProperty("m_Audio").objectReferenceValue = audio;
            SetClips(so, "m_MagazineInSounds", "Weapons/C_MagIn_1", "Weapons/C_MagIn_2");
            so.FindProperty("m_MagazineOutSound").objectReferenceValue = Clip("Weapons/C_MagOut");
            so.FindProperty("m_ChargeBackSound").objectReferenceValue = Clip("Weapons/C_ChargeBack");
            so.FindProperty("m_ChargeSound").objectReferenceValue = Clip("Weapons/C_ChargeForward");
            so.FindProperty("m_SelectorSound").objectReferenceValue = Clip("Weapons/C_Selector");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AddCombatAudio(GameObject rig)
        {
            var combat = rig.GetComponent<CombatAudio>();
            if (combat == null)
                combat = rig.AddComponent<CombatAudio>();
            var so = new SerializedObject(combat);
            SetClips(so, "m_ShotClips", "Weapons/C_Shot");
            SetClips(so, "m_CasingClips", "Weapons/C_Casing");
            SetClips(so, "m_MagazineDropClips", "Weapons/C_MagDrop");
            so.ApplyModifiedPropertiesWithoutUndo();

            var steps = rig.GetComponent<FootstepAudio>();
            if (steps == null)
                steps = rig.AddComponent<FootstepAudio>();
            var fso = new SerializedObject(steps);
            var names = new string[8];
            for (int i = 0; i < names.Length; i++)
                names[i] = $"Footsteps/C_Step_{i + 1}";
            SetClips(fso, "m_Steps", names);
            fso.ApplyModifiedPropertiesWithoutUndo();
        }

        // 블렌더 스크립트가 모델과 같이 쓰는 주머니 자리 (방탄복 기준 m, 유니티 좌표)
        [System.Serializable]
        class VestLayout
        {
            public VestPouch[] pouches;
            public float width, depth, wall, heightBack, heightFront;
            public float[] rows;   // 주머니 안 탄창 두 줄의 가운데 깊이 (주머니 기준)
        }

        [System.Serializable]
        class VestPouch
        {
            public float x, y, z;   // 주머니 뒤 아래 가운데 — 앞판 겉면에 붙는 점
            public float yaw;       // y 축 둘레 (도) — 앞판이 몸을 따라 휜 만큼 바깥을 본다
        }

        // 방탄복 모델 — FBX 를 그대로 놓고 메시 이름으로 재질을 붙인다.
        // 가져오기 설정도 여기서 맞춘다: 재질은 가져오지 않고, 법선은 40° 넘게 꺾인 곳만 각지게(둥글린 모서리는 매끄럽게)
        static void BuildVestModel(Transform vest)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(k_VestModelPath);
            if (importer.materialImportMode != ModelImporterMaterialImportMode.None || importer.importNormals != ModelImporterNormals.Calculate
                || !Mathf.Approximately(importer.normalSmoothingAngle, 40f) || importer.importAnimation || importer.importCameras || importer.importLights)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importNormals = ModelImporterNormals.Calculate;
                importer.normalSmoothingAngle = 40f;
                importer.importAnimation = false;
                importer.animationType = ModelImporterAnimationType.None;
                importer.importCameras = false;
                importer.importLights = false;
                importer.SaveAndReimport();
            }
            var weaveImporter = (TextureImporter)AssetImporter.GetAtPath(k_VestNormalPath);
            if (weaveImporter.textureType != TextureImporterType.NormalMap || weaveImporter.wrapMode != TextureWrapMode.Repeat)
            {
                weaveImporter.textureType = TextureImporterType.NormalMap;
                weaveImporter.wrapMode = TextureWrapMode.Repeat;
                weaveImporter.anisoLevel = 4;
                weaveImporter.SaveAndReimport();
            }
            var weave = AssetDatabase.LoadAssetAtPath<Texture2D>(k_VestNormalPath);

            if (!AssetDatabase.IsValidFolder(k_VestMaterialFolder))
                AssetDatabase.CreateFolder("Assets/C/Materials", "PlateCarrier");
            // 메시 이름 → 재질. 천 결은 UV 1 = 5cm — 띠·고무줄·벨크로는 결을 더 촘촘하게, 주머니 안은 어둡게(결 없음)
            var materials = new Dictionary<string, Material>
            {
                { "Vest_Cloth", ClothMaterial("C_VestCloth", new Color(0.30f, 0.32f, 0.20f), 0.15f, weave, 0.6f, 1f) },
                { "Vest_Webbing", ClothMaterial("C_VestWebbing", new Color(0.24f, 0.26f, 0.16f), 0.2f, weave, 0.8f, 1.7f) },
                { "Vest_Elastic", ClothMaterial("C_VestElastic", new Color(0.07f, 0.07f, 0.07f), 0.25f, weave, 0.5f, 3f) },
                { "Vest_Patch", ClothMaterial("C_VestPatch", new Color(0.27f, 0.29f, 0.19f), 0.05f, weave, 1f, 4f) },
                { "Vest_Inside", ClothMaterial("C_VestInside", new Color(0.10f, 0.11f, 0.07f), 0.05f, null, 0f, 1f) },
                { "Shirt", ClothMaterial("C_Shirt", new Color(0.22f, 0.23f, 0.18f), 0.1f, weave, 0.4f, 2f) },
            };

            Transform model = vest.Find("Model");
            if (model == null)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(k_VestModelPath), vest);
                go.name = "Model";
                model = go.transform;
            }
            model.localPosition = Vector3.zero;
            model.localRotation = Quaternion.identity;
            model.localScale = Vector3.one;
            foreach (MeshRenderer r in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (materials.TryGetValue(r.name, out Material m))
                    r.sharedMaterial = m;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        // 천 재질 (URP Lit) — 메뉴를 다시 누르면 색·결도 다시 맞춘다
        static Material ClothMaterial(string name, Color color, float smoothness, Texture normal, float normalScale, float tiling)
        {
            string path = $"{k_VestMaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = mat == null;
            if (created)
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetTexture("_BumpMap", normal);
            mat.SetFloat("_BumpScale", normalScale);
            mat.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
            BaseShaderGUI.SetMaterialKeywords(mat);
            if (created)
                AssetDatabase.CreateAsset(mat, path);
            else
                EditorUtility.SetDirty(mat);
            return mat;
        }

        // 탄창 6칸 — 주머니마다 앞뒤 두 줄. 휜 탄창을 거꾸로(급탄구가 아래, 바닥판이 위) 주머니 바닥까지 꽂는다.
        // 칸 순서 = 처음에 꽂아 두는 순서: 세 주머니의 안쪽 줄 → 가운데·오른쪽 바깥 줄 → 왼쪽 바깥 줄(처음엔 비어 있다 — 총에 꽂힌 몫).
        // 손을 대는 상자 = 탄창들을 다 덮는 상자보다 옆 5cm · 아래 3cm · 위 10cm · 뒤 2cm · 앞 10cm 넉넉하게
        static GameObject[] BuildVestSlots(Transform vest, RifleMagazine magazine, out Bounds grabZone)
        {
            var layout = JsonUtility.FromJson<VestLayout>(AssetDatabase.LoadAssetAtPath<TextAsset>(k_VestLayoutPath).text);
            Renderer magModel = magazine.GetComponentInChildren<Renderer>(true);
            GameObject magSource = PrefabUtility.GetCorrespondingObjectFromOriginalSource(magModel.gameObject);
            // 탄창 몸통(A 모델)의 꼭짓점만 — 11/12 에 붙인 윗부분(입술 · 탄)은 주머니 안에서 안 보이니 칸에 넣지 않고, 자리 계산에서도 뺀다
            List<Vector3> points = MeshPoints(magazine.transform, magModel.transform);
            FitMagazine(points, layout.heightBack - layout.wall, out float tilt, out float centerZ, out float feedEnd);
            // 탄창 기준 → 주머니 기준: 탄창 앞(휜 쪽, +Z)은 왼쪽, 급탄구(+Y)는 아래, 두께(X)는 앞뒤. 휜 만큼 tilt 로 기울여 세운다
            Quaternion inPouch = Quaternion.LookRotation(Vector3.left, Vector3.down) * Quaternion.AngleAxis(tilt, Vector3.right);

            Transform slots = vest.Find("Slots");
            if (slots == null)
            {
                slots = new GameObject("Slots").transform;
                slots.SetParent(vest, false);
            }
            var order = new (int pouch, int row)[] { (0, 0), (1, 0), (2, 0), (1, 1), (2, 1), (0, 1) };
            var tops = new GameObject[order.Length];
            var all = new Bounds();
            bool first = true;
            for (int i = 0; i < order.Length; i++)
            {
                string slotName = $"Slot{i + 1}";
                Transform slot = slots.Find(slotName);
                if (slot == null)
                {
                    slot = new GameObject(slotName).transform;
                    slot.SetParent(slots, false);
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(magSource, slot);
                    model.transform.localPosition = magModel.transform.localPosition;
                    model.transform.localRotation = magModel.transform.localRotation;
                    model.transform.localScale = magModel.transform.localScale;
                }
                foreach (Renderer r in slot.GetComponentsInChildren<Renderer>(true))
                    r.shadowCastingMode = ShadowCastingMode.Off;
                // 주머니 기준: 들어가는 부분이 가운데 · 급탄구 끝이 바닥 위 1mm · 그 줄의 깊이
                VestPouch p = layout.pouches[order[i].pouch];
                Quaternion pouchRotation = Quaternion.Euler(0f, p.yaw, 0f);
                Vector3 inside = new Vector3(centerZ, layout.wall + 0.001f + feedEnd, layout.rows[order[i].row]);
                slot.localPosition = new Vector3(p.x, p.y, p.z) + pouchRotation * inside;
                slot.localRotation = pouchRotation * inPouch;
                tops[i] = slot.gameObject;
                foreach (Vector3 q in points)
                {
                    Vector3 w = slot.localPosition + slot.localRotation * q;
                    if (first)
                        all = new Bounds(w, Vector3.zero);
                    else
                        all.Encapsulate(w);
                    first = false;
                }
            }
            Vector3 min = all.min - new Vector3(0.05f, 0.03f, 0.02f);
            Vector3 max = all.max + new Vector3(0.05f, 0.10f, 0.10f);
            grabZone = new Bounds((min + max) * 0.5f, max - min);
            return tops;
        }

        // 휜 탄창을 거꾸로 꽂을 때 주머니 안에 드는 부분(급탄구 끝에서 depth 까지)의 앞뒤 폭이 가장 좁아지는 기울기(탄창 x 축 둘레, 도)와
        // 그때 그 부분의 앞뒤 가운데 · 급탄구 끝 높이 (탄창 기준). A 가 탄창 모델을 바꾸면 다시 찾는다 — 지금 탄창은 7° 에서 8.3 → 7.6cm
        static void FitMagazine(List<Vector3> points, float depth, out float tilt, out float centerZ, out float feedEnd)
        {
            tilt = 0f;
            centerZ = 0f;
            feedEnd = 0f;
            float best = float.MaxValue;
            for (float a = -15f; a <= 15f; a += 0.5f)
            {
                Quaternion q = Quaternion.AngleAxis(a, Vector3.right);
                float top = float.MinValue;
                foreach (Vector3 p in points)
                    top = Mathf.Max(top, (q * p).y);
                float zMin = float.MaxValue, zMax = float.MinValue;
                foreach (Vector3 p in points)
                {
                    Vector3 r = q * p;
                    if (r.y < top - depth)
                        continue;
                    zMin = Mathf.Min(zMin, r.z);
                    zMax = Mathf.Max(zMax, r.z);
                }
                if (zMax - zMin < best - 1e-5f)
                {
                    best = zMax - zMin;
                    tilt = a;
                    centerZ = (zMin + zMax) * 0.5f;
                    feedEnd = top;
                }
            }
        }

        // under 아래 모든 메시 꼭짓점 — root 기준 (root 가 원점에 돌지 않고 놓였다고 본다)
        static List<Vector3> MeshPoints(Transform root, Transform under)
        {
            var points = new List<Vector3>();
            foreach (MeshFilter mf in under.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null)
                    continue;
                foreach (Vector3 v in mf.sharedMesh.vertices)
                    points.Add(root.InverseTransformPoint(mf.transform.TransformPoint(v)));
            }
            return points;
        }

        static Component Copy(Component source, GameObject target)
        {
            Component c = target.GetComponent(source.GetType());
            if (c == null)
                c = target.AddComponent(source.GetType());
            EditorUtility.CopySerialized(source, c);
            return c;
        }

        // 복사해 온 설정 안의 참조가 원래 총(from) 안을 가리키면 새 총(to)의 같은 이름·같은 종류 부품으로 바꾼다 (없으면 비움)
        static void Remap(Component target, GameObject from, GameObject to)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.GetIterator();
            while (p.Next(true))
            {
                if (p.propertyType != SerializedPropertyType.ObjectReference || p.objectReferenceValue == null)
                    continue;
                Transform t = p.objectReferenceValue is Component comp ? comp.transform : p.objectReferenceValue is GameObject go ? go.transform : null;
                if (t == null || !t.IsChildOf(from.transform))
                    continue;
                Transform match = t == from.transform ? to.transform : Find(to.transform, t.name);
                Object replacement = null;
                if (match != null)
                    replacement = p.objectReferenceValue is GameObject ? match.gameObject : (Object)match.GetComponent(p.objectReferenceValue.GetType());
                p.objectReferenceValue = replacement;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Transform Find(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                    return t;
            }
            return null;
        }

        // 그 이름의 위치 표시가 root 기준 어디인지 — 표시가 더 깊은 곳으로 옮겨져도 같은 값
        static Vector3 PointIn(Transform root, string name)
        {
            return root.InverseTransformPoint(Find(root, name).position);
        }

        static Transform Child(Transform parent, string name, Vector3 localPosition)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            return t;
        }

        // 총 모양을 따라가는 상자 충돌체 — 메시 꼭짓점을 앞뒤(z)로 slice 씩 잘라 칸마다 테두리 상자 하나 (탄창은 빼고 — 빼고 끼우는 물건이다)
        static void AddFittedBoxes(GameObject root, float slice)
        {
            var points = new System.Collections.Generic.List<Vector3>();
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || IsUnder(mf.transform, "Part_Magazine"))
                    continue;
                foreach (Vector3 v in mf.sharedMesh.vertices)
                    points.Add(root.transform.InverseTransformPoint(mf.transform.TransformPoint(v)));
            }
            float zMin = float.MaxValue, zMax = float.MinValue;
            foreach (Vector3 p in points)
            {
                zMin = Mathf.Min(zMin, p.z);
                zMax = Mathf.Max(zMax, p.z);
            }
            for (float z0 = zMin; z0 < zMax; z0 += slice)
            {
                float z1 = Mathf.Min(z0 + slice, zMax);
                bool any = false;
                var box = new Bounds();
                foreach (Vector3 p in points)
                {
                    if (p.z < z0 || p.z > z1)
                        continue;
                    if (any)
                        box.Encapsulate(p);
                    else
                        box = new Bounds(p, Vector3.zero);
                    any = true;
                }
                if (!any)
                    continue;
                // 앞뒤는 칸 전체를 덮어 상자 사이에 틈이 없게
                Vector3 center = new Vector3(box.center.x, box.center.y, (z0 + z1) * 0.5f);
                Vector3 size = new Vector3(Mathf.Max(box.size.x, 0.01f), Mathf.Max(box.size.y, 0.01f), z1 - z0);
                AddBox(root, center, size);
            }
        }

        static bool IsUnder(Transform t, string name)
        {
            for (; t != null; t = t.parent)
            {
                if (t.name == name)
                    return true;
            }
            return false;
        }

        static void AddBox(GameObject go, Vector3 center, Vector3 size)
        {
            var box = go.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
        }

        // ---------------------------------------------------------------- 피격 화면 · 재질

        // 맞았을 때 시야 가장자리를 붉게 하는 비네트 재질 — XRI 멀미 비네트 셰이더(VR/TunnelingVignette)를 빌려 쓴다.
        // 그 셰이더는 두 눈을 따로 맞게 그리고 깊이 검사를 꺼서 벽에 붙어도 가려지지 않는다. 셰이더 파일(공용 샘플)은 고치지 않는다.
        // 조리개·번짐·색은 실행 중에 PlayerWoundEffects 가 바꾼다
        static void BuildVignetteMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(k_VignetteMaterialPath);
            bool created = mat == null;
            if (created)
                mat = new Material(Shader.Find("VR/TunnelingVignette"));
            mat.SetFloat("_ApertureSize", 1f);
            mat.SetFloat("_FeatheringEffect", 0.3f);
            mat.SetColor("_VignetteColor", new Color(0.75f, 0.02f, 0.02f, 0f));
            mat.SetColor("_VignetteColorBlend", new Color(0.75f, 0.02f, 0.02f, 0f));
            // 멀미 비네트(3005) 다음에 — 둘이 겹치면 붉은 쪽이 위
            mat.renderQueue = 3006;
            if (created)
                AssetDatabase.CreateAsset(mat, k_VignetteMaterialPath);
            else
                EditorUtility.SetDirty(mat);
        }
    }
}
