/* 脚本：ShieldGuard
 * 功能：本体方向盾挡、首次/持续费用和真实盾挡磨损；只由Root Tick。
 * 边界：不读设备、不写HP/架势，不改变主手资格；耗尽必须松键才可再次举盾。
 */
using System;
using Ossastra.CharacterCore;
using Ossastra.Combat;
using UnityEngine;
namespace Ossastra.Characters
{
    [DisallowMultipleComponent]
    public sealed class ShieldGuard:MonoBehaviour,ICombatGuard
    {
        [SerializeField,Min(0)] private float startCost=8,holdCostPerSecond=6;
        [SerializeField,Range(0,180)] private float frontAngle=120;
        [SerializeField,Range(0,1)] private float postureScale=.5f;
        private CharacterRoot owner;
        private CharacterStamina stamina;
        private CharacterEquipment equipment;
        private bool guarding,armed;
        public bool IsGuarding=>guarding&&isActiveAndEnabled&&owner!=null&&owner.CanSimulateIntent&&
            equipment!=null&&equipment.isActiveAndEnabled&&equipment.State.LeftKind==OffhandKind.Shield&&equipment.State.Durability>0;
        public Vector3 GuardDirection=>owner==null||owner.DataBus==null?Vector3.zero:owner.DataBus.AttackDirection;
        public double MinimumDot=>Mathf.Cos(frontAngle*.5f*Mathf.Deg2Rad);
        public double PostureScale=>postureScale;
        public event Action<HitOutcome> Guarded;
        public string LastResult {get;private set;}="未举盾";
        public void Initialize(CharacterRoot root,CharacterStamina resource,CharacterEquipment gear){owner=root;stamina=resource;equipment=gear;}
        public void Tick(bool held,bool playerAllowed,float delta)
        {
            if(!held){guarding=false;armed=true;return;}
            var valid=isActiveAndEnabled&&owner!=null&&owner.CanSimulateIntent&&playerAllowed&&equipment!=null&&equipment.isActiveAndEnabled&&
                equipment.State.LeftKind==OffhandKind.Shield&&equipment.State.Durability>0&&stamina!=null&&
                (owner.DataBus.MotionOverride==null||!owner.DataBus.MotionOverride.IsActive);
            if(!valid){Cancel();return;}
            if(guarding)
            {if(!stamina.Drain(holdCostPerSecond*delta,owner.SimulationTime)){Cancel();LastResult="体力耗尽，需松键";}return;}
            if(!armed)return;armed=false;
            var request=owner.Actions.CreateRequest(CharacterActionKind.Guard,CharacterActionSource.Player);
            if(owner.Actions.TryCommit(request,startCost,()=>{guarding=true;return request.RequestId;},out var reason))LastResult="举盾 SAMPLE";
            else LastResult="拒绝："+reason;
        }
        public void OnGuarded(HitOutcome outcome)
        {if(IsGuarding&&outcome.TargetId==owner.ActorId&&equipment.State.Wear(outcome.ActionId)){LastResult="Guarded #"+outcome.ActionId.ToString("N");Guarded?.Invoke(outcome);if(equipment.State.Durability==0)Cancel();}}
        public void Cancel(){guarding=false;armed=false;}
        private void OnDisable(){Cancel();}
    }
}
