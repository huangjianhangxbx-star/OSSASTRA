/* 脚本：CharacterSkills
 * 功能：本体技能槽、独立冷却与已释放派生效果；Root显式推进。
 * 边界：主动作仍交Root唯一Runtime；命中仍交CombatContactGateway，不直接改HP。
 * 协作：接受时冻结动作、装备来源、范围/延迟；取消不退款，离手后切人不重发，死亡清理。
 */
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Ossastra.CharacterCore;
using Ossastra.Combat;
namespace Ossastra.Characters
{
    [DisallowMultipleComponent]
    public sealed class CharacterSkills:MonoBehaviour,ICharacterSkills
    {
        private sealed class Execution
        {
            public Guid id;
            public int generation;
            public string equipmentId,definitionId;
            public SkillEffectKind effect;
            public float delay,radius,range,elapsed;
            public Vector3 origin,landing;
            public AttackRelease release;
        }
        [SerializeField] private SkillDefinitionSO[] slots=Array.Empty<SkillDefinitionSO>();
        private readonly SkillCooldownBook cooldowns=new SkillCooldownBook();
        private readonly List<Execution> effects=new List<Execution>();
        private CharacterRoot owner;
        private Execution pending;
        public string LastResult {get;private set;}="未使用技能";
        public Vector3 LastEffectPosition {get;private set;}
        public int ReleasedCount {get;private set;}
        public int ResolvedCount {get;private set;}
        public Guid LastActionId {get;private set;}
        public string FrozenEquipmentId {get;private set;}
        public int SlotCount=>slots.Length;
        public SkillDefinitionSO Definition(int slot)=>slot>=0&&slot<slots.Length?slots[slot]:null;
        private string CooldownKey(string definitionId)=>owner?.GetComponent<CharacterEquipment>()?.State?.RightInstanceId+":"+definitionId;
        public double Cooldown(int slot)=>owner==null?0:cooldowns.Remaining(CooldownKey(Definition(slot)?.definitionId),owner.SimulationTime);
        public void Initialize(CharacterRoot root){owner=root;}
        public bool TryRequest(int slot,CharacterActionSource source)
        {
            var definition=Definition(slot);
            if(!isActiveAndEnabled||owner==null||definition==null||!definition.IsValid(out _)||Cooldown(slot)>0)
            {LastResult="拒绝：技能未就绪或冷却中";return false;}
            var equipment=owner.GetComponent<CharacterEquipment>();
            if(equipment?.RightDefinition==null){LastResult="拒绝：需要主手";return false;}
            var frozen=new Execution{generation=owner.LifeGeneration,equipmentId=equipment.State.RightInstanceId,
                definitionId=definition.definitionId,effect=definition.effect,delay=definition.travelTime,radius=definition.radius,range=definition.range};
            var duration=definition.cooldown;
            if(!owner.TryConfiguredAction(definition.action,source,"skill:"+definition.definitionId,out var id))
            {LastResult="拒绝："+owner.LastFact;return false;}
            if(!cooldowns.Commit(frozen.equipmentId+":"+frozen.definitionId,id,owner.SimulationTime,duration))throw new InvalidOperationException("Accepted skill cooldown conflict");
            frozen.id=id;pending=frozen;LastActionId=id;FrozenEquipmentId=frozen.equipmentId;LastResult="接受 "+definition.displayName;return true;
        }
        public void TickInput(CharacterInputFrame frame)
        {
            if(frame.skillOnePressed)TryRequest(0,CharacterActionSource.Player);
            if(frame.skillTwoPressed)TryRequest(1,CharacterActionSource.Player);
            if(pending!=null&&owner.DataBus.ActionId!=pending.id)pending=null;
        }
        public bool TryHandleRelease(AttackRelease release,Vector3 direction)
        {
            if(pending==null||pending.id!=release.ActionId)return false;
            var execution=pending;pending=null;ReleasedCount++;
            if(execution.effect==SkillEffectKind.Melee){LastResult="技能释放：近战";return false;}
            execution.release=release;execution.origin=owner.transform.position;execution.landing=execution.origin+direction*execution.range;
            effects.Add(execution);LastResult="技能释放：派生";return true;
        }
        public void TickEffects(float delta)
        {
            if(owner==null)return;
            if(!owner.IsAlive||!isActiveAndEnabled){Clear();return;}
            if(owner.IsSimulationSuspended)return;
            for(int i=effects.Count-1;i>=0;i--)
            {
                var effect=effects[i];
                if(effect.generation!=owner.LifeGeneration){effects.RemoveAt(i);continue;}
                effect.elapsed+=delta;if(effect.elapsed<effect.delay)continue;
                var candidates=ClearPath(effect.origin,effect.landing)?Physics.OverlapSphere(effect.landing,effect.radius,~0,QueryTriggerInteraction.Ignore)
                    .Select(c=>c.GetComponentInParent<CombatVitals>()).Where(v=>v!=null&&ClearPath(effect.landing,v.transform.position)).Distinct().Cast<IHitTarget>().ToArray():Array.Empty<IHitTarget>();
                var outcomes=CombatContactGateway.Resolve(effect.release,owner.Vitals,candidates,effect.landing);
                LastEffectPosition=effect.landing;ResolvedCount++;LastResult="派生结算 "+outcomes.Count+" #"+effect.id.ToString("N");effects.RemoveAt(i);
            }
        }
        private static bool ClearPath(Vector3 a,Vector3 b)
        {
            a+=Vector3.up*.3f;b+=Vector3.up*.3f;var delta=b-a;
            return !Physics.RaycastAll(a,delta,delta.magnitude,~0,QueryTriggerInteraction.Ignore).Any(h=>h.collider.GetComponentInParent<CombatVitals>()==null);
        }
        public void Clear(){pending=null;effects.Clear();}
        private void OnDisable(){if(owner!=null&&owner.ActionActive&&owner.DataBus.ActionId==LastActionId)owner.CancelOwnedMainAction(LastActionId,"skill-disabled");Clear();}
    }
}
