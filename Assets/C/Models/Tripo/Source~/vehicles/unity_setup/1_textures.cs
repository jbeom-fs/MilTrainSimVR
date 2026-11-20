// 새 텍스처 가져오기 설정 — 색(알파 = 유리 아닌 곳) · 클리어코트(선형) · 유리(알파 = 유리 x 짙기)
if (UnityEngine.Application.isPlaying) return "SKIPPED: Play mode";
string dir = "Assets/C/Models/Tripo/Sedan/";
UnityEditor.AssetDatabase.Refresh();
var sb = new System.Text.StringBuilder();
System.Action<string, bool, bool, float> setup = (file, srgb, hasAlpha, alphaRef) =>
{
    var ti = UnityEditor.AssetImporter.GetAtPath(dir + file) as UnityEditor.TextureImporter;
    if (ti == null) { sb.Append(file + "=MISSING "); return; }
    ti.textureType = UnityEditor.TextureImporterType.Default;
    ti.sRGBTexture = srgb;
    ti.alphaSource = hasAlpha ? UnityEditor.TextureImporterAlphaSource.FromInput : UnityEditor.TextureImporterAlphaSource.None;
    ti.alphaIsTransparency = false;
    ti.maxTextureSize = 2048;
    ti.mipmapEnabled = true;
    ti.anisoLevel = 8;
    ti.mipMapsPreserveCoverage = hasAlpha;
    if (hasAlpha) ti.alphaTestReferenceValue = alphaRef;
    ti.SaveAndReimport();
    sb.Append(file + "=ok ");
};
setup("Sedan_basecolor.png", true, true, 0.5f);
setup("Sedan_clearcoat.png", false, false, 0f);
setup("Sedan_glass.png", true, true, 0.25f);
foreach (string f in new[] { "Sedan_normal.png", "Sedan_metallicSmoothness.png" })
{
    var ti = UnityEditor.AssetImporter.GetAtPath(dir + f) as UnityEditor.TextureImporter;
    if (ti != null && ti.anisoLevel != 8) { ti.anisoLevel = 8; ti.SaveAndReimport(); }
}
return sb.ToString();
