using MolecularSurfaces.Geometry;
using MolecularSurfaces.Geometry.SDF;
using MolecularSurfaces.Molecules.Proteins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace MolecularSurfaces.Rendering
{
    public class SurfaceChunk
    {
        public Vector3[] Vertices;
        public Vector3[] Normals;
        public int[] Indices;
        /// <summary>Index into Protein.Atoms of the atom each vertex belongs to.</summary>
        public int[] Atoms;
    }

    public class ChainSurface
    {
        public char ChainId;
        public int Index;
        public int[] Atoms;
        public List<SurfaceChunk> Chunks = new List<SurfaceChunk>();
    }

    public class ProteinSurface
    {
        public Protein Protein;
        public int Resolution;
        public ChainSurface[] Chains;
        /// <summary>Index into Protein.Residues for each atom.</summary>
        public int[] AtomResidue;
        /// <summary>Index into Chains for each atom, or -1 for atoms left out (waters).</summary>
        public int[] AtomChain;
        public Vector3 Center;
        public float Radius;
    }

    /// <summary>
    /// Builds a per-chain molecular surface from a protein using the SDF model and
    /// marching cubes. Touches no Unity objects, so it runs on a worker thread.
    /// </summary>
    public static class SurfaceBuilder
    {
        // How quickly an atom's influence on vertex normals falls off with distance
        // from its surface (per Å). Higher is closer to the hard union's normals.
        const float NORMAL_FALLOFF = 3f;
        const float INFLUENCE_RANGE = 2.5f;

        public static ProteinSurface Build(Protein protein, int resolution, float radiusScale,
            Action<float> progress = null, CancellationToken cancel = default)
        {
            resolution = Mathf.Clamp(resolution, 1, 4);

            var _atomResidue = new int[protein.Atoms.Length];
            var _atomChain = Enumerable.Repeat(-1, protein.Atoms.Length).ToArray();

            for (int r = 0; r < protein.Residues.Length; r++)
                foreach (var a in protein.Residues[r].Atoms)
                    _atomResidue[a] = r;

            // Waters make noisy blobs on the surface, so they're left out.
            var _chains = protein.Residues
                .Where(x => x.Category != ResidueCategory.Water)
                .GroupBy(x => x.Key.ChainID)
                .Select((g, i) => new ChainSurface
                {
                    ChainId = g.Key,
                    Index = i,
                    Atoms = g.SelectMany(s => s.Atoms).ToArray()
                })
                .ToArray();

            foreach (var _chain in _chains)
                foreach (var a in _chain.Atoms)
                    _atomChain[a] = _chain.Index;

            var _shown = _chains.SelectMany(s => s.Atoms).Select(a => protein.Atoms[a].Position).ToArray();
            var _center = _shown.Length == 0 ? Vector3.zero : _shown.Aggregate(Vector3.zero, (sum, p) => sum + p) / _shown.Length;
            var _radius = _shown.Length == 0 ? 1 : _shown.Max(p => Vector3.Distance(p, _center));

            var _total = Mathf.Max(1, _chains.Sum(s => s.Atoms.Length));
            var _done = 0;

            foreach (var _chain in _chains)
            {
                cancel.ThrowIfCancellationRequested();
                BuildChain(protein, _chain, resolution, radiusScale, cancel);
                _done += _chain.Atoms.Length;
                progress?.Invoke(_done / (float)_total);
            }

            return new ProteinSurface
            {
                Protein = protein,
                Resolution = resolution,
                Chains = _chains,
                AtomResidue = _atomResidue,
                AtomChain = _atomChain,
                Center = _center,
                Radius = _radius,
            };
        }

        static void BuildChain(Protein protein, ChainSurface chain, int resolution, float radiusScale, CancellationToken cancel)
        {
            // The SDF grid has one sample per unit, so resolution is applied by scaling
            // the atoms up before sampling and the vertices back down afterwards.
            var _model = new SDFModel();
            foreach (var a in chain.Atoms)
            {
                var _atom = protein.Atoms[a];
                _model.Stamp(SDF.Sphere(_atom.Position * resolution, _atom.Radius * radiusScale * resolution));
            }

            if (chain.Atoms.Length == 0) return;

            var _iso = _model.IsoSurface(IsoSurface_Size.x32);
            var _grid = new AtomGrid(protein, chain.Atoms, radiusScale, INFLUENCE_RANGE);

            foreach (var _surface in _iso.Chunk)
            {
                cancel.ThrowIfCancellationRequested();

                _surface.Triangulate(resolution, out var _vertices, out _, out var _indices);
                if (_vertices.Length == 0) continue;

                var _normals = new Vector3[_vertices.Length];
                var _atoms = new int[_vertices.Length];

                for (int v = 0; v < _vertices.Length; v++)
                    _grid.Shade(_vertices[v], out _normals[v], out _atoms[v]);

                // Fall back to the face normal anywhere the atom field gave nothing useful.
                for (int t = 0; t < _indices.Length; t += 3)
                {
                    int i0 = _indices[t], i1 = _indices[t + 1], i2 = _indices[t + 2];
                    if (_normals[i0] != Vector3.zero && _normals[i1] != Vector3.zero && _normals[i2] != Vector3.zero) continue;

                    var _face = Vector3.Cross(_vertices[i1] - _vertices[i0], _vertices[i2] - _vertices[i0]).normalized;
                    if (_normals[i0] == Vector3.zero) _normals[i0] = _face;
                    if (_normals[i1] == Vector3.zero) _normals[i1] = _face;
                    if (_normals[i2] == Vector3.zero) _normals[i2] = _face;
                }

                chain.Chunks.Add(new SurfaceChunk
                {
                    Vertices = _vertices,
                    Normals = _normals,
                    Indices = _indices,
                    Atoms = _atoms,
                });
            }
        }

        /// <summary>
        /// Uniform spatial hash over atom spheres, used to find which atom a surface
        /// vertex belongs to and to compute smooth normals from the atom field.
        /// </summary>
        class AtomGrid
        {
            readonly Dictionary<Vector3Int, List<int>> _Cells = new Dictionary<Vector3Int, List<int>>();
            readonly Vector3[] _Position;
            readonly float[] _Radius;
            readonly int[] _AtomIndex;
            readonly float _CellSize;
            readonly float _Range;

            public AtomGrid(Protein protein, int[] atoms, float radiusScale, float range)
            {
                _Position = new Vector3[atoms.Length];
                _Radius = new float[atoms.Length];
                _AtomIndex = atoms;
                _Range = range;

                var _maxRadius = 0f;
                for (int i = 0; i < atoms.Length; i++)
                {
                    var _atom = protein.Atoms[atoms[i]];
                    _Position[i] = _atom.Position;
                    _Radius[i] = _atom.Radius * radiusScale;
                    _maxRadius = Mathf.Max(_maxRadius, _Radius[i]);
                }

                // A cell this size means a query only has to look at its 27 neighbours.
                _CellSize = Mathf.Max(0.5f, _maxRadius + range);

                for (int i = 0; i < atoms.Length; i++)
                {
                    var _key = Cell(_Position[i]);
                    if (!_Cells.TryGetValue(_key, out var _list))
                        _Cells.Add(_key, _list = new List<int>());
                    _list.Add(i);
                }
            }

            Vector3Int Cell(Vector3 p) => Vector3Int.FloorToInt(p / _CellSize);

            /// <summary>
            /// Normal: weighted sum of each nearby sphere's gradient, so shading blends
            /// smoothly across the creases where spheres meet.
            /// Atom: the sphere whose surface is closest to the point.
            /// </summary>
            public void Shade(Vector3 point, out Vector3 normal, out int atom)
            {
                var _cell = Cell(point);
                var _sum = Vector3.zero;
                var _best = float.MaxValue;
                atom = _AtomIndex.Length > 0 ? _AtomIndex[0] : 0;

                for (int z = -1; z <= 1; z++)
                for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                {
                    if (!_Cells.TryGetValue(_cell + new Vector3Int(x, y, z), out var _list)) continue;

                    foreach (var i in _list)
                    {
                        var _offset = point - _Position[i];
                        var _distance = _offset.magnitude;
                        var _surface = _distance - _Radius[i];

                        if (_surface < _best)
                        {
                            _best = _surface;
                            atom = _AtomIndex[i];
                        }

                        if (_surface > _Range || _distance < 1e-5f) continue;
                        _sum += (_offset / _distance) * Mathf.Exp(-NORMAL_FALLOFF * _surface);
                    }
                }

                normal = _sum.sqrMagnitude > 1e-12f ? _sum.normalized : Vector3.zero;
            }
        }
    }
}
