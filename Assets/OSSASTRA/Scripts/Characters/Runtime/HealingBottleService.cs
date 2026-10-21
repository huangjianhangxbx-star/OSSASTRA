/* 脚本：HealingBottleService
 * 功能：场景唯一共享治疗余额；验证当前实控资格后调用纯事务。
 * 边界：不绑定按键、不拥有角色生命和返场；同场死亡不重建余额。
 */
using System.Linq;
using UnityEngine;
using Ossastra.CharacterCore;
namespace Ossastra.Characters
{
    [DisallowMultipleComponent]
    public sealed class HealingBottleService:MonoBehaviour
    {
        [SerializeField] private CharacterControlCoordinator coordinator;
        [SerializeField] private ItemDefinitionSO definition;
        public ItemDefinitionSO Definition=>definition;
        private readonly HealingBottleState state=new HealingBottleState();
        public int Count=>state.Count;
        public int UseCount=>state.UseCount;
        public bool Refilled=>state.Refilled;
        public CharacterControlCoordinator Coordinator=>coordinator;
        public string LastResult {get;private set;}="共享Y 1/1";
        public bool CanRequest=>isActiveAndEnabled&&coordinator!=null&&coordinator.IsIntentSamplingAllowed&&coordinator.Current!=null&&coordinator.Current.HasPlayerInput&&coordinator.Current.CanSimulateIntent;
        private void OnEnable(){if(FindObjectsOfType<HealingBottleService>().Any(s=>s!=this&&s.isActiveAndEnabled)){Debug.LogError("Duplicate healing bottle service",this);enabled=false;}}
        public bool TryUse(){if(!CanRequest){LastResult="拒绝：实控资格";return false;}var ok=state.TryUse(coordinator.Characters.Where(c=>c!=null&&c.IsAlive).Select(c=>c.Vitals.Health),definition==null?100:definition.healAmount);LastResult=ok?"Y +"+(definition==null?100:definition.healAmount)+"（存活成员，上限截断）":"拒绝：无瓶或活体全满";return ok;}
        public bool TryRefill(PrototypeCampfire campfire){if(!CanRequest||campfire==null||!campfire.CanRefill(this))return false;var ok=state.TryRefill();LastResult=ok?"首次篝火补至1瓶":"拒绝：已经补给或已有瓶";return ok;}
    }
}
