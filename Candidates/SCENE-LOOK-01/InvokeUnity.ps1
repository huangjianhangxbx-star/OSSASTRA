param([string]$Name='get_editor_state',[string]$ArgsJson='{}',[string]$CodeFile,[string]$Receipt)
$ErrorActionPreference='Stop'
$arguments=$ArgsJson|ConvertFrom-Json -AsHashtable
if($CodeFile){$Name='execute_code';$arguments=@{code=[IO.File]::ReadAllText([IO.Path]::GetFullPath($CodeFile));skip_refresh=$true;safety_checks=$false;request_key=('native-'+[Guid]::NewGuid().ToString('N'));wait_seconds=20}}
$port=(Get-Content -LiteralPath 'UserSettings/FunplayMcpSettings.json' -Raw|ConvertFrom-Json).port
# Status recovery must stay readable even while the Editor main thread is busy.
# All other operations independently verify the current project before admission.
if($Name -notin @('get_editor_state','get_task')){
 $probe=@{jsonrpc='2.0';id=[Guid]::NewGuid().ToString('N');method='tools/call';params=@{name='get_editor_state';arguments=@{}}}|ConvertTo-Json -Depth 8 -Compress
 $stateReply=Invoke-RestMethod -Uri ('http://127.0.0.1:'+$port+'/mcp') -Method Post -Body $probe -ContentType 'application/json; charset=utf-8' -TimeoutSec 30
 if($stateReply.error){throw 'Cannot verify current Unity project context'}
 $stateText=$stateReply.result.content|Where-Object {$_.type -eq 'text'}|Select-Object -First 1
 if(!$stateText){throw 'Current Unity context response missing'}
 $state=$stateText.text|ConvertFrom-Json
 $context=$state.data.context
 if(!$state.success -or !$context -or [IO.Path]::GetFullPath($context.project_path).TrimEnd('\','/') -ine [IO.Path]::GetFullPath($PWD.Path).TrimEnd('\','/') -or !$context.expected_context){throw 'Unity project context mismatch or unavailable; no operation admitted'}
 $arguments.expected_context=$context.expected_context
}
$body=@{jsonrpc='2.0';id=[Guid]::NewGuid().ToString('N');method='tools/call';params=@{name=$Name;arguments=$arguments}}|ConvertTo-Json -Depth 20 -Compress
if($CodeFile -and $Receipt){
 [pscustomobject]@{tool=$Name;request_key=$arguments.request_key;expected_context=$arguments.expected_context;codeFile=[IO.Path]::GetFullPath($CodeFile);codeSha256=(Get-FileHash -LiteralPath $CodeFile -Algorithm SHA256).Hash;createdUtc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json|Set-Content -LiteralPath ($Receipt+'.request.json') -Encoding UTF8
}
$reply=Invoke-RestMethod -Uri ('http://127.0.0.1:'+$port+'/mcp') -Method Post -Body $body -ContentType 'application/json; charset=utf-8' -TimeoutSec 180
if($reply.error){throw ($reply.error|ConvertTo-Json -Depth 6)}
$payload=$reply.result.content | Where-Object {$_.type -eq 'text'} | Select-Object -First 1
if(!$payload){throw 'Missing tool response'}
if($Receipt){[IO.File]::WriteAllText([IO.Path]::GetFullPath($Receipt),$payload.text)}
$payload.text
$parsed=$payload.text|ConvertFrom-Json
if($parsed.success -eq $false){throw ('Unity operation failed: '+$parsed.code)}
if($CodeFile){
 $execution=$parsed.data.execution
 if(!$execution -or $execution.status -ne 'completed' -or $execution.execution_status -ne 'returned' -or !$execution.executed -or $execution.serialization_status -ne 'completed' -or !$execution.result_success){
  $id=if($execution){$execution.job_id}else{$parsed.data.job_id}
  throw ('Execution receipt needs recovery/paging; do not replay. Read get_task kind=execution task_id=execution:'+$id+' request_key='+$arguments.request_key+'; receipt='+$Receipt)
 }
}
