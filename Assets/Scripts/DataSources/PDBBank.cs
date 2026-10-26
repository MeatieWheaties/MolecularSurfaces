using MolecularSurfaces.Molecules;
using MolecularSurfaces.Molecules.Proteins;
using System;
using System.Net.Http;

namespace MolecularSurfaces.DataSources
{
    internal class PDBBank
    {
        private const string RcsbFilesBase = "https://files.rcsb.org/download/";
        public static Protein DownloadPDB(string pdbId)
        {
            if (string.IsNullOrEmpty(pdbId) || pdbId.Length != 4)
                throw new ArgumentException("PDB id must be 4 characters.");

            pdbId = pdbId.ToUpperInvariant();
            var url = $"{RcsbFilesBase}{pdbId}.pdb";

            using var client = new HttpClient();
            var pdbText = client.GetStringAsync(url).Result;

            var _result = ProteinBuilder.Build(pdbId, pdbText, ProteinFileType.PDB);

            return _result;
        }  
    }
}
