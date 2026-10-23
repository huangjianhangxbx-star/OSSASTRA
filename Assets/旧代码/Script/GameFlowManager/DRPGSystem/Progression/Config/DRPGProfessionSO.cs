using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "DRPGProfession",
    menuName = "ANKALUTE/DRPG/Progression/Profession")]
public sealed class DRPGProfessionSO : ScriptableObject
{
    [SerializeField] private string professionId = "profession";
    [SerializeField] private string displayName = "Profession";
    [TextArea]
    [SerializeField] private string description;
    [SerializeField] private Sprite icon;
    [SerializeField] private Color themeColor = new Color(0.25f, 0.9f, 0.95f, 1f);
    [SerializeField] private DRPGSkillSO basicAttack;
    [SerializeField] private DRPGSkillTreeSO skillTree;
    [SerializeField] private List<string> grantedNodeIds = new List<string>();
    [SerializeField] private List<DRPGSkillSO> initialLoadout =
        new List<DRPGSkillSO>();

    public string ProfessionId => professionId;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public Color ThemeColor => themeColor;
    public DRPGSkillSO BasicAttack => basicAttack;
    public DRPGSkillTreeSO SkillTree => skillTree;
    public IReadOnlyList<string> GrantedNodeIds =>
        grantedNodeIds ?? (IReadOnlyList<string>)Array.Empty<string>();
    public IReadOnlyList<DRPGSkillSO> InitialLoadout =>
        initialLoadout ?? (IReadOnlyList<DRPGSkillSO>)Array.Empty<DRPGSkillSO>();
}
