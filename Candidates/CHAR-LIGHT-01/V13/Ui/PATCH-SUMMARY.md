# CHAR-LIGHT-01 v1.3 UI candidate

Candidate only. Production, Unity, scenes, Profile assets and Git were not mutated by this child.

The existing Panel now exposes Editor-only complete sunlight A/B buttons. Each click checks exact Main scene path/name, receiver EntityId, and original Profile path plus GUID (or the same actor's explicit A/B path). It loads one of four fixed Profile assets and assigns Settings through the existing cloning setter. It never assigns receiver.profile, searches scenes, creates assets, or saves scenes.

The original environment and outer-glow A/B/C controls, clock ownership and input ownership remain. K/C guidance explains larger means later protection / permits brighter output. Diagnostics distinguish author Profile from the stored A/B source and say protection activation requires targeted sampling, not live pixel monitoring. Player hides the A/B load buttons and provides an explanation; no AssetDatabase path survives Player preprocessing.

SaveCurrentDefault is blocked only after complete A/B loading (including subsequent manual edits). Before that, the original explicit-save function remains. RestorePreview restores both settings and A/B source/lock snapshot. Closing or disabling the Panel does not clear that lock. Reopening while already on B captures B and keeps its source/lock on restore.

## Persistence meaning

Four independently saved assets are reused after reentry by the same A/B buttons. The current Main references/defaults are not changed. Do not claim this candidate publishes B as a Player or Main default. Root will choose the current Play B and verify Native persistence/reentry.

## Checks

- Capability RED against the actual prechange Assembly-CSharp.dll: ApplySunlightPreset(int) was absent (expected exit 1).
- Candidate Editor and Player preprocessor compile: zero warnings/errors against existing Unity/project assemblies. The compile overlay intentionally includes only Panel/UI source and references existing CharacterLighting types to avoid mixed-assembly duplicate identity.
- Capability GREEN against the compiled candidate: public ApplySunlightPreset(int) exists. This is only a compiled-entry check, not a simulated rendering/Native behavior test.
- Native behavior, visual comparison and asset readback are delegated to Root; no child Unity calls occurred.

## Root targeted checks remaining

1. Exact main, each receiver A/B yields its own saved complete settings; the other actor/environment/camera/coordinator remain unchanged.
2. Invalid scene, entity, profile identity, index or missing source reject without replacing Settings.
3. receiver.profile refs and original Profile hashes unchanged; main dirty state preserved.
4. SaveCurrentDefault method refuses under A/B and UI is disabled; slider/preset edits retain lock. SaveCandidate remains explicit unique-new-asset flow.
5. Restore immediately after first B returns original settings/unlocked state; close/reopen on B then Restore returns B plus source/lock. Disable/re-enable preserves lock.
6. Play exit/reentry returns author settings, then one-click B restores the saved candidate; Player has no A/B loading control.

Production initial SHA256: 33EB0302A2B9C9B8E42468A84D1B337C25A6F12F4F3F5AA9C915403B33C21ADB
Production current SHA256: 33EB0302A2B9C9B8E42468A84D1B337C25A6F12F4F3F5AA9C915403B33C21ADB
Candidate SHA256: C0D3E2D9FAC1AFEE3B6312450EEE309C7AB00F2AD8A5DB66E89FE937C43F72B2
