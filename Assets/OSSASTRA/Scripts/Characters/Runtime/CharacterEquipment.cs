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
        private ICharacterEquipmentInventory inventory;
        public bool InventoryBound=>inventory!=null;
        public EquipmentDefinitionSO RightDefinition=>InventoryBound?inventory.RightDefinition:rightDefinition;
        public EquipmentDefinitionSO LeftDefinition=>InventoryBound?inventory.LeftDefinition:State==null?null:ownedOffhands.FirstOrDefault(d=>d!=null&&d.definitionId==State.LeftDefinitionId)
            ??ownedOffhands.FirstOrDefault(d=>d!=null&&State.LeftDefinitionId!=null&&State.LeftDefinitionId.StartsWith("legacy:")&&d.offhandKind==State.LeftKind);
        public EquipmentDefinitionSO[] OwnedOffhands=>InventoryBound?inventory.AvailableOffhands:(EquipmentDefinitionSO[])ownedOffhands.Clone();
        public bool CanChangeEquipment=>Idle()&&!(GetComponent<CharacterAbilities>()?.IsBusy??false);
        public void BindInventory(ICharacterEquipmentInventory source)
        {if(source==null||State==null||inventory!=null&&!ReferenceEquals(inventory,source))throw new InvalidOperationException("Invalid inventory binding");State.BindSlots(source);inventory=source;}
        public bool TryEquip(EquipmentDefinitionSO definition)
        {if(InventoryBound){var ok=inventory.TryEquip(definition);LastResult=ok?"装备已转移":"拒绝：库存/动作/容量资格不符";return ok;}if(definition==null||definition.mainHand||!ownedOffhands.Contains(definition)){LastResult="拒绝：非本体持有的副手定义";return false;}return EquipDefinition(definition);}
        public EquipmentState State {get;private set;}
        public string LastResult {get;private set;}="无";
        public bool ValidatePresentations(out string reason)
        {
            try{ValidatePresentation(RightDefinition);ValidatePresentation(LeftDefinition);reason=null;return true;}
            catch(ArgumentException error){reason=error.Message;return false;}
        }
        private void ValidatePresentation(EquipmentDefinitionSO definition)
        {
            if(definition?.spinePresentation==null)return;
            var body=GetComponentInChildren<Spine.Unity.SkeletonAnimation>();
            definition.spinePresentation.Freeze(body?.Skeleton?.Data,owner?.Definition?.rigProfile);
        }
        public void Initialize(CharacterRoot root)
        {
            owner=root;if(State!=null)return;
            var definitions=ownedOffhands.Where(d=>d!=null).ToArray();
            if(definitions.Any(d=>string.IsNullOrWhiteSpace(d.definitionId))||definitions.Select(d=>d.definitionId).Distinct().Count()!=definitions.Length){LastResult="拒绝：副手目录重复或空定义ID";enabled=false;Debug.LogError(LastResult,this);return;}
            State=new EquipmentState(root.ActorId,initialDurability);
            foreach(var definition in ownedOffhands.Where(d=>d!=null))State.RegisterDefinition(definition.definitionId,definition.offhandKind,initialDurability);
            var initial=ownedOffhands.FirstOrDefault(d=>d!=null&&d.offhandKind==initialOffhand);
            if(initial!=null)State.TryEquipDefinition(initial.definitionId,true);else State.TryEquip(initialOffhand,true);
        }
        public bool TryEquip(OffhandKind kind)
        {
            if(InventoryBound){var candidate=OwnedOffhands.FirstOrDefault(d=>d.offhandKind==kind);return candidate!=null&&TryEquip(candidate);}
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
        {try{ValidatePresentation(definition);}catch(ArgumentException error){LastResult="拒绝："+error.Message;return false;}var ok=State!=null&&State.TryEquipDefinition(definition.definitionId,Idle());LastResult=ok?"装备 "+definition.displayName:"拒绝：非空闲";return ok;}
        private bool EquipLegacy(OffhandKind kind)
        {var ok=State!=null&&State.TryEquip(kind,Idle());LastResult=ok?"装备 "+kind:"拒绝：非空闲";return ok;}
    }
}
