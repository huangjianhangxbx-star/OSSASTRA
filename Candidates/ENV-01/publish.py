"""Prepare ENV-01's exact source/contracts/docs whitelist; never commit or push.

Root is the sole Git writer. After all writers stop, Root first runs --dry-run,
reviews the project-external publication-manifest.json, then runs without it to
install the reviewed temporary index. Original dirty bytes and the real index
are protected by the KT-VIS-06 implementation. This module has no Git side
effects when imported; only the explicit main entry prepares an index.
"""
from __future__ import annotations

import importlib.util
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
EXPECTED_ROOT = Path("E:/WORLDCREATOR/new/Project_ANKALUTE")
EVIDENCE = Path("E:/WORLDCREATOR/XingHaiHuiLang/研究/环境模拟/ENV-01")
CANDIDATES = "Candidates/ENV-01"
DOCS = "docs/ossastra/ENV-01"
SCRIPTS = "Assets/OSSASTRA/Scripts"

# Named production files only. No recursive scene/media/asset inclusion.
ENVIRONMENT_FILES = (
    "Core/EnvironmentClock.cs", "Core/SunPath.cs", "Core/SunPose.cs", "Core/WeatherBlend.cs",
    "Runtime/ClimateProfileSO.cs", "Runtime/DayCycleProfileSO.cs",
    "Runtime/EnvironmentCameraRegistry.cs", "Runtime/EnvironmentFrame.cs",
    "Runtime/EnvironmentLightingBinding.cs", "Runtime/EnvironmentMaterialResponse.cs",
    "Runtime/EnvironmentNumbers.cs", "Runtime/EnvironmentRoot.cs",
    "Runtime/EnvironmentVegetationWind.cs", "Runtime/IEnvironmentTimeSource.cs",
    "Runtime/WeatherProfileSO.cs",
    "Rendering/EnvironmentLighting.hlsl", "Rendering/EnvironmentRain.shader",
    "Rendering/EnvironmentSky.shader", "Rendering/EnvironmentSkyBinding.cs",
    "Rendering/EnvironmentSkyPermission.cs",
    "Editor/EnvironmentMainSceneSetup.cs", "Editor/EnvironmentMaterialResponseEditor.cs",
)
PANEL_FILES = (
    "EnvironmentObservationAuthoring.cs", "EnvironmentPanelInputRelay.cs",
    "EnvironmentPanelWidgets.cs", "EnvironmentSimulationPanel.cs",
)
SEAM_FILES = (
    "Characters/Runtime/CharacterEquipmentSpineView.cs", "Characters/Runtime/CharacterOffhand.cs",
    "SceneVisibility/Rooms/Rendering/RoomMaskFrame.cs",
    "SceneVisibility/Rooms/Rendering/RoomPositivePass.cs",
    "SceneVisibility/Rooms/Rendering/RoomPositivePropertyScope.cs",
    "SceneVisibility/Rooms/Rendering/RoomSightDecoration.shader",
    "SceneVisibility/Rooms/Rendering/RoomSightDepthPlane.shader",
    "SceneVisibility/Rooms/Rendering/RoomSightLit.shader",
    "SceneVisibility/Rooms/Rendering/RoomSightSimpleLit.shader",
    "SceneVisibility/Rooms/Rendering/RoomSightSpine.shader",
    "SceneVisibility/Rooms/Rendering/RoomSightSprite.shader",
    "SceneVisibility/Rooms/Rendering/RoomSightSpriteLit.shader",
    "SceneVisibility/Rooms/Rendering/RoomSightThinTop.shader",
    "SceneVisibility/Rooms/Rendering/RoomSightUnlit.shader",
    "SceneVisibility/Rooms/Rendering/RoomSightWorldTile.shader",
    "SceneVisibility/Rooms/Runtime/RoomPresentationBinding.cs",
    "SceneVisibility/Rooms/Runtime/RoomVisibilitySight.cs",
    "WorldRendering/Runtime/WorldLookController.cs", "WorldRendering/Runtime/WorldLookSettings.cs",
)
FOLDER_METAS = (
    "EnvironmentSimulation.meta", "EnvironmentSimulation/Core.meta",
    "EnvironmentSimulation/Runtime.meta", "EnvironmentSimulation/Rendering.meta",
    "EnvironmentSimulation/Editor.meta", "Development/Environment.meta",
)
CANDIDATE_FILES = (
    "AssembleMain.cs.txt", "ClimateDeterminismContract.cs.txt", "ClimateDurationBudgetContract.cs.txt",
    "ContributionContract.cs.txt", "EnvironmentSurfaceRoleContract.cs.txt",
    "FullSurfaceEnvironmentContract.cs.txt", "LightingContract.cs.txt", "LightingExtensionsContract.cs.txt",
    "LightingUniformContract.md", "ManualUniformContract.cs.txt", "NativeBaseline.cs",
    "NativeCloudContract.cs.txt", "NightLookContract.cs.txt", "PanelContract.cs.txt",
    "PanelDiagnosticsContract.cs.txt", "PmaContract.cs.txt", "ProbeShadow.cs.txt",
    "RainProjectionContract.cs.txt", "ReadableNightBaseline.cs.txt", "RenderingFinishContract.cs.txt",
    "RuntimeAdvancedContract.cs.txt", "RuntimeContract.cs.txt", "RuntimeLifecycleContract.cs.txt",
    "SetView.cs.txt", "SetupContract.cs.txt", "ShaderAudit.cs.txt", "SkyContract.cs.txt",
    "SkyLifecycleContract.cs.txt", "SkyWeatherGpuContract.cs.txt", "VegetationWindContract.cs.txt",
    "WetnessContract.cs.txt", "WorldLookEnvironmentContract.cs.txt", "README.md", "mcp.py", "publish.py",
    "NativeAmbientContract.cs.txt", "NativeDarkProbe.cs.txt", "FinalLifecycleContract.cs.txt",
    "WeatherRecordingControl.cs.txt", "FinalRestoreAndSave.cs.txt", "FinalSceneReadback.cs.txt", "check_protection.py",
    "RuntimeHost/Program.cs", "RuntimeHost/RuntimeHost.csproj",
    "RuntimeCompile/RuntimeCompile.csproj", "RuntimeWorldLookCompile/RuntimeWorldLookCompile.csproj",
    "PanelCompile/PanelCompile.csproj",
)
SYNC_BASELINES = {
    "docs/agent-sync/PROJECT_STATE.md": "PROJECT_STATE.md.before.bin",
    "docs/agent-sync/TASK_REGISTRY.md": "TASK_REGISTRY.md.before.bin",
}

spec = importlib.util.spec_from_file_location(
    "env01_protected_publication", ROOT / "Candidates/KT-VIS-06/prepare_publication.py"
)
if spec is None or spec.loader is None:
    raise RuntimeError("Protected temporary-index publication helper missing")
base = importlib.util.module_from_spec(spec)
spec.loader.exec_module(base)
base.EVIDENCE = EVIDENCE
base.CANDIDATES = CANDIDATES
base.DOCS = DOCS
base.SYNC_BASELINES = SYNC_BASELINES


def protected_baseline() -> dict[str, str | None]:
    values = json.loads((EVIDENCE / "protected-dirty-before.json").read_text(encoding="utf-8-sig"))
    if not isinstance(values, dict) or len(values) != 42:
        raise RuntimeError("ENV-01 requires the reviewed 42-path original-dirty baseline")
    for name, backup in SYNC_BASELINES.items():
        if name not in values or base.sha((EVIDENCE / backup).read_bytes()) != values[name]:
            raise RuntimeError("ENV-01 sync prefix backup does not match its protected baseline: " + name)
    return values


def collect(excluded: set[str]) -> tuple[list[str], list[str]]:
    if ROOT != EXPECTED_ROOT.resolve():
        raise RuntimeError("Wrong ENV-01 project root")
    baseline = protected_baseline()
    base.NEVER_STAGE = set(base.NEVER_STAGE) | (set(baseline) - set(SYNC_BASELINES))
    files: set[str] = set()

    def add(name: str) -> None:
        base.reject_forbidden(name)
        path = ROOT / name
        if not path.is_file() or not path.resolve().is_relative_to(ROOT):
            raise RuntimeError("Missing or external exact publication file: " + name)
        if name in baseline:
            raise RuntimeError("Original dirty path cannot enter the regular ENV-01 whitelist: " + name)
        files.add(name)

    for name in ENVIRONMENT_FILES:
        add(f"{SCRIPTS}/EnvironmentSimulation/{name}")
        add(f"{SCRIPTS}/EnvironmentSimulation/{name}.meta")
    for name in PANEL_FILES:
        add(f"{SCRIPTS}/Development/Environment/{name}")
        add(f"{SCRIPTS}/Development/Environment/{name}.meta")
    for name in SEAM_FILES:
        add(f"{SCRIPTS}/{name}")
        add(f"{SCRIPTS}/{name}.meta")
    for name in FOLDER_METAS:
        add(f"{SCRIPTS}/{name}")
    for name in excluded:
        if name not in CANDIDATE_FILES or Path(name).name != name or not name.endswith(".cs.txt"):
            raise RuntimeError("Exclude only an explicitly named root-level ENV-01 NAME.cs.txt: " + name)
        if not (ROOT / CANDIDATES / name).is_file():
            raise RuntimeError("Excluded candidate does not exist: " + name)
    skipped = []
    for name in CANDIDATE_FILES:
        if name in excluded:
            skipped.append(f"{CANDIDATES}/{name}")
        else:
            add(f"{CANDIDATES}/{name}")
    # This dedicated task folder may gain Root's final handoff/validation Markdown.
    # Files in subfolders, evidence, and all non-Markdown extensions stay excluded.
    for path in sorted((ROOT / DOCS).glob("*.md")):
        add(path.relative_to(ROOT).as_posix())
    add("docs/agent-sync/tasks/2026-11-04-env-01.md")
    return sorted(files), sorted(skipped)


base.collect_paths = collect
if __name__ == "__main__":
    base.main()
