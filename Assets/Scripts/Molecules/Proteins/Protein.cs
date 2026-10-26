using System;
using System.Collections.Generic;

namespace MolecularSurfaces.Molecules.Proteins
{
    public class ProteinAtom : Atom
    {
        public int FileLine { get; private set; }

        public bool IsHetAtom { get; private set; }

        public ProteinAtom(Atom source, int fileLineNumber, bool isHetAtom) : base(source)
        {
            FileLine = fileLineNumber;
            IsHetAtom = isHetAtom;
        }

        public ProteinAtom(Atom source, int fileLineNumber, bool isHetAtom, float radius)
             : base(source)
        {
            FileLine = fileLineNumber;
            IsHetAtom = isHetAtom;

            Radius = radius;
        }
    }

    public class Protein : Molecule<ProteinAtom, Residue>
    {
        public Guid Guid { get; private set; } = Guid.NewGuid();

        public Residue[] Residues => Groups;

        public Dictionary<char, Sequence> Chains { get; private set; } = new Dictionary<char, Sequence>();

        public Dictionary<ResidueKey, int> ResidueLookup;

        public ProteinFile File { get; private set; }

        public Protein(string name, 
            ProteinFile file,
            Dictionary<char, Sequence> chains,
            Dictionary<ResidueKey, int> residueLookup,
            ProteinAtom[] atoms, Residue[] residues) 
            : base(name, atoms, null, residues)
        {

            File = file;
            Chains = chains;
            ResidueLookup = residueLookup;
        }
    }

    public readonly struct ProteinFile
    {
        public readonly ProteinFileType Type;
        public readonly string[] Line;

        public ProteinFile(ProteinFileType type, string[] line)
        {
            Line = line;
            Type = type;
        }
    }

    public enum ProteinFileType
    {
        PDB,
        mmCIF,
        PQR,
        Unknown
    }
}
