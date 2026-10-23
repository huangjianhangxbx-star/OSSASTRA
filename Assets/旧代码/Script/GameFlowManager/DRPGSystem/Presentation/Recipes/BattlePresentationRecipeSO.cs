using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "BattlePresentationRecipe",
    menuName = "ANKALUTE/DRPG/Presentation Recipe")]
public sealed class BattlePresentationRecipeSO : ScriptableObject
{
    [SerializeField] private string recipeId = "recipe";
    [SerializeField] private BattlePresentationMoment moment;
    [SerializeField] private BattlePresentationGateKind gateKind;
    [Min(0.1f)]
    [SerializeField] private float maximumGateSeconds = 5f;
    [SerializeField] private List<BattlePresentationBeat> beats =
        new List<BattlePresentationBeat>();

    public string RecipeId => string.IsNullOrWhiteSpace(recipeId) ? name : recipeId;
    public BattlePresentationMoment Moment => moment;
    public BattlePresentationGateKind GateKind => gateKind;
    public float MaximumGateSeconds => maximumGateSeconds;
    public IReadOnlyList<BattlePresentationBeat> Beats => beats;
}
