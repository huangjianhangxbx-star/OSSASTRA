using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu( fileName = "MeleeComboAction",menuName = "ScriptableObject/战斗动作/近战连击")]
public class MeleeComboActionSO : ScriptableObject
{
    [Header("识别")]
    public string actionId;

    [Header("攻击段")]
    public List<MeleeAttackStep> steps = new List<MeleeAttackStep>();

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (steps == null)
        {
            steps = new List<MeleeAttackStep>();
        }

        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i] != null)
            {
                steps[i].Validate();
            }
        }
    }
#endif
}