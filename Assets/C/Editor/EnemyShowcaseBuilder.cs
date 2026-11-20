using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace MilTrainSimVR.UrbanCombat.EditorTools
{
    // 적 동작 보기 씬(Assets/C/Scenes/C_EnemyShowcase.unity)을 만든다 (11/03) — 메뉴 MilTrainSimVR → C → 적 동작 보기 씬 만들기.
    // 적 병사(C_EnemyDummy) 8명을 한 줄로 세우고 한 명씩 한 동작을 되풀이시킨다(EnemyShowcase):
    // 대기 · 걷기 · 뛰기 · 조준(앞 · 위 · 아래) · 돌아가며 · 쓰러짐(머리 · 심장 번갈아). 겨누는 곳엔 빨간 공을 둔다.
    // Play 를 누르고 Scene 창에서 돌려 가며 본다 (헤드셋 없이). 프리팹을 그대로 놓고 길찾기 · 쏘기 컴포넌트만 빼서,
    // 프리팹을 다시 만들면 이 씬의 병사도 따라온다
    public static class EnemyShowcaseBuilder
    {
        const string k_ScenePath = "Assets/C/Scenes/C_EnemyShowcase.unity";
        const string k_PrefabPath = "Assets/C/Prefabs/C_EnemyDummy.prefab";
        const string k_FontPath = "Assets/C/Fonts/NotoSansKR-Regular SDF.asset";
        const float k_Spacing = 2.2f;

        // 이름 · 동작 · 겨누는 곳(병사 기준, 없으면 몸 앞 10m 가슴 높이). 앞을 겨누는 곳은 카메라 뒤(20m)라 Game 창엔 안 보인다
        static readonly (string label, EnemyShowcase.Act act, Vector3? aim)[] k_Stations =
        {
            ("대기", EnemyShowcase.Act.Idle, null),
            ("걷기", EnemyShowcase.Act.Walk, null),
            ("뛰기", EnemyShowcase.Act.Run, null),
            ("조준 앞", EnemyShowcase.Act.Aim, new Vector3(0f, 1.4f, 20f)),
            ("조준 위", EnemyShowcase.Act.Aim, new Vector3(0f, 5f, 6f)),
            ("조준 아래", EnemyShowcase.Act.Aim, new Vector3(0f, 0.1f, 3.5f)),
            ("돌아가며", EnemyShowcase.Act.Cycle, new Vector3(0f, 1.4f, 20f)),
            ("쓰러짐", EnemyShowcase.Act.Deaths, null),
        };

        [MenuItem("MilTrainSimVR/C/적 동작 보기 씬 만들기", false, 52)]
        static void Build()
        {
            // 열린 씬이 이 보기 씬뿐이면 어차피 새로 만들어 덮어쓰니 저장할지 묻지 않는다 — 프리팹을 다시 만들면 이 씬이 '고침' 상태가 되어
            // 매번 저장 창이 떴다 (11/03). 다른 씬에 저장 안 한 변경이 있으면 물어본다
            bool onlyShowcase = SceneManager.sceneCount == 1 && SceneManager.GetActiveScene().path == k_ScenePath;
            if (!onlyShowcase && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_PrefabPath);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(k_FontPath);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            float width = (k_Stations.Length - 1) * k_Spacing;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(width / 10f + 1f, 1f, 2f);

            // 카메라: 병사들 앞 10.5m 에서 줄 전체를 본다 (병사는 +Z 를 본다)
            Camera camera = Camera.main;
            camera.transform.SetPositionAndRotation(new Vector3(0f, 1.7f, 10.5f), Quaternion.LookRotation(new Vector3(0f, -0.08f, -1f)));
            camera.fieldOfView = 50f;

            var red = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "AimTarget" };
            red.SetColor("_BaseColor", new Color(0.9f, 0.1f, 0.1f));

            for (int i = 0; i < k_Stations.Length; i++)
            {
                (string label, EnemyShowcase.Act act, Vector3? aim) = k_Stations[i];
                var soldier = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                soldier.name = label;
                // 카메라에서 보아 왼쪽부터 (카메라가 -Z 를 보니 +X 가 왼쪽)
                soldier.transform.position = new Vector3(width * 0.5f - i * k_Spacing, 0f, 0f);
                // 길찾기 · 쏘기는 빼고 자세만 — EnemyMover 가 길찾기 · 쏘기를 요구하니 먼저 뺀다
                Object.DestroyImmediate(soldier.GetComponent<EnemyMover>());
                Object.DestroyImmediate(soldier.GetComponent<NavMeshAgent>());
                Object.DestroyImmediate(soldier.GetComponent<EnemyShooter>());

                Transform target = null;
                if (aim.HasValue)
                {
                    var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    ball.name = label + " — 겨누는 곳";
                    Object.DestroyImmediate(ball.GetComponent<Collider>());
                    ball.GetComponent<Renderer>().sharedMaterial = red;
                    ball.transform.position = soldier.transform.TransformPoint(aim.Value);
                    ball.transform.localScale = Vector3.one * 0.1f;
                    target = ball.transform;
                }
                soldier.AddComponent<EnemyShowcase>().Setup(act, target);

                // 이름표 — 머리 위, 카메라 쪽으로
                var text = new GameObject("이름표").AddComponent<TextMeshPro>();
                text.transform.SetParent(soldier.transform, false);
                text.transform.SetLocalPositionAndRotation(new Vector3(0f, 2.2f, 0f), Quaternion.Euler(0f, 180f, 0f));
                text.rectTransform.sizeDelta = new Vector2(2.1f, 0.7f);
                text.font = font;
                text.fontSize = 3.2f;
                text.alignment = TextAlignmentOptions.Center;
                text.textWrappingMode = TextWrappingModes.Normal;
                text.color = new Color(0.12f, 0.12f, 0.12f);
                text.text = label;
            }
            EditorSceneManager.SaveScene(scene, k_ScenePath);
            Debug.Log($"[C] 적 동작 보기 씬을 만들었다 — {k_ScenePath}. Play 를 누르고 Scene 창에서 본다");
        }
    }
}
