/* 功能：为指定横版敌人的原有出招附加一份招式效果配置。
 * 边界：仅订阅 EnemyRoot 正式事实；接受资格、攻击范围、动画、伤害与运动仍归原有端口。
 */
using Ossastra.CharacterCore;
using UnityEngine;
using EnemyActor = Ossastra.Enemies.EnemyRoot;

namespace Ossastra.Characters
{
    [DisallowMultipleComponent, RequireComponent(typeof(EnemyActor))]
    public sealed class EnemyActionImpactBinding : MonoBehaviour
    {
        [Tooltip("只读取该招式的效果列表。阵营、HP 和 AI 出招仍使用此敌人原配置。")]
        public ActionDefinitionSO effectSource;
        private EnemyActor actor;
        private ActionImpactRun run;

        private void OnEnable()
        {
            actor = GetComponent<EnemyActor>();
            actor.ActionObserved += Observe;
        }
        private void OnDisable()
        {
            if (actor) actor.ActionObserved -= Observe;
            run?.End();
            run = null;
        }

        private void Observe(ActionFact fact)
        {
            if (fact.Kind == ActionFactKind.Accepted)
            {
                run?.End();
                run = ActionImpactRun.AcceptEnemy(actor, effectSource, fact.ActionId);
            }
            if (run == null || run.Id != fact.ActionId) return;
            run.Observe(fact);
            if (fact.Release != null) run.Bind(fact.Release, null);
            if (fact.Kind == ActionFactKind.Finished || fact.Kind == ActionFactKind.Cancelled) run = null;
        }
    }
}
