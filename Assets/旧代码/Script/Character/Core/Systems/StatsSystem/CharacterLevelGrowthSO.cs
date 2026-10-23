using UnityEngine;

[CreateAssetMenu(fileName = "CharacterLevelGrowth", menuName = "ScriptableObject/角色/角色等级成长")]
public class CharacterLevelGrowthSO : ScriptableObject
{
    public StatSheet growthPerLevel = new StatSheet();

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (growthPerLevel == null)
            growthPerLevel = new StatSheet();

        growthPerLevel.EnsureCompleteAndUnique();

        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}