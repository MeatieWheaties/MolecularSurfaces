/// <reference lib="webworker" />
import { AtomGrid } from '../geometry/atomGrid';
import { buildChunk } from './chunk';
import type { InitMessage, JobMessage, WorkerRequest, WorkerResponse } from './messages';

let current: InitMessage | null = null;
// Spatial hashes are built lazily, once per chain per build.
const grids = new Map<number, AtomGrid>();
// Jobs are queued and run one per task, so a newer build's init message gets through
// between jobs and the rest of a cancelled build's jobs are skipped instead of built.
const queue: JobMessage[] = [];
let scheduled = false;
// A message to ourselves runs the next job as a new task, without setTimeout's nesting clamp.
const tick = new MessageChannel();
tick.port1.onmessage = runNext;
const schedule = () => {
  scheduled = true;
  tick.port2.postMessage(null);
};

const scope = self as unknown as DedicatedWorkerGlobalScope;

scope.onmessage = (event: MessageEvent<WorkerRequest>) => {
  const message = event.data;

  if (message.type === 'init') {
    current = message;
    grids.clear();
  } else {
    queue.push(message);
  }

  if (!scheduled && queue.length > 0) schedule();
};

function runNext(): void {
  scheduled = false;
  const message = queue.shift();
  if (message) run(message);
  if (queue.length > 0) schedule();
}

function run({ build, id, job }: JobMessage): void {
  let response: WorkerResponse;
  try {
    if (!current || current.build !== build) {
      response = { type: 'skipped', build, id };
    } else {
      let grid = grids.get(job.chain);
      if (!grid) grids.set(job.chain, (grid = new AtomGrid(current.positions, current.radii, current.chains[job.chain])));

      const mesh = buildChunk(grid, job.origin, job.size, current.resolution);
      response = { type: 'chunk', build, id, chain: job.chain, mesh };
      scope.postMessage(response, [mesh.positions.buffer, mesh.normals.buffer, mesh.atoms.buffer, mesh.indices.buffer]);
      return;
    }
  } catch (error) {
    response = { type: 'error', build, id, message: error instanceof Error ? error.message : String(error) };
  }
  scope.postMessage(response);
}
