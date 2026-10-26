using UnityEngine;

namespace MolecularSurfaces.Geometry
{
    public static class Raycasting
    {
        public struct Ray
        {
            public Vector3 Origin;
            public Vector3 Direction;

            public Ray(Vector3 origin, Vector3 direction)
            {
                Origin = origin;
                Direction = direction.normalized;
            }
        }

        public struct RaycastHit
        {
            public Vector3 Point;
            public Vector3 Normal;
            public float Distance;

            public RaycastHit(Vector3 point, Vector3 normal, float distance)
            {
                Point = point;
                Normal = normal;
                Distance = distance;
            }

            public void Draw(Color hit, Color normal)
            {
                Gizmos.color = hit;
                Gizmos.DrawSphere(Point, 0.5f);
                Gizmos.color = normal;
                Gizmos.DrawRay(Point, Normal);
            }
        }

        public static bool Intersects(this Ray ray, IBounds bounds, out RaycastHit hit)
        {
            switch (bounds.Type)
            {
                case BoundsType.Box:
                case BoundsType.Grid:
                    return RaycastBox(ray, bounds.Boundary, out hit);

                case BoundsType.Sphere:
                    return RaycastSphere(ray, (BoundsSphere)bounds, out hit);

                default:
                    hit = default;
                    return false;
            }
        }

        static bool RaycastBox(Ray ray, BoundsBox box, out RaycastHit hit)
        {
            const float epsilon = 0.00001f;

            float tMin = 0f;
            float tMax = float.PositiveInfinity;

            Vector3 normal = Vector3.zero;

            if (!Slab(ray.Origin.x, ray.Direction.x, box.xMin, box.xMax, ref tMin, ref tMax, Vector3.left, Vector3.right, ref normal)
             || !Slab(ray.Origin.y, ray.Direction.y, box.yMin, box.yMax, ref tMin, ref tMax, Vector3.down, Vector3.up, ref normal)
             || !Slab(ray.Origin.z, ray.Direction.z, box.zMin, box.zMax, ref tMin, ref tMax, Vector3.back, Vector3.forward, ref normal))
            {
                hit = default;
                return false;
            }

            float distance = tMin >= 0f ? tMin : tMax;
            if (distance < 0f)
            {
                hit = default;
                return false;
            }

            Vector3 point = ray.Origin + ray.Direction * distance;

            if (normal == Vector3.zero)
            {
                normal = EstimateBoxNormal(point, box, epsilon);
            }

            hit = new RaycastHit(point, normal, distance);
            return true;
        }

        static bool Slab(
            float origin,
            float direction,
            float min,
            float max,
            ref float tMin,
            ref float tMax,
            Vector3 nearNormal,
            Vector3 farNormal,
            ref Vector3 hitNormal)
        {
            const float epsilon = 0.00001f;

            if (Mathf.Abs(direction) < epsilon)
            {
                return origin >= min && origin <= max;
            }

            float inv = 1f / direction;
            float t1 = (min - origin) * inv;
            float t2 = (max - origin) * inv;

            Vector3 candidateNormal = nearNormal;

            if (t1 > t2)
            {
                (t1, t2) = (t2, t1);
                candidateNormal = farNormal;
            }

            if (t1 > tMin)
            {
                tMin = t1;
                hitNormal = candidateNormal;
            }

            if (t2 < tMax)
            {
                tMax = t2;
            }

            return tMin <= tMax;
        }

        static Vector3 EstimateBoxNormal(Vector3 point, BoundsBox box, float epsilon)
        {
            if (Mathf.Abs(point.x - box.xMin) <= epsilon) return Vector3.left;
            if (Mathf.Abs(point.x - box.xMax) <= epsilon) return Vector3.right;
            if (Mathf.Abs(point.y - box.yMin) <= epsilon) return Vector3.down;
            if (Mathf.Abs(point.y - box.yMax) <= epsilon) return Vector3.up;
            if (Mathf.Abs(point.z - box.zMin) <= epsilon) return Vector3.back;
            if (Mathf.Abs(point.z - box.zMax) <= epsilon) return Vector3.forward;

            return Vector3.zero;
        }

        static bool RaycastSphere(Ray ray, BoundsSphere sphere, out RaycastHit hit)
        {
            Vector3 center = new Vector3(sphere.x, sphere.y, sphere.z);
            Vector3 oc = ray.Origin - center;

            float a = Vector3.Dot(ray.Direction, ray.Direction);
            float b = 2f * Vector3.Dot(oc, ray.Direction);
            float c = Vector3.Dot(oc, oc) - sphere.Radius * sphere.Radius;

            float discriminant = b * b - 4f * a * c;
            if (discriminant < 0f)
            {
                hit = default;
                return false;
            }

            float sqrt = Mathf.Sqrt(discriminant);
            float t0 = (-b - sqrt) / (2f * a);
            float t1 = (-b + sqrt) / (2f * a);

            float distance = t0 >= 0f ? t0 : t1;
            if (distance < 0f)
            {
                hit = default;
                return false;
            }

            Vector3 point = ray.Origin + ray.Direction * distance;
            Vector3 normal = (point - center).normalized;

            hit = new RaycastHit(point, normal, distance);
            return true;
        }
    }
}
