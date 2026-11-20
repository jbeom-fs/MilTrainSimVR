// 차량 모델을 새 자산으로 들여온다 — FBX 와 텍스처 셋을 복사하고 텍스처 가져오기 설정을 맞춘다
if (UnityEngine.Application.isPlaying) return "SKIPPED: Play mode";
string name = "V_NAME", folder = "V_FOLDER";
string src = "V_SRC";
if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/C/Models/Tripo/" + folder)) UnityEditor.AssetDatabase.CreateFolder("Assets/C/Models/Tripo", folder);
string dir = "Assets/C/Models/Tripo/" + folder + "/";
string full = System.IO.Path.GetFullPath(dir) + "/";
string[] files = { name + ".fbx", name + "_basecolor.jpg", name + "_normal.png", name + "_metallicSmoothness.png" };
foreach (var f in files) if (!System.IO.File.Exists(src + f)) return "MISSING " + f;
foreach (var f in files) System.IO.File.Copy(src + f, full + f, true);
UnityEditor.AssetDatabase.Refresh();
var sb = new System.Text.StringBuilder();
System.Action<string, UnityEditor.TextureImporterType, bool, int> setup = (file, type, srgb, max) =>
{
    var ti = UnityEditor.AssetImporter.GetAtPath(dir + file) as UnityEditor.TextureImporter;
    if (ti == null) { sb.Append(file + "=MISSING "); return; }
    bool same = ti.textureType == type && ti.maxTextureSize == max && ti.anisoLevel == 8 && (type != UnityEditor.TextureImporterType.Default || ti.sRGBTexture == srgb);
    if (same) { sb.Append(file + "=kept "); return; }
    ti.textureType = type;
    if (type == UnityEditor.TextureImporterType.Default) ti.sRGBTexture = srgb;
    ti.alphaIsTransparency = false;
    ti.maxTextureSize = max;
    ti.mipmapEnabled = true;
    ti.anisoLevel = 8;
    ti.SaveAndReimport();
    sb.Append(file + "=ok ");
};
setup(name + "_basecolor.jpg", UnityEditor.TextureImporterType.Default, true, 4096);
setup(name + "_normal.png", UnityEditor.TextureImporterType.NormalMap, false, 4096);
setup(name + "_metallicSmoothness.png", UnityEditor.TextureImporterType.Default, false, 2048);
return sb.ToString();
