# GrassLab cost sampler candidate

`GrassLabCostSampler.cs.txt` is a Development MonoBehaviour candidate only. This child did not import it, call Unity, change production/scene/Profile files, or use Git.

## Use by Root in the owned GrassLab Play window

Attach to the same scene as `GrassLabControls`. Assign `controls` and either the exact same `layers` set or leave layers empty to use controls.layers. It rejects cross-scene or partial sets, missing saved Profile/data, and missing same-camera enabled environment registration.

Call:

```
Begin("artifacts/VEGETATION-01A/cost-original-density.json", 2, 8)
```

It returns true when the coroutine starts. Poll `IsRunning`, `Status`, and `ResultPath`; do not block Editor execution. `Abort()` is public. OnDisable/OnDestroy also abort, release recorders and restore the owned borrow. Output must be a new JSON file below the project's `artifacts/VEGETATION-01A/`; absolute paths are permitted only inside that root. Existing evidence is never overwritten.

Four states use the same saved distribution and normal environment time source:

- S0: Original, grass casting off.
- S1: Static, grass casting off. The material's actual `_GRASS_WIND` keyword must be disabled, rather than only setting strength zero.
- S2: Light, grass casting off.
- S3: Light, grass casting on. Actual Renderer caster mode must be TwoSided.

Each state warms for 2s by default, reads metadata, drains two frames, then measures 8s. It neither drives the environment clock nor modifies Profile/data, density, camera, resolution or quality. It hides the local panel and turns off the local keyboard-reading flag. Those fields and each layer's distinct original mode/shadow choice are restored. An external mode write is detected and is not overwritten with a stale snapshot.

The caller must ensure no recording/build/competing GPU job runs concurrently. The sampler does not start recordings and does not claim to detect arbitrary third-party capture tools. Recordings and timing runs remain separate Root operations.

## Evidence semantics

`ProfilerRecorderHandle.GetAvailable` discovers actual marker registrations. Only exact names are used; the report stores category, unit and data type. Missing, unsupported, invalid or unsampled counters become null summaries, not invented zero values. GC/count zero is valid only after a registered Recorder has an actual sample. Main/Render timings have raw-unit summaries and a separate ms conversion only for actual TimeNanoseconds units. These are total marker times including waiting, not effective CPU work; never add the two threads.

FrameTimingManager requests use only finite positive GPU values with unique nonzero timestamps; warmup/stale duplicates are discarded. Missing data is null/unavailable with any API error. This asynchronous timing is not claimed to pair with the same CPU/raw frame. Frame interval uses Time.unscaledDeltaTime in ms; P50/P95/P99 and every raw observation are saved.

The sampler records actual sharedMaterial keyword/caster state, owned material count from Root's new read-only `OwnedMaterialCount`, current referenced material count, saved near full geometry, and currently assigned enabled-renderer meshes. Frustum candidates and Renderer.isVisible are observations, **not proof of actual GPU draws or occlusion**. LOD can reduce assigned meshes independently of full generated geometry; it is not a per-frame rebuild here.

Per-state before/after and across-S0 checks record camera/quality/environment equality and Profile/data fingerprints. Changed view/grass frames invalidate `comparable`; a completed run can therefore still be unsuitable for cost deltas. Environment seconds continue normally and are recorded separately; they are not removed from the wind or forced equal between states.

Only one coroutine and preallocated frame/timestamp collections are used during sampling. Metadata/allocation work is outside the sample interval with two drain frames. There is no zero-overhead claim: counters, timing requests, the control component and sampling itself can affect CPU/GC.

## Compile boundary

The project references the actual installed Unity profiling APIs, Root's candidates and existing project assemblies. Editor and Player preprocessing must both compile. A Native four-state run/abort and actual counter availability remain Root validation, not this offline compile's claims. Root must keep `GrassLayer.OwnedMaterialCount` in its candidate/production integration; no reflection into private ownership is used.

```
dotnet build Candidates/VEGETATION-01A/Performance/PerformanceCompile.csproj --configuration Release
dotnet build Candidates/VEGETATION-01A/Performance/PerformanceCompile.csproj --configuration Release -p:DefineConstants=
```
