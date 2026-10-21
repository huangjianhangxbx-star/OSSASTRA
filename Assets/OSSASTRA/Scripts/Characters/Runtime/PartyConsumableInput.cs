/* 脚本：PartyConsumableInput
 * 功能：场景唯一Y输入；实控转移、暂停与失焦后须重新松键。
 * 边界：只转发真实按键边沿，不保存瓶余额或角色HP。
 */
using UnityEngine;
using UnityEngine.InputSystem;
namespace Ossastra.Characters
{
    [DisallowMultipleComponent]
    public sealed class PartyConsumableInput:MonoBehaviour
    {
        [SerializeField] private HealingBottleService service;
        public InputAction healingAction=new InputAction("SharedHealing",InputActionType.Button);
        private bool armed,focused=true;
        private int revision=-1;
        private void OnEnable(){armed=false;healingAction.Enable();}
        private void Update()
        {
            if(service==null||service.Coordinator==null)return;
            var currentRevision=service.Coordinator.ControlRevision;
            if(revision!=currentRevision){revision=currentRevision;armed=false;return;}
            if(!focused||!service.CanRequest){armed=false;return;}
            if(!healingAction.IsPressed()){armed=true;return;}
            if(armed&&healingAction.WasPressedThisFrame()){armed=false;service.TryUse();}
        }
        private void OnApplicationFocus(bool value){focused=value;armed=false;}
        private void OnApplicationPause(bool value){if(value)armed=false;}
        private void OnDisable(){armed=false;healingAction.Disable();}
        private void OnDestroy(){healingAction.Dispose();}
    }
}
