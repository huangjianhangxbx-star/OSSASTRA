public sealed class ResolvedBattlePresentationModule<TDefinition>
    where TDefinition : class
{
    public ResolvedBattlePresentationModule(
        BattlePresentationOverrideMode requestedMode,
        bool enabled,
        TDefinition definition)
    {
        RequestedMode = requestedMode;
        Enabled = enabled;
        Definition = definition;
    }

    public BattlePresentationOverrideMode RequestedMode { get; }
    public bool Enabled { get; }
    public TDefinition Definition { get; }
}

public sealed class ResolvedBattleSkillPresentation
{
    public ResolvedBattleSkillPresentation(
        string presentationKey,
        ResolvedBattlePresentationModule<BattleHudPresentationDefinition> hud,
        ResolvedBattlePresentationModule<BattleStagePresentationDefinition> stage,
        ResolvedBattlePresentationModule<BattleCameraPresentationDefinition> camera,
        ResolvedBattlePresentationModule<BattleActorPresentationDefinition> attacker,
        ResolvedBattlePresentationModule<BattleTargetPresentationDefinition> target,
        ResolvedBattlePresentationModule<BattleBackgroundPresentationDefinition> background,
        ResolvedBattlePresentationModule<BattleSpinePresentationDefinition> spine,
        ResolvedBattlePresentationModule<BattleFeedbackPresentationDefinition> feedback,
        ResolvedBattlePresentationModule<BattleExitPresentationDefinition> exit)
    {
        PresentationKey = presentationKey ?? string.Empty;
        Hud = hud;
        Stage = stage;
        Camera = camera;
        Attacker = attacker;
        Target = target;
        Background = background;
        Spine = spine;
        Feedback = feedback;
        Exit = exit;
    }

    public string PresentationKey { get; }
    public ResolvedBattlePresentationModule<BattleHudPresentationDefinition> Hud { get; }
    public ResolvedBattlePresentationModule<BattleStagePresentationDefinition> Stage { get; }
    public ResolvedBattlePresentationModule<BattleCameraPresentationDefinition> Camera { get; }
    public ResolvedBattlePresentationModule<BattleActorPresentationDefinition> Attacker { get; }
    public ResolvedBattlePresentationModule<BattleTargetPresentationDefinition> Target { get; }
    public ResolvedBattlePresentationModule<BattleBackgroundPresentationDefinition> Background { get; }
    public ResolvedBattlePresentationModule<BattleSpinePresentationDefinition> Spine { get; }
    public ResolvedBattlePresentationModule<BattleFeedbackPresentationDefinition> Feedback { get; }
    public ResolvedBattlePresentationModule<BattleExitPresentationDefinition> Exit { get; }
}
