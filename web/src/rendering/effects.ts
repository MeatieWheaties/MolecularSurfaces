import {
  Color,
  DepthTexture,
  FloatType,
  HalfFloatType,
  LinearFilter,
  Mesh,
  NearestFilter,
  NoBlending,
  OrthographicCamera,
  PerspectiveCamera,
  PlaneGeometry,
  RedFormat,
  Scene,
  ShaderMaterial,
  SRGBColorSpace,
  Vector2,
  Vector4,
  WebGLRenderer,
  WebGLRenderTarget,
} from 'three';

const FULLSCREEN_VERTEX = /* glsl */ `
  varying vec2 vUv;
  void main() {
    vUv = uv;
    gl_Position = vec4(position.xy, 0.0, 1.0);
  }
`;

/** Eye-space helpers shared by the AO and blur passes (+z points away from the camera). */
const DEPTH_HELPERS = /* glsl */ `
  #include <packing>
  uniform sampler2D tDepth;
  uniform vec2 depthTexel;
  uniform float cameraNear;
  uniform float cameraFar;
  uniform vec2 tanHalfFov;   // tan(fov/2) * (aspect, 1)

  float rawDepth(vec2 uv) { return texture2D(tDepth, uv).x; }
  bool isBackground(float raw) { return raw >= 1.0; }
  float eyeDepth(float raw) { return -perspectiveDepthToViewZ(raw, cameraNear, cameraFar); }
  vec3 viewPosition(vec2 uv, float raw) {
    float depth = eyeDepth(raw);
    return vec3((uv * 2.0 - 1.0) * tanHalfFov * depth, depth);
  }
  vec3 viewPosition(vec2 uv) { return viewPosition(uv, rawDepth(uv)); }
  vec2 projectToUv(vec3 p) { return (p.xy / (p.z * tanHalfFov)) * 0.5 + 0.5; }
`;

const AO_FRAGMENT = /* glsl */ `
  ${DEPTH_HELPERS}
  uniform vec2 resolution;
  uniform float radius;
  uniform float bias;
  varying vec2 vUv;

  float hash(vec2 p) { return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453); }

  const int SAMPLES = 16;

  void main() {
    float raw = rawDepth(vUv);
    if (isBackground(raw)) { gl_FragColor = vec4(1.0); return; }

    vec3 p = viewPosition(vUv, raw);

    // Normal from neighbouring depths, taking the smaller step on each axis
    // so silhouettes don't smear into the background.
    vec2 t = depthTexel;
    vec3 pr = viewPosition(vUv + vec2(t.x, 0.0));
    vec3 pl = viewPosition(vUv - vec2(t.x, 0.0));
    vec3 pu = viewPosition(vUv + vec2(0.0, t.y));
    vec3 pd = viewPosition(vUv - vec2(0.0, t.y));
    vec3 dx = abs(pr.z - p.z) < abs(p.z - pl.z) ? pr - p : p - pl;
    vec3 dy = abs(pu.z - p.z) < abs(p.z - pd.z) ? pu - p : p - pd;
    vec3 n = normalize(cross(dy, dx));
    if (dot(n, p) > 0.0) n = -n;

    vec2 pixel = vUv * resolution;
    vec3 random = normalize(vec3(hash(pixel), hash(pixel + 17.17), hash(pixel + 43.43)) * 2.0 - 1.0);

    float occlusion = 0.0;
    for (int s = 0; s < SAMPLES; s++) {
      // Spiral of directions with increasing length, rotated per pixel.
      float k = (float(s) + 0.5) / float(SAMPLES);
      float phi = float(s) * 2.39996;
      float z = 1.0 - 2.0 * k;
      float r = sqrt(1.0 - z * z);
      vec3 dir = vec3(cos(phi) * r, sin(phi) * r, z);
      dir = reflect(dir, random);
      dir *= sign(dot(dir, n));
      dir *= mix(0.15, 1.0, k * k);

      vec3 sp = p + (dir + n * 0.1) * radius;
      float sceneDepth = eyeDepth(rawDepth(projectToUv(sp)));

      float range = clamp(radius / abs(p.z - sceneDepth), 0.0, 1.0);
      occlusion += (sceneDepth < sp.z - bias ? 1.0 : 0.0) * range;
    }

    // Squared so crevices read clearly once the result is shown in sRGB.
    float open = 1.0 - occlusion / float(SAMPLES);
    gl_FragColor = vec4(vec3(open * open), 1.0);
  }
`;

const BLUR_FRAGMENT = /* glsl */ `
  ${DEPTH_HELPERS}
  uniform sampler2D tSource;
  uniform vec2 sourceTexel;
  uniform vec2 direction;
  uniform float radius;
  varying vec2 vUv;

  void main() {
    float center = eyeDepth(rawDepth(vUv));
    float sum = 0.0;
    float weights = 0.0;

    for (int s = -4; s <= 4; s++) {
      vec2 uv = vUv + direction * sourceTexel * float(s);
      float depth = eyeDepth(rawDepth(uv));
      float w = exp(-float(s * s) / 8.0) * clamp(1.0 - abs(depth - center) / (radius * 0.5), 0.0, 1.0);
      sum += texture2D(tSource, uv).r * w;
      weights += w;
    }

    gl_FragColor = vec4(vec3(sum / max(weights, 1e-4)), 1.0);
  }
`;

const COMPOSITE_FRAGMENT = /* glsl */ `
  uniform sampler2D tColor;
  uniform sampler2D tId;
  uniform sampler2D tAO;
  uniform vec2 idTexel;
  uniform float aoIntensity;
  uniform float outlineWidth;
  uniform vec4 outlineColor;
  uniform int debugView;     // 0 final, 1 AO buffer, 2 chain IDs
  varying vec2 vUv;

  float id(vec2 uv) { return texture2D(tId, uv).r; }
  float differs(vec2 uv, float center) { return abs(id(uv) - center) > 0.001 ? 1.0 : 0.0; }

  void main() {
    vec4 color = texture2D(tColor, vUv);
    float center = id(vUv);

    if (debugView == 2) {
      float k = center * 255.0;
      gl_FragColor = vec4(fract(k * 0.37), fract(k * 0.61), fract(k * 0.83), 1.0) * (center > 0.0 ? 1.0 : 0.0);
      return;
    }
    if (debugView == 1) {
      gl_FragColor = vec4(texture2D(tAO, vUv).rrr, 1.0);
      return;
    }

    if (center > 0.0) color.rgb *= mix(1.0, texture2D(tAO, vUv).r, aoIntensity);

    // Outline wherever a neighbouring pixel belongs to a different chain
    // (or to the background), averaged over 8 taps for soft edges.
    if (outlineWidth > 0.0) {
      vec2 o = idTexel * outlineWidth;
      float edge = 0.0;
      edge += differs(vUv + vec2( o.x, 0.0), center);
      edge += differs(vUv + vec2(-o.x, 0.0), center);
      edge += differs(vUv + vec2(0.0,  o.y), center);
      edge += differs(vUv + vec2(0.0, -o.y), center);
      edge += differs(vUv + o * 0.7071, center);
      edge += differs(vUv - o * 0.7071, center);
      edge += differs(vUv + vec2(o.x, -o.y) * 0.7071, center);
      edge += differs(vUv + vec2(-o.x, o.y) * 0.7071, center);
      color.rgb = mix(color.rgb, outlineColor.rgb, clamp(edge / 3.0, 0.0, 1.0) * outlineColor.a);
    }

    gl_FragColor = vec4(color.rgb, 1.0);
    #include <colorspace_fragment>
  }
`;

const BACKGROUND_FRAGMENT = /* glsl */ `
  uniform vec3 top;
  uniform vec3 bottom;
  varying vec2 vUv;
  void main() {
    vec2 c = vUv - 0.5;
    float vignette = 1.0 - dot(c, c) * 0.6;
    gl_FragColor = vec4(mix(bottom, top, vUv.y) * vignette, 1.0);
  }
`;

const srgb = (r: number, g: number, b: number) => new Color().setRGB(r, g, b, SRGBColorSpace);

export interface EffectSettings {
  ambientOcclusion: boolean;
  outlines: boolean;
}

export type DebugView = 'final' | 'ao' | 'ids';

/**
 * Screen-space ambient occlusion, outlines around each chain and a gradient background.
 *
 * Each frame: an ID pass renders chain IDs (and the depth the AO reads); the main pass
 * renders the lit scene with MSAA over the background; AO is computed from depth and
 * blurred with a depth-aware separable blur; a composite pass applies AO and outlines.
 */
export class SurfaceEffects {
  readonly settings: EffectSettings = { ambientOcclusion: true, outlines: true };
  debugView: DebugView = 'final';

  /** World-space AO sampling radius (Å). */
  aoRadius = 4;
  aoIntensity = 1;
  aoBias = 0.05;
  /** In CSS pixels. */
  outlineWidth = 1.5;
  readonly outlineColor = srgb(0.03, 0.035, 0.05);
  readonly outlineAlpha = 0.9;
  readonly backgroundTop = srgb(0.16, 0.18, 0.23);
  readonly backgroundBottom = srgb(0.05, 0.055, 0.07);

  private readonly idTarget: WebGLRenderTarget;
  private readonly colorTarget: WebGLRenderTarget;
  private readonly aoTarget: WebGLRenderTarget;
  private readonly blurTarget: WebGLRenderTarget;

  private readonly quad = new Mesh(new PlaneGeometry(2, 2));
  private readonly quadScene = new Scene();
  private readonly quadCamera = new OrthographicCamera(-1, 1, 1, -1, 0, 1);

  private readonly background: ShaderMaterial;
  private readonly ao: ShaderMaterial;
  private readonly blur: ShaderMaterial;
  private readonly composite: ShaderMaterial;

  private pixelRatio = 1;

  constructor(private readonly renderer: WebGLRenderer) {
    this.idTarget = new WebGLRenderTarget(1, 1, {
      minFilter: NearestFilter,
      magFilter: NearestFilter,
      depthTexture: new DepthTexture(1, 1, FloatType),
    });
    this.colorTarget = new WebGLRenderTarget(1, 1, { type: HalfFloatType, samples: 4 });
    const aoOptions = { format: RedFormat, minFilter: LinearFilter, magFilter: LinearFilter, depthBuffer: false };
    this.aoTarget = new WebGLRenderTarget(1, 1, aoOptions);
    this.blurTarget = new WebGLRenderTarget(1, 1, aoOptions);

    const depthUniforms = () => ({
      tDepth: { value: this.idTarget.depthTexture },
      depthTexel: { value: new Vector2() },
      cameraNear: { value: 0.1 },
      cameraFar: { value: 100 },
      tanHalfFov: { value: new Vector2() },
    });
    const pass = (fragmentShader: string, uniforms: Record<string, { value: unknown }>) =>
      new ShaderMaterial({ vertexShader: FULLSCREEN_VERTEX, fragmentShader, uniforms, depthTest: false, depthWrite: false, blending: NoBlending });

    this.background = pass(BACKGROUND_FRAGMENT, { top: { value: this.backgroundTop }, bottom: { value: this.backgroundBottom } });
    this.ao = pass(AO_FRAGMENT, { ...depthUniforms(), resolution: { value: new Vector2() }, radius: { value: 0 }, bias: { value: 0 } });
    this.blur = pass(BLUR_FRAGMENT, {
      ...depthUniforms(),
      tSource: { value: null },
      sourceTexel: { value: new Vector2() },
      direction: { value: new Vector2() },
      radius: { value: 0 },
    });
    this.composite = pass(COMPOSITE_FRAGMENT, {
      tColor: { value: this.colorTarget.texture },
      tId: { value: this.idTarget.texture },
      tAO: { value: this.aoTarget.texture },
      idTexel: { value: new Vector2() },
      aoIntensity: { value: 1 },
      outlineWidth: { value: 0 },
      outlineColor: { value: new Vector4() },
      debugView: { value: 0 },
    });

    this.quad.frustumCulled = false;
    this.quadScene.add(this.quad);
  }

  setSize(width: number, height: number, pixelRatio: number): void {
    this.pixelRatio = pixelRatio;
    const w = Math.max(1, Math.round(width * pixelRatio));
    const h = Math.max(1, Math.round(height * pixelRatio));
    this.idTarget.setSize(w, h);
    this.colorTarget.setSize(w, h);
    // AO is soft, so it's computed at CSS-pixel resolution even on high-DPI screens.
    const aoW = Math.max(1, Math.round(width)), aoH = Math.max(1, Math.round(height));
    this.aoTarget.setSize(aoW, aoH);
    this.blurTarget.setSize(aoW, aoH);
  }

  /**
   * @param drawIds renders the scene with chain-ID materials
   * @param drawScene renders the lit scene (after the background, without clearing)
   */
  render(camera: PerspectiveCamera, drawIds: () => void, drawScene: () => void): void {
    const { renderer } = this;
    const autoClear = renderer.autoClear;
    renderer.autoClear = false;

    // Chain IDs and depth.
    renderer.setRenderTarget(this.idTarget);
    renderer.setClearColor(0x000000, 0);
    renderer.clear(true, true, false);
    drawIds();

    // Lit scene over the background.
    renderer.setRenderTarget(this.colorTarget);
    renderer.clear(true, true, false);
    this.drawQuad(this.background, this.colorTarget);
    drawScene();

    const useAO = this.settings.ambientOcclusion && this.aoIntensity > 0;
    if (useAO) {
      const tanHalf = Math.tan((camera.fov * Math.PI) / 360);
      for (const material of [this.ao, this.blur]) {
        const u = material.uniforms;
        u.depthTexel.value.set(1 / this.idTarget.width, 1 / this.idTarget.height);
        u.cameraNear.value = camera.near;
        u.cameraFar.value = camera.far;
        u.tanHalfFov.value.set(tanHalf * camera.aspect, tanHalf);
        u.radius.value = this.aoRadius;
      }
      this.ao.uniforms.resolution.value.set(this.aoTarget.width, this.aoTarget.height);
      this.ao.uniforms.bias.value = this.aoBias;
      this.drawQuad(this.ao, this.aoTarget);

      this.blur.uniforms.sourceTexel.value.set(1 / this.aoTarget.width, 1 / this.aoTarget.height);
      this.blur.uniforms.tSource.value = this.aoTarget.texture;
      this.blur.uniforms.direction.value.set(1, 0);
      this.drawQuad(this.blur, this.blurTarget);
      this.blur.uniforms.tSource.value = this.blurTarget.texture;
      this.blur.uniforms.direction.value.set(0, 1);
      this.drawQuad(this.blur, this.aoTarget);
    }

    const u = this.composite.uniforms;
    u.idTexel.value.set(1 / this.idTarget.width, 1 / this.idTarget.height);
    u.aoIntensity.value = useAO ? this.aoIntensity : 0;
    u.outlineWidth.value = this.settings.outlines ? this.outlineWidth * this.pixelRatio : 0;
    u.outlineColor.value.set(this.outlineColor.r, this.outlineColor.g, this.outlineColor.b, this.outlineAlpha);
    u.debugView.value = ['final', 'ao', 'ids'].indexOf(this.debugView);
    this.drawQuad(this.composite, null);

    renderer.autoClear = autoClear;
  }

  private drawQuad(material: ShaderMaterial, target: WebGLRenderTarget | null): void {
    this.quad.material = material;
    this.renderer.setRenderTarget(target);
    this.renderer.render(this.quadScene, this.quadCamera);
  }

  dispose(): void {
    for (const target of [this.idTarget, this.colorTarget, this.aoTarget, this.blurTarget]) target.dispose();
    for (const material of [this.background, this.ao, this.blur, this.composite]) material.dispose();
    this.quad.geometry.dispose();
  }
}
