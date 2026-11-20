// 차량을 제안도의 자리에 놓는다 — V_APPLY 가 false 면 재기만 한다(바닥 높이 · 겹치는 물체 · 최종 좌표). 저장은 하지 않는다
// 길가 자리는 도로 가장자리에 차 옆면(거울 포함)을 맞춘다. 방향은 우측통행 기준(북쪽 · 동쪽 가장자리 = 서 · 북쪽을 본다)
if (UnityEngine.Application.isPlaying) return "NOT DONE: Play mode";
bool apply = V_APPLY;
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (scene.path != "Assets/C/Scenes/C_UrbanBlock.unity") return "NOT DONE: active scene is " + scene.path;
var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "C_Vehicles");
if (root == null) return "NOT DONE: C_Vehicles root not found";
string pdir = "Assets/C/Prefabs/Vehicle/";
// 번호 | 프리팹 | 이름 | x | z | 앞 방향(0 북 · 90 동 · 180 남 · 270 서) | 맞출 변(0 없음 · 1 동쪽 끝 · 2 서쪽 끝 · 3 북쪽 끝 · 4 남쪽 끝) | 그 좌표 | 옮겨 올 기존 물체 이름
var spots = new object[][]
{
    new object[] { 2, "C_Vehicle_Van_White", "승합차 1 · 가운데 도로 N32 앞", 112.0f, 124.0f, 270f, 3, 125.08f, "승합차 1 · 가운데 도로" },
    new object[] { 3, "C_Vehicle_Truck_Blue", "트럭 1 · 가운데 도로 연결로", 131.2f, 143.0f, 0f, 1, 132.22f, "트럭 1 · 가운데 도로" },
    new object[] { 4, "C_Vehicle_Sedan_White", "세단 2 · 공영주차장 남서", 43.8f, 150.0f, 270f, 0, 0f, "" },
    new object[] { 5, "C_Vehicle_Van_White", "승합차 2 · 공영주차장 서쪽", 43.9f, 173.0f, 90f, 0, 0f, "" },
    new object[] { 6, "C_Vehicle_Truck_Blue", "트럭 2 · 공영주차장 북동", 59.6f, 181.0f, 270f, 0, 0f, "" },
    new object[] { 7, "C_Vehicle_Sedan_White", "세단 3 · 고리 도로 남쪽", 93.0f, 160.0f, 270f, 3, 160.97f, "" },
    new object[] { 8, "C_Vehicle_Truck_Blue", "트럭 3 · 고리 도로 동쪽", 118.5f, 181.0f, 0f, 1, 119.52f, "" },
    new object[] { 9, "C_Vehicle_Van_White", "승합차 3 · 고리 도로 동쪽 북", 116.35f, 207.5f, 180f, 2, 115.28f, "" },
    new object[] { 10, "C_Vehicle_Sedan_White", "세단 4 · 고리 도로 서쪽", 65.4f, 203.0f, 0f, 1, 66.47f, "" },
};
System.Func<UnityEngine.GameObject, UnityEngine.Bounds> bnd = g =>
{
    UnityEngine.Bounds b = new UnityEngine.Bounds();
    bool first = true;
    foreach (var r in g.GetComponentsInChildren<UnityEngine.Renderer>(true)) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
    return b;
};
System.Func<UnityEngine.Transform, string> topName = t =>
{
    while (t.parent != null && t.parent != root.transform) t = t.parent;
    return t.parent == root.transform ? t.name : "";
};
UnityEngine.Physics.SyncTransforms();
var sb = new System.Text.StringBuilder();
int done = 0;
foreach (var s in spots)
{
    int id = (int)s[0];
    string prefabName = (string)s[1], label = (string)s[2], oldName = (string)s[8];
    float x = (float)s[3], z = (float)s[4], yaw = (float)s[5], edge = (float)s[7];
    int align = (int)s[6];
    var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(pdir + prefabName + ".prefab");
    if (prefab == null) { sb.Append(id + " PREFAB MISSING\n"); continue; }
    // 임시 복사본으로 자리 재기
    var tmp = (UnityEngine.GameObject)UnityEngine.Object.Instantiate(prefab);
    UnityEngine.Vector3 pos;
    UnityEngine.Bounds b;
    float gMin = 99f, gMax = -99f;
    string hitNames;
    try
    {
        foreach (var t in tmp.GetComponentsInChildren<UnityEngine.Transform>(true)) t.gameObject.hideFlags = UnityEngine.HideFlags.HideAndDontSave;
        foreach (var c in tmp.GetComponentsInChildren<UnityEngine.Collider>(true)) c.enabled = false;
        tmp.transform.rotation = UnityEngine.Quaternion.Euler(0f, yaw, 0f);
        tmp.transform.position = new UnityEngine.Vector3(x, 0f, z);
        b = bnd(tmp);
        var shift = UnityEngine.Vector3.zero;
        if (align == 1) shift.x = edge - b.max.x;
        if (align == 2) shift.x = edge - b.min.x;
        if (align == 3) shift.z = edge - b.max.z;
        if (align == 4) shift.z = edge - b.min.z;
        tmp.transform.position += shift;
        b = bnd(tmp);
        // 바닥 높이 — 가운데와 네 모서리 안쪽
        var pts = new[] { b.center, new UnityEngine.Vector3(b.min.x + 0.3f, 0, b.min.z + 0.3f), new UnityEngine.Vector3(b.max.x - 0.3f, 0, b.min.z + 0.3f), new UnityEngine.Vector3(b.min.x + 0.3f, 0, b.max.z - 0.3f), new UnityEngine.Vector3(b.max.x - 0.3f, 0, b.max.z - 0.3f) };
        foreach (var p in pts)
        {
            float best = -99f;
            foreach (var h in UnityEngine.Physics.RaycastAll(new UnityEngine.Vector3(p.x, 0.6f, p.z), UnityEngine.Vector3.down, 3f))
            {
                if (h.collider.transform.IsChildOf(root.transform)) continue;
                best = UnityEngine.Mathf.Max(best, h.point.y);
            }
            gMin = UnityEngine.Mathf.Min(gMin, best);
            gMax = UnityEngine.Mathf.Max(gMax, best);
        }
        float gy = gMax > -50f ? gMax : 0f;
        tmp.transform.position = new UnityEngine.Vector3(tmp.transform.position.x, gy, tmp.transform.position.z);
        pos = tmp.transform.position;
        b = bnd(tmp);
        // 그 자리에 있는 다른 물체 (바닥 위 0.35 m 부터)
        var half = new UnityEngine.Vector3(b.size.x * 0.5f, (b.size.y - 0.35f) * 0.5f, b.size.z * 0.5f);
        var hits = UnityEngine.Physics.OverlapBox(new UnityEngine.Vector3(b.center.x, gy + 0.35f + half.y, b.center.z), half);
        // 옮겨 올 차 자신의 판정 상자는 뺀다
        hitNames = string.Join(",", hits.Where(c => { string tn = topName(c.transform); return !(tn != "" && (tn == oldName || tn == label)); }).Select(c => c.name + "<" + c.transform.root.name + ">").Distinct().Take(6));
    }
    finally
    {
        UnityEngine.Object.DestroyImmediate(tmp);
    }
    sb.Append(id + " " + label + " | pos=" + pos.ToString("F2") + " yaw=" + yaw + " | x " + b.min.x.ToString("F2") + ".." + b.max.x.ToString("F2") + " z " + b.min.z.ToString("F2") + ".." + b.max.z.ToString("F2") + " | ground " + gMin.ToString("F3") + ".." + gMax.ToString("F3") + " | overlaps=[" + hitNames + "]");
    if (apply)
    {
        var tr = root.transform.Find(label);
        if (tr == null && oldName != "") tr = root.transform.Find(oldName);
        UnityEngine.GameObject go;
        if (tr != null)
        {
            go = tr.gameObject;
            UnityEditor.Undo.RecordObject(go, "차량 배치");
            UnityEditor.Undo.RecordObject(go.transform, "차량 배치");
            sb.Append(" | moved");
        }
        else
        {
            go = (UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, root.transform);
            UnityEditor.Undo.RegisterCreatedObjectUndo(go, "차량 배치");
            sb.Append(" | created");
        }
        go.name = label;
        go.transform.rotation = UnityEngine.Quaternion.Euler(0f, yaw, 0f);
        go.transform.position = pos;
        done++;
    }
    sb.Append("\n");
}
if (apply) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
var names = new System.Collections.Generic.List<string>();
foreach (UnityEngine.Transform ch in root.transform) names.Add(ch.name);
sb.Append("applied=" + done + " | C_Vehicles(" + names.Count + "): " + string.Join(" / ", names));
return sb.ToString();
