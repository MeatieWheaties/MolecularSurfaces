using System;
using System.Collections.Generic;
using UnityEngine;

namespace MolecularSurfaces.Molecules.Proteins
{
    [Serializable]
    public readonly struct ResidueKey
    {
        public readonly char ChainID;
        public readonly int ResidueNumber;
        public readonly char InsertCode;

        public ResidueKey(char chainId, int residueSequence, char insertCode)
        {
            ChainID = chainId;
            ResidueNumber = residueSequence;
            InsertCode = insertCode;
        }

        public override string ToString()
        {
            return $"{ChainID}, {ResidueNumber}, {InsertCode}";
        }
    }

    public class Residue : AtomGroup
    {
        public ResidueKey Key { get; private set; }
        public int Index { get; private set; }
        public ResidueType Type { get; private set; }
        public ResidueCategory Category { get; private set; }

        public Residue(int index, ResidueKey key, int[] atoms, ResidueType type, ResidueCategory category) : base("Residue", atoms)
        {
            Key = key;
            Index = index;
            Type = type;
            Category = category;
        }
    }

    #region Residue Configuration

    public enum ResidueType
    {
        // Standard Amino Acids (20)
        Ala,  // Alanine
        Arg,  // Arginine
        Asn,  // Asparagine
        Asp,  // Aspartic Acid
        Cys,  // Cysteine
        Gln,  // Glutamine
        Glu,  // Glutamic Acid
        Gly,  // Glycine
        His,  // Histidine
        Ile,  // Isoleucine
        Leu,  // Leucine
        Lys,  // Lysine
        Met,  // Methionine
        Phe,  // Phenylalanine
        Pro,  // Proline
        Ser,  // Serine
        Thr,  // Threonine
        Trp,  // Tryptophan
        Tyr,  // Tyrosine
        Val,  // Valine

        // Very Common Modified / Non-standard Amino Acids
        Mse,  // Selenomethionine (extremely common in X-ray structures)
        Sec,  // Selenocysteine (21st amino acid)
        Pyl,  // Pyrrolysine (22nd amino acid)
        Hyp,  // Hydroxyproline (common in collagen)

        // Common Ligands / Cofactors (HETATM)
        Atp,  // Adenosine Triphosphate
        Adp,  // Adenosine Diphosphate
        Amp,  // Adenosine Monophosphate
        Gtp,  // Guanosine Triphosphate
        Gdp,  // Guanosine Diphosphate
        Nad,  // NAD+
        Nadh, // NADH
        Fad,  // Flavin Adenine Dinucleotide
        Hem,  // Heme (iron protoporphyrin)
        Coa,  // Coenzyme A

        // Common Ions (HETATM)
        Zn,   // Zinc ion
        Mg,   // Magnesium ion
        Ca,   // Calcium ion
        Na,   // Sodium ion
        K,    // Potassium ion
        Fe,   // Iron ion
        Cu,   // Copper ion
        Mn,   // Manganese ion
        Cl,   // Chloride ion

        // Water
        Hoh,  // Water molecule

        // Catch-all
        Unknown
    }

    public enum ResidueCategory
    {
        AminoAcid,
        Nucleotide,
        ModifiedResidue,
        Ligand,
        Ion,
        Water,
        Unknown
    }

    public static class ResidueParser
    {
        public static ResidueType ParseResidueType(string residueName)
        {
            return residueName?.ToUpperInvariant() switch
            {
                // Standard Amino Acids
                "ALA" or "A" => ResidueType.Ala,
                "ARG" or "R" => ResidueType.Arg,
                "ASN" or "N" => ResidueType.Asn,
                "ASP" or "D" => ResidueType.Asp,
                "CYS" or "C" => ResidueType.Cys,
                "GLN" or "Q" => ResidueType.Gln,
                "GLU" or "E" => ResidueType.Glu,
                "GLY" or "G" => ResidueType.Gly,
                "HIS" or "H" => ResidueType.His,
                "ILE" or "I" => ResidueType.Ile,
                "LEU" or "L" => ResidueType.Leu,
                "LYS" or "K" => ResidueType.Lys,
                "MET" or "M" => ResidueType.Met,
                "PHE" or "F" => ResidueType.Phe,
                "PRO" or "P" => ResidueType.Pro,
                "SER" or "S" => ResidueType.Ser,
                "THR" or "T" => ResidueType.Thr,
                "TRP" or "W" => ResidueType.Trp,
                "TYR" or "Y" => ResidueType.Tyr,
                "VAL" or "V" => ResidueType.Val,

                // Modified Amino Acids
                "MSE" => ResidueType.Mse,
                "SEC" or "U" => ResidueType.Sec,
                "PYL" or "O" => ResidueType.Pyl,
                "HYP" => ResidueType.Hyp,

                // Ligands / Cofactors
                "ATP" => ResidueType.Atp,
                "ADP" => ResidueType.Adp,
                "AMP" => ResidueType.Amp,
                "GTP" => ResidueType.Gtp,
                "GDP" => ResidueType.Gdp,
                "NAD" => ResidueType.Nad,
                "NAI" => ResidueType.Nadh,
                "FAD" => ResidueType.Fad,
                "HEM" => ResidueType.Hem,
                "COA" => ResidueType.Coa,

                // Water
                "HOH" or "WAT" or "H2O" => ResidueType.Hoh,

                _ => ResidueType.Unknown
            };
        }

        public static readonly Dictionary<ResidueType, ResidueCategory> ResidueToCategory =
            new Dictionary<ResidueType, ResidueCategory>
        {
        // Amino Acids
        { ResidueType.Ala, ResidueCategory.AminoAcid },
        { ResidueType.Arg, ResidueCategory.AminoAcid },
        { ResidueType.Asn, ResidueCategory.AminoAcid },
        { ResidueType.Asp, ResidueCategory.AminoAcid },
        { ResidueType.Cys, ResidueCategory.AminoAcid },
        { ResidueType.Gln, ResidueCategory.AminoAcid },
        { ResidueType.Glu, ResidueCategory.AminoAcid },
        { ResidueType.Gly, ResidueCategory.AminoAcid },
        { ResidueType.His, ResidueCategory.AminoAcid },
        { ResidueType.Ile, ResidueCategory.AminoAcid },
        { ResidueType.Leu, ResidueCategory.AminoAcid },
        { ResidueType.Lys, ResidueCategory.AminoAcid },
        { ResidueType.Met, ResidueCategory.AminoAcid },
        { ResidueType.Phe, ResidueCategory.AminoAcid },
        { ResidueType.Pro, ResidueCategory.AminoAcid },
        { ResidueType.Ser, ResidueCategory.AminoAcid },
        { ResidueType.Thr, ResidueCategory.AminoAcid },
        { ResidueType.Trp, ResidueCategory.AminoAcid },
        { ResidueType.Tyr, ResidueCategory.AminoAcid },
        { ResidueType.Val, ResidueCategory.AminoAcid },

        // Modified
        { ResidueType.Mse, ResidueCategory.ModifiedResidue },
        { ResidueType.Sec, ResidueCategory.ModifiedResidue },
        { ResidueType.Pyl, ResidueCategory.ModifiedResidue },
        { ResidueType.Hyp, ResidueCategory.ModifiedResidue },

        // Ligands
        { ResidueType.Atp,  ResidueCategory.Ligand },
        { ResidueType.Adp,  ResidueCategory.Ligand },
        { ResidueType.Amp,  ResidueCategory.Ligand },
        { ResidueType.Gtp,  ResidueCategory.Ligand },
        { ResidueType.Gdp,  ResidueCategory.Ligand },
        { ResidueType.Nad,  ResidueCategory.Ligand },
        { ResidueType.Nadh, ResidueCategory.Ligand },
        { ResidueType.Fad,  ResidueCategory.Ligand },
        { ResidueType.Hem,  ResidueCategory.Ligand },
        { ResidueType.Coa,  ResidueCategory.Ligand },

        // Water
        { ResidueType.Hoh, ResidueCategory.Water },

        { ResidueType.Unknown, ResidueCategory.Unknown },
        };

        public static char To1Char(this ResidueType type)
        {
            switch (type)
            {
                case ResidueType.Ala: return 'A';
                case ResidueType.Arg: return 'R';
                case ResidueType.Asn: return 'N';
                case ResidueType.Asp: return 'D';
                case ResidueType.Cys: return 'C';
                case ResidueType.Gln: return 'Q';
                case ResidueType.Glu: return 'E';
                case ResidueType.Gly: return 'G';
                case ResidueType.His: return 'H';
                case ResidueType.Ile: return 'I';
                case ResidueType.Leu: return 'L';
                case ResidueType.Lys: return 'K';
                case ResidueType.Met: return 'M';
                case ResidueType.Phe: return 'F';
                case ResidueType.Pro: return 'P';
                case ResidueType.Ser: return 'S';
                case ResidueType.Thr: return 'T';
                case ResidueType.Trp: return 'W';
                case ResidueType.Tyr: return 'Y';
                case ResidueType.Val: return 'V';

                case ResidueType.Mse: return 'M'; // Selenomethionine
                case ResidueType.Sec: return 'U'; // Selenocysteine
                case ResidueType.Pyl: return 'O'; // Pyrrolysine
                case ResidueType.Hyp: return 'P'; // Hydroxyproline

                default:
                    return '-';
            }
        }

        public static ResidueType Parse(string rawName)
        {
            return rawName?.ToUpperInvariant() switch
            {
                // Standard Amino Acids
                "ALA" or "A" => ResidueType.Ala,
                "ARG" or "R" => ResidueType.Arg,
                "ASN" or "N" => ResidueType.Asn,
                "ASP" or "D" => ResidueType.Asp,
                "CYS" or "C" => ResidueType.Cys,
                "GLN" or "Q" => ResidueType.Gln,
                "GLU" or "E" => ResidueType.Glu,
                "GLY" or "G" => ResidueType.Gly,
                "HIS" or "H" => ResidueType.His,
                "ILE" or "I" => ResidueType.Ile,
                "LEU" or "L" => ResidueType.Leu,
                "LYS" or "K" => ResidueType.Lys,
                "MET" or "M" => ResidueType.Met,
                "PHE" or "F" => ResidueType.Phe,
                "PRO" or "P" => ResidueType.Pro,
                "SER" or "SEP" or "S" => ResidueType.Ser,
                "THR" or "T" => ResidueType.Thr,
                "TRP" or "W" => ResidueType.Trp,
                "TYR" or "Y" => ResidueType.Tyr,
                "VAL" or "V" => ResidueType.Val,

                // Modified Amino Acids
                "MSE" => ResidueType.Mse,
                "SEC" or "U" => ResidueType.Sec,
                "PYL" or "O" => ResidueType.Pyl,
                "HYP" => ResidueType.Hyp,

                // Ligands / Cofactors
                "ATP" => ResidueType.Atp,
                "ADP" => ResidueType.Adp,
                "AMP" => ResidueType.Amp,
                "GTP" => ResidueType.Gtp,
                "GDP" => ResidueType.Gdp,
                "NAD" => ResidueType.Nad,
                "NAI" => ResidueType.Nadh,
                "FAD" => ResidueType.Fad,
                "HEM" => ResidueType.Hem,
                "COA" => ResidueType.Coa,

                // Water
                "HOH" or "WAT" or "H2O" => ResidueType.Hoh,

                _ => ResidueType.Unknown
            };
        }

        public static ResidueCategory GetCategory(this ResidueType residue)
        {
            return ResidueToCategory.TryGetValue(residue, out ResidueCategory category)
                ? category
                : ResidueCategory.Unknown;
        }

        public static ResidueCategory GetCategory(string rawName)
        {
            return GetCategory(Parse(rawName));
        }
    }

    #endregion
}
