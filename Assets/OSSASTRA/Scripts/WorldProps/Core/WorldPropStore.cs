/* 脚本：WorldPropStore
 * 功能：保留所有已访问区域的场景物状态；场景卸载仅解绑表现，不删除已耗尽记录。
 * 边界：不维护第二套HP/物品，不把未知或坏档当新档，不自动更改重复实例ID。
 * 协作：保存参与者先构造并验证候选Store，再在共同快照发布段切换引用。
 */
using System;
using System.Linq;
using System.Collections.Generic;

namespace Ossastra.WorldProps
{
    public sealed class WorldPropStore
    {
        readonly Dictionary<string, WorldPropDefinition> definitions = new Dictionary<string, WorldPropDefinition>(StringComparer.Ordinal);
        readonly Dictionary<string, WorldPropState> states = new Dictionary<string, WorldPropState>(StringComparer.Ordinal);
        public event Action Changed;
        public IEnumerable<WorldPropState> States => states.Values;

        public void Register(WorldPropDefinition definition)
        {
            if (definition == null || definitions.ContainsKey(definition.Id))
                throw new ArgumentException("Duplicate/missing prop definition");
            definitions.Add(definition.Id, definition);
        }

        public WorldPropState Get(string map, string instance, int life) => states.TryGetValue(WorldPropState.Key(map, instance, life), out var value) ? value : null;
        public WorldPropState Attach(string map, string instance, int life, string definitionId)
        {
            var old = Get(map, instance, life);
            if (old != null)
            {
                if (old.DefinitionId != definitionId)
                    throw new ArgumentException("Stable instance changed definition");
                return old;
            }

            if (!definitions.TryGetValue(definitionId, out var definition))
                throw new ArgumentException("Unknown prop definition");
            return Add(new WorldPropState(map, instance, life, definition));
        }

        WorldPropState Add(WorldPropState state)
        {
            if (states.ContainsKey(state.StateKey))
                throw new ArgumentException("Duplicate stable prop state");
            states.Add(state.StateKey, state);
            state.Changed += Notify;
            return state;
        }

        void Notify()
        {
            var h = Changed;
            if (h == null)
                return;
            foreach (Action callback in h.GetInvocationList())
                try
                {
                    callback();
                }
                catch
                { /* 状态已提交；观察者失败不能导致奖励重放。宿主自行记录诊断。 */
                }
        }

        public WorldPropRecord[] Capture() => states.Values.OrderBy(s => s.StateKey, StringComparer.Ordinal).Select(s => s.Capture()).ToArray();
        public static WorldPropStore Restore(WorldPropRecord[] records, IEnumerable<WorldPropDefinition> definitions)
        {
            if (records == null || definitions == null)
                throw new ArgumentException("Missing world prop snapshot");
            var result = new WorldPropStore();
            foreach (var definition in definitions)
                result.Register(definition);
            foreach (var record in records)
            {
                if (record == null || !result.definitions.TryGetValue(record.definitionId, out var definition))
                    throw new ArgumentException("Unknown saved prop definition");
                result.Add(WorldPropState.Restore(record, definition));
            }

            return result;
        }
    }
}
