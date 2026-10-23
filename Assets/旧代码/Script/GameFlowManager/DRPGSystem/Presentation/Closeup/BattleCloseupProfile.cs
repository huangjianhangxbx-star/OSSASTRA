using System;

internal readonly struct BattleCloseupProfile
{
    internal BattleCloseupProfile(float cameraMagnification, float participantScale,
        float downwardBattleFraction, float spectatorDim, float blurStrength)
    {
        if (!Finite(cameraMagnification) || cameraMagnification < 1f)
            throw new ArgumentOutOfRangeException(nameof(cameraMagnification));
        if (!Finite(participantScale) || participantScale < 1f)
            throw new ArgumentOutOfRangeException(nameof(participantScale));
        if (!Finite(downwardBattleFraction) || downwardBattleFraction < 0f || downwardBattleFraction > .2f)
            throw new ArgumentOutOfRangeException(nameof(downwardBattleFraction));
        if (!Finite(spectatorDim) || spectatorDim < 0f || spectatorDim > 1f)
            throw new ArgumentOutOfRangeException(nameof(spectatorDim));
        if (!Finite(blurStrength) || blurStrength < 0f || blurStrength > 1f)
            throw new ArgumentOutOfRangeException(nameof(blurStrength));
        CameraMagnification = cameraMagnification;
        ParticipantScale = participantScale;
        DownwardBattleFraction = downwardBattleFraction;
        SpectatorDim = spectatorDim;
        BlurStrength = blurStrength;
    }

    internal static BattleCloseupProfile ApprovedDefaults =>
        new BattleCloseupProfile(1.15f, 1.08f, .03f, .42f, .58f);

    internal float CameraMagnification { get; }
    internal float ParticipantScale { get; }
    internal float DownwardBattleFraction { get; }
    internal float SpectatorDim { get; }
    internal float BlurStrength { get; }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
