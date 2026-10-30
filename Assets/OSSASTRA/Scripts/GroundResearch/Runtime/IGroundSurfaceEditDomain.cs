/*
 * 脚本：IGroundSurfaceEditDomain
 * 功能：为原地表工具提供可选的非矩形物理域检查，不依赖高台实现。
 * 边界：只判断落笔/装饰位置，不保存第二套格子或执行渲染；未安装实现时保持旧矩形路径。
 * 协作：Brush/装饰服务使用同一归属判定，边界外的历史作者数据不自动清除。
 */
using UnityEngine;
using UnityEngine.Tilemaps;
namespace Ossastra.GroundResearch
{
    public interface IGroundSurfaceEditDomain
    {
        GroundResearchSurface Surface { get; }
        float DecorationInset { get; }
        bool ContainsWorldPoint(Vector3 position, float inset = 0f);
        bool IntersectsCell(Tilemap tilemap, Vector3Int cell);
    }
    public static class GroundSurfaceEditDomain
    {
        public static IGroundSurfaceEditDomain Find(GroundResearchSurface surface)
        {
            if (surface == null) return null;
            IGroundSurfaceEditDomain result = null;
            foreach (var component in surface.GetComponentsInParent<MonoBehaviour>(true))
            {
                if (!(component is IGroundSurfaceEditDomain candidate) || candidate.Surface != surface) continue;
                if (result != null) throw new System.InvalidOperationException("同一作者Surface有多个物理域；拒绝猜测当前平台。");
                result = candidate;
            }
            return result;
        }
    }
}
