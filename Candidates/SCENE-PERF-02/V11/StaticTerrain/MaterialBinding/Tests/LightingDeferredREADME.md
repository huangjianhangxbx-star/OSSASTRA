# Immediate private-material restore: necessary GPU gate

Native command: `StaticTerrainLightingDeferredRestoreContract` in `LightingDeferredRestoreContract.cs.txt` (or exact compiled `ScenePerf02LightingDeferredRestoreV1.dll`). Root alone executes, outside normal timing. Stable Edit, existing SceneView and AsyncGPUReadback required. Queues one natural SRP context; 25-second bounded timeout; receipt path returned immediately. No Submit, Camera.Render, production shader or pipeline edits.

Twenty-four private draws compare fixed MPB allow/deny, fixed material, material bound before Cull then restored **immediately after DrawRenderers returns**, camera B using the very same restored material, and last-slot foreign MPB precedence. The restore precedes endContextRendering and readback. Source VoxelLit uses its real source-material route and two albedo colors; SightVoxelLit, SightLit and PositiveTruth use the real override-material route and owner permission controls. All ten existing payload fields are assigned. Positive/negative pixels must differ, candidate pixels must match appropriate MPB reference within 1 byte/channel, every material including source/snapshot must retain original CRC, and private objects/RTs must be destroyed without changing the author scene dirty bit/count.

This is only the deferred snapshot gate. Ambient/local/directional lighting, real shadows, light/probe source PerObjectData, batching and final performance are **UNCERTIFIED**. Their tests proceed only if this necessary gate succeeds. If queued draws instead observe restored material values, stop this fallback branch; do not add a global owner workaround. Two disabled private cameras test sequential restoration isolation, not a full URP camera-stack certification.

Offline compile: 0 warnings / 0 errors, 2026-11-09 04:13 UTC (latest normal Player PID checked exited before compile). Source uses engine/editor types only; it does not depend on any ambiguous previously loaded overlay types.

## Actual Native result: negative

Root executed the compiled gate. Receipt: `artifacts/SCENE-PERF-02/v11-lighting-deferred-restore-20261109-041929-4457063.json`.

For PositiveTruth, SightVoxelLit and SightLit, fixed MPB positive/negative controls were meaningful, and fixed Material, restored camera-B draw and last-slot deny all matched their references. **All three allow draws restored immediately after DrawRenderers failed GPU parity**, despite `restoredBeforeEnd=true`. Material/source CRC, author scene, resource cleanup and queued global restoration passed. This is a valid counterexample to treating DrawRenderers as a snapshot of immediately mutable material contents.

The source VoxelLit two-color control produced no meaningful difference, so its source-material timing is **INCONCLUSIVE**, not a passed claim or a cause assigned to the three valid permission negatives. Overall gate is false. Do not continue a material-content restore fallback or claim full lighting coverage. Avoiding immediate restoration alone also does not prove safety against another camera's next Prepare mutating the same material before context submission. No production change, new Shader or global-owner workaround was made.
