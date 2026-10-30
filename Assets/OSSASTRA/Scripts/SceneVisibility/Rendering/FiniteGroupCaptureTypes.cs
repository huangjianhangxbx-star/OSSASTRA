/*
 * 功能：定义检测器交给渲染后端的固定组、双圆及支撑保护参数。
 * 边界：不读取角色、射线或场景；圆心沿用WorldToViewportPoint坐标，半径为实际像素。
 * 生命周期：调用者复用最多四项的数组；几何／材质变化通过GeometryVersion显式更新。
 */
using UnityEngine;

namespace Ossastra.SceneVisibility.Rendering
{
    /// <summary>Camera.WorldToViewportPoint convention; radii are actual camera viewport pixels.</summary>
    public struct CaptureCircle
    {
        public Vector2 CenterViewport;
        public float InnerPixels;
        public float OuterPixels;
        public float Strength;
    }

    /// <summary>Detector-owned fixed frame array. Members are explicit renderers, never discovered actors.</summary>
    public struct RegisteredGroupFrame
    {
        public int StableId;
        public int GeometryVersion;
        public Renderer[] Members;
        public CaptureCircle CircleA;
        public CaptureCircle CircleB;
        public CaptureCircle PreserveSupport;
        public float WholeFade;
        // Authored group floor, enforced after edge styling. Zero preserves existing callers.
        public float MinimumOpacity;
    }
}
