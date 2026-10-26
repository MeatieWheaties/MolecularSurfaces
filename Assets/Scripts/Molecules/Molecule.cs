namespace MolecularSurfaces.Molecules
{

    public class Molecule<TAtom> where TAtom : Atom
    {
        public string Name { get; protected set; }
        public TAtom[] Atoms { get; protected set; }
        public Bond[] Bonds { get; protected set; }
    }

    public class Molecule : Molecule<Atom>
    {
        public Molecule(string name, Atom[] atoms, Bond[] bonds)
        {
            Name = name;
            Atoms = atoms;
            Bonds = bonds;
        }
    }

    public class Molecule<TAtomData, TGroupData> : Molecule<TAtomData> where TAtomData : Atom where TGroupData : AtomGroup
    {
        protected TGroupData[] Groups;

        public Molecule(string name, TAtomData[] atoms, Bond[] bonds, TGroupData[] groups)
        {
            Name = name;
            Atoms = atoms;
            Bonds = bonds;
            Groups = groups;
        }
    }
}
