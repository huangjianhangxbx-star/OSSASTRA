# Bloodborne audio fusion implementation

Goal: apply the user-approved F00–F10 package to the existing single audio root. Unity 2022.3.62t16, existing C#/SO/Mixer only. No Lab engine copying or gameplay changes.

- [x] Fixed baseline, source archive hashes, protected dirty snapshot and read-only Lab audits.
- [x] F01–F03 architecture/source audits and four actual Mixer replays; subjective comparison pending.
- [x] F04 atomic validation/switch and actual 14/14 lifecycle contracts.
- [x] F05–F07 actual contact/death/material/effect facts, distance footsteps, three budgeted loops and existing BGM transport; content gaps explicit.
- [x] F08 monitor and Editor-authored owned scene; 418 dependencies have no private reference.
- [x] F09 targeted contracts/regressions, main natural input 20/21, gun7/7, world8/9; Player blocked, no human signoff invented.
- [x] F10 engineering handoff/audit. Exact GitHub publication and final power action recorded separately in receipt/local power status.

Interfaces: root `TryApplyProfile(profile,out reason)` owns validation, active selection and world invalidation. `SfxSourceHandle` remains the only scoped submit/stop interface. Loop playback extends existing permit consumption, not a second pool. Scene adapters only read entity/position/surface facts. All private native assets are created through Editor APIs under ignored BloodborneLocal, never serialized into public refs.

Validation level C for shared audio lifecycle/profile switch; bounded B for new readonly scene adapters. Tests must demonstrate invalid-profile rollback, stale lease rejection, budgeted loop teardown, actual distance gating, and same-song continuity. P0 gaps remain visible and prevent a claim of full reference coverage.
