/* 脚本：InventoryInteraction
 * 功能：唯一交互输入上下文；散落物优先，其余从商人、合法容器与篝火中选最近项。
 * 边界：一次按键只提交一个命令；打开角色菜单后由UI消费同一输入，不创建第二套F绑定。
 * 协作：距离/LOS/容量仍由会话服务复验，篝火沿原服务，旧场原输入不变。
 */
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Ossastra.Characters;
using Ossastra.UserInterface;
using Ossastra.Commerce.Runtime;
using Ossastra.Commerce.Presentation;
namespace Ossastra.Inventory
{
    public sealed class InventoryInteraction:MonoBehaviour
    {
        [SerializeField] private InventorySession session;
        [SerializeField] private InventoryView view;
        [SerializeField] private PrototypeCampfire campfire;
        [SerializeField] private HealingBottleService healing;
        [SerializeField] private CommerceService commerce;
        [SerializeField] private CommerceUIRoot commerceUi;
        public InputAction interactAction=new InputAction("InventoryInteract",InputActionType.Button,"<Keyboard>/f");
        private bool focused=true;
        private readonly DeviceActionSampler sampler=new DeviceActionSampler();
        public string Prompt {get;private set;}="";
        private void OnEnable(){interactAction.Enable();sampler.Suppress();}
        private void Start(){if(campfire!=null)campfire.SetContextOwner(this);}
        private void Update()
        {
            if(session==null||!session.Ready||view==null)return;
            var actor=session.Coordinator.Current;
            if(actor==null||!focused){sampler.Suppress();Prompt="";return;}
            var layout=actor.GetComponent<CharacterInputHandler>().inputLayout;
            sampler.Synchronize(layout==null?new[]{interactAction}:new[]{interactAction}.Concat(layout.ConfiguredActions()),null,null);
            var pressed=sampler.Devices.Any(d=>d.Pressed(interactAction)&&!d.ConsumedByComposite(interactAction));
            // 商店捕获会关闭角色玩家源，但不应同时切断这个已打开窗口自己的确认入口。
            if(commerceUi!=null&&commerceUi.IsOpen){Prompt="";if(pressed)commerceUi.TryConfirmInteraction();return;}
            if(!actor.GetComponent<CharacterInputHandler>().HasPlayerInput||!actor.CanSimulateIntent||EventSystem.current?.currentSelectedGameObject?.GetComponent<InputField>()?.isFocused==true){sampler.Suppress();Prompt="";return;}
            var nearest=session.AccessibleExternal.OrderBy(id=>Vector3.SqrMagnitude((session.ContainerPosition(id)??actor.transform.position)-actor.transform.position)).ToArray();
            var manual=nearest.FirstOrDefault(id=>id.StartsWith("drop:manual:"));
            var merchant=commerce==null?null:commerce.NearestTarget();
            var merchantDistance=merchant==null?float.PositiveInfinity:(merchant.transform.position-actor.transform.position).sqrMagnitude;
            var containerDistance=nearest.Length==0?float.PositiveInfinity:(session.ContainerPosition(nearest[0]).Value-actor.transform.position).sqrMagnitude;
            var fireAvailable=campfire!=null&&campfire.CanRefill(healing);
            var fireDistance=fireAvailable?(campfire.transform.position-actor.transform.position).sqrMagnitude:float.PositiveInfinity;
            var useFire=fireAvailable&&fireDistance<containerDistance&&fireDistance<merchantDistance;
            var useMerchant=merchant!=null&&merchantDistance<=containerDistance&&merchantDistance<=fireDistance;
            Prompt=manual!=null?"交互 拾取":useFire?"交互 篝火补给":useMerchant?"交互 "+merchant.displayName:nearest.Length>0?"交互 打开容器":"";
            if(!pressed)return;
            // 优先散落条目；容器/篝火分支互斥，绝不对同一次F继续向后派发。
            if(manual!=null)
            {
                var item=session.Ledger.Items.FirstOrDefault(i=>i.Location.ContainerId==manual);if(item==null)return;
                var result=session.Move(session.Ledger.Plan(item.Id,"team"));view.Feedback(result.Ok?"已拾取 "+session.Definition(item.DefinitionId).displayName:result.Reason);session.ReclaimEmptyGround();return;
            }
            if(useFire){healing.TryRefill(campfire);view.Feedback(healing.LastResult);return;}
            if(useMerchant){commerce.OpenRole(merchant);return;}
            if(nearest.Length>0){view.OpenExternal(nearest[0]);return;}
        }
        private void OnApplicationFocus(bool value){focused=value;sampler.Suppress();}
        private void OnDisable(){interactAction.Disable();sampler.Dispose();}
        private void OnDestroy(){if(campfire!=null)campfire.SetContextOwner(null);sampler.Dispose();interactAction.Dispose();}
    }
}


