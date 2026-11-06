/* 功能：只读提供游戏镜头显示资格，独立于地图研究实现与渲染后端。
 * 边界：不授予房间观察权；消费者可仅暂停显示，不能改变控制/碰撞/时间。
 */
namespace Ossastra.Characters.ViewSpace
{
    public interface ISceneRenderView { bool GameplayRenderingAllowed { get; } }
}
