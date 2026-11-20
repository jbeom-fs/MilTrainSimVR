// 10만 세단 재질 — 원본 비교용 재질(white_sedan_original.mat)과 같은 방식(URP Lit + 색 · 노멀 · 금속/매끈함)
if (UnityEngine.Application.isPlaying) return "SKIPPED: Play mode";
string dir = "Assets/C/Models/Tripo/Sedan/";
string fbxPath = dir + "Sedan100k.fbx";
var sb = new System.Text.StringBuilder();
System.Func<string, UnityEngine.Texture2D> tex = f => UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(dir + f);
if (tex("Sedan100k_basecolor.jpg") == null || tex("Sedan100k_normal.png") == null || tex("Sedan100k_metallicSmoothness.png") == null) return "TEXTURES NOT IMPORTED";
var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(dir + "Sedan100k.mat");
if (mat == null)
{
    mat = new UnityEngine.Material(UnityEngine.Shader.Find("Universal Render Pipeline/Lit"));
    UnityEditor.AssetDatabase.CreateAsset(mat, dir + "Sedan100k.mat");
}
mat.SetTexture("_BaseMap", tex("Sedan100k_basecolor.jpg"));
mat.SetColor("_BaseColor", UnityEngine.Color.white);
mat.SetTexture("_BumpMap", tex("Sedan100k_normal.png"));
mat.SetFloat("_BumpScale", 1f);
mat.SetTexture("_MetallicGlossMap", tex("Sedan100k_metallicSmoothness.png"));
mat.SetFloat("_Smoothness", 1f);
mat.SetFloat("_WorkflowMode", 1f);
UnityEditor.BaseShaderGUI.SetMaterialKeywords(mat, UnityEditor.Rendering.Universal.ShaderGUI.LitGUI.SetMaterialKeywords);
UnityEditor.EditorUtility.SetDirty(mat);
UnityEditor.AssetDatabase.SaveAssetIfDirty(mat);

var mi = UnityEditor.AssetImporter.GetAtPath(fbxPath) as UnityEditor.ModelImporter;
if (mi == null) return "FBX NOT IMPORTED";
bool changed = false;
var pLod = typeof(UnityEditor.ModelImporter).GetProperty("generateMeshLods");
if (pLod != null)
{
    bool was = (bool)pLod.GetValue(mi);
    sb.Append("generateMeshLods was " + was + " ");
    if (was) { pLod.SetValue(mi, false); changed = true; }
}
else sb.Append("generateMeshLods property not found ");
var map = mi.GetExternalObjectMap();
foreach (var n in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<UnityEngine.Material>().Select(m => m.name).Distinct().ToList())
{
    var id = new UnityEditor.AssetImporter.SourceAssetIdentifier(typeof(UnityEngine.Material), n);
    UnityEngine.Object cur;
    if (!map.TryGetValue(id, out cur) || cur != mat) { mi.AddRemap(id, mat); changed = true; sb.Append("remap " + n + " "); }
}
if (changed) mi.SaveAndReimport();

long tris = 0; int verts = 0;
foreach (var o in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(fbxPath))
{
    var m = o as UnityEngine.Mesh;
    if (m == null) continue;
    for (int s = 0; s < m.subMeshCount; s++) tris += (long)m.GetIndexCount(s) / 3;
    verts += m.vertexCount;
    sb.Append("mesh " + m.name + " lod=" + m.lodCount + " size=" + m.bounds.size.ToString("F4") + " ");
}
sb.Append("| tris=" + tris + " verts=" + verts + " ");
var go = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(fbxPath);
foreach (var r in go.GetComponentsInChildren<UnityEngine.MeshRenderer>(true))
    sb.Append("| renderer " + r.name + " mats=[" + string.Join(",", r.sharedMaterials.Select(x => x == null ? "null" : x.name)) + "] ");
System.Action<string, string> texInfo = (label, path) =>
{
    var ti = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
    if (ti == null) { sb.Append("| " + label + " importer missing "); return; }
    int w, h;
    ti.GetSourceTextureWidthAndHeight(out w, out h);
    sb.Append("| " + label + " src " + w + "x" + h + " max " + ti.maxTextureSize + " ");
};
texInfo("new base", dir + "Sedan100k_basecolor.jpg");
texInfo("orig base", "Assets/C/Models/Tripo/Bridge/white_sedan_3d_model/tripo_model_basecolor.jpg");
texInfo("orig normal", "Assets/C/Models/Tripo/Bridge/white_sedan_3d_model/tripo_model_normal.png");
UnityEngine.Physics.SyncTransforms();
var hits = UnityEngine.Physics.OverlapBox(new UnityEngine.Vector3(75f, 0.9f, 124.1f), new UnityEngine.Vector3(2.4f, 0.6f, 1.05f));
sb.Append("| overlap at x75 = [" + string.Join(",", hits.Select(c => c.name).Distinct().Take(8)) + "]");
return sb.ToString();
