using MilTrainSimVR.A.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace MilTrainSimVR.A.EditorTools
{
    // 허브 안내판 (11/17 사용자) — 게시판 하나에 판 둘. 버튼 두 개 중 한쪽을 누르면 그쪽이 켜지고 다른 쪽은 꺼진다.
    //   · 위 판: 조준 방식 — 확대 조준(A) / 가늠자 조준(C) (RifleStylePanel → RifleStyle). 11/17 오후 A · C 결정: 총은 C 총 하나, 고르는 건 조준만
    //   · 아래 판: 화면 회전 부드럽게 / 끊어 돌기 (TurnStylePanel → TurnStyle, 모든 과정에 적용)
    // 시작 자리(정문 안)에서 포탈 쪽으로 걸어가는 길 왼쪽에 선 게시판 — 기둥 둘 + 판 + 판 앞 월드 캔버스(레이로 누른다).
    // Hub 밖 씬 루트에 둔다 — 허브 전체를 다시 지어도(HubBuilder 는 Hub 아래만 지운다) 남는다. 다시 누르면 이 게시판만 다시 짓는다.
    // 자리 · 크기는 제안값 — 렌더 보며 조정.
    static class HubRifleStyleBuilder
    {
        const string k_HubScene = "Assets/A/Scenes/A_Hub.unity";
        const string k_Name = "RifleStylePanel";
        const string k_MaterialFolder = "Assets/A/Materials/Hub";

        static readonly Vector3 k_Position = new Vector3(-4.5f, 0f, -12f);
        static readonly Vector3 k_LookAt = new Vector3(0f, 0f, -19f);   // 시작 자리 — 판 앞이 이쪽을 본다

        const float k_BoardWidth = 1.9f, k_BoardDepth = 0.05f;
        const float k_RifleBottom = 1.25f, k_RifleHeight = 1.2f;   // 위 판 — 사격 방식 (11/17 아래 판을 넣으며 0.85 → 1.25 m 로 올림)
        const float k_TurnBottom = 0.35f, k_TurnHeight = 0.8f;     // 아래 판 — 화면 회전
        const float k_CanvasScale = 0.001f;                        // 캔버스 단위 → m

        static readonly Color k_Ink = new Color(0.92f, 0.93f, 0.90f);
        static readonly Color k_Dim = new Color(0.68f, 0.70f, 0.72f);

        [MenuItem("A/만들기/허브 사격 방식 안내판")]
        static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != k_HubScene)
            {
                Debug.LogWarning($"[A] 허브 씬({k_HubScene})을 열고 누르세요 — 지금 {scene.path}");
                return;
            }
            var old = GameObject.Find(k_Name);
            if (old != null)
                Object.DestroyImmediate(old);

            var root = new GameObject(k_Name).transform;
            Vector3 toStart = k_LookAt - k_Position;
            toStart.y = 0f;
            root.SetPositionAndRotation(k_Position, Quaternion.LookRotation(toStart.normalized, Vector3.up));

            // 기둥 둘 + 판 둘 (판 앞 = +Z)
            var post = Lit("Hub_SignPost", new Color(0.18f, 0.19f, 0.20f), 0.35f, 0.6f);
            var board = Lit("Hub_SignBoard", new Color(0.13f, 0.17f, 0.15f), 0.2f, 0f);
            float postHeight = k_RifleBottom + k_RifleHeight + 0.05f;
            for (int side = -1; side <= 1; side += 2)
                Box("Post", root, new Vector3(side * (k_BoardWidth * 0.5f - 0.05f), postHeight * 0.5f, -0.06f), new Vector3(0.07f, postHeight, 0.07f), post);
            Box("Board", root, new Vector3(0f, k_RifleBottom + k_RifleHeight * 0.5f, 0f), new Vector3(k_BoardWidth, k_RifleHeight, k_BoardDepth), board);
            Box("TurnBoard", root, new Vector3(0f, k_TurnBottom + k_TurnHeight * 0.5f, 0f), new Vector3(k_BoardWidth, k_TurnHeight, k_BoardDepth), board);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildRifle(root, font);
            BuildTurn(root, font);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = root.gameObject;
            Debug.Log($"[A] 허브 안내판(사격 방식 · 화면 회전): {root.position}, 판 폭 {k_BoardWidth} m");
        }

        static void BuildRifle(Transform root, Font font)
        {
            var canvas = BoardCanvas("Canvas", root, k_RifleBottom, k_RifleHeight, typeof(RifleStylePanel));
            Label(canvas, "Title", font, 78, FontStyle.Bold, k_Ink, new Vector2(0f, 0.86f), new Vector2(1f, 0.98f), "조준 방식 고르기");
            Label(canvas, "Sub", font, 38, FontStyle.Normal, k_Dim, new Vector2(0f, 0.79f), new Vector2(1f, 0.86f),
                "사로 사격 · 시가지 전투에서 겨누는 방법을 고릅니다 — 총 다루는 법은 같습니다");

            var a = StyleButton(canvas, "ButtonA", font, new Vector2(0.03f, 0.17f), new Vector2(0.49f, 0.76f),
                "확대 조준", "가늠자에 눈을 대면 커진다",
                "· 눈을 가늠자 뒤에 대면 조준 화면이 4배로 커짐\n· 먼 표적 · 작은 표적을 겨누기 쉬움\n· 눈을 떼면 다시 보통 시야", out var checkA);
            var c = StyleButton(canvas, "ButtonC", font, new Vector2(0.51f, 0.17f), new Vector2(0.97f, 0.76f),
                "가늠자 조준", "실제 총처럼 겨눈다",
                "· 가늠자와 가늠쇠를 맞춰 그대로 겨눔\n· 확대 없이 실제와 같은 시야\n· 주변을 넓게 보며 쏘기 좋음", out var checkC);

            var status = Label(canvas, "Status", font, 44, FontStyle.Bold, k_Ink, new Vector2(0f, 0.08f), new Vector2(1f, 0.16f), "");
            Label(canvas, "Note", font, 30, FontStyle.Normal, k_Dim, new Vector2(0f, 0.015f), new Vector2(1f, 0.08f),
                "고른 방식은 다음에 켜도 그대로 남습니다 · 두 손 쥐기 · 방아쇠 · 탄창 · 숨 참기는 어느 쪽이든 같습니다");

            var panel = canvas.GetComponent<RifleStylePanel>();
            var so = new SerializedObject(panel);
            so.FindProperty("m_ButtonA").objectReferenceValue = a;
            so.FindProperty("m_ButtonC").objectReferenceValue = c;
            so.FindProperty("m_CheckA").objectReferenceValue = checkA;
            so.FindProperty("m_CheckC").objectReferenceValue = checkC;
            so.FindProperty("m_Status").objectReferenceValue = status;
            so.ApplyModifiedPropertiesWithoutUndo();
            panel.ShowDefault();
        }

        static void BuildTurn(Transform root, Font font)
        {
            var canvas = BoardCanvas("TurnCanvas", root, k_TurnBottom, k_TurnHeight, typeof(TurnStylePanel));
            Label(canvas, "Title", font, 60, FontStyle.Bold, k_Ink, new Vector2(0f, 0.84f), new Vector2(1f, 0.98f), "방향 바꾸기 방식");
            Label(canvas, "Sub", font, 34, FontStyle.Normal, k_Dim, new Vector2(0f, 0.75f), new Vector2(1f, 0.84f),
                "오른쪽 스틱을 좌우로 밀어 몸을 돌리는 방법 — 모든 훈련에 적용됩니다");

            var smooth = StyleButton(canvas, "SmoothButton", font, new Vector2(0.03f, 0.17f), new Vector2(0.49f, 0.73f),
                "부드럽게 돌기", "미는 동안 계속 돈다", "· 실제로 고개를 돌리듯 자연스러움\n· 어지러우면 '끊어 돌기'를 고르세요", out var smoothCheck, 54, 32);
            var snap = StyleButton(canvas, "SnapButton", font, new Vector2(0.51f, 0.17f), new Vector2(0.97f, 0.73f),
                "끊어 돌기", "한 번 밀 때마다 45°씩", "· 어지러움이 덜함 (처음이면 추천)\n· 스틱을 뒤로 당기면 뒤로 돌기", out var snapCheck, 54, 32);

            var status = Label(canvas, "Status", font, 34, FontStyle.Bold, k_Ink, new Vector2(0f, 0.03f), new Vector2(1f, 0.15f), "");

            var panel = canvas.GetComponent<TurnStylePanel>();
            var so = new SerializedObject(panel);
            so.FindProperty("m_SmoothButton").objectReferenceValue = smooth;
            so.FindProperty("m_SnapButton").objectReferenceValue = snap;
            so.FindProperty("m_SmoothCheck").objectReferenceValue = smoothCheck;
            so.FindProperty("m_SnapCheck").objectReferenceValue = snapCheck;
            so.FindProperty("m_Status").objectReferenceValue = status;
            so.ApplyModifiedPropertiesWithoutUndo();
            panel.ShowDefault();
        }

        // 판 앞면 바로 앞 월드 캔버스 — 판보다 조금 작게 (좌우 5 cm · 위아래 4 cm 안쪽)
        static RectTransform BoardCanvas(string name, Transform root, float bottom, float height, System.Type panelType)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(TrackedDeviceGraphicRaycaster), panelType);
            var canvas = (RectTransform)go.transform;
            canvas.SetParent(root, false);
            canvas.localPosition = new Vector3(0f, bottom + height * 0.5f, k_BoardDepth * 0.5f + 0.005f);
            canvas.localRotation = Quaternion.Euler(0f, 180f, 0f);   // 캔버스 앞면(-Z) → 판 앞(+Z)
            canvas.sizeDelta = new Vector2(k_BoardWidth - 0.1f, height - 0.08f) / k_CanvasScale;
            canvas.localScale = Vector3.one * k_CanvasScale;
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;   // 월드 공간 글자가 흐리지 않게
            return canvas;
        }

        static Button StyleButton(RectTransform parent, string name, Font font, Vector2 min, Vector2 max,
            string title, string sub, string body, out GameObject check, int titleSize = 70, int subSize = 40)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var button = go.GetComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.85f, 0.95f, 1f);   // 겨누면 살짝 푸르게
            colors.pressedColor = new Color(0.7f, 0.8f, 0.85f);
            colors.selectedColor = Color.white;
            button.colors = colors;

            // 첫 글씨(Label) = 버튼 이름 — RifleStylePanel 이 켜짐/꺼짐 색을 칠한다
            Label(rect, "Label", font, titleSize, FontStyle.Bold, Color.white, new Vector2(0f, 0.74f), new Vector2(1f, 0.95f), title);
            Label(rect, "Sub", font, subSize, FontStyle.Normal, k_Ink, new Vector2(0f, 0.62f), new Vector2(1f, 0.75f), sub);
            var bodyText = Label(rect, "Body", font, 34, FontStyle.Normal, k_Ink, new Vector2(0.07f, 0.17f), new Vector2(0.97f, 0.6f), body);
            bodyText.alignment = TextAnchor.MiddleLeft;
            bodyText.lineSpacing = 1.25f;
            check = Label(rect, "Check", font, 38, FontStyle.Bold, new Color(1f, 0.92f, 0.55f), new Vector2(0f, 0.03f), new Vector2(1f, 0.16f), "[ 선택됨 ]").gameObject;
            return button;
        }

        static Text Label(RectTransform parent, string name, Font font, int size, FontStyle style, Color color, Vector2 min, Vector2 max, string value)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = value;
            return t;
        }

        static void Box(string name, Transform parent, Vector3 localCenter, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCenter;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
        }

        static Material Lit(string name, Color color, float smoothness, float metallic)
        {
            string path = $"{k_MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
