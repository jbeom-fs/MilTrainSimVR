// 차량 재질 — URP Lit + 색 · 노멀 · 금속/매끈함. FBX 안의 재질 칸을 이 재질로 잇고, 자동 Mesh LOD 는 끈다
if (UnityEngine.Application.isPlaying) return "SKIPPED: Play mode";
string name = "V_NAME", folder = "V_FOLDER";
string dir = "Assets/C/Models/Tripo/" + folder + "/";
string fbxPath = dir + name + ".fbx";
var sb = new System.Text.StringBuilder();
System.Func<string, UnityEngine.Texture2D> tex = f => UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(dir + f);
if (tex(name + "_basecolor.jpg") == null || tex(name + "_normal.png") == null || tex(name + "_metallicSmoothness.png") == null) return "TEXTURES NOT IMPORTED";
var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(dir + name + ".mat");
if (mat == null)
{
    mat = new UnityEngine.Material(UnityEngine.Shader.Find("Universal Render Pipeline/Lit"));
    UnityEditor.AssetDatabase.CreateAsset(mat, dir + name + ".mat");
}
mat.SetTexture("_BaseMap", tex(name + "_basecolor.jpg"));
mat.SetColor("_BaseColor", UnityEngine.Color.white);
mat.SetTexture("_BumpMap", tex(name + "_normal.png"));
mat.SetFloat("_BumpScale", 1f);
mat.SetTexture("_MetallicGlossMap", tex(name + "_metallicSmoothness.png"));
mat.SetFloat("_Smoothness", 1f);
mat.SetFloat("_WorkflowMode", 1f);
UnityEditor.BaseShaderGUI.SetMaterialKeywords(mat, UnityEditor.Rendering.Universal.ShaderGUI.LitGUI.SetMaterialKeywords);
UnityEditor.EditorUtility.SetDirty(mat);
UnityEditor.AssetDatabase.SaveAssetIfDirty(mat);
var mi = UnityEditor.AssetImporter.GetAtPath(fbxPath) as UnityEditor.ModelImporter;
if (mi == null) return "FBX NOT IMPORTED";
bool changed = false;
var pLod = typeof(UnityEditor.ModelImporter).GetProperty("generateMeshLods");
if (pLod != null && (bool)pLod.GetValue(mi)) { pLod.SetValue(mi, false); changed = true; sb.Append("meshLods off; "); }
var map = mi.GetExternalObjectMap();
foreach (var n in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<UnityEngine.Material>().Select(m => m.name).Distinct().ToList())
{
    var id = new UnityEditor.AssetImporter.SourceAssetIdentifier(typeof(UnityEngine.Material), n);
    UnityEngine.Object cur;
    if (!map.TryGetValue(id, out cur) || cur != mat) { mi.AddRemap(id, mat); changed = true; sb.Append("remap " + n + "; "); }
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
foreach (string f in new[] { name + "_basecolor.jpg", name + "_normal.png", name + "_metallicSmoothness.png" })
{
    var ti = UnityEditor.AssetImporter.GetAtPath(dir + f) as UnityEditor.TextureImporter;
    if (ti != null) sb.Append("| " + f + " " + ti.textureType + " max" + ti.maxTextureSize + " sRGB=" + ti.sRGBTexture + " ");
}
return sb.ToString();
