using UnityEngine;

[CreateAssetMenu(
    fileName = "BattleSkillPresentationRecipe",
    menuName = "ANKALUTE/DRPG/Skill Presentation Recipe")]
public sealed class BattleSkillPresentationRecipeSO : ScriptableObject
{
    [SerializeField] private string presentationKey = string.Empty;
    [SerializeField] private BattleSkillPresentationRecipeSO parent;
    [SerializeField] private BattleHudPresentationModule hud = new BattleHudPresentationModule();
    [SerializeField] private BattleStagePresentationModule stage = new BattleStagePresentationModule();
    [SerializeField] private BattleCameraPresentationModule camera = new BattleCameraPresentationModule();
    [SerializeField] private BattleActorPresentationModule attacker = new BattleActorPresentationModule();
    [SerializeField] private BattleTargetPresentationModule target = new BattleTargetPresentationModule();
    [SerializeField] private BattleBackgroundPresentationModule background = new BattleBackgroundPresentationModule();
    [SerializeField] private BattleSpinePresentationModule spine = new BattleSpinePresentationModule();
    [SerializeField] private BattleFeedbackPresentationModule feedback = new BattleFeedbackPresentationModule();
    [SerializeField] private BattleExitPresentationModule exit = new BattleExitPresentationModule();

    public string PresentationKey => presentationKey ?? string.Empty;
    public BattleSkillPresentationRecipeSO Parent => parent;
    public BattleHudPresentationModule Hud => hud;
    public BattleStagePresentationModule Stage => stage;
    public BattleCameraPresentationModule Camera => camera;
    public BattleActorPresentationModule Attacker => attacker;
    public BattleTargetPresentationModule Target => target;
    public BattleBackgroundPresentationModule Background => background;
    public BattleSpinePresentationModule Spine => spine;
    public BattleFeedbackPresentationModule Feedback => feedback;
    public BattleExitPresentationModule Exit => exit;
}
