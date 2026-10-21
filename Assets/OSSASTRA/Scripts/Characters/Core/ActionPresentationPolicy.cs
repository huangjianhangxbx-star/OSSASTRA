/* 脚本：ActionPresentationPolicy
 * 功能：区分主动作执行存续与主轨姿势保留；恢复移动后可交给运动表现。
 * 边界：只读动作事实，不取消执行、不开放攻击门、不改变费用或释放水位。
 * 协作：CharacterSpineView在本帧实际运动完成后查询，再由唯一动画仲裁器合成。
 */
namespace Ossastra.CharacterCore
{
    public static class ActionPresentationPolicy
    {
        /// <summary>未到移动恢复门必须保留；已恢复且实际移动时让出主轨，原动作仍自行完成。</summary>
        public static bool RetainMainPose(bool actionActive, bool moveReady, bool isMoving)
            => actionActive && (!moveReady || !isMoving);
    }
}
