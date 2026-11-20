// 적 길찾기 지도만 다시 굽는다 — 고친 UrbanGreyboxBuilder.Navigation.cs 의 BakeNavMesh 와 같은 순서(맵 + 콘크리트 벽 + 길가 차량).
// 에디터가 백그라운드라 스크립트 컴파일이 대기 중이어서(11/10), 컴파일된 옛 메뉴 대신 같은 내용을 여기서 실행한다.
// 상수와 보조 함수(HandPlacedBarriers · AddOutsideModifiers)는 프로젝트 코드의 것을 그대로 가져다 쓴다
if (UnityEngine.Application.isPlaying || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return "SKIPPED: Play mode";
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (scene.path != "Assets/C/Scenes/C_UrbanBlock.unity") return "SKIPPED: active scene is " + scene.path;
var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
var allTypes = System.AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } }).ToList();
var T = allTypes.FirstOrDefault(x => x.FullName == "MilTrainSimVR.UrbanCombat.EditorTools.UrbanGreyboxBuilder");
if (T == null) return "NOT DONE: builder type not found";
System.Func<string, object> K = n => { var f = T.GetField(n, flags); if (f == null) throw new System.Exception("const " + n + " not found"); return f.GetRawConstantValue(); };
float radius = (float)K("k_NavRadius"), height = (float)K("k_NavHeight"), climb = (float)K("k_NavClimb"), slope = (float)K("k_NavSlope"), voxel = (float)K("k_NavVoxel");
int tile = (int)K("k_NavTile");
float bottom = (float)K("k_NavBottom"), top = (float)K("k_NavTop");
string rootName = (string)K("k_RootName");
var mBarriers = T.GetMethod("HandPlacedBarriers", flags);
var mOutside = T.GetMethod("AddOutsideModifiers", flags);
if (mBarriers == null || mOutside == null) return "NOT DONE: helper methods not found";
var tops = scene.GetRootGameObjects();
var root = tops.FirstOrDefault(g => g.name == rootName);
if (root == null) return "NOT DONE: map root " + rootName + " not found";
UnityEngine.Component playArea = null;
foreach (var g in tops)
{
    playArea = g.GetComponentsInChildren<UnityEngine.Component>(true).FirstOrDefault(c => c != null && c.GetType().Name == "GreyboxPlayAreaSpec");
    if (playArea != null) break;
}
if (playArea == null) return "NOT DONE: play area not found";
var corners = (System.Collections.Generic.List<UnityEngine.Vector3>)playArea.GetType().GetMethod("Corners", flags).Invoke(playArea, null);
var loaderType = allTypes.FirstOrDefault(x => x.Name == "GreyboxNavMesh" && typeof(UnityEngine.Component).IsAssignableFrom(x));
if (loaderType == null) return "NOT DONE: GreyboxNavMesh type not found";
var pData = loaderType.GetProperty("Data", flags);
var fData = loaderType.GetField("Data", flags);
if (pData == null && fData == null) return "NOT DONE: GreyboxNavMesh.Data not found";

UnityEngine.Physics.SyncTransforms();
var settings = UnityEngine.AI.NavMesh.GetSettingsByID(0);
settings.agentRadius = radius;
settings.agentHeight = height;
settings.agentClimb = climb;
settings.agentSlope = slope;
settings.overrideVoxelSize = true;
settings.voxelSize = voxel;
settings.overrideTileSize = true;
settings.tileSize = tile;
var sources = new System.Collections.Generic.List<UnityEngine.AI.NavMeshBuildSource>();
UnityEngine.AI.NavMeshBuilder.CollectSources(root.transform, ~0, UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders, 0, new System.Collections.Generic.List<UnityEngine.AI.NavMeshBuildMarkup>(), sources);
int nMap = sources.Count;
var barriers = (System.Collections.Generic.List<UnityEngine.Transform>)mBarriers.Invoke(null, new object[] { scene, root });
foreach (UnityEngine.Transform barrier in barriers)
{
    var extra = new System.Collections.Generic.List<UnityEngine.AI.NavMeshBuildSource>();
    UnityEngine.AI.NavMeshBuilder.CollectSources(barrier, ~0, UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders, 0, new System.Collections.Generic.List<UnityEngine.AI.NavMeshBuildMarkup>(), extra);
    sources.AddRange(extra);
}
int nBarrier = sources.Count - nMap;
foreach (UnityEngine.GameObject g in tops)
{
    if (g.name != "C_Vehicles") continue;
    var extra = new System.Collections.Generic.List<UnityEngine.AI.NavMeshBuildSource>();
    UnityEngine.AI.NavMeshBuilder.CollectSources(g.transform, ~0, UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders, 0, new System.Collections.Generic.List<UnityEngine.AI.NavMeshBuildMarkup>(), extra);
    sources.AddRange(extra);
}
int nVehicle = sources.Count - nMap - nBarrier;
if (nVehicle == 0) return "NOT DONE: no vehicle colliders collected";
mOutside.Invoke(null, new object[] { sources, corners });
UnityEngine.Vector3 min = corners[0], max = corners[0];
foreach (UnityEngine.Vector3 c in corners) { min = UnityEngine.Vector3.Min(min, c); max = UnityEngine.Vector3.Max(max, c); }
var bounds = new UnityEngine.Bounds();
bounds.SetMinMax(new UnityEngine.Vector3(min.x - 1f, bottom, min.z - 1f), new UnityEngine.Vector3(max.x + 1f, top, max.z + 1f));
var baked = UnityEngine.AI.NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity);
if (baked == null) return "NOT DONE: build returned nothing (old map kept)";

// 같은 파일에 덮어써서 GUID 를 지킨다. 켜져 있는 지도를 그대로 덮어쓰면 에디터가 죽으므로 먼저 뗀다 (원래 코드의 순서 그대로)
var loader = root.GetComponent(loaderType);
if (loader == null) loader = root.AddComponent(loaderType);
System.Action<UnityEngine.AI.NavMeshData> setData = d => { if (pData != null) pData.SetValue(loader, d); else fData.SetValue(loader, d); };
setData(null);
string folder = scene.path.Substring(0, scene.path.Length - ".unity".Length);
string path = folder + "/NavMesh_" + scene.name + ".asset";
baked.name = "NavMesh_" + scene.name;
var data = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(path);
if (data != null)
{
    UnityEditor.EditorUtility.CopySerialized(baked, data);
    UnityEngine.Object.DestroyImmediate(baked);
    UnityEditor.EditorUtility.SetDirty(data);
}
else
{
    if (!UnityEditor.AssetDatabase.IsValidFolder(folder))
        UnityEditor.AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(folder).Replace('\\', '/'), System.IO.Path.GetFileName(folder));
    UnityEditor.AssetDatabase.CreateAsset(baked, path);
    data = baked;
}
UnityEditor.AssetDatabase.SaveAssetIfDirty(data);
setData(data);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
var tri = UnityEngine.AI.NavMesh.CalculateTriangulation();
string msg = "[C] 적 길찾기 지도를 구웠다 (차량 포함) — " + path + " (삼각형 " + (tri.indices.Length / 3) + ")";
UnityEngine.Debug.Log(msg);
return "BAKED sources: map=" + nMap + " barriers=" + nBarrier + " vehicles=" + nVehicle + " | triangles=" + (tri.indices.Length / 3) + " | " + path;
