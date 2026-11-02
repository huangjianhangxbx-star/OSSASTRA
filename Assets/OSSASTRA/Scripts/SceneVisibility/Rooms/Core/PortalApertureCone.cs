/*
 * 功能：从实际原点与凸门窗孔洞生成三维射线锥及目标侧近面，供人物观察与相机窗景分别使用。
 * 边界：只描述真实开口几何，不附加朝向、远距、房间传播、渲染像素或业务授权。
 * 生命周期：无状态纯运算；结果为本次调用独占的新数组，不持有引擎对象或跨帧历史。
 */
using System.Collections.Generic;

namespace Ossastra.SceneVisibility.Rooms.Core
{
    public static class PortalApertureCone
    {
        public static bool TryBuild(SightVector3 origin, IReadOnlyList<SightVector3> aperture,
            SightVector3 travelNormal, out SightPlane[] planes)
        {
            planes = new SightPlane[0];
            if (!origin.IsFinite || !PortalSightGeometry.ValidatePolygon(aperture, travelNormal, out SightVector3 center)) return false;
            SightVector3 travel = travelNormal.Normalized;
            if (SightVector3.Dot(origin - center, travel) >= -PortalSightGeometry.Epsilon) return false;
            var result = new SightPlane[aperture.Count + 1];
            for (int edge = 0; edge < aperture.Count; edge++)
            {
                SightVector3 a = aperture[edge] - origin;
                SightVector3 b = aperture[(edge + 1) % aperture.Count] - origin;
                SightVector3 normal = SightVector3.Cross(a, b).Normalized;
                if (normal.Length < PortalSightGeometry.Epsilon) return false;
                if (SightVector3.Dot(normal, center - origin) < 0) normal = -normal;
                result[edge] = new SightPlane(normal, -SightVector3.Dot(normal, origin));
            }
            // 源侧和孔洞本身不获许可；相机窗景也必须落在真实孔洞后方。
            result[aperture.Count] = new SightPlane(travel, -SightVector3.Dot(travel, center) - PortalSightGeometry.Epsilon);
            planes = result;
            return true;
        }
    }
}
