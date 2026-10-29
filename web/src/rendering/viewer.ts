import { PCFShadowMap, PerspectiveCamera, Raycaster, Scene, Vector2, Vector3, WebGLRenderer } from 'three';
import type { ProteinSurface } from '../surface/builder';
import type { ColorScheme } from './colorSchemes';
import { SurfaceEffects, type DebugView, type EffectSettings } from './effects';
import { LightRig } from './lightRig';
import { OrbitCamera } from './orbitCamera';
import { SurfaceView } from './surfaceView';

/**
 * The 3D view: renderer, camera, lights, surface meshes and screen-space effects.
 * Renders on demand: continuously while the camera is moving, otherwise only when
 * something changes.
 */
export class Viewer {
  readonly renderer: WebGLRenderer;
  readonly camera = new PerspectiveCamera(60, 1, 0.1, 100);
  readonly orbit: OrbitCamera;
  readonly surface = new SurfaceView();

  /** Called with the hovered atom index, or -1. */
  onHover: (atom: number, clientX: number, clientY: number) => void = () => {};

  private readonly scene = new Scene();
  private readonly rig = new LightRig();
  private readonly effects: SurfaceEffects;
  private readonly raycaster = new Raycaster();
  private dirty = true;
  private lastTime = performance.now();
  private pointer: { x: number; y: number } | null = null;
  private hoverDirty = false;
  private hoverAtom = -1;

  constructor(private readonly container: HTMLElement) {
    this.renderer = new WebGLRenderer({ antialias: false, powerPreference: 'high-performance' });
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.renderer.shadowMap.enabled = true;
    this.renderer.shadowMap.type = PCFShadowMap;
    // The ID pass doesn't need shadows, so the shadow map is only redrawn before the main pass.
    this.renderer.shadowMap.autoUpdate = false;
    container.appendChild(this.renderer.domElement);

    this.effects = new SurfaceEffects(this.renderer);
    this.scene.add(this.surface.root);
    this.rig.addTo(this.scene);

    const canvas = this.renderer.domElement;
    this.orbit = new OrbitCamera(this.camera, canvas);
    this.orbit.onInteract = () => this.invalidate();

    canvas.addEventListener('pointermove', (e) => {
      this.pointer = { x: e.clientX, y: e.clientY };
      this.hoverDirty = true;
    });
    canvas.addEventListener('pointerleave', () => {
      this.pointer = null;
      this.hoverDirty = true;
    });

    new ResizeObserver(() => this.resize()).observe(container);
    this.resize();
    this.renderer.setAnimationLoop(() => this.tick());
  }

  show(surface: ProteinSurface, frame: boolean): void {
    this.surface.show(surface);
    this.rig.setModelRadius(surface.radius + 4 * surface.radiusScale);
    // The furthest atom is usually an outlier on a loop, so frame slightly inside it.
    if (frame) this.orbit.frame(new Vector3(), surface.radius * 0.85 + 2 * surface.radiusScale);
    this.setHover(-1);
    this.invalidate();
  }

  setScheme(scheme: ColorScheme): void {
    this.surface.setScheme(scheme);
    this.invalidate();
  }

  setChainVisible(chain: number, visible: boolean): void {
    this.surface.setChainVisible(chain, visible);
    this.hoverDirty = true;
    this.invalidate();
  }

  setEffects(settings: Partial<EffectSettings>): void {
    Object.assign(this.effects.settings, settings);
    this.invalidate();
  }

  /** Shows an intermediate buffer instead of the final image. */
  setDebugView(view: DebugView): void {
    this.effects.debugView = view;
    this.invalidate();
  }

  invalidate(): void {
    this.dirty = true;
  }

  private resize(): void {
    const width = this.container.clientWidth, height = this.container.clientHeight;
    this.renderer.setSize(width, height);
    this.effects.setSize(width, height, this.renderer.getPixelRatio());
    this.camera.aspect = width / Math.max(1, height);
    this.camera.updateProjectionMatrix();
    this.invalidate();
  }

  private tick(): void {
    const now = performance.now();
    const dt = Math.min(0.1, (now - this.lastTime) / 1000);
    this.lastTime = now;

    // With nothing loaded yet there's nothing to see move, so leave the main thread to
    // the surface build.
    const moved = this.orbit.update(dt) && this.surface.surface !== null;
    // The model may be turning under a still mouse, so re-pick whenever the camera moves.
    if (moved || this.hoverDirty) this.updateHover();
    if (!moved && !this.dirty) return;

    this.dirty = false;
    this.rig.update(this.camera);
    this.effects.render(
      this.camera,
      () => {
        this.surface.useIdMaterials(true);
        this.renderer.render(this.scene, this.camera);
        this.surface.useIdMaterials(false);
      },
      () => {
        this.renderer.shadowMap.needsUpdate = true;
        this.renderer.render(this.scene, this.camera);
      },
    );
  }

  private updateHover(): void {
    this.hoverDirty = false;
    if (!this.pointer || this.orbit.dragging || !this.surface.surface) return this.setHover(-1);

    const rect = this.renderer.domElement.getBoundingClientRect();
    const ndc = new Vector2(
      ((this.pointer.x - rect.left) / rect.width) * 2 - 1,
      -((this.pointer.y - rect.top) / rect.height) * 2 + 1,
    );
    this.raycaster.setFromCamera(ndc, this.camera);
    this.surface.root.updateMatrixWorld();
    this.setHover(this.surface.pick(this.raycaster.ray));
  }

  private setHover(atom: number): void {
    const surface = this.surface.surface;
    const residue = atom >= 0 && surface ? surface.atomResidue[atom] : -1;
    if (residue !== this.surface.highlightedResidue) {
      this.surface.setHighlight(residue);
      this.invalidate();
    }
    if (atom !== this.hoverAtom || atom >= 0) {
      this.hoverAtom = atom;
      this.onHover(atom, this.pointer?.x ?? 0, this.pointer?.y ?? 0);
    }
  }
}
