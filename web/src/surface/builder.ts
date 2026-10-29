import type { Protein } from '../molecules/pdb';
import type { ChunkMesh } from './chunk';
import type { InitMessage, JobMessage, WorkerResponse } from './messages';
import { planChunks, planSurface, type ChunkJob } from './plan';

export interface ChainSurface {
  chainId: string;
  index: number;
  /** Global indices of the chain's atoms. */
  atoms: Uint32Array;
  positions: Float32Array;
  normals: Int16Array;
  /** Global index of the atom each vertex belongs to. */
  vertexAtoms: Uint32Array;
  indices: Uint32Array;
}

export interface ProteinSurface {
  protein: Protein;
  resolution: number;
  radiusScale: number;
  chains: ChainSurface[];
  /** Index into Protein.residues for each atom. */
  atomResidue: Int32Array;
  /** Index into chains for each atom, or -1 for atoms left out (waters). */
  atomChain: Int32Array;
  center: [number, number, number];
  radius: number;
  triangles: number;
  /** Wall-clock time to build, in ms. */
  buildTime: number;
  workers: number;
}

export interface BuildOptions {
  resolution: number;
  radiusScale: number;
  onProgress?: (fraction: number) => void;
  signal?: AbortSignal;
}

/**
 * Builds a per-chain molecular surface: each chain's lattice is split into chunks, and the
 * chunks are sampled, triangulated and shaded in parallel on a pool of Web Workers.
 */
export async function buildSurface(protein: Protein, options: BuildOptions): Promise<ProteinSurface> {
  const start = performance.now();
  const resolution = Math.min(4, Math.max(1, Math.round(options.resolution)));
  const plan = planSurface(protein, options.radiusScale);
  const jobs = planChunks(plan, resolution);

  const pieces: ChunkMesh[][] = plan.chains.map(() => []);
  let done = 0;
  await pool().run(
    {
      type: 'init',
      build: 0,
      positions: plan.positions,
      radii: plan.radii,
      chains: plan.chains.map((c) => c.atoms),
      resolution,
    },
    jobs,
    (chain, mesh) => {
      if (mesh.indices.length > 0) pieces[chain].push(mesh);
      options.onProgress?.(++done / jobs.length);
    },
    options.signal,
  );

  const chains = plan.chains.map((c) => ({ ...merge(pieces[c.index]), chainId: c.chainId, index: c.index, atoms: c.atoms }));
  return {
    protein,
    resolution,
    radiusScale: options.radiusScale,
    chains,
    atomResidue: plan.atomResidue,
    atomChain: plan.atomChain,
    center: plan.center,
    radius: plan.radius,
    triangles: chains.reduce((sum, c) => sum + c.indices.length / 3, 0),
    buildTime: performance.now() - start,
    workers: pool().size,
  };
}

/** Concatenates a chain's chunk meshes into one. */
function merge(chunks: ChunkMesh[]): Omit<ChainSurface, 'chainId' | 'index' | 'atoms'> {
  const vertices = chunks.reduce((sum, c) => sum + c.atoms.length, 0);
  const triangles = chunks.reduce((sum, c) => sum + c.indices.length, 0);
  const positions = new Float32Array(vertices * 3);
  const normals = new Int16Array(vertices * 3);
  const vertexAtoms = new Uint32Array(vertices);
  const indices = new Uint32Array(triangles);

  let v = 0, t = 0;
  for (const chunk of chunks) {
    positions.set(chunk.positions, v * 3);
    normals.set(chunk.normals, v * 3);
    vertexAtoms.set(chunk.atoms, v);
    for (let k = 0; k < chunk.indices.length; k++) indices[t + k] = chunk.indices[k] + v;
    v += chunk.atoms.length;
    t += chunk.indices.length;
  }

  return { positions, normals, vertexAtoms, indices };
}

interface Build {
  id: number;
  queue: ChunkJob[];
  nextJob: number;
  remaining: number;
  onChunk: (chain: number, mesh: ChunkMesh) => void;
  resolve: () => void;
  reject: (error: unknown) => void;
}

/**
 * Jobs queued per worker, so workers keep going while the main thread is busy rendering.
 * Workers skip queued jobs from a cancelled build, so a deep queue doesn't delay the next.
 */
const JOBS_IN_FLIGHT = 16;

/**
 * Fixed pool of surface workers. Each worker holds a few jobs at a time and gets another
 * as each finishes, so big and small chunks balance across cores. Starting a build cancels
 * the previous one; results that arrive late for a cancelled build are dropped.
 */
class WorkerPool {
  readonly size = Math.max(1, Math.min(8, navigator.hardwareConcurrency || 4));
  private readonly workers: { worker: Worker; pending: number }[] = [];
  private current: Build | null = null;
  private nextId = 1;

  constructor() {
    for (let i = 0; i < this.size; i++) {
      const worker = new Worker(new URL('./worker.ts', import.meta.url), { type: 'module', name: `surface-${i}` });
      const slot = { worker, pending: 0 };
      worker.onmessage = (event: MessageEvent<WorkerResponse>) => this.receive(slot, event.data);
      worker.onerror = (event) => this.fail(new Error(event.message || 'Surface worker failed to start.'));
      this.workers.push(slot);
    }
  }

  run(init: InitMessage, jobs: ChunkJob[], onChunk: Build['onChunk'], signal?: AbortSignal): Promise<void> {
    this.cancel();

    return new Promise<void>((resolve, reject) => {
      if (signal?.aborted) return reject(abortError());

      const build: Build = { id: this.nextId++, queue: jobs, nextJob: 0, remaining: jobs.length, onChunk, resolve, reject };
      this.current = build;
      signal?.addEventListener('abort', () => {
        if (this.current === build) this.cancel();
      });

      const message: InitMessage = { ...init, build: build.id };
      for (const { worker } of this.workers) worker.postMessage(message);

      if (jobs.length === 0) {
        this.current = null;
        resolve();
      } else {
        this.pump();
      }
    });
  }

  private cancel(): void {
    const build = this.current;
    this.current = null;
    build?.reject(abortError());
  }

  private fail(error: Error): void {
    const build = this.current;
    this.current = null;
    build?.reject(error);
  }

  private pump(): void {
    const build = this.current;
    if (!build) return;
    // Round-robin, so jobs spread across workers before any worker's queue fills.
    for (let depth = 0; depth < JOBS_IN_FLIGHT; depth++)
      for (const slot of this.workers) {
        if (slot.pending > depth || build.nextJob >= build.queue.length) continue;
        const id = build.nextJob++;
        const message: JobMessage = { type: 'job', build: build.id, id, job: build.queue[id] };
        slot.pending++;
        slot.worker.postMessage(message);
      }
  }

  private receive(slot: { pending: number }, response: WorkerResponse): void {
    slot.pending--;
    const build = this.current;

    if (build && response.build === build.id && response.type !== 'skipped') {
      if (response.type === 'error') {
        this.fail(new Error(response.message));
        return;
      }
      build.onChunk(response.chain, response.mesh);
      if (--build.remaining === 0) {
        this.current = null;
        build.resolve();
        return;
      }
    }

    this.pump();
  }
}

let shared: WorkerPool | null = null;
const pool = () => (shared ??= new WorkerPool());

function abortError(): DOMException {
  return new DOMException('Surface build cancelled.', 'AbortError');
}
