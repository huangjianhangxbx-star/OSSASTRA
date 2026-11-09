# Exact task-owned publication list. Does not stage shared dirty docs or unrelated assets.
$ErrorActionPreference='Stop'
$paths=[Collections.Generic.List[string]]::new()
$room='Assets/OSSASTRA/Scripts/SceneVisibility/Rooms/'
foreach($p in @('Rendering/RoomMaskFrame.cs','Rendering/RoomMaskRendererFeature.cs','Rendering/RoomPositiveMaterialBuckets.cs','Rendering/RoomPositivePass.cs','Rendering/RoomPositivePropertyScope.cs','Rendering/RoomPositiveRenderingLayers.cs','Runtime/RoomVisibilityModule.cs','Runtime/RoomVisibilitySight.cs','Runtime/RoomStaticTerrainPreparation.cs','Runtime/RoomStaticTerrainPreparation.cs.meta')){$paths.Add($room+$p)}
$paths.Add('Assets/旧代码/Script/Development/DebugHub/Integration/ANKALUTEDevDebugActions.cs')
$paths.Add('Candidates/SCENE-LOOK-01/InvokeUnity.ps1')
$paths.Add('docs/agent-sync/tasks/2026-11-08-scene-perf-02.md')
foreach($p in @('RunPlayer.ps1','QueueBuild.cs.txt','StopOwnProfiler.cs.txt','SummarizeRun.ps1','SetPlayerBuildRoute.cs.txt','RestoreAuthorRoute.cs.txt','ResumeInspect.cs.txt')){$paths.Add('Candidates/SCENE-PERF-02/'+$p)}
$folders=@('Candidates/SCENE-PERF-02/Player','Candidates/SCENE-PERF-02/V11/Editor','Candidates/SCENE-PERF-02/V11/StaticTerrain/Tests','Candidates/SCENE-PERF-02/V11/StaticTerrain/Overlay','docs/ossastra/SCENE-PERF-02')
foreach($folder in $folders){foreach($f in (Get-ChildItem -LiteralPath $folder -File)){
 if($f.Name -match '(\.cs\.txt|\.csproj|\.ps1|\.md)$' -or ($folder -like 'docs/*' -and $f.Name -eq 'final-matrix.json') -or $f.Name -eq 'dynamic-route-example.json'){$paths.Add([IO.Path]::GetRelativePath($PWD.Path,$f.FullName).Replace('\','/'))}
}}
foreach($name in @('RunPlayer.ps1','ImportDynamicProbe.cs.txt','ImportDynamicCompile.csproj','TestRunPlayerGuards.ps1','guard-tests.json','README.md','RunOrdinaryPlaytest.ps1','ReadOrdinaryStartup.cs.txt','CollectMatrix.ps1','CleanupOwned.cs.txt','StagePublication.ps1')){$paths.Add('Candidates/SCENE-PERF-02/V11/Final/'+$name)}
foreach($name in @('integration-manifest.json','README.md','MaterialBinding/STOPPED.md','MaterialBinding/stopped-snapshot.json','MaterialBinding/Tests/LightingDeferredRestoreContract.cs.txt','MaterialBinding/Tests/LightingDeferredCompile.csproj','MaterialBinding/Tests/LightingDeferredREADME.md')){$paths.Add('Candidates/SCENE-PERF-02/V11/StaticTerrain/'+$name)}
# Full same-version embedded dependency, including license/resources. No PackageCache or local-file plugin manifests.
foreach($f in (Get-ChildItem -LiteralPath 'Packages/com.unity.textmeshpro' -File -Recurse)){$paths.Add([IO.Path]::GetRelativePath($PWD.Path,$f.FullName).Replace('\','/'))}
$unique=@($paths|Sort-Object -Unique)
foreach($path in $unique){if(!(Test-Path -LiteralPath $path -PathType Leaf)){throw ('Missing publication file '+$path)};if($path -match '(^|/)(bin|obj|artifacts|Library|Temp|Logs|UserSettings)/|\.raw$|\.exe$|\.dll$|\.pdb$'){throw ('Forbidden generated file '+$path)}}
if(@(git diff --name-only --cached).Count){throw 'Index is not empty; refuse mixing other staged work'}
$unique|Set-Content -LiteralPath 'artifacts/SCENE-PERF-02/Final/publication-paths.txt'
foreach($path in $unique){git add -f -- $path;if($LASTEXITCODE -ne 0){throw ('Stage failed '+$path)}}
$staged=@(git -c core.quotepath=false diff --name-only --cached)
if(@(Compare-Object $unique $staged).Count){throw 'Exact staged path readback mismatch'}
[pscustomobject]@{paths=$unique.Count;stageVerified=$true;tmpDependencyFiles=@($unique|Where-Object {$_ -like 'Packages/com.unity.textmeshpro/*'}).Count;serializedMainAssetsStaged=$false}|ConvertTo-Json
