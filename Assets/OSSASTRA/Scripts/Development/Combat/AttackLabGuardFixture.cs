// 独立训练场的固定格挡条件。正常命中批次采样防御，木桩不会额外调用结算。
using Ossastra.CharacterCore;
using Ossastra.Combat;
using UnityEngine;

namespace Ossastra.Development
{
    public sealed class AttackLabGuardFixture : MonoBehaviour, ICombatDefense
    {
        public DefenseSnapshot CaptureDefense(Vector3 incomingOrigin) => new DefenseSnapshot(false, true, 1, 0, .35);
        public void OnDefenseResolved(HitOutcome outcome) { }
    }
}
