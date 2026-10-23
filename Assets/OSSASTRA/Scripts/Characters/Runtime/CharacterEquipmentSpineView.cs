/* 脚本：CharacterEquipmentSpineView
 * 功能：由角色根显式推进两只手的独立Spine表现；身体语义/阶段重播身份映射到装备自身动画。
 * 边界：没有Update/输入/HP/伤害或费用；装备Spine的Event/Complete不接玩法结算。
 * 协作：Root初始化与LateUpdate调用；受理事实冻结动画表，骨骼、插槽、握持点、翻转与排序分别处理。
 */
using System;
using Ossastra.AnimationCore;
using Ossastra.CharacterCore;
using Spine.Unity;
using UnityEngine;
using SemanticIntent=Ossastra.AnimationCore.AnimationIntent;
using PlaybackIdentity=Ossastra.AnimationCore.AnimationPlaybackIdentity;
namespace Ossastra.Characters
{
    [DisallowMultipleComponent]
    public sealed class CharacterEquipmentSpineView:MonoBehaviour
    {
        private CharacterRoot owner;
        private CharacterEquipment equipment;
        private CharacterSpineView bodyView;
        private SkeletonAnimation body;
        private SlotView left,right;
        public string LastResult {get;private set;}="未初始化";
        public SkeletonAnimation LeftSkeleton=>left?.Animation;
        public SkeletonAnimation RightSkeleton=>right?.Animation;
        public int LeftCommitCount=>left?.CommitCount??0;
        public int RightCommitCount=>right?.CommitCount??0;
        public Guid LeftExecutionId=>left?.ExecutionId??Guid.Empty;
        public Guid LeftReplayId=>left?.ReplayId??Guid.Empty;
        public float LeftGripError=>left?.GripError??0;
        public Guid RightExecutionId=>right?.ExecutionId??Guid.Empty;
        public Guid RightReplayId=>right?.ReplayId??Guid.Empty;
        public float RightGripError=>right?.GripError??0;
        public void Initialize(CharacterRoot root)
        {
            if(owner!=null)owner.ActionObserved-=OnAction;
            DisposeViews();owner=root;equipment=root.GetComponent<CharacterEquipment>();
            bodyView=root.GetComponentInChildren<CharacterSpineView>();body=bodyView==null?null:bodyView.GetComponent<SkeletonAnimation>();
            if(body==null)body=root.GetComponentInChildren<SkeletonAnimation>();
            if(equipment==null||bodyView==null||body==null){LastResult="缺少身体/装备端口";return;}
            left=new SlotView("LeftEquipmentSpine");right=new SlotView("RightEquipmentSpine");
            owner.ActionObserved+=OnAction;LastResult="就绪";
        }
        private void OnAction(ActionFact fact)
        {
            if(fact.Kind!=ActionFactKind.Accepted)return;
            // 冻结的是这次执行的动画映射。之后修改SO不会追改已接受动作。
            BindViews();left?.Capture(fact.ActionId);right?.Capture(fact.ActionId);
        }
        private void BindViews()
        {
            if(body?.Skeleton==null||equipment==null)return;
            left?.Bind(equipment.LeftDefinition,body,owner.Definition?.rigProfile);right?.Bind(equipment.RightDefinition,body,owner.Definition?.rigProfile);
        }
        public void TickPresentation()
        {
            if(owner==null||bodyView==null||body==null||body.Skeleton==null)return;
            BindViews();
            var visible=isActiveAndEnabled&&owner.isActiveAndEnabled&&owner.IsAlive;
            var delta=owner.IsSimulationSuspended?0:Time.deltaTime;
            left?.Present(owner,bodyView,delta,visible);right?.Present(owner,bodyView,delta,visible);
            LastResult="左："+left?.Status+"；右："+right?.Status;
        }
        public void Hide(){left?.Hide();right?.Hide();}
        private void OnDisable(){Hide();}
        private void OnDestroy(){if(owner!=null)owner.ActionObserved-=OnAction;DisposeViews();}
        private void DisposeViews(){left?.Dispose();right?.Dispose();left=null;right=null;}

        private sealed class SlotView
        {
            private readonly string objectName;
            private EquipmentDefinitionSO definition;
            private EquipmentSpinePresentationSO profile;
            private EquipmentAnimationCatalog catalog,capturedCatalog;
            private SkeletonAnimation body;
            private Spine.Skeleton boundBody;
            private SkeletonRenderer spineRenderer;
            private MeshRenderer meshRenderer;
            private Renderer bodyRenderer;
            private Spine.Bone bodyBone,grip;
            private Spine.Slot bodySlot;
            private CharacterRigProfileSO rigProfile;
            private ResolvedRigSocket socket;
            private int profileVersion,rigVersion;
            private PlaybackIdentity playback=new PlaybackIdentity();
            private Guid capturedExecution;
            private Vector3 offset;
            private float scale,rotation;
            private int sortingOffset;
            private bool followRotation,slotColor,slotVisibility;
            public SkeletonAnimation Animation {get;private set;}
            public int CommitCount {get;private set;}
            public Guid ExecutionId {get;private set;}
            public Guid ReplayId {get;private set;}
            public float GripError {get;private set;}
            public string Status {get;private set;}="无Spine配置";
            public SlotView(string name){objectName=name;}
            public void Bind(EquipmentDefinitionSO next,SkeletonAnimation nextBody,CharacterRigProfileSO nextRig)
            {
                var nextProfile=next==null?null:next.spinePresentation;
                if(definition==next&&profile==nextProfile&&rigProfile==nextRig&&profileVersion==(nextProfile?.contentVersion??0)&&rigVersion==(nextRig?.contentVersion??0)&&ReferenceEquals(boundBody,nextBody.Skeleton))return;
                Dispose();definition=next;profile=nextProfile;body=nextBody;boundBody=nextBody.Skeleton;rigProfile=nextRig;profileVersion=profile?.contentVersion??0;rigVersion=nextRig?.contentVersion??0;
                capturedCatalog=null;capturedExecution=Guid.Empty;playback=new PlaybackIdentity();
                if(profile==null){Status="无Spine配置（旧表现路径）";return;}
                try
                {
                    catalog=profile.Freeze(body.Skeleton.Data,rigProfile);
                    socket=string.IsNullOrEmpty(profile.semanticSocketId)?null:rigProfile.Resolve(profile.semanticSocketId,body.Skeleton.Data);
                    var child=new GameObject(objectName);child.transform.SetParent(body.transform,false);
                    SkeletonAnimation.AddToGameObject(child,profile.skeletonData);
                    Animation=child.GetComponent<SkeletonAnimation>();spineRenderer=child.GetComponent<SkeletonRenderer>();
                    Animation.UpdateTiming=UpdateTiming.ManualUpdate;Animation.Initialize(false);Animation.Update(0);
                    var boneName=socket?.BoneName??profile.bodyBone;var slotName=socket?.SlotName??profile.bodySlot;
                    bodyBone=body.Skeleton.FindBone(boneName);bodySlot=string.IsNullOrEmpty(slotName)?null:body.Skeleton.FindSlot(slotName);
                    grip=Animation.Skeleton.FindBone(profile.gripBone);meshRenderer=child.GetComponent<MeshRenderer>();bodyRenderer=body.GetComponent<Renderer>();
                    offset=profile.localOffset;scale=profile.localScale;rotation=profile.rotationOffset;sortingOffset=profile.sortingOrderOffset;
                    followRotation=profile.followBoneRotation;slotColor=profile.followSlotColor;slotVisibility=profile.followSlotAttachmentVisibility;
                    Status="独立Spine";
                }
                catch(Exception e){Status="装备表现配置拒绝："+e.Message;Dispose();}
            }
            public void Capture(Guid actionId)
            {
                if(profile==null||body==null)return;
                try{capturedCatalog=profile.Freeze(body.Skeleton.Data,rigProfile);capturedExecution=actionId;}
                catch(Exception e){Status="冻结映射失败："+e.Message;Hide();}
            }
            public void Present(CharacterRoot root,CharacterSpineView view,float delta,bool visible)
            {
                if(Animation==null||bodyBone==null||grip==null)return;
                var slotPose=bodySlot?.AppliedPose;
                var tint=slotColor&&slotPose!=null?slotPose.GetColor():Color.white;
                visible=visible&&tint.a>0&&(!slotVisibility||slotPose==null||slotPose.Attachment!=null);
                meshRenderer.enabled=visible;if(!visible)return;
                var activeCatalog=root.ActionActive&&root.DataBus.ActionId==capturedExecution?capturedCatalog:catalog;
                var binding=activeCatalog?.Find(view.CurrentSemantic);
                if(binding==null){Status="未映射语义："+view.CurrentSemantic;meshRenderer.enabled=false;return;}
                var intent=new SemanticIntent(root.ActorId,view.CurrentSemantic,"equipment",view.CurrentReplayId,root.LifeGeneration);
                if(playback.ShouldCommit(binding.Clip,intent))
                {
                    var track=Animation.AnimationState.SetAnimation(0,binding.Clip,binding.Loop);
                    track.MixDuration=binding.Mix;track.TimeScale=binding.Rate;CommitCount++;
                }
                ExecutionId=view.CurrentSource=="action"?root.DataBus.ActionId:Guid.Empty;ReplayId=view.CurrentReplayId;
                Animation.Skeleton.SetColor(tint*body.Skeleton.GetColor());Animation.Update(delta);
                var pose=bodyBone.AppliedPose;
                // 骨骼矩阵的X轴承载朝向；负行列式单独放在Y轴，避免左右镜像被旋转重复应用。
                var determinant=pose.A*pose.D-pose.B*pose.C;
                var angle=followRotation?(socket?.Rotation(pose)??Mathf.Atan2(pose.C,pose.A)*Mathf.Rad2Deg):0;
                var localScale=followRotation?new Vector3(scale,(determinant<0?-1:1)*scale,1):
                    new Vector3(Mathf.Sign(body.Skeleton.ScaleX)*scale,Mathf.Sign(body.Skeleton.ScaleY)*scale,1);
                // 内部握持骨骼也可带旋转：先逆掉装备握持角，再施加角色挂点及作者偏移。
                var gripAngle=Mathf.Atan2(grip.AppliedPose.C,grip.AppliedPose.A)*Mathf.Rad2Deg;
                var localRotation=Quaternion.Euler(0,0,angle+rotation-Mathf.Sign(localScale.x*localScale.y)*gripAngle);
                var anchor=(socket?.Position(pose)??new Vector3(pose.WorldX,pose.WorldY,0))+new Vector3(offset.x*Mathf.Sign(body.Skeleton.ScaleX),offset.y,offset.z);
                var gripPosition=new Vector3(grip.AppliedPose.WorldX,grip.AppliedPose.WorldY,0);
                var visual=Animation.transform;visual.localRotation=localRotation;visual.localScale=localScale;
                visual.localPosition=anchor-localRotation*Vector3.Scale(localScale,gripPosition);
                GripError=Vector3.Distance(visual.TransformPoint(gripPosition),body.transform.TransformPoint(anchor));
                if(bodyRenderer!=null){meshRenderer.sortingLayerID=bodyRenderer.sortingLayerID;meshRenderer.sortingOrder=bodyRenderer.sortingOrder+sortingOffset;}
                Status="独立Spine / "+binding.Clip;
            }
            public void Hide(){if(meshRenderer!=null)meshRenderer.enabled=false;}
            public void Dispose()
            {
                if(Animation!=null){Animation.gameObject.SetActive(false);UnityEngine.Object.Destroy(Animation.gameObject);}
                Animation=null;spineRenderer=null;meshRenderer=null;bodyBone=null;grip=null;bodySlot=null;socket=null;
            }
        }
    }
}
