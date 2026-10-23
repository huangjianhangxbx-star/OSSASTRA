using UnityEngine;

[CreateAssetMenu(fileName = "CharacterStatProfile",menuName = "ScriptableObject/角色/角色数值档案")]
public class CharacterStatProfileSO : ScriptableObject
{
    public CharacterBaseStatsSO baseStats;
    public CharacterLevelGrowthSO levelGrowth;
}