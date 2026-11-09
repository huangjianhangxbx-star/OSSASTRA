param([string]$RunDirectory)
$ErrorActionPreference='Stop'
if(!$RunDirectory){$RunDirectory=(Get-Content -LiteralPath 'artifacts/SCENE-PERF-02/player-run-latest.json' -Raw | ConvertFrom-Json).runDirectory}
$report=Get-Content -LiteralPath (Join-Path $RunDirectory 'result.json') -Raw | ConvertFrom-Json
[pscustomobject]@{
 run=$RunDirectory
 mode=$report.mode
 route=$report.route
 frames=$report.sampledFrames
 seconds=$report.sampledSeconds
 measurementPass=$report.pass
 performance60Passed=$report.performance60Passed
 poseChanged=$report.poseChanged
 qualityChanged=$report.qualityChanged
 hours=@($report.samples.hours | Sort-Object -Unique)
 exteriorValues=@($report.samples.exteriorView | Sort-Object -Unique)
 runtimeErrors=$report.runtimeErrorCount
 frame=$report.frameMilliseconds
 gpu=$report.gpuFrameMilliseconds
 warmSeconds=$report.normalStaticWarmRenderedSeconds
 stableWarmFrames=$report.normalStaticStableWarmFrames
 warmResets=$report.normalStaticWarmResets
 start=$report.start
 end=$report.end
} | ConvertTo-Json -Depth 4
