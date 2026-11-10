# SurfaceLighting cost summary

Raw source: `E:\WORLDCREATOR\new\Project_ANKALUTE\artifacts\LOOKDEV-01\SurfaceLighting\PlayerRuns\20261110-091500-751-cost\cost.json`

Formal conditions: **True**; GPU increment: **NotMet**; CPU effective increment: **Unknown**; combined: **NotMet**.

| State | Frame p50/p95 ms | GPU p50/p95 ms | GPU delta vs A ms | CPU main/render inclusive p50 ms | Present/residual p50 ms |
|---|---:|---:|---:|---:|---:|
| A_previous_before | 27.413 / 32.474 | 27.404 / 28.230 | 0.000 / 0.000 | 26.537 / 7.468 | 0.004 / 26.120 |
| B_material_lighting | 28.965 / 32.698 | 28.339 / 29.220 | 0.914 / 0.989 | 28.883 / 7.900 | 0.004 / 28.759 |
| C_blue_complete | 29.529 / 32.768 | 28.540 / 29.298 | 1.094 / 1.065 | 29.392 / 7.951 | 0.004 / 29.328 |
| A_previous_after | 27.292 / 32.408 | 27.467 / 28.234 | 0.000 / 0.000 | 26.522 / 7.475 | 0.004 / 26.173 |

A interpolation uses phase order (B 1/3, C 2/3), not unavailable stage timestamps. CPU effective work remains Unknown unless measured as a complete non-overlapping decomposition. Inclusive CPU, residual and waits must not be summed or treated as effective work.

| State / series | p50/p95 | Availability / note |
|---|---:|---|
| A_previous_before / Draw Calls Count | 4036.000 / 4036.000 | Measured |
| A_previous_before / SetPass Calls Count | 1251.000 / 1251.000 | Measured |
| A_previous_before / Batches Count | 4036.000 / 4036.000 | Measured |
| A_previous_before / Main Thread [all matching threads] | 27.433 / 31.259 ms | Measured; possibly overlapping |
| A_previous_before / Render Thread [all matching threads] | Unknown / Unknown ms | Unavailable: no valid samples; possibly overlapping |
| A_previous_before / WaitForTargetFPS [main thread only] | 0.004 / 4.315 ms | Measured; possibly overlapping |
| A_previous_before / Gfx.WaitForPresentOnGfxThread [all matching threads] | 0.001 / 0.001 ms | Measured; possibly overlapping |
| A_previous_before / Semaphore.WaitForSignal [main thread only] | Unknown / Unknown ms | Unavailable: no valid samples; possibly overlapping |
| A_previous_before / Style.HDRSource | CPU Unknown / Unknown; GPU Unknown / Unknown; inline Unknown / Unknown ms | expected=True; Release timings may be unavailable |
| A_previous_before / Style.LogicalGrid | CPU Unknown / Unknown; GPU Unknown / Unknown; inline Unknown / Unknown ms | expected=True; Release timings may be unavailable |
| A_previous_before / Style.Bloom.Down | CPU Unknown / Unknown; GPU Unknown / Unknown; inline Unknown / Unknown ms | expected=True; Release timings may be unavailable |
| A_previous_before / Style.Bloom.Up | CPU Unknown / Unknown; GPU Unknown / Unknown; inline Unknown / Unknown ms | expected=True; Release timings may be unavailable |
| A_previous_before / Style.Composite | CPU Unknown / Unknown; GPU Unknown / Unknown; inline Unknown / Unknown ms | expected=True; Release timings may be unavailable |
| B_material_lighting / Draw Calls Count | 4200.000 / 4200.000 | Measured |
| B_material_lighting / SetPass Calls Count | 1323.000 / 1323.000 | Measured |
| B_material_lighting / Batches Count | 4200.000 / 4200.000 | Measured |
| B_material_lighting / Main Thread [all matching threads] | 29.096 / 31.899 ms | Measured; possibly overlapping |
| B_material_lighting / Render Thread [all matching threads] | Unknown / Unknown ms | Unavailable: no valid samples; possibly overlapping |
| B_material_lighting / WaitForTargetFPS [main thread only] | 0.004 / 1.242 ms | Measured; possibly overlapping |
| B_material_lighting / Gfx.WaitForPresentOnGfxThread [all matching threads] | 0.001 / 0.001 ms | Measured; possibly overlapping |
| B_material_lighting / Semaphore.WaitForSignal [main thread only] | Unknown / Unknown ms | Unavailable: no valid samples; possibly overlapping |
| B_material_lighting / Style.HDRSource | CPU Unknown / Unknown; GPU Unknown / Unknown; inline Unknown / Unknown ms | expected=False; Release timings may be unavailable |
| B_material_lighting / Style.LogicalGrid | CPU Unknown / Unknown; GPU Unknown / Unknown; inline Unknown / Unknown ms | expected=False; Release timings may be unavailable |
| B_material_lighting / Style.Bloom.Down | CPU Unknown / Unknown; GPU Unknown / Unknown; inline Unknown / Unknown ms | expected=False; Release timings may be unavailable |
| B_material_lighting / Style.Bloom.Up | CPU Unknown / Unknown; GPU Unknown / Unknown; inline Unknown / Unknown ms | expected=False; Release timings may be unavailable |
| B_material_lighting / Style.Composite | CPU Unknown / Unknown; GPU Unknown / Unknown; inline Unknown / Unknown ms | expected=True; Release timings may be unavailable |
| C_blue_complete / Draw Calls Count | 4208.000 / 4208.000 | Measured |
| C_blue_complete / SetPass Calls Count | 1339.000 / 1339.000 | Measured |
| C_blue_complete / Batches Count | 4208.000 / 4208.000 | Measured |
| C_blue_complete / Main Thread [all matching threads] | 29.557 / 32.338 ms | Measured; possibly overlapping |
| C_blue_complete / Render Thread [all matching threads] | Unknown / Unknown ms | Unavailable: no valid samples; possibly overlapping |
| C_blue_complete / WaitForTargetFPS [main thread only] | 0.004 / 1.024 ms | Measured; possibly overlapping |
| C_blue_complete / Gfx.WaitForPresentOnGfxThread [all matching threads] | 0.001 / 0.001 ms | Measured; possibly overlapping |
| C_blue_complete / Semaphore.WaitForSignal [main thread only] | Unknown / Unknown ms | Unavailable: no valid samples; possibly overlapping |
| C_blue_complete / Style.HDRSource | CPU Unknown / Unknown; GPU Unknown / Unknown; inline Unknown / Unknown ms | expected=True; Release timings may be unavailable |
| C_blue_complete / Style.LogicalGrid | CPU Unknown / Unknown; GPU Unknown / Unknown; inline Unknown / Unknown ms | expected=False; Release timings may be unavailable |
| C_blue_complete / Style.Bloom.Down | CPU Unknown / Unknown; GPU Unknown / Unknown; inline Unknown / Unknown ms | expected=True; Release timings may be unavailable |
| C_blue_complete / Style.Bloom.Up | CPU Unknown / Unknown; GPU Unknown / Unknown; inline Unknown / Unknown ms | expected=True; Release timings may be unavailable |
| C_blue_complete / Style.Composite | CPU Unknown / Unknown; GPU Unknown / Unknown; inline Unknown / Unknown ms | expected=True; Release timings may be unavailable |
| A_previous_after / Draw Calls Count | 4036.000 / 4036.000 | Measured |
| A_previous_after / SetPass Calls Count | 1251.000 / 1251.000 | Measured |
| A_previous_after / Batches Count | 4036.000 / 4036.000 | Measured |
| A_previous_after / Main Thread [all matching threads] | 27.550 / 31.060 ms | Measured; possibly overlapping |
| A_previous_after / Render Thread [all matching threads] | Unknown / Unknown ms | Unavailable: no valid samples; possibly overlapping |
| A_previous_after / WaitForTargetFPS [main thread only] | 0.004 / 4.350 ms | Measured; possibly overlapping |
| A_previous_after / Gfx.WaitForPresentOnGfxThread [all matching threads] | 0.001 / 0.001 ms | Measured; possibly overlapping |
| A_previous_after / Semaphore.WaitForSignal [main thread only] | Unknown / Unknown ms | Unavailable: no valid samples; possibly overlapping |
| A_previous_after / Style.HDRSource | CPU Unknown / Unknown; GPU Unknown / Unknown; inline Unknown / Unknown ms | expected=True; Release timings may be unavailable |
| A_previous_after / Style.LogicalGrid | CPU Unknown / Unknown; GPU Unknown / Unknown; inline Unknown / Unknown ms | expected=True; Release timings may be unavailable |
| A_previous_after / Style.Bloom.Down | CPU Unknown / Unknown; GPU Unknown / Unknown; inline Unknown / Unknown ms | expected=True; Release timings may be unavailable |
| A_previous_after / Style.Bloom.Up | CPU Unknown / Unknown; GPU Unknown / Unknown; inline Unknown / Unknown ms | expected=True; Release timings may be unavailable |
| A_previous_after / Style.Composite | CPU Unknown / Unknown; GPU Unknown / Unknown; inline Unknown / Unknown ms | expected=True; Release timings may be unavailable |

Tracked RT return: reported=True; detailed category match=True. Payload is an estimate, not total VRAM.

| State | Room RT / Look RT | Tracked payload bytes |
|---|---:|---:|
| A_previous_before | 4 / 11 | 171763200 |
| B_material_lighting | 4 / 2 | 149760000 |
| C_blue_complete | 4 / 10 | 168883200 |
| A_previous_after | 4 / 11 | 171763200 |

Stable60: **NotMet**. If any observed p95 exceeds 16.667ms, this window fails. Otherwise a short A/B/C/A run does not certify independent sustained 60FPS.

- A_previous_before/Style.HDRSource: one or more expected Release scope timings unavailable
- A_previous_before/Style.LogicalGrid: one or more expected Release scope timings unavailable
- A_previous_before/Style.Bloom.Down: one or more expected Release scope timings unavailable
- A_previous_before/Style.Bloom.Up: one or more expected Release scope timings unavailable
- A_previous_before/Style.Composite: one or more expected Release scope timings unavailable
- B_material_lighting/Style.Composite: one or more expected Release scope timings unavailable
- C_blue_complete/Style.HDRSource: one or more expected Release scope timings unavailable
- C_blue_complete/Style.Bloom.Down: one or more expected Release scope timings unavailable
- C_blue_complete/Style.Bloom.Up: one or more expected Release scope timings unavailable
- C_blue_complete/Style.Composite: one or more expected Release scope timings unavailable
- A_previous_after/Style.HDRSource: one or more expected Release scope timings unavailable
- A_previous_after/Style.LogicalGrid: one or more expected Release scope timings unavailable
- A_previous_after/Style.Bloom.Down: one or more expected Release scope timings unavailable
- A_previous_after/Style.Bloom.Up: one or more expected Release scope timings unavailable
- A_previous_after/Style.Composite: one or more expected Release scope timings unavailable
