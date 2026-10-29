export type Element =
  // The Big Six (CHNOPS) - core of all biomolecules
  | 'C' | 'H' | 'N' | 'O' | 'P' | 'S'
  // Macrominerals
  | 'Ca' | 'K' | 'Na' | 'Mg' | 'Cl'
  // Trace / metal cofactors
  | 'Fe' | 'Zn' | 'Cu' | 'Mn' | 'Co' | 'Ni' | 'Se'
  // Halogens (ligands, phasing)
  | 'F' | 'Br' | 'I'
  // Other occasionally modeled
  | 'B' | 'Mo'
  | 'Unknown';

/**
 * Van der Waals radii in Ångströms.
 * Primary source: Bondi (1964) with common biochemistry supplements.
 * Where Bondi values are unavailable, Alvarez (2013) or empirical values are used.
 */
const VDW_RADIUS: Record<Element, number> = {
  C: 1.7, H: 1.2, N: 1.55, O: 1.52, P: 1.8, S: 1.8,
  Ca: 2.31, K: 2.75, Na: 2.27, Mg: 1.73, Cl: 1.75,
  Fe: 2.05, Zn: 2.1, Cu: 2.0, Mn: 2.05, Co: 2.0, Ni: 2.0, Se: 1.9,
  F: 1.47, Br: 1.85, I: 1.98,
  B: 1.92, Mo: 2.1,
  // Fall back to a carbon-like radius.
  Unknown: 1.7,
};

const SYMBOLS = new Map<string, Element>(
  (Object.keys(VDW_RADIUS) as Element[])
    .filter((e) => e !== 'Unknown')
    .map((e) => [e.toUpperCase(), e]),
);

/** Parses an element symbol from a PDB file, case-insensitively. */
export function parseElement(symbol: string): Element {
  return SYMBOLS.get(symbol.trim().toUpperCase()) ?? 'Unknown';
}

export function vdwRadius(element: Element): number {
  return VDW_RADIUS[element];
}
