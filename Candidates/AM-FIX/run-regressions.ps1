param([string]$ToolsPath='E:/WORLDCREATOR/XingHaiHuiLang/work/OSSASTRA/ROADMAP03-20261021/tools.ps1')
. $ToolsPath
$cases=@(
    @{file='Candidates/AM01/Tests/input-story.cs.txt';key='AM01_INPUT_STORY';out='natural-input'},
    @{file='Candidates/AM02/Tests/hands-story.cs.txt';key='AM02_HANDS';out='hands'},
    @{file='Candidates/AM-FIX/Tests/lifecycle-adapted.cs.txt';key='AM01_LIFECYCLE';out='lifecycle'},
    @{file='Candidates/VC02A/Tests/six-skills.cs.txt';key='VC02_SIX_SKILLS';out='six-skills'},
    @{file='Candidates/AM03/Tests/directional-ability-world.cs.txt';key='AM03_TARGET_WORLD';out='targeting'},
    @{file='Candidates/AM02/Tests/freeze-world.cs.txt';key='AM02_FREEZE';out='freeze'}
)
foreach($case in $cases){
    Ensure-Mode edit
    Ensure-Mode play
    $launch=Invoke-UnityTool execute_code @{code=(Get-Content -LiteralPath $case.file -Raw);skip_refresh=$true}
    if(!$launch.success){throw ($launch|ConvertTo-Json -Depth 8)}
    $deadline=[DateTime]::UtcNow.AddSeconds(100)
    do{
        Start-Sleep -Seconds 4
        $read=Invoke-UnityTool execute_code @{code=('public static class ReadRegression {public static string Run(){return UnityEditor.SessionState.GetString("'+$case.key+'", "{}");}}');skip_refresh=$true}
        $result=$read.data.result|ConvertFrom-Json
        if([DateTime]::UtcNow -gt $deadline){throw ('Timeout '+$case.key)}
    }while($result.state -eq 'running')
    $read.data.result|Set-Content -LiteralPath ('Candidates/AM-FIX/Evidence/'+$case.out+'-regression.json') -Encoding utf8
    @{key=$case.key;state=$result.state;failed=$result.failed;checks=$result.rows.Count}|ConvertTo-Json -Compress
    if($result.state -ne 'completed' -or $result.failed -ne 0){throw ('Regression failed '+$case.key)}
}
Ensure-Mode edit
