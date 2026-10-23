using UnityEngine;

[DisallowMultipleComponent]
public sealed class DRPGEncounterPoint : MonoBehaviour
{
    [SerializeField] private string pointId = "encounter-1";
    [SerializeField] private DRPGNodeBattleCoordinator coordinator;
    [SerializeField] private DRPGEncounterDefinitionSO encounter;
    [SerializeField, Min(.05f)] private float triggerHalfWidth = .75f;

    private readonly DRPGEncounterPointState state =
        new DRPGEncounterPointState();
    private readonly DRPGEncounterPointTraversal traversal =
        new DRPGEncounterPointTraversal();

    public string PointId => pointId;
    public bool Completed => state.Completed;

    public void ConfigureForRuntime(
        string id,
        DRPGNodeBattleCoordinator battleCoordinator,
        DRPGEncounterDefinitionSO encounterDefinition,
        float halfWidth)
    {
        pointId = string.IsNullOrWhiteSpace(id) ? "encounter" : id.Trim();
        coordinator = battleCoordinator;
        encounter = encounterDefinition;
        triggerHalfWidth = Mathf.Max(.05f, halfWidth);
    }

    private void OnEnable()
    {
        if (coordinator == null)
            coordinator = GetComponentInParent<DRPGNodeBattleCoordinator>();
        if (coordinator != null)
            coordinator.BattleFinished += HandleBattleFinished;
    }

    private void Update()
    {
        DRPGNodePartyRoot party = coordinator != null
            ? coordinator.Context?.PartyRoot
            : null;
        if (party == null || !party.ExplorationEnabled)
            return;

        DRPGEncounterPointSample sample = traversal.Sample(
            party.QueueRoot.position.x,
            transform.position.x,
            Mathf.Max(.05f, triggerHalfWidth));
        if (sample.Left)
            state.Leave();
        if (sample.ShouldEnter)
            TryStartFor(party);
    }

    private void OnDisable()
    {
        if (coordinator != null)
            coordinator.BattleFinished -= HandleBattleFinished;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        DRPGNodePartyRoot party = coordinator != null
            ? coordinator.Context?.PartyRoot
            : null;
        if (other == null || party == null || other.transform != party.QueueRoot)
            return;
        TryStartFor(party);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        DRPGNodePartyRoot party = coordinator != null
            ? coordinator.Context?.PartyRoot
            : null;
        if (other != null && party != null && other.transform == party.QueueRoot)
            state.Leave();
    }

    private void HandleBattleFinished(DRPGBattleCompletionContext completion)
    {
        state.Finish(completion.Result.Outcome == DRPGBattleOutcome.Victory);
    }

    private void TryStartFor(DRPGNodePartyRoot party)
    {
        if (party == null || coordinator == null ||
            !ReferenceEquals(party, coordinator.Context?.PartyRoot))
        {
            return;
        }
        bool valid = coordinator.Context != null &&
            coordinator.Context.TryValidate(out _);
        if (!state.TryEnter(valid))
            return;
        DRPGBattleStartResult result = coordinator.TryStart(encounter);
        if (result != DRPGBattleStartResult.Started)
            state.Finish(false);
    }

    private void OnValidate()
    {
        triggerHalfWidth = Mathf.Max(.05f, triggerHalfWidth);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Completed
            ? new Color(.2f, .45f, .25f, .55f)
            : new Color(.75f, .2f, .28f, .65f);
        Gizmos.DrawWireCube(
            transform.position,
            new Vector3(Mathf.Max(.1f, triggerHalfWidth * 2f), 6f, .1f));
    }
}
