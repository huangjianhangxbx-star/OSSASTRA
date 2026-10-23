#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;

public static class GameAudioRootChannelProbe
{
    public static string Run()
    {
        GameObject rootObject = new GameObject("AudioRootProbe");
        AudioClip clip = AudioClip.Create("probe_clip", 4410, 1, 44100, false);
        AudioEventSO audioEvent = ScriptableObject.CreateInstance<AudioEventSO>();

        try
        {
            SetField(audioEvent, "clips", new[] { clip });
            GameAudioRoot root = rootObject.AddComponent<GameAudioRoot>();
            SetField(root, "initialPoolSize", 1);
            root.Initialize(null);

            AssertFalse(root.PlayMusic(null, 0f),
                "Missing music must be rejected safely");
            AssertTrue(root.PlayMusic(audioEvent, 0f),
                "A configured music event must be accepted");
            AssertEqual(clip, root.CurrentMusicClip,
                "The music channel must expose the active clip for diagnostics");

            AssertTrue(root.PlayVoice(audioEvent),
                "A configured voice event must be accepted");
            AssertEqual(clip, root.CurrentVoiceClip,
                "The voice channel must replace and expose the current line");

            AssertFalse(root.PlayUi(null),
                "Missing UI audio must be rejected safely");

            root.StopMusic(0f);
            AssertEqual<AudioClip>(null, root.CurrentMusicClip,
                "An immediate stop must clear the active music clip");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(audioEvent);
            UnityEngine.Object.DestroyImmediate(clip);
            UnityEngine.Object.DestroyImmediate(rootObject);
        }

        BattleEndOnInactiveAudioHostClearsMusicWithoutCoroutine();
        BattleEndOutsidePlayModeClearsMusicImmediately();
        EditorReinitializationReusesTransientSources();
        return "PASS GameAudioRoot channel playback";
    }

    public static void EditorReinitializationReusesTransientSources()
    {
        AssertFalse(Application.isPlaying,
            "Run the editor persistence regression in EditMode");
        var host = new GameObject("AudioEditorReinitializationProbe");
        try
        {
            var first = host.AddComponent<GameAudioRoot>();
            SetField(first, "initialPoolSize", 2);
            first.Initialize(null);
            AssertEqual(5, host.transform.childCount,
                "The fixture must contain two pooled and three dedicated sources");

            UnityEngine.Object.DestroyImmediate(first);
            var reconstructed = host.AddComponent<GameAudioRoot>();
            SetField(reconstructed, "initialPoolSize", 2);
            reconstructed.Initialize(null);

            AssertEqual(5, host.transform.childCount,
                "Editor/domain reinitialization must reuse its five owned sources instead of duplicating them");
            foreach (Transform child in host.transform)
            {
                AssertTrue((child.gameObject.hideFlags & HideFlags.DontSaveInEditor) != 0,
                    "Runtime-created audio sources must not be serialized into an editor scene");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    public static void BattleEndOnInactiveAudioHostClearsMusicWithoutCoroutine()
    {
        VerifyBattleEnd(true);
    }

    public static void BattleEndOutsidePlayModeClearsMusicImmediately()
    {
        AssertFalse(Application.isPlaying, "Run this exit-to-editor regression in EditMode");
        VerifyBattleEnd(false);
    }

    private static void VerifyBattleEnd(bool disableParent)
    {
        var parent = new GameObject("InactiveAudioHostProbe");
        var child = new GameObject("AudioHost");
        child.transform.SetParent(parent.transform, false);
        var clip = AudioClip.Create("shutdown_music", 4410, 1, 44100, false);
        var audioEvent = ScriptableObject.CreateInstance<AudioEventSO>();
        int errors = 0;
        Application.LogCallback capture = (message, stack, type) =>
        { if (type == LogType.Error || type == LogType.Exception) errors++; };
        try
        {
            SetField(audioEvent, "clips", new[] { clip });
            var root = child.AddComponent<GameAudioRoot>();
            SetField(root, "initialPoolSize", 0);
            root.Initialize(null);
            var sink = child.AddComponent<DRPGBattleAudioCueSink>();
            SetField(sink, "audioRoot", root);
            AssertTrue(root.PlayMusic(audioEvent, 0), "Fixture must load actual music");
            if (disableParent) parent.SetActive(false);
            Application.logMessageReceived += capture;
            sink.End();
            sink.End();
            AssertEqual(0, errors, "Repeated battle shutdown must not start a coroutine on an inactive audio host");
            AssertEqual<AudioClip>(null, root.CurrentMusicClip, "Inactive shutdown must immediately clear music");
            foreach (var source in child.GetComponentsInChildren<AudioSource>(true))
                AssertEqual<AudioClip>(null, source.clip, "Both crossfade channels must be cleared");
        }
        finally
        {
            Application.logMessageReceived -= capture;
            UnityEngine.Object.DestroyImmediate(parent);
            UnityEngine.Object.DestroyImmediate(audioEvent);
            UnityEngine.Object.DestroyImmediate(clip);
        }
    }

    public static void BattleEndAfterQuitSignalDoesNotRestartFade()
    {
        AssertTrue(Application.isPlaying, "Run the quit lifecycle regression in PlayMode");
        var host = new GameObject("AudioQuitProbe");
        var clip = AudioClip.Create("quit_music", 4410, 1, 44100, false);
        var audioEvent = ScriptableObject.CreateInstance<AudioEventSO>();
        try
        {
            SetField(audioEvent, "clips", new[] { clip });
            var root = host.AddComponent<GameAudioRoot>();
            SetField(root, "initialPoolSize", 0);
            root.Initialize(null);
            var sink = host.AddComponent<DRPGBattleAudioCueSink>();
            SetField(sink, "audioRoot", root);
            AssertTrue(root.PlayMusic(audioEvent, 0), "Fixture must play music before quit");
            host.SendMessage("OnApplicationQuit", SendMessageOptions.DontRequireReceiver);
            sink.End();
            sink.End();
            AssertEqual<AudioClip>(null, root.CurrentMusicClip,
                "Quit then presentation shutdown must clear music synchronously, not restart a fade");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(audioEvent);
            UnityEngine.Object.DestroyImmediate(clip);
        }
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
            throw new Exception($"{message}. Expected={expected}, Actual={actual}");
    }
}
#endif
