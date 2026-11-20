#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ossastra.Characters
{
    // 单个编辑器试验只覆盖指定角色的配置。已受理动作继续持有原冻结快照。
    public sealed class AttackLabBindings : IDisposable
    {
        private static AttackLabBindings active;
        private readonly CharacterRoot actor;
        private readonly Dictionary<ScriptableObject, ScriptableObject> copies;
        private readonly ActionDefinitionSO[] sourceActions, trialActions;

        public AttackLabBindings(CharacterRoot actor, Dictionary<ScriptableObject, ScriptableObject> copies,
            ActionDefinitionSO[] sourceActions)
        {
            this.actor = actor;
            this.copies = copies;
            this.sourceActions = sourceActions;
            trialActions = Array.ConvertAll(sourceActions, a => Copy(a));
        }

        private T Copy<T>(T source) where T : ScriptableObject =>
            source && copies.TryGetValue(source, out var value) ? (T)value : source;

        public static T Resolve<T>(CharacterRoot actor, T source) where T : ScriptableObject =>
            active != null && actor == active.actor ? active.Copy(source) : source;

        public static ActionDefinitionSO[] ResolveActions(CharacterRoot actor, ActionDefinitionSO[] source) =>
            active != null && active.actor == actor && ReferenceEquals(active.sourceActions,source) ? active.trialActions : source;

        public void Activate()
        {
            if (active != null && active != this) throw new InvalidOperationException("另一个实验仍持有角色配置");
            active = this;
            actor.StateArbitrator.Combat.RebindLaboratorySequence();
        }

        public void Dispose()
        {
            if (active != this) return;
            active = null;
            if (actor && !actor.ActionActive) actor.StateArbitrator?.Combat?.RebindLaboratorySequence();
            else if (actor) actor.GetComponent<CharacterMainAction>()?.InvalidateLaboratoryProfile();
        }
    }
}
#endif
