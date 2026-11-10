# SurfaceLighting cost summary

Raw source: `E:\WORLDCREATOR\new\Project_ANKALUTE\artifacts\LOOKDEV-01\SurfaceLighting\editor-cost.json`

Formal conditions: **False**; GPU increment: **Unknown**; CPU effective increment: **Unknown**; combined: **Unknown**.

| State | Frame p50/p95 ms | GPU p50/p95 ms | GPU delta vs A ms | CPU main/render inclusive p50 ms | Present/residual p50 ms |
|---|---:|---:|---:|---:|---:|
| A_previous_before | 41.471 / 45.083 | 28.775 / 42.830 | 0.000 / 0.000 | 39.623 / 8.573 | 0.000 / 39.623 |
| B_material_lighting | 46.911 / 61.964 | 28.767 / 48.739 | 0.009 / 6.007 | 44.968 / 9.065 | 0.000 / 44.968 |
| C_blue_complete | 47.304 / 54.319 | 28.826 / 49.553 | 0.084 / 6.919 | 45.406 / 9.076 | 0.000 / 45.406 |
| A_previous_after | 41.166 / 44.353 | 28.725 / 42.537 | 0.000 / 0.000 | 39.303 / 8.540 | 0.000 / 39.303 |

A interpolation uses phase order (B 1/3, C 2/3), not unavailable stage timestamps. CPU effective work remains Unknown unless measured as a complete non-overlapping decomposition. Inclusive CPU, residual and waits must not be summed or treated as effective work.

| State / series | p50/p95 | Availability / note |
|---|---:|---|
| A_previous_before / Draw Calls Count | 4035.000 / 4035.000 | Measured |
| A_previous_before / SetPass Calls Count | 1251.000 / 1251.000 | Measured |
| A_previous_before / Batches Count | 4035.000 / 4035.000 | Measured |
| A_previous_before / Main Thread [all matching threads] | 41.484 / 46.418 ms | Measured; possibly overlapping |
| A_previous_before / Render Thread [all matching threads] | Unknown / Unknown ms | Unavailable: no valid samples; possibly overlapping |
| A_previous_before / WaitForTargetFPS [main thread only] | Unknown / Unknown ms | Unavailable: no valid samples; possibly overlapping |
| A_previous_before / Gfx.WaitForPresentOnGfxThread [all matching threads] | 0.001 / 0.001 ms | Measured; possibly overlapping |
| A_previous_before / Semaphore.WaitForSignal [main thread only] | 0.000 / 0.000 ms | Measured; possibly overlapping |
| A_previous_before / Style.HDRSource | CPU 0.007 / 0.009; GPU 0.016 / 0.016; inline 0.007 / 0.007 ms | expected=True; Release timings may be unavailable |
| A_previous_before / Style.LogicalGrid | CPU 0.011 / 0.013; GPU 0.009 / 0.010; inline 0.015 / 0.016 ms | expected=True; Release timings may be unavailable |
| A_previous_before / Style.Bloom.Down | CPU 0.022 / 0.026; GPU 0.014 / 0.015; inline 0.013 / 0.015 ms | expected=True; Release timings may be unavailable |
| A_previous_before / Style.Bloom.Up | CPU 0.014 / 0.017; GPU 0.022 / 0.023; inline 0.011 / 0.012 ms | expected=True; Release timings may be unavailable |
| A_previous_before / Style.Composite | CPU 0.007 / 0.009; GPU 0.042 / 0.043; inline 0.008 / 0.009 ms | expected=True; Release timings may be unavailable |
| B_material_lighting / Draw Calls Count | 4199.000 / 4199.000 | Measured |
| B_material_lighting / SetPass Calls Count | 1323.000 / 1323.000 | Measured |
| B_material_lighting / Batches Count | 4199.000 / 4199.000 | Measured |
| B_material_lighting / Main Thread [all matching threads] | 46.860 / 61.908 ms | Measured; possibly overlapping |
| B_material_lighting / Render Thread [all matching threads] | Unknown / Unknown ms | Unavailable: no valid samples; possibly overlapping |
| B_material_lighting / WaitForTargetFPS [main thread only] | Unknown / Unknown ms | Unavailable: no valid samples; possibly overlapping |
| B_material_lighting / Gfx.WaitForPresentOnGfxThread [all matching threads] | 0.001 / 0.001 ms | Measured; possibly overlapping |
| B_material_lighting / Semaphore.WaitForSignal [main thread only] | 0.000 / 0.035 ms | Measured; possibly overlapping |
| B_material_lighting / Style.HDRSource | CPU Unknown / Unknown; GPU Unknown / Unknown; inline Unknown / Unknown ms | expected=False; Release timings may be unavailable |
| B_material_lighting / Style.LogicalGrid | CPU Unknown / Unknown; GPU Unknown / Unknown; inline Unknown / Unknown ms | expected=False; Release timings may be unavailable |
| B_material_lighting / Style.Bloom.Down | CPU Unknown / Unknown; GPU Unknown / Unknown; inline Unknown / Unknown ms | expected=False; Release timings may be unavailable |
| B_material_lighting / Style.Bloom.Up | CPU Unknown / Unknown; GPU Unknown / Unknown; inline Unknown / Unknown ms | expected=False; Release timings may be unavailable |
| B_material_lighting / Style.Composite | CPU 0.012 / 0.013; GPU 0.020 / 0.022; inline 0.017 / 0.019 ms | expected=True; Release timings may be unavailable |
| C_blue_complete / Draw Calls Count | 4207.000 / 4207.000 | Measured |
| C_blue_complete / SetPass Calls Count | 1339.000 / 1339.000 | Measured |
| C_blue_complete / Batches Count | 4207.000 / 4207.000 | Measured |
| C_blue_complete / Main Thread [all matching threads] | 47.248 / 54.359 ms | Measured; possibly overlapping |
| C_blue_complete / Render Thread [all matching threads] | Unknown / Unknown ms | Unavailable: no valid samples; possibly overlapping |
| C_blue_complete / WaitForTargetFPS [main thread only] | Unknown / Unknown ms | Unavailable: no valid samples; possibly overlapping |
| C_blue_complete / Gfx.WaitForPresentOnGfxThread [all matching threads] | 0.001 / 0.001 ms | Measured; possibly overlapping |
| C_blue_complete / Semaphore.WaitForSignal [main thread only] | 0.000 / 0.000 ms | Measured; possibly overlapping |
| C_blue_complete / Style.HDRSource | CPU 0.011 / 0.013; GPU 0.019 / 0.020; inline 0.014 / 0.016 ms | expected=True; Release timings may be unavailable |
| C_blue_complete / Style.LogicalGrid | CPU Unknown / Unknown; GPU Unknown / Unknown; inline Unknown / Unknown ms | expected=False; Release timings may be unavailable |
| C_blue_complete / Style.Bloom.Down | CPU 0.021 / 0.025; GPU 0.017 / 0.018; inline 0.013 / 0.015 ms | expected=True; Release timings may be unavailable |
| C_blue_complete / Style.Bloom.Up | CPU 0.014 / 0.017; GPU 0.036 / 0.037; inline 0.011 / 0.012 ms | expected=True; Release timings may be unavailable |
| C_blue_complete / Style.Composite | CPU 0.007 / 0.008; GPU 0.032 / 0.032; inline 0.009 / 0.010 ms | expected=True; Release timings may be unavailable |
| A_previous_after / Draw Calls Count | 4035.000 / 4035.000 | Measured |
| A_previous_after / SetPass Calls Count | 1251.000 / 1251.000 | Measured |
| A_previous_after / Batches Count | 4035.000 / 4035.000 | Measured |
| A_previous_after / Main Thread [all matching threads] | 41.087 / 44.638 ms | Measured; possibly overlapping |
| A_previous_after / Render Thread [all matching threads] | Unknown / Unknown ms | Unavailable: no valid samples; possibly overlapping |
| A_previous_after / WaitForTargetFPS [main thread only] | Unknown / Unknown ms | Unavailable: no valid samples; possibly overlapping |
| A_previous_after / Gfx.WaitForPresentOnGfxThread [all matching threads] | 0.001 / 0.001 ms | Measured; possibly overlapping |
| A_previous_after / Semaphore.WaitForSignal [main thread only] | 0.000 / 0.000 ms | Measured; possibly overlapping |
| A_previous_after / Style.HDRSource | CPU 0.007 / 0.008; GPU 0.016 / 0.016; inline 0.007 / 0.008 ms | expected=True; Release timings may be unavailable |
| A_previous_after / Style.LogicalGrid | CPU 0.011 / 0.013; GPU 0.009 / 0.011; inline 0.015 / 0.017 ms | expected=True; Release timings may be unavailable |
| A_previous_after / Style.Bloom.Down | CPU 0.021 / 0.024; GPU 0.014 / 0.014; inline 0.013 / 0.015 ms | expected=True; Release timings may be unavailable |
| A_previous_after / Style.Bloom.Up | CPU 0.014 / 0.017; GPU 0.022 / 0.023; inline 0.011 / 0.012 ms | expected=True; Release timings may be unavailable |
| A_previous_after / Style.Composite | CPU 0.007 / 0.008; GPU 0.042 / 0.043; inline 0.008 / 0.009 ms | expected=True; Release timings may be unavailable |

Tracked RT return: reported=True; detailed category match=True. Payload is an estimate, not total VRAM.

| State | Room RT / Look RT | Tracked payload bytes |
|---|---:|---:|
| A_previous_before | 4 / 11 | 171763200 |
| B_material_lighting | 4 / 2 | 149760000 |
| C_blue_complete | 4 / 10 | 168883200 |
| A_previous_after | 4 / 11 | 171763200 |

Stable60: **NotMet**. If any observed p95 exceeds 16.667ms, this window fails. Otherwise a short A/B/C/A run does not certify independent sustained 60FPS.

- Incomplete/invalid formal Release conditions; numeric observations remain descriptive
