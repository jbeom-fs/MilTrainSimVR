using MilTrainSimVR.A.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MilTrainSimVR.A.EditorTools
{
    // 사격장 문 두 개 (11/03). 모델 Models/DoubleDoor/DoubleDoor.fbx (양여닫이, 폭 1.968 · 높이 2.284 · 문틀 두께 0.15, 원점 = 아래 가운데,
    // 앞 +Z = 도어 클로저가 달린 쪽 → 사격장 안을 본다). 부스 뒤 대기 공간(z -0.4)에서 양 옆 벽에 마주 보게 — 벽 두께(0.2) 가운데에 끼운다.
    //   · 오른쪽 벽(사수가 표적을 볼 때 오른쪽 = Wall_East, +x) Door_Barracks — 들어온 문. 허브에서 사로 사격(A_Range)으로 오면 이 문 앞(Points/Point_RangeEntry)에 선다.
    //   · 왼쪽 벽(Wall_West, -x) Door_Exit — 비상구 유도등이 달린 문.
    //   두 문 모두 앞쪽 레버 손잡이(DoorTransition, m_ToHub)를 만지면 까매진 채 허브 씬을 불러온다. 늘 열림.
    //   11/10 사용자: 내무반 ↔ 사격장을 잇지 않고 내무반 - 허브 / 사격장 - 허브 로 따로 잇는다 (사격장 총은 4번 사로에 따로 고정).
    // 벽: 문이 있는 쪽 벽을 문 앞쪽(표적 쪽)만 남기고, 문 뒤쪽 Wall_*_Back · 문 위 Wall_*_Lintel 을 만든다 (내무반 벽 재질, m 단위 UV).
    // IndoorRangeBuilder 가 벽을 방 길이로 다시 늘리면 끝에 CutWall 을 다시 부른다.
    // 다시 누르면 문 두 개를 지우고 다시 짓는다.
    static class RangeDoorBuilder
    {
        const string k_Room = "Room_Range";
        const string k_ModelPath = "Assets/A/Models/DoubleDoor/DoubleDoor.fbx";
        const string k_BackingMaterialPath = "Assets/A/Models/Wall/Materials/Wall.mat";

        // 문 자리 (Room_Range 기준) — 사선 뒤 대기 공간(z -3.4 ~ 2.6) 가운데쯤
        const float k_DoorCenterZ = -0.4f;
        const float k_DoorWidth = 1.968f;     // 모델 문틀 바깥 폭
        const float k_DoorHeight = 2.284f;    // 모델 문틀 위 끝
        const float k_RoomBack = -3.4f;       // IndoorRangeBuilder.k_RoomBack
        const float k_RoomLength = 32f;       // IndoorRangeBuilder.k_RoomLength
        const float k_WallThickness = 0.2f;
        const float k_BackingDepth = 0.02f;   // 벽 바깥 면에 붙이는 얇은 막 — 문짝 틈으로 방 밖 빈 곳이 보이지 않게

        // 문 = (이름, 벽, 앞이 방 안쪽을 보게 돌리는 각도)
        static readonly (string door, string wall, float yaw)[] k_Doors =
        {
            ("Door_Barracks", "Wall_East", -90f),   // 앞 +Z → -x
            ("Door_Exit", "Wall_West", 90f),        // 앞 +Z → +x
        };

        // 사격장 시작 자리 (Room_Range 기준) — 들어온 문(Door_Barracks) 앞 1.15 m, 문을 등지고 방 안쪽(-x)을 봄. DoorBuilder 도 이 값을 쓴다
        internal static readonly Vector3 EntryLocal = new Vector3(6.6f, 0f, k_DoorCenterZ);
        internal const float EntryYaw = 270f;

        const string k_RangeCourseId = "A_Range";

        [MenuItem("A/만들기/사격장 문 (DoubleDoor · 내무반 · B/C 씬)")]
        static void Build()
        {
            var room = GameObject.Find(k_Room);
            Transform shell = room != null ? room.transform.Find("Shell") : null;
            var points = GameObject.Find("Points");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(k_ModelPath);
            if (shell == null || shell.Find("Wall_East") == null || shell.Find("Wall_West") == null || points == null || model == null)
            {
                Debug.LogError($"[A] {k_Room}/Shell/Wall_East · Wall_West · Points · {k_ModelPath} 중 없는 것이 있다");
                return;
            }
            Undo.SetCurrentGroupName("A: 사격장 문");
            int group = Undo.GetCurrentGroup();

            Material material = PaintedMaterial.Ensure("Assets/A/Models/DoubleDoor", "DoubleDoor");
            foreach (var (doorName, wallName, yaw) in k_Doors)
                BuildDoor(room.transform, shell.Find(wallName), doorName, yaw, model, material, null, toHub: true);

            // 사격장 시작 자리를 들어온 문 앞으로 (CourseStart 의 A_Range 가 이 점을 가리킨다)
            Transform entry = points.transform.Find("Point_RangeEntry");
            if (entry != null)
            {
                Undo.RecordObject(entry, "A: 사격장 문");
                entry.position = room.transform.TransformPoint(EntryLocal);
                entry.rotation = Quaternion.Euler(0f, room.transform.eulerAngles.y + EntryYaw, 0f);
            }
            else
                Debug.LogWarning("[A] Points/Point_RangeEntry 가 없다 — 메뉴 'A/만들기/내무반 문'을 먼저 누른다");

            CutWall(shell);

            Undo.CollapseUndoOperations(group);
            Debug.Log($"[A] 사격장 문 2개: z {k_DoorCenterZ} 양 옆 벽에 {k_DoorWidth} × {k_DoorHeight} m — 두 문 모두 → 허브");
        }

        // 과정 시작 자리 (11/10) — CourseStart 에 A_Range → Points/Point_RangeEntry(들어온 문 Door_Barracks 앞)를 건다.
        // A_Assembly 는 목록에 없다 → 씬에 놓인 리그 자리(내무반) 그대로. 다시 누르면 점 자리와 A_Range 줄만 고친다.
        [MenuItem("A/만들기/과정 시작 자리 (허브 → 사로 사격은 사격장 문 앞)")]
        static void BuildCourseStart()
        {
            var room = GameObject.Find(k_Room);
            var points = GameObject.Find("Points");
            if (room == null || points == null)
            {
                Debug.LogError($"[A] {k_Room} · Points 중 없는 것이 있다");
                return;
            }
            Undo.SetCurrentGroupName("A: 과정 시작 자리");
            int group = Undo.GetCurrentGroup();

            Transform point = points.transform.Find("Point_RangeEntry");
            if (point == null)
            {
                point = new GameObject("Point_RangeEntry").transform;
                Undo.RegisterCreatedObjectUndo(point.gameObject, "A: 과정 시작 자리");
                Undo.SetTransformParent(point, points.transform, "A: 과정 시작 자리");
            }
            Undo.RecordObject(point, "A: 과정 시작 자리");
            point.position = room.transform.TransformPoint(EntryLocal);
            point.rotation = Quaternion.Euler(0f, room.transform.eulerAngles.y + EntryYaw, 0f);
            // 11/10 오전에 쓰던 나가는 문 앞 점은 지운다
            Transform oldPoint = points.transform.Find("Point_HubEntry");
            if (oldPoint != null)
                Undo.DestroyObjectImmediate(oldPoint.gameObject);

            var start = Object.FindFirstObjectByType<CourseStart>();
            if (start == null)
            {
                var go = new GameObject("CourseStart");
                Undo.RegisterCreatedObjectUndo(go, "A: 과정 시작 자리");
                start = Undo.AddComponent<CourseStart>(go);
            }
            var so = new SerializedObject(start);
            var entries = so.FindProperty("m_Entries");
            SerializedProperty row = null;
            for (int i = 0; i < entries.arraySize; i++)
                if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("CourseId").stringValue == k_RangeCourseId)
                    row = entries.GetArrayElementAtIndex(i);
            if (row == null)
            {
                entries.arraySize++;
                row = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            }
            row.FindPropertyRelative("CourseId").stringValue = k_RangeCourseId;
            row.FindPropertyRelative("Point").objectReferenceValue = point;
            so.ApplyModifiedProperties();

            Undo.CollapseUndoOperations(group);
            Debug.Log($"[A] 과정 시작 자리: {k_RangeCourseId} → Points/Point_RangeEntry {point.position} · {point.eulerAngles.y}°");
        }

        static void BuildDoor(Transform room, Transform wall, string name, float yaw, GameObject model, Material material, Transform destination, bool toHub)
        {
            var old = room.Find(name);
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);
            var door = new GameObject(name).transform;
            Undo.RegisterCreatedObjectUndo(door.gameObject, "A: 사격장 문");
            door.SetParent(room, false);
            door.localPosition = new Vector3(wall.localPosition.x, 0f, k_DoorCenterZ);   // 벽 두께 가운데
            door.localRotation = Quaternion.Euler(0f, yaw, 0f);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, door);
            instance.name = "Model";
            PaintedMaterial.Assign(instance, material);

            // 걸어서 못 지나가게 문 전체를 막는 상자 (문 기준)
            var block = door.gameObject.AddComponent<BoxCollider>();
            block.center = new Vector3(0f, k_DoorHeight * 0.5f, 0f);
            block.size = new Vector3(k_DoorWidth, k_DoorHeight, k_WallThickness);

            // 벽 바깥 면에 붙인 얇은 막 (문 뒤 -Z 쪽)
            var backing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backing.name = "Backing";
            Object.DestroyImmediate(backing.GetComponent<BoxCollider>());
            backing.transform.SetParent(door, false);
            backing.transform.localPosition = new Vector3(0f, k_DoorHeight * 0.5f, -(k_WallThickness * 0.5f + k_BackingDepth * 0.5f));
            backing.transform.localScale = new Vector3(k_DoorWidth, k_DoorHeight, k_BackingDepth);
            var backingMaterial = AssetDatabase.LoadAssetAtPath<Material>(k_BackingMaterialPath);
            if (backingMaterial != null)
                backing.GetComponent<MeshRenderer>().sharedMaterial = backingMaterial;

            // 손잡이 — 앞쪽(사격장 쪽) 레버 두 개를 함께 덮는 상자
            Handle(door, instance, destination, toHub);

            if (name == "Door_Exit")
                ExitSign(door);
        }

        // 비상구 유도등 (11/03 사용자 그림 Models/EmergencyExit.png, 정사각형) — 나가는 문 위 벽에 붙인 얇은 등 상자. 그림이 은은하게 빛난다
        const string k_ExitSignTexture = "Assets/A/Models/EmergencyExit.png";
        const float k_ExitSignSize = 0.34f;
        const float k_ExitSignDepth = 0.05f;
        const float k_ExitSignGap = 0.12f;   // 문틀 위 끝과 등 아래 끝 사이

        static void ExitSign(Transform door)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(k_ExitSignTexture);
            if (texture == null)
            {
                Debug.LogWarning($"[A] {k_ExitSignTexture} 이 없다 — 비상구 등 없이 짓는다");
                return;
            }
            const string matPath = "Assets/A/Materials/Range/Range_EmergencyExit.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Range_EmergencyExit" };
                material.SetFloat("_Smoothness", 0.6f);   // 아크릴 판
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                material.SetColor("_EmissionColor", Color.white * 0.8f);   // 유도등 — 주위보다 조금 밝게
                AssetDatabase.CreateAsset(material, matPath);
            }
            material.SetTexture("_BaseMap", texture);
            material.SetTexture("_EmissionMap", texture);
            EditorUtility.SetDirty(material);

            float wallFace = k_WallThickness * 0.5f;   // 문 기준 앞(+Z) 벽 면
            var sign = new GameObject("ExitSign").transform;
            sign.SetParent(door, false);
            sign.localPosition = new Vector3(0f, k_DoorHeight + k_ExitSignGap + k_ExitSignSize * 0.5f, wallFace);

            var housing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            housing.name = "Housing";
            Object.DestroyImmediate(housing.GetComponent<Collider>());
            housing.transform.SetParent(sign, false);
            housing.transform.localPosition = new Vector3(0f, 0f, k_ExitSignDepth * 0.5f);
            housing.transform.localScale = new Vector3(k_ExitSignSize + 0.03f, k_ExitSignSize + 0.03f, k_ExitSignDepth);
            var frame = AssetDatabase.LoadAssetAtPath<Material>("Assets/A/Materials/Range/Range_SignFace.mat");   // 흰 테두리
            if (frame != null)
                housing.GetComponent<MeshRenderer>().sharedMaterial = frame;

            var face = GameObject.CreatePrimitive(PrimitiveType.Quad);
            face.name = "Face";
            Object.DestroyImmediate(face.GetComponent<Collider>());
            face.transform.SetParent(sign, false);
            face.transform.localPosition = new Vector3(0f, 0f, k_ExitSignDepth + 0.001f);
            face.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // 쿼드 앞면(-Z) → 방 안쪽(+Z)
            face.transform.localScale = new Vector3(k_ExitSignSize, k_ExitSignSize, 1f);
            face.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        // 문이 있는 벽을 문 구멍만큼 나눈다: 앞쪽(Wall_*) · 뒤쪽(Wall_*_Back) · 문 위(Wall_*_Lintel). 문이 없는 벽은 그대로
        internal static void CutWall(Transform shell)
        {
            if (shell == null)
                return;
            foreach (var (doorName, wallName, _) in k_Doors)
                if (shell.parent.Find(doorName) != null)
                    CutWall(shell, wallName);
        }

        static void CutWall(Transform shell, string wallName)
        {
            Transform wall = shell.Find(wallName);
            if (wall == null)
                return;
            float z0 = k_DoorCenterZ - k_DoorWidth * 0.5f;
            float z1 = k_DoorCenterZ + k_DoorWidth * 0.5f;
            float height = wall.localPosition.y + wall.localScale.y * 0.5f;
            var material = wall.GetComponent<MeshRenderer>()?.sharedMaterial;

            Undo.RecordObject(wall, "A: 사격장 문");
            wall.localScale = new Vector3(wall.localScale.x, wall.localScale.y, k_RoomLength - z1);
            wall.localPosition = new Vector3(wall.localPosition.x, wall.localPosition.y, (z1 + k_RoomLength) * 0.5f);

            Transform back = Segment(shell, wall, wallName + "_Back", material,
                new Vector3(wall.localPosition.x, height * 0.5f, (k_RoomBack + z0) * 0.5f),
                new Vector3(wall.localScale.x, height, z0 - k_RoomBack));
            Transform lintel = Segment(shell, wall, wallName + "_Lintel", material,
                new Vector3(wall.localPosition.x, (k_DoorHeight + height) * 0.5f, k_DoorCenterZ),
                new Vector3(wall.localScale.x, height - k_DoorHeight, k_DoorWidth));

            WallUvBuilder.Apply(wall);
            WallUvBuilder.Apply(back);
            WallUvBuilder.Apply(lintel);
        }

        static Transform Segment(Transform shell, Transform source, string name, Material material, Vector3 position, Vector3 scale)
        {
            Transform t = shell.Find(name);
            if (t == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                Undo.RegisterCreatedObjectUndo(go, "A: 사격장 문");
                t = go.transform;
                t.SetParent(shell, false);
                go.layer = source.gameObject.layer;
                GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(source.gameObject));
                if (material != null)
                    go.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
            else
                Undo.RecordObject(t, "A: 사격장 문");
            t.localRotation = Quaternion.identity;
            t.localPosition = position;
            t.localScale = scale;
            return t;
        }

        static void Handle(Transform door, GameObject model, Transform destination, bool toHub)
        {
            Bounds b = default;
            bool found = false;
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                if (!r.name.EndsWith("_Front_Lever"))
                    continue;
                if (!found) { b = r.bounds; found = true; }
                else b.Encapsulate(r.bounds);
            }
            if (!found)
            {
                Debug.LogWarning("[A] 문 모델에 *_Front_Lever 가 없다 — 문 가운데 1 m 높이에 둔다");
                b = new Bounds(door.TransformPoint(new Vector3(0f, 1.05f, 0.06f)), new Vector3(0.36f, 0.03f, 0.03f));
            }

            var root = new GameObject(door.name == "Door_Exit" ? "Handle_Exit" : "Handle_Range").transform;
            root.SetParent(door, false);
            root.position = b.center;
            root.localRotation = Quaternion.identity;
            var collider = root.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0f, 0.01f);
            collider.size = new Vector3(0.44f, 0.14f, 0.08f);   // 레버 두 개(x ±0.18) + 잡기 여유
            root.gameObject.AddComponent<XRSimpleInteractable>();
            var transition = root.gameObject.AddComponent<DoorTransition>();
            var so = new SerializedObject(transition);
            so.FindProperty("m_Destination").objectReferenceValue = destination;
            so.FindProperty("m_Fader").objectReferenceValue = Object.FindFirstObjectByType<ScreenFader>();
            so.FindProperty("m_ToHub").boolValue = toHub;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
