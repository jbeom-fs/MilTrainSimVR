using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

namespace MilTrainSimVR.UrbanCombat.EditorTools
{
    // 적 병사(C_EnemyDummy.prefab)를 만든다 (11/03) — 메뉴 MilTrainSimVR → C → 적 병사 만들기. 전투 자산 만들기도 이걸 부른다.
    // 회색 박스 더미 대신 Tripo 로 만든 병사 모델(Models/Enemy/C_Enemy.fbx, Mixamo 로 뼈를 넣음)에 Mixamo 애니메이션을 붙인다.
    // 프리팹 경로와 몸 맨 위 컴포넌트(몸 상태 · 쏘기 · 걷기)는 그대로라 씬에 놓인 적 10명의 이름 · 자리 · 경로가 그대로 남는다.
    //   · 가져오기 설정: 모델은 Humanoid(아바타를 여기서 만듦), 애니메이션은 그 아바타를 빌려 쓴다.
    //     걷기 · 뛰기는 앞으로 나아가는 애니메이션이라 앞뒤 · 좌우 이동만 루트 모션으로 빼서 버리고(제자리 걸음 — 실제 이동은 길찾기),
    //     나머지(대기 · 조준 · 쓰러짐)는 움직임을 모두 자세에 넣는다. 텍스처는 2048 로 줄여 쓴다 (원본 4K)
    //   · 애니메이터(C_Enemy.controller): Pose(0 대기 · 1 조준 · 2 걷기 · 3 뛰기) · MoveSpeed(걷기 · 뛰기 재생 배율) ·
    //     Dead · DeathClip(0 · 1 보통 쓰러짐, 2 · 3 헤드샷) — 값은 EnemySoldier 가 넣는다
    //   · 부위 판정: 뼈마다 상자 하나 — 그 뼈에 가장 크게 붙은 메시 꼭짓점들을 뼈 기준으로 감싼다 (손가락은 손, 발가락은 발에 합침)
    //   · 총: 가슴 뼈(UpperChest)에 붙인다. 조준 자세 = 조준 애니메이션에서 가늠자가 오른눈 앞, 낮춘 자세 = 대기 애니메이션의 두 손목 자리.
    //     두 손은 K2C1 권총손잡이 · 총열덮개에 맞춰 정한 자리 · 방향 · 손가락 굽힘으로 붙인다 (PlaceRifle 위 설명) — 실행 중에 IK
    public static class EnemySoldierBuilder
    {
        const string k_K2C1Path = "Assets/A/Prefabs/K2C1.prefab";
        const string k_TempRiflePath = "Assets/C/Prefabs/C_TempRifle.prefab";
        const string k_PrefabPath = "Assets/C/Prefabs/C_EnemyDummy.prefab";
        const string k_Folder = "Assets/C/Models/Enemy";
        const string k_ModelPath = k_Folder + "/C_Enemy.fbx";
        const string k_ControllerPath = k_Folder + "/C_Enemy.controller";
        const string k_BaseColorPath = k_Folder + "/Textures/C_Enemy_BaseColor.jpg";
        const string k_NormalPath = k_Folder + "/Textures/C_Enemy_Normal.png";
        const string k_MaterialPath = "Assets/C/Materials/Enemy/C_EnemySoldier.mat";
        const string k_HoldPath = k_Folder + "/C_EnemyHold.asset";

        // 애니메이션 파일 · 상태 이름 · 반복 · 앞으로 나아가는지 · 잘라 쓰는 구간(초, from < 0 이면 전부) · 몸 방향 돌림(°).
        // 순서가 애니메이터 값이다 (앞 열 = Pose — EnemySoldier.PoseIdle ~ PoseCrouchReload, 뒤 넷 = DeathClip).
        // 11/16 추가 (사용자가 Mixamo 에서 받음, 60fps · 스킨 없음): 엄폐 대기 · 무릎 쏴 · 옆걸음 둘 · 재장전 둘.
        //   · 옆걸음은 Mixamo 가 가는 쪽을 루트 앞으로 두고 몸(총)을 왼쪽으로 100° 남짓 돌려 놓아서, 몸 방향을 돌려 총이 루트 앞을 보게 한다
        //     (조준 대기의 총 방향과 같게 — 첫 자세의 왼손 − 오른손 방향: 조준 −14°, 왼쪽 옆걸음 −115°, 오른쪽 −121°.
        //     Root Transform Rotation Offset 은 몸을 그 반대로 돌린다 → −101° · −107°. 그러면 왼쪽은 몸 기준 −79°, 오른쪽은 +107° 로 간다)
        //   · 재장전은 앞뒤 대기를 잘라 낸다 — 서서 0.5~2.55초(탄창 빼기 · 끼우기 · 장전 손잡이), 앉아서 1.15~3.45초(탄창 빼기 · 끼우기 — 뒤의 만지작거림은 뺀다)
        static readonly (string file, string name, bool loop, bool travels, float from, float to, float turn)[] k_Clips =
        {
            ("C_Enemy@RifleIdle.fbx", "RifleIdle", true, false, -1f, -1f, 0f),
            ("C_Enemy@IdleAiming.fbx", "IdleAiming", true, false, -1f, -1f, 0f),
            ("C_Enemy@WalkWithRifle.fbx", "WalkWithRifle", true, true, -1f, -1f, 0f),
            ("C_Enemy@RifleRun.fbx", "RifleRun", true, true, -1f, -1f, 0f),
            ("C_Enemy@CoverIdle.fbx", "CoverIdle", true, false, -1f, -1f, 0f),
            ("C_Enemy@CrouchIdle.fbx", "CrouchIdle", true, false, -1f, -1f, 0f),
            ("C_Enemy@StrafeLeft.fbx", "StrafeLeft", true, true, -1f, -1f, k_StrafeLeftTurn),
            ("C_Enemy@StrafeRight.fbx", "StrafeRight", true, true, -1f, -1f, k_StrafeRightTurn),
            ("C_Enemy@Reload.fbx", "Reload", false, false, 0.5f, 2.55f, 0f),
            ("C_Enemy@CrouchReload.fbx", "CrouchReload", false, false, 1.15f, 3.45f, 0f),
            ("C_Enemy@DeathFront.fbx", "DeathFront", false, false, -1f, -1f, 0f),
            ("C_Enemy@DeathBack.fbx", "DeathBack", false, false, -1f, -1f, 0f),
            ("C_Enemy@DeathFrontHeadshot.fbx", "DeathFrontHeadshot", false, false, -1f, -1f, 0f),
            ("C_Enemy@DeathBackHeadshot.fbx", "DeathBackHeadshot", false, false, -1f, -1f, 0f),
        };
        const int k_PoseCount = EnemySoldier.PoseCount;
        const float k_StrafeLeftTurn = -101f, k_StrafeRightTurn = -107f;

        // 부위 판정 상자를 붙일 뼈와 부위
        static readonly (HumanBodyBones bone, HitZone.Part part)[] k_Zones =
        {
            (HumanBodyBones.Head, HitZone.Part.Head),
            (HumanBodyBones.Neck, HitZone.Part.Head),
            (HumanBodyBones.UpperChest, HitZone.Part.Chest),
            (HumanBodyBones.Chest, HitZone.Part.Chest),
            (HumanBodyBones.LeftShoulder, HitZone.Part.Chest),
            (HumanBodyBones.RightShoulder, HitZone.Part.Chest),
            (HumanBodyBones.Spine, HitZone.Part.Abdomen),
            (HumanBodyBones.Hips, HitZone.Part.Abdomen),
            (HumanBodyBones.LeftUpperArm, HitZone.Part.Arm),
            (HumanBodyBones.LeftLowerArm, HitZone.Part.Arm),
            (HumanBodyBones.LeftHand, HitZone.Part.Arm),
            (HumanBodyBones.RightUpperArm, HitZone.Part.Arm),
            (HumanBodyBones.RightLowerArm, HitZone.Part.Arm),
            (HumanBodyBones.RightHand, HitZone.Part.Arm),
            (HumanBodyBones.LeftUpperLeg, HitZone.Part.Leg),
            (HumanBodyBones.LeftLowerLeg, HitZone.Part.Leg),
            (HumanBodyBones.LeftFoot, HitZone.Part.Leg),
            (HumanBodyBones.RightUpperLeg, HitZone.Part.Leg),
            (HumanBodyBones.RightLowerLeg, HitZone.Part.Leg),
            (HumanBodyBones.RightFoot, HitZone.Part.Leg),
        };

        // 심장 — 가슴 상자(UpperChest) 앞면에서 이만큼 안쪽, 몸 가운데에서 왼쪽(-X)으로 (m, T 자세 기준)
        const float k_HeartDepth = 0.10f;
        const float k_HeartLeft = 0.035f;
        const float k_HeartRadius = 0.06f;

        // 눈 — 머리 뼈에서 위 · 앞으로 (m)
        static readonly Vector3 k_EyesFromHead = new Vector3(0f, 0.09f, 0.09f);

        [MenuItem("MilTrainSimVR/C/적 병사 만들기 (모델 · 애니메이션 · 프리팹)", false, 51)]
        static void BuildFromMenu()
        {
            var k2c1 = AssetDatabase.LoadAssetAtPath<GameObject>(k_K2C1Path);
            var temp = AssetDatabase.LoadAssetAtPath<GameObject>(k_TempRiflePath);
            if (k2c1 == null || temp == null)
            {
                Debug.LogWarning($"[C] {k_K2C1Path} 또는 {k_TempRiflePath} 가 없다");
                return;
            }
            Build(k2c1, temp);
            AssetDatabase.SaveAssets();
        }

        public static void Build(GameObject k2c1, GameObject temp)
        {
            Avatar avatar = ImportModel();
            if (avatar == null || !avatar.isHuman)
            {
                Debug.LogWarning($"[C] {k_ModelPath} 의 Humanoid 아바타를 만들지 못했다");
                return;
            }
            AnimationClip[] clips = ImportClips(avatar);
            AnimatorController controller = BuildController(clips);
            Material material = BuildMaterial();
            string reach = BuildPrefab(k2c1, temp, avatar, controller, clips, material);
            Debug.Log($"[C] 적 병사를 만들었다 — {k_PrefabPath} (걷기 {clips[2].averageSpeed.magnitude:0.00} · 뛰기 {clips[3].averageSpeed.magnitude:0.00} m/초 · 손목~어깨 {reach}, 팔 0.48)");
        }

        // ---------------------------------------------------------------- 가져오기 설정

        static Avatar ImportModel()
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(k_ModelPath);
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importBlendShapes = false;
            // 재질은 프리팹에서 C_EnemySoldier 로 — FBX 안에 든 미리보기 텍스처는 가져오지 않는다
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(k_ModelPath).OfType<Avatar>().FirstOrDefault();
        }

        static AnimationClip[] ImportClips(Avatar avatar) =>
            k_Clips.Select(c => ImportClip(c.file, c.name, c.loop, c.travels, c.from, c.to, c.turn, avatar)).ToArray();

        static AnimationClip ImportClip(string file, string clipName, bool loop, bool travels, float from, float to, float turn, Avatar avatar)
        {
            string path = $"{k_Folder}/Animations/{file}";
            var mi = (ModelImporter)AssetImporter.GetAtPath(path);
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            mi.sourceAvatar = avatar;
            mi.importAnimation = true;
            mi.importCameras = false;
            mi.importLights = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            ModelImporterClipAnimation clip = mi.clipAnimations.Length > 0 ? mi.clipAnimations[0] : mi.defaultClipAnimations[0];
            clip.name = clipName;
            clip.loopTime = loop;
            clip.loopPose = loop;
            if (from >= 0f)
            {
                float rate = mi.importedTakeInfos.Length > 0 ? mi.importedTakeInfos[0].sampleRate : 30f;
                clip.firstFrame = from * rate;
                clip.lastFrame = to * rate;
            }
            clip.rotationOffset = turn;
            // 돌기 · 높이는 늘 자세에 넣는다 (쓰러질 때 몸이 바닥으로 내려가야 한다)
            clip.lockRootRotation = true;
            clip.keepOriginalOrientation = true;
            clip.lockRootHeightY = true;
            clip.keepOriginalPositionY = true;
            clip.heightFromFeet = false;
            // 앞으로 나아가는 걷기 · 뛰기만 앞뒤 · 좌우 이동을 루트 모션으로 뺀다 → Apply Root Motion 을 꺼서 제자리 걸음
            clip.lockRootPositionXZ = !travels;
            clip.keepOriginalPositionXZ = true;
            mi.clipAnimations = new[] { clip };
            mi.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => c.name == clipName);
        }

        static Material BuildMaterial()
        {
            ImportTexture(k_BaseColorPath, false);
            ImportTexture(k_NormalPath, true);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(k_MaterialPath);
            bool created = mat == null;
            if (created)
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(k_BaseColorPath));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(k_NormalPath));
            mat.SetFloat("_BumpScale", 1f);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 0.15f);
            BaseShaderGUI.SetMaterialKeywords(mat);
            if (created)
                AssetDatabase.CreateAsset(mat, k_MaterialPath);
            else
                EditorUtility.SetDirty(mat);
            return mat;
        }

        static void ImportTexture(string path, bool normal)
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = !normal;
            ti.maxTextureSize = 2048;
            ti.mipmapEnabled = true;
            ti.textureCompression = TextureImporterCompression.Compressed;
            ti.SaveAndReimport();
        }

        // ---------------------------------------------------------------- 애니메이터

        static AnimatorController BuildController(AnimationClip[] clips)
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(k_ControllerPath);
            if (ctrl == null)
                ctrl = AnimatorController.CreateAnimatorControllerAtPath(k_ControllerPath);
            foreach (AnimatorControllerParameter p in ctrl.parameters)
                ctrl.RemoveParameter(p);
            AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
            foreach (AnimatorStateTransition t in sm.anyStateTransitions)
                sm.RemoveAnyStateTransition(t);
            foreach (ChildAnimatorState s in sm.states)
                sm.RemoveState(s.state);

            ctrl.AddParameter("Pose", AnimatorControllerParameterType.Int);
            ctrl.AddParameter(new AnimatorControllerParameter { name = "MoveSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            ctrl.AddParameter(new AnimatorControllerParameter { name = "ActionSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            ctrl.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("DeathClip", AnimatorControllerParameterType.Int);

            // 살아 있을 때: 어느 자세에서든 Pose 값이 바뀌면 그 자세로 0.2초에 걸쳐 넘어간다 (앉기 · 웅크리기는 몸이 크게 움직여 0.3초).
            // 걷기 · 뛰기 · 옆걸음은 실제 빠르기(MoveSpeed), 재장전은 갈아 끼우는 시간(ActionSpeed)에 맞춰 튼다
            for (int i = 0; i < k_PoseCount; i++)
            {
                AnimatorState state = sm.AddState(clips[i].name, new Vector3(300f, 60f * i, 0f));
                state.motion = clips[i];
                if (k_Clips[i].travels)
                {
                    state.speedParameterActive = true;
                    state.speedParameter = "MoveSpeed";
                }
                else if (i == EnemySoldier.PoseReload || i == EnemySoldier.PoseCrouchReload)
                {
                    state.speedParameterActive = true;
                    state.speedParameter = "ActionSpeed";
                }
                bool big = i == EnemySoldier.PoseCover || i == EnemySoldier.PoseCrouch || i == EnemySoldier.PoseCrouchReload;
                AnimatorStateTransition t = AnyState(sm, state, big ? 0.3f : 0.2f);
                t.AddCondition(AnimatorConditionMode.Equals, i, "Pose");
                t.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
                if (i == EnemySoldier.PoseIdle)
                    sm.defaultState = state;
            }
            // 전사: 고른 쓰러짐으로 넘어가 마지막 자세에 멈춘다
            for (int i = k_PoseCount; i < clips.Length; i++)
            {
                AnimatorState state = sm.AddState(clips[i].name, new Vector3(600f, 60f * (i - k_PoseCount), 0f));
                state.motion = clips[i];
                AnimatorStateTransition t = AnyState(sm, state, 0.1f);
                t.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
                t.AddCondition(AnimatorConditionMode.Equals, i - k_PoseCount, "DeathClip");
            }
            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        static AnimatorStateTransition AnyState(AnimatorStateMachine sm, AnimatorState to, float seconds)
        {
            AnimatorStateTransition t = sm.AddAnyStateTransition(to);
            t.hasExitTime = false;
            t.hasFixedDuration = true;
            t.duration = seconds;
            t.canTransitionToSelf = false;
            return t;
        }

        // ---------------------------------------------------------------- 프리팹

        // 앞 = +Z, 원점 = 두 발 사이 바닥 (모델이 그렇게 나와 있다)
        static string BuildPrefab(GameObject k2c1, GameObject temp, Avatar avatar, AnimatorController controller, AnimationClip[] clips, Material material)
        {
            var root = new GameObject("C_EnemyDummy");
            var health = new SerializedObject(root.AddComponent<CombatantHealth>());
            health.FindProperty("m_DisplayName").stringValue = "적";
            health.ApplyModifiedPropertiesWithoutUndo();
            var soldier = root.AddComponent<EnemySoldier>();

            var body = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(k_ModelPath), root.transform);
            body.name = "Body";
            var animator = body.GetComponent<Animator>();
            if (animator == null)
                animator = body.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            // 화면 밖에서도 뼈를 움직인다 — 부위 판정 상자 · 총구가 뼈를 따라가야 한다
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var skin = body.GetComponentInChildren<SkinnedMeshRenderer>();
            skin.sharedMaterial = material;

            AddHitZones(animator, skin);
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            var eyes = new GameObject("Eyes").transform;
            eyes.SetParent(head, false);
            eyes.position = head.position + k_EyesFromHead;

            // 총: 가슴 뼈에 붙인다
            var rifle = (GameObject)PrefabUtility.InstantiatePrefab(k2c1);
            rifle.name = "Rifle";
            var soldierSettings = new SerializedObject(soldier);
            MeasureFalls(animator, clips, soldierSettings);
            EnemyHoldSettings hold = LoadOrCreateHold();
            string reach = PlaceRifle(animator, clips[EnemySoldier.PoseAim], clips[EnemySoldier.PoseIdle], rifle.transform, hold, soldierSettings);
            Transform muzzle = Find(rifle.transform, "Anchor_Muzzle");

            // 총구 불빛 · 탄도선 — 임시 소총 것을 복사. 사격음은 플레이어 리그의 CombatAudio 가 낸다 (11/16 — 그 전엔 적마다 AudioSource 를 붙였다)
            GameObject flash = Object.Instantiate(temp.transform.Find("Muzzle/MuzzleFlash").gameObject, muzzle, false);
            flash.name = "MuzzleFlash";
            GameObject tracer = Object.Instantiate(temp.transform.Find("Tracer").gameObject, root.transform, false);
            tracer.name = "Tracer";

            var shooter = root.AddComponent<EnemyShooter>();
            var so = new SerializedObject(shooter);
            so.FindProperty("m_Muzzle").objectReferenceValue = muzzle;
            so.FindProperty("m_MuzzleFlash").objectReferenceValue = flash.GetComponent<MuzzleFlash>();
            so.FindProperty("m_Tracer").objectReferenceValue = tracer.GetComponent<LineRenderer>();
            so.FindProperty("m_Eyes").objectReferenceValue = eyes;
            so.ApplyModifiedPropertiesWithoutUndo();

            so = soldierSettings;
            so.FindProperty("m_Animator").objectReferenceValue = animator;
            so.FindProperty("m_Hold").objectReferenceValue = hold;
            so.FindProperty("m_Rifle").objectReferenceValue = rifle.transform;
            so.FindProperty("m_Muzzle").objectReferenceValue = muzzle;
            ApplyClipMeasures(so, clips);
            so.FindProperty("m_Magazine").objectReferenceValue = Find(rifle.transform, "Part_Magazine");
            so.ApplyModifiedPropertiesWithoutUndo();

            // 걷기 — 몸 크기는 길찾기 지도를 구운 값(반지름 0.3m)과 같게. 몸 돌리기는 EnemyMover · EnemyShooter 가 한다
            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.3f;
            agent.height = 1.75f;
            agent.baseOffset = 0f;
            agent.speed = 1.4f;
            agent.angularSpeed = 360f;
            agent.acceleration = 10f;
            agent.stoppingDistance = 0.3f;
            agent.autoBraking = true;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            root.AddComponent<EnemyMover>();

            PrefabUtility.SaveAsPrefabAsset(root, k_PrefabPath);
            Object.DestroyImmediate(root);
            return reach;
        }

        // 뼈마다 상자 하나 — 그 뼈(또는 그 밑의 손가락 · 발가락 · 머리끝)에 가장 크게 붙은 꼭짓점을 T 자세의 뼈 기준으로 감싼다
        static void AddHitZones(Animator animator, SkinnedMeshRenderer skin)
        {
            var parts = new Dictionary<Transform, HitZone.Part>();
            foreach ((HumanBodyBones bone, HitZone.Part part) in k_Zones)
            {
                Transform t = animator.GetBoneTransform(bone);
                if (t != null)
                    parts[t] = part;
            }
            Mesh mesh = skin.sharedMesh;
            Transform[] bones = skin.bones;
            Matrix4x4[] bind = mesh.bindposes;
            var index = new Dictionary<Transform, int>();
            for (int i = 0; i < bones.Length; i++)
                index[bones[i]] = i;

            var boxes = new Dictionary<Transform, Bounds>();
            Vector3[] vertices = mesh.vertices;
            BoneWeight[] weights = mesh.boneWeights;
            for (int v = 0; v < vertices.Length; v++)
            {
                Transform b = bones[weights[v].boneIndex0];
                while (b != null && !parts.ContainsKey(b))
                    b = b.parent;
                if (b == null || !index.TryGetValue(b, out int bi))
                    continue;
                Vector3 p = bind[bi].MultiplyPoint3x4(vertices[v]);
                if (boxes.TryGetValue(b, out Bounds box))
                    box.Encapsulate(p);
                else
                    box = new Bounds(p, Vector3.zero);
                boxes[b] = box;
            }

            Transform upperChest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            foreach (KeyValuePair<Transform, Bounds> pair in boxes)
            {
                var go = new GameObject("Hit_" + pair.Key.name.Replace("mixamorig:", ""));
                go.transform.SetParent(pair.Key, false);
                var collider = go.AddComponent<BoxCollider>();
                collider.center = pair.Value.center;
                collider.size = pair.Value.size;
                var zone = go.AddComponent<HitZone>();
                if (pair.Key != upperChest)
                {
                    zone.Setup(parts[pair.Key]);
                    continue;
                }
                // 심장 — 가슴 상자 앞면에서 안쪽, 가운데에서 왼쪽. 높이는 가슴 뼈 높이(어깨 관절보다 8cm 쯤 아래 — 상자 가운데는 쇄골 쪽이라 너무 높다)
                Bounds world = WorldBounds(go.transform, pair.Value);
                var heart = new GameObject("Heart").transform;
                heart.SetParent(pair.Key, false);
                heart.position = new Vector3(world.center.x - k_HeartLeft, pair.Key.position.y, world.max.z - k_HeartDepth);
                zone.Setup(HitZone.Part.Chest, heart, k_HeartRadius);
            }
        }

        static Bounds WorldBounds(Transform t, Bounds local)
        {
            var world = new Bounds(t.TransformPoint(local.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                world.Encapsulate(t.TransformPoint(local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            return world;
        }

        // 총 드는 모양 설정 — 없으면 기본값으로 만든다 (값과 그 까닭은 EnemyHoldSettings)
        static EnemyHoldSettings LoadOrCreateHold()
        {
            var hold = AssetDatabase.LoadAssetAtPath<EnemyHoldSettings>(k_HoldPath);
            if (hold != null)
                return hold;
            hold = ScriptableObject.CreateInstance<EnemyHoldSettings>();
            AssetDatabase.CreateAsset(hold, k_HoldPath);
            return hold;
        }

        // 총 드는 모양에 쓰는 뼈대 값을 재서 EnemySoldier 에 넣는다 (계산은 EnemyHoldSettings — Play 중에 값을 바꾸면 바로 보이게):
        //   · T 자세: 손 · 손가락 뼈의 부모 기준 회전, 오른눈 자리(머리 뼈 기준)
        //   · 조준 애니메이션 첫 자세: 오른눈 자리 · 몸 방향 (가슴 뼈 기준)
        //   · 대기 애니메이션 첫 자세: 가슴 뼈 (몸 기준)
        // 몸 원점은 바닥 가운데, 앞 = +Z 그대로라 몸 기준 = 월드. 총은 낮춘 자세 자리에 둔다.
        // 두 자세에서 손목이 어깨에서 얼마나 먼지 돌려준다 (팔 길이와 견주게)
        static string PlaceRifle(Animator animator, AnimationClip aim, AnimationClip idle, Transform rifle, EnemyHoldSettings hold, SerializedObject soldier)
        {
            Transform Bone(HumanBodyBones b) => animator.GetBoneTransform(b);
            Transform chest = Bone(HumanBodyBones.UpperChest);
            Transform head = Bone(HumanBodyBones.Head);

            // T 자세
            SerializedProperty handRest = soldier.FindProperty("m_HandRest");
            handRest.arraySize = 2;
            handRest.GetArrayElementAtIndex(0).quaternionValue = Bone(HumanBodyBones.RightHand).localRotation;
            handRest.GetArrayElementAtIndex(1).quaternionValue = Bone(HumanBodyBones.LeftHand).localRotation;
            SerializedProperty fingerRest = soldier.FindProperty("m_FingerRest");
            fingerRest.arraySize = EnemySoldier.Fingers.Length;
            for (int i = 0; i < EnemySoldier.Fingers.Length; i++)
            {
                Transform t = Bone(EnemySoldier.Fingers[i]);
                fingerRest.GetArrayElementAtIndex(i).quaternionValue = t != null ? t.localRotation : Quaternion.identity;
            }
            Vector3 eyeInHead = head.InverseTransformPoint(head.position + hold.rightEyeFromHead * 0.01f);
            hold.HandPoses(Bone(HumanBodyBones.RightMiddleProximal).localPosition, Bone(HumanBodyBones.LeftMiddleProximal).localPosition,
                out Vector3 rightWrist, out _, out Vector3 leftWrist, out _);
            string Reach(string label) =>
                $"{label} 오른손목 {Vector3.Distance(rifle.TransformPoint(rightWrist), Bone(HumanBodyBones.RightUpperArm).position):0.00} · " +
                $"왼손목 {Vector3.Distance(rifle.TransformPoint(leftWrist), Bone(HumanBodyBones.LeftUpperArm).position):0.00} m";

            string reach = "";
            Vector3 lowPosition = Vector3.zero;
            Quaternion lowRotation = Quaternion.identity;
            rifle.SetParent(chest, false);
            AnimationMode.StartAnimationMode();
            try
            {
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(animator.gameObject, aim, 0f);
                AnimationMode.EndSampling();
                Vector3 eye = chest.InverseTransformPoint(head.TransformPoint(eyeInHead));
                Quaternion basis = Quaternion.Inverse(chest.rotation);
                soldier.FindProperty("m_AimEye").vector3Value = eye;
                soldier.FindProperty("m_AimBasis").quaternionValue = basis;
                MeasureAimHand(animator, soldier);
                hold.AimRifle(eye, basis, out Vector3 aimPosition, out Quaternion aimRotation);
                rifle.SetLocalPositionAndRotation(aimPosition, aimRotation);
                reach = Reach("조준");

                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(animator.gameObject, idle, 0f);
                AnimationMode.EndSampling();
                soldier.FindProperty("m_IdleChestPosition").vector3Value = chest.position;
                soldier.FindProperty("m_IdleChestRotation").quaternionValue = chest.rotation;
                hold.LowRifle(chest.position, chest.rotation, out lowPosition, out lowRotation);
                rifle.localPosition = lowPosition;
                rifle.rotation = lowRotation;
                reach += " / " + Reach("낮춤");
            }
            finally
            {
                AnimationMode.StopAnimationMode();
            }
            rifle.localPosition = lowPosition;
            rifle.rotation = lowRotation;
            return reach;
        }

        // 애니메이션 길이 · 빠르기 — 걷기 · 뛰기 · 옆걸음은 원래 나아가는 빠르기(발이 미끄러지지 않게 재생 빠르기를 맞춘다), 재장전은 잘라 쓴 길이
        static void ApplyClipMeasures(SerializedObject soldier, AnimationClip[] clips)
        {
            soldier.FindProperty("m_WalkClipSpeed").floatValue = clips[EnemySoldier.PoseWalk].averageSpeed.magnitude;
            soldier.FindProperty("m_RunClipSpeed").floatValue = clips[EnemySoldier.PoseRun].averageSpeed.magnitude;
            soldier.FindProperty("m_StrafeClipSpeed").vector2Value = new Vector2(
                clips[EnemySoldier.PoseStrafeLeft].averageSpeed.magnitude, clips[EnemySoldier.PoseStrafeRight].averageSpeed.magnitude);
            soldier.FindProperty("m_ReloadClipSeconds").vector2Value = new Vector2(
                clips[EnemySoldier.PoseReload].length, clips[EnemySoldier.PoseCrouchReload].length);
        }

        // 조준 애니메이션을 샘플한 상태에서: 오른손 뼈의 자리 · 방향 (가슴 뼈 기준) — 재장전 중 총이 애니메이션의 오른손을 따라가는 기준 (11/16)
        static void MeasureAimHand(Animator animator, SerializedObject soldier)
        {
            Transform chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            soldier.FindProperty("m_AimRightHandPosition").vector3Value = chest.InverseTransformPoint(hand.position);
            soldier.FindProperty("m_AimRightHandRotation").quaternionValue = Quaternion.Inverse(chest.rotation) * hand.rotation;
        }

        // 애니메이션만 다시 넣는다 (11/16 엄폐 · 재장전 애니메이션) — 클립 가져오기 설정 · 애니메이터를 다시 만들고, 프리팹은 열어서
        // EnemySoldier 의 잰 값(빠르기 · 길이 · 조준 손 · 쓰러짐 · 탄창)만 바꾼다. 몸 · 총 · 판정 상자 · 다른 값은 그대로
        [MenuItem("MilTrainSimVR/C/적 애니메이션만 다시 넣기 (프리팹은 그대로)", false, 52)]
        public static void UpdateAnimations()
        {
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(k_ModelPath).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isHuman)
            {
                Debug.LogWarning($"[C] {k_ModelPath} 의 Humanoid 아바타가 없다 — '적 병사 만들기'를 먼저");
                return;
            }
            AnimationClip[] clips = ImportClips(avatar);
            BuildController(clips);
            GameObject root = PrefabUtility.LoadPrefabContents(k_PrefabPath);
            try
            {
                var so = new SerializedObject(root.GetComponent<EnemySoldier>());
                var animator = (Animator)so.FindProperty("m_Animator").objectReferenceValue;
                var rifle = (Transform)so.FindProperty("m_Rifle").objectReferenceValue;
                ApplyClipMeasures(so, clips);
                MeasureFalls(animator, clips, so);
                AnimationMode.StartAnimationMode();
                try
                {
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(animator.gameObject, clips[EnemySoldier.PoseAim], 0f);
                    AnimationMode.EndSampling();
                    MeasureAimHand(animator, so);
                }
                finally
                {
                    AnimationMode.StopAnimationMode();
                }
                so.FindProperty("m_Magazine").objectReferenceValue = Find(rifle, "Part_Magazine");
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, k_PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[C] 적 애니메이션을 다시 넣었다 — 옆걸음 {clips[EnemySoldier.PoseStrafeLeft].averageSpeed.magnitude:0.00} · " +
                      $"{clips[EnemySoldier.PoseStrafeRight].averageSpeed.magnitude:0.00} m/초, 재장전 {clips[EnemySoldier.PoseReload].length:0.0} · " +
                      $"{clips[EnemySoldier.PoseCrouchReload].length:0.0} 초");
        }

        // 쓰러지는 동작마다 끝 자세에서 머리가 어디 있는지 (x 오른쪽 · z 앞, m — 몸 원점이 월드 원점이라 월드 좌표 그대로) (11/12).
        // EnemySoldier 가 죽는 순간 그쪽 자리가 비었는지 보고 동작 · 방향을 고른다 (벽 · 차 · 엄폐물 속으로 쓰러지지 않게)
        static void MeasureFalls(Animator animator, AnimationClip[] clips, SerializedObject soldier)
        {
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            SerializedProperty reach = soldier.FindProperty("m_DeathReach");
            reach.arraySize = clips.Length - k_PoseCount;
            AnimationMode.StartAnimationMode();
            try
            {
                for (int i = k_PoseCount; i < clips.Length; i++)
                {
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(animator.gameObject, clips[i], clips[i].length);
                    AnimationMode.EndSampling();
                    reach.GetArrayElementAtIndex(i - k_PoseCount).vector2Value = new Vector2(head.position.x, head.position.z);
                }
            }
            finally
            {
                AnimationMode.StopAnimationMode();
            }
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
    }
}
