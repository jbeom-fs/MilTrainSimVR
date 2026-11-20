// 읽기만 한다 — 지금 켜져 있는 길찾기 지도에서 순찰 경로와 차량 자리를 잰다 (굽기 전 · 후 비교용)
var sb = new System.Text.StringBuilder();
UnityEngine.Physics.SyncTransforms();
var tri = UnityEngine.AI.NavMesh.CalculateTriangulation();
sb.Append("navmesh triangles=" + (tri.indices.Length / 3) + " playing=" + UnityEngine.Application.isPlaying + "\n");
System.Func<UnityEngine.Vector3, UnityEngine.Vector3, string> route = (a, b) =>
{
    UnityEngine.AI.NavMeshHit ha, hb;
    bool oa = UnityEngine.AI.NavMesh.SamplePosition(a, out ha, 0.5f, UnityEngine.AI.NavMesh.AllAreas);
    bool ob = UnityEngine.AI.NavMesh.SamplePosition(b, out hb, 0.5f, UnityEngine.AI.NavMesh.AllAreas);
    if (!oa || !ob) return "endpoint off the map (a=" + oa + " b=" + ob + ")";
    var path = new UnityEngine.AI.NavMeshPath();
    UnityEngine.AI.NavMesh.CalculatePath(ha.position, hb.position, UnityEngine.AI.NavMesh.AllAreas, path);
    float len = 0f;
    for (int i = 1; i < path.corners.Length; i++) len += UnityEngine.Vector3.Distance(path.corners[i - 1], path.corners[i]);
    float straight = UnityEngine.Vector3.Distance(ha.position, hb.position);
    return path.status + " length=" + len.ToString("F2") + " (straight " + straight.ToString("F2") + ") corners=" + path.corners.Length + " " + string.Join(" ", path.corners.Select(c => "(" + c.x.ToString("F1") + "," + c.z.ToString("F1") + ")").Take(10));
};
sb.Append("route 7  west ring  : " + route(new UnityEngine.Vector3(64.5f, 0f, 247f), new UnityEngine.Vector3(64.5f, 0f, 175f)) + "\n");
sb.Append("route 8  east ring  : " + route(new UnityEngine.Vector3(117.3f, 0f, 247f), new UnityEngine.Vector3(117.3f, 0f, 175f)) + "\n");
sb.Append("route 10 north leg  : " + route(new UnityEngine.Vector3(117.3f, 0f, 255f), new UnityEngine.Vector3(64.5f, 0f, 255f)) + "\n");
sb.Append("route 10 west leg   : " + route(new UnityEngine.Vector3(64.5f, 0f, 255f), new UnityEngine.Vector3(64.5f, 0f, 158f)) + "\n");
sb.Append("route 10 south leg  : " + route(new UnityEngine.Vector3(64.5f, 0f, 158f), new UnityEngine.Vector3(117.3f, 0f, 158f)) + "\n");
sb.Append("route 10 east leg   : " + route(new UnityEngine.Vector3(117.3f, 0f, 158f), new UnityEngine.Vector3(117.3f, 0f, 255f)) + "\n");
sb.Append("centre road, west-east: " + route(new UnityEngine.Vector3(45f, 0f, 124.2f), new UnityEngine.Vector3(125f, 0f, 124.2f)) + "\n");
sb.Append("link road, south-north: " + route(new UnityEngine.Vector3(131.2f, 0f, 128f), new UnityEngine.Vector3(131.2f, 0f, 155f)) + "\n");
sb.Append("parking lot diagonal  : " + route(new UnityEngine.Vector3(43f, 0f, 148f), new UnityEngine.Vector3(61f, 0f, 186f)) + "\n");
// 차량마다: 차 가운데 바닥이 걷는 곳인지, 차 양옆 0.6 m 바깥이 걷는 곳인지
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "C_Vehicles");
if (root != null)
    foreach (UnityEngine.Transform ch in root.transform)
    {
        var cols = ch.GetComponentsInChildren<UnityEngine.BoxCollider>(true);
        if (cols.Length == 0) { sb.Append(ch.name + ": no colliders (not in the map)\n"); continue; }
        UnityEngine.Bounds b = cols[0].bounds;
        foreach (var c in cols) b.Encapsulate(c.bounds);
        System.Func<UnityEngine.Vector3, string> onGround = p =>
        {
            UnityEngine.AI.NavMeshHit h;
            bool ok = UnityEngine.AI.NavMesh.SamplePosition(new UnityEngine.Vector3(p.x, 0.05f, p.z), out h, 0.25f, UnityEngine.AI.NavMesh.AllAreas);
            return ok && h.position.y < 0.4f ? "walk" : "blocked";
        };
        bool alongX = b.size.x > b.size.z;
        var side = alongX ? new UnityEngine.Vector3(0f, 0f, b.extents.z + 0.6f) : new UnityEngine.Vector3(b.extents.x + 0.6f, 0f, 0f);
        sb.Append(ch.name + ": centre=" + onGround(b.center) + " sideA=" + onGround(b.center - side) + " sideB=" + onGround(b.center + side) + "\n");
    }
return sb.ToString();
