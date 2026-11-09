# Final M9B6 route entry candidate

Candidate-only overlay of the current canonical `Candidates/SCENE-PERF-02/RunPlayer.ps1` (basis SHA256 `2AEE7D82BD9E29A1214EC00012ED81AB2DC5FDCF72E4F550B4508FFFCB63DF97`), retaining its numeric BuildOptions / Mono2x / executable+Assembly hash gate. Root reviews/applies; this directory has not edited production, imported scripts or changed Unity.

## One import and one final build

Native command `ImportScenePerf02FinalDynamicProbe` from `ImportDynamicProbe.cs.txt` imports exactly the previously audited `Player/ScenePerformanceDynamicRouteProbe.cs.txt` to `Assets/OSSASTRA/Scripts/Development/ScenePerformanceDynamicRouteProbe.cs`. Hardcoded candidate SHA256 `CFDAEB483F44A333889A7DA27E1252CDE4A1824492F83F9B990345BCBD14850D`. Requires unique stable author-main Edit, pre-Start Commerce, absent target **and .meta**; no overwrite/replay. A durable `artifacts/SCENE-PERF-02/Final/dynamic-import-*.json` records identity before copying/importing. Author dirty bit, main on-disk SHA, business route/profile and immutable C26 are guarded. No scene save or route mutation. If a response is lost, inspect receipt/target instead of replaying.

The Dynamic driver is unchanged. Explicit route flag plus normal-run/dynamic activates it only in Windows Player. Actual virtual Keyboard/Mouse input goes through the original InputSystem; optional one initial Lab exterior placement is before sampling, not a timed teleport. Existing door/window Lab APIs are labeled API calls, not physical interaction.

After the one import has compiled, Root builds the chosen M9B6 once via existing final build tooling. No per-route or per-candidate scene creation. All launches reuse C26 and the existing isolated commerce route.

## Runtime CLI

Use the reviewed overlay as the canonical runner only after Root review. Two separate dynamic launches:

```powershell
& 'Candidates/SCENE-PERF-02/V11/Final/RunPlayer.ps1' -RequestId '<final-build-request>' -Configuration Release -Mode run -Route dynamic -Seconds 120 -InputRouteJson 'E:/WORLDCREATOR/new/Project_ANKALUTE/Candidates/SCENE-PERF-02/Player/dynamic-route-example.json'
```

The actual CLI extension is `--scene-perf02-input-route "<absolute JSON>"`. The known example SHA256 is `394F54A83FAA00687FA6B48610CB8F81D315D0DD55A7C4EEC3125EF6AA60379E`, 27 steps. The runner requires an existing regular `.json`, explicit absolute path, valid nonempty v1 plan, and exactly120 requested seconds. It writes `input-route-launch.json` and path/SHA in launch receipts, then validates unchanged original SHA, identical runtime `route-plan.json`, actual `result.json` sample >=120 seconds, driver `dynamic-route.json` pass/coverage/device cleanup and exit0. No driver/missing coverage is a failed launch, not an empty dynamic result. Runtime and driver JSON remain separate evidence from the performance60 verdict; input identity success does not claim60fps.

Interior/exterior use the existing flags without `InputRouteJson`, twice60 seconds each. Existing static and CPU branches, dimensions, quality, profiler shutdown and source-route arguments are retained. Dynamic inputs are rejected on other modes/routes rather than silently ignored. A timeout retains the existing process-for-inspection behavior and cannot count as a completed route.

Root sequence: one final build; interior2×60, dynamic2×120, exterior2×60. Natural world and focus behavior are unchanged; the existing driver reports lost focus, death/stuck route or coverage failure instead of faking them.

## Verification and limit

PowerShell parser and bounded pre/post-launch guard tests run without Unity/Player. `ImportDynamicCompile.csproj`:0 warnings/errors. Existing `Player/DynamicCompile.csproj`:0 warnings/errors with **UNITY_STANDALONE_WIN**, not an empty Editor branch. Native import/final route execution still Root-pending.

Registered offscreen1/2/4 expansion is **NOT RUN**. The old Expansion helper edits and saves a writable build scene and does not register duplicates in `VoxelTerrainInstallation.chunks`; it cannot certify M9 eligibility. Do not run it against immutable C26, create per-candidate scenes, or label generic fallback duplicates as retained-terrain expansion coverage. This remains an explicit delivery gap.

## Actual integration correction
First actual dynamic launch was rejected by the Continuation empty-RunDir pre-Start gate because Runner prewrote input-route-launch.json. Canonical/Final Runner now records launch identity outside RunDir before launch, and writes the redundant input sidecar only after Player exit. The original isolation guard is unchanged. The failed Run051324 is excluded; actual Run051740 completed120seconds with coverage and cleanup. Runner hash is superseded by this correction; use current exact file hash. No runtime/build source change was required.
