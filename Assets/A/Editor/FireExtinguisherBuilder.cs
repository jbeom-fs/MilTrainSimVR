using UnityEditor;
using UnityEngine;

namespace MilTrainSimVR.A.EditorTools
{
    // 소화기 (11/03 사용자 모델 Models/FireExtinguisher — 받침대 포함 0.24 × 0.53 × 0.26 m(씬에서도 그대로 1배 — 배율은 아래 두 값), 원점 = 아래 가운데, 압력계가 앞 +Z).
    //   · 사격장: 사선 뒤 벽(Wall_South, 안쪽 면 z -3.4)과 왼쪽 벽(Wall_West, 안쪽 면 x -7.75)이 만나는 구석 — 방 안쪽을 비스듬히 본다
    //   · 내무반: 문 오른쪽 전등 스위치(Barracks/LightSwitch, x 0.75 · 높이 1.2) 바로 아래 바닥 — 방 안쪽(-Z)을 본다
    // 바닥에 둔다. 부딪힘 상자 하나 (떨어진 총·탄창이 지나가지 않게). 다시 누르면 지우고 다시 놓는다.
    static class FireExtinguisherBuilder
    {
        const string k_Folder = "Assets/A/Models/FireExtinguisher";
        const string k_Name = "FireExtinguisher";
        const float k_WallGap = 0.02f;   // 벽과 받침대 사이
        const float k_RangeScale = 1f;       // 11/03 사용자: 2배 → 모델 크기 그대로 — 높이 약 0.53 m
        const float k_BarracksScale = 1f;    // 11/03 사용자: 내무반은 모델 크기 그대로 — 높이 약 0.53 m

        [MenuItem("A/만들기/소화기 (사격장 구석 · 내무반 스위치 아래)")]
        static void Build()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{k_Folder}/{k_Name}.fbx");
            var range = GameObject.Find("Room_Range");
            var barracks = GameObject.Find("Room_Assembly");
            var lightSwitch = GameObject.Find("Room_Assembly/Barracks/LightSwitch");
            if (model == null || range == null || barracks == null || lightSwitch == null)
            {
                Debug.LogError($"[A] {k_Folder}/{k_Name}.fbx · Room_Range · Room_Assembly · Room_Assembly/Barracks/LightSwitch 중 없는 것이 있다");
                return;
            }
            Undo.SetCurrentGroupName("A: 소화기");
            int group = Undo.GetCurrentGroup();
            Material material = PaintedMaterial.Ensure(k_Folder, k_Name);

            // 사격장 남서쪽 구석 — 45° 돌려 방 안쪽(+x +z)을 본다. 돌린 받침대(0.24 × 0.26)가 x·z 로 차지하는 반폭 = (0.12 + 0.13) × cos45° × 배율
            float half45 = (0.12f + 0.13f) * 0.7071f * k_RangeScale + k_WallGap;
            Place(range.transform, "FireExtinguisher_Range", model, material,
                new Vector3(-7.75f + half45, 0f, -3.4f + half45), 45f, k_RangeScale);

            // 내무반 스위치 아래 — 스위치가 붙은 벽 면(z -0.1)에서 받침대 반 깊이(0.13 × 배율)만큼 방 쪽으로, 압력계가 방(-Z)을 본다
            float half = 0.13f * k_BarracksScale + k_WallGap;
            Vector3 switchLocal = barracks.transform.InverseTransformPoint(lightSwitch.transform.position);
            Place(barracks.transform, "FireExtinguisher_Barracks", model, material,
                new Vector3(switchLocal.x, 0f, -0.1f - half), 180f, k_BarracksScale);

            Undo.CollapseUndoOperations(group);
            Debug.Log("[A] 소화기 2개: 사격장 남서쪽 구석, 내무반 전등 스위치 아래");
        }

        static void Place(Transform parent, string name, GameObject model, Material material, Vector3 localPosition, float yaw, float scale)
        {
            var old = parent.Find(name);
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);
            var root = new GameObject(name).transform;
            Undo.RegisterCreatedObjectUndo(root.gameObject, "A: 소화기");
            root.SetParent(parent, false);
            root.localPosition = localPosition;
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);
            root.localScale = Vector3.one * scale;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root);
            instance.name = "Model";
            PaintedMaterial.Assign(instance, material);

            var box = root.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.267f, 0f);
            box.size = new Vector3(0.24f, 0.534f, 0.26f);
        }
    }
}
