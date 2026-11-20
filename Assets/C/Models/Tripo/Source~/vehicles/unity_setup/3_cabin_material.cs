// 실내 소품 재질(모든 차량 공용) — 색판 텍스처 한 장. 세단 FBX 의 'Vehicle_Cabin' 재질 이름을 여기에 잇는다
if (UnityEngine.Application.isPlaying) return "SKIPPED: Play mode";
UnityEditor.AssetDatabase.Refresh();
string shared = "Assets/C/Models/Tripo/VehicleShared/";
string dir = "Assets/C/Models/Tripo/Sedan/";
var ti = UnityEditor.AssetImporter.GetAtPath(shared + "Vehicle_CabinPalette.png") as UnityEditor.TextureImporter;
if (ti == null) return "MISSING palette";
ti.textureType = UnityEditor.TextureImporterType.Default;
ti.sRGBTexture = true;
ti.mipmapEnabled = false;
ti.filterMode = UnityEngine.FilterMode.Point;
ti.wrapMode = UnityEngine.TextureWrapMode.Clamp;
ti.textureCompression = UnityEditor.TextureImporterCompression.Uncompressed;
ti.maxTextureSize = 32;
ti.alphaSource = UnityEditor.TextureImporterAlphaSource.None;
ti.SaveAndReimport();
var palette = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(shared + "Vehicle_CabinPalette.png");
var cabin = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(shared + "Vehicle_Cabin.mat");
if (cabin == null)
{
    cabin = new UnityEngine.Material(UnityEngine.Shader.Find("Universal Render Pipeline/Lit"));
    UnityEditor.AssetDatabase.CreateAsset(cabin, shared + "Vehicle_Cabin.mat");
}
cabin.SetTexture("_BaseMap", palette);
cabin.SetColor("_BaseColor", UnityEngine.Color.white);
cabin.SetFloat("_WorkflowMode", 1f);
cabin.SetFloat("_Metallic", 0f);
cabin.SetFloat("_Smoothness", 0.28f);
cabin.SetFloat("_Surface", 0f);
cabin.SetFloat("_AlphaClip", 0f);
cabin.SetFloat("_Cull", 2f);
UnityEditor.BaseShaderGUI.SetMaterialKeywords(cabin, UnityEditor.Rendering.Universal.ShaderGUI.LitGUI.SetMaterialKeywords);
UnityEditor.EditorUtility.SetDirty(cabin);
UnityEditor.AssetDatabase.SaveAssets();
var mi = (UnityEditor.ModelImporter)UnityEditor.AssetImporter.GetAtPath(dir + "Sedan.fbx");
mi.AddRemap(new UnityEditor.AssetImporter.SourceAssetIdentifier(typeof(UnityEngine.Material), "Vehicle_Cabin"), cabin);
mi.SaveAndReimport();
var fbx = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(dir + "Sedan.fbx");
var sb = new System.Text.StringBuilder();
int tris = 0, verts = 0;
foreach (var r in fbx.GetComponentsInChildren<UnityEngine.MeshRenderer>(true))
{
    var mesh = r.GetComponent<UnityEngine.MeshFilter>().sharedMesh;
    tris += mesh.triangles.Length / 3;
    verts += mesh.vertexCount;
    sb.Append(r.name + ":" + (mesh.triangles.Length / 3) + "tris/" + mesh.vertexCount + "v/" + string.Join(",", r.sharedMaterials.Select(m => m == null ? "null" : m.name + "(q" + m.renderQueue + ")")) + " | ");
}
return sb + "total " + tris + " tris / " + verts + " verts";
