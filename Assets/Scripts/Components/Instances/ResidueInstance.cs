using MolecularSurfaces.Geometry.SDF;
using MolecularSurfaces.Helpers;
using MolecularSurfaces.Molecules;
using MolecularSurfaces.Molecules.Proteins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace MolecularSurfaces.Components.Instances
{
    public class ResidueInstance : Instance<Residue>
    {
        Transform _Render => transform.Cursor("[Render]");

        public ResidueKey Key;
        public ResidueType Type;
        public ResidueCategory Category;

        public SDFModel Model { get; private set; } = new SDFModel();
        public SDFModelInstance ModelInstance { get; private set; }

        public void Build(Protein _SOURCE, Color color, float positionScale, float radiusScale)
        {
            Key = Value.Key;
            Type = Value.Type;
            Category = Value.Category;

            Model = new SDFModel();

            foreach (var atom in _SOURCE.GetAtoms(Value))
            {
                Model.Stamp(SDF.Sphere(atom.Position * positionScale, atom.Radius * radiusScale));
            }

            _Render.Purge<SDFModelInstance>();
            ModelInstance = _Render.Create<SDFModelInstance>(Value.Guid.ToString(), Model);
            ModelInstance.Scale = positionScale;
            ModelInstance.Color = color;
            ModelInstance.Build = true;
        }
    }
}
