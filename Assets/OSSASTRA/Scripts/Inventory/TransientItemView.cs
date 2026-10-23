/* 脚本：TransientItemView
 * 功能：监听真实消费释放事实，将定义图标在角色显式Rig挂点短暂展示；由根LateUpdate读取同帧姿态。
 * 边界：表现失败不退款或重复消费；关闭本组件不改变效果，不写共享骨架或材质。
 */
using System;
using UnityEngine;
using Ossastra.Characters;
namespace Ossastra.Inventory
{
    using CharacterRoot=Ossastra.Characters.CharacterRoot;
    using ItemDefinitionSO=Ossastra.Characters.ItemDefinitionSO;
    public sealed class TransientItemView:MonoBehaviour,ICharacterTransientItemPresentation
    {
        [SerializeField] private InventoryItemUse source;
        private CharacterRoot owner;private Spine.Unity.SkeletonAnimation body;private SpriteRenderer visual;
        private ResolvedRigSocket socket;private Spine.Bone bone;private double until;private Guid execution;
        public string LastResult {get;private set;}="无临时物品";
        public Guid ExecutionId=>execution;
        public bool Visible=>visual!=null&&visual.enabled;
        public void Initialize(CharacterRoot root)
        {if(source!=null)source.Released-=OnReleased;owner=root;body=root.GetComponentInChildren<Spine.Unity.SkeletonAnimation>();if(source!=null&&isActiveAndEnabled)source.Released+=OnReleased;}
        private void OnEnable(){if(owner!=null&&source!=null){source.Released-=OnReleased;source.Released+=OnReleased;}}
        private void OnReleased(CharacterRoot actor,Guid id,ItemDefinitionSO definition)
        {
            if(actor!=owner||!isActiveAndEnabled||id==execution||string.IsNullOrEmpty(definition.temporarySocketId)||definition.icon==null)return;
            try
            {
                if(body?.Skeleton==null||owner.Definition?.rigProfile==null)throw new ArgumentException(definition.name+".temporarySocketId missing body/RigProfile");
                socket=owner.Definition.rigProfile.Resolve(definition.temporarySocketId,body.Skeleton.Data);bone=body.Skeleton.FindBone(socket.BoneName);
                if(visual==null){var go=new GameObject("TransientItemSprite");go.transform.SetParent(body.transform,false);visual=go.AddComponent<SpriteRenderer>();}
                execution=id;until=owner.SimulationTime+definition.temporaryVisualSeconds;visual.sprite=definition.icon;visual.transform.localScale=Vector3.one*definition.temporaryVisualScale;
                var renderer=body.GetComponent<Renderer>();if(renderer!=null){visual.sortingLayerID=renderer.sortingLayerID;visual.sortingOrder=renderer.sortingOrder+8;}LastResult="实际释放 #"+id.ToString("N")+" → "+definition.temporarySocketId;
            }
            catch(ArgumentException error){Hide();LastResult=error.Message;}
        }
        public void TickPresentation()
        {
            if(visual==null)return;var show=isActiveAndEnabled&&owner!=null&&owner.IsAlive&&owner.isActiveAndEnabled&&owner.SimulationTime<until&&bone!=null;
            visual.enabled=show;if(!show)return;visual.transform.localPosition=socket.Position(bone.AppliedPose);visual.transform.localRotation=Quaternion.identity;
        }
        public void Hide(){if(visual!=null)visual.enabled=false;until=0;}
        private void OnDisable(){Hide();if(source!=null)source.Released-=OnReleased;}
        private void OnDestroy(){if(source!=null)source.Released-=OnReleased;if(visual!=null)Destroy(visual.gameObject);}
    }
}
