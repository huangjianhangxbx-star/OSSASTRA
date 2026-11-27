/*
 * 功能：验证手感台连段候选的深复制、原招式数据保留和逐连接编辑。
 * 边界：仅使用内存 SO。正式资源与出招由独立试玩验证。
 */
using System;
using System.IO;
using System.Linq;
using Ossastra.Characters;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ossastra.Development.Editor
{
    public static class AttackLabBehaviorAcceptance
    {
        public static string Verify()
        {
            var third = ScriptableObject.CreateInstance<ActionDefinitionSO>();
            var fourth = ScriptableObject.CreateInstance<ActionDefinitionSO>();
            var shared = ScriptableObject.CreateInstance<ChainDaggerExecutorSO>();
            third.executor = shared;
            third.effects.entries.Add(new SelfDisplacementEffect { distance = .62f });
            fourth.effects.entries.Add(new TargetKnockbackEffect { distance = 1.7f, duration = .14f });
            try
            {
                using (var draft = new AttackLabBehaviorDraft(third, fourth, new[] { 1 }, true))
                {
                    Need(!ReferenceEquals(draft.Third.executor, shared), "候选共享了原行为。");
                    ((ChainDaggerExecutorSO)draft.Third.executor).pullSpeed = 3;
                    Need(!Mathf.Approximately(shared.pullSpeed, 3), "候选修改污染共享行为。");
                    ((ChainDaggerExecutorSO)draft.Third.executor).projectile.range = 3.42f;
                    Need(!Mathf.Approximately(shared.projectile.range, 3.42f), "候选共享了内联投递参数。");
                    ((SelfDisplacementEffect)draft.Third.effects.entries[0]).distance = 1.1f;
                    Need(Mathf.Approximately(((SelfDisplacementEffect)third.effects.entries[0]).distance, .62f),
                        "候选修改污染原招式效果。");
                    Need(draft.Fourth.effects.entries.OfType<TargetKnockbackEffect>().Count() == 1 &&
                        Mathf.Approximately(draft.Fourth.effects.entries.OfType<TargetKnockbackEffect>().Single().distance, 1.7f),
                        "候选替换了已有第四击效果。");
                    Need(draft.ResolveConnections(new[] { 1, 2 }).SequenceEqual(new[] { 1, 2, 3 }),
                        "启用第三接第四时覆盖了其他连接。");
                    draft.RequireSuccess = false;
                    Need(draft.ResolveConnections(new[] { 1, 3 }).SequenceEqual(new[] { 1 }),
                        "关闭时误删了其他连接。");
                    VerifySavedDraft(third, fourth, draft);
                }
                return "连段候选检查通过：深复制、原效果保留、逐连接修改与本机冷读。";
            }
            finally
            {
                Object.DestroyImmediate(third);
                Object.DestroyImmediate(fourth);
                Object.DestroyImmediate(shared);
            }
        }

        private static void VerifySavedDraft(ActionDefinitionSO third, ActionDefinitionSO fourth, AttackLabBehaviorDraft draft)
        {
            string path = Path.Combine(Path.GetTempPath(), "ossastra-behavior-" + Guid.NewGuid().ToString("N") + ".asset");
            try
            {
                AttackLabBehaviorPreset.StoreAt(path, draft);
                using (var restored = new AttackLabBehaviorDraft(third, fourth, null, true))
                {
                    Need(AttackLabBehaviorPreset.RestoreAt(path, restored), "本机候选冷读失败。");
                    var executor = (ChainDaggerExecutorSO)restored.Third.executor;
                    Need(Mathf.Approximately(executor.projectile.range, 3.42f) && Mathf.Approximately(executor.pullSpeed, 3f),
                        "冷读丢失投递或回拉参数。");
                    Need(!restored.RequireSuccess, "冷读丢失接段条件。");
                    Need(restored.Third.effects.entries[0] is SelfDisplacementEffect travel && Mathf.Approximately(travel.distance, 1.1f),
                        "冷读丢失多态第三段效果。");
                    Need(restored.Fourth.effects.entries[0] is TargetKnockbackEffect push && Mathf.Approximately(push.distance, 1.7f),
                        "冷读丢失多态第四段效果。");
                }
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        private static void Need(bool condition, string error)
        {
            if (!condition) throw new InvalidOperationException(error);
        }
    }
}
