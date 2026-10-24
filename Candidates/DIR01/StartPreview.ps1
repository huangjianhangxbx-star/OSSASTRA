param(
    [string]$Study = 'E:/WORLDCREATOR/XingHaiHuiLang/work/OSSASTRA/DIR01-20261024',
    [string]$Runtime = 'E:/WORLDCREATOR/XingHaiHuiLang/Origin/work/AL-01/assets/vendor/spine-webgl-4.1.56.js',
    [string]$Node = 'E:/tools/nodejs/node-v22.14.0-win-x64/node.exe'
)
$ErrorActionPreference = 'Stop'
$previewUrl = 'http://127.0.0.1:5187/'
try { $response = Invoke-WebRequest -Uri $previewUrl -UseBasicParsing -TimeoutSec 2 } catch { $response = $null }
if ($null -ne $response) {
    if ($response.Content -notmatch 'DIR01') { throw '5187 is used by another service; leaving it untouched.' }
    Write-Output $previewUrl
    return
}
foreach ($required in @($Node, $Runtime, (Join-Path $Study 'assets/unit.json'), (Join-Path $Study 'assets/unit.atlas'), (Join-Path $Study 'assets/小蓝.png'))) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "Missing local prerequisite: $required" }
}
$server = Join-Path $PSScriptRoot 'Scripts/Server.cjs'
$arguments = @('"' + $server + '"', '--study', '"' + $Study + '"', '--runtime', '"' + $Runtime + '"')
$process = Start-Process -FilePath $Node -ArgumentList $arguments -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $Study 'server.log') -RedirectStandardError (Join-Path $Study 'server-error.log')
$process.Id | Set-Content -LiteralPath (Join-Path $Study 'server.pid')
Write-Output "Started DIR01 (PID $($process.Id)): $previewUrl"
Write-Output 'Open this URL after the service starts. No dependency installation or model writes are performed.'
