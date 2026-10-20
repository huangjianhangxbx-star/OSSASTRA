/*
 * 脚本：CapsuleRespawnPlacement
 * 功能：检查本层地面、真实胶囊阻挡与同批次间距，返回安全返场位置。
 * 边界：只查询物理世界，不移动实体；失败保持pending，不使用原点兜底。
 */
using System.Collections.Generic;
using UnityEngine;
namespace Ossastra.Characters
{
    public interface IRespawnPlacement
    {
        bool TryFind(CharacterController body, Vector3 checkpoint, IReadOnlyList<Vector3> reserved, out Vector3 position);
    }
    public sealed class CapsuleRespawnPlacement : MonoBehaviour, IRespawnPlacement
    {
        [SerializeField] private LayerMask environment = ~0;
        [SerializeField, Min(.01f)] private float sameLayerTolerance=.2f;
        [SerializeField, Min(.1f)] private float searchSpacing=.8f;
        public bool TryFind(CharacterController body, Vector3 checkpoint, IReadOnlyList<Vector3> reserved, out Vector3 position)
        {
            position=checkpoint;
            if(body==null) return false;
            // 本工程角色胶囊保持竖直；拒绝不支持的倾斜/非等比配置，不能假装验证通过。
            var scale=body.transform.lossyScale;
            if(Vector3.Dot(body.transform.up,Vector3.up)<.999f || Mathf.Abs(scale.x-scale.z)>.001f) return false;
            var radius=body.radius*Mathf.Abs(scale.x);
            var height=Mathf.Max(body.height*Mathf.Abs(scale.y),radius*2);
            for(var i=0;i<9;i++)
            {
                var offset=i==0?Vector3.zero:new Vector3(Mathf.Cos((i-1)*Mathf.PI/4),0,Mathf.Sin((i-1)*Mathf.PI/4))*searchSpacing;
                var candidate=checkpoint+offset;
                if(!Physics.Raycast(candidate+Vector3.up*.5f,Vector3.down,out var floor,1f,environment,QueryTriggerInteraction.Ignore)) continue;
                if(Mathf.Abs(floor.point.y-checkpoint.y)>sameLayerTolerance || floor.normal.y<.7f) continue;
                candidate.y=floor.point.y+.03f;
                var center=candidate+Vector3.Scale(body.center,scale);
                var a=center+Vector3.up*(height*.5f-radius);
                var b=center-Vector3.up*(height*.5f-radius);
                var blocked=false;
                foreach(var collider in Physics.OverlapCapsule(a,b,radius+.01f,environment,QueryTriggerInteraction.Ignore))
                    if(collider!=null && !collider.transform.IsChildOf(body.transform)) { blocked=true; break; }
                if(blocked) continue;
                foreach(var other in reserved)
                    if((other-candidate).sqrMagnitude<Mathf.Pow(height+radius,2)) {blocked=true;break;}
                if(blocked) continue;
                position=candidate; return true;
            }
            return false;
        }
    }
}
