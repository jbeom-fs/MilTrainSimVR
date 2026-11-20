using MilTrainSimVR.A.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MilTrainSimVR.A.EditorTools
{
    // 내무반 ↔ 사격장 문 (10/29). 모델 Models/Door/sage_glass_door.fbx (폭 1.016 · 높이 2.644 · 두께 0.194, 원점 = 아래 가운데, 앞 +Z = 사격장 쪽).
    // 벽을 문에 맞춘다 (사용자 10/29): 내무반 북쪽 벽의 구멍 폭 = 문 폭, 문 위 벽 없음, 내무반 높이(벽·천장) = 문 높이 — 문이 천장에 딱 닿는다.
    // 문은 열리지 않는다 — 내무반 쪽 손잡이(DoorTransition)를 만지면 화면이 까매졌다가 사격장 도착 자리에서 밝아진다.
    // → 11/10 사용자: 내무반 - 허브 / 사격장 - 허브 로 따로 잇는다. 손잡이는 허브로 나간다 (m_ToHub). 이름(Door_Range)은 B 반입 도구가 찾으므로 그대로.
    // 손잡이 자리는 모델의 둥근 손잡이(Round_knob_Back, 내무반 쪽 -Z).
    // 사격장은 따로 떨어진 방이다 (IndoorRangeBuilder, 사용자 10/29: 같은 씬, 맵만 분리). 문 바로 바깥은 얇은 벽(Backing)으로 막아 유리 너머가 비어 보이지 않게 한다.
    // 사격장 도착 점은 Room_Range 기준 자리에 둔다. 돌아오는 도착 점 Point_BarracksEntry 는 만들어 둔다 — 사격장 쪽 새 문(따로 디자인)에 DoorTransition 으로 잇는다.
    // 다시 누르면 Door_Range 를 지우고 다시 짓는다. 도착 점·화면 덮개(ScreenFader)는 있으면 그대로 쓴다.
    static class DoorBuilder
    {
        const string k_Parent = "Room_Assembly";
        const string k_Shell = "Room_Assembly/Shell";
        const string k_Points = "Points";
        const string k_RangeRoom = "Room_Range";
        const string k_BackingMaterialPath = "Assets/A/Models/Wall/Materials/Wall.mat";   // 내무반 벽과 같은 재질 (유리 너머로 보인다)
        const float k_BackingDepth = 0.07f;   // 문 바깥 면(z 0.1)에서 안쪽으로 — 문 모델 앞쪽 손잡이·문틀이 묻히는 깊이
        const string k_ModelPath = "Assets/A/Models/Door/sage_glass_door.fbx";
        const string k_FadeMaterialFolder = "Assets/A/Materials/Common";
        const string k_FadeMaterialPath = k_FadeMaterialFolder + "/ScreenFade.mat";

        const float k_RoomHalfWidth = 5.2f;   // 북쪽 벽 바깥 끝 (Wall_North_L/R)
        const float k_WallThickness = 0.2f;
        const float k_OldRoomHeight = 3f;     // 문에 맞추기 전 내무반 높이 — 이 높이에 닿던 벽만 바꾼다

        static readonly Vector3 k_BarracksEntry = new Vector3(0f, 0f, -1.2f);  // 내무반 문 앞, 내무반 쪽(-Z)을 봄

        [MenuItem("A/만들기/내무반 문 (문 모델 · 사격장 전환)")]
        static void Build()
        {
            var parent = GameObject.Find(k_Parent);
            var shell = GameObject.Find(k_Shell);
            var points = GameObject.Find(k_Points);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(k_ModelPath);
            if (parent == null || shell == null || points == null || model == null)
            {
                Debug.LogError($"[A] {k_Parent} · {k_Shell} · {k_Points} · {k_ModelPath} 중 없는 것이 있다");
                return;
            }
            Undo.SetCurrentGroupName("A: 내무반 문");
            int group = Undo.GetCurrentGroup();

            var rangeRoom = GameObject.Find(k_RangeRoom);
            // 사격장 도착 = 사격장 문 앞 (11/03 사용자 — 자리는 RangeDoorBuilder 가 정한다)
            Vector3 rangeEntryWorld = rangeRoom != null ? rangeRoom.transform.TransformPoint(RangeDoorBuilder.EntryLocal) : RangeDoorBuilder.EntryLocal;
            float rangeYaw = (rangeRoom != null ? rangeRoom.transform.eulerAngles.y : 0f) + RangeDoorBuilder.EntryYaw;
            Transform rangeEntry = Point(points.transform, "Point_RangeEntry", rangeEntryWorld, rangeYaw);
            Point(points.transform, "Point_BarracksEntry", k_BarracksEntry, 180f);   // 사격장 쪽 새 문이 쓸 자리
            EnsureFader();

            var old = parent.transform.Find("Door_Range");
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);
            var door = new GameObject("Door_Range").transform;
            Undo.RegisterCreatedObjectUndo(door.gameObject, "A: 내무반 문");
            door.SetParent(parent.transform, false);
            door.localPosition = Vector3.zero;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, door);
            instance.name = "Model";
            Bounds bounds = WorldBounds(instance);
            float width = bounds.size.x;
            float height = bounds.max.y;

            // 걸어서 못 지나가게 문 전체를 막는 상자
            var block = door.gameObject.AddComponent<BoxCollider>();
            block.center = door.InverseTransformPoint(bounds.center);
            block.size = bounds.size;

            FitWalls(shell.transform, width, height);

            // 문 바로 바깥을 막는 얇은 벽 — 앞면은 벽 바깥 면(z 0.1)
            var backing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backing.name = "Backing";
            backing.transform.SetParent(door, false);
            backing.transform.localPosition = new Vector3(0f, height * 0.5f, k_WallThickness * 0.5f - k_BackingDepth * 0.5f);
            backing.transform.localScale = new Vector3(width, height, k_BackingDepth);
            var backingMaterial = AssetDatabase.LoadAssetAtPath<Material>(k_BackingMaterialPath);
            if (backingMaterial != null)
                backing.GetComponent<MeshRenderer>().sharedMaterial = backingMaterial;

            // 손잡이 — 모델의 둥근 손잡이 자리 (내무반 쪽 Back, -Z) → 사격장
            Handle("Handle_Barracks", door, KnobCenter(instance, "Round_knob_Back"), -1f, rangeEntry);

            Undo.CollapseUndoOperations(group);
            Debug.Log($"[A] 내무반 문: 모델 {width:0.###} × {height:0.###} m → 문 구멍 폭 {width:0.###} m, 내무반 높이 {height:0.###} m. 벽 무늬는 메뉴 'A/만들기/조립실 벽'을 다시 누른다");
        }

        // 내무반 벽·천장을 문에 맞춘다: 북쪽 벽 구멍 = 문 폭, 문 위 벽 없음, 예전 높이(3 m)에 닿던 벽·천장 = 문 높이
        static void FitWalls(Transform shell, float doorWidth, float height)
        {
            float half = doorWidth * 0.5f;
            foreach (Transform t in shell)
            {
                if (t.name == "Wall_North_Lintel")
                {
                    Undo.RecordObject(t.gameObject, "A: 내무반 문");
                    t.gameObject.SetActive(false);   // 문이 천장까지 닿는다
                    continue;
                }
                Undo.RecordObject(t, "A: 내무반 문");
                if (t.name == "Wall_North_L" || t.name == "Wall_North_R")
                {
                    float side = t.name.EndsWith("_L") ? -1f : 1f;
                    float w = k_RoomHalfWidth - half;
                    t.localScale = new Vector3(w, height, k_WallThickness);
                    t.localPosition = new Vector3(side * (half + w * 0.5f), height * 0.5f, t.localPosition.z);
                    continue;
                }
                if (t.name == "Ceiling")
                {
                    t.localPosition = new Vector3(t.localPosition.x, height + t.localScale.y * 0.5f, t.localPosition.z);
                    continue;
                }
                if (!t.name.StartsWith("Wall_"))
                    continue;
                // 예전 높이(또는 이미 맞춘 높이)에 닿는 벽: 아래 끝은 두고 위 끝을 문 높이로 (바닥부터 서는 벽·창 위 벽 모두)
                float bottom = t.localPosition.y - t.localScale.y * 0.5f;
                float top = t.localPosition.y + t.localScale.y * 0.5f;
                if (Mathf.Abs(top - k_OldRoomHeight) > 0.01f && Mathf.Abs(top - height) > 0.01f)
                    continue;
                float newHeight = height - bottom;
                t.localScale = new Vector3(t.localScale.x, newHeight, t.localScale.z);
                t.localPosition = new Vector3(t.localPosition.x, bottom + newHeight * 0.5f, t.localPosition.z);
            }
        }

        static Bounds WorldBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers)
                b.Encapsulate(r.bounds);
            return b;
        }

        // 모델 속 손잡이 메시의 가운데 (이름 앞부분으로 찾는다). 없으면 문 가운데 1 m 높이
        static Vector3 KnobCenter(GameObject model, string prefix)
        {
            foreach (var r in model.GetComponentsInChildren<Renderer>())
                if (r.name.StartsWith(prefix))
                    return r.bounds.center;
            Debug.LogWarning($"[A] 문 모델에 {prefix} 가 없다");
            return model.transform.position + Vector3.up;
        }

        // 손잡이 자리의 잡기 쉬운 부딪힘 상자 + 문 전환 (모양은 모델의 손잡이)
        static void Handle(string name, Transform door, Vector3 worldPosition, float side, Transform destination)
        {
            var root = new GameObject(name).transform;
            root.SetParent(door, false);
            root.position = worldPosition;

            var collider = root.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0f, side * 0.01f);
            collider.size = new Vector3(0.12f, 0.12f, 0.08f);
            root.gameObject.AddComponent<XRSimpleInteractable>();
            var transition = root.gameObject.AddComponent<DoorTransition>();
            var so = new SerializedObject(transition);
            so.FindProperty("m_Destination").objectReferenceValue = destination;
            so.FindProperty("m_Fader").objectReferenceValue = Object.FindFirstObjectByType<ScreenFader>();
            so.FindProperty("m_ToHub").boolValue = true;   // 11/10 — 사격장 대신 허브로
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Transform Point(Transform points, string name, Vector3 position, float yaw)
        {
            Transform t = points.Find(name);
            if (t == null)
            {
                t = new GameObject(name).transform;
                Undo.RegisterCreatedObjectUndo(t.gameObject, "A: 내무반 문");
                t.SetParent(points, false);
            }
            else
                Undo.RecordObject(t, "A: 내무반 문");
            t.position = position;
            t.rotation = Quaternion.Euler(0f, yaw, 0f);
            return t;
        }

        // 화면 덮개 — 씬에 하나. 재질은 조준 창과 같은 '맨 위에 그리기' 셰이더
        static void EnsureFader()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(k_FadeMaterialPath);
            if (material == null)
            {
                if (!AssetDatabase.IsValidFolder(k_FadeMaterialFolder))
                    AssetDatabase.CreateFolder("Assets/A/Materials", "Common");
                material = new Material(Shader.Find("MilTrainSimVR/A/AimOverlay")) { name = "ScreenFade" };
                material.SetColor("_Color", Color.black);
                material.renderQueue = 4100;   // 조준 창(Overlay+2 까지)보다 위
                AssetDatabase.CreateAsset(material, k_FadeMaterialPath);
            }
            var fader = Object.FindFirstObjectByType<ScreenFader>();
            if (fader == null)
            {
                var go = new GameObject("ScreenFader");
                Undo.RegisterCreatedObjectUndo(go, "A: 내무반 문");
                fader = go.AddComponent<ScreenFader>();
            }
            var so = new SerializedObject(fader);
            so.FindProperty("m_Material").objectReferenceValue = material;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
