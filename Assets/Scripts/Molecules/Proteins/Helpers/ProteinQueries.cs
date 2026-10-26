using MolecularSurfaces.Molecules.Proteins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MolecularSurfaces.Molecules
{
    public static class ProteinQueries
    {
        public static ProteinAtom GetAtom(this Protein protein, Residue residue, string atomName)
        {
            return protein.Atoms[residue.Atoms.First(x => protein.Atoms[x].Name == atomName)];
        }

        public static ProteinAtom[] GetAtoms(this Protein protein, params int[] atoms)
        {
            return protein.Atoms.Where(x => atoms.Contains(x.Id)).ToArray();
        }

        public static ProteinAtom[] GetAtoms(this Protein protein, params Residue[] residues)
        {
            return residues.SelectMany(s => s.Atoms).Select(s => protein.Atoms[s]).ToArray();
        }


        public static Residue[] GetResidues(this Protein protein, Selection _select)
        {
            return protein.Residues.Where(x => _select.Ids.Contains(x.Index)).ToArray();
        }

        public static Residue[] GetResidues(this Protein protein, params Selection[] _select)
        {
            return protein.Residues.Where(x => _select.Any(a => a.Ids.Contains(x.Index))).ToArray();
        }

        public static Selection FindByType(this Protein protein, string residueName)
        {
            if (residueName.Count() == 1 || residueName.Count() == 3)
            {
                return protein.FindByType(ResidueParser.Parse(residueName));
            }
            else
            {
                return new Selection
                {
                    Ids = new int[0]
                };
            }
        }

        public static Selection FindByType(this Protein protein, ResidueType type)
        {
            return new Selection { Ids = protein.Residues.Where(x => x.Type == type).Select(s => s.Index).ToArray() };
        }

        public static Residue[] FindByPattern(this Protein protein, string pattern)
        {
            var _result = new List<Selection>();

            foreach(var _chain in protein.Chains)
            {
                foreach(Match _match in Regex.Matches(_chain.Value.FASTA, pattern))
                {
                    var _select = new int[_match.Length];

                    for(int s = 0; s < _select.Length; s++)
                    {
                        _select[s] = _chain.Value.Residues[_match.Index + s];
                    }

                    _result.Add(new Selection
                    {
                        Ids = _select
                    });
                }
            }

            return protein.GetResidues(_result.ToArray());
        }
    }

    public struct Selection
    {
        public int[] Ids;
    }
}
