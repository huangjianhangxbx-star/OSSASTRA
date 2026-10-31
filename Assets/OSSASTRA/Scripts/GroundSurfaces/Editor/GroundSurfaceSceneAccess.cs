/* 脚本：GroundSurfaceSceneAccess
 * 功能：集中声明高台作者服务仅接受的确切样板路径，以及原位保存资格。
 * 边界：路径白名单不是Map所有权证明；调用方仍必须验证实际活动Scene、注册资产与独占目录。
 * 协作：保留旧Authoring.ScenePath供原builder使用；另存副本回调必须先比较源/目标，不能保存源Map。
 */
#if UNITY_EDITOR
using System;
namespace Ossastra.GroundSurfaces.Editor
{
    public static class GroundSurfaceSceneAccess
    {
        public const string VisibilityLabScenePath="Assets/OSSASTRA/GroundWorkshop/Samples/KT-VIS-01/SC_OcclusionVisibilityLab.unity";
        public const string WorldPropsScenePath="Assets/OSSASTRA/GroundWorkshop/Samples/WORLD-PROP-01/SC_WorldPropsIntegration.unity";
        public const string WorldPropsTransitScenePath="Assets/OSSASTRA/GroundWorkshop/Samples/WORLD-PROP-01/SC_WorldPropsTransit.unity";
        public static bool IsSupported(string path)=>
            string.Equals(path,GroundSurfaceAuthoring.ScenePath,StringComparison.Ordinal)||
            string.Equals(path,VisibilityLabScenePath,StringComparison.Ordinal)||
            string.Equals(path,WorldPropsScenePath,StringComparison.Ordinal)||
            string.Equals(path,WorldPropsTransitScenePath,StringComparison.Ordinal);
        public static bool IsInPlaceSave(string scenePath,string destinationPath)=>
            IsSupported(scenePath)&&string.Equals(scenePath,destinationPath,StringComparison.Ordinal);
    }
}
#endif
