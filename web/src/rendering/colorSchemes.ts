import { Color, SRGBColorSpace } from 'three';
import type { Element } from '../molecules/elements';
import type { Residue } from '../molecules/pdb';
import { isPolymer, type ResidueType } from '../molecules/residues';
import type { ProteinSurface } from '../surface/builder';

export type ColorScheme = 'chain' | 'residue' | 'rainbow' | 'element';

export const COLOR_SCHEMES: { id: ColorScheme; label: string }[] = [
  { id: 'chain', label: 'Chain' },
  { id: 'residue', label: 'Residue' },
  { id: 'rainbow', label: 'Rainbow' },
  { id: 'element', label: 'Element' },
];

export interface LegendEntry {
  label: string;
  /** CSS colour. */
  color: string;
  /** Chain index for chain entries (so the legend can toggle them), otherwise -1. */
  chain: number;
}

/** sRGB colour as 0..1 components. */
type RGB = [number, number, number];

const hex = (value: number): RGB => [((value >> 16) & 0xff) / 255, ((value >> 8) & 0xff) / 255, (value & 0xff) / 255];

export const CHAIN_PALETTE: RGB[] = [
  0x4e9be6, 0xf2a541, 0x63c59a, 0xe5676f,
  0xa78bda, 0x5cc8d6, 0xe88fc7, 0xc5d860,
  0x8d9db6, 0xf07f4f, 0x3fb8af, 0xd6b56b,
].map(hex);

const HYDROPHOBIC = hex(0xe9c46a);
const POLAR = hex(0x8ad1b0);
const POSITIVE = hex(0x5b8def);
const NEGATIVE = hex(0xe76f6f);
const LIGAND = hex(0xc77ddb);
const OTHER = hex(0xa8adb5);

const HYDROPHOBIC_TYPES = new Set<ResidueType>(['Ala', 'Val', 'Leu', 'Ile', 'Met', 'Phe', 'Trp', 'Pro', 'Gly', 'Mse']);
const POLAR_TYPES = new Set<ResidueType>(['Ser', 'Thr', 'Asn', 'Gln', 'Tyr', 'Cys', 'Sec', 'Hyp']);
const POSITIVE_TYPES = new Set<ResidueType>(['Lys', 'Arg', 'His', 'Pyl']);
const NEGATIVE_TYPES = new Set<ResidueType>(['Asp', 'Glu']);

const ELEMENT_COLORS: Partial<Record<Element, RGB>> = {
  C: hex(0xb8bec6),
  N: hex(0x5b7fe0),
  O: hex(0xe5534b),
  S: hex(0xf0d04a),
  P: hex(0xf29a3d),
  H: hex(0xf4f4f4),
};

// Blue at the N-terminus through to red at the C-terminus.
const RAINBOW_START = 0.66;
const rainbow = (t: number): RGB => hsv(RAINBOW_START * (1 - t), 0.62, 0.92);

export function chainColor(chain: number): RGB {
  return chain < 0 ? OTHER : CHAIN_PALETTE[chain % CHAIN_PALETTE.length];
}

/** Linear-space RGB for every atom in the protein under the given scheme. */
export function atomColors(surface: ProteinSurface, scheme: ColorScheme): Float32Array {
  const { protein, atomResidue, atomChain } = surface;
  const colors = new Float32Array(protein.atoms.length * 3);
  const sequence = scheme === 'rainbow' ? sequencePositions(protein.residues) : null;
  const linear = new Color();

  for (let a = 0; a < protein.atoms.length; a++) {
    const residue = protein.residues[atomResidue[a]];
    let rgb: RGB;
    switch (scheme) {
      case 'chain':
        rgb = chainColor(atomChain[a]);
        break;
      case 'residue':
        rgb = propertyColor(residue);
        break;
      case 'rainbow': {
        const t = sequence![atomResidue[a]];
        rgb = t < 0 ? OTHER : rainbow(t);
        break;
      }
      case 'element':
        rgb = ELEMENT_COLORS[protein.atoms[a].element] ?? OTHER;
        break;
    }
    linear.setRGB(rgb[0], rgb[1], rgb[2], SRGBColorSpace);
    colors[a * 3] = linear.r;
    colors[a * 3 + 1] = linear.g;
    colors[a * 3 + 2] = linear.b;
  }

  return colors;
}

export function legend(surface: ProteinSurface, scheme: ColorScheme): LegendEntry[] {
  switch (scheme) {
    case 'chain':
      return surface.chains.map((c) => entry(`Chain ${c.chainId.trim() || '(blank)'}`, chainColor(c.index), c.index));
    case 'residue':
      return [
        entry('Hydrophobic', HYDROPHOBIC), entry('Polar', POLAR), entry('Positive', POSITIVE),
        entry('Negative', NEGATIVE), entry('Ligand', LIGAND), entry('Other', OTHER),
      ];
    case 'rainbow':
      return [entry('N-terminus', rainbow(0)), entry('C-terminus', rainbow(1))];
    case 'element':
      return [
        entry('Carbon', ELEMENT_COLORS.C!), entry('Nitrogen', ELEMENT_COLORS.N!), entry('Oxygen', ELEMENT_COLORS.O!),
        entry('Sulfur', ELEMENT_COLORS.S!), entry('Phosphorus', ELEMENT_COLORS.P!), entry('Other', OTHER),
      ];
  }
}

/** CSS gradient for the rainbow legend. */
export function rainbowGradient(): string {
  const stops = [0, 0.25, 0.5, 0.75, 1].map((t) => `${css(rainbow(t))} ${t * 100}%`);
  return `linear-gradient(90deg, ${stops.join(', ')})`;
}

function propertyColor(residue: Residue): RGB {
  if (residue.category === 'Ligand') return LIGAND;
  if (!isPolymer(residue.category)) return OTHER;
  if (HYDROPHOBIC_TYPES.has(residue.type)) return HYDROPHOBIC;
  if (POLAR_TYPES.has(residue.type)) return POLAR;
  if (POSITIVE_TYPES.has(residue.type)) return POSITIVE;
  if (NEGATIVE_TYPES.has(residue.type)) return NEGATIVE;
  return OTHER;
}

/** 0..1 position of each residue along its chain's amino-acid sequence; -1 for non-polymer residues. */
function sequencePositions(residues: Residue[]): Float32Array {
  const result = new Float32Array(residues.length).fill(-1);
  const byChain = new Map<string, number[]>();
  residues.forEach((r, i) => {
    if (!isPolymer(r.category)) return;
    let list = byChain.get(r.chainId);
    if (!list) byChain.set(r.chainId, (list = []));
    list.push(i);
  });
  for (const ordered of byChain.values())
    ordered.forEach((r, i) => (result[r] = ordered.length === 1 ? 0 : i / (ordered.length - 1)));
  return result;
}

function entry(label: string, rgb: RGB, chain = -1): LegendEntry {
  return { label, color: css(rgb), chain };
}

function css([r, g, b]: RGB): string {
  return `rgb(${Math.round(r * 255)} ${Math.round(g * 255)} ${Math.round(b * 255)})`;
}

function hsv(h: number, s: number, v: number): RGB {
  const i = Math.floor(h * 6);
  const f = h * 6 - i;
  const p = v * (1 - s), q = v * (1 - f * s), t = v * (1 - (1 - f) * s);
  switch (((i % 6) + 6) % 6) {
    case 0: return [v, t, p];
    case 1: return [q, v, p];
    case 2: return [p, v, t];
    case 3: return [p, q, v];
    case 4: return [t, p, v];
    default: return [v, p, q];
  }
}
