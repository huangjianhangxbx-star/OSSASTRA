/*
 * 脚本：CharacterOrientation
 * 功能：统一解析攻击候选、最近移动意图和最终显示朝向；键鼠普通移动/攻击与手柄移动定向明确分开。
 * 边界：只读会话有效帧与原动作事实；不采设备、不计时/受理/改写已经发出的攻击快照。
 * 协作：请求前解析玩法方向，动作/运动后决议显示；两个模型经Bus读取同一政策结果。
 */
using UnityEngine;
using System;
using Ossastra.CharacterCore;

namespace Ossastra.Characters
{
    [DisallowMultipleComponent]
    public sealed class CharacterOrientation : MonoBehaviour, ICharacterOrientation
    {
        public Vector3 AttackDirection { get; private set; } = Vector3.right;
        public Vector3 PresentationDirection { get; private set; } = Vector3.right;
        public Vector3 LastMoveDirection { get; private set; } = Vector3.right;
        public string DirectionReason { get; private set; } = "出生朝向";
        public float VisualFacingX { get; private set; } = 1;
        public bool UsesMovementAim => movementAim;
        private bool movementAim;
        private Guid releasedAction;
        private Vector3 releaseDirection;

        public void ResolveDirections(CharacterInputFrame frame, Vector3? pointerDirection, bool movementFacing, bool playerControl)
        {
            if (!playerControl) movementAim=false;
            else if (frame.aimSource!=PlayerInputSource.None) movementAim=frame.aimSource==PlayerInputSource.Gamepad;
            var move=new Vector3(frame.move.x,0,frame.move.y);
            if(TryNormalize(move,out var validMove))LastMoveDirection=validMove;
            else if(movementAim&&playerControl&&frame.hasDirectionalAim&&
                TryNormalize(new Vector3(frame.aimDirection.x,0,frame.aimDirection.y),out var rememberedMove))
                LastMoveDirection=rememberedMove; // 会话已过滤右杆的最近移动方向；切人后也沿同一操作上下文。
            if(movementAim&&playerControl)
            {
                // 意图即方向：受攻击门/碰墙而没有实际速度，仍可确定下一次攻击。
                TrySetAttackDirection(LastMoveDirection);
            }
            else
            {
                var pointerValid=pointerDirection.HasValue&&TrySetAttackDirection(pointerDirection.Value);
                if(!pointerValid&&!TrySetAttackDirection(frame.hasDirectionalAim?new Vector3(frame.aimDirection.x,0,frame.aimDirection.y):Vector3.zero))
                    TrySetAttackDirection(move);
            }
        }

        /// <summary>新投递采样；已发出的投递持有值快照。具名强制机动和非手柄保持受理方向。</summary>
        public Vector3 ResolveDeliveryDirection(Vector3 acceptedDirection,bool frozenMotion)
            =>movementAim&&!frozenMotion?AttackDirection:acceptedDirection;
        public void ObserveActionFact(ActionFact fact)
        {
            if(fact.Kind==ActionFactKind.Accepted)releasedAction=Guid.Empty;
            if(fact.Release!=null){releasedAction=fact.ActionId;releaseDirection=AttackDirection;}
            if((fact.Kind==ActionFactKind.Finished||fact.Kind==ActionFactKind.Cancelled)&&releasedAction==fact.ActionId)releasedAction=Guid.Empty;
        }

        public void ResolvePresentation(CharacterDataBus bus,ICharacterAbilityController abilities,bool playerControl)
        {
            if(bus==null)return;
            var move=new Vector3(bus.Input.move.x,0,bus.Input.move.y);
            var motion=bus.MotionOverride;
            // 单次闪避是既有强制机动，不用当前摇杆把已接受的闪避反向。
            if(motion is ICharacterMotionAnimation&&motion.IsActive&&TryNormalize(motion.MotionVelocity,out var dodge))
            {SetPresentation(dodge,"闪避受理方向");return;}
            bool retain=ActionPresentationPolicy.RetainMainPose(bus.ActionActive,bus.MoveReady,bus.IsMoving)||bus.AbilityMotionActive;
            var source=abilities as ICharacterActionDirectionSource;
            if(movementAim&&playerControl)
            {
                if(retain&&source!=null&&source.TryGetLockedMotionDirection(out var forced))SetPresentation(forced,"具名机动锁向（移动受理）");
                else SetPresentation(LastMoveDirection,"手柄移动/最近移动");
                return;
            }
            if(abilities is Ossastra.Combat.ICombatGuard guard&&guard.IsGuarding)
            {SetPresentation(guard.GuardDirection,"持续防御方向");return;}
            if(retain&&source!=null&&source.TryGetBodyActionDirection(out var ability))
            {SetPresentation(ability,"具名动作受理锁向");return;}
            if(retain&&bus.ActionActive)
            {
                SetPresentation(releasedAction==bus.ActionId?releaseDirection:AttackDirection,releasedAction==bus.ActionId?"实际释放方向":"攻击前摇方向");
                return;
            }
            if(TryNormalize(move,out var movement))SetPresentation(movement,playerControl?"键鼠移动":"AI移动");
            else DirectionReason="保留末显示方向";
        }

        private void SetPresentation(Vector3 direction,string reason)
        {
            if(!TryNormalize(direction,out var value))return;
            PresentationDirection=value;DirectionReason=reason;
            if(Mathf.Abs(value.x)>.01f)VisualFacingX=Mathf.Sign(value.x);
        }

        public void ResolveDirections(Vector2 moveInput, Vector3? pointerDirection, Vector2 stickDirection, bool movementFacing)
        {
            // 历史调用适配；正式状态机使用包含有效来源的Frame入口。
            var pointerValid = pointerDirection.HasValue && TrySetAttackDirection(pointerDirection.Value);
            if (!pointerValid && !TrySetAttackDirection(new Vector3(stickDirection.x, 0, stickDirection.y)))
                TrySetAttackDirection(new Vector3(moveInput.x, 0, moveInput.y));
            if (movementFacing && Mathf.Abs(moveInput.x) > .01f) VisualFacingX = Mathf.Sign(moveInput.x);
        }

        public void OrientTowards(Vector3 worldPosition) { TrySetAttackDirection(worldPosition - transform.position); }

        /// <summary>本体生命循环清除死亡前的朝向残留，后续正常输入重新采样。</summary>
        public void ResetOrientation()
        {AttackDirection=PresentationDirection=LastMoveDirection=Vector3.right;VisualFacingX=1;movementAim=false;releasedAction=Guid.Empty;DirectionReason="出生朝向";}

        public void FaceAttackDirection()
        {
            SetPresentation(AttackDirection,"动作接受/释放");
        }

        private bool TrySetAttackDirection(Vector3 direction)
        {
            if(!TryNormalize(direction,out var value))return false;
            AttackDirection=value;return true;
        }
        private static bool TryNormalize(Vector3 direction,out Vector3 value)
        {
            value=Vector3.zero;direction.y = 0;
            if (float.IsNaN(direction.x) || float.IsNaN(direction.z) || float.IsInfinity(direction.x) ||
                float.IsInfinity(direction.z) || direction.sqrMagnitude < .0001f) return false;
            value = direction.normalized;
            return true;
        }
    }
}
