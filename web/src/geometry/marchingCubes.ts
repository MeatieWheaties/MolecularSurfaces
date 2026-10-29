import { EDGE_TABLE, TRI_TABLE } from './tables';

/** Iso level of the surface; samples below it are inside. */
export const ISO_LEVEL = 0;

/**
 * A scalar field sampled on an integer grid, x fastest, then y, then z.
 * `origin` is the grid coordinate of the first sample.
 */
export interface ScalarField {
  values: Float32Array;
  origin: [number, number, number];
  size: [number, number, number];
}

export interface Triangulation {
  /** xyz per vertex, in grid units. */
  positions: Float32Array;
  indices: Uint32Array;
}

// Each table edge as (corner offset x, y, z, axis), with the offset at the edge's lower end,
// so neighbouring cells look the same edge up under the same key.
// Corners: A=(0,0,0) B=(0,0,1) C=(1,0,1) D=(1,0,0) E=(0,1,0) F=(0,1,1) G=(1,1,1) H=(1,1,0)
const X = 0, Y = 1, Z = 2;
const EDGES: ReadonlyArray<readonly [number, number, number, number]> = [
  [0, 0, 0, Z], // 0  A-B
  [0, 0, 1, X], // 1  B-C
  [1, 0, 0, Z], // 2  C-D
  [0, 0, 0, X], // 3  D-A
  [0, 1, 0, Z], // 4  E-F
  [0, 1, 1, X], // 5  F-G
  [1, 1, 0, Z], // 6  G-H
  [0, 1, 0, X], // 7  H-E
  [0, 0, 0, Y], // 8  A-E
  [0, 0, 1, Y], // 9  B-F
  [1, 0, 1, Y], // 10 C-G
  [1, 0, 0, Y], // 11 D-H
];

/**
 * Marching cubes over a sampled field. Vertices on cell edges are shared between the
 * cells that meet there, so the result is an indexed mesh rather than a triangle soup.
 * Triangles wind counter-clockwise when seen from outside (lower values inside).
 */
export function triangulate(field: ScalarField): Triangulation {
  const { values } = field;
  const [nx, ny, nz] = field.size;
  const [ox, oy, oz] = field.origin;
  const ys = nx;
  const zs = nx * ny;
  const strides = [1, ys, zs];

  const positions = new GrowableFloat32();
  const indices = new GrowableUint32();
  // Vertex index for each (sample, axis) edge, or -1 if not created yet.
  const edgeVertex = new Int32Array(values.length * 3).fill(-1);
  const cellEdges = new Uint32Array(12);

  for (let z = 0; z < nz - 1; z++)
    for (let y = 0; y < ny - 1; y++)
      for (let x = 0; x < nx - 1; x++) {
        const a = x + y * ys + z * zs;
        const cube =
          (values[a] < ISO_LEVEL ? 1 : 0) |
          (values[a + zs] < ISO_LEVEL ? 2 : 0) |
          (values[a + zs + 1] < ISO_LEVEL ? 4 : 0) |
          (values[a + 1] < ISO_LEVEL ? 8 : 0) |
          (values[a + ys] < ISO_LEVEL ? 16 : 0) |
          (values[a + ys + zs] < ISO_LEVEL ? 32 : 0) |
          (values[a + ys + zs + 1] < ISO_LEVEL ? 64 : 0) |
          (values[a + ys + 1] < ISO_LEVEL ? 128 : 0);

        const crossed = EDGE_TABLE[cube];
        if (crossed === 0) continue;

        for (let e = 0; e < 12; e++) {
          if ((crossed & (1 << e)) === 0) continue;
          const [ex, ey, ez, axis] = EDGES[e];
          const p = a + ex + ey * ys + ez * zs;
          const key = p * 3 + axis;

          let v = edgeVertex[key];
          if (v < 0) {
            const v1 = values[p];
            const v2 = values[p + strides[axis]];
            const denom = v2 - v1;
            const t = Math.abs(denom) < 1e-6 ? 0.5 : (ISO_LEVEL - v1) / denom;
            v = positions.length / 3;
            positions.push3(
              ox + x + ex + (axis === X ? t : 0),
              oy + y + ey + (axis === Y ? t : 0),
              oz + z + ez + (axis === Z ? t : 0),
            );
            edgeVertex[key] = v;
          }
          cellEdges[e] = v;
        }

        // The table winds triangles clockwise from outside, so swap the last two.
        for (let t = cube * 16; TRI_TABLE[t] !== -1; t += 3)
          indices.push3(cellEdges[TRI_TABLE[t]], cellEdges[TRI_TABLE[t + 2]], cellEdges[TRI_TABLE[t + 1]]);
      }

  return { positions: positions.toArray(), indices: indices.toArray() };
}

class GrowableFloat32 {
  private data = new Float32Array(3 * 1024);
  length = 0;

  push3(a: number, b: number, c: number): void {
    if (this.length + 3 > this.data.length) {
      const next = new Float32Array(this.data.length * 2);
      next.set(this.data);
      this.data = next;
    }
    this.data[this.length++] = a;
    this.data[this.length++] = b;
    this.data[this.length++] = c;
  }

  toArray(): Float32Array {
    return this.data.slice(0, this.length);
  }
}

class GrowableUint32 {
  private data = new Uint32Array(3 * 2048);
  length = 0;

  push3(a: number, b: number, c: number): void {
    if (this.length + 3 > this.data.length) {
      const next = new Uint32Array(this.data.length * 2);
      next.set(this.data);
      this.data = next;
    }
    this.data[this.length++] = a;
    this.data[this.length++] = b;
    this.data[this.length++] = c;
  }

  toArray(): Uint32Array {
    return this.data.slice(0, this.length);
  }
}
