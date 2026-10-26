using MolecularSurfaces.Components;
using MolecularSurfaces.Components.Instances;
using MolecularSurfaces.DataSources;
using MolecularSurfaces.Helpers;
using MolecularSurfaces.Molecules.Proteins;
using UnityEngine;

namespace MolecularSurfaces.Demos
{
    [ExecuteAlways]
    public class ProteinLoadingDemo : MonoBehaviour
    {
        public string ProteinName = "1UBQ";

        public bool UseCache;

        public float RenderScale = 1;
        public float RadiusScale = 1;

        public bool CompressResidues = false;

        public bool Download;
        void _Download()
        {
            Download = false;
            transform.Purge<ProteinInstance>();

            if (UseCache)
                _Instance = transform.Create<ProteinInstance>(ProteinName, LocalCache.LoadProtein(ProteinName, ProteinFileType.PDB)
                    );
            else 
                _Instance = transform.Create<ProteinInstance>(ProteinName, PDBBank.DownloadPDB(ProteinName));

            _Instance.RenderIndividualResidues = !CompressResidues;

            _Instance.RenderScale = RenderScale;
            _Instance.RadiusScale = RadiusScale;

            _Instance.Refresh = true;
        }
        ProteinInstance _Instance;

        void Update()
        {
            if (Download) _Download();
        }
    }
}
