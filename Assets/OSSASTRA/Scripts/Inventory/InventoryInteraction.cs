/* 脚本：InventoryInteraction
 * 功能：本候选唯一F上下文：散落物优先，其余从合法容器与篝火中选最近项。
 * 边界：一次按键只提交一个命令；不替商人/对话占位，不创建物品或补瓶余额。
 * 协作：距离/LOS/容量仍由会话服务复验，篝火沿原服务，旧场原输入不变。
 */
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Ossastra.Characters;
using Ossastra.UserInterface;
namespace Ossastra.Inventory
{
    public sealed class InventoryInteraction:MonoBehaviour
    {
        [SerializeField] private InventorySession session;
        [SerializeField] private InventoryView view;
        [SerializeField] private PrototypeCampfire campfire;
        [SerializeField] private HealingBottleService healing;
        public InputAction interactAction=new InputAction("InventoryInteract",InputActionType.Button,"<Keyboard>/f");
        private bool armed,focused=true;
        public string Prompt {get;private set;}="";
        private void OnEnable(){interactAction.Enable();armed=false;}
        private void Start(){if(campfire!=null)campfire.SetContextOwner(this);}
        private void Update()
        {
            if(session==null||!session.Ready||view==null)return;
            var actor=session.Coordinator.Current;
            if(actor==null||!actor.CanSimulateIntent||!focused||EventSystem.current?.currentSelectedGameObject?.GetComponent<InputField>()?.isFocused==true){armed=false;Prompt="";return;}
            var nearest=session.AccessibleExternal.OrderBy(id=>Vector3.SqrMagnitude((session.ContainerPosition(id)??actor.transform.position)-actor.transform.position)).ToArray();
            var manual=nearest.FirstOrDefault(id=>id.StartsWith("drop:manual:"));
            var fireAvailable=campfire!=null&&campfire.CanRefill(healing);
            var useFire=fireAvailable&&(nearest.Length==0||Vector3.SqrMagnitude(campfire.transform.position-actor.transform.position)<Vector3.SqrMagnitude(session.ContainerPosition(nearest[0]).Value-actor.transform.position));
            Prompt=manual!=null?"F 拾取":useFire?"F 篝火补给":nearest.Length>0?"F 打开容器":"";
            if(!interactAction.IsPressed()){armed=true;return;}
            if(!armed||!interactAction.WasPressedThisFrame())return;armed=false;
            // 优先散落条目；容器/篝火分支互斥，绝不对同一次F继续向后派发。
            if(manual!=null)
            {
                var item=session.Ledger.Items.FirstOrDefault(i=>i.Location.ContainerId==manual);if(item==null)return;
                var result=session.Move(session.Ledger.Plan(item.Id,"team"));view.Feedback(result.Ok?"已拾取 "+session.Definition(item.DefinitionId).displayName:result.Reason);session.ReclaimEmptyGround();return;
            }
            if(useFire){healing.TryRefill(campfire);view.Feedback(healing.LastResult);return;}
            if(nearest.Length>0){view.OpenExternal(nearest[0]);return;}
        }
        private void OnApplicationFocus(bool value){focused=value;armed=false;}
        private void OnDisable(){interactAction.Disable();armed=false;}
        private void OnDestroy(){if(campfire!=null)campfire.SetContextOwner(null);interactAction.Dispose();}
    }
}
