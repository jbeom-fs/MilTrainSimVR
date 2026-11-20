// 차량 프리팹 만들기 — 모델을 재서 방향(앞 = +Z) · 크기 · 바닥 높이 · 판정 상자 둘을 정한다. BUILD 가 false 면 재기만 한다
// 뿌리(BoxCollider 둘) - Model(자리 · 방향 · 크기) - <이름>.fbx. 열린 씬은 건드리지 않게 미리보기 씬에서 만든다
// 11/18: 상자는 몸만 막고 총알 판정은 보이는 차체로 — 이걸로 프리팹을 다시 만들면 메뉴 'MilTrainSimVR → C → 차량 총알 판정 맞추기'도 다시 누른다 (VehicleBulletColliderBuilder)
if (UnityEngine.Application.isPlaying) return "SKIPPED: Play mode";
string name = "V_NAME", folder = "V_FOLDER", prefabName = "V_PREFAB";
float targetH = V_HEIGHTf;                 // 실제 높이(m) — 이 값으로 전체 배율을 정한다
bool frontIsHighEnd = V_FRONT_HIGH;        // 트럭: 앞(운전석)이 뒤(짐칸)보다 높다 / 승용 · 승합: 앞(보닛)이 낮다
var fix = new UnityEngine.Vector3(V_FIX_Lf, V_FIX_Hf, V_FIX_Wf);   // 길이 · 높이 · 폭 보정 배율 (1 = 그대로)
bool build = V_BUILD;
string fbxPath = "Assets/C/Models/Tripo/" + folder + "/" + name + ".fbx";
var fbx = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(fbxPath);
if (fbx == null) return "FBX MISSING " + fbxPath;
var mf0 = fbx.GetComponentInChildren<UnityEngine.MeshFilter>(true);
var m0 = mf0.transform.localToWorldMatrix;
var vs = mf0.sharedMesh.vertices;
int n = vs.Length;
var V = new UnityEngine.Vector3[n];
var mn = new UnityEngine.Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
var mx = -mn;
for (int i = 0; i < n; i++) { V[i] = m0.MultiplyPoint3x4(vs[i]); mn = UnityEngine.Vector3.Min(mn, V[i]); mx = UnityEngine.Vector3.Max(mx, V[i]); }
var size = mx - mn;
bool lengthIsX = size.x >= size.z;
float a0 = lengthIsX ? mn.x : mn.z, len = lengthIsX ? size.x : size.z;
float topMinEnd = float.MinValue, topMaxEnd = float.MinValue;
for (int i = 0; i < n; i++)
{
    float u = ((lengthIsX ? V[i].x : V[i].z) - a0) / len;
    if (u < 0.12f) topMinEnd = UnityEngine.Mathf.Max(topMinEnd, V[i].y);
    if (u > 0.88f) topMaxEnd = UnityEngine.Mathf.Max(topMaxEnd, V[i].y);
}
bool frontIsMin = frontIsHighEnd ? (topMinEnd > topMaxEnd) : (topMinEnd < topMaxEnd);
float yaw = lengthIsX ? (frontIsMin ? 90f : 270f) : (frontIsMin ? 180f : 0f);
float s = targetH / size.y;
var holderScale = lengthIsX ? new UnityEngine.Vector3(s * fix.x, s * fix.y, s * fix.z) : new UnityEngine.Vector3(s * fix.z, s * fix.y, s * fix.x);
var rot = UnityEngine.Quaternion.Euler(0f, yaw, 0f);
var trs = UnityEngine.Matrix4x4.TRS(UnityEngine.Vector3.zero, rot, holderScale);
var P = new UnityEngine.Vector3[n];
var pmn = new UnityEngine.Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
var pmx = -pmn;
for (int i = 0; i < n; i++) { P[i] = trs.MultiplyPoint3x4(V[i]); pmn = UnityEngine.Vector3.Min(pmn, P[i]); pmx = UnityEngine.Vector3.Max(pmx, P[i]); }
var holderPos = new UnityEngine.Vector3(-(pmn.x + pmx.x) * 0.5f, -pmn.y, -(pmn.z + pmx.z) * 0.5f);
for (int i = 0; i < n; i++) P[i] += holderPos;
float H = pmx.y - pmn.y, L = pmx.z - pmn.z, Wall = pmx.x - pmn.x;
System.Func<System.Collections.Generic.List<float>, float, float> pct = (list, q) =>
{
    if (list.Count == 0) return 0f;
    list.Sort();
    return list[UnityEngine.Mathf.Clamp((int)(list.Count * q), 0, list.Count - 1)];
};
// 몸통 폭 — 거울 아래 높이(0.35 ~ 0.75 m) 띠에서 잰다
var bandX = new System.Collections.Generic.List<float>();
for (int i = 0; i < n; i++) if (P[i].y > 0.35f && P[i].y < 0.75f) bandX.Add(P[i].x);
float bx0 = pct(bandX, 0.005f), bx1 = pct(bandX, 0.995f);
float bodyW = bx1 - bx0;
// 길이 방향 40 칸의 꼭대기 높이 (거울 · 안테나를 빼려고 몸통 폭 안쪽만)
int NS = 40;
var sl = new System.Collections.Generic.List<float>[NS];
for (int k = 0; k < NS; k++) sl[k] = new System.Collections.Generic.List<float>();
float z0 = -L * 0.5f, dz = L / NS;
for (int i = 0; i < n; i++)
{
    if (UnityEngine.Mathf.Abs(P[i].x) > bodyW * 0.5f + 0.02f) continue;
    int k = UnityEngine.Mathf.Clamp((int)((P[i].z - z0) / dz), 0, NS - 1);
    sl[k].Add(P[i].y);
}
var tops = new float[NS];
for (int k = 0; k < NS; k++) tops[k] = pct(sl[k], 0.995f);
float tMin = float.MaxValue, tMax = float.MinValue;
for (int k = 2; k < NS - 2; k++) { tMin = UnityEngine.Mathf.Min(tMin, tops[k]); tMax = UnityEngine.Mathf.Max(tMax, tops[k]); }
float thr = (tMin + tMax) * 0.5f;
var lows = new System.Collections.Generic.List<float>();
int k0 = -1, k1 = -1;
for (int k = 0; k < NS; k++)
{
    if (tops[k] < thr) { if (k >= 2 && k < NS - 2) lows.Add(tops[k]); }
    else { if (k0 < 0) k0 = k; k1 = k; }
}
float hLow = lows.Count > 0 ? pct(lows, 0.35f) : thr;
float zu0 = z0 + k0 * dz, zu1 = z0 + (k1 + 1) * dz;
var roofX = new System.Collections.Generic.List<float>();
for (int i = 0; i < n; i++)
    if (P[i].z >= zu0 && P[i].z <= zu1 && P[i].y > hLow + 0.8f * (tMax - hLow) && UnityEngine.Mathf.Abs(P[i].x) <= bodyW * 0.5f + 0.02f) roofX.Add(P[i].x);
float roofW = pct(roofX, 0.995f) - pct(roofX, 0.005f);
float upperW = UnityEngine.Mathf.Min(bodyW, roofW * 1.18f);
var lowC = new UnityEngine.Vector3(0f, hLow * 0.5f, 0f);
var lowS = new UnityEngine.Vector3(bodyW, hLow, L - 0.05f);
var upC = new UnityEngine.Vector3(0f, (hLow + tMax) * 0.5f, (zu0 + zu1) * 0.5f);
var upS = new UnityEngine.Vector3(upperW, tMax - hLow, zu1 - zu0);
var sb = new System.Text.StringBuilder();
sb.Append(name + ": model size=" + size.ToString("F4") + " lengthIsX=" + lengthIsX + " frontIsMin=" + frontIsMin + " endTops(min,max)=" + topMinEnd.ToString("F3") + "/" + topMaxEnd.ToString("F3"));
sb.Append(" | yaw=" + yaw + " holderScale=" + holderScale.ToString("F3") + " holderPos=" + holderPos.ToString("F4"));
sb.Append(" | real L=" + L.ToString("F2") + " W(all)=" + Wall.ToString("F2") + " W(body)=" + bodyW.ToString("F2") + " H=" + H.ToString("F2"));
sb.Append(" | tops front-to-rear: ");
for (int k = NS - 1; k >= 0; k -= 3) sb.Append(tops[k].ToString("F2") + " ");
sb.Append("| hLow=" + hLow.ToString("F2") + " thr=" + thr.ToString("F2") + " upper z=" + zu0.ToString("F2") + ".." + zu1.ToString("F2") + " roofW=" + roofW.ToString("F2"));
sb.Append(" | box1 c=" + lowC.ToString("F3") + " s=" + lowS.ToString("F3") + " | box2 c=" + upC.ToString("F3") + " s=" + upS.ToString("F3"));
if (!build) return sb.ToString();
string pfolder = "Assets/C/Prefabs/Vehicle";
string path = pfolder + "/" + prefabName + ".prefab";
var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
bool dirtyBefore = active.isDirty;
var ps = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
try
{
    var root = new UnityEngine.GameObject(prefabName);
    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, ps);
    var holder = new UnityEngine.GameObject("Model");
    holder.transform.SetParent(root.transform, false);
    holder.transform.localPosition = holderPos;
    holder.transform.localRotation = rot;
    holder.transform.localScale = holderScale;
    UnityEditor.PrefabUtility.InstantiatePrefab(fbx, holder.transform);
    var b1 = root.AddComponent<UnityEngine.BoxCollider>();
    b1.center = lowC; b1.size = lowS;
    var b2 = root.AddComponent<UnityEngine.BoxCollider>();
    b2.center = upC; b2.size = upS;
    var flags = UnityEditor.StaticEditorFlags.BatchingStatic | UnityEditor.StaticEditorFlags.ReflectionProbeStatic;
    UnityEditor.GameObjectUtility.SetStaticEditorFlags(root, flags);
    UnityEditor.GameObjectUtility.SetStaticEditorFlags(holder, flags);
    var saved = UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, path);
    sb.Append(" | SAVED=" + (saved != null) + " " + path);
}
finally
{
    UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(ps);
}
sb.Append(" | scene dirty before=" + dirtyBefore + " after=" + active.isDirty);
return sb.ToString();
