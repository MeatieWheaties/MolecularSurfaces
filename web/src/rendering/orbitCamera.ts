import { MathUtils, PerspectiveCamera, Vector3 } from 'three';

const ROTATE_SPEED = 0.4; // degrees per pixel
const ZOOM_SPEED = 0.0015; // per wheel pixel
const SMOOTHING = 10; // higher is snappier
const AUTO_ROTATE_SPEED = 8; // degrees per second
// Extra distance when framing, so parts that stick out past the framed radius aren't cut off.
const FRAME_MARGIN = 1.2;

/**
 * Orbit camera: left-drag (or one finger) rotates, right/middle-drag (or two fingers)
 * pans, wheel (or pinch) zooms, F reframes. Input moves a goal pose and the camera
 * eases towards it.
 */
export class OrbitCamera {
  autoRotate = true;
  /**
   * Shifts the model this many CSS pixels right of centre, e.g. to centre it in the
   * space beside a side panel. The camera slides sideways rather than using an off-axis
   * projection, so screen-space effects can assume a symmetric frustum.
   */
  screenOffset = 0;
  minDistance = 1;
  maxDistance = 2000;
  /** Called whenever the user starts interacting. */
  onInteract: () => void = () => {};

  private target = new Vector3();
  private distance = 10;
  private yaw = 30;
  private pitch = 20;
  private offset = 0;

  // Goal pose; the visible pose eases towards it.
  private goalTarget = new Vector3();
  private goalDistance = 10;
  private goalYaw = 30;
  private goalPitch = 20;

  private frameTarget = new Vector3();
  private frameDistance = 10;
  private frameRadius = 5;

  private readonly pointers = new Map<number, { x: number; y: number; button: number }>();
  private pinch = 0;

  constructor(
    readonly camera: PerspectiveCamera,
    private readonly element: HTMLElement,
  ) {
    element.addEventListener('pointerdown', this.onPointerDown);
    element.addEventListener('pointermove', this.onPointerMove);
    element.addEventListener('pointerup', this.onPointerUp);
    element.addEventListener('pointercancel', this.onPointerUp);
    element.addEventListener('wheel', this.onWheel, { passive: false });
    element.addEventListener('contextmenu', (e) => e.preventDefault());
    window.addEventListener('keydown', this.onKeyDown);
    this.apply();
  }

  /** True while the user is dragging the camera. */
  get dragging(): boolean {
    return this.pointers.size > 0;
  }

  /** Eases the camera to look at a sphere with the whole of it in view. */
  frame(center: Vector3, radius: number): void {
    const halfFov = MathUtils.degToRad(this.camera.fov) / 2;
    // Fit the model in the part of the view the offset leaves clear.
    const width = this.element.clientWidth, height = this.element.clientHeight || 1;
    const aspect = width > 0 ? Math.max(0.3, (width - 2 * this.screenOffset) / height) : this.camera.aspect;
    const halfFovX = Math.atan(Math.tan(halfFov) * aspect);
    this.frameTarget.copy(center);
    this.frameRadius = radius;
    this.frameDistance = (FRAME_MARGIN * radius) / Math.sin(Math.min(halfFov, halfFovX));

    this.goalTarget.copy(this.frameTarget);
    this.goalDistance = this.frameDistance;
    this.maxDistance = Math.max(this.maxDistance, this.frameDistance * 4);
  }

  reframe(): void {
    this.goalTarget.copy(this.frameTarget);
    this.goalDistance = this.frameDistance;
  }

  /** Advances easing and auto-rotation. Returns true if the camera moved. */
  update(dt: number): boolean {
    if (this.autoRotate && !this.dragging) this.goalYaw += AUTO_ROTATE_SPEED * dt;

    const t = 1 - Math.exp(-SMOOTHING * dt);
    const before = [this.target.x, this.target.y, this.target.z, this.distance, this.yaw, this.pitch, this.offset];
    this.target.lerp(this.goalTarget, t);
    this.distance = MathUtils.lerp(this.distance, this.goalDistance, t);
    this.yaw = MathUtils.lerp(this.yaw, this.goalYaw, t);
    this.pitch = MathUtils.lerp(this.pitch, this.goalPitch, t);
    this.offset = MathUtils.lerp(this.offset, this.screenOffset, t);
    this.apply();

    const after = [this.target.x, this.target.y, this.target.z, this.distance, this.yaw, this.pitch, this.offset];
    const scale = Math.max(this.distance, 1);
    return after.some((v, i) => Math.abs(v - before[i]) > 1e-5 * scale);
  }

  private apply(): void {
    const yaw = MathUtils.degToRad(this.yaw);
    const pitch = MathUtils.degToRad(this.pitch);
    this.camera.position.set(
      this.target.x - Math.sin(yaw) * Math.cos(pitch) * this.distance,
      this.target.y + Math.sin(pitch) * this.distance,
      this.target.z + Math.cos(yaw) * Math.cos(pitch) * this.distance,
    );
    this.camera.lookAt(this.target);
    const right = new Vector3(1, 0, 0).applyQuaternion(this.camera.quaternion);
    this.camera.position.addScaledVector(right, -this.offset * this.worldPerPixel(this.distance));
    this.camera.updateMatrixWorld();

    // Keep the depth range tight around the model; AO precision depends on it.
    const reach = this.distance + this.frameRadius * 2;
    this.camera.far = Math.max(10, reach * 1.5);
    this.camera.near = MathUtils.clamp(this.distance - this.frameRadius * 2, 0.1, 5);
    this.camera.updateProjectionMatrix();
  }

  private rotate(dx: number, dy: number): void {
    this.goalYaw += dx * ROTATE_SPEED;
    this.goalPitch = MathUtils.clamp(this.goalPitch + dy * ROTATE_SPEED, -89, 89);
    this.autoRotate = false;
  }

  /** Size of a CSS pixel at the given distance from the camera. */
  private worldPerPixel(distance: number): number {
    return (2 * distance * Math.tan(MathUtils.degToRad(this.camera.fov) / 2)) / (this.element.clientHeight || 1);
  }

  /** Pans so the point under the cursor follows it. */
  private pan(dx: number, dy: number): void {
    const scale = this.worldPerPixel(this.goalDistance);
    const right = new Vector3().setFromMatrixColumn(this.camera.matrixWorld, 0);
    const up = new Vector3().setFromMatrixColumn(this.camera.matrixWorld, 1);
    this.goalTarget.addScaledVector(right, -dx * scale).addScaledVector(up, dy * scale);
  }

  private zoom(factor: number): void {
    this.goalDistance = MathUtils.clamp(this.goalDistance * factor, this.minDistance, this.maxDistance);
  }

  private onPointerDown = (e: PointerEvent): void => {
    this.element.setPointerCapture(e.pointerId);
    this.pointers.set(e.pointerId, { x: e.clientX, y: e.clientY, button: e.button });
    if (this.pointers.size === 2) this.pinch = this.pinchDistance();
    this.onInteract();
  };

  private onPointerMove = (e: PointerEvent): void => {
    const pointer = this.pointers.get(e.pointerId);
    if (!pointer) return;

    if (this.pointers.size === 1) {
      const dx = e.clientX - pointer.x, dy = e.clientY - pointer.y;
      if (pointer.button === 0) this.rotate(dx, dy);
      else this.pan(dx, dy);
    } else if (this.pointers.size === 2) {
      // Two fingers: pan with the midpoint, zoom with the spread.
      const before = this.midpoint();
      pointer.x = e.clientX;
      pointer.y = e.clientY;
      const after = this.midpoint();
      this.pan(after.x - before.x, after.y - before.y);
      const spread = this.pinchDistance();
      if (this.pinch > 0 && spread > 0) this.zoom(this.pinch / spread);
      this.pinch = spread;
    }

    pointer.x = e.clientX;
    pointer.y = e.clientY;
  };

  private onPointerUp = (e: PointerEvent): void => {
    this.pointers.delete(e.pointerId);
    if (this.pointers.size < 2) this.pinch = 0;
  };

  private onWheel = (e: WheelEvent): void => {
    e.preventDefault();
    const pixels = e.deltaMode === WheelEvent.DOM_DELTA_LINE ? e.deltaY * 33 : e.deltaY;
    this.zoom(Math.exp(MathUtils.clamp(pixels, -200, 200) * ZOOM_SPEED));
    this.onInteract();
  };

  private onKeyDown = (e: KeyboardEvent): void => {
    const typing = e.target instanceof HTMLInputElement || e.target instanceof HTMLTextAreaElement;
    if (!typing && (e.key === 'f' || e.key === 'F') && !e.ctrlKey && !e.metaKey) {
      this.reframe();
      this.onInteract();
    }
  };

  private midpoint(): { x: number; y: number } {
    const [a, b] = [...this.pointers.values()];
    return { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 };
  }

  private pinchDistance(): number {
    const [a, b] = [...this.pointers.values()];
    return Math.hypot(a.x - b.x, a.y - b.y);
  }
}
