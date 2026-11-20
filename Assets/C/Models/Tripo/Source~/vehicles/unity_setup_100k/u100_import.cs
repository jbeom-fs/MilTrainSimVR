// 10만 세단을 새 자산으로 들여온다 — 기존 Sedan.fbx 와 그 텍스처는 건드리지 않는다
if (UnityEngine.Application.isPlaying) return "SKIPPED: Play mode";
string src = "C:/Users/leesn/AppData/Local/Temp/claude/28213761c82ff9e9/fd0f780c-958c-467e-a45e-15486246a889/scratchpad/tripo/sedan_100k/";
string dir = "Assets/C/Models/Tripo/Sedan/";
string full = System.IO.Path.GetFullPath(dir) + "/";
string[] files = { "Sedan100k.fbx", "Sedan100k_basecolor.jpg", "Sedan100k_normal.png", "Sedan100k_metallicSmoothness.png" };
foreach (var f in files) if (!System.IO.File.Exists(src + f)) return "MISSING " + f;
foreach (var f in files) System.IO.File.Copy(src + f, full + f, true);
UnityEditor.AssetDatabase.Refresh();
var sb = new System.Text.StringBuilder();
System.Action<string, UnityEditor.TextureImporterType, bool, int> setup = (file, type, srgb, max) =>
{
    var ti = UnityEditor.AssetImporter.GetAtPath(dir + file) as UnityEditor.TextureImporter;
    if (ti == null) { sb.Append(file + "=MISSING "); return; }
    ti.textureType = type;
    if (type == UnityEditor.TextureImporterType.Default) ti.sRGBTexture = srgb;
    ti.alphaIsTransparency = false;
    ti.maxTextureSize = max;
    ti.mipmapEnabled = true;
    ti.anisoLevel = 8;
    ti.SaveAndReimport();
    sb.Append(file + "=ok ");
};
setup("Sedan100k_basecolor.jpg", UnityEditor.TextureImporterType.Default, true, 4096);
setup("Sedan100k_normal.png", UnityEditor.TextureImporterType.NormalMap, false, 4096);
setup("Sedan100k_metallicSmoothness.png", UnityEditor.TextureImporterType.Default, false, 2048);
return sb.ToString();
