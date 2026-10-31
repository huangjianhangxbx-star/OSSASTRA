/* 脚本：WorldPropDefinitionSO
 * 功能：可复用破坏资格、最大耐久、固定版本掉落表及表现音声语义的作者配置。
 * 边界：不保存实例HP、随机进度或耗尽状态；版本变化由存档校验明确拒绝，不偷偷重生。
 */
using System;
using UnityEngine;

namespace Ossastra.WorldProps
{
    [CreateAssetMenu(menuName = "Ossastra/World Props/Definition")]
    public sealed class WorldPropDefinitionSO : ScriptableObject
    {
        public string definitionId;
        [Min(1)]
        public int version = 1;
        public bool destructible = true;
        [Min(1)]
        public int maximumDurability = 60;
        [Serializable]
        public sealed class DropRule
        {
            public string itemDefinitionId;
            [Min(1)]
            public int minimum = 1, maximum = 1;
            [Range(0, 1)]
            public float chance = 1;
        }

        public DropRule[] drops = Array.Empty<DropRule>();
        public string hitSoundEvent, breakSoundEvent;
        [TextArea]
        public string provenance;
        public WorldPropDefinition Freeze()
        {
            if (drops == null)
                throw new ArgumentException("Missing drop table");
            return new WorldPropDefinition(definitionId, version, maximumDurability, Array.ConvertAll(drops, d => d == null ? null : new WorldPropDropRule(d.itemDefinitionId, d.minimum, d.maximum, d.chance)));
        }
    }
}
