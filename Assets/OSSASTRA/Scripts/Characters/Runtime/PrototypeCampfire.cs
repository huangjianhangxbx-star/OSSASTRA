/* 脚本：PrototypeCampfire
 * 功能：新试验场的近距离、存活、脱战首次补瓶入口。
 * 边界：G为本场独立样板键，不订阅/消费NpcRoot交互，不代表正式篝火或世界存档。
 */
using UnityEngine;
using UnityEngine.InputSystem;
using Ossastra.Combat;
namespace Ossastra.Characters
{
    public sealed class PrototypeCampfire:MonoBehaviour
    {
        [SerializeField] private HealingBottleService service;
        [SerializeField] private CombatEngagementSession engagement;
        [SerializeField,Min(.1f)] private float radius=1.5f;
        public InputAction interactAction=new InputAction("SampleCampfire",InputActionType.Button);
        private bool armed;
        private object contextOwner;
        public void SetContextOwner(object owner){contextOwner=owner;armed=false;}
        public bool CanRefill(HealingBottleService requester)=>isActiveAndEnabled&&requester==service&&service!=null&&service.CanRequest&&
            engagement!=null&&engagement.IsAvailable&&!engagement.Engaged&&Vector3.Distance(service.Coordinator.Current.transform.position,transform.position)<=radius;
        private void OnEnable(){armed=false;interactAction.Enable();}
        private void Update(){if(contextOwner!=null)return;if(!CanRefill(service)){armed=false;return;}if(!interactAction.IsPressed()){armed=true;return;}if(armed&&interactAction.WasPressedThisFrame()){armed=false;service.TryRefill(this);}}
        private void OnDisable(){armed=false;interactAction.Disable();}
        private void OnDestroy(){interactAction.Dispose();}
    }
}
