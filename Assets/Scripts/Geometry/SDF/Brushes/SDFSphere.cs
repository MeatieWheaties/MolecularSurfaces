using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEngine.UI.Image;

namespace MolecularSurfaces.Geometry.SDF
{
    public static partial class SDF
    {
        public static SDFBrush ToSDF(this BoundsSphere sphere)
        {
            return new SDFBrush()
            {
                Guid = Guid.NewGuid(),
                Type = SDFType.Sphere,
                Origin = sphere.Center(),
                Parameters = new float[1] { sphere.Radius }
            };
        }

        public static SDFBrush Sphere(Vector3 origin, float radius)
        {
            return new SDFBrush()
            {
                Guid = Guid.NewGuid(),
                Type = SDFType.Sphere,
                Origin = origin,
                Parameters = new float[1] { radius }
            };
        }

        internal static BoundsBox Boundary_Sphere(this SDFBrush brush)
            => new BoundsBox(brush.Origin - Vector3.one * brush.Parameters[0] / 2, brush.Origin + Vector3.one * brush.Parameters[0] / 2);

        internal static float Iso_Sphere(Vector3 origin, float radius, Vector3 point)
        {
            return Vector3.Magnitude(origin - point) - radius;
        }
    }
}
