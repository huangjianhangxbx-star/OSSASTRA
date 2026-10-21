/* 脚本：CharacterEquipment
 * 功能：显式初始化本体独立装备；Inspector初始左手仅用于新场样板。
 * 边界：不控制主手资格，不重置耐久/CD；开发切装也必须本体存活空闲。
 */
using Ossastra.CharacterCore;
using UnityEngine;
namespace Ossastra.Characters
{
    [DisallowMultipleComponent]
    public sealed class CharacterEquipment:MonoBehaviour
    {
        [SerializeField] private OffhandKind initialOffhand=OffhandKind.Shield;
        [SerializeField,Min(1)] private int initialDurability=100;
        private CharacterRoot owner;
        public EquipmentState State {get;private set;}
        public string LastResult {get;private set;}="无";
        public void Initialize(CharacterRoot root)
        {owner=root;if(State==null){State=new EquipmentState(root.ActorId,initialDurability);State.TryEquip(initialOffhand,true);}}
        public bool TryEquip(OffhandKind kind)
        {
            var guard=GetComponent<ShieldGuard>();
            var idle=isActiveAndEnabled&&owner!=null&&owner.CanSimulateIntent&&!owner.ActionActive&&
                (owner.DataBus.MotionOverride==null||!owner.DataBus.MotionOverride.IsActive)&&(guard==null||!guard.IsGuarding);
            // 副手提供独立空闲资格；不认识/修改主手时钟。
            foreach(var candidate in GetComponents<MonoBehaviour>())if(candidate is ICharacterOffhandStatus offhand && offhand.IsAccepting)idle=false;
            var ok=State!=null&&State.TryEquip(kind,idle);LastResult=ok?"装备 "+kind:"拒绝：非空闲";return ok;
        }
    }
}
