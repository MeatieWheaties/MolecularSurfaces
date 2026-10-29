import { parseElement, vdwRadius, type Element } from './elements';
import { parseResidue, residueCategory, type ResidueCategory, type ResidueType } from './residues';

export interface Atom {
  /** Atom name from the file, e.g. "CA". */
  name: string;
  element: Element;
  x: number;
  y: number;
  z: number;
  /** Van der Waals radius in Å. */
  radius: number;
  isHet: boolean;
}

export interface Residue {
  chainId: string;
  number: number;
  insertCode: string;
  /** Residue name as written in the file, e.g. "ALA" or "HEM". */
  name: string;
  type: ResidueType;
  category: ResidueCategory;
  /** Indices into Protein.atoms. */
  atoms: number[];
}

export interface Protein {
  id: string;
  /** Joined TITLE records, or "" if the file has none. */
  title: string;
  /** Atoms in file order. */
  atoms: Atom[];
  /** Residues grouped by chain (in order of first appearance), then in file order. */
  residues: Residue[];
}

/**
 * Parses a PDB-format file. Only the first model of multi-model (NMR) entries is used,
 * and only the first alternate location of each atom.
 */
export function parsePDB(id: string, text: string): Protein {
  const atoms: Atom[] = [];
  // Map preserves insertion order, which gives chains in order of first appearance.
  const chains = new Map<string, Map<string, Residue>>();
  const titles: string[] = [];

  for (const line of text.split(/\r?\n/)) {
    const record = line.slice(0, 6);

    if (record === 'ENDMDL') break;
    if (record === 'TITLE ') {
      titles.push(line.slice(10, 80).trim());
      continue;
    }
    if (record !== 'ATOM  ' && record !== 'HETATM') continue;

    const name = line.slice(12, 16).trim();
    const altLoc = line[16] ?? ' ';
    const chainId = line[21] ?? ' ';
    const number = parseInt(line.slice(22, 26), 10) || 0;
    const insertCode = line[26] ?? ' ';
    const residueName = line.slice(17, 20).trim();

    let residues = chains.get(chainId);
    if (!residues) chains.set(chainId, (residues = new Map()));

    const key = `${number}:${insertCode}`;
    let residue = residues.get(key);
    if (!residue) {
      const type = parseResidue(residueName);
      residue = { chainId, number, insertCode, name: residueName, type, category: residueCategory(type), atoms: [] };
      residues.set(key, residue);
    }

    // Alternate conformers would just stack overlapping spheres; keep the first.
    if (altLoc !== ' ' && residue.atoms.some((a) => atoms[a].name === name)) continue;

    const element = parseElement(line.slice(76, 78));
    const resolved = element !== 'Unknown' ? element : elementFromName(line.slice(12, 16));

    residue.atoms.push(atoms.length);
    atoms.push({
      name,
      element: resolved,
      x: parseFloat(line.slice(30, 38)) || 0,
      y: parseFloat(line.slice(38, 46)) || 0,
      z: parseFloat(line.slice(46, 54)) || 0,
      radius: vdwRadius(resolved),
      isHet: record === 'HETATM',
    });
  }

  const residues = [...chains.values()].flatMap((r) => [...r.values()]);
  return { id, title: titles.join(' ').replace(/\s+/g, ' ').trim(), atoms, residues };
}

/**
 * Older files can leave the element columns blank. By PDB convention the element is
 * right-aligned in the first two characters of the atom name field.
 */
function elementFromName(field: string): Element {
  const two = field.slice(0, 2).trim();
  if (/^[A-Za-z]{2}$/.test(two)) {
    // Two letters starting in column 13 is a two-letter element (e.g. "FE"), unless it's
    // a hydrogen name such as "HD21" in files that start those at column 13.
    const element = parseElement(two);
    if (element !== 'Unknown' && !(two[0].toUpperCase() === 'H' && field.trim().length === 4)) return element;
  }
  const first = field.replace(/^[\s\d]+/, '')[0] ?? '';
  return parseElement(first);
}
