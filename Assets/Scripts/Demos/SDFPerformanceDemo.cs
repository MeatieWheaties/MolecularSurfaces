using MolecularSurfaces.Components;
using MolecularSurfaces.Components.Instances;
using MolecularSurfaces.DataSources;
using MolecularSurfaces.Helpers;
using MolecularSurfaces.Molecules.Proteins;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MolecularSurfaces.Demos
{
    [ExecuteAlways]
    internal class SDFPerformanceDemo : MonoBehaviour
    {
        public string Protein = "1UBQ";

        public bool UseCache;

        public bool CompressResidues = false;

        public bool Build;
        void _Build()
        {
            Build = false;

            var _timer = Stopwatch.StartNew();
            _timer.Reset();
            _timer.Start();

            _ProteinContainer.Purge<ProteinInstance>();
            var _protein = UseCache ? LocalCache.LoadProtein(Protein, ProteinFileType.PDB) : PDBBank.DownloadPDB(Protein);
            _instance = _ProteinContainer.Create<ProteinInstance>(Protein, _protein);

            _instance.RenderIndividualResidues = !CompressResidues;

            _instance.Refresh = true;
        }
        Transform _ProteinContainer => transform.Cursor("[Protein]");
        ProteinInstance _instance;

        private void Update()
        {
            if (Build) _Build();
        }
    }
}
