using System.Collections.Generic;
using UnityEngine;

namespace MilTrainSimVR.B.BleedingControl.PressureBandage
{
    // One cross-section of a ribbon: centre, unit tangent along the length, unit front normal, unit width direction
    // (= cross(tangent, normal)) and the U texture coordinate (length / band width, as in the model strip). Builders fill the
    // position, the wanted width and U; RibbonMeshWriter.CompleteFrames derives tangent, width and normal.
    internal struct RibbonStation
    {
        public Vector3 position;
        public Vector3 tangent;
        public Vector3 normal;
        public Vector3 width;
        public float u;
    }

    // Writes a ribbon into a runtime work mesh, following the model strip contract (PB_BandageStrip / PB_WrapStrip /
    // PB_BandageEnd: zero-thickness surface, V = 0..1 across the width, U = length / width with Repeat wrap). The ribbon is
    // two sheets of separate vertices, a front sheet facing each station's normal and a back sheet facing away, so a
    // front-face-only material lights both sides with their own normals (URP Lit does not flip back-face normals). Normals are
    // set from the stations, tangents and bounds are recalculated. Lists are reused; no allocation per write beyond the mesh.
    internal sealed class RibbonMeshWriter
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int> triangles = new List<int>();

        // toLocal: world to the renderer's space (rotation and translation; the bandage parts are not scaled).
        public void Write(Mesh mesh, List<RibbonStation> stations, float halfWidth, int widthSegments, Matrix4x4 toLocal)
        {
            mesh.Clear();
            var count = stations.Count;
            if (count < 2 || widthSegments < 1)
                return;
            vertices.Clear();
            normals.Clear();
            uvs.Clear();
            triangles.Clear();

            var columns = widthSegments + 1;
            var first = stations[0];
            var flip = Vector3.Dot(Vector3.Cross(toLocal.MultiplyVector(first.width), toLocal.MultiplyVector(first.tangent)),
                                   toLocal.MultiplyVector(first.normal)) < 0f;
            for (var side = 0; side < 2; side++)
            {
                var sign = side == 0 ? 1f : -1f;
                for (var i = 0; i < count; i++)
                {
                    var s = stations[i];
                    var p = toLocal.MultiplyPoint3x4(s.position);
                    var w = toLocal.MultiplyVector(s.width).normalized;
                    var n = toLocal.MultiplyVector(s.normal).normalized * sign;
                    for (var j = 0; j < columns; j++)
                    {
                        var t = (float)j / widthSegments;
                        vertices.Add(p + w * Mathf.Lerp(-halfWidth, halfWidth, t));
                        normals.Add(n);
                        uvs.Add(new Vector2(s.u, t));
                    }
                }
            }

            // Quad (a, b = next across, c = next along, d): (a, b, c) faces cross(width, tangent); the front sheet must face its
            // normal and the back sheet the opposite way.
            var sheet = count * columns;
            for (var side = 0; side < 2; side++)
            {
                var reverse = flip ^ (side == 1);
                var offset = side * sheet;
                for (var i = 0; i < count - 1; i++)
                {
                    for (var j = 0; j < widthSegments; j++)
                    {
                        var a = offset + i * columns + j;
                        var b = a + 1;
                        var c = a + columns;
                        var d = c + 1;
                        if (!reverse)
                        {
                            triangles.Add(a); triangles.Add(b); triangles.Add(c);
                            triangles.Add(b); triangles.Add(d); triangles.Add(c);
                        }
                        else
                        {
                            triangles.Add(a); triangles.Add(c); triangles.Add(b);
                            triangles.Add(b); triangles.Add(c); triangles.Add(d);
                        }
                    }
                }
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
        }

        // Frames for positions along a ribbon from each station's wanted width: tangent from neighbours, width = the wanted width
        // made perpendicular to the tangent, normal = cross(width, tangent) (so width = cross(tangent, normal)). A station whose
        // wanted width is parallel to the tangent keeps the previous width, so the band does not flip.
        public static void CompleteFrames(List<RibbonStation> stations)
        {
            var previousWidth = Vector3.zero;
            for (var i = 0; i < stations.Count; i++)
            {
                var s = stations[i];
                var ahead = stations[Mathf.Min(i + 1, stations.Count - 1)].position;
                var behind = stations[Mathf.Max(i - 1, 0)].position;
                var tangent = ahead - behind;
                s.tangent = tangent.sqrMagnitude > 1e-12f ? tangent.normalized : (i > 0 ? stations[i - 1].tangent : Vector3.forward);
                var width = s.width - s.tangent * Vector3.Dot(s.width, s.tangent);
                if (width.sqrMagnitude < 1e-8f)
                    width = previousWidth.sqrMagnitude > 0f ? previousWidth : Vector3.Cross(s.tangent, Vector3.up);
                s.width = width.normalized;
                s.normal = Vector3.Cross(s.width, s.tangent).normalized;
                previousWidth = s.width;
                stations[i] = s;
            }
        }
    }

    // Flat, two-sided marker meshes (end attach target, tape frame, wrap zone outline): every triangle is added once facing its normal
    // and once facing away, so markers read from both sides with any culling. No UVs (plain colour materials).
    internal sealed class FlatMeshWriter
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<int> triangles = new List<int>();

        public void Clear()
        {
            vertices.Clear();
            normals.Clear();
            triangles.Clear();
        }

        public void AddTriangle(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 normal)
        {
            if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), normal) < 0f)
                (p1, p2) = (p2, p1);
            Add(p0, p1, p2, normal);
            Add(p0, p2, p1, -normal);
        }

        public void AddQuad(Vector3 centre, Vector3 halfA, Vector3 halfB, Vector3 normal)
        {
            AddTriangle(centre - halfA - halfB, centre + halfA - halfB, centre + halfA + halfB, normal);
            AddTriangle(centre - halfA - halfB, centre + halfA + halfB, centre - halfA + halfB, normal);
        }

        public void Write(Mesh mesh)
        {
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateBounds();
        }

        void Add(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 normal)
        {
            var start = vertices.Count;
            vertices.Add(p0);
            vertices.Add(p1);
            vertices.Add(p2);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }

        // Attach target in the local XY plane facing +Z: an outer ring of the attach radius and a small centre ring (the mark).
        public static void Target(Mesh mesh, FlatMeshWriter writer, float radius, float line, float centre, int segments)
        {
            writer.Clear();
            foreach (var (inner, outer) in new[] { (radius - line, radius), (Mathf.Max(0f, centre - line), centre) })
            {
                for (var i = 0; i < segments; i++)
                {
                    var a0 = Mathf.PI * 2f * i / segments;
                    var a1 = Mathf.PI * 2f * (i + 1) / segments;
                    var d0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f);
                    var d1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f);
                    writer.AddTriangle(d0 * inner, d0 * outer, d1 * outer, Vector3.forward);
                    if (inner > 0f)
                        writer.AddTriangle(d0 * inner, d1 * outer, d1 * inner, Vector3.forward);
                }
            }
            writer.Write(mesh);
        }

        // Rectangle outline in the local XY plane facing +Z: size along X and Y, line width.
        public static void Frame(Mesh mesh, FlatMeshWriter writer, Vector2 size, float line)
        {
            writer.Clear();
            var hx = size.x * 0.5f;
            var hy = size.y * 0.5f;
            var l = line * 0.5f;
            writer.AddQuad(new Vector3(0f, hy, 0f), new Vector3(hx + l, 0f, 0f), new Vector3(0f, l, 0f), Vector3.forward);
            writer.AddQuad(new Vector3(0f, -hy, 0f), new Vector3(hx + l, 0f, 0f), new Vector3(0f, l, 0f), Vector3.forward);
            writer.AddQuad(new Vector3(hx, 0f, 0f), new Vector3(l, 0f, 0f), new Vector3(0f, hy - l, 0f), Vector3.forward);
            writer.AddQuad(new Vector3(-hx, 0f, 0f), new Vector3(l, 0f, 0f), new Vector3(0f, hy - l, 0f), Vector3.forward);
            writer.Write(mesh);
        }

        // Box outline centred on the origin: each of its 12 edges as two crossed strips of the line width (both faces), so it
        // reads from any side.
        public static void BoxEdges(Mesh mesh, FlatMeshWriter writer, Vector3 size, float line)
        {
            writer.Clear();
            var half = size * 0.5f;
            for (var axis = 0; axis < 3; axis++)
            {
                var a = Vector3.zero;
                var b = Vector3.zero;
                var c = Vector3.zero;
                a[axis] = 1f;
                b[(axis + 1) % 3] = 1f;
                c[(axis + 2) % 3] = 1f;
                var hb = half[(axis + 1) % 3];
                var hc = half[(axis + 2) % 3];
                for (var i = 0; i < 4; i++)
                {
                    var centre = b * ((i & 1) == 0 ? -hb : hb) + c * ((i & 2) == 0 ? -hc : hc);
                    writer.AddQuad(centre, a * half[axis], b * (line * 0.5f), c);
                    writer.AddQuad(centre, a * half[axis], c * (line * 0.5f), b);
                }
            }
            writer.Write(mesh);
        }
    }
}
