/* 脚本：CraftingStation
 * 功能：稳定工位实例和明确交互锚点；由CraftingRoot集中查询，静态工位没有Update。
 * 边界：遮挡消隐只改变表现，不改变真实同层、距离与LOS。只忽略自己的明确Collider。
 */
using System;
using UnityEngine;
using Ossastra.Characters;
namespace Ossastra.Crafting
{
    using CharacterRoot=Ossastra.Characters.CharacterRoot;
    [DisallowMultipleComponent]
    public sealed class CraftingStation:MonoBehaviour
    {
        [Header("场景实例身份 / 不在OnValidate自动生成")]
        public string instanceId,mapId;
        public CraftingStationDefinitionSO definition;
        [Header("明确交互锚点与真实访问范围")]
        public Transform interactionAnchor;
        [Tooltip("可选二维立牌；仅根在镜头朝向变化时更新，自身无Update，不旋转碰撞")]
        public Transform billboardVisual;
        [Min(.1f)] public float interactionDistance=2.6f;
        [Range(.05f,1)] public float maximumHeightDifference=.45f;
        [Min(.1f)] public float sightHeight=.65f;
        public LayerMask interactionLayers=~0,obstacleLayers=~0;
        public Collider[] ownedColliders=Array.Empty<Collider>();
        [Tooltip("可制作开关；禁用组件/物体同样会失效")]
        public bool available=true;
        readonly RaycastHit[] hits=new RaycastHit[64];
        public Vector3 Anchor=>interactionAnchor==null?transform.position:interactionAnchor.position;
        public string CheckAccess(CharacterRoot actor)
        {
            if(!isActiveAndEnabled||!gameObject.activeInHierarchy||!available)return "装备台已停用";
            if(actor==null||!actor.CanSimulateIntent)return "当前角色不能交互";
            if((interactionLayers.value&(1<<gameObject.layer))==0)return "装备台不在可交互层";
            var anchor=Anchor;var delta=anchor-actor.transform.position;
            if(!Finite(delta.x)||!Finite(delta.y)||!Finite(delta.z)||!Finite(interactionDistance)||interactionDistance<.1f||!Finite(maximumHeightDifference)||!Finite(sightHeight))return "装备台访问参数无效";
            if(Mathf.Abs(delta.y)>maximumHeightDifference)return "装备台不在当前角色同一层";
            if(new Vector2(delta.x,delta.z).sqrMagnitude>interactionDistance*interactionDistance)return "请靠近装备台";
            var start=actor.transform.position+Vector3.up*sightHeight;var end=anchor+Vector3.up*sightHeight;var ray=end-start;
            if(ray.sqrMagnitude<.00001f)return null;
            int count=Physics.RaycastNonAlloc(start,ray.normalized,hits,ray.magnitude,obstacleLayers,QueryTriggerInteraction.Ignore);
            if(count==hits.Length)return "装备台视线命中超出预算";
            for(int i=0;i<count;i++)
            {
                var c=hits[i].collider;if(c==null||c.transform.IsChildOf(actor.transform))continue;
                bool own=false;foreach(var candidate in ownedColliders)if(candidate==c){own=true;break;}
                if(!own)return "角色与装备台之间有真实遮挡";
            }
            return null;
        }
        static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
    }
}
