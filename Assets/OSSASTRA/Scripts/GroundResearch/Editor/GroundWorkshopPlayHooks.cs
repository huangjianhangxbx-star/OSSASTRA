/* 脚本：GroundWorkshopPlayHooks
 * 功能：为纯工坊提供可选的游戏试玩入口；未安装项目适配时给出不可用状态，不调用不存在菜单。
 * 边界：不引用角色/游戏根/私有媒体，不创建控制器；具体会话与恢复由可选适配器负责。
 */
#if UNITY_EDITOR
using System;
namespace Ossastra.GroundResearch.Editor
{
    public static class GroundWorkshopPlayHooks
    {
        private static Action playCurrentMap,openGameplayLab;
        public static bool IsAvailable=>playCurrentMap!=null&&openGameplayLab!=null;
        public static void Register(Action playCurrent,Action openLab)
        {
            playCurrentMap=playCurrent??throw new ArgumentNullException(nameof(playCurrent));
            openGameplayLab=openLab??throw new ArgumentNullException(nameof(openLab));
        }
        public static bool TryPlayCurrentMap(){if(playCurrentMap==null)return false;playCurrentMap();return true;}
        public static bool TryOpenGameplayLab(){if(openGameplayLab==null)return false;openGameplayLab();return true;}
    }
}
#endif
