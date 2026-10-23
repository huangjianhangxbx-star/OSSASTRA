using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns progression state for the current game session. Save/load can replace
/// the in-memory service later without exposing mutable state to UI or battle.
/// </summary>
public sealed class DRPGProgressionRoot : MonoBehaviour
{
    [SerializeField] private List<DRPGCombatantProfileSO> profileRegistry =
        new List<DRPGCombatantProfileSO>();
    [SerializeField] private DRPGPartySetupSO partySetup;
    [SerializeField] private DRPGResonanceDefinitionSO resonanceDefinition;

    public DRPGProgressionService Service { get; private set; }
    public DRPGResonanceService ResonanceService { get; private set; }
    public DRPGPartyCompositionState PartyComposition { get; private set; }
    public IReadOnlyList<DRPGCombatantProfileSO> ProfileRegistry => profileRegistry;
    public DRPGPartySetupSO PartySetup => partySetup;
    public DRPGResonanceDefinitionSO ResonanceDefinition => resonanceDefinition;

    public void Initialize()
    {
        if (Service != null)
            return;
        if (partySetup != null)
            PartyComposition = DRPGPartyCompositionState.FromSetup(partySetup);
        if (resonanceDefinition != null)
            ResonanceService = new DRPGResonanceService(resonanceDefinition);
        Service = new DRPGProgressionService(ResonanceService);
    }

    private void Awake()
    {
        Initialize();
    }

    public DRPGProgressionSaveData ExportSnapshot()
    {
        Initialize();
        return Service.CaptureSaveData(profileRegistry);
    }

    public DRPGProgressionOperationResult ImportSnapshot(
        DRPGProgressionSaveData snapshot)
    {
        Initialize();
        return Service.RestoreSaveData(
            profileRegistry,
            snapshot,
            PartyComposition);
    }

    public DRPGProgressionSaveData ExportSnapshot(
        IEnumerable<DRPGCombatantProfileSO> profiles)
    {
        Initialize();
        return Service.CaptureSaveData(profiles);
    }

    public DRPGProgressionOperationResult ImportSnapshot(
        IEnumerable<DRPGCombatantProfileSO> profiles,
        DRPGProgressionSaveData snapshot)
    {
        Initialize();
        return Service.RestoreSaveData(
            profiles,
            snapshot,
            PartyComposition);
    }
}
