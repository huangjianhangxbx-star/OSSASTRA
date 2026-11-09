# Parser + bounded extracted pre-launch guard tests only: never invokes Unity or a Player.
$ErrorActionPreference='Stop'
$path=Join-Path $PSScriptRoot 'RunPlayer.ps1'
$tokens=$null;$parseErrors=$null
$null=[Management.Automation.Language.Parser]::ParseFile($path,[ref]$tokens,[ref]$parseErrors)
if($parseErrors.Count -ne 0){throw ($parseErrors|Out-String)}
$source=Get-Content -LiteralPath $path -Raw
$boundary=$source.IndexOf('$manifestFolder=')
if($boundary -lt 0){throw 'Exact canonical launch boundary missing'}
$guard=[scriptblock]::Create($source.Substring(0,$boundary))
$post=[scriptblock]::Create($source.Substring($source.LastIndexOf('if($normalDynamic){')))
$plan=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../Player/dynamic-route-example.json'))
$scratch=Join-Path $PSScriptRoot ('guard-fixture-'+[Guid]::NewGuid().ToString('N'))
$results=[Collections.Generic.List[object]]::new()
function Check([string]$label,[hashtable]$parameters,[bool]$reject){
 $rejected=$false;$errorText=$null
 try{& $guard -RequestId 'guard-only-no-manifest-access' @parameters}catch{$rejected=$true;$errorText=$_.Exception.Message}
 $pass=$rejected -eq $reject;$results.Add([pscustomobject]@{label=$label;pass=$pass;rejected=$rejected;error=$errorText})
 if(!$pass){throw "Guard expectation failed: $label"}
}
function CheckPost([string]$label,[string]$change,[bool]$reject){
 $run=Join-Path $scratch ([Guid]::NewGuid().ToString('N'));New-Item -ItemType Directory -Path $run | Out-Null
 $InputRouteJson=Join-Path $run 'input.json';Copy-Item -LiteralPath $plan -Destination $InputRouteJson
 $inputRouteSha=(Get-FileHash -LiteralPath $InputRouteJson -Algorithm SHA256).Hash
 Copy-Item -LiteralPath $InputRouteJson -Destination (Join-Path $run 'route-plan.json')
 Set-Content -LiteralPath (Join-Path $run 'dynamic-route.json') -Value '{"pass":true,"coveragePassed":true,"devicesReleased":true}'
 $runtime=[pscustomobject]@{pass=$true;requiredSeconds=120;sampledSeconds=120.02};$exit=[pscustomobject]@{exitCode=0};$normalDynamic=$true
 switch($change){
  'source-mutated' {Add-Content -LiteralPath $InputRouteJson -Value ' '}
  'runtime-copy-mutated' {Add-Content -LiteralPath (Join-Path $run 'route-plan.json') -Value ' '}
  'route-missing' {Remove-Item -LiteralPath (Join-Path $run 'dynamic-route.json') -Force}
  'empty-route' {Set-Content -LiteralPath (Join-Path $run 'dynamic-route.json') -Value '{"pass":false,"coveragePassed":false,"devicesReleased":true}'}
  'short-timing' {$runtime.sampledSeconds=119.99}
  'exit-failed' {$exit.exitCode=2}
 }
 $rejected=$false;$errorText=$null;try{& $post}catch{$rejected=$true;$errorText=$_.Exception.Message}
 $pass=$rejected -eq $reject;$results.Add([pscustomobject]@{label=$label;pass=$pass;rejected=$rejected;error=$errorText})
 if(!$pass){throw "Post-run guard expectation failed: $label"}
}
try{
 New-Item -ItemType Directory -Path $scratch | Out-Null
 Set-Content -LiteralPath (Join-Path $scratch 'empty.json') -Value '{"schemaVersion":1,"steps":[]}'
 Set-Content -LiteralPath (Join-Path $scratch 'broken.json') -Value '{broken'
 Set-Content -LiteralPath (Join-Path $scratch 'plan.txt') -Value '{"schemaVersion":1,"steps":[{}]}'
 Check 'dynamic-missing-explicit-plan' @{Mode='run';Route='dynamic';Seconds=120} $true
 Check 'dynamic-default60-rejected' @{Mode='run';Route='dynamic';InputRouteJson=$plan} $true
 Check 'dynamic-relative-path-rejected' @{Mode='run';Route='dynamic';Seconds=120;InputRouteJson='Candidates/SCENE-PERF-02/Player/dynamic-route-example.json'} $true
 Check 'dynamic-directory-rejected' @{Mode='run';Route='dynamic';Seconds=120;InputRouteJson=$scratch} $true
 Check 'dynamic-missing-file-rejected' @{Mode='run';Route='dynamic';Seconds=120;InputRouteJson=(Join-Path $scratch 'missing.json')} $true
 Check 'dynamic-empty-steps-rejected' @{Mode='run';Route='dynamic';Seconds=120;InputRouteJson=(Join-Path $scratch 'empty.json')} $true
 Check 'dynamic-malformed-json-rejected' @{Mode='run';Route='dynamic';Seconds=120;InputRouteJson=(Join-Path $scratch 'broken.json')} $true
 Check 'dynamic-non-json-file-rejected' @{Mode='run';Route='dynamic';Seconds=120;InputRouteJson=(Join-Path $scratch 'plan.txt')} $true
 Check 'actual27-step120-route-accepted' @{Mode='run';Route='dynamic';Seconds=120;InputRouteJson=$plan} $false
 Check 'ordinary-exterior60-unchanged' @{Mode='run';Route='exterior';Seconds=60} $false
 Check 'ordinary-interior60-unchanged' @{Mode='run';Route='interior';Seconds=60} $false
 Check 'ordinary-CPU-unchanged' @{Mode='cpu';Route='exterior';Seconds=60} $false
 Check 'route-flag-never-silently-ignored' @{Mode='run';Route='exterior';Seconds=60;InputRouteJson=$plan} $true
 CheckPost 'matching-source-runtime-copy-and120s-accepted' 'none' $false
 CheckPost 'postrun-source-SHA-change-rejected' 'source-mutated' $true
 CheckPost 'runtime-plan-copy-SHA-change-rejected' 'runtime-copy-mutated' $true
 CheckPost 'missing-dynamic-driver-rejected' 'route-missing' $true
 CheckPost 'empty-dynamic-driver-rejected' 'empty-route' $true
 CheckPost 'short-actual-window-rejected' 'short-timing' $true
 CheckPost 'nonzero-exit-rejected' 'exit-failed' $true
 [pscustomobject]@{parserPassed=$true;checks=$results.Count;pass=($results.Where({!$_.pass}).Count -eq 0);rows=$results} | ConvertTo-Json -Depth 5
}finally{
 # Only this invocation's verified local fixture directory is removed.
 $resolved=[IO.Path]::GetFullPath($scratch);$root=[IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\')+'\'
 if(!$resolved.StartsWith($root,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($resolved) -notmatch '^guard-fixture-[0-9a-f]{32}$'){throw 'Fixture cleanup boundary rejected'}
 if(Test-Path -LiteralPath $resolved){Remove-Item -LiteralPath $resolved -Recurse -Force}
}
