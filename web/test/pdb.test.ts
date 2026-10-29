import { describe, expect, it } from 'vitest';
import { parsePDB } from '../src/molecules/pdb';
import crambin from './fixtures/1crn.pdb?raw';

/** Formats a fixed-column ATOM/HETATM record. */
function atomLine(o: { record?: string; serial: number; name: string; alt?: string; res: string; chain: string; seq: number; x: number; y: number; z: number; element?: string }): string {
  return (
    (o.record ?? 'ATOM').padEnd(6) +
    String(o.serial).padStart(5) + ' ' +
    o.name.padEnd(4) +
    (o.alt ?? ' ') +
    o.res.padStart(3) + ' ' +
    o.chain +
    String(o.seq).padStart(4) + '    ' +
    o.x.toFixed(3).padStart(8) + o.y.toFixed(3).padStart(8) + o.z.toFixed(3).padStart(8) +
    '  1.00  0.00          ' +
    (o.element ?? '').padStart(2)
  );
}

describe('parsePDB', () => {
  it('reads crambin', () => {
    const protein = parsePDB('1CRN', crambin);

    expect(protein.atoms).toHaveLength(327);
    expect(protein.residues).toHaveLength(46);
    expect(new Set(protein.residues.map((r) => r.chainId))).toEqual(new Set(['A']));
    expect(protein.title).toBe('WATER STRUCTURE OF A HYDROPHOBIC PROTEIN AT ATOMIC RESOLUTION. PENTAGON RINGS OF WATER MOLECULES IN CRYSTALS OF CRAMBIN');

    const first = protein.residues[0];
    expect(first).toMatchObject({ number: 1, name: 'THR', type: 'Thr', category: 'AminoAcid' });
    expect(protein.atoms[first.atoms[0]]).toMatchObject({ name: 'N', element: 'N', x: 17.047, y: 14.099, z: 3.625, radius: 1.55 });
    expect(protein.atoms[first.atoms[1]]).toMatchObject({ name: 'CA', element: 'C', radius: 1.7 });
  });

  it('indexes every atom from exactly one residue', () => {
    const protein = parsePDB('1CRN', crambin);
    const seen = protein.residues.flatMap((r) => r.atoms).sort((a, b) => a - b);
    expect(seen).toEqual(protein.atoms.map((_, i) => i));
  });

  it('uses only the first model of multi-model entries', () => {
    const text = [
      'MODEL        1',
      atomLine({ serial: 1, name: ' N', res: 'GLY', chain: 'A', seq: 1, x: 0, y: 0, z: 0, element: 'N' }),
      'ENDMDL',
      'MODEL        2',
      atomLine({ serial: 1, name: ' N', res: 'GLY', chain: 'A', seq: 1, x: 9, y: 9, z: 9, element: 'N' }),
      'ENDMDL',
    ].join('\n');

    const protein = parsePDB('TEST', text);
    expect(protein.atoms).toHaveLength(1);
    expect(protein.atoms[0].x).toBe(0);
  });

  it('keeps only the first alternate location of an atom', () => {
    const text = [
      atomLine({ serial: 1, name: ' CB', alt: 'A', res: 'SER', chain: 'A', seq: 5, x: 1, y: 0, z: 0, element: 'C' }),
      atomLine({ serial: 2, name: ' CB', alt: 'B', res: 'SER', chain: 'A', seq: 5, x: 2, y: 0, z: 0, element: 'C' }),
      atomLine({ serial: 3, name: ' OG', alt: 'A', res: 'SER', chain: 'A', seq: 5, x: 3, y: 0, z: 0, element: 'O' }),
    ].join('\n');

    const protein = parsePDB('TEST', text);
    expect(protein.atoms.map((a) => [a.name, a.x])).toEqual([['CB', 1], ['OG', 3]]);
  });

  it('groups residues by chain and categorises ligands and waters', () => {
    const text = [
      atomLine({ serial: 1, name: ' CA', res: 'ALA', chain: 'A', seq: 1, x: 0, y: 0, z: 0, element: 'C' }),
      atomLine({ serial: 2, name: ' CA', res: 'LYS', chain: 'B', seq: 1, x: 0, y: 0, z: 0, element: 'C' }),
      atomLine({ record: 'HETATM', serial: 3, name: 'FE', res: 'HEM', chain: 'A', seq: 200, x: 0, y: 0, z: 0, element: 'FE' }),
      atomLine({ record: 'HETATM', serial: 4, name: ' O', res: 'HOH', chain: 'A', seq: 300, x: 0, y: 0, z: 0, element: 'O' }),
    ].join('\n');

    const protein = parsePDB('TEST', text);
    expect(protein.residues.map((r) => `${r.chainId}:${r.name}:${r.category}`)).toEqual([
      'A:ALA:AminoAcid',
      'A:HEM:Ligand',
      'A:HOH:Water',
      'B:LYS:AminoAcid',
    ]);
    expect(protein.atoms[2]).toMatchObject({ element: 'Fe', isHet: true, radius: 2.05 });
  });

  it('infers the element from the atom name when the element columns are blank', () => {
    const text = [
      atomLine({ serial: 1, name: ' CA', res: 'ALA', chain: 'A', seq: 1, x: 0, y: 0, z: 0 }),
      atomLine({ serial: 2, name: 'HD21', res: 'ASN', chain: 'A', seq: 2, x: 0, y: 0, z: 0 }),
      atomLine({ record: 'HETATM', serial: 3, name: 'ZN', res: ' ZN', chain: 'A', seq: 3, x: 0, y: 0, z: 0 }),
    ].join('\n');

    expect(parsePDB('TEST', text).atoms.map((a) => a.element)).toEqual(['C', 'H', 'Zn']);
  });
});
