// 세단 프리팹의 모델을 10만 세단으로 바꾼다 — 뿌리(충돌체 둘)와 Model 받침(위치 · 방향 · 크기)은 그대로 두고, 그 아래 모델만 갈아 끼운다
if (UnityEngine.Application.isPlaying) return "SKIPPED: Play mode";
if (UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null) return "SKIPPED: a prefab is open in Prefab Mode";
string prefabPath = "Assets/C/Prefabs/Vehicle/C_Vehicle_Sedan_White.prefab";
var fbx = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/C/Models/Tripo/Sedan/Sedan100k.fbx");
if (fbx == null) return "FBX MISSING";
var sb = new System.Text.StringBuilder();
var rootGo = UnityEditor.PrefabUtility.LoadPrefabContents(prefabPath);
try
{
    var model = rootGo.transform.Find("Model");
    if (model == null) return "NOT CHANGED: no Model child";
    bool already = model.childCount == 1 && UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(model.GetChild(0).gameObject) == fbx;
    if (already) sb.Append("already 100k; ");
    else
    {
        for (int i = model.childCount - 1; i >= 0; i--)
        {
            sb.Append("removed " + model.GetChild(i).name + "; ");
            UnityEngine.Object.DestroyImmediate(model.GetChild(i).gameObject);
        }
        var inst = (UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(fbx, model);
        sb.Append("added " + inst.name + " localPos=" + inst.transform.localPosition.ToString("F4") + " localRot=" + inst.transform.localEulerAngles.ToString("F1") + " localScale=" + inst.transform.localScale.ToString("F3") + "; ");
        UnityEditor.PrefabUtility.SaveAsPrefabAsset(rootGo, prefabPath);
        sb.Append("saved; ");
    }
}
finally
{
    UnityEditor.PrefabUtility.UnloadPrefabContents(rootGo);
}
// 확인 1 — 프리팹 자체(원점에 임시로 꺼내 본다)
var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(prefabPath);
var tmp = (UnityEngine.GameObject)UnityEngine.Object.Instantiate(prefab);
tmp.hideFlags = UnityEngine.HideFlags.HideAndDontSave;
try
{
    UnityEngine.Bounds b = new UnityEngine.Bounds();
    bool first = true;
    foreach (var r in tmp.GetComponentsInChildren<UnityEngine.MeshRenderer>(true))
    {
        if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
        var mf = r.GetComponent<UnityEngine.MeshFilter>();
        sb.Append("| renderer " + r.name + " mesh=" + (mf != null && mf.sharedMesh != null ? mf.sharedMesh.name + "/" + (mf.sharedMesh.GetIndexCount(0) / 3) + " tris" : "none") + " mats=[" + string.Join(",", r.sharedMaterials.Select(m => m == null ? "null" : m.name)) + "] ");
    }
    sb.Append("| prefab bounds min=" + b.min.ToString("F3") + " max=" + b.max.ToString("F3") + " ");
    foreach (var c in tmp.GetComponents<UnityEngine.BoxCollider>()) sb.Append("| box c=" + c.center.ToString("F3") + " s=" + c.size.ToString("F3") + " ");
    // 앞 = +Z 인지: 앞쪽 끝 12 % 의 지붕이 뒤쪽 끝보다 낮아야 한다(보닛이 트렁크보다 낮다)
    var mesh = tmp.GetComponentInChildren<UnityEngine.MeshFilter>();
    var l2w = mesh.transform.localToWorldMatrix;
    var vs = mesh.sharedMesh.vertices;
    float topFront = float.MinValue, topRear = float.MinValue;
    for (int i = 0; i < vs.Length; i += 3)
    {
        var p = l2w.MultiplyPoint3x4(vs[i]);
        float u = (p.z - b.min.z) / b.size.z;
        if (u > 0.88f) topFront = UnityEngine.Mathf.Max(topFront, p.y);
        if (u < 0.12f) topRear = UnityEngine.Mathf.Max(topRear, p.y);
    }
    sb.Append("| top at +Z end=" + topFront.ToString("F3") + " at -Z end=" + topRear.ToString("F3") + " ");
}
finally
{
    UnityEngine.Object.DestroyImmediate(tmp);
}
// 확인 2 — 씬에 놓인 인스턴스
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "C_Vehicles");
if (root != null)
    foreach (UnityEngine.Transform ch in root.transform)
    {
        if (UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(ch.gameObject) != prefab) continue;
        UnityEngine.Bounds b = new UnityEngine.Bounds();
        bool first = true;
        var rs = ch.GetComponentsInChildren<UnityEngine.MeshRenderer>(true);
        foreach (var r in rs) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
        sb.Append("| scene '" + ch.name + "' renderers=" + rs.Length + " centre=" + b.center.ToString("F2") + " size=" + b.size.ToString("F2") + " minY=" + b.min.y.ToString("F3") + " ");
    }
return sb.ToString();
