#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DRPGBattleAudioPaletteProbe
{
    public static string Run()
    {
        DRPGBattleAudioPaletteSO palette =
            ScriptableObject.CreateInstance<DRPGBattleAudioPaletteSO>();
        AudioEventSO genericAttack = ScriptableObject.CreateInstance<AudioEventSO>();
        AudioEventSO skillAttack = ScriptableObject.CreateInstance<AudioEventSO>();
        AudioEventSO voice = ScriptableObject.CreateInstance<AudioEventSO>();
        AudioEventSO victory = ScriptableObject.CreateInstance<AudioEventSO>();
        AudioEventSO defeat = ScriptableObject.CreateInstance<AudioEventSO>();
        AudioEventSO specialWarning =
            ScriptableObject.CreateInstance<AudioEventSO>();
        AudioEventSO windowOpen = ScriptableObject.CreateInstance<AudioEventSO>();
        AudioEventSO guard = ScriptableObject.CreateInstance<AudioEventSO>();
        AudioEventSO perfectGuard =
            ScriptableObject.CreateInstance<AudioEventSO>();
        AudioEventSO miss = ScriptableObject.CreateInstance<AudioEventSO>();
        AudioEventSO weakHit = ScriptableObject.CreateInstance<AudioEventSO>();
        AudioEventSO windowClose = ScriptableObject.CreateInstance<AudioEventSO>();

        try
        {
            SetField(palette, "defaultAttack", genericAttack);
            SetField(palette, "victory", victory);
            SetField(palette, "defeat", defeat);
            SetField(palette, "specialWarning", specialWarning);
            SetField(palette, "interactiveWindowOpen", windowOpen);
            SetField(palette, "guard", guard);
            SetField(palette, "perfectGuard", perfectGuard);
            SetField(palette, "miss", miss);
            SetField(palette, "weakHit", weakHit);
            SetField(palette, "interactiveWindowClose", windowClose);
            SetField(
                palette,
                "skillOverrides",
                new List<DRPGBattleSkillAudioOverride>
                {
                    new DRPGBattleSkillAudioOverride(
                        "skill_heavy",
                        skillAttack,
                        null)
                });
            SetField(
                palette,
                "voiceCues",
                new List<DRPGBattleVoiceAudioEntry>
                {
                    new DRPGBattleVoiceAudioEntry("voice_turn_01", voice)
                });

            AssertResolved(
                palette,
                new BattleAudioCueRequest(
                    BattleAudioCue.Attack,
                    skillId: "skill_heavy"),
                skillAttack,
                BattleAudioPlaybackChannel.Sfx,
                "A skill-specific attack must override the generic attack cue");
            AssertResolved(
                palette,
                new BattleAudioCueRequest(
                    BattleAudioCue.Attack,
                    skillId: "skill_unknown"),
                genericAttack,
                BattleAudioPlaybackChannel.Sfx,
                "An unknown skill must fall back to the generic attack cue");
            AssertResolved(
                palette,
                new BattleAudioCueRequest(
                    BattleAudioCue.Voice,
                    semanticCueId: "voice_turn_01"),
                voice,
                BattleAudioPlaybackChannel.Voice,
                "A semantic voice ID must resolve through the voice map");
            AssertResolved(
                palette,
                new BattleAudioCueRequest(BattleAudioCue.Victory),
                victory,
                BattleAudioPlaybackChannel.Ui,
                "Victory must resolve as a non-spatial result cue");
            AssertResolved(
                palette,
                new BattleAudioCueRequest(BattleAudioCue.Defeat),
                defeat,
                BattleAudioPlaybackChannel.Ui,
                "Defeat must resolve as a non-spatial result cue");
            AssertResolved(
                palette,
                new BattleAudioCueRequest(BattleAudioCue.SpecialWarning),
                specialWarning,
                BattleAudioPlaybackChannel.Ui,
                "A special warning must use the UI channel");
            AssertResolved(
                palette,
                new BattleAudioCueRequest(BattleAudioCue.InteractiveWindowOpen),
                windowOpen,
                BattleAudioPlaybackChannel.Ui,
                "An interaction window opening must use the UI channel");
            AssertResolved(
                palette,
                new BattleAudioCueRequest(BattleAudioCue.Guard),
                guard,
                BattleAudioPlaybackChannel.Sfx,
                "A normal guard must use the SFX channel");
            AssertResolved(
                palette,
                new BattleAudioCueRequest(BattleAudioCue.PerfectGuard),
                perfectGuard,
                BattleAudioPlaybackChannel.Sfx,
                "A perfect guard must use the SFX channel");
            AssertResolved(
                palette,
                new BattleAudioCueRequest(BattleAudioCue.Miss),
                miss,
                BattleAudioPlaybackChannel.Ui,
                "A miss cue must use the UI channel");
            AssertResolved(
                palette,
                new BattleAudioCueRequest(BattleAudioCue.WeakHit),
                weakHit,
                BattleAudioPlaybackChannel.Sfx,
                "A weak-point hit must use the SFX channel");
            AssertResolved(
                palette,
                new BattleAudioCueRequest(BattleAudioCue.InteractiveWindowClose),
                windowClose,
                BattleAudioPlaybackChannel.Ui,
                "An interaction window closing must use the UI channel");

            NullBattleAudioCueSink.Instance.Begin(palette);
            NullBattleAudioCueSink.Instance.Play(
                new BattleAudioCueRequest(BattleAudioCue.BattleBgm));
            NullBattleAudioCueSink.Instance.End();

            bool resolvedMissing = palette.TryResolve(
                new BattleAudioCueRequest(
                    BattleAudioCue.Voice,
                    semanticCueId: "voice_missing"),
                out AudioEventSO missingEvent,
                out _);
            AssertFalse(resolvedMissing,
                "An unconfigured semantic voice ID must resolve to safe silence");
            AssertEqual<AudioEventSO>(null, missingEvent,
                "Safe silence must not return a stale AudioEventSO");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(windowClose);
            UnityEngine.Object.DestroyImmediate(weakHit);
            UnityEngine.Object.DestroyImmediate(miss);
            UnityEngine.Object.DestroyImmediate(perfectGuard);
            UnityEngine.Object.DestroyImmediate(guard);
            UnityEngine.Object.DestroyImmediate(windowOpen);
            UnityEngine.Object.DestroyImmediate(specialWarning);
            UnityEngine.Object.DestroyImmediate(defeat);
            UnityEngine.Object.DestroyImmediate(victory);
            UnityEngine.Object.DestroyImmediate(voice);
            UnityEngine.Object.DestroyImmediate(skillAttack);
            UnityEngine.Object.DestroyImmediate(genericAttack);
            UnityEngine.Object.DestroyImmediate(palette);
        }

        return "PASS DRPG battle audio palette routing";
    }

    private static void AssertResolved(
        DRPGBattleAudioPaletteSO palette,
        BattleAudioCueRequest request,
        AudioEventSO expectedEvent,
        BattleAudioPlaybackChannel expectedChannel,
        string message)
    {
        bool resolved = palette.TryResolve(
            request,
            out AudioEventSO actualEvent,
            out BattleAudioPlaybackChannel actualChannel);
        AssertTrue(resolved, message + " (not resolved)");
        AssertEqual(expectedEvent, actualEvent, message + " (wrong event)");
        AssertEqual(expectedChannel, actualChannel, message + " (wrong channel)");
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, fieldName);
        field.SetValue(target, value);
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }

    private static void AssertFalse(bool value, string message)
    {
        AssertTrue(!value, message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
        {
            throw new Exception(
                $"{message}. Expected={expected}, Actual={actual}");
        }
    }
}
#endif
