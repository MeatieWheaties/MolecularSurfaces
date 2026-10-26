using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SocialPlatforms;
using UnityEngine.UIElements;
using static MolecularSurfaces.Geometry.Raycasting;
using Ray = MolecularSurfaces.Geometry.Raycasting.Ray;
using RaycastHit = MolecularSurfaces.Geometry.Raycasting.RaycastHit;

namespace MolecularSurfaces.Geometry
{
    public enum BoundsType
    {
        Box,
        Grid,
        Sphere
    }

    public struct BoundsRef
    {
        public BoundsType Type;

        public BoundsBox AsBox;
        public BoundsGrid AsGrid;
        public BoundsSphere AsSphere;

        public readonly IBounds Resolve()
        {
            switch (Type)
            {
                case BoundsType.Box: return AsBox;
                case BoundsType.Grid: return AsGrid;
                case BoundsType.Sphere: return AsSphere;
                default: return default;
            }
        }
    }

    public interface IBounds
    {
        public BoundsBox Boundary { get; }
        public BoundsType Type { get; }
        public bool Contains(Vector3 point);

        public float _Radius();

        public Vector3 Clamp(Vector3 point);

        public void Draw(Color color);
    }

    public struct BoundsBox : IBounds
    {
        public float xMin { get; }
        public float xMax { get; }


        public float yMin { get; }
        public float yMax { get; }


        public float zMin { get; }
        public float zMax { get; }

        public Vector3 Size => new Vector3(
            Mathf.Abs(xMax - xMin),
            Mathf.Abs(yMax - yMin),
            Mathf.Abs(zMax - zMin)
            );

        public Vector3 Min => new Vector3(xMin, yMin, zMin);
        public Vector3 Max => new Vector3(xMax, yMax, zMax);

        public BoundsBox Boundary => this;

        public BoundsType Type => BoundsType.Box;

        public BoundsBox(float xMin, float xMax, float yMin, float yMax, float zMin, float zMax)
        {
            this.xMin = xMin;
            this.xMax = xMax;
            this.yMin = yMin;
            this.yMax = yMax;
            this.zMin = zMin;
            this.zMax = zMax;
        }

        public BoundsBox(params Vector3[] containedPoints)
        {
            if(containedPoints == null || containedPoints.Length == 0)
            {
                xMin = 0;
                xMax = 0;
                yMin = 0;
                yMax = 0;
                zMin = 0;
                zMax = 0;
            } else
            {
                this.xMin = containedPoints.Min(p => p.x);
                this.xMax = containedPoints.Max(p => p.x);

                this.yMin = containedPoints.Min(p => p.y);
                this.yMax = containedPoints.Max(p => p.y);

                this.zMin = containedPoints.Min(p => p.z);
                this.zMax = containedPoints.Max(p => p.z);
            }
        }

        public bool Contains(Vector3 point)
        {
            return point.x >= xMin && point.x <= xMax
                && point.y >= yMin && point.y <= yMax
                && point.z >= zMin && point.z <= zMax;
        }

        public static Color DEBUG_Color = Color.white;

        public BoundsBox Merge(BoundsBox box)
        {
            return new BoundsBox(Min, Max, box.Min, box.Max);
        }

        public Vector3 Clamp(Vector3 point)
        {
            return new Vector3(
                Mathf.Clamp(point.x, xMin, xMax),
                Mathf.Clamp(point.y, yMin, yMax),
                Mathf.Clamp(point.z, zMin, zMax)
                );
        }

        public float _Radius() => 0;
        public void Draw(Color color)
        {
            Gizmos.color = color;
            this.DrawBox();
        }
    }

    public struct BoundsGrid : IBounds
    {
        public int xMin { get; }
        public int xMax { get; }


        public int yMin { get; }
        public int yMax { get; }


        public int zMin { get; }
        public int zMax { get; }

        public Vector3Int Min => new Vector3Int(xMin, yMin, zMin);
        public Vector3Int Max => new Vector3Int(xMax, yMax, zMax);

        public Vector3Int Size => new Vector3Int(
            Mathf.Abs(xMax - xMin),
            Mathf.Abs(yMax - yMin),
            Mathf.Abs(zMax - zMin)
            );

        public BoundsBox Boundary => new BoundsBox(xMin, xMax, yMin, yMax, zMin, zMax);

        public BoundsType Type => BoundsType.Box;

        public BoundsGrid(int xMin, int xMax, int yMin, int yMax, int zMin, int zMax)
        {
            this.xMin = xMin;
            this.xMax = xMax;
            this.yMin = yMin;
            this.yMax = yMax;
            this.zMin = zMin;
            this.zMax = zMax;
        }

        public BoundsGrid(params Vector3Int[] containedPoints)
        {
            if (containedPoints == null || containedPoints.Length == 0)
            {
                xMin = 0;
                xMax = 0;
                yMin = 0;
                yMax = 0;
                zMin = 0;
                zMax = 0;
            }
            else
            {
                this.xMin = containedPoints.Min(p => p.x);
                this.xMax = containedPoints.Max(p => p.x);

                this.yMin = containedPoints.Min(p => p.y);
                this.yMax = containedPoints.Max(p => p.y);

                this.zMin = containedPoints.Min(p => p.z);
                this.zMax = containedPoints.Max(p => p.z);
            }
        }

        public bool Contains(Vector3 point)
        {
            return point.x >= xMin && point.x <= xMax
                && point.y >= yMin && point.y <= yMax
                && point.z >= zMin && point.z <= zMax;
        }

        public Vector3 Clamp(Vector3 point)
        {
            return new Vector3(
                Mathf.Clamp(point.x, xMin, xMax),
                Mathf.Clamp(point.y, yMin, yMax),
                Mathf.Clamp(point.z, zMin, zMax)
                );
        }

        public float _Radius() => 0;

        public static Color DEBUG_Color = Color.white;
        public void Draw(Color color)
        {
            Gizmos.color = color;
            this.DrawBox();
        }
    }

    public static class BoundsGridF
    {

        public static void AtEach_Local(this BoundsGrid grid, Action<int, Vector3Int> action)
        {
            int _index = 0;
            for (int x = 0; x < grid.Size.x; x++)
                for (int y = 0; y < grid.Size.y; y++)
                    for (int z = 0; z < grid.Size.z; z++)
                    {
                        action(_index++, new Vector3Int(x, y, z));
                    }
        }

        public static BoundsGrid ToGrid(this BoundsBox box)
        {
            return new BoundsGrid(Vector3Int.FloorToInt(box.Min), Vector3Int.CeilToInt(box.Max));
        }
    }

    public struct BoundsSphere : IBounds
    {
        public float x { get; }
        public float y { get; }
        public float z { get; }

        public float Radius { get; }
        public float _Radius() => Radius;

        public BoundsBox Boundary => new BoundsBox(x - Radius, x + Radius, y - Radius, y + Radius, z - Radius, z + Radius);

        public BoundsType Type => BoundsType.Sphere;

        public BoundsSphere(float x, float y, float z, float radius)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.Radius = radius;

        }

        public BoundsSphere(Vector3 center, float radius)
        {
            this.x = center.x;
            this.y = center.y;
            this.z = center.z;
            this.Radius = radius;
        }

        public bool Contains(Vector3 point)
        {
            var dx = point.x - x;
            var dy = point.y - y;
            var dz = point.z - z;
            return dx * dx + dy * dy + dz * dz <= Radius * Radius;
        }

        public Vector3 Clamp(Vector3 point)
        {
            if (Contains(point)) return point;

            var _center = new Vector3(x, y, z);

            //TODO: Untested
            return _center + point * (Radius / point.magnitude);
        }


        public static Color DEBUG_Color = Color.white;
        public void Draw(Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawWireSphere(new Vector3(x, y, z), Radius);
        }
    }

    public static class BoundsF
    {
        public static void DrawBox(this IBounds bounds)
        {
            Gizmos.DrawWireCube(bounds.Center(), 
                new Vector3(
                    bounds.Boundary.xMax - bounds.Boundary.xMin,
                    bounds.Boundary.yMax - bounds.Boundary.yMin,
                    bounds.Boundary.zMax - bounds.Boundary.zMin));
        }

        public static void DrawBox(this BoundsBox bounds)
        {
            Gizmos.DrawWireCube(bounds.Center(),
                new Vector3(
                    bounds.xMax - bounds.xMin,
                    bounds.yMax - bounds.yMin,
                    bounds.zMax - bounds.zMin));
        }

        public static void DrawBox(this BoundsGrid bounds)
        {
            Gizmos.DrawWireCube(bounds.Center(),
                new Vector3(
                    bounds.xMax - bounds.xMin,
                    bounds.yMax - bounds.yMin,
                    bounds.zMax - bounds.zMin));
        }

        public static bool Intersects(this IBounds bounds, IBounds other)
        {
            switch (bounds.Type)
            {
                case BoundsType.Box:
                    switch (other.Type)
                    {
                        case BoundsType.Box:
                        case BoundsType.Grid:
                            return Intersect_Box_Box(ref bounds, ref other);
                        case BoundsType.Sphere:
                            return Intersect_Box_Sphere(ref bounds, ref other);
                        default: return false;
                    }
                case BoundsType.Sphere:
                    switch (other.Type)
                    {
                        case BoundsType.Box:
                        case BoundsType.Grid:
                            return Intersect_Box_Sphere(ref other, ref bounds);
                        case BoundsType.Sphere:
                            return Intersect_Sphere_Sphere(ref bounds, ref other);
                        default: return false;
                    }
                default:
                    return false;
            }
        }

        public static Vector3 Center(this IBounds bounds)
        {
            return new Vector3(
                (bounds.Boundary.xMax + bounds.Boundary.xMin) / 2,
                (bounds.Boundary.yMax + bounds.Boundary.yMin) / 2,
                (bounds.Boundary.zMax + bounds.Boundary.zMin) / 2);
        }

        static bool Intersect_Box_Box(ref IBounds a, ref IBounds b)
        {
            return (a.Boundary.xMax >= b.Boundary.xMin) && (a.Boundary.xMin <= b.Boundary.xMax)
                && (a.Boundary.yMax >= b.Boundary.yMin) && (a.Boundary.yMin <= b.Boundary.yMax)
                && (a.Boundary.zMax >= b.Boundary.zMin) && (a.Boundary.zMin <= b.Boundary.zMax);
        }

        static bool Intersect_Box_Sphere(ref IBounds box, ref IBounds sphere)
        {
            var _center = sphere.Center();
            var _closest = box.Boundary.Clamp(_center);
            var _dist = (_center - _closest).sqrMagnitude;
            var _radius = sphere._Radius();
            return _dist <= (_radius * _radius);
        }

        static bool Intersect_Sphere_Sphere(ref IBounds a, ref IBounds b)
        {
            var _r = a._Radius() + b._Radius();
            var _test = (a.Center() - b.Center()).sqrMagnitude;
            var _test_2 = (_r* _r);
            return (a.Center() - b.Center()).sqrMagnitude <= (_r * _r);
        }
    }

    public class BoundsSpace
    {
        public BoundsBox Boundary { get; private set; }

        public Dictionary<Guid, IBounds> Bounds
        {
            get
            {
                var _result = new Dictionary<Guid, IBounds>();

                foreach(var _at in _At)
                {
                    _result.Add(_at.Key, _Bounds[_at.Value].Bounds);
                }

                return _result;
            }
        }
        private Dictionary<int, Node> _Bounds = new Dictionary<int, Node>();
        private Node _root;

        private Dictionary<Guid, int> _At = new Dictionary<Guid, int>();
        private int _nodeId = 0;
        internal int NewId => _nodeId++;

        public bool Contains(Guid id)
        {
            return _At.ContainsKey(id);
        }

        public IEnumerable<(Guid guid, RaycastHit hit)> Query(Ray ray, HashSet<Guid> searchSet = null, HashSet<Guid> ignore = null)
        {
            if (_root != null) return _root.Query(ref _Bounds, ray, ref searchSet, ref ignore);

            return Array.Empty<(Guid guid, RaycastHit hit)> ();
        }

        public IEnumerable<Guid> Query(IBounds searchSpace, HashSet<Guid> searchSet = null, HashSet<Guid> ignore = null/*, QueryType queryType = QueryType.All*/)
        {
            if (_root != null) return _root.Query(ref _Bounds, searchSpace, ref searchSet, ref ignore);

            return Array.Empty<Guid>();
        }

        public IEnumerable<Guid> Query(Guid nodeSpace, HashSet<Guid> searchSet = null)
            => Query(_Bounds[_At[nodeSpace]].Bounds, searchSet, new HashSet<Guid> { nodeSpace });

        #region Tree Insertions / Deletions
        public void Insert(Guid guid, IBounds space)
        {
            if (_At.ContainsKey(guid))
                Remove(guid);

            _At.Add(guid, _Insert(guid, space));
            RefreshBoundary();
        }

        private int _Insert(Guid guid, IBounds space)
        {
            var node = new Node
            {
                Id = NewId,
                Guid = guid,
                Boundary = space.Boundary,
                Bounds = space
            };
            _Bounds.TryAdd(node.Id, node);

            var _threshold = 1000;

            if (_root == null)
                _root = node;
            else
            {
                var currentNode = _root;
                while (!currentNode.IsLeaf && _threshold > 0)
                {
                    _threshold--;

                    if (currentNode._b == -1)
                        currentNode = _Bounds[currentNode._a];
                    else if (currentNode._a == -1) currentNode = _Bounds[currentNode._b];
                    else
                        currentNode = _Bounds[DecideBranch(ref currentNode, ref node) ? currentNode._a : currentNode._b];
                }

                var encompassNode = new Node
                {
                    Id = NewId,
                    Boundary = currentNode.Boundary.Merge(node.Boundary)
                };
                _Bounds.TryAdd(encompassNode.Id, encompassNode);

                encompassNode._parent = currentNode._parent;
                if (encompassNode._parent != Node.Empty)
                {
                    if (_Bounds[encompassNode._parent]._a == currentNode.Id)
                    {
                        _Bounds[encompassNode._parent]._a = encompassNode.Id;
                    }
                    else
                    {
                        _Bounds[encompassNode._parent]._b = encompassNode.Id;
                    }

                    _Bounds[encompassNode._parent].RefreshBounds(ref _Bounds, space);
                }

                encompassNode._a = currentNode.Id;
                currentNode._parent = encompassNode.Id;

                encompassNode._b = node.Id;
                node._parent = encompassNode.Id;

                if (currentNode.Id == _root.Id) _root = encompassNode;
            }

            if (_threshold <= 0) throw new Exception("Threshold exceeded");

            return node.Id;
        }

        public void Remove(Guid guid)
        {
            if (_At.ContainsKey(guid))
            {
                _Remove(guid);
                _At.Remove(guid);
            }
        }

        private void _Remove(Guid guid)
        {
            if (_root == null) return;

            var node = _Bounds[_At[guid]];

            if (node._parent == Node.Empty)
            {
                _Bounds.Remove(node.Id);
                _root = null;
                RefreshBoundary();
                return;
            }

            var parent = _Bounds[node._parent];
            var siblingId = parent._a == node.Id ? parent._b : parent._a;
            var sibling = _Bounds[siblingId];
            var grandParentId = parent._parent;

            sibling._parent = grandParentId;
            if (grandParentId == Node.Empty)
            {
                _root = sibling;
            }
            else
            {
                var grandParent = _Bounds[grandParentId];
                if (grandParent._a == parent.Id)
                    grandParent._a = sibling.Id;
                else
                    grandParent._b = sibling.Id;

                grandParent.RefreshBounds(ref _Bounds);
            }

            _Bounds.Remove(node.Id);
            _Bounds.Remove(parent.Id);
            RefreshBoundary();
        }

        private void RefreshBoundary()
        {
            Boundary = _root == null ? default : _root.Boundary;
        }

        private bool DecideBranch(ref Node branch, ref Node candidate)
        {
            if (branch._a != Node.Empty)
            {
                if (branch._b == Node.Empty)
                    return true;

                var a_intersects = _Bounds[branch._a].Boundary.Intersects(candidate.Boundary);
                var b_intersects = _Bounds[branch._b].Boundary.Intersects(candidate.Boundary);

                if (a_intersects && !b_intersects)
                    return true;
                else if (b_intersects && !a_intersects)
                    return false;
                else
                {
                    var _a = _Bounds[branch._a].Boundary.Merge(candidate.Boundary);
                    var _b = _Bounds[branch._b].Boundary.Merge(candidate.Boundary);

                    return _a.Size.magnitude <= _b.Size.magnitude;
                }
            }

            return false;
        }
        #endregion


        public void Debug(List<IBounds> interactions)
        {
            foreach(var interaction in interactions)
            {
                bool _hit = false;
                foreach (var _in in Query(interaction))
                {
                    var _bounds = _Bounds[_At[_in]].Bounds;

                    if (_bounds.Intersects(interaction))
                    {
                        _hit = true;
                        _bounds.Draw(Color.white);
                    }
                }

                if (_hit)
                {
                    interaction.Draw(Color.green);
                } else
                {
                    interaction.Draw(Color.red);
                }
            }
        }

        Color _Debug_Gray = new Color(0.5f, 0.5f, 0.5f, 0.1f);

        public void Draw(Color color)
        {
            foreach (var _node in _Bounds.Where(x => x.Value.IsLeaf))
            {
                _node.Value.Bounds.Draw(color);
            }
        }

        public void Debug_IdSelect(Guid id)
        {
            foreach (var _node in _Bounds.Where(x => x.Value.IsLeaf))
            {
                _node.Value.Bounds.Draw(_Debug_Gray);
            }

            foreach (var _in in Query(id))
            {
                _Bounds[_At[_in]].Bounds.Draw(Color.green);
            }

            _Bounds[_At[id]].Bounds.Draw(Color.yellow);
        }

        public void Debug_BoxSelect(BoundsBox box, Color boxColor, Color hitColor, Color missColor, bool hideMiss = false)
        {
            if (!hideMiss)
            {
                foreach (var _node in _Bounds.Where(x => x.Value.IsLeaf))
                {
                    _node.Value.Bounds.Draw(missColor);
                }
            }

            foreach(var _in in Query(box))
            {
                _Bounds[_At[_in]].Bounds.Draw(hitColor);
            }

            box.Draw(boxColor);
        }

        public void Debug_SphereSelect(BoundsSphere sphere, Color sphereColor, Color hitColor, Color missColor, bool hideMiss = false)
        {
            if (!hideMiss)
            {
                foreach (var _node in _Bounds.Where(x => x.Value.IsLeaf))
                {
                    _node.Value.Bounds.Draw(hitColor);
                }
            }

            foreach (var _in in Query(sphere))
            {
                _Bounds[_At[_in]].Bounds.Draw(missColor);
            }

            sphere.Draw(sphereColor);
        }

        public void Debug_RaySelect(Ray ray, Color rayColor, Color hitColor, Color missColor, bool hideMiss = false)
        {
            if (!hideMiss)
            {
                foreach (var _node in _Bounds.Where(x => x.Value.IsLeaf))
                {
                    _node.Value.Bounds.Draw(missColor);
                }
            }

            foreach (var _in in Query(ray))
            {
                _Bounds[_At[_in.guid]].Bounds.Draw(hitColor);
                _in.hit.Draw(rayColor, Color.white);
            }

            Gizmos.color = rayColor;
            Gizmos.DrawRay(ray.Origin, ray.Direction);
        }

        [Flags]
        public enum QueryType
        {
            Inside = 1,
            Partial = 1 << 1,
            Touching = 1 << 2,

            All = Inside | Partial | Touching
        }

        internal class Node
        {
            public int Id { get; set; }
            public Guid Guid { get; set; } = Guid.Empty;
            public BoundsBox Boundary { get; set; }
            public IBounds Bounds { get; set; }

            public int _parent = -1;
            public int _a = -1;
            public int _b = -1;
            public bool IsLeaf => _a == -1 && _b == -1;

            internal void RefreshBounds(ref Dictionary<int, Node> _nodeSpace, IBounds bounds = null)
            {
                if (IsLeaf)
                {
                    Boundary = bounds.Boundary;
                    Bounds = bounds;
                }
                else
                {
                    if (_a == -1)
                    {
                        if (_b != -1)
                        {
                            Boundary = _nodeSpace[_b].Boundary;
                        }
                    }
                    else if (_b != -1)
                    {
                        Boundary = _nodeSpace[_b].Boundary.Merge(_nodeSpace[_a].Boundary);
                    }
                }

                if (_parent != Empty)
                {
                    _nodeSpace[_parent].RefreshBounds(ref _nodeSpace, bounds);
                }
            }

            internal IEnumerable<(Guid guid, RaycastHit hit)> Query(ref Dictionary<int, Node> _nodeSpace, Ray ray, ref HashSet<Guid> searchSet, ref HashSet<Guid> ignore)
            {
                var result = new List<(Guid guid, RaycastHit hit)>();

                if (ray.Intersects(Boundary, out var _hit))
                {
                    if (IsLeaf)
                    {
                        if ((ignore == null || !ignore.Contains(Guid)) && (searchSet == null || searchSet.Contains(Guid))
                            && ray.Intersects(Bounds, out _hit))
                            result.Add((Guid, _hit));
                    } else
                    {
                        result.AddRange(_nodeSpace[_a].Query(ref _nodeSpace, ray, ref searchSet, ref ignore));
                        result.AddRange(_nodeSpace[_b].Query(ref _nodeSpace, ray, ref searchSet, ref ignore));
                    }
                }

                return result;
            }

            internal IEnumerable<Guid> Query(ref Dictionary<int, Node> _nodeSpace, IBounds range, ref HashSet<Guid> searchSet, ref HashSet<Guid> ignore)
            {
                var result = new List<Guid>();

                if (Boundary.Intersects(range))
                {
                    if (IsLeaf)
                    {
                        if ((ignore == null || !ignore.Contains(Guid)) && (searchSet == null || searchSet.Contains(Guid))
                            && Bounds.Intersects(range))
                            result.Add(Guid);
                    }
                    else
                    {
                        result.AddRange(_nodeSpace[_a].Query(ref _nodeSpace, range, ref searchSet, ref ignore));
                        result.AddRange(_nodeSpace[_b].Query(ref _nodeSpace, range, ref searchSet, ref ignore));
                    }
                }

                return result;
            }

            internal const int Empty = -1;
        }
    }
}
