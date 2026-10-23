#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class GalSequencePlayerProbe
{
    public static string Run()
    {
        RejectsMissingContent();
        ResolvesLinesAndAdvancesExactlyOnce();
        ResolvesBackgroundPriority();
        ResolvesAutoAdvanceOverride();
        ClampsAndDefaultsInvalidAutoAdvanceOverrides();
        return "PASS gal sequence player";
    }

    private static void RejectsMissingContent()
    {
        var player = new GalSequencePlayer();

        AssertFalse(
            player.TryBegin(null, out _),
            "A null sequence must be rejected");

        GalSequenceSO empty = ScriptableObject.CreateInstance<GalSequenceSO>();
        try
        {
            SetPrivateField(empty, "lines", new List<GalLine>());
            AssertFalse(
                player.TryBegin(empty, out _),
                "An empty sequence must be rejected");
            AssertFalse(
                player.HasActiveSequence,
                "Rejected content must not leave an active playback session");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(empty);
        }
    }

    private static void ResolvesLinesAndAdvancesExactlyOnce()
    {
        Texture2D texture = new Texture2D(4, 4);
        Sprite defaultPortrait = Sprite.Create(
            texture,
            new Rect(0f, 0f, 4f, 4f),
            new Vector2(0.5f, 0f));
        Sprite variantPortrait = Sprite.Create(
            texture,
            new Rect(0f, 0f, 4f, 4f),
            new Vector2(0.5f, 0f));
        Sprite overridePortrait = Sprite.Create(
            texture,
            new Rect(0f, 0f, 4f, 4f),
            new Vector2(0.5f, 0f));
        GalActorSO actor = ScriptableObject.CreateInstance<GalActorSO>();
        GalSequenceSO sequence = ScriptableObject.CreateInstance<GalSequenceSO>();

        try
        {
            SetPrivateField(actor, "displayName", "临时角色");
            SetPrivateField(actor, "defaultPortrait", defaultPortrait);
            SetPrivateField(
                actor,
                "portraitVariants",
                new List<GalPortraitVariant>
                {
                    new GalPortraitVariant("alert", variantPortrait)
                });

            SetPrivateField(
                sequence,
                "lines",
                new List<GalLine>
                {
                    new GalLine(actor, "第一句", "alert"),
                    new GalLine(
                        actor,
                        "第二句",
                        portraitOverride: overridePortrait,
                        speakerNameOverride: "覆盖名称",
                        portraitSlot: GalPortraitSlot.Right),
                    new GalLine(null, "旁白")
                });

            var player = new GalSequencePlayer();

            AssertTrue(
                player.TryBegin(sequence, out GalLineViewModel first),
                "A sequence containing lines must start");
            AssertEqual(0, first.LineIndex, "The first line index must be zero");
            AssertEqual("临时角色", first.SpeakerName, "Actor display name must be used");
            AssertSame(
                variantPortrait,
                first.Portrait,
                "A matching actor variant must beat the actor default portrait");

            AssertTrue(
                player.TryAdvance(out GalLineViewModel second),
                "The second line must be available");
            AssertEqual(1, second.LineIndex, "Advance must increment exactly once");
            AssertEqual("覆盖名称", second.SpeakerName, "Line speaker override must win");
            AssertSame(
                overridePortrait,
                second.Portrait,
                "Line portrait override must beat actor variants and defaults");
            AssertEqual(
                GalPortraitSlot.Right,
                second.PortraitSlot,
                "The requested portrait slot must be preserved");

            AssertTrue(
                player.TryAdvance(out GalLineViewModel third),
                "The final line must be available");
            AssertEqual("旁白", third.Text, "Line text must be preserved");
            AssertEqual(string.Empty, third.SpeakerName, "A line without an actor may be narration");
            AssertSame(null, third.Portrait, "A missing portrait must remain valid");

            AssertFalse(
                player.TryAdvance(out _),
                "Advancing past the final line must finish playback");
            AssertFalse(
                player.HasActiveSequence,
                "Playback must become inactive after the final line");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sequence);
            UnityEngine.Object.DestroyImmediate(actor);
            UnityEngine.Object.DestroyImmediate(defaultPortrait);
            UnityEngine.Object.DestroyImmediate(variantPortrait);
            UnityEngine.Object.DestroyImmediate(overridePortrait);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private static void ResolvesBackgroundPriority()
    {
        Texture2D texture = new Texture2D(8, 8);
        Sprite sequenceBackground = Sprite.Create(
            texture,
            new Rect(0f, 0f, 8f, 8f),
            new Vector2(0.5f, 0.5f));
        Sprite lineBackground = Sprite.Create(
            texture,
            new Rect(0f, 0f, 8f, 8f),
            new Vector2(0.5f, 0.5f));
        GalSequenceSO sequence = ScriptableObject.CreateInstance<GalSequenceSO>();
        GalSequenceSO noBackgroundSequence =
            ScriptableObject.CreateInstance<GalSequenceSO>();

        try
        {
            SetPrivateField(sequence, "defaultBackground", sequenceBackground);
            SetPrivateField(
                sequence,
                "lines",
                new List<GalLine>
                {
                    new GalLine(
                        null,
                        "使用行覆盖",
                        backgroundOverride: lineBackground),
                    new GalLine(null, "使用序列默认")
                });
            SetPrivateField(
                noBackgroundSequence,
                "lines",
                new List<GalLine> { new GalLine(null, "允许空背景") });

            var player = new GalSequencePlayer();
            AssertTrue(
                player.TryBegin(sequence, out GalLineViewModel first),
                "Background sequence must start");
            AssertSame(
                lineBackground,
                first.Background,
                "Line background must beat the sequence default");
            AssertTrue(
                player.TryAdvance(out GalLineViewModel second),
                "The default-background line must be available");
            AssertSame(
                sequenceBackground,
                second.Background,
                "Sequence default must be used without a line override");

            AssertTrue(
                player.TryBegin(noBackgroundSequence, out GalLineViewModel empty),
                "A missing background must not reject dialogue");
            AssertSame(
                null,
                empty.Background,
                "Missing line and sequence backgrounds must remain null");
            AssertEqual(
                "允许空背景",
                empty.Text,
                "Missing background must not discard dialogue text");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(noBackgroundSequence);
            UnityEngine.Object.DestroyImmediate(sequence);
            UnityEngine.Object.DestroyImmediate(lineBackground);
            UnityEngine.Object.DestroyImmediate(sequenceBackground);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private static void ResolvesAutoAdvanceOverride()
    {
        GalSequenceSO sequence = ScriptableObject.CreateInstance<GalSequenceSO>();

        try
        {
            SetPrivateField(
                sequence,
                "lines",
                new List<GalLine>
                {
                    new GalLine(
                        null,
                        "使用覆盖时长",
                        autoAdvanceSecondsOverride: 2.75f),
                    new GalLine(null, "使用默认时长")
                });

            var player = new GalSequencePlayer();
            AssertTrue(
                player.TryBegin(sequence, out GalLineViewModel overridden),
                "A line with an auto-advance override must start");
            AssertApproximately(
                2.75f,
                overridden.AutoAdvanceSecondsOverride,
                "The line auto-advance override must reach the view model");

            AssertTrue(
                player.TryAdvance(out GalLineViewModel defaulted),
                "The default auto-advance line must be available");
            AssertApproximately(
                0f,
                defaulted.AutoAdvanceSecondsOverride,
                "A line without an override must resolve to zero");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sequence);
        }
    }

    private static void ClampsAndDefaultsInvalidAutoAdvanceOverrides()
    {
        GalSequenceSO sequence = ScriptableObject.CreateInstance<GalSequenceSO>();

        try
        {
            SetPrivateField(
                sequence,
                "lines",
                new List<GalLine>
                {
                    new GalLine(
                        null,
                        "负数覆盖应被夹到零",
                        autoAdvanceSecondsOverride: -3f),
                    null
                });

            var player = new GalSequencePlayer();
            AssertTrue(
                player.TryBegin(sequence, out GalLineViewModel clamped),
                "A line with a negative override must still start");
            AssertApproximately(
                0f,
                clamped.AutoAdvanceSecondsOverride,
                "A negative line override must clamp to zero");

            AssertTrue(
                player.TryAdvance(out GalLineViewModel missingLine),
                "A null line entry must use the safe fallback view model");
            AssertApproximately(
                0f,
                missingLine.AutoAdvanceSecondsOverride,
                "A null line entry must default its override to zero");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sequence);
        }
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        if (field == null)
            throw new Exception($"Missing test setup field: {fieldName}");

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

    private static void AssertSame(object expected, object actual, string message)
    {
        if (!ReferenceEquals(expected, actual))
            throw new Exception(message);
    }

    private static void AssertApproximately(
        float expected,
        float actual,
        string message)
    {
        if (float.IsNaN(actual) ||
            float.IsInfinity(actual) ||
            Math.Abs(expected - actual) > 0.0001f)
        {
            throw new Exception(
                $"{message}. Expected={expected}, Actual={actual}");
        }
    }
}
#endif
