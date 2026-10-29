/* 脚本：GroundWorkshopContracts
 * 功能：声明工坊地层用途、二维装饰策略与换材范围；枚举不携带输入或地图状态。
 * 边界：这里只是可序列化语义，真实格子归Tilemap，编辑事务归Editor作者服务。
 */
namespace Ossastra.GroundResearch
{
    public enum GroundLayerPurpose { Base, Overlay, Road }
    public enum GroundDecorationMode { Flat, UprightYaw, FixedUpright }
    public enum GroundReplacementScope { FutureOnly, ExistingAndFuture }
}
