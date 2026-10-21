/* 脚本：CharacterEquipmentView
 * 功能：读取本体装备定义，在真实Spine挂点显示左手装备。
 * 边界：由Root LateUpdate显式推进；不改变附件/装备实例/耐久或技能资格。
 * 协作：Spine Update已求骨骼后读取World坐标；死亡/禁用立即隐藏，返场仍读同一实例。
 */
using Spine.Unity;
using UnityEngine;
namespace Ossastra.Characters
{
    public sealed class CharacterEquipmentView:MonoBehaviour
    {
        private CharacterRoot owner;
        private CharacterEquipment equipment;
        private SkeletonAnimation skeleton;
        private SpriteRenderer renderer;
        private EquipmentDefinitionSO current;
        private Spine.Bone bone;
        public string LastResult {get;private set;}="未初始化";
        public void Initialize(CharacterRoot root)
        {
            owner=root;equipment=GetComponent<CharacterEquipment>();skeleton=GetComponentInChildren<SkeletonAnimation>();
            var child=new GameObject("LeftHandEquipmentVisual");child.transform.SetParent(transform,false);renderer=child.AddComponent<SpriteRenderer>();renderer.enabled=false;
            if(skeleton!=null){var original=skeleton.GetComponent<Renderer>();if(original!=null){renderer.sortingLayerID=original.sortingLayerID;renderer.sortingOrder=original.sortingOrder+3;}}
        }
        public void TickPresentation()
        {
            if(renderer==null||equipment==null||skeleton==null||skeleton.Skeleton==null||owner==null)return;
            var definition=equipment.LeftDefinition;
            if(definition!=current)
            {
                current=definition;bone=current==null?null:skeleton.Skeleton.FindBone(current.boneName);
                renderer.sprite=current==null?null:current.worldSprite;
                LastResult=current==null?"空副手":bone==null?"挂点缺失："+current.boneName:current.displayName;
            }
            renderer.enabled=isActiveAndEnabled&&owner.IsAlive&&owner.isActiveAndEnabled&&bone!=null&&renderer.sprite!=null;
            if(!renderer.enabled)return;
            var sign=skeleton.Skeleton.ScaleX<0?-1f:1f;
            var rotation=skeleton.transform.rotation;
            renderer.transform.position=skeleton.transform.TransformPoint(new Vector3(bone.Pose.WorldX,bone.Pose.WorldY,0))+rotation*new Vector3(current.offset.x*sign,current.offset.y,current.offset.z);
            renderer.transform.rotation=rotation;
            var parent=transform.lossyScale;
            renderer.transform.localScale=new Vector3(sign*current.worldScale/Mathf.Abs(parent.x),current.worldScale/Mathf.Abs(parent.y),current.worldScale/Mathf.Abs(parent.z));
        }
        public void Hide(){if(renderer!=null)renderer.enabled=false;}
        private void OnDisable(){Hide();}
        private void OnDestroy(){if(renderer!=null)Destroy(renderer.gameObject);}
    }
}
