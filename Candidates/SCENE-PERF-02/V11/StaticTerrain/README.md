# M9 isolated candidate

Root remains the only Unity/Git/production writer. Child owns only this directory. No production overlay has been applied.

1. Read `design.md`; scope starts at explicit `VoxelTerrainInstallation.chunks`, leaving the two unlisted caps on fallback.
2. Root runs `RepeatedMainCallsContract.cs.txt` (class `StaticTerrainRepeatedMainCallsContract`) in a stable already-rendered main Play, outside normal timing. The command executes real Module/Feature methods synchronously and restores their scopes; it does not change camera, author assets, scene or quality.
3. Expected original result: `semanticsPass=true`, `baselineRed=true`, with real storage-reuse assertions failing. Errors, incomplete actual plans, initial foreign MPBs, unstable main or unprepared Feature make the fixture inconclusive rather than a false RED.
4. Candidate implementation follows only after that baseline receipt. Re-run the same real calls expecting all retention checks to pass, followed by the separate invalidation/foreign-block/source-CRC/GPU tests described in `design.md`.

Optional offline contract compile, **only after checking the latest Player PID has exited**:

```powershell
dotnet build Candidates/SCENE-PERF-02/V11/StaticTerrain/ContractCompile.csproj --nologo --verbosity quiet
```

The fixture intentionally does not print source/material contents or time the editor method calls as Player performance. Final improvement requires same-build normal Player pairs.
