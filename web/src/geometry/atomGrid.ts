/** How quickly an atom's influence on vertex normals falls off with distance from its surface (per Å). */
export const NORMAL_FALLOFF = 3;
/** Atoms further than this (Å) outside their own surface don't contribute to a vertex normal. */
export const INFLUENCE_RANGE = 2.5;

/**
 * Uniform spatial hash over a set of atom spheres (in Å), stored as a dense grid in
 * compressed-row form. Used to find the atoms that touch a chunk of the SDF, and to
 * find which atom a surface vertex belongs to and its smooth normal.
 */
export class AtomGrid {
  /** Global atom index for each local atom. */
  readonly atoms: Uint32Array;
  readonly maxRadius: number;
  readonly cellSize: number;
  private readonly range: number;

  private readonly px: Float32Array;
  private readonly py: Float32Array;
  private readonly pz: Float32Array;
  private readonly radius: Float32Array;
  private readonly min = [0, 0, 0];
  private readonly dims = [1, 1, 1];
  private readonly cellStart: Uint32Array;
  private readonly cellAtoms: Uint32Array;

  /**
   * @param positions xyz for every atom in the protein
   * @param radii radius for every atom in the protein
   * @param atoms global indices of the atoms to include
   */
  constructor(positions: Float32Array, radii: Float32Array, atoms: Uint32Array, range = INFLUENCE_RANGE) {
    const n = atoms.length;
    this.atoms = atoms;
    this.range = range;
    this.px = new Float32Array(n);
    this.py = new Float32Array(n);
    this.pz = new Float32Array(n);
    this.radius = new Float32Array(n);

    let maxRadius = 0;
    const lo = [Infinity, Infinity, Infinity];
    const hi = [-Infinity, -Infinity, -Infinity];
    for (let i = 0; i < n; i++) {
      const a = atoms[i];
      const p = [positions[a * 3], positions[a * 3 + 1], positions[a * 3 + 2]];
      this.px[i] = p[0];
      this.py[i] = p[1];
      this.pz[i] = p[2];
      this.radius[i] = radii[a];
      maxRadius = Math.max(maxRadius, radii[a]);
      for (let k = 0; k < 3; k++) {
        lo[k] = Math.min(lo[k], p[k]);
        hi[k] = Math.max(hi[k], p[k]);
      }
    }
    this.maxRadius = maxRadius;
    // A cell this size means a shading query only has to look at its 27 neighbours.
    this.cellSize = Math.max(0.5, maxRadius + range);

    if (n > 0)
      for (let k = 0; k < 3; k++) {
        this.min[k] = lo[k];
        this.dims[k] = this.axisCell(hi[k], k) + 1;
      }

    const cellOf = new Uint32Array(n);
    for (let i = 0; i < n; i++)
      cellOf[i] = this.cellIndex(this.axisCell(this.px[i], 0), this.axisCell(this.py[i], 1), this.axisCell(this.pz[i], 2));

    // Counting sort of atoms into cells.
    const cells = this.dims[0] * this.dims[1] * this.dims[2];
    this.cellStart = new Uint32Array(cells + 1);
    for (let i = 0; i < n; i++) this.cellStart[cellOf[i] + 1]++;
    for (let c = 0; c < cells; c++) this.cellStart[c + 1] += this.cellStart[c];
    const fill = this.cellStart.slice(0, cells);
    this.cellAtoms = new Uint32Array(n);
    for (let i = 0; i < n; i++) this.cellAtoms[fill[cellOf[i]]++] = i;
  }

  get count(): number {
    return this.atoms.length;
  }

  x(i: number): number { return this.px[i]; }
  y(i: number): number { return this.py[i]; }
  z(i: number): number { return this.pz[i]; }
  r(i: number): number { return this.radius[i]; }

  private axisCell(value: number, axis: number): number {
    return Math.floor((value - this.min[axis]) / this.cellSize);
  }

  private cellIndex(x: number, y: number, z: number): number {
    return x + this.dims[0] * (y + this.dims[1] * z);
  }

  /** Calls `visit` with the local index of every atom whose centre lies in the box. */
  forEachInBox(
    minX: number, minY: number, minZ: number,
    maxX: number, maxY: number, maxZ: number,
    visit: (i: number) => void,
  ): void {
    const [dx, dy, dz] = this.dims;
    const x0 = Math.max(0, this.axisCell(minX, 0)), x1 = Math.min(dx - 1, this.axisCell(maxX, 0));
    const y0 = Math.max(0, this.axisCell(minY, 1)), y1 = Math.min(dy - 1, this.axisCell(maxY, 1));
    const z0 = Math.max(0, this.axisCell(minZ, 2)), z1 = Math.min(dz - 1, this.axisCell(maxZ, 2));

    for (let z = z0; z <= z1; z++)
      for (let y = y0; y <= y1; y++)
        for (let x = x0; x <= x1; x++) {
          const c = this.cellIndex(x, y, z);
          for (let k = this.cellStart[c]; k < this.cellStart[c + 1]; k++) {
            const i = this.cellAtoms[k];
            const ax = this.px[i], ay = this.py[i], az = this.pz[i];
            if (ax >= minX && ax <= maxX && ay >= minY && ay <= maxY && az >= minZ && az <= maxZ) visit(i);
          }
        }
  }

  /**
   * Normal: weighted sum of each nearby sphere's gradient, so shading blends smoothly
   * across the creases where spheres meet. Written to `normal`, or zero if no sphere is
   * in range.
   *
   * Returns the global index of the atom whose surface is closest to the point.
   */
  shade(px: number, py: number, pz: number, normal: Float32Array): number {
    const cx = this.axisCell(px, 0), cy = this.axisCell(py, 1), cz = this.axisCell(pz, 2);
    const [dx, dy, dz] = this.dims;
    let sx = 0, sy = 0, sz = 0;
    let best = Infinity;
    let atom = this.atoms.length > 0 ? this.atoms[0] : 0;

    for (let z = Math.max(0, cz - 1); z <= Math.min(dz - 1, cz + 1); z++)
      for (let y = Math.max(0, cy - 1); y <= Math.min(dy - 1, cy + 1); y++)
        for (let x = Math.max(0, cx - 1); x <= Math.min(dx - 1, cx + 1); x++) {
          const c = this.cellIndex(x, y, z);
          for (let k = this.cellStart[c]; k < this.cellStart[c + 1]; k++) {
            const i = this.cellAtoms[k];
            const ox = px - this.px[i], oy = py - this.py[i], oz = pz - this.pz[i];
            const distance = Math.sqrt(ox * ox + oy * oy + oz * oz);
            const surface = distance - this.radius[i];

            if (surface < best) {
              best = surface;
              atom = this.atoms[i];
            }

            if (surface > this.range || distance < 1e-5) continue;
            const w = Math.exp(-NORMAL_FALLOFF * surface) / distance;
            sx += ox * w;
            sy += oy * w;
            sz += oz * w;
          }
        }

    const length = Math.sqrt(sx * sx + sy * sy + sz * sz);
    if (length > 1e-6) {
      normal[0] = sx / length;
      normal[1] = sy / length;
      normal[2] = sz / length;
    } else {
      normal[0] = normal[1] = normal[2] = 0;
    }
    return atom;
  }
}
