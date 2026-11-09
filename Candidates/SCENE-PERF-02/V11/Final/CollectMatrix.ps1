$ErrorActionPreference='Stop'
$names=@('Run_Release_run_exterior_20261109_053149_6040223','Run_Release_run_exterior_20261109_053300_8855359','Run_Release_run_interior_20261109_050951_3091729','Run_Release_run_interior_20261109_051133_3398242','Run_Release_run_dynamic_20261109_051740_8297436','Run_Release_run_dynamic_20261109_052942_3473299')
$build=Get-Content -LiteralPath 'artifacts/SCENE-PERF-02/Final/timing-build1.json' -Raw|ConvertFrom-Json
if($build.requestId -ne '1b94e817b5bf4977ab3c3728c6aadd9f' -or !$build.success){throw 'Exact archived timing build required'}
$rows=foreach($name in $names){
 $dir=Join-Path 'artifacts/SCENE-PERF-02/Runs' $name
 $r=Get-Content -LiteralPath (Join-Path $dir 'result.json') -Raw|ConvertFrom-Json
 $exit=Get-Content -LiteralPath (Join-Path $dir 'exit.json') -Raw|ConvertFrom-Json
 $identity=Get-Content -LiteralPath (Join-Path $dir 'build-identity.json') -Raw|ConvertFrom-Json
 $required=if($r.route -eq 'dynamic'){120}else{60}
 if(!$r.pass -or $r.sampledSeconds -lt $required -or $r.runtimeErrorCount -ne 0 -or !$r.sourceArchiveUnchanged -or !$r.preStartIsolation -or !$r.isolatedRouteValid -or !$r.settingsRestored -or !$r.cameraCaptureReleased -or !$r.profilerRestored -or $r.qualityChanged -or $r.developmentBuild -or $r.applicationIsEditor -or !$exit.exited -or $exit.exitCode -ne 0 -or !$identity.pass -or $identity.requestId -ne $build.requestId -or $identity.sourceStamp -ne $build.sourceStampBefore -or $identity.frozenCopySha -ne $build.frozenCopySha){throw ('Actual route identity/conditions failed '+$name)}
 if($r.route -ne 'dynamic' -and ($r.poseChanged -or !$r.normalStaticWarmGate -or $r.normalStaticWarmRenderedSeconds -lt 5 -or $r.normalStaticStableWarmFrames -lt 60)){throw 'Static warm/pose gate failed'}
 $coverage=$null
 if($r.route -eq 'dynamic'){
  $c=Get-Content -LiteralPath (Join-Path $dir 'dynamic-route.json') -Raw|ConvertFrom-Json
  $input=Get-Content -LiteralPath (Join-Path $dir 'input-route-identity.json') -Raw|ConvertFrom-Json
  if(!$c.pass -or !$c.coveragePassed -or !$c.devicesReleased -or !$input.pass){throw 'Dynamic actual input/coverage failed'}
  $coverage=[pscustomobject]@{steps=$c.completedSteps;cycles=$c.completedCycles;yawMask=$c.observedYawMask;switches=$c.observedSwitches;orbits=$c.observedOrbitSteps;entries=$c.observedRoomEntries;exits=$c.observedRoomExits;doorApiCalls=$c.doorApiCalls;shutterApiCalls=$c.shutterApiCalls;devicesReleased=$c.devicesReleased}
 }
 [pscustomobject]@{run=$name;route=$r.route;resultSha256=(Get-FileHash -LiteralPath (Join-Path $dir 'result.json')).Hash;seconds=$r.sampledSeconds;frames=$r.sampledFrames;measurementPass=$r.pass;p50Ms=$r.frameMilliseconds.p50;p95Ms=$r.frameMilliseconds.p95;p99Ms=$r.frameMilliseconds.p99;maxMs=$r.frameMilliseconds.max;overBudgetRatio=$r.overBudgetRatio;longestOverBudgetRun=$r.longestOverBudgetRun;performance60Passed=$r.performance60Passed;headroomPassed=$r.headroomPassed;gpuAvailable=$r.gpuFrameMilliseconds.available;gpuStatus=$r.gpuFrameMilliseconds.status;runtimeErrors=$r.runtimeErrorCount;sourceUnchanged=$r.sourceArchiveUnchanged;qualityChanged=$r.qualityChanged;start=$r.start;end=$r.end;coverage=$coverage}
}
$summary=[pscustomobject]@{verdict='阶段改善／稳定60未达';timingBuild=$build.requestId;sourceStamp=$build.sourceStampBefore;frozenSceneSha256=$build.frozenCopySha;normalProfilerEnabled=$false;rows=$rows;excluded=@('Run_Release_run_dynamic_20261109_051324_9919597: Runner sidecar rejected by empty startup directory','Run_Release_run_dynamic_20261109_052215_1227035: focus lost at46.65s','Run_Release_run_dynamic_20261109_052555_5029774 and Run_Release_run_exterior_20261109_052740_9694222: Root overlapping launch');limits=@('门窗动作使用既有Lab API，不是实体硬件操作认证','进出计数来自ExteriorView变化，切控可能贡献','Release GPU timing unavailable；不得将raw0当作0ms','offscreen登记1/2/4尚未执行；2倍可见和实际60Hz呈现未通过先决预算','普通试玩startup隔离后续修复单独构建，不把它记作算法性能收益')}
$summary|ConvertTo-Json -Depth 9|Set-Content -LiteralPath 'docs/ossastra/SCENE-PERF-02/final-matrix.json'
$rows|Select-Object run,route,p50Ms,p95Ms,p99Ms,performance60Passed|ConvertTo-Json
