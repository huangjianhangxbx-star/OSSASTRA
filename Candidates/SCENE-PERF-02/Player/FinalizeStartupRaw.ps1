param([Parameter(Mandatory=$true)][string]$RunDirectory)
$ErrorActionPreference='Stop'
$folder=[IO.Path]::GetFullPath($RunDirectory).TrimEnd('\','/')
$resultPath=Join-Path $folder 'result.json'
$exitPath=Join-Path $folder 'exit.json'
$result=Get-Content -LiteralPath $resultPath -Raw|ConvertFrom-Json
$exit=Get-Content -LiteralPath $exitPath -Raw|ConvertFrom-Json
if(!$exit.exited -or $exit.exitCode -ne 0 -or !$result.pass -or !$result.startupProfilerMode -or !$result.profilerFlagsUntouchedByRuntime -or !$result.rawHashPending -or $result.engineCaptureFrameLimit -ne 256 -or $result.actualFinalFrame -lt 259){throw 'Exact completed successful startup CPU launch with pending closed-raw hash required'}
$raw=[IO.Path]::GetFullPath($result.rawLogFile)
if($raw -ne (Join-Path $folder 'startup-cpu.raw') -or !(Test-Path -LiteralPath $raw -PathType Leaf)){throw 'Only exact owned startup raw may be finalized'}
$info=Get-Item -LiteralPath $raw
if(($info.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or $info.Length -le 128){throw 'Real closed raw file required'}
# Hold an exclusive read handle after the launcher recorded actual OS process exit. No Player can still append this stream.
$stream=[IO.File]::Open($raw,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None)
try{
 $head=New-Object byte[] 4
 if($stream.Read($head,0,4) -ne 4 -or [Text.Encoding]::ASCII.GetString($head) -ne 'PD3U'){throw 'Unexpected actual Unity raw signature'}
 $stream.Position=0
 $algorithm=[Security.Cryptography.SHA256]::Create()
 try{$sha=[BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-','')}finally{$algorithm.Dispose()}
 $length=$stream.Length
}finally{$stream.Dispose()}
$result.rawBytes=$length
$result.rawSha256=$sha
$result.rawHashPending=$false
$result|Add-Member -NotePropertyName rawClosedAfterProcessExit -NotePropertyValue $true
$result|Add-Member -NotePropertyName rawHashFinalizedUtc -NotePropertyValue ([DateTime]::UtcNow.ToString('O'))
$result|Add-Member -NotePropertyName rawExitReceipt -NotePropertyValue $exitPath
$result|ConvertTo-Json -Depth 20|Set-Content -LiteralPath $resultPath
[PSCustomObject]@{result=$resultPath;raw=$raw;bytes=$length;sha256=$sha;rawClosedAfterProcessExit=$true;exitCode=$exit.exitCode}|ConvertTo-Json
