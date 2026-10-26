using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using static UnityEngine.UI.Image;

namespace MolecularSurfaces.Geometry.SDF
{
    public class SDFModel
    {
        public BoundsSpace Space => _Space;
        BoundsSpace _Space = new BoundsSpace();
        Dictionary<Guid, (int order, CSGMode, SDFBrush)> _Stack = new Dictionary<Guid, (int, CSGMode, SDFBrush)>();

        public int Density {
            get => _Density;
            set { _Density = Mathf.Max(1, value);  }
        }
        int _Density;

        public BoundsBox Bound => Space.Boundary;

        public SDFModel(int density = 1)
        {
            Density = density;
        }

        enum CSGMode
        {
            Stamp,
            Clip,
            Fill,
            Replace
        }

        Vector3Int At(int index, int width, int height)
        {
            var x = index % width;
            var y = (index / width) % height;
            var z = index / (width * height);
            return new Vector3Int(x, y, z);
        }

        public IsoSurfaceRender IsoSurface(IsoSurface_Size chunkSize)
        {
            var _alloc = _Space.Boundary.ToGrid().GenerateChunks(chunkSize);
            Dictionary<Vector3Int, IsoSurface> _chunks = new Dictionary<Vector3Int, IsoSurface>();

            foreach (var chunk in _alloc.Chunks)
            {
                var _isoSurface = new IsoSurface(chunk.Value.Min, chunkSize);

                var _brushSet = _Space.Query(chunk.Value);
                var _brushes = _brushSet.Select(g => _Stack[g]).ToArray();

                if(_brushes.Count() == 0)
                {
                    continue;
                }

                Parallel.For(0, chunk.Value.Size.x * chunk.Value.Size.y * chunk.Value.Size.z, i =>
                {
                    var _value = 1f;
                    var _at = At(i, chunk.Value.Size.x, chunk.Value.Size.y) + chunk.Value.Min;

                    foreach (var _brush in _brushes.OrderBy(o => o.order))
                    {
                        var _brushValue = _brush.Item3.IsoSurface(_at);
                        switch (_brush.Item2)
                        {
                            case CSGMode.Stamp:
                                _value = Mathf.Min(_value, _brushValue);
                                break;
                            case CSGMode.Clip:
                                _value = Mathf.Max(_value, -_brushValue);
                                break;
                        }
                    }

                    _isoSurface.Value[i] = _value;
                });

                _chunks.Add(chunk.Key, _isoSurface);
            }

            return new IsoSurfaceRender
            {
                Chunk = _chunks.Values.ToArray()
            };
        }

        public void DebugBrushes()
        {
            _Space.Boundary.Draw(Color.black);

            Color _color = Color.white;

            foreach(var _brush in _Stack.OrderBy(o => o.Value.order))
            {
                switch (_brush.Value.Item2)
                {
                    case CSGMode.Stamp:
                        _color = Color.green;
                        break;
                    case CSGMode.Clip:
                        _color = Color.red;
                        break;
                    case CSGMode.Fill:
                        _color = Color.cyan;
                        break;
                    case CSGMode.Replace:
                        _color = Color.magenta;
                        break;
                }

                var _test = _brush.Value.Item3.Bounds();
                _brush.Value.Item3.Bounds().Resolve().Draw(_color);
            }
        }

        public void DebugChunks(IsoSurface_Size chunkSize)
        {
            var _chunks = _Space.Boundary.ToGrid().GenerateChunks(chunkSize);
            foreach(var _chunk in _chunks.Chunks)
            {
                _chunk.Value.Draw(Color.white);
            }
        }

        #region CSG

        private int _order = 0;

        public void Stamp(SDFBrush brush)
        {
            _Stack.Add(brush.Guid, (_order++, CSGMode.Stamp, brush));
            _Space.Insert(brush.Guid, brush.Bounds().Resolve());
        }

        public void Clip(SDFBrush brush)
        {
            _Stack.Add(brush.Guid, (_order++, CSGMode.Clip, brush));
            _Space.Insert(brush.Guid, brush.Bounds().Resolve());
        }

        public void Fill(SDFBrush brush)
        {
            _Stack.Add(brush.Guid, (_order++, CSGMode.Fill, brush));
            _Space.Insert(brush.Guid, brush.Bounds().Resolve());
        }

        public void Replace(SDFBrush brush)
        {
            _Stack.Add(brush.Guid, (_order++, CSGMode.Replace, brush));
            _Space.Insert(brush.Guid, brush.Bounds().Resolve());
        }

        #endregion

        public void DebugSpace()
        {
            Debug.Log($"{_Space.Boundary.Size.x}, {_Space.Boundary.Size.y}, {_Space.Boundary.Size.z}");
        }
    }

    public struct SDFBrush
    {
        public Guid Guid;
        public Vector3 Origin;
        public SDFType Type;
        public float[] Parameters;
        public SDFMode Mode;

        public float this[int index]
        {
            get => Parameters[index];
            set => Parameters[index] = value;
        }
    }

    public enum SDFType
    {
        Box,
        Sphere,
    }

    public enum SDFMode
    {
        Stamp,
        Clip,
        Fill,
        Replace
    }

    public static partial class SDF
    {
        public static BoundsRef Bounds(this SDFBrush brush)
        {
            switch (brush.Type)
            {
                case SDFType.Box:
                    return new BoundsRef
                    {
                        Type = BoundsType.Box,
                        AsBox = new BoundsBox(
                            brush.Origin + new Vector3(brush[0], brush[1], brush[2]),
                            brush.Origin - new Vector3(brush[0], brush[1], brush[2]))
                    };
                case SDFType.Sphere:
                    return new BoundsRef
                    {
                        Type = BoundsType.Sphere,
                        AsSphere = new BoundsSphere(brush.Origin, brush[0])
                    };
                default:
                    return new BoundsRef
                    {
                        Type = BoundsType.Box,
                        AsBox = new BoundsBox()
                    };
            }
        }

        public static float IsoSurface(this SDFBrush brush, Vector3 point)
        {
            switch (brush.Type)
            {
                case SDFType.Sphere:
                    return SDF.Iso_Sphere(brush.Origin, brush[0], point);
                case SDFType.Box:
                    return SDF.Iso_Box(brush.Origin, brush[0], brush[1], brush[2], point);
                default:
                    return 0;
            }
        }
    }
}
