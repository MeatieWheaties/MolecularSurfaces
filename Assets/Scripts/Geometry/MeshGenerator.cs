using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

namespace MolecularSurfaces.Geometry
{
    public static class MeshGenerator
    {
        public static Mesh NewMesh(Vector3[] triangleBuffer, Vector2[] uvBuffer, int[] indexBuffer)
        {
            var mesh = new Mesh();
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.subMeshCount = 1;

            mesh.SetVertices(triangleBuffer);

            mesh.SetUVs(0, uvBuffer);

            mesh.SetTriangles(indexBuffer, 0, true);

            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            return mesh;
        }
    }
}
