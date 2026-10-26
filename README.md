# MolecularSurfaces

A Unity (2022.3) toolkit for loading protein structures from the RCSB Protein Data Bank and turning them into meshes, using signed distance fields and a parallel marching-cubes mesher.

## Highlights

- **SDF modeling with CSG.** `SDFModel` keeps an ordered stack of box and sphere brushes and combines them with stamp, clip, fill and replace operations. The result is sampled into a voxel grid at a chosen density.
- **Chunked, parallel marching cubes.** `IsoSurface` splits the field into power-of-two chunks (2 to 32 cells per side). It classifies cells and generates triangles in two `Parallel.For` passes, using prefix offsets so each thread writes into its own part of the vertex buffer. `MeshData` and `MeshGenerator` turn the output into Unity meshes.
- **Spatial bounds and raycasting.** `BoundsBox`, `BoundsSphere`, `BoundsGrid` and `BoundsSpace` all implement a shared `IBounds` interface. `Raycasting` provides ray/bounds intersection, and `SDFModelInstance` handles world/local transforms for picking.
- **PDB parsing and molecular model.** `ProteinBuilder` parses PDB text into chains, residues and atoms. It comes with element and residue property tables and selection queries by residue type or sequence pattern.
- **Unity integration.** Instance components render proteins one residue at a time or as merged meshes. The project also includes a lightweight slide and overlay system for live demos.

## Layout

```
Assets/
  Scripts/
    Geometry/      bounds, raycasting, marching cubes, SDF model and brushes
    Molecules/     atom / residue / protein model, PDB builder, queries
    DataSources/   RCSB download, local PDB cache, CSV reader
    Components/    MonoBehaviour instances that render the above
    Presentation/  slide and on-screen text helpers
    Demos/         ProteinViewer (ID entry + loading), OrbitCamera
  Scenes/
    Protein Viewer.unity
```

## Running the viewer

1. Open the project in Unity 2022.3.23f1. If Unity asks, import the TextMeshPro Essential Resources.
2. Open `Assets/Scenes/Protein Viewer.unity` and press Play. Ubiquitin (`1UBQ`) loads first.
3. Type any 4-character PDB ID (for example `1CRN`, `4HHB` or `2PTC`) into the panel and press **Enter** or **Load**. The structure is downloaded from RCSB on a background thread, then meshed and framed in view.

Controls: left-drag to rotate, right- or middle-drag to pan, scroll to zoom, and **F** to reframe. The model slowly auto-rotates until you drag it.

An internet connection is required. Very large entries that RCSB only publishes as mmCIF can't be loaded.
