using UnityEngine;

[CreateAssetMenu(
    fileName = "BattleEntryPresentationRecipe",
    menuName = "ANKALUTE/DRPG/Presentation/Battle Entry Recipe")]
public sealed class BattleEntryPresentationRecipeSO : ScriptableObject
{
    [SerializeField] private BattleCameraShotDefinition spawnShot;
    [SerializeField] private BattleCameraShotDefinition readyShot;
    [SerializeField, Min(0f)] private float spawnDuration = 0.65f;
    [SerializeField, Min(0f)] private float holdDuration = 0.2f;
    [SerializeField, Min(0f)] private float hudRevealDuration = 0.24f;
    [SerializeField, Range(0.01f, 1f)] private float spawnStartScale = 0.72f;

    public BattleCameraShotDefinition SpawnShot => spawnShot;
    public BattleCameraShotDefinition ReadyShot => readyShot;
    public float SpawnDuration => Mathf.Max(0f, spawnDuration);
    public float HoldDuration => Mathf.Max(0f, holdDuration);
    public float HudRevealDuration => Mathf.Max(0f, hudRevealDuration);
    public float SpawnStartScale => Mathf.Clamp(spawnStartScale, 0.01f, 1f);

    public void Configure(
        BattleCameraShotDefinition initialShot,
        BattleCameraShotDefinition settledShot,
        float actorSpawnSeconds,
        float holdSeconds,
        float revealSeconds,
        float initialScale)
    {
        spawnShot = initialShot;
        readyShot = settledShot;
        spawnDuration = Mathf.Max(0f, actorSpawnSeconds);
        holdDuration = Mathf.Max(0f, holdSeconds);
        hudRevealDuration = Mathf.Max(0f, revealSeconds);
        spawnStartScale = Mathf.Clamp(initialScale, 0.01f, 1f);
    }
}
