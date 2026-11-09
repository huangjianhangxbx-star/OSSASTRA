# GrassScatter candidate

Candidate only. No Unity calls, production edits, asset edits, scene work or Git operations were performed by this child.

## Root integration

- Copy `GrassScatter.cs.txt` only after Root checks its final hash and owns the production destination.
- Namespace and DTO/API names match the requested `Ossastra.WorldRendering.Vegetation` contract.
- `maxDensity` is the **fixed reference cells/m²** for every compared density. Keep it unchanged for half/original/double density: only change `density` within `0..maxDensity`. Roots and retained size/yaw/phase then remain identical.
- Explicit surface IDs are unique nonnegative `surfaceIndex` values. The sampler receives the exact ID plus world XZ. It returns that surface's true root/local anchor and normals, or null when no allowed hit exists. This module never falls back to a different surface or Physics world.
- The exclusion predicate receives the sampled world root including its real height. Edge inset, roof/terrain identity and Collider.Raycast belong to Root's sampler, not global height guessing here.
- Width/height use world units; yaw uses degrees; phase is in `[0,2π)` radians. Both returned normals are normalized. Root's mesh builder still owns transforms, UVs, per-card data, alpha and bounded wind geometry.
- Results are sorted deterministically by surface index and global X/Z cell order. No UnityRandom, material-color heuristic or per-frame state is involved.
- Invalid settings/bounds/duplicate IDs/unbounded grids throw `ArgumentException`; corrupt sampler output or a placement quota breach throws `InvalidOperationException`. Do not persist a partial generation or delete prior author data on error.
- A finite guard rejects more than ten million reference cells in a generation before any sampling, even at sparse requested density. Split oversized explicit targets. Individual root quotas never silently truncate the beginning of a patch.
- This does not prove Native collider sampling, mesh construction, lighting, visibility, animation or GPU performance.

## TDD evidence

`ScatterContract.cs` was written first. A same-API empty-function scaffold returned no placements: 13 behavior contracts failed and only zero-density passed. The first failure was the hand-derived 2x2m plane at 4 cells/m² expecting 16 valid anchors, receiving zero. No compile error was counted as the RED result (`red.log`).

The implementation then passed all 14 contracts (`green.log`): exact planar count/containment, seed replay/change, nested density with unchanged retained attributes, height-aware exclusions, overlapping explicit layers/order independence, missed hits, normalized slope cutoff, zero density, invalid settings and sampled data, atomic quota, duplicate identities/unbounded cells, and adjacent regions.

Run:

```
dotnet run --project Candidates/VEGETATION-01A/Scatter/ScatterContract.csproj --configuration Release
dotnet build Candidates/VEGETATION-01A/Scatter/GrassScatterCompile.csproj --configuration Release
```

The executable references the real installed UnityEngine.CoreModule structures. It uses managed structure/math operations only; no Physics/Mesh/Renderer/GameObject or Unity native runtime methods are invoked. Test samplers are analytic geometry input functions, not a simulated physics API or assertion on mock calls.

Generated `bin/obj` and logs are local compilation products; do not publish binaries as implementation evidence. Runtime-only target is netstandard2.1. Parent owns Unity/visual integration and eventual publication.
