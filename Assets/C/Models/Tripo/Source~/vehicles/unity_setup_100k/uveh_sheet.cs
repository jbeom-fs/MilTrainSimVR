// 차량 프리팹을 아홉 방향 + 가까이 세 장으로 찍는다 — 임시 복사본(저장 안 됨)을 돌려 가며 남쪽 길 위 카메라로. 씬은 바꾸지 않는다
// around: 앞-왼 · 앞 · 앞-오 / 왼 · 위 · 오 / 뒤-왼 · 뒤 · 뒤-오      close: 왼쪽 옆 · 앞 모서리 · 뒤 모서리
if (UnityEngine.Application.isPlaying) return "SKIPPED: Play mode";
string outDir = "C:/Users/leesn/AppData/Local/Temp/claude/28213761c82ff9e9/fd0f780c-958c-467e-a45e-15486246a889/scratchpad/tripo/shots_V_TAG/";
var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/C/Prefabs/Vehicle/V_PREFAB.prefab");
if (prefab == null) return "PREFAB MISSING";
var target = new UnityEngine.Vector3(V_Xf, 0f, 123.95f);
bool unlit = V_UNLIT;
UnityEngine.GameObject tmp = null, camGo = null;
UnityEngine.RenderTexture rt = null;
UnityEngine.Texture2D sheet = null;
UnityEngine.Material um = null;
bool asyncBefore = UnityEditor.ShaderUtil.allowAsyncCompilation;
string info = "";
try
{
    UnityEditor.ShaderUtil.allowAsyncCompilation = false;
    System.IO.Directory.CreateDirectory(outDir);
    tmp = (UnityEngine.GameObject)UnityEngine.Object.Instantiate(prefab);
    foreach (var t in tmp.GetComponentsInChildren<UnityEngine.Transform>(true)) t.gameObject.hideFlags = UnityEngine.HideFlags.HideAndDontSave;
    foreach (var c in tmp.GetComponentsInChildren<UnityEngine.Collider>(true)) c.enabled = false;
    tmp.transform.position = target;
    tmp.transform.rotation = UnityEngine.Quaternion.Euler(0f, 270f, 0f);
    UnityEngine.Bounds b = new UnityEngine.Bounds();
    bool first = true;
    var rends = tmp.GetComponentsInChildren<UnityEngine.Renderer>(true);
    foreach (var r in rends) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
    float L = b.size.x;
    info = "size=" + b.size.ToString("F2") + " minY=" + b.min.y.ToString("F3");
    if (unlit)
    {
        um = new UnityEngine.Material(UnityEngine.Shader.Find("Universal Render Pipeline/Unlit"));
        um.hideFlags = UnityEngine.HideFlags.HideAndDontSave;
        um.SetTexture("_BaseMap", rends[0].sharedMaterial.GetTexture("_BaseMap"));
        foreach (var r in rends) r.sharedMaterial = um;
    }
    camGo = new UnityEngine.GameObject("tmp_shot_cam");
    camGo.hideFlags = UnityEngine.HideFlags.HideAndDontSave;
    var cam = camGo.AddComponent<UnityEngine.Camera>();
    cam.nearClipPlane = 0.05f;
    cam.farClipPlane = 600f;
    cam.allowHDR = true;
    cam.fieldOfView = 40f;
    System.Action<UnityEngine.Vector3, UnityEngine.Vector3, UnityEngine.Vector3, int, int, int, int> shot = (pos, look, up, x, y, w, h) =>
    {
        cam.transform.position = pos;
        cam.transform.rotation = UnityEngine.Quaternion.LookRotation(look - pos, up);
        cam.Render();
        var prev = UnityEngine.RenderTexture.active;
        UnityEngine.RenderTexture.active = rt;
        sheet.ReadPixels(new UnityEngine.Rect(0, 0, w, h), x, y);
        UnityEngine.RenderTexture.active = prev;
    };
    // 1) 아홉 방향
    int tw = 560, th = 420;
    rt = new UnityEngine.RenderTexture(tw, th, 24, UnityEngine.RenderTextureFormat.ARGB32, UnityEngine.RenderTextureReadWrite.sRGB);
    rt.antiAliasing = 4;
    cam.targetTexture = rt;
    sheet = new UnityEngine.Texture2D(tw * 3, th * 3, UnityEngine.TextureFormat.RGB24, false);
    float[] yaws = { 225f, 180f, 135f, 270f, -1f, 90f, 315f, 0f, 45f };
    for (int i = 0; i < 9; i++)
    {
        if (yaws[i] < 0f)
        {
            tmp.transform.rotation = UnityEngine.Quaternion.Euler(0f, 270f, 0f);
            shot(target + new UnityEngine.Vector3(0f, 9.0f, 0f), target, UnityEngine.Vector3.forward, (i % 3) * tw, (2 - i / 3) * th, tw, th);
        }
        else
        {
            tmp.transform.rotation = UnityEngine.Quaternion.Euler(0f, yaws[i], 0f);
            shot(target + new UnityEngine.Vector3(0f, 1.5f, -7.6f), target + new UnityEngine.Vector3(0f, 0.85f, 0f), UnityEngine.Vector3.up, (i % 3) * tw, (2 - i / 3) * th, tw, th);
        }
    }
    sheet.Apply();
    System.IO.File.WriteAllBytes(outDir + "around" + (unlit ? "_unlit" : "") + ".png", UnityEngine.ImageConversion.EncodeToPNG(sheet));
    cam.targetTexture = null;
    UnityEngine.Object.DestroyImmediate(sheet);
    rt.Release();
    UnityEngine.Object.DestroyImmediate(rt);
    // 2) 가까이 세 장 (앞 = 서쪽)
    tw = 640; th = 440;
    rt = new UnityEngine.RenderTexture(tw, th, 24, UnityEngine.RenderTextureFormat.ARGB32, UnityEngine.RenderTextureReadWrite.sRGB);
    rt.antiAliasing = 4;
    cam.targetTexture = rt;
    sheet = new UnityEngine.Texture2D(tw * 3, th, UnityEngine.TextureFormat.RGB24, false);
    tmp.transform.rotation = UnityEngine.Quaternion.Euler(0f, 270f, 0f);
    float hw = b.size.z * 0.5f;
    shot(target + new UnityEngine.Vector3(-0.4f, 1.45f, -hw - 2.5f), target + new UnityEngine.Vector3(-0.4f, 1.15f, -hw), UnityEngine.Vector3.up, 0, 0, tw, th);
    shot(target + new UnityEngine.Vector3(-L * 0.5f - 1.3f, 1.3f, -hw - 2.0f), target + new UnityEngine.Vector3(-L * 0.5f + 0.9f, 0.9f, -0.2f), UnityEngine.Vector3.up, tw, 0, tw, th);
    shot(target + new UnityEngine.Vector3(L * 0.5f + 1.3f, 1.3f, -hw - 2.0f), target + new UnityEngine.Vector3(L * 0.5f - 0.9f, 0.9f, -0.2f), UnityEngine.Vector3.up, tw * 2, 0, tw, th);
    sheet.Apply();
    System.IO.File.WriteAllBytes(outDir + "close" + (unlit ? "_unlit" : "") + ".png", UnityEngine.ImageConversion.EncodeToPNG(sheet));
    cam.targetTexture = null;
}
finally
{
    UnityEditor.ShaderUtil.allowAsyncCompilation = asyncBefore;
    if (sheet != null) UnityEngine.Object.DestroyImmediate(sheet);
    if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
    if (camGo != null) UnityEngine.Object.DestroyImmediate(camGo);
    if (tmp != null) UnityEngine.Object.DestroyImmediate(tmp);
    if (um != null) UnityEngine.Object.DestroyImmediate(um);
}
return "shots_V_TAG " + info;
