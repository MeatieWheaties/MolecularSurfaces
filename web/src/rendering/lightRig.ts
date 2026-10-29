import { Camera, Color, DirectionalLight, HemisphereLight, Object3D, SRGBColorSpace, Vector3 } from 'three';

const srgb = (r: number, g: number, b: number) => new Color().setRGB(r, g, b, SRGBColorSpace);

/**
 * Direction a light shines from, in camera space (x right, y up, +z towards the viewer),
 * matching the Unity rig's key (35°, -40°) and fill (10°, 55°) Euler offsets.
 */
function cameraSpaceDirection(pitch: number, yaw: number): Vector3 {
  const p = (pitch * Math.PI) / 180, y = (yaw * Math.PI) / 180;
  // Unity's light forward in its left-handed camera space is (cos p sin y, -sin p, cos p cos y);
  // the light comes from the opposite direction, and z flips for a right-handed camera.
  return new Vector3(-Math.cos(p) * Math.sin(y), Math.sin(p), Math.cos(p) * Math.cos(y)).normalize();
}

/**
 * Key + fill lighting that turns with the camera, so whichever side of the molecule
 * you're looking at is well lit, plus a soft sky/ground ambient. Unity's lights don't
 * include the 1/π of a Lambert BRDF, so intensities are scaled by π to match.
 */
export class LightRig {
  readonly key = new DirectionalLight(srgb(1, 0.96, 0.9), 1.05 * Math.PI);
  readonly fill = new DirectionalLight(srgb(0.7, 0.8, 1), 0.35 * Math.PI);
  readonly ambient = new HemisphereLight(srgb(0.38, 0.42, 0.5), srgb(0.08, 0.08, 0.09), 1.25);

  private readonly keyDirection = cameraSpaceDirection(35, -40);
  private readonly fillDirection = cameraSpaceDirection(10, 55);
  private radius = 20;

  constructor() {
    this.key.castShadow = true;
    this.key.shadow.mapSize.set(2048, 2048);
    this.key.shadow.intensity = 0.55;
    this.key.shadow.radius = 3;
    this.key.shadow.normalBias = 0.6;
    this.key.shadow.bias = -0.0005;
  }

  addTo(parent: Object3D): void {
    parent.add(this.key, this.key.target, this.fill, this.fill.target, this.ambient);
  }

  /** Fits the shadow camera around a model of this radius centred on the origin. */
  setModelRadius(radius: number): void {
    this.radius = radius;
    const shadow = this.key.shadow.camera;
    shadow.left = shadow.bottom = -radius * 1.1;
    shadow.right = shadow.top = radius * 1.1;
    shadow.near = 0.1;
    shadow.far = radius * 4;
    shadow.updateProjectionMatrix();
  }

  /** Points the lights relative to the camera. The model is centred on the origin. */
  update(camera: Camera): void {
    for (const [light, local] of [[this.key, this.keyDirection], [this.fill, this.fillDirection]] as const) {
      const world = local.clone().applyQuaternion(camera.quaternion);
      light.position.copy(world).multiplyScalar(this.radius * 2);
      light.target.position.set(0, 0, 0);
    }
  }
}
