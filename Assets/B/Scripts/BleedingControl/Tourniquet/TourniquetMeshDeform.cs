using System.Collections.Generic;
using UnityEngine;

namespace MilTrainSimVR.B.BleedingControl.Tourniquet
{
    // Runtime work copy of one part visual's mesh for limited vertex deformation (band end pull, internal band, securing strap
    // and time patch). The imported sharedMesh is never written: on first use the mesh is copied (the model importer must have
    // Read/Write enabled), the copy is shown, and every deformation is computed from the
    // untouched source vertices. Restore puts the source shape back; destroying the component shows the source mesh again.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    public sealed class TourniquetMeshDeform : MonoBehaviour
    {
        // Deformed position and the rotation applied to the vertex normal and tangent, both in this visual's local space.
        public delegate void VertexMap(Vector3 basePosition, out Vector3 position, out Quaternion rotation);

        MeshFilter filter;
        Mesh source;
        Mesh work;
        Vector3[] baseVertices;
        Vector3[] baseNormals;
        Vector4[] baseTangents;
        Vector3[] vertices;
        Vector3[] normals;
        Vector4[] tangents;

        public bool IsReady => work != null;
        public bool IsDeformed { get; private set; }
        // Incremented on every change of the shown shape.
        public int Version { get; private set; }
        public IReadOnlyList<Vector3> BaseVertices => baseVertices;
        public IReadOnlyList<Vector3> Vertices => vertices;

        public bool TryPrepare(out string error)
        {
            error = null;
            if (work != null)
                return true;

            filter = GetComponent<MeshFilter>();
            source = filter.sharedMesh;
            if (source == null)
                error = $"TourniquetMeshDeform '{name}': MeshFilter has no mesh.";
            else if (!source.isReadable)
                error = $"TourniquetMeshDeform '{name}': mesh '{source.name}' is not readable. Run 'B/Tourniquet/1. Build Prefab and Placement Scene' (enables Read/Write on the B tourniquet model).";
            if (error != null)
                return false;

            baseVertices = source.vertices;
            baseNormals = source.normals;
            baseTangents = source.tangents;
            vertices = (Vector3[])baseVertices.Clone();
            normals = (Vector3[])baseNormals.Clone();
            tangents = (Vector4[])baseTangents.Clone();
            work = Instantiate(source);
            work.name = source.name + " (TQ work copy)";
            work.MarkDynamic();
            filter.sharedMesh = work;
            return true;
        }

        // Every source vertex index used by one sub-mesh, without duplicates.
        public int[] SubMeshVertices(int subMesh)
        {
            if (source == null || subMesh < 0 || subMesh >= source.subMeshCount)
                return System.Array.Empty<int>();
            var set = new HashSet<int>(source.GetIndices(subMesh));
            var result = new int[set.Count];
            set.CopyTo(result);
            return result;
        }

        // Deforms the listed vertices (all when null) from the source shape; the others keep their source shape.
        public void Apply(VertexMap map, int[] indices = null)
        {
            if (work == null)
                return;

            var count = indices?.Length ?? baseVertices.Length;
            for (var k = 0; k < count; k++)
            {
                var i = indices?[k] ?? k;
                map(baseVertices[i], out var position, out var rotation);
                vertices[i] = position;
                if (normals.Length > i)
                    normals[i] = rotation * baseNormals[i];
                if (tangents.Length > i)
                {
                    var t = rotation * (Vector3)baseTangents[i];
                    tangents[i] = new Vector4(t.x, t.y, t.z, baseTangents[i].w);
                }
            }

            Upload();
            IsDeformed = true;
        }

        public void Restore()
        {
            if (work == null || !IsDeformed)
                return;
            System.Array.Copy(baseVertices, vertices, baseVertices.Length);
            System.Array.Copy(baseNormals, normals, baseNormals.Length);
            System.Array.Copy(baseTangents, tangents, baseTangents.Length);
            Upload();
            IsDeformed = false;
        }

        void Upload()
        {
            work.SetVertices(vertices);
            if (normals.Length == vertices.Length)
                work.SetNormals(normals);
            if (tangents.Length == vertices.Length)
                work.SetTangents(tangents);
            work.RecalculateBounds();
            Version++;
        }

        void OnDestroy()
        {
            if (work == null)
                return;
            if (filter != null && filter.sharedMesh == work)
                filter.sharedMesh = source;
            Destroy(work);
            work = null;
        }
    }
}
