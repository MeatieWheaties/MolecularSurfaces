import type { ChunkMesh } from './chunk';
import type { ChunkJob } from './plan';

/** Sent to every worker at the start of a build; later chunk jobs refer to it. */
export interface InitMessage {
  type: 'init';
  build: number;
  positions: Float32Array;
  radii: Float32Array;
  /** Atom indices of each chain. */
  chains: Uint32Array[];
  resolution: number;
}

export interface JobMessage {
  type: 'job';
  build: number;
  id: number;
  job: ChunkJob;
}

export type WorkerRequest = InitMessage | JobMessage;

export type WorkerResponse =
  | { type: 'chunk'; build: number; id: number; chain: number; mesh: ChunkMesh }
  /** The job's build was superseded before the job ran. */
  | { type: 'skipped'; build: number; id: number }
  | { type: 'error'; build: number; id: number; message: string };
