$root=Resolve-Path (Join-Path $PSScriptRoot '../../..')
$checks=@{
 'CharacterLighting/Runtime/CharacterLightingReceiver.cs'='Ossastra.Characters|CharacterEquipmentSpineView|CharacterEquipmentView';
 'CharacterLighting/Integration/CharacterLightingSystem.cs'='Ossastra.Development|CharacterLightingPanel';
 'EnvironmentSimulation/Rendering/EnvironmentSkyBinding.cs'='Ossastra.Development|EnvironmentSimulationPanel';
 'SceneVisibility/Runtime/SceneVisibilityRoot.cs'='Ossastra.MapResearch|MapResearchRoot';
 'WorldRendering/Runtime/WorldLookController.cs'='XingHai.T7|HD2DPostProfile|OutdoorAtmosphereProfile|RegionalLookProfile';
 'WorldRendering/Runtime/WorldLookSettings.cs'='XingHai.T7|HD2DPostProfile|OutdoorAtmosphereProfile|RegionalLookProfile'
}
$failed=@();foreach($entry in $checks.GetEnumerator()){$path=Join-Path $root ('Assets/OSSASTRA/Scripts/'+$entry.Key);if([IO.File]::ReadAllText($path) -match $entry.Value){$failed+=$entry.Key}}
if($failed.Count){'FAIL production reverse dependencies: '+($failed -join ', ');exit 1}
'PASS production dependency boundaries'
