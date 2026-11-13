/*
 * 脚本：CombatReleaseVisualFact
 * 功能：记录经过投递入口验证的横版挥击身份和冻结空间事实。
 * 边界：不持有可变的 AttackRelease，不参与判定。来源引用仅供释放刀光跟随显示位置。
 */
using System;

namespace Ossastra.Combat
{
    public readonly struct CombatReleaseVisualFact
    {
        public Guid ParentExecutionId { get; }
        public Guid ActionId { get; }
        public int EmissionIndex { get; }
        public string ActorId { get; }
        public CombatVitals Source { get; }
        public int SourceSceneHandle { get; }
        public CombatContactGeometry Geometry { get; }
        public float WeaponHeight { get; }

        internal CombatReleaseVisualFact(Guid parentExecutionId, Guid actionId, int emissionIndex, string actorId,
            CombatVitals source, CombatContactGeometry geometry, float weaponHeight)
        {
            ParentExecutionId = parentExecutionId;
            ActionId = actionId;
            EmissionIndex = emissionIndex;
            ActorId = actorId;
            Source = source;
            SourceSceneHandle = source.gameObject.scene.handle;
            Geometry = geometry;
            WeaponHeight = weaponHeight;
        }
    }
}

