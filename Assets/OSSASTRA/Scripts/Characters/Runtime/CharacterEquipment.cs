/* 脚本：CharacterEquipment
 * 功能：显式登记本体持有定义并装配独立实例；同类装备按定义ID区分，SO不保存实例状态。
 * 边界：不控制主手资格，不重置耐久/CD；开发切装也必须本体存活空闲。
 */
using Ossastra.CharacterCore;
using UnityEngine;
using System;
using System.Linq;
namespace Ossastra.Characters
{
    [DisallowMultipleComponent]
    public sealed class CharacterEquipment:MonoBehaviour
    {
        [SerializeField] private OffhandKind initialOffhand=OffhandKind.Shield;
        [SerializeField,Min(1)] private int initialDurability=100;
        [SerializeField] private EquipmentDefinitionSO rightDefinition;
        [SerializeField] private EquipmentDefinitionSO[] ownedOffhands=Array.Empty<EquipmentDefinitionSO>();
        private CharacterRoot owner;
        public EquipmentDefinitionSO RightDefinition=>rightDefinition;
        public EquipmentDefinitionSO LeftDefinition=>State==null?null:ownedOffhands.FirstOrDefault(d=>d!=null&&d.definitionId==State.LeftDefinitionId)
            ??ownedOffhands.FirstOrDefault(d=>d!=null&&State.LeftDefinitionId!=null&&State.LeftDefinitionId.StartsWith("legacy:")&&d.offhandKind==State.LeftKind);
        public EquipmentDefinitionSO[] OwnedOffhands=>(EquipmentDefinitionSO[])ownedOffhands.Clone();
        public bool TryEquip(EquipmentDefinitionSO definition)
        {if(definition==null||definition.mainHand||!ownedOffhands.Contains(definition)){LastResult="拒绝：非本体持有的副手定义";return false;}return EquipDefinition(definition);}
        public EquipmentState State {get;private set;}
        public string LastResult {get;private set;}="无";
        public void Initialize(CharacterRoot root)
        {
            owner=root;if(State!=null)return;State=new EquipmentState(root.ActorId,initialDurability);
            foreach(var definition in ownedOffhands.Where(d=>d!=null))State.RegisterDefinition(definition.definitionId,definition.offhandKind,initialDurability);
            var initial=ownedOffhands.FirstOrDefault(d=>d!=null&&d.offhandKind==initialOffhand);
            if(initial!=null)State.TryEquipDefinition(initial.definitionId,true);else State.TryEquip(initialOffhand,true);
        }
        public bool TryEquip(OffhandKind kind)
        {
            var definition=ownedOffhands.FirstOrDefault(d=>d!=null&&d.offhandKind==kind);
            return definition!=null?EquipDefinition(definition):EquipLegacy(kind);
        }
        private bool Idle()
        {
            var guard=GetComponent<ShieldGuard>();
            var idle=isActiveAndEnabled&&owner!=null&&owner.CanSimulateIntent&&!owner.ActionActive&&
                (owner.DataBus.MotionOverride==null||!owner.DataBus.MotionOverride.IsActive)&&(guard==null||!guard.IsGuarding);
            // 副手提供独立空闲资格；不认识/修改主手时钟。
            foreach(var candidate in GetComponents<MonoBehaviour>())if(candidate is ICharacterOffhandStatus offhand && offhand.IsAccepting)idle=false;
            return idle;
        }
        private bool EquipDefinition(EquipmentDefinitionSO definition)
        {var ok=State!=null&&State.TryEquipDefinition(definition.definitionId,Idle());LastResult=ok?"装备 "+definition.displayName:"拒绝：非空闲";return ok;}
        private bool EquipLegacy(OffhandKind kind)
        {var ok=State!=null&&State.TryEquip(kind,Idle());LastResult=ok?"装备 "+kind:"拒绝：非空闲";return ok;}
    }
}
