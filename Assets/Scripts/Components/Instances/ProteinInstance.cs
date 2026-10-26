using MolecularSurfaces.Geometry;
using MolecularSurfaces.Geometry.SDF;
using MolecularSurfaces.Helpers;
using MolecularSurfaces.Molecules;
using MolecularSurfaces.Molecules.Proteins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MolecularSurfaces.Components.Instances
{
    [ExecuteAlways]
    public class ProteinInstance : Instance<Protein>
    {
        public Color ModelColor = Color.white;

        public bool RenderIndividualResidues = false;

        public float RenderScale = 1;
        public float RadiusScale = 1;

        public override Protein Value
        {
            get
            {
                if (_Dirty) Build();
                return _Value;
            }
            set
            {
                _Value = value;
                Build();
            }
        }
        Protein _Value;
        bool _Dirty = false;
        public bool Refresh
        {
            get => false;
            set
            {
                _Dirty = false;
                Build();
            }
        }

        public void ClearSelections()
        {
            _SelectedResidues.Clear();
            _SelectionColor.Clear();
            Refresh = true;
        }

        public Residue[] GetSelection(string name)
        {
            if(_SelectedResidues.ContainsKey(name)) return _SelectedResidues[name];
            return new Residue[0];
        }

        Dictionary<string, Residue[]> _SelectedResidues = new Dictionary<string, Residue[]>();
        Dictionary<string, Color> _SelectionColor = new Dictionary<string, Color>();
        public Residue[] Select(string pattern, Color color)
        {
            if(_SelectedResidues.ContainsKey(pattern)) return _SelectedResidues[pattern];

            var _select = Value.FindByPattern(pattern);

            _SelectedResidues.TryAdd(pattern, _select);
            _SelectionColor.TryAdd(pattern, color);
            _Dirty = true;

            return _select;
        }

        public Residue[] Select(string selectionId, Residue[] selection, Color color)
        {
            if (_SelectedResidues.ContainsKey(selectionId)) return _SelectedResidues[selectionId];

            _SelectedResidues.TryAdd(selectionId, selection);
            _SelectionColor.TryAdd(selectionId, color);
            _Dirty = true;

            return selection;
        }

        public Residue[] Select(string selectionId, Color color, params Residue[] selection)
        {
            if (_SelectedResidues.ContainsKey(selectionId)) return _SelectedResidues[selectionId];

            _SelectedResidues.TryAdd(selectionId, selection);
            _SelectionColor.TryAdd(selectionId, color);
            _Dirty = true;

            return selection;
        }

        public Residue[] Select(string selectionId, Color color, params int[] selection)
        {
            if (_SelectedResidues.ContainsKey(selectionId)) return _SelectedResidues[selectionId];

            var _selection = selection.ToList();
            _SelectedResidues.TryAdd(selectionId, Value.Residues.Where(x => _selection.Contains(x.Index)).ToArray());
            _SelectionColor.TryAdd(selectionId, color);
            _Dirty = true;

            return _SelectedResidues[selectionId];
        }

        public Residue[] Select(string selectionName, Color color, params ResidueKey[] key)
        {
            if (_SelectedResidues.ContainsKey(selectionName)) return _SelectedResidues[selectionName];

            var _selection = key.ToList();

            var _select = Value.Residues.Where(x => _selection.Contains(x.Key)).ToArray();

            if (_select.Length == 0) return new Residue[0];

            _SelectedResidues.TryAdd(selectionName, _select);
            _SelectionColor.TryAdd(selectionName, color);

            _Dirty = true;

            return _select;
        }

        private void Update()
        {
            if (_Dirty) Build();
        }


        Transform _RenderContainer => transform.Cursor("[Model]");
        Transform _ResidueContainer => _RenderContainer.Cursor("[Residues]");
        Transform _SelectionContainer => _RenderContainer.Cursor("[Selection]");

        Dictionary<string, Transform> _Selection = new Dictionary<string, Transform>();

        IsoSurface_Size IsoSurface_Size = IsoSurface_Size.x16;

        public List<SDFModel> SDFModels { get; private set; } = new List<SDFModel>();
        public List<SDFModelInstance> SDFModelInstances { get; private set; } = new List<SDFModelInstance>();
        public Dictionary<string, ResidueInstance> ResidueInstances { get; private set; } = new Dictionary<string, ResidueInstance>();
        public Dictionary<string, List<ResidueInstance>> Selections { get; private set; } = new Dictionary<string, List<ResidueInstance>>();
        void Build()
        {
            _Dirty = false;

            if (Value == null) return;

            foreach (var _selectContainer in _Selection)
            {
                _selectContainer.Value.Purge<ResidueInstance>();
            }
            _Selection.Clear();
            ResidueInstances.Clear();

            _ResidueContainer.Purge<ResidueInstance>();
            _ResidueContainer.Purge<SDFModelInstance>();

            SDFModels.Clear();
            SDFModelInstances.Clear();

            if (RenderIndividualResidues)
            {
                var _selection = _SelectedResidues.SelectMany(s => s.Value);

                foreach (var _residue in Value.Residues
                    .Where(x => !_selection.Contains(x)))
                {
                    var _instance = _ResidueContainer.Create<ResidueInstance>(_residue.Key + " [" + _residue.Type.ToString() + "/" + _residue.Type.To1Char() + "]", _residue);
                    _instance.Build(Value, ModelColor, RenderScale, RadiusScale);

                    SDFModels.Add(_instance.Model);
                    SDFModelInstances.Add(_instance.ModelInstance);
                    ResidueInstances.Add(_residue.Key.ToString(), _instance);
                }
            }
            else
            {
                var _model = new SDFModel();

                var _selectedResidues = _SelectedResidues.Values.SelectMany(s => s).Distinct();

                foreach (var atom in Value.GetAtoms(Value.Residues.Where(x => !_selectedResidues.Contains(x)).ToArray()))
                {
                    _model.Stamp(SDF.Sphere(atom.Position * RenderScale, atom.Radius * RadiusScale));
                }

                SDFModels.Add(_model);

                var _render = _ResidueContainer.Create<SDFModelInstance>(Value.Guid.ToString(), _model);
                _render.Scale = RenderScale;
                _render.Color = ModelColor;
                _render.IsoSize = IsoSurface_Size;
                _render.Build = true;
                SDFModelInstances.Add(_render);
            }

            Selections.Clear();
            foreach (var _s in _SelectedResidues)
            {
                var _container = _SelectionContainer.Cursor(_s.Key);
                _container.Purge<ResidueInstance>();

                Selections.Add(_s.Key, new List<ResidueInstance>());

                foreach (var _residue in _s.Value)
                {
                    var _instance = _container.Create<ResidueInstance>(_residue.Key + " [" + _residue.Type.ToString() + "/" + _residue.Type.To1Char() + "]", _residue);

                    _instance.Build(Value, _SelectionColor[_s.Key], RenderScale, RadiusScale);
                    SDFModels.Add(_instance.Model);
                    SDFModelInstances.Add(_instance.ModelInstance);

                    Selections[_s.Key].Add(_instance);
                }
            }
        }
    }
}
