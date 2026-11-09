param([Parameter(Mandatory=$true)][string]$RequestId,[ValidateSet('cpu','run','startupcpu')][string]$Mode='cpu',[ValidateSet('exterior','interior','dynamic')][string]$Route='exterior',[int]$Seconds=60,[switch]$Census,[ValidateSet('Development','Release')][string]$Configuration='Development',[string]$InputRouteJson)
$ErrorActionPreference='Stop'
if($Census -and $Mode -ne 'cpu'){throw 'MPB census is a separate CPU diagnostic, never a normal timing run'}
if($Configuration -eq 'Release' -and $Mode -ne 'run'){throw 'Release comparison uses normal frame timing only; CPU profiling diagnostics require Development'}
$normalDynamic=$Mode -eq 'run' -and $Route -eq 'dynamic'
$inputRouteSha=$null
$inputRouteStepCount=0
if($normalDynamic){
 if($Seconds -ne 120){throw 'Normal dynamic acceptance requires exactly Seconds=120'}
 if([string]::IsNullOrWhiteSpace($InputRouteJson) -or ![IO.Path]::IsPathFullyQualified($InputRouteJson)){throw 'Normal dynamic requires an explicit absolute InputRouteJson path'}
 $inputRouteFile=Get-Item -LiteralPath $InputRouteJson -ErrorAction Stop
 if($inputRouteFile.PSIsContainer -or ($inputRouteFile.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or $inputRouteFile.Extension -ine '.json'){throw 'InputRouteJson must be an existing regular JSON file, not a directory or reparse point'}
 $InputRouteJson=$inputRouteFile.FullName
 $inputRouteSha=(Get-FileHash -LiteralPath $InputRouteJson -Algorithm SHA256).Hash
 $inputPlan=Get-Content -LiteralPath $InputRouteJson -Raw | ConvertFrom-Json
 $inputRouteStepCount=@($inputPlan.steps).Count
 if($null -eq $inputPlan -or $inputPlan.schemaVersion -ne 1 -or $null -eq $inputPlan.steps -or $inputRouteStepCount -lt 1 -or $inputRouteStepCount -gt 128){throw 'Explicit dynamic route v1 requires 1..128 nonempty steps; no empty dynamic timing'}
}
elseif(![string]::IsNullOrWhiteSpace($InputRouteJson)){throw 'InputRouteJson is only accepted for Mode=run Route=dynamic'}
$manifestFolder=Join-Path $PWD ('artifacts/SCENE-PERF-02/BuildManifests/'+$Configuration)
$receipt=Get-Content -LiteralPath (Join-Path $manifestFolder 'build-latest.json') -Raw | ConvertFrom-Json
$queue=Get-Content -LiteralPath (Join-Path $manifestFolder 'queue-latest.json') -Raw | ConvertFrom-Json
if($receipt.requestId -ne $RequestId -or $queue.requestId -ne $RequestId -or !$queue.callbackRan -or $queue.status -ne 'finished-see-build-receipt' -or !$receipt.success -or !$receipt.originalSourcePreserved -or $receipt.result -ne 'Succeeded'){throw 'Matching completed successful Native build required'}
$expectedDevelopment=$Configuration -eq 'Development'
$expectedOptionsValue=if($expectedDevelopment){1}else{0}
# Tuanjie BuildOptions has obsolete aliases for value 1. Native build receipt already
# requires summary.options == requested options; compare numeric value and exact readback,
# not Enum.ToString(), which can list aliases such as Il2CPP despite the Mono2x guard.
if($receipt.configuration -ne $Configuration -or $queue.configuration -ne $Configuration -or $receipt.developmentBuild -ne $expectedDevelopment -or $receipt.buildOptionsValue -ne $expectedOptionsValue -or $receipt.actualBuildOptions -ne $receipt.buildOptions -or $receipt.scriptingBackend -ne 'Mono2x' -or !$receipt.sourceCodeUnchanged){throw 'Manifest configuration/options/backend/source identity mismatch'}
if((Get-FileHash -LiteralPath $receipt.executable).Hash -ne $receipt.executableSha256 -or (Get-FileHash -LiteralPath $receipt.playerAssemblyPath).Hash -ne $receipt.playerAssemblySha256){throw 'Stable Player output changed since this configuration manifest was built'}
$existingPlayer=@(Get-Process -Name ([IO.Path]::GetFileNameWithoutExtension($receipt.executable)) -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $receipt.executable })
if($existingPlayer.Count -ne 0){throw 'An instance of this exact Player is still running; wait for its actual exit before launching another timing route'}
# Editor profiling is a separate diagnostic; do not leave its UI recording in a normal Player timing window.
if($Mode -eq 'run'){
 $null=& 'Candidates/SCENE-LOOK-01/InvokeUnity.ps1' -CodeFile 'Candidates/SCENE-PERF-02/StopOwnProfiler.cs.txt' -Receipt 'artifacts/SCENE-PERF-02/pre-normal-editor-profiler.json'
}
if(!(Test-Path -LiteralPath $receipt.executable)){throw 'Exact current build executable missing'}
$run=Join-Path (Join-Path $PWD "artifacts/SCENE-PERF-02/Runs") ('Run_'+$Configuration+'_'+$Mode+'_'+$Route+'_'+[DateTime]::UtcNow.ToString('yyyyMMdd_HHmmss_fffffff'))
New-Item -ItemType Directory -Path $run | Out-Null
$launchArgs=@('-screen-fullscreen','0','-screen-width','1600','-screen-height','900',('--scene-perf02-'+$(if($Mode -eq 'startupcpu'){'cpu'}else{$Mode})),('"'+$run+'"'),'--scene-perf02-route',$Route,'--scene-perf02-seconds',[string]$Seconds,'--scene-perf02-source','"E:/WORLDCREATOR/XingHaiHuiLang/研究/SCENE-PERF-02/test-commerce-v2"','--scene-perf-build-scene',('"'+$receipt.actualBuildScenePath+'"'),'-logFile',('"'+($run+'.Player.log')+'"'))
if($Mode -eq 'cpu'){$launchArgs+=@('-profiler-maxusedmemory','268435456')}
if($Census){$launchArgs+='--scene-perf02-mpb-census'}
if($Mode -eq 'startupcpu'){$launchArgs+=@('--scene-perf02-startup-profiler','-profiler-enable','-profiler-log-file',('"'+(Join-Path $run 'startup-cpu.raw')+'"'),'-profiler-capture-frame-count','256','-profiler-maxusedmemory','268435456')}
if($normalDynamic){
 if((Get-FileHash -LiteralPath $InputRouteJson -Algorithm SHA256).Hash -ne $inputRouteSha){throw 'Input route changed during launch preparation'}
 $launchArgs+=@('--scene-perf02-input-route',('"'+$InputRouteJson+'"'))
}
$process=Start-Process -FilePath $receipt.executable -ArgumentList $launchArgs -PassThru
$playerNativeHandle=$process.Handle
$launch=[PSCustomObject]@{requestId=$RequestId;configuration=$Configuration;expectedDevelopmentBuild=$expectedDevelopment;sourceStamp=$receipt.sourceStampBefore;frozenCopySha=$receipt.frozenCopySha;mode=$Mode;route=$Route;seconds=$Seconds;processId=$process.Id;executable=$receipt.executable;runDirectory=$run;actualScene=$receipt.actualBuildScenePath;startedUtc=[DateTime]::UtcNow.ToString('o')}
if($normalDynamic){$launch | Add-Member -NotePropertyName inputRouteJson -NotePropertyValue $InputRouteJson; $launch | Add-Member -NotePropertyName inputRouteSha256 -NotePropertyValue $inputRouteSha}
$launch | ConvertTo-Json | Set-Content -LiteralPath 'artifacts/SCENE-PERF-02/player-run-latest.json'
$launch | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $manifestFolder 'run-latest.json')
$launch | ConvertTo-Json
$deadline=[DateTime]::UtcNow.AddSeconds([Math]::Max(100,$Seconds+75))
while(!$process.WaitForExit(1000)){
 if([DateTime]::UtcNow -ge $deadline){
  [PSCustomObject]@{requestId=$RequestId;processId=$process.Id;exited=$false;status='timeout-process-left-running'} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'exit.json')
  throw 'Player timeout; left running for inspection'
 }
}
$process.Refresh()
$exit=[PSCustomObject]@{requestId=$RequestId;processId=$process.Id;exited=$true;exitCode=$process.ExitCode;finishedUtc=[DateTime]::UtcNow.ToString('o')}
$exit | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'exit.json')
$exit | ConvertTo-Json
$process.Dispose()
if($normalDynamic){
 # Keep RunDir empty for the runtime's strict pre-Start isolation gate. Launch identity
 # is recorded outside RunDir above; publish this redundant sidecar only after exit.
 [PSCustomObject]@{path=$InputRouteJson;sha256=$inputRouteSha;steps=$inputRouteStepCount;seconds=$Seconds;verifiedBeforeLaunch=$true;writtenAfterExit=$true} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'input-route-launch.json')
}
if($Mode -eq 'startupcpu' -and $exit.exitCode -eq 0){ & 'Candidates/SCENE-PERF-02/Player/FinalizeStartupRaw.ps1' -RunDirectory $run }
$runtimePath=Join-Path $run 'result.json'
if($normalDynamic){$runtime=$null}
if(Test-Path -LiteralPath $runtimePath){
 $runtime=Get-Content -LiteralPath $runtimePath -Raw|ConvertFrom-Json
 $identity=[PSCustomObject]@{configuration=$Configuration;expectedDevelopmentBuild=$expectedDevelopment;actualDevelopmentBuild=$runtime.developmentBuild;pass=($runtime.applicationIsEditor -eq $false -and $runtime.runtimePlatform -eq 'WindowsPlayer' -and $runtime.developmentBuild -eq $expectedDevelopment -and $runtime.actualBuildScenePath -eq $receipt.actualBuildScenePath);buildManifest=(Join-Path $manifestFolder 'build-latest.json');requestId=$RequestId;sourceStamp=$receipt.sourceStampBefore;frozenCopySha=$receipt.frozenCopySha}
 $identity|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $run 'build-identity.json')
 if(!$identity.pass){throw 'Actual Player configuration identity differs; result cannot join this M7 comparison'}
}
if($normalDynamic){
 $routeResultPath=Join-Path $run 'dynamic-route.json'
 $routeCopyPath=Join-Path $run 'route-plan.json'
 $routeFileAfter=Get-Item -LiteralPath $InputRouteJson -ErrorAction SilentlyContinue
 $routeShaAfter=if($null -ne $routeFileAfter -and !$routeFileAfter.PSIsContainer -and ($routeFileAfter.Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0){(Get-FileHash -LiteralPath $InputRouteJson -Algorithm SHA256).Hash}else{$null}
 $routeCopySha=if(Test-Path -LiteralPath $routeCopyPath -PathType Leaf){(Get-FileHash -LiteralPath $routeCopyPath -Algorithm SHA256).Hash}else{$null}
 $routeResult=if(Test-Path -LiteralPath $routeResultPath -PathType Leaf){Get-Content -LiteralPath $routeResultPath -Raw | ConvertFrom-Json}else{$null}
 $dynamicValid=$exit.exitCode -eq 0 -and $routeShaAfter -eq $inputRouteSha -and $routeCopySha -eq $inputRouteSha -and $null -ne $runtime -and $runtime.pass -eq $true -and $runtime.requiredSeconds -eq 120 -and $runtime.sampledSeconds -ge 120 -and $null -ne $routeResult -and $routeResult.pass -eq $true -and $routeResult.coveragePassed -eq $true -and $routeResult.devicesReleased -eq $true
 [PSCustomObject]@{pass=$dynamicValid;path=$InputRouteJson;launchSha256=$inputRouteSha;afterSha256=$routeShaAfter;runtimeCopySha256=$routeCopySha;requiredSeconds=120;actualSampledSeconds=$runtime.sampledSeconds;routePass=$routeResult.pass;coveragePassed=$routeResult.coveragePassed;devicesReleased=$routeResult.devicesReleased;scope='Input/source identity and actual nonempty120-second dynamic coverage; performance60 remains a separate result gate'} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'input-route-identity.json')
 if(!$dynamicValid){throw 'Dynamic route failed input SHA, actual120-second timing, coverage, cleanup or exit checks; do not count this as a completed dynamic acceptance run'}
}
