using UnityEngine;

[CreateAssetMenu(fileName = "CharacterBaseStats",menuName = "ScriptableObject/角色/角色基础数值")]
public class CharacterBaseStatsSO : ScriptableObject
{
    public StatSheet baseStats = new StatSheet();

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (baseStats == null)
            baseStats = new StatSheet();

        baseStats.EnsureCompleteAndUnique();

        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
