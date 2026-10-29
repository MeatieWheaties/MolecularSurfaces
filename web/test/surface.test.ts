import { describe, expect, it } from 'vitest';
import { AtomGrid } from '../src/geometry/atomGrid';
import { triangulate } from '../src/geometry/marchingCubes';
import { parsePDB } from '../src/molecules/pdb';
import { buildChunk, type ChunkMesh } from '../src/surface/chunk';
import { planChunks, planSurface } from '../src/surface/plan';
import crambin from './fixtures/1crn.pdb?raw';

function sphereField(n: number, center: number, radius: number) {
  const values = new Float32Array(n * n * n);
  for (let z = 0; z < n; z++)
    for (let y = 0; y < n; y++)
      for (let x = 0; x < n; x++)
        values[x + n * (y + n * z)] = Math.hypot(x - center, y - center, z - center) - radius;
  return { values, origin: [0, 0, 0] as [number, number, number], size: [n, n, n] as [number, number, number] };
}

/**
 * Counts edges that aren't shared by exactly two triangles, welding vertices with exactly
 * equal positions (seam vertices from neighbouring chunks must match bit for bit).
 */
function openEdges(positions: Float32Array, indices: Uint32Array): number {
  const weld = new Map<string, number>();
  const id = (v: number) => {
    const key = [0, 1, 2].map((k) => positions[v * 3 + k]).join(',');
    if (!weld.has(key)) weld.set(key, weld.size);
    return weld.get(key)!;
  };
  const edges = new Map<string, number>();
  for (let t = 0; t < indices.length; t += 3) {
    const tri = [id(indices[t]), id(indices[t + 1]), id(indices[t + 2])];
    for (let e = 0; e < 3; e++) {
      const a = tri[e], b = tri[(e + 1) % 3];
      if (a === b) continue;
      const key = a < b ? `${a}-${b}` : `${b}-${a}`;
      edges.set(key, (edges.get(key) ?? 0) + 1);
    }
  }
  return [...edges.values()].filter((count) => count !== 2).length;
}

/** Fraction of triangles whose winding (counter-clockwise = front) faces away from `center`. */
function outwardFraction(positions: Float32Array, indices: Uint32Array, center: number[]): number {
  let outward = 0;
  for (let t = 0; t < indices.length; t += 3) {
    const p = [indices[t], indices[t + 1], indices[t + 2]].map((v) => [0, 1, 2].map((k) => positions[v * 3 + k]));
    const a = p[1].map((v, k) => v - p[0][k]);
    const b = p[2].map((v, k) => v - p[0][k]);
    const n = [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
    const out = p[0].map((v, k) => v - center[k]);
    if (n[0] * out[0] + n[1] * out[1] + n[2] * out[2] > 0) outward++;
  }
  return outward / (indices.length / 3);
}

describe('triangulate', () => {
  it('produces a closed, outward-facing sphere', () => {
    const mesh = triangulate(sphereField(16, 7.5, 5));

    expect(mesh.indices.length).toBeGreaterThan(0);
    for (let v = 0; v < mesh.positions.length; v += 3) {
      const r = Math.hypot(mesh.positions[v] - 7.5, mesh.positions[v + 1] - 7.5, mesh.positions[v + 2] - 7.5);
      expect(Math.abs(r - 5)).toBeLessThan(0.15);
    }
    expect(openEdges(mesh.positions, mesh.indices)).toBe(0);
    expect(outwardFraction(mesh.positions, mesh.indices, [7.5, 7.5, 7.5])).toBe(1);
  });

  it('shares vertices between neighbouring cells', () => {
    const mesh = triangulate(sphereField(16, 7.5, 5));
    const vertices = mesh.positions.length / 3;
    const triangles = mesh.indices.length / 3;
    // A closed genus-0 mesh has V - E + F = 2, i.e. roughly two triangles per vertex.
    expect(vertices - (triangles * 3) / 2 + triangles).toBe(2);
  });
});

describe('surface chunks', () => {
  const protein = parsePDB('1CRN', crambin);

  function buildAll(resolution: number) {
    const plan = planSurface(protein, 1);
    const grid = new AtomGrid(plan.positions, plan.radii, plan.chains[0].atoms);
    const chunks = planChunks(plan, resolution).map((job) => buildChunk(grid, job.origin, job.size, resolution));
    return { plan, chunks, merged: merge(chunks) };
  }

  function merge(chunks: ChunkMesh[]) {
    const positions = new Float32Array(chunks.reduce((s, c) => s + c.positions.length, 0));
    const indices = new Uint32Array(chunks.reduce((s, c) => s + c.indices.length, 0));
    let v = 0, t = 0;
    for (const c of chunks) {
      positions.set(c.positions, v * 3);
      c.indices.forEach((i, k) => (indices[t + k] = i + v));
      v += c.positions.length / 3;
      t += c.indices.length;
    }
    return { positions, indices };
  }

  it('splits crambin into several chunks at x2', () => {
    const { chunks } = buildAll(2);
    expect(chunks.filter((c) => c.indices.length > 0).length).toBeGreaterThan(1);
  });

  it('is watertight across chunk seams', () => {
    for (const resolution of [1, 2]) {
      const { merged } = buildAll(resolution);
      expect(openEdges(merged.positions, merged.indices)).toBe(0);
    }
  });

  it('gives every vertex a unit normal and an atom of its chain', () => {
    const { plan, chunks } = buildAll(1);
    const chainAtoms = new Set(plan.chains[0].atoms);
    for (const c of chunks) {
      for (let v = 0; v < c.atoms.length; v++) {
        expect(chainAtoms.has(c.atoms[v])).toBe(true);
        const length = Math.hypot(c.normals[v * 3], c.normals[v * 3 + 1], c.normals[v * 3 + 2]) / 32767;
        expect(Math.abs(length - 1)).toBeLessThan(0.01);
      }
    }
  });

  it('places each vertex on the surface of the atom it is assigned to', () => {
    const { plan, chunks } = buildAll(2);
    for (const c of chunks)
      for (let v = 0; v < c.atoms.length; v++) {
        const a = c.atoms[v];
        const d = Math.hypot(
          c.positions[v * 3] - plan.positions[a * 3],
          c.positions[v * 3 + 1] - plan.positions[a * 3 + 1],
          c.positions[v * 3 + 2] - plan.positions[a * 3 + 2],
        );
        // Marching cubes cuts corners between samples (0.5 Å apart at x2).
        expect(Math.abs(d - plan.radii[a])).toBeLessThan(0.3);
      }
  });
});
