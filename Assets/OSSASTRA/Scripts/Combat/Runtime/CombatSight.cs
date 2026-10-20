/*
 * 脚本：CombatSight
 * 功能：三维世界实体间的中立视线查询，忽略双方本体碰撞，阻挡物不可穿透。
 * 边界：只读物理，不选Current、不扣HP；敌感知和正式近战共用。
 */
using UnityEngine;
namespace Ossastra.Combat
{
    public static class CombatSight
    {
        public static bool IsClear(Transform source, Transform target,float height=.8f)
        {
            if(source==null||target==null)return false;
            var start=source.position+Vector3.up*height;var end=target.position+Vector3.up*height;
            foreach(var hit in Physics.RaycastAll(start,end-start,Vector3.Distance(start,end),~0,QueryTriggerInteraction.Ignore))
                if(!hit.transform.IsChildOf(source)&&!hit.transform.IsChildOf(target))return false;
            return true;
        }
    }
}
