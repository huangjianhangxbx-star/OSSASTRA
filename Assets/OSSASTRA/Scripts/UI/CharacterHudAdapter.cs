/* 脚本：CharacterHudAdapter
 * 功能：连接现有唯一控制会话、角色能力与HUD窄端口。
 * 边界：只读实际所有者；所有按钮仍调用正式命令，不创建装备或补充资源。
 */
using System;
using System.Linq;
using UnityEngine;
using Ossastra.Characters;
using Ossastra.Combat;
namespace Ossastra.UserInterface
{
    public sealed class CharacterHudAdapter:MonoBehaviour,ICharacterHudPort
    {
        [SerializeField] private CharacterControlCoordinator coordinator;
        [SerializeField] private HealingBottleService bottle;
        public string Notice {get;private set;}="";
        private string previousAdmission,previousDodge,previousSkill,previousOffhand,previousAbility,previousMainAction;
        public void SetModalCapture(object owner,bool captured){coordinator?.SetInputCapture(owner,captured);}
        private void WatchRejection(string current,ref string previous){if(current!=previous&&(current?.StartsWith("Rejected")==true||current?.StartsWith("拒绝")==true))Notice=current.Replace("stamina","体力不足");previous=current;}
        public int BottleCount=>bottle==null?0:bottle.Count;
        public CharacterHudSnapshot Read(bool companion)
        {
            var root=coordinator?.Characters.ElementAtOrDefault(companion?1:0);return Project(root);
        }
        public CharacterHudSnapshot ReadCurrent()=>Project(coordinator?.Current);
        private CharacterHudSnapshot Project(Ossastra.Characters.CharacterRoot root)
        {
            if(root==null)return default;
            var stamina=root.GetComponent<CharacterStamina>();var posture=root.GetComponent<CombatPosture>();
            var equipment=root.GetComponent<CharacterEquipment>();var skills=root.GetComponent<CharacterSkills>();
            var abilities=root.GetComponent<CharacterAbilities>();var modern=abilities!=null&&abilities.isActiveAndEnabled;
            var mainAction=root.GetComponent<CharacterMainAction>();if(root==coordinator.Current)WatchRejection(mainAction?.LastResult,ref previousMainAction);
            if(root==coordinator.Current&&modern)WatchRejection(abilities.LastResult,ref previousAbility);
            if(root==coordinator.Current){WatchRejection(root.Actions?.LastResult,ref previousAdmission);WatchRejection(root.GetComponent<CharacterDodge>()?.LastResult,ref previousDodge);WatchRejection(skills?.LastResult,ref previousSkill);WatchRejection(root.GetComponent<CharacterOffhand>()?.LastResult,ref previousOffhand);}
            return new CharacterHudSnapshot{actorId=root.ActorId,name=root.Definition?.displayName??root.ActorId,alive=root.IsAlive,controlled=root==coordinator.Current,
                hp=root.Vitals?.Health.Current??0,maxHp=root.Vitals?.Health.Maximum??0,stamina=(float)(stamina?.Current??0),maxStamina=(float)(stamina?.Maximum??0),
                grayHp=(float)(root.Vitals?.Recovery?.Gray??0),
                abilityResources=modern?abilities.ResourceSummary:"",
                posture=(float)(posture?.Current??0),maxPosture=(float)(posture?.Maximum??0),rightHand=equipment?.RightDefinition?.displayName??"未接入",mainHandForm=mainAction?.FormLabel??"I",
                leftHand=equipment?.LeftDefinition?.displayName??"空",rightInstanceId=equipment?.State?.RightInstanceId,leftInstanceId=equipment?.State?.LeftInstanceId,leftDefinitionId=equipment?.LeftDefinition?.definitionId,durability=equipment?.State?.Durability??0,
                condition=!root.IsAlive?"死亡 · 等待返场":posture!=null&&posture.IsStaggered?"失衡":root!=coordinator.Current?"自主战斗 / 跟随":"",
                skillNames=Enumerable.Range(0,3).Select(i=>modern?abilities.SkillDefinition(i)?.displayName??"空槽":skills?.Definition(i)?.displayName??"未接入").ToArray(),
                skillStates=Enumerable.Range(0,3).Select(i=>modern?AbilityStatus(root,abilities,i):SkillStatus(root,skills,i)).ToArray(),
                skillCooldowns=Enumerable.Range(0,3).Select(i=>(float)(modern?abilities.SkillCooldown(i):skills?.Cooldown(i)??0)).ToArray()};
        }
        private static string AbilityStatus(Ossastra.Characters.CharacterRoot root,CharacterAbilities abilities,int slot)
        {
            var definition=abilities.SkillDefinition(slot);if(definition==null)return "空槽";
            if(!root.IsAlive)return "死亡";var cd=abilities.SkillCooldown(slot);if(cd>0)return "冷却 "+cd.ToString("0.0")+"s";
            return abilities.SkillAvailability(slot);
        }
        private static string SkillStatus(Ossastra.Characters.CharacterRoot root,CharacterSkills skills,int slot)
        {
            var definition=skills?.Definition(slot);if(skills==null||!skills.isActiveAndEnabled||definition==null||!definition.IsValid(out _))return "未接入";
            if(!root.IsAlive)return "死亡";var cd=skills.Cooldown(slot);if(cd>0)return "冷却 "+cd.ToString("0.0")+"s";
            if(root.GetComponent<CharacterEquipment>()?.RightDefinition==null)return "需要主手";
            var query=new CharacterActionRequest(root.ActorId,root.LifeGeneration,root.ActionControlRevision,CharacterActionKind.Main,CharacterActionSource.UserInterface,Guid.Empty);
            if(root.Actions.CanAccept(query,definition.action.staminaCost,out var reason))return "可用";
            return reason=="stamina"?"体力不足":reason=="main-not-ready"?"动作恢复中":reason=="source-authority"?"输入被占用":"暂不可用";
        }        public void UseBottle(){if(bottle==null){Notice="共享瓶未接入";return;}bottle.TryUse();Notice=bottle.LastResult;}
        public void UseSkill(int slot)
        {
            var abilities=coordinator?.Current?.GetComponent<CharacterAbilities>();
            if(abilities!=null&&abilities.isActiveAndEnabled){abilities.RequestSkill(slot,CharacterActionSource.UserInterface);Notice=abilities.LastResult;return;}
            var skills=coordinator?.Current?.GetComponent<CharacterSkills>();if(skills==null){Notice="技能未接入";return;}skills.TryRequest(slot,CharacterActionSource.UserInterface);Notice=skills.LastResult;
        }
        public (string id,string name)[] SkillChoices(string actorId)=>Actor(actorId)?.GetComponent<CharacterAbilities>()?.SkillChoices.Select(d=>(d.definitionId,d.displayName)).ToArray()??Array.Empty<(string,string)>();
        public void ConfigureSkill(string actorId,int slot,string definitionId)
        {var abilities=Actor(actorId)?.GetComponent<CharacterAbilities>();if(abilities==null){Notice="技能配置不可用";return;}abilities.ConfigureSkill(slot,definitionId);Notice=abilities.LastResult;}
        public (string id,string name)[] MainActionChoices(string actorId)=>Actor(actorId)?.GetComponent<CharacterMainAction>()?.Choices.Where(p=>p!=null).Select(p=>(p.definitionId,p.displayName)).ToArray()??Array.Empty<(string,string)>();
        public string SelectedMainAction(string actorId)=>Actor(actorId)?.GetComponent<CharacterMainAction>()?.SelectedId??"";
        public void ConfigureMainAction(string actorId,string definitionId)
        {var main=Actor(actorId)?.GetComponent<CharacterMainAction>();if(main==null){Notice="主手动作组未接入";return;}main.Configure(definitionId);Notice=main.LastResult;}
        public (string id,string name,string state,bool held)[] SourceChoices(string actorId)
        {
            var abilities=Actor(actorId)?.GetComponent<CharacterAbilities>();if(abilities==null)return Array.Empty<(string,string,string,bool)>();
            var rows=abilities.EvaluateSources();return abilities.SourceChoices.Select(d=>
            {
                var row=rows.FirstOrDefault(r=>r.Definition?.Id==d.definitionId);
                var state=row==null?"未授予":row.Status==Ossastra.CharacterCore.Abilities.EffectSourceStatus.Active?"生效":
                    row.Status==Ossastra.CharacterCore.Abilities.EffectSourceStatus.Dormant?"休眠：缺少已装配技能根":
                    row.Status==Ossastra.CharacterCore.Abilities.EffectSourceStatus.Superseded?"被更高优先级/阶段来源替代":"未实现/职业不匹配";
                return(d.definitionId,d.nativeName,state,row!=null);
            }).ToArray();
        }
        public void ToggleExperimentalSource(string actorId,string definitionId)
        {var abilities=Actor(actorId)?.GetComponent<CharacterAbilities>();if(abilities==null){Notice="来源配置不可用";return;}abilities.ToggleExperimentalSource(definitionId);Notice=abilities.LastResult;}
        public void SwitchCharacter(){Notice=coordinator!=null&&coordinator.SwitchNext()?"已切换实控角色":"当前无法切换";}
        private Ossastra.Characters.CharacterRoot Actor(string id)=>coordinator?.Characters.FirstOrDefault(r=>r!=null&&r.ActorId==id);
        public void Equip(string actorId,string definitionId)
        {
            var equipment=Actor(actorId)?.GetComponent<CharacterEquipment>();if(equipment==null){Notice="角色装备不可用";return;}
            var definition=equipment.OwnedOffhands.FirstOrDefault(d=>d!=null&&d.definitionId==definitionId);
            equipment.TryEquip(definition);Notice=equipment.LastResult;
        }
        public (string id,string name,Sprite sprite)[] EquipmentChoices(string actorId)
            =>Actor(actorId)?.GetComponent<CharacterEquipment>()?.OwnedOffhands.Where(d=>d!=null).Select(d=>(d.definitionId,d.displayName,d.worldSprite)).ToArray()
                ??Array.Empty<(string,string,Sprite)>();
    }
}
