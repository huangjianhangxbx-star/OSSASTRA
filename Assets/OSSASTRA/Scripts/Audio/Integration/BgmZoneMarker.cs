/* 脚本：BgmZoneMarker
 * 功能：读取本场自有区域几何与当前主控位置，转送远室事实。
 * 边界：无原型x>20全局规则、不移动角色；禁用只撤自己的区域贡献。
 */
using UnityEngine;
using Ossastra.Characters;
namespace Ossastra.Audio
{
    public sealed class BgmZoneMarker:MonoBehaviour
    {
        [SerializeField] private BgmContextHost host;
        [SerializeField] private CharacterControlCoordinator coordinator;
        [SerializeField] private Vector2 size=new Vector2(8,8);
        private void Update()
        {
            if(host==null)return;
            var actor=coordinator==null?null:coordinator.Current;
            var p=actor==null?Vector3.positiveInfinity:transform.InverseTransformPoint(actor.transform.position);
            host.SetFarZone(this,Mathf.Abs(p.x)<=size.x*.5f&&Mathf.Abs(p.z)<=size.y*.5f);
        }
        private void OnDisable(){if(host!=null)host.SetFarZone(this,false);}
        private void OnDrawGizmosSelected(){Gizmos.color=Color.cyan;Gizmos.matrix=transform.localToWorldMatrix;Gizmos.DrawWireCube(Vector3.zero,new Vector3(size.x,.1f,size.y));}
    }
}
