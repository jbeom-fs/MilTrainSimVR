// 세단 프리팹 — 뿌리(콜라이더 둘) → Model(자리 · 방향 · 크기) → Sedan.fbx. 열린 씬은 건드리지 않게 미리보기 씬에서 만든다
var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
bool dirtyBefore = active.isDirty;
string folder = "Assets/C/Prefabs/Vehicle";
if (!UnityEditor.AssetDatabase.IsValidFolder(folder))
    UnityEditor.AssetDatabase.CreateFolder("Assets/C/Prefabs", "Vehicle");
string path = folder + "/C_Vehicle_Sedan_White.prefab";
var fbx = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/C/Models/Tripo/Sedan/Sedan.fbx");
var mesh = fbx.GetComponentInChildren<UnityEngine.MeshFilter>(true).sharedMesh;
var scale = new UnityEngine.Vector3(4.35f, 4.12f, 4.2f);       // 길이 4.35 m · 높이 1.49 m · 폭 1.96 m(거울 포함)
float lift = -mesh.bounds.min.y * scale.y;                      // 바퀴 바닥이 y = 0
var ps = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
string result;
try
{
    var root = new UnityEngine.GameObject("C_Vehicle_Sedan_White");
    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, ps);
    var holder = new UnityEngine.GameObject("Model");
    holder.transform.SetParent(root.transform, false);
    holder.transform.localPosition = new UnityEngine.Vector3(0f, lift, 0f);
    holder.transform.localRotation = UnityEngine.Quaternion.Euler(0f, 90f, 0f);   // 모델의 앞(-X) → 프리팹 +Z
    holder.transform.localScale = scale;
    var model = (UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(fbx, holder.transform);
    // 판정 상자 둘 — 아래 몸통(바닥 ~ 보닛 높이) · 객실(유리 ~ 지붕)
    var body = root.AddComponent<UnityEngine.BoxCollider>();
    body.center = new UnityEngine.Vector3(0f, 0.46f, 0f);
    body.size = new UnityEngine.Vector3(1.70f, 0.92f, 4.30f);
    var cabin = root.AddComponent<UnityEngine.BoxCollider>();
    cabin.center = new UnityEngine.Vector3(0f, 1.195f, -0.275f);
    cabin.size = new UnityEngine.Vector3(1.40f, 0.55f, 2.15f);
    var flags = UnityEditor.StaticEditorFlags.BatchingStatic | UnityEditor.StaticEditorFlags.ReflectionProbeStatic;
    foreach (var t in root.GetComponentsInChildren<UnityEngine.Transform>(true))
        UnityEditor.GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
    var saved = UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, path);
    result = "saved=" + (saved != null) + " path=" + path + " lift=" + lift.ToString("F4");
}
finally
{
    UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(ps);
}
UnityEditor.AssetDatabase.SaveAssets();
return result + " | activeScene dirty before=" + dirtyBefore + " after=" + active.isDirty + " rootCount=" + active.rootCount;
