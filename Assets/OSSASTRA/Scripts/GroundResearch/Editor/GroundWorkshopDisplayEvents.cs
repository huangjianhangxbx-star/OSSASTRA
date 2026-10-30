/* 脚本：GroundWorkshopDisplayEvents
 * 功能：为可选显示后端发布真实作者/预览/观察变化，不反向控制作者事务。
 * 边界：无订阅时零行为；订阅者失败仅报告，不把已完成的原生笔画伪装为已回滚。
 */
#if UNITY_EDITOR
using System;
using UnityEngine;
namespace Ossastra.GroundResearch.Editor
{
    public static class GroundWorkshopDisplayEvents
    {
        public static event Action<GroundWorkshopMap> Changed;
        public static void Notify(GroundWorkshopMap map)
        {
            if (map == null || Changed == null) return;
            foreach (Action<GroundWorkshopMap> handler in Changed.GetInvocationList())
                try { handler(map); } catch (Exception e) { Debug.LogWarning("派生地表显示更新失败，原生作者数据保留：" + e.Message, map); }
        }
    }
}
#endif
