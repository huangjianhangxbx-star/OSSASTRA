using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene owner for a node-local party. Mode transitions do not recreate it.
/// </summary>
[DisallowMultipleComponent]
public sealed class DRPGNodePartyRoot : MonoBehaviour
{
    [SerializeField, Min(0f)] private float movementSpeed = 2f;
    [SerializeField] private float minimumX = -100f;
    [SerializeField] private float maximumX = 100f;
    [SerializeField, Min(0.01f)] private float slotSpacing = 1.25f;

    private readonly Dictionary<string, DRPGScenePartyMember> members =
        new Dictionary<string, DRPGScenePartyMember>(StringComparer.Ordinal);
    private DRPGPartyExplorationMotor motor;
    private readonly DRPGNodePartyBattleState battleState =
        new DRPGNodePartyBattleState();

    public DRPGNodePartyFormation Formation { get; private set; }
    public Transform QueueRoot => transform;
    public DRPGPartyExplorationMotor Motor => motor ??=
        new DRPGPartyExplorationMotor(transform, movementSpeed, minimumX, maximumX);
    public bool ExplorationEnabled => Motor.Enabled;
    public DRPGNodePartyBattleState BattleState => battleState;

    private void Awake()
    {
        RebuildFromChildren();
    }

    public bool ExternalMovement { get; set; }

    private void Update()
    {
        if (ExternalMovement || !ExplorationEnabled || Formation == null)
            return;

        var flow = GameFlowRoot.Instance;
        if (flow == null || flow.drpgGameLoop == null ||
            flow.drpgGameLoop.AnkalutePhase != AnkaluteGameLoopPhase.ExpeditionExploration ||
            flow.huntFlow == null || flow.huntFlow.huntFlowDataBus.isPaused)
            return;
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null) return;
        float horizontal = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
            - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
        Motor.Step(horizontal, Time.deltaTime);
    }

    public void ConfigureForRuntime(
        float speed,
        float minX,
        float maxX,
        float spacing = 1.25f)
    {
        movementSpeed = speed;
        minimumX = minX;
        maximumX = maxX;
        slotSpacing = Mathf.Max(0.01f, spacing);
        if (motor == null)
            motor = new DRPGPartyExplorationMotor(transform, speed, minX, maxX);
        else
            motor.Configure(speed, minX, maxX);
        if (Formation != null)
            ApplyFormationSlots();
    }

    public void RebuildFromChildren()
    {
        DRPGScenePartyMember[] found =
            GetComponentsInChildren<DRPGScenePartyMember>(true);
        members.Clear();
        var order = new List<string>(found.Length);
        for (int i = 0; i < found.Length; i++)
        {
            DRPGScenePartyMember member = found[i];
            if (member == null || string.IsNullOrWhiteSpace(member.StableId))
                continue;
            if (!members.TryAdd(member.StableId, member))
                throw new InvalidOperationException(
                    "Duplicate scene party stable id: " + member.StableId);
            order.Add(member.StableId);
        }
        Formation = new DRPGNodePartyFormation(order);
        ApplyFormationSlots();
    }

    public bool TryGetMember(string stableId, out DRPGScenePartyMember member) =>
        members.TryGetValue(stableId ?? string.Empty, out member);

    public bool TrySwap(string firstStableId, string secondStableId)
    {
        if (Formation == null || !Formation.TrySwap(firstStableId, secondStableId))
            return false;
        ApplyFormationSlots();
        return true;
    }

    public void RestoreDefaultFormation()
    {
        if (Formation == null)
            return;
        Formation.RestoreDefault();
        ApplyFormationSlots();
    }

    public bool RemoveMember(string stableId)
    {
        if (Formation == null || !Formation.Remove(stableId))
            return false;
        ApplyFormationSlots();
        return true;
    }

    public void SetExplorationEnabled(bool enabled)
    {
        Motor.Enabled = enabled;
    }

    public void ApplyFormationSlots()
    {
        if (Formation == null)
            return;
        int activeCount = 0;
        for (int i = 0; i < Formation.Count; i++)
        {
            string id = Formation.Order[i];
            if (!battleState.TryGetHitPoints(id, out float hp) || hp > 0f)
                activeCount++;
        }
        float center = (activeCount - 1) * 0.5f;
        int activeIndex = 0;
        for (int i = 0; i < Formation.Count; i++)
        {
            if (!members.TryGetValue(Formation.Order[i], out DRPGScenePartyMember member) ||
                member == null)
            {
                continue;
            }
            bool incapacitated = battleState.TryGetHitPoints(
                Formation.Order[i], out float hp) && hp <= 0f;
            member.SetIncapacitated(incapacitated);
            if (incapacitated)
                continue;
            Vector3 local = member.transform.localPosition;
            local.x = (activeIndex++ - center) * slotSpacing;
            member.transform.localPosition = local;
            member.transform.SetSiblingIndex(i);
        }
    }

    public void CaptureCommittedBattleState(DRPGBattleSession session)
    {
        battleState.Capture(session);
        // The presentation has already committed the rendered formation homes.
        // Capturing state must not replace those homes with a fresh exploration layout.
        foreach (var pair in members)
        {
            if (pair.Value != null)
                pair.Value.SetIncapacitated(battleState.TryGetHitPoints(pair.Key, out float hp) && hp <= 0f);
        }
    }

    private void OnValidate()
    {
        if (maximumX < minimumX)
            maximumX = minimumX;
        if (Application.isPlaying && motor != null)
            motor.Configure(movementSpeed, minimumX, maximumX);
    }
}
