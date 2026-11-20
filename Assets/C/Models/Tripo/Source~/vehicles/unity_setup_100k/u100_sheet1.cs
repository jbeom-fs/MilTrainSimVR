// 10만 세단을 아홉 방향에서 — 임시 복사본(저장 안 됨)을 돌려 가며 남쪽 길 위 카메라로 찍는다. 씬은 바꾸지 않는다
if (UnityEngine.Application.isPlaying) return "SKIPPED: Play mode";
string outPng = "C:/Users/leesn/AppData/Local/Temp/claude/28213761c82ff9e9/fd0f780c-958c-467e-a45e-15486246a889/scratchpad/tripo/shots100k/around.png";
var fbx = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/C/Models/Tripo/Sedan/Sedan100k.fbx");
if (fbx == null) return "NOT IMPORTED";
var target = new UnityEngine.Vector3(75f, 0f, 124.1f);
int tw = 560, th = 420;
UnityEngine.GameObject holder = null, camGo = null;
UnityEngine.RenderTexture rt = null;
UnityEngine.Texture2D sheet = null;
bool asyncBefore = UnityEditor.ShaderUtil.allowAsyncCompilation;
string info = "";
try
{
    UnityEditor.ShaderUtil.allowAsyncCompilation = false;
    holder = new UnityEngine.GameObject("tmp_100k_holder");
    UnityEngine.Object.Instantiate(fbx, holder.transform);
    foreach (var t in holder.GetComponentsInChildren<UnityEngine.Transform>(true)) t.gameObject.hideFlags = UnityEngine.HideFlags.HideAndDontSave;
    holder.transform.localScale = new UnityEngine.Vector3(4.35f, 4.12f, 4.2f);
    System.Func<UnityEngine.Bounds> bnd = () =>
    {
        UnityEngine.Bounds b = new UnityEngine.Bounds();
        bool first = true;
        foreach (var r in holder.GetComponentsInChildren<UnityEngine.Renderer>(true)) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
        return b;
    };
    System.Action<float> pose = yaw =>
    {
        holder.transform.rotation = UnityEngine.Quaternion.Euler(0f, yaw, 0f);
        var b = bnd();
        holder.transform.position += new UnityEngine.Vector3(target.x - b.center.x, target.y - b.min.y, target.z - b.center.z);
    };
    pose(0f);
    info = "size=" + bnd().size.ToString("F2");
    camGo = new UnityEngine.GameObject("tmp_shot_cam");
    camGo.hideFlags = UnityEngine.HideFlags.HideAndDontSave;
    var cam = camGo.AddComponent<UnityEngine.Camera>();
    cam.nearClipPlane = 0.1f;
    cam.farClipPlane = 600f;
    cam.allowHDR = true;
    cam.fieldOfView = 40f;
    rt = new UnityEngine.RenderTexture(tw, th, 24, UnityEngine.RenderTextureFormat.ARGB32, UnityEngine.RenderTextureReadWrite.sRGB);
    rt.antiAliasing = 4;
    cam.targetTexture = rt;
    sheet = new UnityEngine.Texture2D(tw * 3, th * 3, UnityEngine.TextureFormat.RGB24, false);
    // 윗줄: 앞-왼쪽 · 앞 · 앞-오른쪽 / 가운데: 왼쪽 · 위 · 오른쪽 / 아랫줄: 뒤-왼쪽 · 뒤 · 뒤-오른쪽
    float[] yaws = { 315f, 270f, 225f, 0f, -1f, 180f, 45f, 90f, 135f };
    for (int i = 0; i < 9; i++)
    {
        if (yaws[i] < 0f)
        {
            pose(0f);
            cam.transform.position = target + new UnityEngine.Vector3(0f, 7.0f, 0f);
            cam.transform.rotation = UnityEngine.Quaternion.LookRotation(UnityEngine.Vector3.down, UnityEngine.Vector3.forward);
        }
        else
        {
            pose(yaws[i]);
            var pos = target + new UnityEngine.Vector3(0f, 1.35f, -6.2f);
            cam.transform.position = pos;
            cam.transform.rotation = UnityEngine.Quaternion.LookRotation(target + new UnityEngine.Vector3(0f, 0.7f, 0f) - pos, UnityEngine.Vector3.up);
        }
        cam.Render();
        var prev = UnityEngine.RenderTexture.active;
        UnityEngine.RenderTexture.active = rt;
        sheet.ReadPixels(new UnityEngine.Rect(0, 0, tw, th), (i % 3) * tw, (2 - i / 3) * th);
        UnityEngine.RenderTexture.active = prev;
    }
    sheet.Apply();
    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outPng));
    System.IO.File.WriteAllBytes(outPng, UnityEngine.ImageConversion.EncodeToPNG(sheet));
    cam.targetTexture = null;
}
finally
{
    UnityEditor.ShaderUtil.allowAsyncCompilation = asyncBefore;
    if (sheet != null) UnityEngine.Object.DestroyImmediate(sheet);
    if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
    if (camGo != null) UnityEngine.Object.DestroyImmediate(camGo);
    if (holder != null) UnityEngine.Object.DestroyImmediate(holder);
}
return "around.png " + info;
