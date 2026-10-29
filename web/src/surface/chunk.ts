import { AtomGrid } from '../geometry/atomGrid';
import { triangulate } from '../geometry/marchingCubes';

/** Cells per chunk side. Chunks are the unit of work handed to the worker pool. */
export const CHUNK_CELLS = 32;

export interface ChunkMesh {
  /** xyz per vertex, in Å. */
  positions: Float32Array;
  /** Unit normal per vertex, packed as normalized int16. */
  normals: Int16Array;
  /** Global index of the atom each vertex belongs to. */
  atoms: Uint32Array;
  indices: Uint32Array;
}

/**
 * Samples the union of the grid's atom spheres over one chunk of the lattice and turns
 * it into a shaded mesh.
 *
 * The lattice has one sample per grid unit, so `resolution` is applied by scaling the
 * atoms up before sampling and the vertices back down afterwards (x2 = 0.5 Å spacing).
 *
 * @param origin lattice coordinate of the chunk's first sample
 * @param size samples per axis (cells + 1)
 */
export function buildChunk(
  grid: AtomGrid,
  origin: [number, number, number],
  size: [number, number, number],
  resolution: number,
): ChunkMesh {
  const [ox, oy, oz] = origin;
  const [nx, ny, nz] = size;
  const values = new Float32Array(nx * ny * nz);
  // The field only matters close to the surface, so it's clamped at one cell outside.
  values.fill(1);

  // Every sphere that can pull a sample below 1 lies within maxRadius + 1 cell of the chunk.
  const reach = grid.maxRadius + 1 / resolution;
  let touched = 0;
  grid.forEachInBox(
    ox / resolution - reach, oy / resolution - reach, oz / resolution - reach,
    (ox + nx - 1) / resolution + reach, (oy + ny - 1) / resolution + reach, (oz + nz - 1) / resolution + reach,
    (i) => {
      touched++;
      // Distances are taken in global lattice coordinates, so samples shared by
      // neighbouring chunks get bit-identical values and the seams line up exactly.
      const cx = grid.x(i) * resolution;
      const cy = grid.y(i) * resolution;
      const cz = grid.z(i) * resolution;
      const r = grid.r(i) * resolution;
      const extent = r + 1;

      const x0 = Math.max(ox, Math.ceil(cx - extent)), x1 = Math.min(ox + nx - 1, Math.floor(cx + extent));
      const y0 = Math.max(oy, Math.ceil(cy - extent)), y1 = Math.min(oy + ny - 1, Math.floor(cy + extent));
      const z0 = Math.max(oz, Math.ceil(cz - extent)), z1 = Math.min(oz + nz - 1, Math.floor(cz + extent));

      for (let z = z0; z <= z1; z++) {
        const dz = z - cz;
        for (let y = y0; y <= y1; y++) {
          const dy = y - cy;
          const row = ((z - oz) * ny + (y - oy)) * nx - ox;
          for (let x = x0; x <= x1; x++) {
            const dx = x - cx;
            const d = Math.sqrt(dx * dx + dy * dy + dz * dz) - r;
            if (d < values[row + x]) values[row + x] = d;
          }
        }
      }
    },
  );

  if (touched === 0) return emptyChunk();

  const mesh = triangulate({ values, origin, size });
  const count = mesh.positions.length / 3;
  if (count === 0) return emptyChunk();

  const positions = mesh.positions;
  for (let k = 0; k < positions.length; k++) positions[k] /= resolution;

  // Smooth normals from the atom field, and the atom each vertex belongs to.
  const normals = new Float32Array(count * 3);
  const atoms = new Uint32Array(count);
  const normal = new Float32Array(3);
  let missing = 0;
  for (let v = 0; v < count; v++) {
    atoms[v] = grid.shade(positions[v * 3], positions[v * 3 + 1], positions[v * 3 + 2], normal);
    normals.set(normal, v * 3);
    if (normal[0] === 0 && normal[1] === 0 && normal[2] === 0) missing++;
  }
  if (missing > 0) fillFromFaces(positions, mesh.indices, normals);

  return { positions, normals: packNormals(normals), atoms, indices: mesh.indices };
}

/** Falls back to the area-weighted face normal anywhere the atom field gave nothing useful. */
function fillFromFaces(positions: Float32Array, indices: Uint32Array, normals: Float32Array): void {
  const missing = new Uint8Array(positions.length / 3);
  for (let v = 0; v < missing.length; v++)
    missing[v] = normals[v * 3] === 0 && normals[v * 3 + 1] === 0 && normals[v * 3 + 2] === 0 ? 1 : 0;

  for (let t = 0; t < indices.length; t += 3) {
    const i0 = indices[t], i1 = indices[t + 1], i2 = indices[t + 2];
    if (!missing[i0] && !missing[i1] && !missing[i2]) continue;
    const ax = positions[i1 * 3] - positions[i0 * 3], ay = positions[i1 * 3 + 1] - positions[i0 * 3 + 1], az = positions[i1 * 3 + 2] - positions[i0 * 3 + 2];
    const bx = positions[i2 * 3] - positions[i0 * 3], by = positions[i2 * 3 + 1] - positions[i0 * 3 + 1], bz = positions[i2 * 3 + 2] - positions[i0 * 3 + 2];
    const fx = ay * bz - az * by, fy = az * bx - ax * bz, fz = ax * by - ay * bx;
    for (const i of [i0, i1, i2]) {
      if (!missing[i]) continue;
      normals[i * 3] += fx;
      normals[i * 3 + 1] += fy;
      normals[i * 3 + 2] += fz;
    }
  }

  for (let v = 0; v < missing.length; v++) {
    if (!missing[v]) continue;
    const x = normals[v * 3], y = normals[v * 3 + 1], z = normals[v * 3 + 2];
    const length = Math.hypot(x, y, z) || 1;
    normals[v * 3] = x / length;
    normals[v * 3 + 1] = y / length;
    normals[v * 3 + 2] = z / length;
  }
}

function packNormals(normals: Float32Array): Int16Array {
  const packed = new Int16Array(normals.length);
  for (let k = 0; k < normals.length; k++) packed[k] = Math.round(Math.max(-1, Math.min(1, normals[k])) * 32767);
  return packed;
}

function emptyChunk(): ChunkMesh {
  return { positions: new Float32Array(0), normals: new Int16Array(0), atoms: new Uint32Array(0), indices: new Uint32Array(0) };
}
