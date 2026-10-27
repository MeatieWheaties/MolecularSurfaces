using MolecularSurfaces.Molecules;
using MolecularSurfaces.Molecules.Proteins;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MolecularSurfaces.Rendering
{
    public enum ColorScheme
    {
        Chain,
        ResidueProperty,
        Rainbow,
        Element,
    }

    public struct LegendEntry
    {
        public string Label;
        public Color Color;
        /// <summary>Chain index for chain entries (so the legend can toggle them), otherwise -1.</summary>
        public int Chain;
    }

    public static class ColorSchemes
    {
        public static readonly Color[] ChainPalette =
        {
            Hex(0x4E9BE6), Hex(0xF2A541), Hex(0x63C59A), Hex(0xE5676F),
            Hex(0xA78BDA), Hex(0x5CC8D6), Hex(0xE88FC7), Hex(0xC5D860),
            Hex(0x8D9DB6), Hex(0xF07F4F), Hex(0x3FB8AF), Hex(0xD6B56B),
        };

        static readonly Color Hydrophobic = Hex(0xE9C46A);
        static readonly Color Polar = Hex(0x8AD1B0);
        static readonly Color Positive = Hex(0x5B8DEF);
        static readonly Color Negative = Hex(0xE76F6F);
        static readonly Color Ligand = Hex(0xC77DDB);
        static readonly Color Other = Hex(0xA8ADB5);

        static readonly HashSet<ResidueType> _Hydrophobic = new HashSet<ResidueType>
        {
            ResidueType.Ala, ResidueType.Val, ResidueType.Leu, ResidueType.Ile, ResidueType.Met,
            ResidueType.Phe, ResidueType.Trp, ResidueType.Pro, ResidueType.Gly, ResidueType.Mse,
        };
        static readonly HashSet<ResidueType> _Polar = new HashSet<ResidueType>
        {
            ResidueType.Ser, ResidueType.Thr, ResidueType.Asn, ResidueType.Gln, ResidueType.Tyr,
            ResidueType.Cys, ResidueType.Sec, ResidueType.Hyp,
        };
        static readonly HashSet<ResidueType> _Positive = new HashSet<ResidueType> { ResidueType.Lys, ResidueType.Arg, ResidueType.His, ResidueType.Pyl };
        static readonly HashSet<ResidueType> _Negative = new HashSet<ResidueType> { ResidueType.Asp, ResidueType.Glu };

        public static string Label(this ColorScheme scheme)
        {
            switch (scheme)
            {
                case ColorScheme.Chain: return "Chain";
                case ColorScheme.ResidueProperty: return "Residue";
                case ColorScheme.Rainbow: return "Rainbow";
                case ColorScheme.Element: return "Element";
                default: return scheme.ToString();
            }
        }

        /// <summary>Colour for every atom in the protein under the given scheme.</summary>
        public static Color[] AtomColors(ProteinSurface surface, ColorScheme scheme)
        {
            var _protein = surface.Protein;
            var _colors = new Color[_protein.Atoms.Length];

            float[] _sequencePosition = scheme == ColorScheme.Rainbow ? SequencePositions(surface) : null;

            for (int a = 0; a < _colors.Length; a++)
            {
                var _residue = _protein.Residues[surface.AtomResidue[a]];
                switch (scheme)
                {
                    case ColorScheme.Chain:
                        _colors[a] = ChainColor(surface.AtomChain[a]);
                        break;
                    case ColorScheme.ResidueProperty:
                        _colors[a] = PropertyColor(_residue);
                        break;
                    case ColorScheme.Rainbow:
                        var _t = _sequencePosition[surface.AtomResidue[a]];
                        // Blue at the N-terminus through to red at the C-terminus.
                        _colors[a] = _t < 0 ? Other : Color.HSVToRGB(Mathf.Lerp(0.66f, 0f, _t), 0.62f, 0.92f);
                        break;
                    case ColorScheme.Element:
                        _colors[a] = ElementColor(_protein.Atoms[a].Element);
                        break;
                }
            }

            return _colors;
        }

        public static List<LegendEntry> Legend(ProteinSurface surface, ColorScheme scheme)
        {
            switch (scheme)
            {
                case ColorScheme.Chain:
                    return surface.Chains.Select(c => new LegendEntry { Label = $"Chain {c.ChainId}", Color = ChainColor(c.Index), Chain = c.Index }).ToList();
                case ColorScheme.ResidueProperty:
                    return new List<LegendEntry>
                    {
                        Entry("Hydrophobic", Hydrophobic), Entry("Polar", Polar), Entry("Positive", Positive),
                        Entry("Negative", Negative), Entry("Ligand", Ligand), Entry("Other", Other),
                    };
                case ColorScheme.Rainbow:
                    return new List<LegendEntry>
                    {
                        Entry("N-terminus", Color.HSVToRGB(0.66f, 0.62f, 0.92f)),
                        Entry("C-terminus", Color.HSVToRGB(0f, 0.62f, 0.92f)),
                    };
                case ColorScheme.Element:
                    return new List<LegendEntry>
                    {
                        Entry("Carbon", ElementColor(Element.C)), Entry("Nitrogen", ElementColor(Element.N)),
                        Entry("Oxygen", ElementColor(Element.O)), Entry("Sulfur", ElementColor(Element.S)),
                        Entry("Phosphorus", ElementColor(Element.P)), Entry("Other", Other),
                    };
                default:
                    return new List<LegendEntry>();
            }
        }

        public static Color ChainColor(int chain) => chain < 0 ? Other : ChainPalette[chain % ChainPalette.Length];

        static Color PropertyColor(Residue residue)
        {
            switch (residue.Category)
            {
                case ResidueCategory.Ligand: return Ligand;
                case ResidueCategory.AminoAcid:
                case ResidueCategory.ModifiedResidue:
                    if (_Hydrophobic.Contains(residue.Type)) return Hydrophobic;
                    if (_Polar.Contains(residue.Type)) return Polar;
                    if (_Positive.Contains(residue.Type)) return Positive;
                    if (_Negative.Contains(residue.Type)) return Negative;
                    return Other;
                default: return Other;
            }
        }

        static Color ElementColor(Element element)
        {
            switch (element)
            {
                case Element.C: return Hex(0xB8BEC6);
                case Element.N: return Hex(0x5B7FE0);
                case Element.O: return Hex(0xE5534B);
                case Element.S: return Hex(0xF0D04A);
                case Element.P: return Hex(0xF29A3D);
                case Element.H: return Hex(0xF4F4F4);
                default: return Other;
            }
        }

        /// <summary>0..1 position of each residue along its chain's amino-acid sequence; -1 for non-polymer residues.</summary>
        static float[] SequencePositions(ProteinSurface surface)
        {
            var _residues = surface.Protein.Residues;
            var _result = Enumerable.Repeat(-1f, _residues.Length).ToArray();

            var _byChain = Enumerable.Range(0, _residues.Length)
                .Where(r => _residues[r].Category == ResidueCategory.AminoAcid || _residues[r].Category == ResidueCategory.ModifiedResidue)
                .GroupBy(r => _residues[r].Key.ChainID);

            foreach (var _chain in _byChain)
            {
                var _ordered = _chain.ToArray();
                for (int i = 0; i < _ordered.Length; i++)
                    _result[_ordered[i]] = _ordered.Length == 1 ? 0 : i / (float)(_ordered.Length - 1);
            }

            return _result;
        }

        static LegendEntry Entry(string label, Color color) => new LegendEntry { Label = label, Color = color, Chain = -1 };

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }
}
