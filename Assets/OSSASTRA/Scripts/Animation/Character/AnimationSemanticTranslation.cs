/* 脚本：AnimationSemanticTranslation
 * 功能：将角色细粒度动画意图映射到共享观察语义，保留角色内部墙边意图身份。
 */
using Ossastra.AnimationCore;
namespace Ossastra.CharacterAnimation
{
    public static class AnimationSemanticTranslation
    {
        public static EAnimSemantic FromShared(AnimationSemantic value)
        {
            switch(value)
            {
                case AnimationSemantic.Idle:return EAnimSemantic.Idle;
                case AnimationSemantic.Move:return EAnimSemantic.Jog;
                case AnimationSemantic.BasicAttack:return EAnimSemantic.Attack_Normal_1;
                case AnimationSemantic.BasicAttackSecond:return EAnimSemantic.Attack_Normal_2;
                case AnimationSemantic.Guard:return EAnimSemantic.Guard;
                case AnimationSemantic.Dodge:return EAnimSemantic.Dodge;
                case AnimationSemantic.HitReaction:return EAnimSemantic.HitReact;
                case AnimationSemantic.PostureBroken:return EAnimSemantic.Stagger;
                case AnimationSemantic.Death:return EAnimSemantic.Death;
                default:return EAnimSemantic.None;
            }
        }
        public static AnimationSemantic ToShared(EAnimSemantic value)
        {
            switch(value)
            {
                case EAnimSemantic.Idle:return AnimationSemantic.Idle;
                case EAnimSemantic.Jog:case EAnimSemantic.Walk:case EAnimSemantic.Dash:case EAnimSemantic.JogFast:
                case EAnimSemantic.WallSlide:case EAnimSemantic.LedgeHang:case EAnimSemantic.LedgeClimb:return AnimationSemantic.Move;
                case EAnimSemantic.Attack_Normal_2:return AnimationSemantic.BasicAttackSecond;
                case EAnimSemantic.Guard:return AnimationSemantic.Guard;
                case EAnimSemantic.Dodge:return AnimationSemantic.Dodge;
                case EAnimSemantic.HitReact:return AnimationSemantic.HitReaction;
                case EAnimSemantic.Stagger:return AnimationSemantic.PostureBroken;
                case EAnimSemantic.Death:return AnimationSemantic.Death;
                case EAnimSemantic.Attack_Normal_1:case EAnimSemantic.Attack_Normal_3:case EAnimSemantic.Attack_Normal_4:case EAnimSemantic.Ability:return AnimationSemantic.BasicAttack;
                default:throw new System.ArgumentOutOfRangeException(nameof(value),value,"无共享语义对应，不能按整数强转");
            }
        }
    }
}
