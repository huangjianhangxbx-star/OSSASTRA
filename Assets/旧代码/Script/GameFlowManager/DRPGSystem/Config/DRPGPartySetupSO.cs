using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家队伍配置。主角固定在顺序 0，并与普通队友一样始终驻场。
/// </summary>

[CreateAssetMenu(
    fileName = "DRPGPartySetup",
    menuName = "ANKALUTE/DRPG/Party Setup")]
public sealed class DRPGPartySetupSO : ScriptableObject
{
    [SerializeField] private DRPGCombatantProfileSO protagonist;
    [SerializeField] private List<DRPGCombatantProfileSO> partyMembers =
        new List<DRPGCombatantProfileSO>();

    public DRPGCombatantProfileSO Protagonist => protagonist;
    public IReadOnlyList<DRPGCombatantProfileSO> PartyMembers => partyMembers;
}
