/* 脚本：CompanionInventoryConsumer
 * 功能：本体通用补给消费者，按冻结原型有限规则选择后备药/炸弹；根显式调度，定义用引用区分。
 * 边界：不移动、不取消战斗动作、不扣HP/物品；不把所有食物或技能扩展为自动使用。
 * 协作：队伍仅提供真实后备与优先，InventoryItemUse仍走唯一能力事务；接管停止新请求。
 */
using System;
using System.Linq;
using UnityEngine;
using Ossastra.Characters;
using Ossastra.Combat;
namespace Ossastra.Inventory
{
    using CharacterRoot=Ossastra.Characters.CharacterRoot;
    using ItemDefinitionSO=Ossastra.Characters.ItemDefinitionSO;
    [DisallowMultipleComponent]
    public sealed class CompanionInventoryConsumer:MonoBehaviour,ICharacterInventoryConsumer
    {
        [SerializeField] private InventorySession session;
        [SerializeField] private InventoryItemUse itemUse;
        [SerializeField] private CombatEngagementSession engagement;
        [SerializeField] private ItemDefinitionSO medicine,bomb;
        [SerializeField,Min(.01f)] private float decisionInterval=.2f,healingReach=3,healingRatio=.65f,bombInterval=5;
        private CharacterRoot owner;
        private double nextDecision,nextBomb;
        public string Reason {get;private set;}="未采样";
        public int Uses {get;private set;}
        public void Initialize(CharacterRoot root){owner=root;}
        public bool TickCandidate()
        {
            if(!isActiveAndEnabled||owner==null||session==null||!session.Ready||itemUse==null||engagement==null||!engagement.CanSample||owner.ActorId!=session.ReserveOwnerId)return Wait("补给会话不可用");
            if(owner.GetComponent<CharacterInputHandler>().HasPlayerControl)return Wait("实控本体 · 等待托管");
            if(owner.SimulationTime<nextDecision)return false;nextDecision=owner.SimulationTime+decisionInterval;
            if(!owner.CanSimulateIntent||owner.ActionActive||owner.GetComponent<CharacterAbilities>()?.IsBusy==true||owner.GetComponent<CharacterDodge>()?.IsActive==true||owner.DataBus.MotionOverride?.IsActive==true)return Wait("等待动作空闲");
            var enemies=engagement.Enemies.Where(e=>e!=null&&e.isActiveAndEnabled&&e.Vitals!=null&&e.Vitals.IsAvailable).ToArray();
            if(enemies.Any(e=>e.WarningVisible&&Mathf.Abs(e.WarningCenter.y-owner.transform.position.y)<.6f&&Vector2.Distance(new Vector2(e.WarningCenter.x,e.WarningCenter.z),new Vector2(owner.transform.position.x,owner.transform.position.z))<=e.WarningRadius))return Wait("先避险");
            var stock=session.Ledger.Items.Where(i=>i.Location.ContainerId=="reserve").OrderBy(i=>i.Id,StringComparer.Ordinal).ToArray();
            var drug=stock.FirstOrDefault(i=>i.DefinitionId==medicine?.definitionId);
            if(drug!=null)
            {
                var target=session.Coordinator.Characters.Where(r=>r!=null&&r.IsAlive&&r.Vitals.Health.Current/(float)r.Vitals.Health.Maximum<=healingRatio&&
                    r.GetComponent<CombatRegeneration>()?.Active==false&&SameLayer(r.transform.position)&&Vector3.Distance(r.transform.position,owner.transform.position)<=healingReach&&
                    CombatDeliveryQueue.Clear(owner.transform.position,r.transform.position)).OrderBy(r=>r.Vitals.Health.Current/(float)r.Vitals.Health.Maximum).ThenBy(r=>r.ActorId,StringComparer.Ordinal).FirstOrDefault();
                if(target!=null&&itemUse.TryUseFor(drug.Id,owner,target.Vitals,null,true)){Uses++;Reason="先治疗 · "+target.ActorId;return true;}
            }
            var explosive=stock.FirstOrDefault(i=>i.DefinitionId==bomb?.definitionId);
            if(explosive==null)return Wait(drug==null?session.PriorityDefinitionId!=null?"未持有或耗尽 · 请交付":"后备无可用道具":"无需治疗或距离/视线不符");
            if(owner.SimulationTime<nextBomb)return Wait("等待投弹间隔");
            var preferred=session.PriorityDefinitionId==bomb.definitionId;var geometry=bomb.ability?.delivery;if(geometry==null)return Wait("投弹定义缺失");
            var visible=enemies.Where(e=>e.HasCombatTarget&&SameLayer(e.transform.position)&&OnScreen(e.transform.position)).OrderBy(e=>e.ActorId,StringComparer.Ordinal).ToArray();
            foreach(var enemy in visible)
            {
                var point=enemy.transform.position;if(Vector3.Distance(point,owner.transform.position)>geometry.range||!CombatDeliveryQueue.Clear(owner.transform.position,point))continue;
                var count=visible.Count(e=>Vector3.Distance(e.transform.position,point)<=geometry.radius);if(count<(preferred?1:2))continue;
                if(itemUse.TryUseFor(explosive.Id,owner,enemy.Vitals,point,true)){nextBomb=owner.SimulationTime+bombInterval;Uses++;Reason="已接受实验投弹 · 释放时消耗1";return true;}
            }
            return Wait("等待安全敌群/距离/投弹间隔");
        }
        private bool SameLayer(Vector3 point)=>Mathf.Abs(point.y-owner.transform.position.y)<.5f;
        private static bool OnScreen(Vector3 point)
        {var camera=Camera.main;if(camera==null)return false;var p=camera.WorldToViewportPoint(point);return p.z>0&&p.x>=0&&p.x<=1&&p.y>=0&&p.y<=1;}
        private bool Wait(string reason){Reason=reason;return false;}
    }
}

