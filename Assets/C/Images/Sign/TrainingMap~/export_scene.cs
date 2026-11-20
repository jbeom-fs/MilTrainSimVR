// 안내판 지도 — 씬에서 맵 배치(건물 · 땅 · 지하도 · 점령지 · 시작 자리)를 map_scene.json 으로 내보낸다.
// C_UrbanBlock 을 연 에디터에서 Unity CLI 로 돌린다 (씬은 바꾸지 않는다):
//   unity command eval_file <이 파일의 절대 경로> --no-banner --result-only
// 그다음 make_map.py 로 PNG 를 다시 만든다. 경로 · 표시(차량 · 보급 등)는 map_layers.json 에서 고친다.
var inv = System.Globalization.CultureInfo.InvariantCulture;
System.Func<float, string> F = v => v.ToString("0.###", inv);
System.Func<string, string> Q = s => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
System.Func<Transform, string> Box = t =>
    "\"x\": " + F(t.position.x) + ", \"z\": " + F(t.position.z) + ", \"yaw\": " + F(Mathf.Round(t.eulerAngles.y * 1000f) / 1000f % 360f) +
    ", \"sx\": " + F(Mathf.Abs(t.lossyScale.x)) + ", \"sz\": " + F(Mathf.Abs(t.lossyScale.z));
var sb = new System.Text.StringBuilder();
sb.Append("{\n  \"scene\": " + Q(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path) + ",\n");

sb.Append("  \"ground\": [\n");
var list = new System.Collections.Generic.List<string>();
foreach (var g in UnityEngine.Object.FindObjectsByType<MilTrainSimVR.UrbanCombat.GreyboxGroundSpec>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID))
    list.Add("    {\"name\": " + Q(g.name) + ", \"kind\": " + Q(g.kind.ToString()) + ", " + Box(g.transform) + "}");
sb.Append(string.Join(",\n", list) + "\n  ],\n");

sb.Append("  \"buildings\": [\n");
list.Clear();
foreach (var b in UnityEngine.Object.FindObjectsByType<MilTrainSimVR.UrbanCombat.GreyboxBuildingSpec>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID))
    list.Add("    {\"name\": " + Q(b.name) + ", " + Box(b.transform) + ", \"floors\": " + b.floors + ", \"enterable\": " + b.enterable + ", \"departmentStore\": " + (b.departmentStore ? "true" : "false") + "}");
sb.Append(string.Join(",\n", list) + "\n  ],\n");

sb.Append("  \"underground\": [\n");
list.Clear();
foreach (var u in UnityEngine.Object.FindObjectsByType<MilTrainSimVR.UrbanCombat.GreyboxUndergroundSpec>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID))
    list.Add("    {\"name\": " + Q(u.name) + ", \"kind\": " + Q(u.kind.ToString()) + ", " + Box(u.transform) + "}");
sb.Append(string.Join(",\n", list) + "\n  ],\n");

sb.Append("  \"zones\": [\n");
list.Clear();
foreach (var z in UnityEngine.Object.FindObjectsByType<MilTrainSimVR.UrbanCombat.CaptureZone>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID))
{
    Vector3 size = new SerializedObject(z).FindProperty("m_Size").vector3Value;
    var t = z.transform;
    list.Add("    {\"name\": " + Q(z.DisplayName) + ", \"x\": " + F(t.position.x) + ", \"z\": " + F(t.position.z) + ", \"yaw\": " + F(t.eulerAngles.y) + ", \"sx\": " + F(size.x) + ", \"sz\": " + F(size.z) + "}");
}
sb.Append(string.Join(",\n", list) + "\n  ],\n");

var xr = GameObject.Find("C_XR Origin");
Vector3 start = xr != null ? xr.transform.position : Vector3.zero;
sb.Append("  \"start\": {\"x\": " + F(start.x) + ", \"z\": " + F(start.z) + "}\n}\n");

string path = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Assets/C/Images/Sign/TrainingMap~/map_scene.json");
System.IO.File.WriteAllText(path, sb.ToString(), new System.Text.UTF8Encoding(false));
return path;
