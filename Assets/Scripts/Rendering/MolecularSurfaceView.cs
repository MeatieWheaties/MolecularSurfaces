using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace MolecularSurfaces.Rendering
{
    /// <summary>
    /// Turns a <see cref="ProteinSurface"/> into meshes (one GameObject per chain) and
    /// handles recolouring, chain visibility, residue highlighting and picking.
    /// </summary>
    public class MolecularSurfaceView : MonoBehaviour
    {
        public const string SURFACE_SHADER = "MolecularSurfaces/Surface";

        public Color HighlightColor = new Color(1f, 0.95f, 0.55f);
        [Range(0, 1)] public float HighlightStrength = 0.75f;

        public ProteinSurface Surface { get; private set; }
        public ColorScheme Scheme { get; private set; } = ColorScheme.Chain;
        public int HighlightedResidue { get; private set; } = -1;

        class ChunkView
        {
            public Mesh Mesh;
            public int[] Atoms;
            public Color[] Colors;
        }

        class ChainView
        {
            public GameObject Root;
            public Material Material;
            public List<ChunkView> Chunks = new List<ChunkView>();
        }

        readonly List<ChainView> _Chains = new List<ChainView>();
        // Which chunks contain each residue, so a highlight only touches those meshes.
        readonly Dictionary<int, List<ChunkView>> _ResidueChunks = new Dictionary<int, List<ChunkView>>();
        Color[] _AtomColors;

        public void Show(ProteinSurface surface)
        {
            Clear();
            Surface = surface;
            HighlightedResidue = -1;

            // Centre the protein on this object so the camera can orbit its origin.
            transform.localPosition = -surface.Center;

            var _shader = Shader.Find(SURFACE_SHADER);

            foreach (var _chain in surface.Chains)
            {
                var _view = new ChainView
                {
                    Root = new GameObject($"Chain {_chain.ChainId}"),
                    Material = new Material(_shader) { name = $"Surface (Chain {_chain.ChainId})" },
                };
                _view.Root.transform.SetParent(transform, false);
                // Read by the chain-ID replacement shader for the outline pass.
                _view.Material.SetFloat("_ChainId", _chain.Index + 1);

                for (int c = 0; c < _chain.Chunks.Count; c++)
                {
                    var _data = _chain.Chunks[c];
                    var _mesh = new Mesh { name = $"{_chain.ChainId}:{c}", indexFormat = IndexFormat.UInt32 };
                    _mesh.SetVertices(_data.Vertices);
                    _mesh.SetNormals(_data.Normals);
                    _mesh.SetTriangles(_data.Indices, 0, true);
                    _mesh.MarkDynamic();

                    var _chunk = new ChunkView { Mesh = _mesh, Atoms = _data.Atoms, Colors = new Color[_data.Vertices.Length] };
                    _view.Chunks.Add(_chunk);

                    foreach (var _residue in _data.Atoms.Select(a => surface.AtomResidue[a]).Distinct())
                    {
                        if (!_ResidueChunks.TryGetValue(_residue, out var _list))
                            _ResidueChunks.Add(_residue, _list = new List<ChunkView>());
                        _list.Add(_chunk);
                    }

                    var _go = new GameObject(_mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
                    _go.transform.SetParent(_view.Root.transform, false);
                    _go.GetComponent<MeshFilter>().sharedMesh = _mesh;
                    var _renderer = _go.GetComponent<MeshRenderer>();
                    _renderer.sharedMaterial = _view.Material;
                    _renderer.shadowCastingMode = ShadowCastingMode.On;
                    _renderer.receiveShadows = true;
                }

                _Chains.Add(_view);
            }

            SetScheme(Scheme);
        }

        public void SetScheme(ColorScheme scheme)
        {
            Scheme = scheme;
            if (Surface == null) return;

            _AtomColors = ColorSchemes.AtomColors(Surface, scheme);
            foreach (var _chunk in _Chains.SelectMany(s => s.Chunks))
                Recolor(_chunk);
        }

        public bool IsChainVisible(int chain) => chain >= 0 && chain < _Chains.Count && _Chains[chain].Root.activeSelf;

        public void SetChainVisible(int chain, bool visible)
        {
            if (chain >= 0 && chain < _Chains.Count)
                _Chains[chain].Root.SetActive(visible);
        }

        public void SetHighlight(int residue)
        {
            if (residue == HighlightedResidue) return;

            var _previous = HighlightedResidue;
            HighlightedResidue = residue;

            var _dirty = new HashSet<ChunkView>();
            if (_ResidueChunks.TryGetValue(_previous, out var _a)) _dirty.UnionWith(_a);
            if (_ResidueChunks.TryGetValue(residue, out var _b)) _dirty.UnionWith(_b);
            foreach (var _chunk in _dirty) Recolor(_chunk);
        }

        void Recolor(ChunkView chunk)
        {
            var _linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            for (int v = 0; v < chunk.Atoms.Length; v++)
            {
                var _atom = chunk.Atoms[v];
                var _color = _AtomColors[_atom];
                if (HighlightedResidue >= 0 && Surface.AtomResidue[_atom] == HighlightedResidue)
                    _color = Color.Lerp(_color, HighlightColor, HighlightStrength);
                // Vertex colours aren't converted by Unity, so do it here in linear projects.
                chunk.Colors[v] = _linear ? _color.linear : _color;
            }
            chunk.Mesh.SetColors(chunk.Colors);
        }

        /// <summary>
        /// Finds the atom under a world-space ray by intersecting it with every visible
        /// atom sphere. The surface is a union of those spheres, so this matches what's drawn.
        /// </summary>
        public bool Pick(Ray worldRay, float radiusScale, out int atom)
        {
            atom = -1;
            if (Surface == null) return false;

            var _origin = transform.InverseTransformPoint(worldRay.origin);
            var _direction = transform.InverseTransformDirection(worldRay.direction).normalized;
            var _atoms = Surface.Protein.Atoms;
            var _nearest = float.MaxValue;

            foreach (var _chain in Surface.Chains)
            {
                if (!IsChainVisible(_chain.Index)) continue;

                foreach (var a in _chain.Atoms)
                {
                    var _radius = _atoms[a].Radius * radiusScale;
                    var _toCenter = _atoms[a].Position - _origin;
                    var _along = Vector3.Dot(_toCenter, _direction);
                    var _closest = _toCenter.sqrMagnitude - _along * _along;
                    var _r2 = _radius * _radius;
                    if (_closest > _r2) continue;

                    var _t = _along - Mathf.Sqrt(_r2 - _closest);
                    if (_t > 0 && _t < _nearest)
                    {
                        _nearest = _t;
                        atom = a;
                    }
                }
            }

            return atom >= 0;
        }

        public void Clear()
        {
            foreach (var _chain in _Chains)
            {
                foreach (var _chunk in _chain.Chunks) Destroy(_chunk.Mesh);
                Destroy(_chain.Material);
                Destroy(_chain.Root);
            }
            _Chains.Clear();
            _ResidueChunks.Clear();
            Surface = null;
        }

        void OnDestroy() => Clear();
    }
}
