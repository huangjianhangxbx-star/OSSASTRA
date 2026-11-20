// 普攻和受理能力的接触配方随父动作冻结，按真实回执选择表现。
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Ossastra.CharacterCore;
using Ossastra.Characters;
using Ossastra.Combat;
using UnityEngine;
using ActorRoot = Ossastra.Characters.CharacterRoot;

namespace Ossastra.Audio
{
    public static class SideViewFeedbackRuntime
    {
        private sealed class RoleState
        {
            internal object previewOwner;
            internal SideViewFeedbackProfileSO preview;
            internal int lifeGeneration;
            internal int sceneGeneration;
            internal readonly Dictionary<Guid, Accepted> accepted = new Dictionary<Guid, Accepted>();
            internal readonly Queue<Guid> order = new Queue<Guid>();
        }

        internal sealed class Accepted
        {
            internal string selector, profileName;
            internal int stage;
            internal SideViewCombatFeedbackBinding[] rules;
        }

        private static readonly ConditionalWeakTable<ActorRoot, RoleState> roles =
            new ConditionalWeakTable<ActorRoot, RoleState>();
        private static readonly Dictionary<int, int> scenes = new Dictionary<int, int>();
        internal static event Action<int> TrialReset;

        public static bool SetPreview(ActorRoot actor, SideViewFeedbackProfileSO source, object owner)
        {
            if (actor == null || source == null || owner == null) return false;
            var state = roles.GetValue(actor, _ => new RoleState());
            if (state.previewOwner != null && !ReferenceEquals(state.previewOwner, owner)) return false;
            state.previewOwner = owner;
            state.preview = source;
            return true;
        }

        public static void ClearPreview(ActorRoot actor, object owner)
        {
            if (actor == null || !roles.TryGetValue(actor, out var state) || !ReferenceEquals(state.previewOwner, owner))
                return;
            state.previewOwner = null;
            state.preview = null;
        }

        public static void ResetTrial(int sceneHandle)
        {
            scenes[sceneHandle] = SceneGeneration(sceneHandle) + 1;
            TrialReset?.Invoke(sceneHandle);
        }

        internal static int SceneGeneration(int sceneHandle)
            => scenes.TryGetValue(sceneHandle, out var value) ? value : 0;

        internal static SideViewFeedbackProfileSO ProfileFor(ActorRoot actor)
        {
            if (actor == null) return null;
            if (roles.TryGetValue(actor, out var state) && state.preview != null) return state.preview;
            return actor.Definition?.sideViewFeedback;
        }

        internal static string MaterialOf(Component component, bool ground)
        {
            var surface = component == null ? null : component.GetComponentInParent<AudioSurfaceFact>();
            return surface == null || !surface.isActiveAndEnabled ? null :
                (ground ? surface.surface : surface.impactMaterial)?.Trim().ToLowerInvariant();
        }

        internal static void Observe(ActorRoot actor, ActionFact fact)
        {
            if (actor == null || fact == null || fact.Kind != ActionFactKind.Accepted) return;
            var state = roles.GetValue(actor, _ => new RoleState());
            int sceneGeneration = SceneGeneration(actor.gameObject.scene.handle);
            if (state.lifeGeneration != actor.LifeGeneration || state.sceneGeneration != sceneGeneration)
            {
                state.accepted.Clear();
                state.order.Clear();
                state.lifeGeneration = actor.LifeGeneration;
                state.sceneGeneration = sceneGeneration;
            }
            var source = fact.Source ?? "";
            var main = actor.StateMainAction;
            string selector = source.StartsWith("ability:", StringComparison.Ordinal) ? source.Substring(8) :
                main != null && main.ExecutionId == fact.ActionId && !string.IsNullOrEmpty(main.SelectedId)
                    ? "main:" + main.SelectedId : "body.basic";
            var profile = ProfileFor(actor);
            if (profile == null) return;
            if (state.accepted.ContainsKey(fact.ActionId)) return;
            state.accepted.Add(fact.ActionId, new Accepted
            {
                selector = selector,
                profileName = profile.name,
                stage = actor.AttackStage,
                rules = profile.FreezeCombat()
            });
            state.order.Enqueue(fact.ActionId);
            if (state.order.Count > 128) state.accepted.Remove(state.order.Dequeue());
        }

        internal static SideViewCombatFeedbackBinding Contact(CombatContactBatch batch, SideViewContactResult result,
            string material, out string label, string channel = null, string deliveryMode = null)
        {
            var actor = batch.Source == null ? null : batch.Source.GetComponentInParent<ActorRoot>();
            label = "共用反馈";
            if (actor == null || !roles.TryGetValue(actor, out var state) ||
                state.sceneGeneration != SceneGeneration(actor.gameObject.scene.handle) ||
                state.lifeGeneration != actor.LifeGeneration ||
                !state.accepted.TryGetValue(batch.ParentExecutionId, out var data)) return null;
            int stage = batch.Geometry?.BasicAttackStage > 0 ? batch.Geometry.Value.BasicAttackStage : data.stage;
            var row = SideViewFeedbackProfileSO.ResolveCombat(data.rules, data.selector, stage, result, material,
                channel ?? batch.Channel, deliveryMode ?? batch.DeliveryMode);
            if (row != null)
                label = $"{data.profileName} / {data.selector} / 第 {stage} 段 / {result} / {material ?? "通用"}";
            return row;
        }
    }
}
