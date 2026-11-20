// 놓인 차량 확인 — 위에서 곧게 내려다본 사진(북쪽이 위). 임시 카메라만 만들었다 지운다
// 1) spots_top.png: 자리 1 ~ 10 을 한 칸씩(18.7 x 14 m)  2) spots_map.png: 구역 전체  3) spots_street.png: 좁은 고리 도로 세 자리를 길에서
string outDir = "C:/Users/leesn/AppData/Local/Temp/claude/28213761c82ff9e9/fd0f780c-958c-467e-a45e-15486246a889/scratchpad/tripo/shots_spots/";
System.IO.Directory.CreateDirectory(outDir);
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "C_Vehicles");
if (root == null) return "C_Vehicles not found";
string[] order = { "세단 1", "승합차 1", "트럭 1", "세단 2", "승합차 2", "트럭 2", "세단 3", "트럭 3", "승합차 3", "세단 4" };
var centres = new System.Collections.Generic.List<UnityEngine.Vector3>();
foreach (var key in order)
{
    UnityEngine.Transform found = null;
    foreach (UnityEngine.Transform ch in root.transform) if (ch.name.StartsWith(key + " ")) { found = ch; break; }
    if (found == null) return "MISSING " + key;
    centres.Add(found.position);
}
UnityEngine.GameObject camGo = null;
UnityEngine.RenderTexture rt = null;
UnityEngine.Texture2D sheet = null;
bool asyncBefore = UnityEditor.ShaderUtil.allowAsyncCompilation;
try
{
    UnityEditor.ShaderUtil.allowAsyncCompilation = false;
    camGo = new UnityEngine.GameObject("tmp_shot_cam");
    camGo.hideFlags = UnityEngine.HideFlags.HideAndDontSave;
    var cam = camGo.AddComponent<UnityEngine.Camera>();
    cam.allowHDR = true;
    cam.nearClipPlane = 0.1f;
    cam.farClipPlane = 200f;
    var down = UnityEngine.Quaternion.LookRotation(UnityEngine.Vector3.down, UnityEngine.Vector3.forward);
    // 1) 자리별
    int tw = 400, th = 300;
    rt = new UnityEngine.RenderTexture(tw, th, 24, UnityEngine.RenderTextureFormat.ARGB32, UnityEngine.RenderTextureReadWrite.sRGB);
    rt.antiAliasing = 4;
    cam.targetTexture = rt;
    cam.orthographic = true;
    cam.orthographicSize = 7f;
    sheet = new UnityEngine.Texture2D(tw * 5, th * 2, UnityEngine.TextureFormat.RGB24, false);
    for (int i = 0; i < 10; i++)
    {
        cam.transform.position = new UnityEngine.Vector3(centres[i].x, 64f, centres[i].z);
        cam.transform.rotation = down;
        cam.Render();
        var prev = UnityEngine.RenderTexture.active;
        UnityEngine.RenderTexture.active = rt;
        sheet.ReadPixels(new UnityEngine.Rect(0, 0, tw, th), (i % 5) * tw, (1 - i / 5) * th);
        UnityEngine.RenderTexture.active = prev;
    }
    sheet.Apply();
    System.IO.File.WriteAllBytes(outDir + "spots_top.png", UnityEngine.ImageConversion.EncodeToPNG(sheet));
    cam.targetTexture = null;
    UnityEngine.Object.DestroyImmediate(sheet);
    rt.Release();
    UnityEngine.Object.DestroyImmediate(rt);
    // 2) 구역 전체 (x 35 ~ 140, z 111 ~ 216)
    int mw = 1260;
    rt = new UnityEngine.RenderTexture(mw, mw, 24, UnityEngine.RenderTextureFormat.ARGB32, UnityEngine.RenderTextureReadWrite.sRGB);
    rt.antiAliasing = 4;
    cam.targetTexture = rt;
    cam.orthographicSize = 52.5f;
    cam.transform.position = new UnityEngine.Vector3(87.5f, 64f, 163.5f);
    cam.transform.rotation = down;
    cam.Render();
    sheet = new UnityEngine.Texture2D(mw, mw, UnityEngine.TextureFormat.RGB24, false);
    var prev2 = UnityEngine.RenderTexture.active;
    UnityEngine.RenderTexture.active = rt;
    sheet.ReadPixels(new UnityEngine.Rect(0, 0, mw, mw), 0, 0);
    UnityEngine.RenderTexture.active = prev2;
    sheet.Apply();
    System.IO.File.WriteAllBytes(outDir + "spots_map.png", UnityEngine.ImageConversion.EncodeToPNG(sheet));
    cam.targetTexture = null;
    UnityEngine.Object.DestroyImmediate(sheet);
    rt.Release();
    UnityEngine.Object.DestroyImmediate(rt);
    // 3) 길에서 본 고리 도로 세 자리 (트럭 3 · 승합차 3 · 세단 4) — 도로 가운데 선 위, 남쪽 9 m 에서
    tw = 640; th = 440;
    rt = new UnityEngine.RenderTexture(tw, th, 24, UnityEngine.RenderTextureFormat.ARGB32, UnityEngine.RenderTextureReadWrite.sRGB);
    rt.antiAliasing = 4;
    cam.targetTexture = rt;
    cam.orthographic = false;
    cam.fieldOfView = 46f;
    sheet = new UnityEngine.Texture2D(tw * 3, th, UnityEngine.TextureFormat.RGB24, false);
    int[] idx = { 7, 8, 9 };
    float[] roadX = { 117.4f, 117.4f, 64.5f };
    for (int k = 0; k < 3; k++)
    {
        var c = centres[idx[k]];
        var pos = new UnityEngine.Vector3(roadX[k], 1.6f, c.z - 9.5f);
        cam.transform.position = pos;
        cam.transform.rotation = UnityEngine.Quaternion.LookRotation(new UnityEngine.Vector3(roadX[k], 1.0f, c.z) - pos, UnityEngine.Vector3.up);
        cam.Render();
        var prev = UnityEngine.RenderTexture.active;
        UnityEngine.RenderTexture.active = rt;
        sheet.ReadPixels(new UnityEngine.Rect(0, 0, tw, th), k * tw, 0);
        UnityEngine.RenderTexture.active = prev;
    }
    sheet.Apply();
    System.IO.File.WriteAllBytes(outDir + "spots_street.png", UnityEngine.ImageConversion.EncodeToPNG(sheet));
    cam.targetTexture = null;
}
finally
{
    UnityEditor.ShaderUtil.allowAsyncCompilation = asyncBefore;
    if (sheet != null) UnityEngine.Object.DestroyImmediate(sheet);
    if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
    if (camGo != null) UnityEngine.Object.DestroyImmediate(camGo);
}
return "shots_spots done: " + string.Join(" ", centres.Select(c => "(" + c.x.ToString("F1") + "," + c.z.ToString("F1") + ")"));
