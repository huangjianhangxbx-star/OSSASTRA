# One bounded fallback capability, not an algorithm

M9's actual remaining MPB stage is about .543 ms. This is a local ceiling, not a promise of recovered frame time or SRP Batcher gain. No fallback production code or Shader changes are authorized by this artifact.

`BindingMaterialGpuContract.cs.txt`, command `StaticTerrainPrivateMaterialBindingCapability`, uses the already-proven natural SRP-context/real Cull/DrawRenderers fixture pattern. It creates private immutable-before-Cull material variants and compares them to original MPBs on PositiveTruth pass3, SightVoxelLit pass0 and SightLit pass0. Allow/deny cases use opposite global owner values; a last-slot deny verifies MPB precedence. Empty submesh0 ensures the last slot is the actual geometry. All material variants have their own renderer mask and material, avoiding queued mutation aliasing.

An independent aggregate `Renderer.HasPropertyBlock()` probe checks renderer-level and final-slot foreign writes plus clear. This is necessary before considering fewer native block reads. Shader Material.HasProperty data is recorded as observation; declaration reflection alone does not decide GPU support.

Result is `necessaryPrerequisitesPass`; `productionBindingAllowed` is always false. Even a positive result does not certify environment-light response, shadows, per-object data, mixed groups, camera lifecycle, source/private CRC bookkeeping or performance. A valid material-owner negative with successful MPB controls stops this route without expanding Shader/URP work. No need to retry the already-negative post-Cull MPB route.

Root alone runs in stable Edit outside timing, byte-loads the uniquely named compiled command, and polls its exact asynchronous JSON path. No manual camera render, context.Submit, quality change, new Shader or original-material edit occurs. All temporary renderers/materials/RTs are destroyed after readbacks; original outer globals are restored through the actual context before normal camera work continues.
