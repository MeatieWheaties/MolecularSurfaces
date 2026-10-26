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
        public static SDFBrush ToSDF(this BoundsBox box)
        {
            return new SDFBrush()
            {
                Guid = Guid.NewGuid(),
                Type = SDFType.Box,
                Origin = box.Center(),
                Parameters = new float[3]
                {
                    (box.xMax - box.xMin) / 2,
                    (box.yMax - box.yMin) / 2,
                    (box.zMax - box.zMin) / 2
                }
            };
        }

        public static SDFBrush ToSDF(this BoundsGrid grid)
        {
            return new SDFBrush()
            {
                Guid = Guid.NewGuid(),
                Type = SDFType.Box,
                Origin = grid.Center(),
                Parameters = new float[3]
                {
                    (grid.xMax - grid.xMin) / 2,
                    (grid.yMax - grid.yMin) / 2,
                    (grid.zMax - grid.zMin) / 2
                }
            };
        }

        public static SDFBrush Box(Vector3 center, float x, float y, float z)
        {
            return new SDFBrush()
            {
                Guid = Guid.NewGuid(),
                Type = SDFType.Box,
                Origin = center,
                Parameters = new float[3] { x, y, z }
            };
        }

        internal static BoundsBox Boundary_Box(this SDFBrush brush)
            => new BoundsBox(
                brush.Origin - new Vector3(brush[0], brush[1], brush[2]),
                brush.Origin + new Vector3(brush[0], brush[1], brush[2]));

        internal static float Iso_Box(Vector3 center, float x, float y, float z, Vector3 point)
        {
            var _q = new Vector3(Mathf.Abs(center.x), Mathf.Abs(center.y), Mathf.Abs(center.z)) - point;
            return
                Vector3.Magnitude(
                    new Vector3(
                        Mathf.Max(_q.x, 0),
                        Mathf.Max(_q.y, 0),
                        Mathf.Max(_q.z, 0)
                        )
                    )
                +
                Mathf.Min(Mathf.Max(_q.x, Mathf.Max(_q.y, _q.z)), 0);
        }
    }
}
