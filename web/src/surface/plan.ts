import type { Protein } from '../molecules/pdb';
import { CHUNK_CELLS } from './chunk';

export interface ChainPlan {
  chainId: string;
  index: number;
  /** Global indices of the chain's atoms. */
  atoms: Uint32Array;
}

export interface SurfacePlan {
  chains: ChainPlan[];
  /** Index into Protein.residues for each atom. */
  atomResidue: Int32Array;
  /** Index into chains for each atom, or -1 for atoms left out (waters). */
  atomChain: Int32Array;
  /** xyz per atom. */
  positions: Float32Array;
  /** Surface radius per atom (van der Waals radius times the radius scale). */
  radii: Float32Array;
  center: [number, number, number];
  radius: number;
}

export interface ChunkJob {
  chain: number;
  origin: [number, number, number];
  size: [number, number, number];
}

/** Groups atoms into one surface per chain, leaving out waters. */
export function planSurface(protein: Protein, radiusScale: number): SurfacePlan {
  const n = protein.atoms.length;
  const positions = new Float32Array(n * 3);
  const radii = new Float32Array(n);
  protein.atoms.forEach((a, i) => {
    positions.set([a.x, a.y, a.z], i * 3);
    radii[i] = a.radius * radiusScale;
  });

  const atomResidue = new Int32Array(n);
  protein.residues.forEach((r, i) => r.atoms.forEach((a) => (atomResidue[a] = i)));

  // Waters make noisy blobs on the surface, so they're left out.
  const byChain = new Map<string, number[]>();
  for (const residue of protein.residues) {
    if (residue.category === 'Water') continue;
    let list = byChain.get(residue.chainId);
    if (!list) byChain.set(residue.chainId, (list = []));
    list.push(...residue.atoms);
  }

  const chains = [...byChain].map(([chainId, atoms], index) => ({ chainId, index, atoms: Uint32Array.from(atoms) }));
  const atomChain = new Int32Array(n).fill(-1);
  for (const chain of chains) for (const a of chain.atoms) atomChain[a] = chain.index;

  let count = 0;
  const center: [number, number, number] = [0, 0, 0];
  for (const chain of chains)
    for (const a of chain.atoms) {
      for (let k = 0; k < 3; k++) center[k] += positions[a * 3 + k];
      count++;
    }
  if (count > 0) for (let k = 0; k < 3; k++) center[k] /= count;

  let radius = count > 0 ? 0 : 1;
  for (const chain of chains)
    for (const a of chain.atoms)
      radius = Math.max(radius, Math.hypot(positions[a * 3] - center[0], positions[a * 3 + 1] - center[1], positions[a * 3 + 2] - center[2]));

  return { chains, atomResidue, atomChain, positions, radii, center, radius };
}

/**
 * Splits each chain's lattice into chunks of CHUNK_CELLS cells per side. Neighbouring
 * chunks share their boundary samples, so the surface is continuous across them.
 */
export function planChunks(plan: SurfacePlan, resolution: number): ChunkJob[] {
  const jobs: ChunkJob[] = [];

  for (const chain of plan.chains) {
    if (chain.atoms.length === 0) continue;

    const lo = [Infinity, Infinity, Infinity];
    const hi = [-Infinity, -Infinity, -Infinity];
    for (const a of chain.atoms) {
      const r = plan.radii[a];
      for (let k = 0; k < 3; k++) {
        lo[k] = Math.min(lo[k], (plan.positions[a * 3 + k] - r) * resolution);
        hi[k] = Math.max(hi[k], (plan.positions[a * 3 + k] + r) * resolution);
      }
    }
    // One sample of padding so the surface is always closed at the lattice edge.
    const min = lo.map((v) => Math.floor(v) - 1);
    const max = hi.map((v) => Math.ceil(v) + 1);
    const chunks = min.map((m, k) => Math.ceil((max[k] - m) / CHUNK_CELLS));

    for (let cz = 0; cz < chunks[2]; cz++)
      for (let cy = 0; cy < chunks[1]; cy++)
        for (let cx = 0; cx < chunks[0]; cx++) {
          const origin: [number, number, number] = [min[0] + cx * CHUNK_CELLS, min[1] + cy * CHUNK_CELLS, min[2] + cz * CHUNK_CELLS];
          const size = origin.map((o, k) => Math.min(CHUNK_CELLS, max[k] - o) + 1) as [number, number, number];
          if (size.every((s) => s >= 2)) jobs.push({ chain: chain.index, origin, size });
        }
  }

  return jobs;
}
