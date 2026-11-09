# MaterialBinding fallback — STOPPED / NOT APPLIED

2026-11-09. Root agreed to stop this isolated fallback after a decisive native GPU prerequisite failed. Keep the already measured M9 implementation. No production, Shader, URP, scene, or shared registry file was changed by this candidate.

## Actual evidence

- `artifacts/SCENE-PERF-02/v11-private-material-binding-capability-20261109-034105-1571815.json`: 15 rows on three permission shaders passed pre-Cull Material/MPB parity and last-slot override detection. This established only a necessary binding prerequisite; it did not certify lifetime, lighting, or whole-frame benefit.
- `artifacts/SCENE-PERF-02/v11-lighting-deferred-restore-20261109-041929-4457063.json`: `deferredRestorePrerequisitePass=false`, `productionBindingAllowed=false`, `parity=false`. On PositiveTruth, SightVoxelLit, and SightLit, stable MPB/Material allow, deny, last-slot override, and restored camera-B controls passed. Each `deferred-allow-restore-now` draw failed after `CopyPropertiesFromMaterial` restored the same Material immediately after `DrawRenderers`, before context completion. All three therefore supply a valid negative result for immediate content restoration of a queued Material.
- The Source VoxelLit two-color control did not produce a meaningful positive/negative distinction. Its six rows are **inconclusive**, not a passing source-path timing or lighting result; the receipt explicitly reports this control error.
- Material/source CRC, resource cleanup, and scene preservation passed in the timing receipt. These do not cancel the visible queued-draw failure.

## Why the bounded candidate stops

The isolated sketch avoided mutating Source and borrowed Clone, and left camera-private Adapter content in place at EndScope. That alone is insufficient: its existing Bucket `Refresh` still copies source content into the same Adapter on the next Prepare or source/foreign-content change (`NeedsTerrainReset` forces that refresh). A second use within an unfinished render context can therefore modify a queued draw's referenced Material. No existing callback here proves that earlier GPU commands have finished consuming that reference.

Making every queued Adapter immutable until a proven context boundary would require versioned resources and new lifetime/pinning rules across Final, Truth, multiple cameras, repeated camera invocations, source changes, and foreign edits. That exceeds this single bounded fallback. The negative test is not weakened, delayed until readback, or replaced by a new global owner/culling framework.

## Unproved / intentionally not delivered

- Same-context multi-camera or repeated-Prepare immutable Material lifetime.
- Complete Final/Truth parity and foreign-pinned private-material recovery.
- Full main-scene environment, local-light, shadow, probe, and source `PerObjectData` parity.
- Early conventional Source VoxelLit RGB consumer equivalence. Removing its MPB may change environment RGB before the owned clear/redraw; unknown early consumers require conservative fallback.
- SRP Batcher recovery or any normal-frame performance benefit. The measured M9 PropertyScope cost (~0.54 ms) is a local cost ceiling, not a promised gain.

## Candidate state

`Overlay/` contains an unfinished, unpromoted sketch. Buckets and PropertyScope have candidate changes; PositivePass remains its copied M9 baseline. Truth integration, early-consumer guard, complete lifecycle tests, and full lighting proof are absent. This directory must not be used as a ready patch, compiled into production, or included in the M9 production manifest. See `stopped-snapshot.json` for frozen file hashes and the production comparison at stop time. Scope tests were prepared but not compiled/executed as an integrated fallback.

No further implementation or tests are requested for this branch. Root retains M9 and completes the existing bounded room/replay checks and honest performance reporting.
