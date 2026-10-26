using System;
using System.Collections.Generic;
using UnityEngine;

namespace MolecularSurfaces.Molecules
{
    public class Atom
    {
        public Element Element;
        public Vector3 Position;
        public float Radius;
        public int Id;
        public string Name;

        public Atom(Atom copy)
        {
            Element = copy.Element;
            Position = copy.Position;
            Radius = copy.Radius;
            Id = copy.Id;
            Name = copy.Name;
        }

        public Atom(Element element, Vector3 position, int id)
        {
            Name = element.ToString();
            Element = element;
            Position = position;
            Radius = ElementProperties.GetVdwRadius(Element);
            Id = id;
        }

        public Atom(string element, Vector3 position, int id)
        {
            Name = element;
            Element = ElementParser.Parse(element);
            Position = position;
            Radius = ElementProperties.GetVdwRadius(Element);
            Id = id;
        }

        public Atom(string name, string element, Vector3 position, int id)
        {
            Name = name;
            Element = ElementParser.Parse(element);
            Position = position;
            Radius = ElementProperties.GetVdwRadius(Element);
            Id = id;
        }

        public Atom(string name, Element element, Vector3 position, int id)
        {
            Name = name;
            Element = element;
            Position = position;
            Radius = ElementProperties.GetVdwRadius(Element);
            Id = id;
        }
    }

    public class Bond
    {
        int AtomA;
        int AtomB;
    }

    public class AtomGroup
    {
        public Guid Guid { get; private set; } = Guid.NewGuid();

        public string Kind;           // "Residue", "Ligand", "Water", "UnitCell", "Selection", etc.
        public int[] Atoms;

        public AtomGroup(string kind, int[] atoms)
        {
            Kind = kind;
            Atoms = atoms;
        }
    }

    #region Atomic Configuration

    public enum Element
    {
        // The Big Six (CHNOPS) - core of all biomolecules
        C,   // Carbon
        H,   // Hydrogen
        N,   // Nitrogen
        O,   // Oxygen
        P,   // Phosphorus
        S,   // Sulfur

        // Macrominerals
        Ca,  // Calcium
        K,   // Potassium
        Na,  // Sodium
        Mg,  // Magnesium
        Cl,  // Chlorine

        // Trace / Metal cofactors
        Fe,  // Iron
        Zn,  // Zinc
        Cu,  // Copper
        Mn,  // Manganese
        Co,  // Cobalt
        Ni,  // Nickel
        Se,  // Selenium (selenomethionine - very common in X-ray crystallography)

        // Halogens (ligands, phasing)
        F,   // Fluorine
        Br,  // Bromine
        I,   // Iodine

        // Other occasionally modeled
        B,   // Boron (protease inhibitors)
        Mo,  // Molybdenum

        // Catch-all for unknown/unsupported elements
        Unknown
    }

    public static class ElementParser
    {
        public static readonly Dictionary<string, Element> SymbolToElement =
            new Dictionary<string, Element>(StringComparer.OrdinalIgnoreCase)
        {
        // The Big Six
        { "C",  Element.C  },
        { "H",  Element.H  },
        { "N",  Element.N  },
        { "O",  Element.O  },
        { "P",  Element.P  },
        { "S",  Element.S  },

        // Macrominerals
        { "CA", Element.Ca },
        { "K",  Element.K  },
        { "NA", Element.Na },
        { "MG", Element.Mg },
        { "CL", Element.Cl },

        // Trace / Metal cofactors
        { "FE", Element.Fe },
        { "ZN", Element.Zn },
        { "CU", Element.Cu },
        { "MN", Element.Mn },
        { "CO", Element.Co },
        { "NI", Element.Ni },
        { "SE", Element.Se },

        // Halogens
        { "F",  Element.F  },
        { "BR", Element.Br },
        { "I",  Element.I  },

        // Other
        { "B",  Element.B  },
        { "MO", Element.Mo },
        };

        /// <summary>
        /// Parses the element symbol from a PDB or mmCIF file.
        /// Returns Element.Unknown if the symbol is not recognized.
        /// </summary>
        public static Element Parse(string rawSymbol)
        {
            if (string.IsNullOrWhiteSpace(rawSymbol))
                return Element.Unknown;

            string trimmed = rawSymbol.Trim();

            return SymbolToElement.TryGetValue(trimmed, out Element element)
                ? element
                : Element.Unknown;
        }
    }

    public static class ElementProperties
    {
        /// <summary>
        /// Van der Waals radii in Angstroms (Å).
        /// Primary source: Bondi (1964) with common biochemistry supplements.
        /// Where Bondi values are unavailable, Alvarez (2013) or empirical values are used.
        /// </summary>
        public static readonly Dictionary<Element, float> VanDerWaalsRadius =
            new Dictionary<Element, float>
        {
        // The Big Six
        { Element.C,  1.70f },
        { Element.H,  1.20f },
        { Element.N,  1.55f },
        { Element.O,  1.52f },
        { Element.P,  1.80f },
        { Element.S,  1.80f },

        // Macrominerals
        { Element.Ca, 2.31f },
        { Element.K,  2.75f },
        { Element.Na, 2.27f },
        { Element.Mg, 1.73f },
        { Element.Cl, 1.75f },

        // Trace / Metal cofactors
        { Element.Fe, 2.05f },
        { Element.Zn, 2.10f },
        { Element.Cu, 2.00f },
        { Element.Mn, 2.05f },
        { Element.Co, 2.00f },
        { Element.Ni, 2.00f },
        { Element.Se, 1.90f },

        // Halogens
        { Element.F,  1.47f },
        { Element.Br, 1.85f },
        { Element.I,  1.98f },

        // Other
        { Element.B,  1.92f },
        { Element.Mo, 2.10f },

        // Unknown - use a neutral fallback
        { Element.Unknown, 1.70f },  // fallback to carbon-like radius
        };

        /// <summary>
        /// Looks up the Van der Waals radius for a given element, in Angstroms.
        /// Falls back to the Unknown entry if not found.
        /// </summary>
        public static float GetVdwRadius(Element element)
        {
            return VanDerWaalsRadius.TryGetValue(element, out float radius)
                ? radius
                : VanDerWaalsRadius[Element.Unknown];
        }
    }

    #endregion
}
