import './styles.css';
import { parsePDB, type Protein } from './molecules/pdb';
import { COLOR_SCHEMES, legend, rainbowGradient, type ColorScheme } from './rendering/colorSchemes';
import { Viewer } from './rendering/viewer';
import { buildSurface } from './surface/builder';

const EXAMPLES = [
  { id: '1UBQ', name: 'Ubiquitin' },
  { id: '1CRN', name: 'Crambin' },
  { id: '4HHB', name: 'Hemoglobin' },
  { id: '2PTC', name: 'Trypsin with its inhibitor BPTI' },
  { id: '1AON', name: 'GroEL–GroES chaperonin (21 chains)' },
];
const DEFAULT_ID = '1UBQ';
const PDB_ID = /^[0-9][A-Za-z0-9]{3}$/;
const RCSB = 'https://files.rcsb.org/download/';

const $ = <T extends HTMLElement>(id: string) => document.getElementById(id) as T;
const ui = {
  panel: $('panel'),
  collapse: $<HTMLButtonElement>('collapse'),
  form: $<HTMLFormElement>('load-form'),
  input: $<HTMLInputElement>('pdb-id'),
  file: $<HTMLInputElement>('file-input'),
  examples: $('examples'),
  status: $('status'),
  title: $('title'),
  progress: $('progress'),
  progressFill: $('progress').firstElementChild as HTMLElement,
  schemes: $('schemes'),
  resolution: $('resolution'),
  ao: $<HTMLInputElement>('ao'),
  outlines: $<HTMLInputElement>('outlines'),
  rotate: $<HTMLInputElement>('rotate'),
  legendTitle: $('legend-title'),
  legend: $('legend'),
  tooltip: $('tooltip'),
  dropHint: $('drop-hint'),
};

const viewer = new Viewer($('viewport'));
// ?debug=ao or ?debug=ids shows the ambient-occlusion or chain-ID buffer.
const debug = new URLSearchParams(location.search).get('debug');
if (debug === 'ao' || debug === 'ids') viewer.setDebugView(debug);
// In development, expose the viewer to the browser console for poking at the scene.
if (import.meta.env.DEV) Object.assign(window, { viewer });

const state = {
  protein: null as Protein | null,
  scheme: 'chain' as ColorScheme,
  resolution: 1,
  radiusScale: 1,
  task: null as AbortController | null,
};

// #region Loading

/** Cancels any load or build in flight and starts a new one. */
function restart(): AbortSignal {
  state.task?.abort();
  state.task = new AbortController();
  return state.task.signal;
}

async function load(rawId: string): Promise<void> {
  const id = rawId.trim().toUpperCase();
  if (!PDB_ID.test(id)) {
    setStatus('PDB IDs are 4 characters, starting with a digit (e.g. 1UBQ).', { error: true });
    return;
  }

  const signal = restart();
  ui.input.value = id;
  markExample(id);
  setStatus(`Downloading ${id}…`);
  setProgress('indeterminate');

  let text: string;
  try {
    const response = await fetch(`${RCSB}${id}.pdb`, { signal });
    if (!response.ok)
      throw new Error(response.status === 404
        ? `Couldn't find ${id} as a .pdb file. Check the ID exists on rcsb.org. (Very large entries have no .pdb file, only mmCIF.)`
        : `Couldn't download ${id} (HTTP ${response.status}).`);
    text = await response.text();
  } catch (error) {
    if (signal.aborted) return;
    fail(error instanceof TypeError ? `Couldn't download ${id}. Check that you're online.` : (error as Error).message);
    return;
  }

  history.replaceState(null, '', `#${id}`);
  await show(parsePDB(id, text), signal);
}

async function loadFile(file: File): Promise<void> {
  const signal = restart();
  const name = file.name.replace(/\.(pdb|ent|txt)$/i, '');
  setStatus(`Reading ${file.name}…`);
  setProgress('indeterminate');
  markExample('');
  const text = await file.text();
  if (signal.aborted) return;

  const protein = parsePDB(name, text);
  if (protein.atoms.length === 0) return fail(`${file.name} has no ATOM or HETATM records.`);
  // The URL names RCSB entries only, so a local file clears it.
  history.replaceState(null, '', location.pathname + location.search);
  await show(protein, signal);
}

async function show(protein: Protein, signal: AbortSignal): Promise<void> {
  if (protein.atoms.length === 0) return fail(`${protein.id} has no atoms.`);
  state.protein = protein;
  ui.title.textContent = protein.title;
  await build(signal, true);
}

async function rebuild(): Promise<void> {
  if (state.protein) await build(restart(), false);
}

async function build(signal: AbortSignal, frame: boolean): Promise<void> {
  const protein = state.protein!;
  setStatus(`Building surface for ${protein.id} (${protein.atoms.length.toLocaleString()} atoms)…`);
  setProgress(0);

  try {
    const surface = await buildSurface(protein, {
      resolution: state.resolution,
      radiusScale: state.radiusScale,
      onProgress: (p) => setProgress(p),
      signal,
    });
    if (signal.aborted) return;

    viewer.show(surface, frame);
    viewer.setScheme(state.scheme);
    renderLegend();
    setProgress(null);

    const chains = surface.chains.length;
    setStatus(
      `${protein.id}: ${chains} chain${chains === 1 ? '' : 's'}, ${protein.residues.length.toLocaleString()} residues, ${protein.atoms.length.toLocaleString()} atoms`,
      { detail: `${Math.round(surface.triangles).toLocaleString()} triangles in ${(surface.buildTime / 1000).toFixed(2)} s on ${surface.workers} worker${surface.workers === 1 ? '' : 's'}` },
    );
  } catch (error) {
    if (signal.aborted || (error as Error).name === 'AbortError') return;
    console.error(error);
    fail(`Failed to build the surface: ${(error as Error).message}`);
  }
}

function fail(message: string): void {
  setStatus(message, { error: true });
  setProgress(null);
}

// #endregion

// #region Panel

function setStatus(text: string, options: { error?: boolean; detail?: string } = {}): void {
  ui.status.textContent = text;
  ui.status.classList.toggle('error', !!options.error);
  if (options.detail) {
    const detail = document.createElement('span');
    detail.className = 'detail';
    detail.textContent = options.detail;
    ui.status.append(detail);
  }
}

function setProgress(value: number | 'indeterminate' | null): void {
  ui.progress.hidden = value === null;
  ui.progress.classList.toggle('indeterminate', value === 'indeterminate');
  if (typeof value === 'number') ui.progressFill.style.width = `${Math.round(value * 100)}%`;
  else ui.progressFill.style.width = '';
}

function markExample(id: string): void {
  for (const button of ui.examples.querySelectorAll('button')) button.classList.toggle('active', button.dataset.id === id);
}

/** Builds a radio-style segmented control. */
function segmented<T>(container: HTMLElement, options: { value: T; label: string }[], current: T, onChange: (value: T) => void): void {
  const buttons = options.map(({ value, label }) => {
    const button = document.createElement('button');
    button.type = 'button';
    button.textContent = label;
    button.setAttribute('role', 'radio');
    button.addEventListener('click', () => {
      buttons.forEach((b, i) => b.setAttribute('aria-checked', String(options[i].value === value)));
      onChange(value);
    });
    button.setAttribute('aria-checked', String(value === current));
    return button;
  });
  container.replaceChildren(...buttons);
}

function renderLegend(): void {
  const surface = viewer.surface.surface;
  if (!surface) return;

  ui.legendTitle.textContent = state.scheme === 'chain' ? 'Chains (click to show/hide)' : 'Legend';

  if (state.scheme === 'rainbow') {
    const bar = document.createElement('div');
    bar.className = 'rainbow-bar';
    bar.style.background = rainbowGradient();
    const labels = document.createElement('div');
    labels.className = 'rainbow-labels';
    labels.innerHTML = '<span>N-terminus</span><span>C-terminus</span>';
    ui.legend.replaceChildren(bar, labels);
    return;
  }

  ui.legend.replaceChildren(
    ...legend(surface, state.scheme).map((entry) => {
      const row = document.createElement(entry.chain >= 0 ? 'label' : 'div');
      row.className = 'legend-entry';
      const swatch = document.createElement('span');
      swatch.className = 'swatch';
      swatch.style.background = entry.color;
      const label = document.createElement('span');
      label.textContent = entry.label;

      if (entry.chain >= 0) {
        const checkbox = document.createElement('input');
        checkbox.type = 'checkbox';
        checkbox.checked = viewer.surface.isChainVisible(entry.chain);
        row.classList.toggle('hidden-chain', !checkbox.checked);
        checkbox.addEventListener('change', () => {
          viewer.setChainVisible(entry.chain, checkbox.checked);
          row.classList.toggle('hidden-chain', !checkbox.checked);
        });
        row.append(checkbox);
      }
      row.append(swatch, label);
      return row;
    }),
  );
}

function hoverLabel(atom: number): string {
  const surface = viewer.surface.surface!;
  const residue = surface.protein.residues[surface.atomResidue[atom]];
  // Names come from the file, which may be a local one, so escape them.
  const name = escapeHtml(residue.name || residue.category);
  const insert = escapeHtml(residue.insertCode.trim());
  const chain = escapeHtml(residue.chainId.trim() || '(blank)');
  const dot = '<span class="muted">·</span>';
  return `<strong>${name} ${residue.number}${insert}</strong> ${dot} Chain ${chain} ${dot} ${escapeHtml(surface.protein.atoms[atom].name)}`;
}

function escapeHtml(text: string): string {
  return text.replace(/[&<>"']/g, (c) => `&#${c.charCodeAt(0)};`);
}

viewer.onHover = (atom, x, y) => {
  if (atom < 0) {
    ui.tooltip.hidden = true;
    return;
  }
  ui.tooltip.innerHTML = hoverLabel(atom);
  ui.tooltip.hidden = false;
  const { width, height } = ui.tooltip.getBoundingClientRect();
  ui.tooltip.style.left = `${x + 16 + width > window.innerWidth ? x - width - 8 : x + 16}px`;
  ui.tooltip.style.top = `${y + 12 + height > window.innerHeight ? y - height - 8 : y + 12}px`;
};

ui.examples.replaceChildren(
  ...EXAMPLES.map(({ id, name }) => {
    const button = document.createElement('button');
    button.type = 'button';
    button.textContent = id;
    button.title = name;
    button.dataset.id = id;
    button.addEventListener('click', () => load(id));
    return button;
  }),
);

segmented(ui.schemes, COLOR_SCHEMES.map((s) => ({ value: s.id, label: s.label })), state.scheme, (scheme) => {
  state.scheme = scheme;
  viewer.setScheme(scheme);
  renderLegend();
});

segmented(ui.resolution, [{ value: 1, label: 'x1' }, { value: 2, label: 'x2' }], state.resolution, (resolution) => {
  if (resolution === state.resolution) return;
  state.resolution = resolution;
  rebuild();
});

ui.form.addEventListener('submit', (e) => {
  e.preventDefault();
  ui.input.blur();
  load(ui.input.value);
});
ui.input.addEventListener('input', () => (ui.input.value = ui.input.value.toUpperCase()));
ui.file.addEventListener('change', () => {
  const file = ui.file.files?.[0];
  if (file) loadFile(file);
  ui.file.value = '';
});

ui.ao.addEventListener('change', () => viewer.setEffects({ ambientOcclusion: ui.ao.checked }));
ui.outlines.addEventListener('change', () => viewer.setEffects({ outlines: ui.outlines.checked }));
ui.rotate.addEventListener('change', () => (viewer.orbit.autoRotate = ui.rotate.checked));
// Dragging to rotate turns auto-rotate off, so keep the checkbox in step.
viewer.renderer.domElement.addEventListener('pointerup', () => (ui.rotate.checked = viewer.orbit.autoRotate));

const narrow = window.matchMedia('(max-width: 640px)');

/** Centres the model in the space beside the panel on wide screens. */
function updateScreenOffset(): void {
  const beside = !narrow.matches && !ui.panel.classList.contains('collapsed');
  viewer.orbit.screenOffset = beside ? ui.panel.getBoundingClientRect().right / 2 : 0;
  viewer.invalidate();
}

ui.collapse.addEventListener('click', () => {
  const collapsed = ui.panel.classList.toggle('collapsed');
  ui.collapse.setAttribute('aria-expanded', String(!collapsed));
  ui.collapse.title = collapsed ? 'Show panel' : 'Hide panel';
  updateScreenOffset();
});
if (narrow.matches) ui.collapse.click();
window.addEventListener('resize', updateScreenOffset);
updateScreenOffset();

// Drag and drop a local file anywhere on the page.
let dragDepth = 0;
window.addEventListener('dragenter', (e) => {
  if (!e.dataTransfer?.types.includes('Files')) return;
  dragDepth++;
  ui.dropHint.hidden = false;
});
window.addEventListener('dragleave', () => {
  if (--dragDepth <= 0) {
    dragDepth = 0;
    ui.dropHint.hidden = true;
  }
});
window.addEventListener('dragover', (e) => e.preventDefault());
window.addEventListener('drop', (e) => {
  e.preventDefault();
  dragDepth = 0;
  ui.dropHint.hidden = true;
  const file = e.dataTransfer?.files[0];
  if (file) loadFile(file);
});

// #endregion

// Deep links: viewer.html#4HHB opens that entry.
const idFromHash = () => decodeURIComponent(location.hash.slice(1)).trim();
window.addEventListener('hashchange', () => {
  const id = idFromHash();
  if (PDB_ID.test(id) && id.toUpperCase() !== state.protein?.id) load(id);
});
load(PDB_ID.test(idFromHash()) ? idFromHash() : DEFAULT_ID);
