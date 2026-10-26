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
    Demos/         ProteinLoadingDemo, SDFPerformanceDemo
  Scenes/
    SDF Performance.unity
```

## Running

1. Open the project in Unity 2022.3.23f1. If Unity asks, import the TextMeshPro Essential Resources.
2. Open `Assets/Scenes/SDF Performance.unity`, select the demo object, and turn on **Build**. With **Use Cache** turned off, the structure (`1UBQ` by default) is downloaded from RCSB.
3. To use local files, put `.pdb` files in `Assets/Data/Cache/` and turn on **Use Cache**.

`ProteinLoadingDemo` works the same way and can be added to any GameObject.
