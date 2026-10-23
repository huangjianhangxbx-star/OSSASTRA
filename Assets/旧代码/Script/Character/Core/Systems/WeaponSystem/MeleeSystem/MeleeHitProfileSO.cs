using System.Collections.Generic;
using UnityEngine;



public enum MeleeHitShapeType
{
    Box,
    Circle
}




[CreateAssetMenu( fileName = "MeleeHitProfile", menuName = "ScriptableObject/战斗动作/近战命中")]
public class MeleeHitProfileSO : ScriptableObject
{
    public LayerMask targetMask;
    public bool allowRepeatedHit;
    [Min(1)] public int maxTargets = 8;
    public List<CombatEffectSO> effects = new List<CombatEffectSO>();

#if UNITY_EDITOR
    private void OnValidate()
    {

        maxTargets = Mathf.Max(1, maxTargets);

        if (effects == null)
        {
            effects = new List<CombatEffectSO>();
        }
    }
#endif
}