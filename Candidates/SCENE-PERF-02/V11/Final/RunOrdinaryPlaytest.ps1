# Root-owned two-launch check of the already-built Player, without measurement CLI.
# Does not alter user programs, Editor state, frozen source, or existing playtest progress.
param([Parameter(Mandatory=$true)][string]$RequestId)
$ErrorActionPreference='Stop'
if($PWD.Path -ne 'E:\WORLDCREATOR\new\Project_ANKALUTE'){throw 'Exact project root required'}
$manifest=Get-Content -LiteralPath 'artifacts/SCENE-PERF-02/BuildManifests/Release/build-latest.json' -Raw|ConvertFrom-Json
if(!$manifest.success -or $manifest.requestId -ne $RequestId){throw 'Exact requested final Release required'}
if((Get-FileHash -LiteralPath $manifest.executable).Hash -ne $manifest.executableSha256 -or (Get-FileHash -LiteralPath $manifest.playerAssemblyPath).Hash -ne $manifest.playerAssemblySha256){throw 'Final binary changed'}
if(@(Get-Process -Name ScenePerformance02Main -ErrorAction SilentlyContinue).Count){throw 'Wait for previous Player exit'}
$taskRoot='E:/WORLDCREATOR/XingHaiHuiLang/研究/SCENE-PERF-02'
$source=Join-Path $taskRoot 'test-commerce-v2'
$profile=Join-Path $taskRoot 'playtest-profile'
$audit=Join-Path $taskRoot 'playtest-profile-launch-latest.json'
function Fingerprint([string]$folder){
 @(Get-ChildItem -LiteralPath $folder -File|Sort-Object Name|ForEach-Object {[pscustomobject]@{name=$_.Name;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash;length=$_.Length}})
}
$before=Fingerprint $source
if($before.Count -ne 4){throw 'Exact four frozen source files required'}
$out=Join-Path $PWD ('artifacts/SCENE-PERF-02/Final/Ordinary_'+[DateTime]::UtcNow.ToString('yyyyMMdd_HHmmss_fffffff'))
$null=New-Item -ItemType Directory -Path $out
$rows=@()
foreach($attempt in 1..2){
 $started=[DateTime]::UtcNow
 $log=Join-Path $out ('launch-'+$attempt+'.log')
 $proc=Start-Process -FilePath $manifest.executable -ArgumentList @('-screen-fullscreen','0','-screen-width','1600','-screen-height','900','-logFile',('"'+$log+'"')) -PassThru
 $null=$proc.Handle
 $receipt=$null;$deadline=[DateTime]::UtcNow.AddSeconds(30)
 while([DateTime]::UtcNow -lt $deadline -and !$proc.HasExited){
  if(Test-Path -LiteralPath $audit){
   $candidate=Get-Content -LiteralPath $audit -Raw|ConvertFrom-Json
   # PowerShell may already parse ISO Z timestamps as DateTime. Parsing that value
   # again via its culture string drops Kind and shifts UTC by the local offset.
   $observedUtc=if($candidate.utc -is [DateTime]){$candidate.utc.ToUniversalTime()}else{[DateTime]::Parse($candidate.utc,[Globalization.CultureInfo]::InvariantCulture,[Globalization.DateTimeStyles]::RoundtripKind).ToUniversalTime()}
   if($observedUtc -ge $started){$receipt=$candidate;break}
  }
  Start-Sleep -Milliseconds 500
 }
 if($null -eq $receipt -or !$receipt.pass -or !$receipt.preStart -or !$receipt.sourceUnchanged -or [IO.Path]::GetFullPath($receipt.safeRoute) -ne [IO.Path]::GetFullPath($profile)){throw 'Fresh actual unflagged pre-Start isolation receipt missing; process preserved for inspection'}
 $receipt|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $out ('launch-'+$attempt+'-isolation.json'))
 Start-Sleep -Seconds 8
 $proc.Refresh()
 if($proc.HasExited -or !$proc.CloseMainWindow()){throw 'Expected own running Player window unavailable; no force kill'}
 if(!$proc.WaitForExit(20000)){throw 'Own Player did not close gracefully; preserved for inspection'}
 $proc.Refresh()
 $rows+= [pscustomobject]@{launch=$attempt;pid=$proc.Id;exitCode=$proc.ExitCode;createdNewPlaytest=$receipt.createdNewPlaytest;preStart=$receipt.preStart;safeRoute=$receipt.safeRoute;profileAfter=(Fingerprint $profile)}
 $proc.Dispose()
 if($rows[-1].exitCode -ne 0){throw 'Ordinary Player exited with error'}
 if(((Fingerprint $source)|ConvertTo-Json -Compress) -ne ($before|ConvertTo-Json -Compress)){throw 'Frozen source changed'}
}
if($rows[1].createdNewPlaytest){throw 'Second actual launch unexpectedly recreated existing playtest profile'}
$result=[pscustomobject]@{pass=$true;requestId=$manifest.requestId;sourceUnchanged=$true;sourceBefore=$before;sourceAfter=(Fingerprint $source);launches=$rows;scope='Two actual unflagged launches; startup isolation, existing-profile reuse, source SHA and graceful exits. No claim of every gameplay persistence field or hardware input coverage.'}
$result|ConvertTo-Json -Depth 7|Set-Content -LiteralPath (Join-Path $out 'result.json')
[pscustomobject]@{pass=$result.pass;out=$out;created=@($rows.createdNewPlaytest);exitCodes=@($rows.exitCode)}|ConvertTo-Json
