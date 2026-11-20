// 차량 프리팹을 가운데 도로 북쪽 갓길 줄에 놓는다 — 세단과 같은 방향(앞 = 서쪽), 북쪽 옆면을 세단과 같은 선(z 125.08)에 맞춘다. 저장은 하지 않는다
if (UnityEngine.Application.isPlaying) return "NOT PLACED: Play mode";
var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/C/Prefabs/Vehicle/V_PREFAB.prefab");
if (prefab == null) return "PREFAB MISSING";
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (scene.path != "Assets/C/Scenes/C_UrbanBlock.unity") return "NOT PLACED: active scene is " + scene.path;
var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "C_Vehicles");
if (root == null) return "NOT PLACED: C_Vehicles root not found";
string label = "V_LABEL";
float x = V_Xf;
bool dryRun = V_DRY;
// 그 자리에 다른 것이 있는지 (바닥 위 0.3 ~ 1.7 m)
UnityEngine.Physics.SyncTransforms();
var hits = UnityEngine.Physics.OverlapBox(new UnityEngine.Vector3(x, 1.0f, 124.0f), new UnityEngine.Vector3(2.75f, 0.7f, 1.1f));
string hitNames = string.Join(",", hits.Select(c => c.name + "<" + c.transform.root.name + ">").Distinct().Take(8));
if (dryRun) return "spot x=" + x + " overlaps=[" + hitNames + "]";
var old = root.transform.Find(label);
if (old != null) UnityEditor.Undo.DestroyObjectImmediate(old.gameObject);
var inst = (UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, root.transform);
inst.name = label;
inst.transform.rotation = UnityEngine.Quaternion.Euler(0f, 270f, 0f);
inst.transform.position = new UnityEngine.Vector3(x, 0f, 124.1f);
System.Func<UnityEngine.Bounds> bnd = () =>
{
    UnityEngine.Bounds b = new UnityEngine.Bounds();
    bool first = true;
    foreach (var r in inst.GetComponentsInChildren<UnityEngine.Renderer>(true)) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
    return b;
};
var b0 = bnd();
inst.transform.position += new UnityEngine.Vector3(0f, 0f, 125.08f - b0.max.z);
UnityEditor.Undo.RegisterCreatedObjectUndo(inst, "차량 배치");
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
var b1 = bnd();
return "PLACED '" + label + "' pos=" + inst.transform.position.ToString("F2") + " bounds centre=" + b1.center.ToString("F2") + " size=" + b1.size.ToString("F2") + " minY=" + b1.min.y.ToString("F3") + " | overlaps before=[" + hitNames + "]";
