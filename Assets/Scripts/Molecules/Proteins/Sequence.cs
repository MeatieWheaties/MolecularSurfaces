using System.Collections.Generic;
using System.Linq;

namespace MolecularSurfaces.Molecules.Proteins
{
    public struct Sequence
    {
        public char ChainId { get; }
        public int[] Residues { get; }
        public string FASTA { get; }

        public Sequence(Dictionary<ResidueKey, int> chain, Dictionary<int, ResidueType> _typeLookup)
        {
            ChainId = chain.First().Key.ChainID;

            var _residue = new List<int>();
            FASTA = "";
            foreach (var residueNumber in chain.GroupBy(g => g.Key.ResidueNumber))
            {
                foreach (var insertCode in residueNumber.OrderBy(g => GetCodeOrder(g.Key.InsertCode)))
                {
                    _residue.Add(insertCode.Value);
                    FASTA += _typeLookup[insertCode.Value].To1Char();
                }
            }
            Residues = _residue.ToArray();
        }

        static int GetCodeOrder(char value) => value == ' ' ? 0 : (int)value;
    }
}
