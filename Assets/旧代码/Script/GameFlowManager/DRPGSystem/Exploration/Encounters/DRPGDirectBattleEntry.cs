using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DRPGDirectBattleEntry : MonoBehaviour
{
    [SerializeField] private DRPGNodeBattleCoordinator coordinator;
    [SerializeField] private DRPGEncounterDefinitionSO encounter;
    [SerializeField] private TMP_Text completionMessage;
    [SerializeField] private bool beginOnStart = true;

    private bool started;

    private void OnEnable()
    {
        if (coordinator == null)
            coordinator = GetComponentInParent<DRPGNodeBattleCoordinator>();
        if (coordinator != null)
            coordinator.BattleFinished += HandleBattleFinished;
    }

    private void Start()
    {
        if (beginOnStart)
            Begin();
    }

    private void OnDisable()
    {
        if (coordinator != null)
            coordinator.BattleFinished -= HandleBattleFinished;
    }

    public bool Begin()
    {
        if (started || coordinator == null ||
            coordinator.Context == null ||
            coordinator.Context.Mode != DRPGNodeBattleMode.DirectBattle)
        {
            return false;
        }
        started = coordinator.TryStart(encounter) ==
            DRPGBattleStartResult.Started;
        if (!started && completionMessage != null)
            completionMessage.text = coordinator.LastStartError;
        return started;
    }

    private void HandleBattleFinished(DRPGBattleCompletionContext completion)
    {
        if (completionMessage != null)
        {
            completionMessage.text = completion.Result.Outcome ==
                DRPGBattleOutcome.Victory
                    ? "战斗完成"
                    : "战斗未完成";
        }
    }
}
