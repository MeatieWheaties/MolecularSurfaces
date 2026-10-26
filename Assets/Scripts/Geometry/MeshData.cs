using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

namespace MolecularSurfaces.Geometry
{
    public class MeshData
    {
        public bool UseUVs { get; set; }


        public SubMesh this[int index]
        {
            get => Sub(index);
            set
            {
                if (!SubMesh.ContainsKey(index))
                {
                    SubMesh.Add(index, value);
                }
                else
                {
                    SubMesh[index] = value;
                }
            }
        }

        public IEnumerable<SubMesh> SubMeshes => SubMesh.Values;

        public Dictionary<int, SubMesh> SubMesh = new Dictionary<int, SubMesh>();

        public MeshData(params int[] subMeshIds)
        {
            SubMesh = new Dictionary<int, SubMesh>();
            for (int x = 0; x < subMeshIds.Length; x++)
            {
                SubMesh.Add(subMeshIds[x], new SubMesh(subMeshIds[x]));
            }
        }

        public SubMesh Sub(int id)
        {
            if (SubMesh == null)
                SubMesh = new Dictionary<int, SubMesh>();

            if (!SubMesh.ContainsKey(id))
            {
                SubMesh.Add(id, new SubMesh(id));
            }

            return SubMesh[id];
        }

        public SubMesh Sub(int id, Vector3[] verticies, int[] indicies)
        {
            if (SubMesh == null)
                SubMesh = new Dictionary<int, SubMesh>();

            if (!SubMesh.ContainsKey(id))
            {
                SubMesh.Add(id, new SubMesh(id, verticies, indicies));
            }
            else
            {
                SubMesh[id].Merge(verticies.ToList(), indicies.ToList(), new List<Vector2>());
            }

            return SubMesh[id];
        }

        public MeshData AddTriangle(int subMesh, Vector3 a, Vector3 b, Vector3 c, bool inverted = false)
        {
            Sub(subMesh).AddTriangle(a, b, c, inverted);
            return this;
        }

        public MeshData AddQuad(int subMesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d, bool inverted = false)
        {
            Sub(subMesh).AddQuad(a, b, c, d, inverted);
            return this;
        }

        public MeshData Merge(MeshData other)
        {
            foreach (var subMesh in other.SubMesh.Keys)
            {
                Sub(subMesh).Merge(other[subMesh]);
            }
            return this;
        }

        public Mesh ToMesh()
        {
            var orderedSubMeshes = SubMesh
        .OrderBy(kvp => kvp.Key)
        .Select(kvp => kvp.Value)
        .ToList();

            var mesh = new Mesh();
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.subMeshCount = orderedSubMeshes.Count;

            var allVertices = new List<Vector3>();
            var allUVs = new List<Vector2>();

            foreach (var sm in orderedSubMeshes)
            {
                allVertices.AddRange(sm.Verticies);
                allUVs.AddRange(sm.UVs);
            }

            mesh.SetVertices(allVertices);

            if (allUVs.Count == allVertices.Count)
                mesh.SetUVs(0, allUVs);

            int vertexOffset = 0;

            for (int i = 0; i < orderedSubMeshes.Count; i++)
            {
                var sm = orderedSubMeshes[i];

                var triangles = sm.Triangles
                    .Select(t => t + vertexOffset)
                    .ToArray();

                mesh.SetTriangles(triangles, i, true);

                vertexOffset += sm.Verticies.Count;
            }

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();


            return mesh;
        }
    }

    public class SubMesh
    {
        public int Id { get; set; }
        public List<Vector3> Verticies { get; private set; } = new List<Vector3>();
        public List<int> Triangles { get; private set; } = new List<int>();
        public List<Vector2> UVs { get; private set; } = new List<Vector2>();

        public SubMesh(int subMeshId)
        {
            Id = subMeshId;
            Verticies = new List<Vector3>();
            Triangles = new List<int>();
            UVs = new List<Vector2>();
        }

        public SubMesh(int subMeshId, Vector3[] verticies, int[] indicies)
        {
            Id = subMeshId;
            Verticies = verticies.ToList();
            Triangles = indicies.ToList();
        }

        public SubMesh AddTriangle(Vector3 a, Vector3 b, Vector3 c, bool inverted = false)
        {

            Triangles.Add(Verticies.Count);
            Triangles.Add(Verticies.Count + 1);
            Triangles.Add(Verticies.Count + 2);

            Verticies.Add(a);
            switch (inverted)
            {
                case true:
                    Verticies.Add(c);
                    Verticies.Add(b);
                    break;
                case false:
                default:
                    Verticies.Add(b);
                    Verticies.Add(c);
                    break;
            }

            return this;
        }

        public SubMesh AddQuad(Vector3 bottom_left, Vector3 top_left, Vector3 top_right, Vector3 bottom_right, bool inverted = false)
        {
            AddTriangle(bottom_left, top_right, top_left, inverted);
            AddTriangle(bottom_left, bottom_right, top_right, inverted);

            return this;
        }

        public void AddUVs(IEnumerable<Vector2> uvs)
        {
            UVs.AddRange(uvs);
        }

        public void Merge(SubMesh other)
        {
            var triangleOffset = Verticies.Count;
            Verticies.AddRange(other.Verticies);
            Triangles.AddRange(other.Triangles.Select(s => s + triangleOffset));
            UVs.AddRange(other.UVs);
        }

        public void Merge(List<Vector3> verticies, List<int> indicies, List<Vector2> uvs)
        {
            var triangleOffset = Verticies.Count;
            Verticies.AddRange(verticies);
            Triangles.AddRange(indicies.Select(s => s + triangleOffset));
            UVs.AddRange(uvs);
        }
    }
}
