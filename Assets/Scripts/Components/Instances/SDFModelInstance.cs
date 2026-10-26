using MolecularSurfaces.Geometry;
using MolecularSurfaces.Geometry.SDF;
using MolecularSurfaces.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Ray = MolecularSurfaces.Geometry.Raycasting.Ray;
using RaycastHit = MolecularSurfaces.Geometry.Raycasting.RaycastHit;

namespace MolecularSurfaces.Components.Instances
{
    [ExecuteAlways]
    public class SDFModelInstance : Instance<SDFModel>
    {
        public IsoSurface_Size IsoSize = IsoSurface_Size.x16;

        public bool AutoBuild;
        public bool Build;

        public float Scale = 1f;

        public Color Color = Color.white;
        public bool DebugBounds;

        private void Update()
        {
            if (Build && Value != null)
            {
                Build = false;
                BuildSDFModel();
            }
        }

        #region CSG
        public void Stamp(params SDFBrush[] brush)
        {
            foreach (var _b in brush)
            {
                Value.Stamp(_b);
            }

            if (Build || AutoBuild)
            {
                BuildSDFModel();
                Build = false;
            }
        }

        public void Clip(params SDFBrush[] brush)
        {
            foreach (var _b in brush)
            {
                Value.Clip(_b);
            }

            if (Build || AutoBuild)
            {
                BuildSDFModel();
                Build = false;
            }
        }

        public void Replace(params SDFBrush[] brush)
        {
            foreach (var _b in brush)
            {
                Value.Replace(_b);
            }

            if (Build || AutoBuild)
            {
                BuildSDFModel();
                Build = false;
            }
        }

        public void Fill(params SDFBrush[] brush)
        {
            foreach (var _b in brush)
            {
                Value.Fill(_b);
            }

            if (Build || AutoBuild)
            {
                BuildSDFModel();
                Build = false;
            }
        }
        #endregion

        Transform _Render => transform.Cursor("[Render]");

        IsoSurfaceRender _Rendered = new IsoSurfaceRender
        {
            Chunk = new IsoSurface[0]
        };

        /// <summary>
        /// Queries the model's local bounds tree with a world-space ray.
        /// The tree remains local, so moving or rotating this GameObject does not
        /// require rebuilding it.
        /// </summary>
        public IEnumerable<(Guid guid, RaycastHit hit)> Query(
            Ray worldRay,
            HashSet<Guid> searchSet = null,
            HashSet<Guid> ignore = null)
        {
            if (Value == null)
                return Array.Empty<(Guid guid, RaycastHit hit)>();

            var localRay = new Ray(
                transform.InverseTransformPoint(worldRay.Origin),
                transform.InverseTransformDirection(worldRay.Direction));

            return Value.Space.Query(localRay, searchSet, ignore)
                .Select(result => (result.guid, ToWorldHit(worldRay, result.hit)))
                .ToArray();
        }

        public BoundsBox WorldBoundary
        {
            get
            {
                if (Value == null)
                    return default;

                var bounds = Value.Space.Boundary;
                return new BoundsBox(
                    transform.TransformPoint(new Vector3(bounds.xMin, bounds.yMin, bounds.zMin)),
                    transform.TransformPoint(new Vector3(bounds.xMin, bounds.yMin, bounds.zMax)),
                    transform.TransformPoint(new Vector3(bounds.xMin, bounds.yMax, bounds.zMin)),
                    transform.TransformPoint(new Vector3(bounds.xMin, bounds.yMax, bounds.zMax)),
                    transform.TransformPoint(new Vector3(bounds.xMax, bounds.yMin, bounds.zMin)),
                    transform.TransformPoint(new Vector3(bounds.xMax, bounds.yMin, bounds.zMax)),
                    transform.TransformPoint(new Vector3(bounds.xMax, bounds.yMax, bounds.zMin)),
                    transform.TransformPoint(new Vector3(bounds.xMax, bounds.yMax, bounds.zMax)));
            }
        }

        private RaycastHit ToWorldHit(Ray worldRay, RaycastHit localHit)
        {
            var point = transform.TransformPoint(localHit.Point);
            var normalMatrix = transform.localToWorldMatrix.inverse.transpose;
            var normal = normalMatrix.MultiplyVector(localHit.Normal).normalized;
            return new RaycastHit(point, normal, Vector3.Distance(worldRay.Origin, point));
        }

        public void DrawBounds(Color color)
        {
            if (Value == null)
                return;

            var previousMatrix = Gizmos.matrix;
            try
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Value.Space.Draw(color);
            }
            finally
            {
                Gizmos.matrix = previousMatrix;
            }
        }

        public void DebugBoxSelect(
            BoundsBox worldBox,
            Color boxColor,
            Color hitColor,
            Color missColor,
            bool hideMiss = false)
        {
            if (Value == null)
                return;

            var localBox = WorldBoxToLocalAabb(worldBox);
            var bounds = Value.Space.Bounds;
            var hits = Value.Space.Query(localBox)
                .Where(id => IntersectsWorldBox(bounds[id], worldBox))
                .ToHashSet();

            DrawQueryResults(hits, hitColor, missColor, hideMiss);
            worldBox.Draw(boxColor);
        }

        public void DebugSphereSelect(
            BoundsSphere worldSphere,
            Color sphereColor,
            Color hitColor,
            Color missColor,
            bool hideMiss = false)
        {
            if (Value == null)
                return;

            var worldCenter = new Vector3(worldSphere.x, worldSphere.y, worldSphere.z);
            var localCenter = transform.InverseTransformPoint(worldCenter);
            var localRadius = Mathf.Max(
                transform.InverseTransformVector(Vector3.right * worldSphere.Radius).magnitude,
                transform.InverseTransformVector(Vector3.up * worldSphere.Radius).magnitude,
                transform.InverseTransformVector(Vector3.forward * worldSphere.Radius).magnitude);
            var localSphere = new BoundsSphere(localCenter, localRadius);
            var hits = Value.Space.Query(localSphere).ToHashSet();

            DrawQueryResults(hits, hitColor, missColor, hideMiss);
            worldSphere.Draw(sphereColor);
        }

        public void DebugRaySelect(
            Ray worldRay,
            Color rayColor,
            Color hitColor,
            Color missColor,
            bool hideMiss = false)
        {
            if (Value == null)
                return;

            var hits = Query(worldRay).ToArray();
            DrawQueryResults(hits.Select(hit => hit.guid).ToHashSet(), hitColor, missColor, hideMiss);

            foreach (var hit in hits)
                hit.hit.Draw(rayColor, Color.white);

            Gizmos.color = rayColor;
            Gizmos.DrawRay(worldRay.Origin, worldRay.Direction);
        }

        private void DrawQueryResults(HashSet<Guid> hits, Color hitColor, Color missColor, bool hideMiss)
        {
            var previousMatrix = Gizmos.matrix;
            try
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                foreach (var bounds in Value.Space.Bounds)
                {
                    if (hits.Contains(bounds.Key))
                        bounds.Value.Draw(hitColor);
                    else if (!hideMiss)
                        bounds.Value.Draw(missColor);
                }
            }
            finally
            {
                Gizmos.matrix = previousMatrix;
            }
        }

        private BoundsBox WorldBoxToLocalAabb(BoundsBox box)
        {
            return new BoundsBox(
                transform.InverseTransformPoint(new Vector3(box.xMin, box.yMin, box.zMin)),
                transform.InverseTransformPoint(new Vector3(box.xMin, box.yMin, box.zMax)),
                transform.InverseTransformPoint(new Vector3(box.xMin, box.yMax, box.zMin)),
                transform.InverseTransformPoint(new Vector3(box.xMin, box.yMax, box.zMax)),
                transform.InverseTransformPoint(new Vector3(box.xMax, box.yMin, box.zMin)),
                transform.InverseTransformPoint(new Vector3(box.xMax, box.yMin, box.zMax)),
                transform.InverseTransformPoint(new Vector3(box.xMax, box.yMax, box.zMin)),
                    transform.InverseTransformPoint(new Vector3(box.xMax, box.yMax, box.zMax)));
        }

        private bool IntersectsWorldBox(IBounds localBounds, BoundsBox worldBox)
        {
            if (localBounds.Type == BoundsType.Sphere)
            {
                var sphere = (BoundsSphere)localBounds;
                var center = transform.TransformPoint(new Vector3(sphere.x, sphere.y, sphere.z));
                var scale = transform.lossyScale;
                var radius = sphere.Radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                var closest = worldBox.Clamp(center);
                return (center - closest).sqrMagnitude <= radius * radius;
            }

            return IntersectsWorldBox(localBounds.Boundary, worldBox);
        }

        private bool IntersectsWorldBox(BoundsBox localBox, BoundsBox worldBox)
        {
            const float epsilon = 0.00001f;

            var localCenter = localBox.Center();
            var localExtents = localBox.Size * 0.5f;
            var worldCenter = worldBox.Center();
            var worldExtents = worldBox.Size * 0.5f;
            var scale = transform.lossyScale;

            var axes = new[]
            {
                transform.TransformDirection(Vector3.right).normalized,
                transform.TransformDirection(Vector3.up).normalized,
                transform.TransformDirection(Vector3.forward).normalized
            };
            var extents = new[]
            {
                localExtents.x * Mathf.Abs(scale.x),
                localExtents.y * Mathf.Abs(scale.y),
                localExtents.z * Mathf.Abs(scale.z)
            };
            var targetExtents = new[] { worldExtents.x, worldExtents.y, worldExtents.z };
            var targetAxes = new[] { Vector3.right, Vector3.up, Vector3.forward };
            var rotation = new float[3, 3];
            var absoluteRotation = new float[3, 3];

            for (var i = 0; i < 3; i++)
            {
                for (var j = 0; j < 3; j++)
                {
                    rotation[i, j] = Vector3.Dot(axes[i], targetAxes[j]);
                    absoluteRotation[i, j] = Mathf.Abs(rotation[i, j]) + epsilon;
                }
            }

            var centerDelta = worldCenter - transform.TransformPoint(localCenter);
            var translation = new[]
            {
                Vector3.Dot(centerDelta, axes[0]),
                Vector3.Dot(centerDelta, axes[1]),
                Vector3.Dot(centerDelta, axes[2])
            };

            // The three axes of the transformed model box.
            for (var i = 0; i < 3; i++)
            {
                var targetRadius = targetExtents[0] * absoluteRotation[i, 0]
                    + targetExtents[1] * absoluteRotation[i, 1]
                    + targetExtents[2] * absoluteRotation[i, 2];
                if (Mathf.Abs(translation[i]) > extents[i] + targetRadius)
                    return false;
            }

            // The three axes of the world-space selector box.
            for (var j = 0; j < 3; j++)
            {
                var projectedTranslation = Mathf.Abs(
                    translation[0] * rotation[0, j]
                    + translation[1] * rotation[1, j]
                    + translation[2] * rotation[2, j]);
                var modelRadius = extents[0] * absoluteRotation[0, j]
                    + extents[1] * absoluteRotation[1, j]
                    + extents[2] * absoluteRotation[2, j];
                if (projectedTranslation > modelRadius + targetExtents[j])
                    return false;
            }

            // The nine cross products between model-box and selector-box axes.
            for (var i = 0; i < 3; i++)
            {
                var i1 = (i + 1) % 3;
                var i2 = (i + 2) % 3;
                for (var j = 0; j < 3; j++)
                {
                    var j1 = (j + 1) % 3;
                    var j2 = (j + 2) % 3;
                    var projectedTranslation = Mathf.Abs(
                        translation[i2] * rotation[i1, j]
                        - translation[i1] * rotation[i2, j]);
                    var modelRadius = extents[i1] * absoluteRotation[i2, j]
                        + extents[i2] * absoluteRotation[i1, j];
                    var targetRadius = targetExtents[j1] * absoluteRotation[i, j2]
                        + targetExtents[j2] * absoluteRotation[i, j1];
                    if (projectedTranslation > modelRadius + targetRadius)
                        return false;
                }
            }

            return true;
        }

        void BuildSDFModel()
        {
            _Render.Purge<MeshInstance>();

            _Rendered = Value.IsoSurface(IsoSize);

            foreach (var _chunk in _Rendered.Chunk)
            {
                var _instance = _Render.Create<MeshInstance>($"{_chunk.Origin.ToString()}", _chunk.ToMesh(Scale));
                _instance.SetDebugMaterials(Color);
                _instance.Render();
            }
        }

        public void ShowModel(bool mode)
        {
            _Render.GetComponent<MeshInstance>().enabled = mode;
        }

        private void OnDrawGizmos()
        {
            if (DebugBounds)
                DrawBounds(Color.black);

            //foreach(var chunk in _Rendered.Chunk)
            //{
            //    chunk.Bounds.Draw(Color.black);
            //}
        }
    }
}
