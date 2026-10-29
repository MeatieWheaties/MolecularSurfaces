export type ResidueCategory =
  | 'AminoAcid'
  | 'Nucleotide'
  | 'ModifiedResidue'
  | 'Ligand'
  | 'Ion'
  | 'Water'
  | 'Unknown';

export type ResidueType =
  // Standard amino acids
  | 'Ala' | 'Arg' | 'Asn' | 'Asp' | 'Cys' | 'Gln' | 'Glu' | 'Gly' | 'His' | 'Ile'
  | 'Leu' | 'Lys' | 'Met' | 'Phe' | 'Pro' | 'Ser' | 'Thr' | 'Trp' | 'Tyr' | 'Val'
  // Common modified / non-standard amino acids
  | 'Mse' | 'Sec' | 'Pyl' | 'Hyp'
  // Common ligands / cofactors
  | 'Atp' | 'Adp' | 'Amp' | 'Gtp' | 'Gdp' | 'Nad' | 'Nadh' | 'Fad' | 'Hem' | 'Coa'
  | 'Hoh'
  | 'Unknown';

const AMINO_ACIDS: ResidueType[] = [
  'Ala', 'Arg', 'Asn', 'Asp', 'Cys', 'Gln', 'Glu', 'Gly', 'His', 'Ile',
  'Leu', 'Lys', 'Met', 'Phe', 'Pro', 'Ser', 'Thr', 'Trp', 'Tyr', 'Val',
];
const MODIFIED: ResidueType[] = ['Mse', 'Sec', 'Pyl', 'Hyp'];
const LIGANDS: ResidueType[] = ['Atp', 'Adp', 'Amp', 'Gtp', 'Gdp', 'Nad', 'Nadh', 'Fad', 'Hem', 'Coa'];

const NAMES = new Map<string, ResidueType>([
  ...[...AMINO_ACIDS, ...MODIFIED, ...LIGANDS].map((t) => [t.toUpperCase(), t] as [string, ResidueType]),
  // One-letter codes and aliases.
  ['A', 'Ala'], ['R', 'Arg'], ['N', 'Asn'], ['D', 'Asp'], ['C', 'Cys'],
  ['Q', 'Gln'], ['E', 'Glu'], ['G', 'Gly'], ['H', 'His'], ['I', 'Ile'],
  ['L', 'Leu'], ['K', 'Lys'], ['M', 'Met'], ['F', 'Phe'], ['P', 'Pro'],
  ['S', 'Ser'], ['T', 'Thr'], ['W', 'Trp'], ['Y', 'Tyr'], ['V', 'Val'],
  ['SEP', 'Ser'], ['U', 'Sec'], ['O', 'Pyl'], ['NAI', 'Nadh'],
  ['HOH', 'Hoh'], ['WAT', 'Hoh'], ['H2O', 'Hoh'],
]);

const CATEGORY = new Map<ResidueType, ResidueCategory>([
  ...AMINO_ACIDS.map((t) => [t, 'AminoAcid'] as [ResidueType, ResidueCategory]),
  ...MODIFIED.map((t) => [t, 'ModifiedResidue'] as [ResidueType, ResidueCategory]),
  ...LIGANDS.map((t) => [t, 'Ligand'] as [ResidueType, ResidueCategory]),
  ['Hoh', 'Water'],
]);

/** Parses a residue name (three-letter code, one-letter code or alias), case-insensitively. */
export function parseResidue(name: string): ResidueType {
  return NAMES.get(name.trim().toUpperCase()) ?? 'Unknown';
}

export function residueCategory(type: ResidueType): ResidueCategory {
  return CATEGORY.get(type) ?? 'Unknown';
}

export function isPolymer(category: ResidueCategory): boolean {
  return category === 'AminoAcid' || category === 'ModifiedResidue';
}
