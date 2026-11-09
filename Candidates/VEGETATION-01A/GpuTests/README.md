# Finite GrassDeform GPU contract candidate

Child task only prepares these files. It does not import, compile shaders in Unity, run GPU commands, change production or stage Git. Root is the sole Unity writer.

1. Root Native-imports `GrassWindProbe.shader.txt` exactly as `Assets/OSSASTRA/Scripts/WorldRendering/Vegetation/Rendering/GrassWindProbe.shader`; production `GrassWind.hlsl` and `GrassLayer.shader` must already be imported at their planned paths.
2. Root writes `artifacts/VEGETATION-01A/gpu-probe-request.json` containing `{"profilePath":"<exact owned GrassLayerProfileSO asset path under Assets/OSSASTRA/WorldRendering/Vegetation/GrassLab/>"}`. This is the only required fixture input; no search/first-profile fallback occurs. The snippet reads the actual serialized `maximumBend` and refuses zero/nonfinite/>.5 for this positive-motion test.
3. Root executes `GrassWindGpuContract.cs.txt` with the project-native IFunplayCommand tool. Tool success alone is insufficient: inspect `returnValue.pass`, then the output JSON checks/failures. It returns a small receipt even on a failed assertion so resource cleanup evidence remains available.
4. Root removes only the exact owned temporary shader/meta after checking hash and no retained reference. No test pass is added to production Shader.

The probe defines `_GRASS_WIND` and includes the formal HLSL. Material wind is `(direction*strength X/Y, phaseSeconds, maximumBend)`; point `(2,.4,3)`, root `(2,0,3)`. GPU results are world positions read from a private2×2 linear ARGBFloat RT. Alpha, normals and source art are not sampled. Each draw fills and reads all four pixels; nonfinite or nonuniform output fails.

Checks cover the root and horizontal root-row edge, zero wind/zero bend, partial versus full weight, actual tip motion at two phases, phase0/16 recurrence and nearby wrap continuity, identical input reproducibility, representative positive/negative/large finite environmental seconds transported modulo16, and a conservative total-displacement bound `maximumBend*1.5`. The time list is a finite contract sample, not a proof for arbitrary invalid/NaN input or the actual EnvironmentCameraRegistry transport. Source/Profile SHA guards and finally cleanup/activeRT restoration are included.

The snippet also checks four declared production pass names and cached ShaderUtil errors. It does **not** render those passes or prove all variants compiled. This is an actual HLSL function contract, not a real grass-card, shadow, lighting, occlusion, final-frame or performance result. Root still performs those Native scene checks separately.

`dotnet build GrassWindGpuCompile.csproj` only checks the C# snippet against existing local assemblies; it cannot compile HLSL or prove GPU correctness.
