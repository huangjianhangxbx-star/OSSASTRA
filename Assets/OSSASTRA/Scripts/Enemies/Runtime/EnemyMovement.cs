/*
 * 脚本：EnemyMovement
 * 功能：敌方物理位移适配；由根显式Tick，回报碰撞后的实际速度。
 * 边界：不选择目标/Current，不直接改Transform，不自动寻路。
 */
using UnityEngine;
namespace Ossastra.Enemies
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class EnemyMovement : MonoBehaviour,IEnemyMovement
    {
        [SerializeField,Min(0)] private float speed=1.8f,groundStick=2;
        private CharacterController body;
        private Ossastra.Combat.CombatStatusEffects statusEffects;
        public Vector3 Velocity { get; private set; }
        public bool HasMovementRequest { get; private set; }
        private void Awake(){body=GetComponent<CharacterController>();statusEffects=GetComponent<Ossastra.Combat.CombatStatusEffects>();}
        public void Stop(){Velocity=Vector3.zero;HasMovementRequest=false;}
        public void Tick(Vector3 direction,float delta)
        {
            Stop();if(!isActiveAndEnabled||body==null||!body.enabled||delta<=0)return;
            direction.y=0;var before=transform.position;
            HasMovementRequest=direction.sqrMagnitude>.01f;
            var velocity=Vector3.ClampMagnitude(direction,1)*speed;
            if(statusEffects!=null)velocity=statusEffects.HasForcedMotion?statusEffects.ForcedVelocity:
                statusEffects.BlocksIntent?Vector3.zero:velocity*statusEffects.SpeedScale;
            var collision=body.Move((velocity+Vector3.down*groundStick)*delta);
            if((collision&CollisionFlags.Sides)!=0&&statusEffects!=null&&statusEffects.HasForcedMotion)statusEffects.StopForcedMotion();
            var displacement=transform.position-before;displacement.y=0;Velocity=displacement/delta;
        }
    }
}
