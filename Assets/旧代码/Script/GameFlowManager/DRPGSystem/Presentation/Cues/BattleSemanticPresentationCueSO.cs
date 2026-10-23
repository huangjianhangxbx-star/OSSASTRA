using UnityEngine;

public enum BattleSemanticPresentationModuleKind
{
    Hud = 0,
    Stage = 1,
    Camera = 2,
    Attacker = 3,
    Target = 4,
    Background = 5,
    Spine = 6,
    Feedback = 7,
    Exit = 8
}

public sealed class BattleSemanticPresentationCueSO : BattlePresentationCueSO
{
    private BattleSemanticPresentationModuleKind moduleKind;
    private object definition;

    public override BattlePresentationCueKind Kind =>
        BattlePresentationCueKind.SemanticModule;
    public BattleSemanticPresentationModuleKind ModuleKind => moduleKind;

    public TDefinition GetDefinition<TDefinition>() where TDefinition : class
    {
        return definition as TDefinition;
    }

    internal static BattleSemanticPresentationCueSO Create(
        BattleSemanticPresentationModuleKind kind,
        object definition)
    {
        BattleSemanticPresentationCueSO cue =
            CreateInstance<BattleSemanticPresentationCueSO>();
        cue.name = "SemanticCue_" + kind;
        cue.hideFlags = HideFlags.HideAndDontSave;
        cue.moduleKind = kind;
        cue.definition = definition;
        return cue;
    }
}
