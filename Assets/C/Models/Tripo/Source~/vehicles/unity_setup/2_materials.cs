// 세단 재질 셋 — 몸통(클리어코트 도장 · 유리 자리 도려냄) · 유리(투명 · 반사) · 실내(어둡게)
if (UnityEngine.Application.isPlaying) return "SKIPPED: Play mode";
string dir = "Assets/C/Models/Tripo/Sedan/";
System.Func<string, UnityEngine.Texture2D> tex = f => UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(dir + f);
var baseMap = tex("Sedan_basecolor.png");
var normal = tex("Sedan_normal.png");
var ms = tex("Sedan_metallicSmoothness.png");
var cc = tex("Sedan_clearcoat.png");
var glassMap = tex("Sedan_glass.png");
if (baseMap == null || normal == null || ms == null || cc == null || glassMap == null) return "MISSING texture";
var lit = UnityEngine.Shader.Find("Universal Render Pipeline/Lit");
var complex = UnityEngine.Shader.Find("Universal Render Pipeline/Complex Lit");
System.Func<string, UnityEngine.Shader, UnityEngine.Material> get = (name, shader) =>
{
    var m = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(dir + name + ".mat");
    if (m == null)
    {
        m = new UnityEngine.Material(shader);
        UnityEditor.AssetDatabase.CreateAsset(m, dir + name + ".mat");
    }
    m.shader = shader;
    return m;
};
// 몸통
var body = get("Sedan_Body", complex);
body.SetTexture("_BaseMap", baseMap);
body.SetColor("_BaseColor", UnityEngine.Color.white);
body.SetTexture("_BumpMap", normal);
body.SetFloat("_BumpScale", 1f);
body.SetTexture("_MetallicGlossMap", ms);
body.SetFloat("_Smoothness", 1f);
body.SetFloat("_SmoothnessTextureChannel", 0f);
body.SetFloat("_WorkflowMode", 1f);
body.SetFloat("_Surface", 0f);
body.SetFloat("_AlphaClip", 1f);
body.SetFloat("_Cutoff", 0.5f);
body.SetFloat("_Cull", 2f);
body.SetFloat("_ClearCoat", 1f);
body.SetTexture("_ClearCoatMap", cc);
body.SetFloat("_ClearCoatMask", 1f);
body.SetFloat("_ClearCoatSmoothness", 0.92f);
UnityEditor.BaseShaderGUI.SetMaterialKeywords(body, UnityEditor.Rendering.Universal.ShaderGUI.LitGUI.SetMaterialKeywords);
UnityEditor.EditorUtility.SetDirty(body);
// 유리
var glass = get("Sedan_Glass", lit);
glass.SetTexture("_BaseMap", glassMap);
glass.SetColor("_BaseColor", UnityEngine.Color.white);
glass.SetFloat("_WorkflowMode", 1f);
glass.SetFloat("_Metallic", 0f);
glass.SetFloat("_Smoothness", 0.95f);
glass.SetFloat("_Surface", 1f);
glass.SetFloat("_Blend", 0f);
glass.SetFloat("_BlendModePreserveSpecular", 1f);
glass.SetFloat("_AlphaClip", 1f);
glass.SetFloat("_Cutoff", 0.25f);
glass.SetFloat("_Cull", 2f);
glass.SetFloat("_ReceiveShadows", 1f);
UnityEditor.BaseShaderGUI.SetMaterialKeywords(glass, UnityEditor.Rendering.Universal.ShaderGUI.LitGUI.SetMaterialKeywords);
UnityEditor.EditorUtility.SetDirty(glass);
// 실내 — 몸통 텍스처(유리 자리 알파)를 어둡게 곱한다
var interior = get("Sedan_Interior", lit);
interior.SetTexture("_BaseMap", baseMap);
interior.SetColor("_BaseColor", new UnityEngine.Color(0.17f, 0.17f, 0.18f, 1f));
interior.SetFloat("_WorkflowMode", 1f);
interior.SetFloat("_Metallic", 0f);
interior.SetFloat("_Smoothness", 0.25f);
interior.SetFloat("_Surface", 0f);
interior.SetFloat("_AlphaClip", 1f);
interior.SetFloat("_Cutoff", 0.5f);
interior.SetFloat("_Cull", 2f);
UnityEditor.BaseShaderGUI.SetMaterialKeywords(interior, UnityEditor.Rendering.Universal.ShaderGUI.LitGUI.SetMaterialKeywords);
UnityEditor.EditorUtility.SetDirty(interior);
UnityEditor.AssetDatabase.SaveAssets();
// FBX 의 재질 이름 → 재질 에셋
var mi = (UnityEditor.ModelImporter)UnityEditor.AssetImporter.GetAtPath(dir + "Sedan.fbx");
mi.AddRemap(new UnityEditor.AssetImporter.SourceAssetIdentifier(typeof(UnityEngine.Material), "Sedan_Body"), body);
mi.AddRemap(new UnityEditor.AssetImporter.SourceAssetIdentifier(typeof(UnityEngine.Material), "Sedan_Glass"), glass);
mi.AddRemap(new UnityEditor.AssetImporter.SourceAssetIdentifier(typeof(UnityEngine.Material), "Sedan_Interior"), interior);
mi.SaveAndReimport();
// 예전 JPG 색 텍스처는 더 쓰지 않는다
string oldJpg = dir + "Sedan_basecolor.jpg";
bool removed = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(oldJpg) != null && UnityEditor.AssetDatabase.DeleteAsset(oldJpg);
var fbx = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(dir + "Sedan.fbx");
var sb = new System.Text.StringBuilder();
foreach (var r in fbx.GetComponentsInChildren<UnityEngine.MeshRenderer>(true))
{
    var mesh = r.GetComponent<UnityEngine.MeshFilter>().sharedMesh;
    sb.Append(r.name + ":" + (mesh.triangles.Length / 3) + "tris/" + mesh.vertexCount + "v/" + string.Join(",", r.sharedMaterials.Select(m => m == null ? "null" : m.name + "(" + m.shader.name.Replace("Universal Render Pipeline/", "") + " q" + m.renderQueue + ")")) + " | ");
}
return sb + "body keywords=[" + string.Join(" ", body.shaderKeywords) + "] glass keywords=[" + string.Join(" ", glass.shaderKeywords) + "] oldJpgRemoved=" + removed;
