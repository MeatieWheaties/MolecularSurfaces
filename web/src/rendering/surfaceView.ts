import {
  BufferAttribute,
  BufferGeometry,
  Color,
  Group,
  Matrix4,
  Mesh,
  MeshStandardMaterial,
  Ray,
  ShaderMaterial,
  SRGBColorSpace,
  Vector3,
} from 'three';
import type { ProteinSurface } from '../surface/builder';
import { atomColors, type ColorScheme } from './colorSchemes';

const HIGHLIGHT_COLOR = new Color().setRGB(1, 0.95, 0.55, SRGBColorSpace);
const HIGHLIGHT_STRENGTH = 0.75;
const RIM_COLOR = new Color().setRGB(0.65, 0.78, 1, SRGBColorSpace);
const RIM_POWER = 3;
const RIM_STRENGTH = 0.2;

/**
 * Lit surface coloured by vertex colour, with a soft rim term. Each vertex also carries
 * its residue index so the hovered residue can be highlighted with a single uniform.
 */
function createSurfaceMaterial(): MeshStandardMaterial {
  const material = new MeshStandardMaterial({ vertexColors: true, roughness: 0.65, metalness: 0 });
  const highlight = { value: -1 };
  material.userData.highlight = highlight;

  material.onBeforeCompile = (shader) => {
    shader.uniforms.highlightResidue = highlight;
    shader.uniforms.highlightColor = { value: HIGHLIGHT_COLOR };
    shader.uniforms.rimColor = { value: RIM_COLOR };

    shader.vertexShader = shader.vertexShader
      .replace('#include <common>', `#include <common>
        attribute float residue;
        uniform float highlightResidue;
        varying float vHighlight;`)
      .replace('#include <begin_vertex>', `#include <begin_vertex>
        vHighlight = abs(residue - highlightResidue) < 0.5 ? 1.0 : 0.0;`);

    shader.fragmentShader = shader.fragmentShader
      .replace('#include <common>', `#include <common>
        uniform vec3 highlightColor;
        uniform vec3 rimColor;
        varying float vHighlight;`)
      .replace('#include <color_fragment>', `#include <color_fragment>
        diffuseColor.rgb = mix(diffuseColor.rgb, highlightColor, vHighlight * ${HIGHLIGHT_STRENGTH.toFixed(2)});`)
      .replace('#include <emissivemap_fragment>', `#include <emissivemap_fragment>
        float rim = 1.0 - saturate(dot(normalize(vViewPosition), normal));
        totalEmissiveRadiance += rimColor * pow(rim, ${RIM_POWER.toFixed(1)}) * ${RIM_STRENGTH.toFixed(2)};`);
  };

  return material;
}

/** Writes each chain's ID into the red channel, so outlines can be drawn wherever the chain changes. 0 = background. */
function createIdMaterial(chain: number): ShaderMaterial {
  // Keep IDs in 1..255 so they survive an 8-bit target.
  const id = (chain % 255) + 1;
  return new ShaderMaterial({
    vertexShader: 'void main() { gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); }',
    fragmentShader: `void main() { gl_FragColor = vec4(${(id / 255).toFixed(6)}, 0.0, 0.0, 1.0); }`,
  });
}

interface ChainView {
  mesh: Mesh<BufferGeometry, MeshStandardMaterial | ShaderMaterial>;
  idMaterial: ShaderMaterial;
  vertexAtoms: Uint32Array;
  colors: BufferAttribute;
}

/**
 * Turns a ProteinSurface into meshes (one per chain) and handles recolouring, chain
 * visibility, residue highlighting and picking.
 */
export class SurfaceView {
  readonly root = new Group();
  surface: ProteinSurface | null = null;
  scheme: ColorScheme = 'chain';

  private readonly material = createSurfaceMaterial();
  private chains: ChainView[] = [];

  get highlightedResidue(): number {
    return this.material.userData.highlight.value;
  }

  show(surface: ProteinSurface): void {
    this.clear();
    this.surface = surface;
    this.setHighlight(-1);

    // Centre the protein on the origin so the camera can orbit it.
    this.root.position.set(-surface.center[0], -surface.center[1], -surface.center[2]);

    for (const chain of surface.chains) {
      const count = chain.vertexAtoms.length;
      const residues = new Float32Array(count);
      for (let v = 0; v < count; v++) residues[v] = surface.atomResidue[chain.vertexAtoms[v]];

      const colors = new BufferAttribute(new Uint8Array(count * 3), 3, true);
      const geometry = new BufferGeometry();
      geometry.setAttribute('position', new BufferAttribute(chain.positions, 3));
      geometry.setAttribute('normal', new BufferAttribute(chain.normals, 3, true));
      geometry.setAttribute('color', colors);
      geometry.setAttribute('residue', new BufferAttribute(residues, 1));
      geometry.setIndex(new BufferAttribute(chain.indices, 1));
      geometry.computeBoundingSphere();

      const mesh = new Mesh<BufferGeometry, MeshStandardMaterial | ShaderMaterial>(geometry, this.material);
      mesh.name = `Chain ${chain.chainId}`;
      mesh.castShadow = true;
      mesh.receiveShadow = true;
      this.root.add(mesh);
      this.chains.push({ mesh, idMaterial: createIdMaterial(chain.index), vertexAtoms: chain.vertexAtoms, colors });
    }

    this.setScheme(this.scheme);
  }

  setScheme(scheme: ColorScheme): void {
    this.scheme = scheme;
    if (!this.surface) return;

    const atoms = atomColors(this.surface, scheme);
    for (const chain of this.chains) {
      const out = chain.colors.array as Uint8Array;
      for (let v = 0; v < chain.vertexAtoms.length; v++) {
        const a = chain.vertexAtoms[v] * 3;
        out[v * 3] = Math.round(atoms[a] * 255);
        out[v * 3 + 1] = Math.round(atoms[a + 1] * 255);
        out[v * 3 + 2] = Math.round(atoms[a + 2] * 255);
      }
      chain.colors.needsUpdate = true;
    }
  }

  isChainVisible(chain: number): boolean {
    return this.chains[chain]?.mesh.visible ?? false;
  }

  setChainVisible(chain: number, visible: boolean): void {
    if (this.chains[chain]) this.chains[chain].mesh.visible = visible;
  }

  setHighlight(residue: number): void {
    this.material.userData.highlight.value = residue;
  }

  /** Swaps every chain to its ID material (for the outline pass), or back. */
  useIdMaterials(on: boolean): void {
    for (const chain of this.chains) chain.mesh.material = on ? chain.idMaterial : this.material;
  }

  /**
   * Finds the atom under a world-space ray by intersecting it with every visible atom
   * sphere. The surface is a union of those spheres, so this matches what's drawn.
   */
  pick(worldRay: Ray): number {
    const surface = this.surface;
    if (!surface) return -1;

    const ray = worldRay.clone().applyMatrix4(new Matrix4().copy(this.root.matrixWorld).invert());
    ray.direction.normalize();
    const { atoms } = surface.protein;
    const toCenter = new Vector3();
    let nearest = Infinity;
    let hit = -1;

    for (const chain of surface.chains) {
      if (!this.isChainVisible(chain.index)) continue;
      for (const a of chain.atoms) {
        const atom = atoms[a];
        const radius = atom.radius * surface.radiusScale;
        toCenter.set(atom.x, atom.y, atom.z).sub(ray.origin);
        const along = toCenter.dot(ray.direction);
        const closest = toCenter.lengthSq() - along * along;
        const r2 = radius * radius;
        if (closest > r2) continue;

        const t = along - Math.sqrt(r2 - closest);
        if (t > 0 && t < nearest) {
          nearest = t;
          hit = a;
        }
      }
    }

    return hit;
  }

  clear(): void {
    for (const chain of this.chains) {
      chain.mesh.geometry.dispose();
      chain.idMaterial.dispose();
      this.root.remove(chain.mesh);
    }
    this.chains = [];
    this.surface = null;
  }
}
