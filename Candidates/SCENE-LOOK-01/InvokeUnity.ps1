param([string]$Name='get_editor_state',[string]$ArgsJson='{}',[string]$CodeFile,[string]$Receipt)
$ErrorActionPreference='Stop'
$arguments=$ArgsJson|ConvertFrom-Json -AsHashtable
if($CodeFile){$Name='execute_code';$arguments=@{code=[IO.File]::ReadAllText([IO.Path]::GetFullPath($CodeFile));skip_refresh=$true;safety_checks=$false}}
$body=@{jsonrpc='2.0';id=[Guid]::NewGuid().ToString('N');method='tools/call';params=@{name=$Name;arguments=$arguments}}|ConvertTo-Json -Depth 20 -Compress
$port=(Get-Content -LiteralPath 'UserSettings/FunplayMcpSettings.json' -Raw|ConvertFrom-Json).port
$reply=Invoke-RestMethod -Uri ('http://127.0.0.1:'+$port+'/mcp') -Method Post -Body $body -ContentType 'application/json; charset=utf-8' -TimeoutSec 180
if($reply.error){throw ($reply.error|ConvertTo-Json -Depth 6)}
$payload=$reply.result.content | Where-Object {$_.type -eq 'text'} | Select-Object -First 1
if(!$payload){throw 'Missing tool response'}
if($Receipt){[IO.File]::WriteAllText([IO.Path]::GetFullPath($Receipt),$payload.text)}
$payload.text
$parsed=$payload.text|ConvertFrom-Json
if($parsed.success -eq $false){throw ('Unity operation failed: '+$parsed.code)}
