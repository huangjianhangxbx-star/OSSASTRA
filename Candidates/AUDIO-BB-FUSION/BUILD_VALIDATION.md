# Build and Editor verification

Editor integration is verified in Tuanjie `2022.3.62t16` Windows64. Final owned scene is saved, Edit mode, no compilation underway, no missing scripts, no temporary input devices or test objects, mute=false, Listener volume=1, timeScale=1. No setting was changed to fabricate audible output.

One bounded Windows64 Development Player build was actually attempted using the new scene. It **failed** in 3.19s with 14 errors in existing `Assets/Unity-Favorites-Tool/Editor/QuickAccessWindow.cs`: missing EditorWindow/AssetModificationProcessor/AssetPostprocessor/MenuItem types. These are the previously documented unrelated plugin build boundary. The plugin and build target were not changed. No runnable Player was delivered. Exact messages: `results/player-build-probe.json`.

Contracts and real gameplay are separate. See `VERIFICATION_INDEX.md`, `results/main-story.json` (20/21), `gun-story.json` (7/7), `world-story.json` (8/9). Pure contracts do not turn these into all-green end-to-end acceptance. `FusionSceneOwnershipContracts` adapts only the obsolete scene guard; its original seven assertions pass. `FusionSceneCycle` returns to the new scene and uses a bounded lease count rather than the obsolete exactly-four-lease expectation, because world/UI adapters deliberately own additional scoped leases. Original fixture files/results remain intact.

All mixed WAVs remain private external work. Public reports contain measurements, hashes and event metadata only. Neither a public Player nor reference-media redistribution is authorized by this technical result.
