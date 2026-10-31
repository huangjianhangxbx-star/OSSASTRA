/* 脚本：WorldPropState
 * 功能：单个稳定场景物的唯一Health、生命周期和固定破坏奖励意图。
 * 边界：不查物理、不读输入、不直接发物；Applied回执不二次扣血，卸载不破坏。
 * 协作：WorldPropsRoot调度；奖励适配在原子发布段调用无分配的receipt提交，然后才通知库存。
 */
using System;
using System.Linq;
using Ossastra.CharacterCore;

namespace Ossastra.WorldProps
{
    public enum WorldPropPhase
    {
        Alive,
        BreakPending,
        Depleted
    }

    [Serializable]
    public sealed class WorldPropDrop
    {
        public string definitionId;
        public int quantity;
        public WorldPropDrop Copy() => new WorldPropDrop
        {
            definitionId = definitionId,
            quantity = quantity
        };
    }

    public sealed class WorldPropDropRule
    {
        public string DefinitionId { get; }
        public int Minimum { get; }
        public int Maximum { get; }
        public double Chance { get; }

        public WorldPropDropRule(string id, int minimum, int maximum, double chance)
        {
            if (string.IsNullOrWhiteSpace(id) || minimum < 1 || maximum < minimum || maximum > 1000 || double.IsNaN(chance) || chance < 0 || chance > 1)
                throw new ArgumentException("Invalid loot rule");
            DefinitionId = id;
            Minimum = minimum;
            Maximum = maximum;
            Chance = chance;
        }
    }

    public sealed class WorldPropDefinition
    {
        readonly WorldPropDropRule[] rules;
        public string Id { get; }
        public int Version { get; }
        public int Durability { get; }

        public WorldPropDefinition(string id, int version, int durability, WorldPropDropRule[] loot)
        {
            if (string.IsNullOrWhiteSpace(id) || version < 1 || durability < 1 || loot == null || loot.Length > 16 || loot.Any(r => r == null))
                throw new ArgumentException("Invalid prop definition");
            Id = id;
            Version = version;
            Durability = durability;
            rules = loot.ToArray();
        }

        // 稳定生命周期只决定一次计划；不使用进程相关string.GetHashCode，也不在重试时重新抽取。
        public WorldPropDrop[] Roll(string breakId)
        {
            uint seed = 2166136261;
            foreach (char c in breakId)
            {
                seed ^= c;
                seed *= 16777619;
            }

            double Next()
            {
                seed ^= seed << 13;
                seed ^= seed >> 17;
                seed ^= seed << 5;
                return seed / 4294967296.0;
            }

            return rules.Select(r => Next() < r.Chance ? new WorldPropDrop { definitionId = r.DefinitionId, quantity = r.Minimum + (int)(Next() * (r.Maximum - r.Minimum + 1)) } : null).Where(d => d != null).ToArray();
        }
    }

    [Serializable]
    public sealed class WorldPropRecord
    {
        public string mapId, instanceId, definitionId, breakId;
        public int lifeGeneration = 1, definitionVersion, durability;
        public WorldPropPhase phase;
        public WorldPropDrop[] plan = Array.Empty<WorldPropDrop>();
    }

    public sealed class WorldPropState
    {
        readonly WorldPropDefinition definition;
        WorldPropDrop[] plan = Array.Empty<WorldPropDrop>();
        public string MapId { get; }
        public string InstanceId { get; }
        public int LifeGeneration { get; }
        public string DefinitionId => definition.Id;
        public Health Health { get; private set; }
        public WorldPropPhase Phase { get; private set; } = WorldPropPhase.Alive;
        public string BreakId { get; private set; }
        public WorldPropDrop[] Plan => plan.Select(x => x.Copy()).ToArray();

        public event Action Changed;
        public string LastObserverError { get; private set; }

        public static string Key(string map, string instance, int life)
        {
            if (string.IsNullOrWhiteSpace(map) || string.IsNullOrWhiteSpace(instance) || map.Contains("|") || instance.Contains("|") || life < 1)
                throw new ArgumentException("Stable map/instance/life required");
            return map + "|" + instance + "|" + life;
        }

        public string StateKey => Key(MapId, InstanceId, LifeGeneration);

        public WorldPropState(string map, string instance, int life, WorldPropDefinition data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            var key = Key(map, instance, life);
            MapId = map;
            InstanceId = instance;
            LifeGeneration = life;
            definition = data;
            Health = new Health("worldprop:" + key, data.Durability);
        }

        public void OnHit(HitOutcome result)
        {
            if (result == null || result.Reason != HitReason.Applied || result.TargetId != Health.Id || Phase != WorldPropPhase.Alive)
                return;
            if (result.HpAfter != Health.Current)
                throw new InvalidOperationException("Outcome does not match authoritative Health");
            if (Health.Current == 0)
            {
                BreakId = "break:" + StateKey;
                plan = definition.Roll(BreakId);
                Phase = WorldPropPhase.BreakPending;
            }

            Notify();
        }

        /// <summary>sink必须在库存/世界记录共同发布时调用commit；回执通知在业务提交之后，失败保留同一Pending。</summary>
        public bool TryDeliver(Func<string, WorldPropDrop[], Action, bool> sink)
        {
            if (Phase != WorldPropPhase.BreakPending)
                return false;
            if (sink == null)
                throw new ArgumentNullException(nameof(sink));
            bool committed = false;
            Action publish = () =>
            {
                if (committed || Phase != WorldPropPhase.BreakPending)
                    throw new InvalidOperationException("Receipt publish repeated");
                Phase = WorldPropPhase.Depleted;
                committed = true;
            };
            bool accepted = sink(BreakId, Plan, publish);
            if (accepted != committed)
                throw new InvalidOperationException("Reward sink violated atomic receipt contract");
            if (committed)
                Notify();
            return committed;
        }

        void Notify()
        {
            var handlers = Changed;
            if (handlers == null)
                return;
            foreach (Action callback in handlers.GetInvocationList())
                try
                {
                    callback();
                }
                catch (Exception ex)
                {
                    LastObserverError = ex.Message;
                }
        }

        public WorldPropRecord Capture() => new WorldPropRecord
        {
            mapId = MapId,
            instanceId = InstanceId,
            lifeGeneration = LifeGeneration,
            definitionId = definition.Id,
            definitionVersion = definition.Version,
            durability = Health.Current,
            phase = Phase,
            breakId = BreakId,
            plan = Plan
        };
        public static WorldPropState Restore(WorldPropRecord saved, WorldPropDefinition definition)
        {
            if (saved == null || definition == null || saved.definitionId != definition.Id || saved.definitionVersion != definition.Version || saved.plan == null || !Enum.IsDefined(typeof(WorldPropPhase), saved.phase))
                throw new ArgumentException("Prop snapshot definition/version/state invalid");
            var result = new WorldPropState(saved.mapId, saved.instanceId, saved.lifeGeneration, definition);
            result.Health = Health.FromSnapshot(result.Health.Id, definition.Durability, saved.durability);
            if (saved.phase == WorldPropPhase.Alive)
            {
                if (saved.durability <= 0 || !string.IsNullOrEmpty(saved.breakId) || saved.plan.Length != 0)
                    throw new ArgumentException("Alive snapshot inconsistent");
            }
            else
            {
                if (saved.durability != 0 || saved.breakId != "break:" + result.StateKey)
                    throw new ArgumentException("Terminal snapshot inconsistent");
                var expected = definition.Roll(saved.breakId);
                if (saved.plan.Any(x => x == null) || saved.plan.Length != expected.Length || saved.plan.Where((x, i) => x.definitionId != expected[i].definitionId || x.quantity != expected[i].quantity).Any())
                    throw new ArgumentException("Frozen loot plan incompatible");
            }

            result.Phase = saved.phase;
            result.BreakId = saved.breakId;
            result.plan = saved.plan.Select(x => x.Copy()).ToArray();
            return result;
        }
    }
}
