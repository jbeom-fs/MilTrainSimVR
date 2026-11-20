// 같은 자리 · 같은 카메라에서 원본과 10만을 번갈아 찍는다 (임시 복사본만 쓰고 씬은 바꾸지 않는다)
// 윗줄: 빛 받은 모습(원본 | 10만), 아랫줄: 색 텍스처만(원본 | 10만)
if (UnityEngine.Application.isPlaying) return "SKIPPED: Play mode";
string outPng = "C:/Users/leesn/AppData/Local/Temp/claude/28213761c82ff9e9/fd0f780c-958c-467e-a45e-15486246a889/scratchpad/tripo/shots100k/AB_NAME.png";
string dirN = "Assets/C/Models/Tripo/Sedan/";
string dirO = "Assets/C/Models/Tripo/Bridge/white_sedan_3d_model/";
var fbxN = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(dirN + "Sedan100k.fbx");
var fbxO = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(dirO + "white_sedan_3d_model.fbx");
var litN = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(dirN + "Sedan100k.mat");
var litO = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(dirO + "white_sedan_original.mat");
var baseN = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(dirN + "Sedan100k_basecolor.jpg");
var baseO = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(dirO + "tripo_model_basecolor.jpg");
if (fbxN == null || fbxO == null || litN == null || litO == null || baseN == null || baseO == null) return "ASSET MISSING";
var target = new UnityEngine.Vector3(75f, 0f, 124.1f);
var camPos = new UnityEngine.Vector3(CAM_POS);
var camLook = new UnityEngine.Vector3(CAM_LOOK);
int tw = 800, th = 600;
var holders = new UnityEngine.GameObject[2];
UnityEngine.GameObject camGo = null;
UnityEngine.RenderTexture rt = null;
UnityEngine.Texture2D sheet = null;
var unlit = new UnityEngine.Material[2];
bool asyncBefore = UnityEditor.ShaderUtil.allowAsyncCompilation;
try
{
    UnityEditor.ShaderUtil.allowAsyncCompilation = false;
    var fbx = new[] { fbxO, fbxN };
    var lit = new[] { litO, litN };
    var bases = new[] { baseO, baseN };
    for (int k = 0; k < 2; k++)
    {
        unlit[k] = new UnityEngine.Material(UnityEngine.Shader.Find("Universal Render Pipeline/Unlit"));
        unlit[k].hideFlags = UnityEngine.HideFlags.HideAndDontSave;
        unlit[k].SetTexture("_BaseMap", bases[k]);
        holders[k] = new UnityEngine.GameObject("tmp_ab_holder" + k);
        UnityEngine.Object.Instantiate(fbx[k], holders[k].transform);
        foreach (var t in holders[k].GetComponentsInChildren<UnityEngine.Transform>(true)) t.gameObject.hideFlags = UnityEngine.HideFlags.HideAndDontSave;
        holders[k].transform.localScale = new UnityEngine.Vector3(4.35f, 4.12f, 4.2f);
        holders[k].transform.rotation = UnityEngine.Quaternion.Euler(0f, YAW, 0f);
        UnityEngine.Bounds b = new UnityEngine.Bounds();
        bool first = true;
        foreach (var r in holders[k].GetComponentsInChildren<UnityEngine.Renderer>(true)) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
        holders[k].transform.position += new UnityEngine.Vector3(target.x - b.center.x, target.y - b.min.y, target.z - b.center.z);
        holders[k].SetActive(false);
    }
    camGo = new UnityEngine.GameObject("tmp_shot_cam");
    camGo.hideFlags = UnityEngine.HideFlags.HideAndDontSave;
    var cam = camGo.AddComponent<UnityEngine.Camera>();
    cam.nearClipPlane = 0.05f;
    cam.farClipPlane = 600f;
    cam.allowHDR = true;
    cam.fieldOfView = FOV;
    rt = new UnityEngine.RenderTexture(tw, th, 24, UnityEngine.RenderTextureFormat.ARGB32, UnityEngine.RenderTextureReadWrite.sRGB);
    rt.antiAliasing = 4;
    cam.targetTexture = rt;
    cam.transform.position = camPos;
    cam.transform.rotation = UnityEngine.Quaternion.LookRotation(camLook - camPos, UnityEngine.Vector3.up);
    sheet = new UnityEngine.Texture2D(tw * 2, th * 2, UnityEngine.TextureFormat.RGB24, false);
    for (int row = 0; row < 2; row++)
        for (int k = 0; k < 2; k++)
        {
            holders[0].SetActive(k == 0);
            holders[1].SetActive(k == 1);
            foreach (var r in holders[k].GetComponentsInChildren<UnityEngine.Renderer>(true)) r.sharedMaterial = row == 0 ? lit[k] : unlit[k];
            cam.Render();
            var prev = UnityEngine.RenderTexture.active;
            UnityEngine.RenderTexture.active = rt;
            sheet.ReadPixels(new UnityEngine.Rect(0, 0, tw, th), k * tw, (1 - row) * th);
            UnityEngine.RenderTexture.active = prev;
        }
    sheet.Apply();
    System.IO.File.WriteAllBytes(outPng, UnityEngine.ImageConversion.EncodeToPNG(sheet));
    cam.targetTexture = null;
}
finally
{
    UnityEditor.ShaderUtil.allowAsyncCompilation = asyncBefore;
    if (sheet != null) UnityEngine.Object.DestroyImmediate(sheet);
    if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
    if (camGo != null) UnityEngine.Object.DestroyImmediate(camGo);
    for (int k = 0; k < 2; k++) { if (holders[k] != null) UnityEngine.Object.DestroyImmediate(holders[k]); if (unlit[k] != null) UnityEngine.Object.DestroyImmediate(unlit[k]); }
}
return "AB_NAME.png";
