using UnityEngine;
using UnityEngine.Rendering;

public static class GameRenderCharacterGlobals
{
    private static readonly int InkId = Shader.PropertyToID("_GRGlobalCharacterInk");
    private static readonly int OutlineId = Shader.PropertyToID("_GRGlobalCharacterOutline");
    private static readonly int HighlightId = Shader.PropertyToID("_GRGlobalCharacterHighlight");
    private static readonly int EnabledId = Shader.PropertyToID("_GRGlobalStyleEnabled");

    public static void Apply(GameRenderProfile profile, bool active)
    {
        Shader.SetGlobalFloat(EnabledId, active && profile != null ? 1f : 0f);
        Shader.SetGlobalFloat(InkId, active && profile != null ?
            GameRenderParameters.Clamp(profile.characterInkStrength, 0f, 1f) : 0f);
        Shader.SetGlobalFloat(OutlineId, active && profile != null ?
            GameRenderParameters.Clamp(profile.characterOutlineStrength, 0f, 1f) : 0f);
        Shader.SetGlobalFloat(HighlightId, active && profile != null ?
            GameRenderParameters.Clamp(profile.characterHighlightLift, 0f, 1f) : 0f);
    }

    internal static void Write(CommandBuffer cmd, GameRenderProfile profile, bool active)
    {
        cmd.SetGlobalFloat(EnabledId, active && profile != null ? 1f : 0f);
        cmd.SetGlobalFloat(InkId, active && profile != null ?
            GameRenderParameters.Clamp(profile.characterInkStrength, 0f, 1f) : 0f);
        cmd.SetGlobalFloat(OutlineId, active && profile != null ?
            GameRenderParameters.Clamp(profile.characterOutlineStrength, 0f, 1f) : 0f);
        cmd.SetGlobalFloat(HighlightId, active && profile != null ?
            GameRenderParameters.Clamp(profile.characterHighlightLift, 0f, 1f) : 0f);
    }
}
