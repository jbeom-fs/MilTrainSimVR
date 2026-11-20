// 10만 세단을 원본 옆(동쪽 6.5 m)에 비교용으로 놓는다 — 같은 방향 · 같은 크기. 기존 두 대는 건드리지 않고, 저장도 하지 않는다
if (UnityEngine.Application.isPlaying) return "NOT PLACED: Play mode";
var fbx = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/C/Models/Tripo/Sedan/Sedan100k.fbx");
if (fbx == null) return "NOT IMPORTED YET";
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (scene.path != "Assets/C/Scenes/C_UrbanBlock.unity") return "NOT PLACED: active scene is " + scene.path;
var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "C_Vehicles");
if (root == null) return "NOT PLACED: C_Vehicles root not found";
string holderName = "세단 10만 (비교용)";
var old = root.transform.Find(holderName);
if (old != null) UnityEditor.Undo.DestroyObjectImmediate(old.gameObject);
var holder = new UnityEngine.GameObject(holderName);
holder.transform.SetParent(root.transform, false);
var inst = (UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(fbx, holder.transform);
var pts = new System.Collections.Generic.List<UnityEngine.Vector3>();
foreach (var mf in inst.GetComponentsInChildren<UnityEngine.MeshFilter>(true))
{
    var l2w = mf.transform.localToWorldMatrix;
    var vs = mf.sharedMesh.vertices;
    for (int i = 0; i < vs.Length; i += 5) pts.Add(l2w.MultiplyPoint3x4(vs[i]));
}
var mn = new UnityEngine.Vector3(pts.Min(p => p.x), pts.Min(p => p.y), pts.Min(p => p.z));
var mx = new UnityEngine.Vector3(pts.Max(p => p.x), pts.Max(p => p.y), pts.Max(p => p.z));
var size = mx - mn;
bool lengthIsX = size.x >= size.z;
float len0 = lengthIsX ? mn.x : mn.z, len = lengthIsX ? size.x : size.z;
float topLow = pts.Where(p => ((lengthIsX ? p.x : p.z) - len0) / len < 0.12f).Max(p => p.y);
float topHigh = pts.Where(p => ((lengthIsX ? p.x : p.z) - len0) / len > 0.88f).Max(p => p.y);
bool frontIsMin = topLow < topHigh;
holder.transform.localScale = lengthIsX ? new UnityEngine.Vector3(4.35f, 4.12f, 4.2f) : new UnityEngine.Vector3(4.2f, 4.12f, 4.35f);
float yaw = lengthIsX ? (frontIsMin ? 0f : 180f) : (frontIsMin ? 90f : 270f);
holder.transform.rotation = UnityEngine.Quaternion.Euler(0f, yaw, 0f);
System.Func<UnityEngine.Bounds> bnd = () =>
{
    UnityEngine.Bounds b = new UnityEngine.Bounds();
    bool first = true;
    foreach (var r in holder.GetComponentsInChildren<UnityEngine.Renderer>(true)) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
    return b;
};
var target = new UnityEngine.Vector3(75f, 0f, 124.1f);
var b0 = bnd();
holder.transform.position += new UnityEngine.Vector3(target.x - b0.center.x, target.y - b0.min.y, target.z - b0.center.z);
UnityEditor.Undo.RegisterCreatedObjectUndo(holder, "10만 세단 배치");
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
var b1 = bnd();
return "PLACED '" + holderName + "' center=" + b1.center.ToString("F2") + " size=" + b1.size.ToString("F2") + " importSize=" + size.ToString("F3") + " lengthIsX=" + lengthIsX + " frontIsMin=" + frontIsMin + " yaw=" + yaw;
