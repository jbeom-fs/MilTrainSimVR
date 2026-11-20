using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace MilTrainSimVR.UrbanCombat.EditorTools
{
    // 경기 진행을 씬에 넣는다 — 메뉴 MilTrainSimVR → C → 경기 진행 넣기 (10/27).
    // 지금 씬(C_UrbanBlock)의 'C_Match' 를 지우고 다시 만든다:
    //   · MatchFlow · MatchRecord · MatchDisplay
    //   · 점령지 백화점 3층 (CaptureZone) — 배치에서 백화점(쇼핑몰 + '백화점' 표시, 청운백화점 N05) 상자를 찾아 3층 자리·크기를 잰다.
    //     (10/27 ~ 11/15 는 N21 2층. 11/16 사용자: "N24 같은 나만 아는 게 아닌 모두가 알 수 있는 백화점으로") 지킬 자리는 적 수만큼
    //     길찾기 지도 위에서 서로 멀리 떨어지게 고르고, 점령지 가운데를 보게 한다. 열리면 켜지는 빛기둥(지붕 위 40m)
    //   · 앞판(안내·알림·결과 + 다시하기 버튼). 손목 정보는 손목시계(WristWatchBuilder 프리팹)를 연결한다
    //     (11/18 까지 있던 '손목 표시' — 왼손을 따라다니던 작은 글자판 — 는 사용자 지시로 지웠다)
    //   · 씬에 UI 입력(EventSystem + XRUIInputModule)이 없으면 넣는다 — 손 광선으로 버튼을 누르게
    // 맵을 다시 만들어도(맵 만들기) 이건 안 지워진다. 백화점을 옮겼거나 길찾기 지도를 다시 구웠으면 '점령지만 다시 넣기'를 누른다
    // (C_Match 전체를 지우지 않고 점령지만 바꾼다 — 앞판 자리 등 손댄 것이 남는다)
    public static class MatchSetupBuilder
    {
        const string k_RootName = "C_Match";
        const string k_FontPath = "Assets/C/Fonts/NotoSansKR-Regular SDF.asset";
        const string k_BeaconMaterialPath = "Assets/C/Materials/C_CaptureBeacon.mat";
        const int k_ZoneFloor = 3;    // 백화점 몇 층 (1층 = 1)
        // 맵 생성기와 같은 값 — 인도 턱(1층 바닥 높이) · 층 높이 · 벽 두께 · 옥상 난간
        const float k_Curb = 0.15f, k_FloorHeight = 3.4f, k_Wall = 0.2f, k_Parapet = 0.6f;
        const int k_SpotCount = 20;   // 적 수와 같게 — 10/29 적 8 → 10명, 11/16 11 → 20명 (가장 먼 곳부터 고른다)

        static string ZoneName => $"백화점 {k_ZoneFloor}층";

        // 배치에서 백화점 상자 — 쇼핑몰 평면에 '백화점'을 켠 건물 (청운백화점 N05)
        static GreyboxBuildingSpec FindZoneBuilding()
        {
            GameObject layout = UrbanLayoutTools.FindRoot(UrbanLayoutTools.LayoutRootName);
            if (layout == null)
                return null;
            foreach (GreyboxBuildingSpec spec in layout.GetComponentsInChildren<GreyboxBuildingSpec>(true))
            {
                if (spec.departmentStore && spec.layout == GreyboxBuildingSpec.Layout.Mall && spec.enterable >= k_ZoneFloor)
                    return spec;
            }
            return null;
        }

        [MenuItem("MilTrainSimVR/C/점령지만 다시 넣기 (백화점 3층)", false, 52)]
        public static void RebuildZone()
        {
            var scene = EditorSceneManager.GetActiveScene();
            GameObject root = UrbanLayoutTools.FindRoot(k_RootName);
            MatchFlow flow = root != null ? root.GetComponent<MatchFlow>() : null;
            GreyboxBuildingSpec building = FindZoneBuilding();
            if (EditorApplication.isPlayingOrWillChangePlaymode || flow == null || building == null)
            {
                Debug.LogWarning("[C] Play 중이 아니고, C_Match(경기 진행)와 배치의 백화점(3층까지 들어갈 수 있는)이 있어야 한다");
                return;
            }
            foreach (CaptureZone old in root.GetComponentsInChildren<CaptureZone>(true))
                Undo.DestroyObjectImmediate(old.gameObject);
            CaptureZone zone = BuildZone(root.transform, building);
            Undo.RegisterCreatedObjectUndo(zone.gameObject, "점령지");
            var so = new SerializedObject(flow);
            so.FindProperty("m_Zone").objectReferenceValue = zone;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[C] 점령지를 다시 넣었다 — {zone.DisplayName}, 지킬 자리 {zone.Spots.Count}곳");
        }

        [MenuItem("MilTrainSimVR/C/경기 진행 넣기 (점령지 · 결과판 · 손목시계 연결)", false, 52)]
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.path.StartsWith("Assets/C/"))
            {
                Debug.LogWarning("[C] Play 중이 아니고 C 씬에서만 넣는다");
                return;
            }
            GameObject enemies = UrbanLayoutTools.FindRoot("C_Enemies");
            var player = Object.FindAnyObjectByType<PlayerHitboxes>();
            var rifle = Object.FindAnyObjectByType<TempRifle>();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(k_FontPath);
            GreyboxBuildingSpec building = FindZoneBuilding();
            if (building == null || enemies == null || player == null || rifle == null || font == null)
            {
                Debug.LogWarning("[C] 배치의 백화점(3층까지) · C_Enemies · 플레이어(C_XR Origin) · 소총 · C 글꼴이 모두 있어야 한다");
                return;
            }

            GameObject old = UrbanLayoutTools.FindRoot(k_RootName);
            if (old != null)
                Undo.DestroyObjectImmediate(old);
            var root = new GameObject(k_RootName);
            Undo.RegisterCreatedObjectUndo(root, "경기 진행");
            var flow = root.AddComponent<MatchFlow>();          // MatchRecord 가 같이 붙는다
            var display = root.AddComponent<MatchDisplay>();

            CaptureZone zone = BuildZone(root.transform, building);
            Transform board = BuildBoard(root.transform, font, flow, out TMP_Text boardTitle, out TMP_Text boardText, out ScrollRect boardScroll, out GameObject restart);
            PlaceBoardAtTable(board);
            board.gameObject.SetActive(false);   // 시작 전 안내는 집결지의 임무 안내판이 한다 (11/19) — 앞판은 알림 · 결과 때 MatchDisplay 가 켠다
            EnsureEventSystem();

            var so = new SerializedObject(flow);
            so.FindProperty("m_Zone").objectReferenceValue = zone;
            so.FindProperty("m_EnemiesRoot").objectReferenceValue = enemies.transform;
            so.FindProperty("m_Player").objectReferenceValue = player.GetComponent<CombatantHealth>();
            so.FindProperty("m_StartRifle").objectReferenceValue = rifle.GetComponent<XRGrabInteractable>();
            so.ApplyModifiedPropertiesWithoutUndo();
            so = new SerializedObject(display);
            so.FindProperty("m_Flow").objectReferenceValue = flow;
            so.FindProperty("m_Record").objectReferenceValue = root.GetComponent<MatchRecord>();
            so.FindProperty("m_Player").objectReferenceValue = player.GetComponent<CombatantHealth>();
            so.FindProperty("m_Board").objectReferenceValue = board;
            so.FindProperty("m_BoardTitle").objectReferenceValue = boardTitle;
            so.FindProperty("m_BoardText").objectReferenceValue = boardText;
            so.FindProperty("m_BoardScroll").objectReferenceValue = boardScroll;
            so.FindProperty("m_RestartButton").objectReferenceValue = restart;
            so.FindProperty("m_WatchPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<WristWatch>(WristWatchBuilder.k_Prefab);   // 손목시계 (11/18) — 없으면 비어 있다
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[C] 경기 진행을 넣었다 — 점령지 {zone.DisplayName}, 지킬 자리 {zone.Spots.Count}곳");
        }

        // ---------------------------------------------------------------- 점령지

        static CaptureZone BuildZone(Transform parent, GreyboxBuildingSpec building)
        {
            Transform t = building.transform;
            float width = Mathf.Abs(t.lossyScale.x), depth = Mathf.Abs(t.lossyScale.z);
            float floor = k_Curb + (k_ZoneFloor - 1) * k_FloorHeight;
            var go = new GameObject("점령지 " + ZoneName);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(new Vector3(t.position.x, floor + 1.5f, t.position.z), Quaternion.Euler(0f, t.eulerAngles.y, 0f));
            var zone = go.AddComponent<CaptureZone>();
            // 그 층 바닥 ~ 3m 위, 바깥벽 안쪽 (가운데 뚫린 곳 · 계단실도 그 층 높이면 들어간다)
            var size = new Vector3(width - 2f * k_Wall, 3f, depth - 2f * k_Wall);

            List<Transform> spots = PickSpots(go.transform, size, floor);
            GameObject beacon = BuildBeacon(go.transform, t.position, k_Curb + building.floors * k_FloorHeight + k_Parapet);

            var so = new SerializedObject(zone);
            so.FindProperty("m_DisplayName").stringValue = ZoneName;
            so.FindProperty("m_Size").vector3Value = size;
            SerializedProperty list = so.FindProperty("m_Spots");
            list.arraySize = spots.Count;
            for (int i = 0; i < spots.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = spots[i];
            so.FindProperty("m_Marker").objectReferenceValue = beacon;
            so.ApplyModifiedPropertiesWithoutUndo();
            return zone;
        }

        // 그 층 바닥에서 걸을 수 있는 점(1m 격자)을 모아, 서로 가장 멀리 떨어진 k_SpotCount 곳을 고른다 — 방마다 흩어지게.
        // 보는 쪽은 점령지 가운데 (계단으로 올라온 사람을 보게)
        static List<Transform> PickSpots(Transform zone, Vector3 size, float floor)
        {
            var points = new List<Vector3>();
            var seen = new HashSet<Vector3Int>();
            for (float x = -size.x * 0.5f + 0.5f; x < size.x * 0.5f; x += 1f)
            {
                for (float z = -size.z * 0.5f + 0.5f; z < size.z * 0.5f; z += 1f)
                {
                    Vector3 p = zone.TransformPoint(new Vector3(x, 0f, z));
                    p.y = floor;
                    if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 0.3f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - floor) > 0.25f)
                        continue;
                    if (seen.Add(Vector3Int.RoundToInt(hit.position * 2f)))
                        points.Add(new Vector3(hit.position.x, floor, hit.position.z));
                }
            }
            var chosen = new List<Vector3>();
            Vector3 center = new Vector3(zone.position.x, floor, zone.position.z);
            while (chosen.Count < k_SpotCount && points.Count > 0)
            {
                int best = -1;
                float bestScore = -1f;
                for (int i = 0; i < points.Count; i++)
                {
                    float score = float.MaxValue;
                    if (chosen.Count == 0)
                        score = (points[i] - center).sqrMagnitude;
                    foreach (Vector3 c in chosen)
                        score = Mathf.Min(score, (points[i] - c).sqrMagnitude);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = i;
                    }
                }
                chosen.Add(points[best]);
                points.RemoveAt(best);
            }
            var spots = new List<Transform>();
            for (int i = 0; i < chosen.Count; i++)
            {
                var spot = new GameObject($"자리 {i + 1}").transform;
                spot.SetParent(zone, true);
                Vector3 look = Vector3.ProjectOnPlane(center - chosen[i], Vector3.up);
                if (look.sqrMagnitude < 2f)
                    look = zone.rotation * Vector3.back;   // 가운데 근처면 건물 앞(문) 쪽
                spot.SetPositionAndRotation(chosen[i], Quaternion.LookRotation(look, Vector3.up));
                spots.Add(spot);
            }
            if (spots.Count < k_SpotCount)
                Debug.LogWarning($"[C] 점령지 {ZoneName}에서 걸을 수 있는 점이 모자라 지킬 자리를 {spots.Count}곳만 골랐다 — 길찾기 지도를 확인");
            return spots;
        }

        // 점령지가 열리면 멀리서도 보이는 빛기둥 — 건물 지붕 위로 40m, 콜라이더 없음(총알·몸이 안 걸린다)
        static GameObject BuildBeacon(Transform zone, Vector3 buildingCenter, float roofTop)
        {
            GameObject beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beacon.name = "빛기둥";
            Object.DestroyImmediate(beacon.GetComponent<Collider>());
            beacon.transform.SetParent(zone, true);
            beacon.transform.position = new Vector3(buildingCenter.x, roofTop + 20f, buildingCenter.z);
            beacon.transform.localScale = new Vector3(1.6f, 20f, 1.6f);
            var renderer = beacon.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = BeaconMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            beacon.SetActive(false);
            return beacon;
        }

        static Material BeaconMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(k_BeaconMaterialPath);
            if (mat != null)
                return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetFloat("_Surface", 1f);   // 반투명
            mat.SetFloat("_Blend", 0f);     // 알파
            mat.SetColor("_BaseColor", new Color(1f, 0.62f, 0.18f, 0.35f));
            BaseShaderGUI.SetMaterialKeywords(mat);
            AssetDatabase.CreateAsset(mat, k_BeaconMaterialPath);
            return mat;
        }

        // ---------------------------------------------------------------- 손목 표시 · 앞판

        // 세계 공간 캔버스 — 1단위 = scale m. 글자 크기도 그 단위.
        // 캔버스는 일반 오브젝트(holder) 밑에 두고 holder 를 옮긴다 — 일반 오브젝트 밑 RectTransform 을 에디터에서 옮기면
        // 저장할 때 x·y 가 0 으로 돌아갔다 (10/27, 다시 연 씬에서 안내판이 맵 원점 쪽으로 감)
        static RectTransform MakeCanvas(string name, Transform parent, Vector2 size, float scale, out Transform holder)
        {
            holder = new GameObject(name).transform;
            holder.SetParent(parent, false);
            var go = new GameObject("판", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(holder, false);
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one * scale;
            return rect;
        }

        static Image Panel(Transform parent, Color color)
        {
            var go = new GameObject("배경", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform, Vector4.zero);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static TMP_Text MakeText(Transform parent, TMP_FontAsset font, float size, TextAlignmentOptions align, Vector4 margin)
        {
            var go = new GameObject("글", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform, margin);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.alignment = align;
            text.color = MatchDisplay.BoardTextColor;
            text.raycastTarget = false;
            return text;
        }

        // 부모를 꽉 채우고 안쪽 여백(왼·아래·오른·위)만큼 뺀다
        static void Stretch(RectTransform rect, Vector4 margin)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(margin.x, margin.y);
            rect.offsetMax = new Vector2(-margin.z, -margin.w);
        }

        // 앞판: 1.1 × 0.80m, 글자 4cm. 손 광선으로 누르는 다시하기 버튼 (TrackedDeviceGraphicRaycaster).
        // 11/19 사용자: B 의 UI 기준에 맞춘다 (Docs/B/1117_B_UI_색상팔레트_스타일가이드.md — 색은 MatchDisplay.Board…):
        //   · 위: 제목 줄 (가운데 · 보통 굵기, 제목 52 + 이유 40) — 그 전엔 본문 첫 줄 왼쪽에 굵게
        //   · 가운데: 본문 세로 스크롤 (세로만 · Clamped · 관성 없음 · 감도 8, 오른쪽 스크롤바 폭 28 · 내용이 다 보이면 숨김)
        //   · 아래: 스크롤 밖 고정 버튼 줄 — 단색 사각 파랑 (그 전엔 모서리 둥근 초록)
        // 제목 줄이 따로 생긴 만큼 판을 0.66 → 0.80m 로 키웠다 — 흔한 결과(포탈 안내 2줄 + 네 항목 + 처치 2줄 + 맞은 곳 2줄)가 스크롤 없이 다 보이게.
        // 판 크기는 B 문서가 C 화면에 강제하지 않는다 (B 교육 화면은 720 × 440)
        static Transform BuildBoard(Transform parent, TMP_FontAsset font, MatchFlow flow, out TMP_Text title, out TMP_Text text, out ScrollRect scroll, out GameObject restart)
        {
            const float side = 56f, top = 36f, titleHeight = 150f, gap = 12f, bottom = 150f;   // 제목 두 줄 = 147, bottom = 버튼 줄 몫
            RectTransform rect = MakeCanvas("앞판", parent, new Vector2(1100f, 800f), 0.001f, out Transform holder);
            rect.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            Panel(rect, MatchDisplay.BoardBackground);

            title = MakeText(rect, font, 52f, TextAlignmentOptions.Top, Vector4.zero);
            title.name = "제목";
            var tr = (RectTransform)title.transform;
            tr.anchorMin = new Vector2(0f, 1f);
            tr.anchorMax = new Vector2(1f, 1f);
            tr.pivot = new Vector2(0.5f, 1f);
            tr.offsetMin = new Vector2(side, -(top + titleHeight));
            tr.offsetMax = new Vector2(-side, -top);
            title.SetText("시가지 전투");

            var body = new GameObject("본문", typeof(RectTransform), typeof(ScrollRect));
            body.transform.SetParent(rect, false);
            Stretch((RectTransform)body.transform, new Vector4(side, bottom, side, top + titleHeight + gap));
            var viewport = new GameObject("보이는 곳", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            viewport.transform.SetParent(body.transform, false);
            Stretch((RectTransform)viewport.transform, Vector4.zero);
            viewport.GetComponent<Image>().color = Color.clear;   // 안 보이지만 광선은 받는다 — 본문을 끌어서 넘긴다
            text = MakeText(viewport.transform, font, 40f, TextAlignmentOptions.TopLeft, Vector4.zero);
            var cr = (RectTransform)text.transform;
            cr.anchorMin = new Vector2(0f, 1f);
            cr.anchorMax = new Vector2(1f, 1f);
            cr.pivot = new Vector2(0.5f, 1f);
            cr.offsetMin = cr.offsetMax = Vector2.zero;
            text.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;   // 글 높이만큼 — 넘치면 스크롤
            text.SetText("탁자 위 소총을 잡으면 시작한다");

            var bar = new GameObject("스크롤바", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            bar.transform.SetParent(body.transform, false);
            var barRect = (RectTransform)bar.transform;
            barRect.anchorMin = new Vector2(1f, 0f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(0f, 0.5f);
            barRect.sizeDelta = new Vector2(28f, 0f);
            barRect.anchoredPosition = new Vector2(14f, 0f);   // 본문 오른쪽 여백(56) 안
            bar.GetComponent<Image>().color = MatchDisplay.BoardTrack;
            var slide = new GameObject("움직이는 곳", typeof(RectTransform));
            slide.transform.SetParent(bar.transform, false);
            Stretch((RectTransform)slide.transform, Vector4.zero);
            var handle = new GameObject("손잡이", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(slide.transform, false);
            Stretch((RectTransform)handle.transform, Vector4.zero);
            handle.GetComponent<Image>().color = MatchDisplay.BoardButton;
            var scrollbar = bar.GetComponent<Scrollbar>();
            scrollbar.handleRect = (RectTransform)handle.transform;
            scrollbar.targetGraphic = handle.GetComponent<Image>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            scroll = body.GetComponent<ScrollRect>();
            scroll.viewport = (RectTransform)viewport.transform;
            scroll.content = cr;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            scroll.scrollSensitivity = 8f;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            restart = new GameObject("다시하기", typeof(RectTransform), typeof(Image), typeof(Button));
            restart.transform.SetParent(rect, false);
            var br = (RectTransform)restart.transform;
            br.anchorMin = br.anchorMax = new Vector2(0.5f, 0f);
            br.pivot = new Vector2(0.5f, 0f);
            br.sizeDelta = new Vector2(320f, 90f);
            br.anchoredPosition = new Vector2(0f, 36f);
            var image = restart.GetComponent<Image>();
            image.color = MatchDisplay.BoardButton;   // 단색 사각 (스프라이트 없음)
            var button = restart.GetComponent<Button>();
            button.targetGraphic = image;
            UnityEventTools.AddPersistentListener(button.onClick, flow.Restart);
            TMP_Text label = MakeText(restart.transform, font, 40f, TextAlignmentOptions.Center, Vector4.zero);
            label.SetText("다시하기");
            restart.SetActive(false);
            return holder;
        }

        // 씬의 앞판만 새로 만든다 — C_Match 의 다른 것(점령지 · 손댄 값)은 그대로. 앞판 자리는 뜰 때마다 MatchDisplay 가 눈앞으로 옮긴다
        [MenuItem("MilTrainSimVR/C/결과판만 다시 넣기 (B UI 기준)", false, 52)]
        public static void RebuildBoard()
        {
            var scene = EditorSceneManager.GetActiveScene();
            GameObject root = UrbanLayoutTools.FindRoot(k_RootName);
            MatchFlow flow = root != null ? root.GetComponent<MatchFlow>() : null;
            MatchDisplay display = root != null ? root.GetComponent<MatchDisplay>() : null;
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(k_FontPath);
            if (EditorApplication.isPlayingOrWillChangePlaymode || flow == null || display == null || font == null)
            {
                Debug.LogWarning("[C] Play 중이 아니고, C_Match(경기 진행)와 C 글꼴이 있어야 한다");
                return;
            }
            var so = new SerializedObject(display);
            var old = so.FindProperty("m_Board").objectReferenceValue as Transform;
            Transform board = BuildBoard(root.transform, font, flow, out TMP_Text boardTitle, out TMP_Text boardText, out ScrollRect boardScroll, out GameObject restart);
            Undo.RegisterCreatedObjectUndo(board.gameObject, "결과판");
            if (old != null)
            {
                board.SetSiblingIndex(old.GetSiblingIndex());
                board.SetPositionAndRotation(old.position, old.rotation);
                Undo.DestroyObjectImmediate(old.gameObject);
            }
            board.gameObject.SetActive(false);
            so.FindProperty("m_Board").objectReferenceValue = board;
            so.FindProperty("m_BoardTitle").objectReferenceValue = boardTitle;
            so.FindProperty("m_BoardText").objectReferenceValue = boardText;
            so.FindProperty("m_BoardScroll").objectReferenceValue = boardScroll;
            so.FindProperty("m_RestartButton").objectReferenceValue = restart;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[C] 결과판을 다시 넣었다 (B UI 기준 — 제목 줄 · 본문 스크롤 · 파랑 버튼)");
        }

        // 시작 전 안내 자리 — 탁자 뒤 위, 출발 자리(리그)를 보게
        static void PlaceBoardAtTable(Transform board)
        {
            GameObject table = GameObject.Find("C_StartArea/RifleTable");
            var origin = Object.FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
            if (table == null || origin == null)
                return;
            Vector3 eye = origin.transform.position + Vector3.up * 1.6f;
            Vector3 p = table.transform.position + new Vector3(0f, 1.35f, 1.3f);
            board.SetPositionAndRotation(p, Quaternion.LookRotation(Vector3.ProjectOnPlane(p - eye, Vector3.up), Vector3.up));
        }

        // 손 광선으로 UI 를 누르려면 씬에 EventSystem + XRUIInputModule 이 있어야 한다 (C_Sandbox 와 같은 구성)
        static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
                return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(XRUIInputModule));
            Undo.RegisterCreatedObjectUndo(go, "UI 입력");
        }
    }
}
