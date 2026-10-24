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
using System.Linq;
using System.Collections.Generic;
using A=Ossastra.CharacterAnimation;
using SemanticIntent=Ossastra.AnimationCore.AnimationIntent;
using PlaybackIdentity=Ossastra.AnimationCore.AnimationPlaybackIdentity;
namespace Ossastra.Characters
{
    [DisallowMultipleComponent]
    public sealed class CharacterEquipmentSpineView:MonoBehaviour
    {
        private CharacterRoot owner;
        private CharacterEquipment equipment;
        private ICharacterAnimationStateView bodyView;
        private CharacterAbilities abilities;
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
        public string LeftAnchorBone=>left?.AnchorBone;
        public string RightAnchorBone=>right?.AnchorBone;
        public void Initialize(CharacterRoot root)
        {
            if(owner!=null)owner.ActionObserved-=OnAction;
            if(abilities!=null)abilities.Feedback-=OnAbilityFeedback;
            DisposeViews();owner=root;equipment=root.GetComponent<CharacterEquipment>();
            bodyView=root.GetComponentsInChildren<MonoBehaviour>().OfType<ICharacterAnimationStateView>().FirstOrDefault();body=bodyView?.Skeleton;
            if(body==null)body=root.GetComponentInChildren<SkeletonAnimation>();
            if(equipment==null||bodyView==null||body==null){LastResult="缺少身体/装备端口";return;}
            left=new SlotView("LeftEquipmentSpine",false);right=new SlotView("RightEquipmentSpine",true);
            abilities=root.GetComponent<CharacterAbilities>();if(abilities!=null)abilities.Feedback+=OnAbilityFeedback;
            owner.ActionObserved+=OnAction;LastResult="就绪";
        }
        private void OnAction(ActionFact fact)
        {
            if(fact.Kind!=ActionFactKind.Accepted)return;
            // 冻结的是这次执行的动画映射。之后修改SO不会追改已接受动作。
            BindViews();right?.Capture(fact.ActionId);
        }
        private void OnAbilityFeedback(Guid id,string kind,Vector3 position)
        {
            if(kind!="accepted"||abilities==null)return;
            BindViews();
            if(abilities.TryGetHandPresentation(false,out var leftId,out _,out _)&&leftId==id)left?.Capture(id);
            if(abilities.TryGetHandPresentation(true,out var rightId,out _,out _)&&rightId==id)right?.Capture(id);
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
        private void OnDestroy(){if(owner!=null)owner.ActionObserved-=OnAction;if(abilities!=null)abilities.Feedback-=OnAbilityFeedback;DisposeViews();}
        private void DisposeViews(){left?.Dispose();right?.Dispose();left=null;right=null;}

        private sealed class SlotView
        {
            private readonly string objectName;
            private readonly bool main;
            private A.AnimationHandleSystem handle;
            private A.CharacterAnimationComposer composer;
            private A.CharacterAnimationConfigManager config;
            private readonly Dictionary<A.EAnimationLogicState,A.CharacterAnimationPlan> capturedPlans=new Dictionary<A.EAnimationLogicState,A.CharacterAnimationPlan>();
            private int bindingGeneration;
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
            private RigSocketRoute socketRoute;
            private CharacterDirectionPresentation direction;
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
            public string AnchorBone=>bodyBone?.Data.Name;
            public string Status {get;private set;}="无Spine配置";
            public SlotView(string name,bool main){objectName=name;this.main=main;}
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
                    socketRoute=string.IsNullOrEmpty(profile.semanticSocketId)?null:rigProfile.CompileRoute(profile.semanticSocketId,body.Skeleton.Data);
                    direction=body.GetComponent<CharacterDirectionPresentation>();
                    var child=new GameObject(objectName);child.transform.SetParent(body.transform,false);
                    SkeletonAnimation.AddToGameObject(child,profile.skeletonData);
                    Animation=child.GetComponent<SkeletonAnimation>();spineRenderer=child.GetComponent<SkeletonRenderer>();
                    Animation.UpdateTiming=UpdateTiming.ManualUpdate;Animation.Initialize(false);Animation.Update(0);
                    handle=child.AddComponent<A.AnimationHandleSystem>();config=child.AddComponent<A.CharacterAnimationConfigManager>();composer=child.AddComponent<A.CharacterAnimationComposer>();
                    config.baseMap=profile.animationMap;bindingGeneration++;
                    var boneName=socket!=null?socket.BoneName:profile.bodyBone;var slotName=socket!=null?socket.SlotName:profile.bodySlot;
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
                capturedPlans.Clear();
                if(profile.animationMap!=null)foreach(A.EAnimationLogicState logic in Enum.GetValues(typeof(A.EAnimationLogicState)))
                {var plan=profile.animationMap.GetDefault(logic);if(plan!=null)capturedPlans[logic]=plan;}
            }
            public void Present(CharacterRoot root,ICharacterAnimationStateView view,float delta,bool visible)
            {
                if(Animation==null||bodyBone==null||grip==null)return;
                if(socketRoute!=null)
                {
                    var track=body.AnimationState.GetTrack(0);
                    float normalized=track?.Animation==null||track.Animation.Duration<=0?0:Mathf.Clamp01(track.TrackTime/track.Animation.Duration);
                    socket=socketRoute.Select(direction?.FaceId,track?.Animation?.Name,normalized);
                    bodyBone=body.Skeleton.Bones.Items[socket.BoneIndex];
                    bodySlot=socket.SlotIndex<0?null:body.Skeleton.Slots.Items[socket.SlotIndex];
                }
                var slotPose=bodySlot?.AppliedPose;
                var tint=slotColor&&slotPose!=null?slotPose.GetColor():Color.white;
                visible=visible&&tint.a>0&&(!slotVisibility||slotPose==null||slotPose.Attachment!=null);
                bool renderEquipment=visible&&(direction==null||direction.IndependentEquipmentVisible&&direction.IsAnchorVisible(bodyBone));
                meshRenderer.enabled=renderEquipment;if(!visible)return;
                var module=root.GetComponent<CharacterAbilities>();
                bool own=module!=null&&module.TryGetHandPresentation(main,out _,out _,out _);
                Guid execution=Guid.Empty;bool guarding=false,usesBody=false;
                if(own)module.TryGetHandPresentation(main,out execution,out guarding,out usesBody);
                if(own&&usesBody&&!ActionPresentationPolicy.RetainMainPose(root.ActionActive,root.DataBus.MoveReady,root.DataBus.IsMoving)&&!root.DataBus.AbilityMotionActive)own=false;
                bool mainPose=main&&root.ActionActive&&view.CurrentSource=="action"&&
                    (!(view is Ossastra.CharacterAnimation.CharacterAnimationController controller)||controller.CapturedSource==Ossastra.CharacterAnimation.AnimationIntentSourceStateMachine.MainHand);
                var semantic=own?(guarding?AnimationSemantic.Guard:AnimationSemantic.BasicAttack):mainPose?view.CurrentSemantic:
                    view.CurrentSemantic==AnimationSemantic.BasicAttack||view.CurrentSemantic==AnimationSemantic.BasicAttackSecond?(root.DataBus.IsMoving?AnimationSemantic.Move:AnimationSemantic.Idle):view.CurrentSemantic;
                ExecutionId=own?execution:mainPose?root.DataBus.ActionId:Guid.Empty;
                var bodyController=view as A.CharacterAnimationController;
                ReplayId=ExecutionId!=Guid.Empty&&bodyController!=null&&bodyController.CapturedActionId==ExecutionId?bodyController.CapturedPhaseId:ExecutionId;
                if(semantic==AnimationSemantic.Dodge&&ExecutionId==Guid.Empty)ReplayId=view.CurrentReplayId;
                var logic=own&&!guarding?A.EAnimationLogicState.Ability:Logic(semantic);
                var source=main?A.AnimationIntentSourceStateMachine.MainHand:A.AnimationIntentSourceStateMachine.OffHand;
                if(profile.animationMap!=null)
                {
                    if(!handle.Bind(root,Animation,objectName))return;
                    config.Initialize(root);composer.Initialize(root,config,handle,objectName,_=>definition);
                    A.CharacterAnimationPlan frozen=null;
                    if(ExecutionId!=Guid.Empty&&capturedExecution==ExecutionId)capturedPlans.TryGetValue(logic,out frozen);
                    composer.SubmitIntent(new A.AnimationIntent{root=root,actorId=root.ActorId,target=objectName,intentSourceStateMatine=source,
                        semantic=own&&!guarding?A.EAnimSemantic.Ability:A.AnimationSemanticTranslation.FromShared(semantic),logicId=logic,priority=10,
                        lifeGeneration=root.LifeGeneration,presentationGeneration=bindingGeneration,actionId=ExecutionId,phaseId=ReplayId,
                        configurationVersion=config.ConfigurationVersion,oneShot=ExecutionId!=Guid.Empty,frozenPlan=frozen});
                    composer.Commit(bindingGeneration);CommitCount=handle.CommitCount;
                }
                else
                {
                    // 历史未迁移资产的只读适配也交同一Handle，装备不再直接SetAnimation。
                    var activeCatalog=ExecutionId!=Guid.Empty&&capturedExecution==ExecutionId?capturedCatalog:catalog;
                    var binding=activeCatalog?.Find(semantic);if(binding==null){Status="未映射语义："+semantic;meshRenderer.enabled=false;return;}
                    handle.Bind(root,Animation,objectName);
                    var plan=new A.CharacterAnimationPlan();plan.clips.Add(new A.CharacterAnimationClip{animaName=binding.Clip,loop=binding.Loop,mixDuration=binding.Mix,playbackRate=binding.Rate,
                        runtimeActor=root.ActorId,runtimeTarget=objectName,runtimeSourceStateMachine=source,runtimeActionId=ExecutionId,runtimePhaseId=ReplayId,runtimeGeneration=bindingGeneration,runtimeLifeGeneration=root.LifeGeneration});
                    handle.ApplyPlan(plan,bindingGeneration);CommitCount=handle.CommitCount;
                }
                Animation.Skeleton.SetColor(tint*body.Skeleton.GetColor());Animation.Update(delta);
                handle.FlushSignals();
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
                Status="独立Spine / "+Animation.AnimationState.GetTrack(0)?.Animation?.Name;
            }
            private static A.EAnimationLogicState Logic(AnimationSemantic semantic)
            {
                switch(semantic){case AnimationSemantic.Move:return A.EAnimationLogicState.Jog;case AnimationSemantic.Guard:return A.EAnimationLogicState.Guard;
                    case AnimationSemantic.Dodge:return A.EAnimationLogicState.Dodge;case AnimationSemantic.HitReaction:return A.EAnimationLogicState.HitReact;
                    case AnimationSemantic.PostureBroken:return A.EAnimationLogicState.Stagger;case AnimationSemantic.Death:return A.EAnimationLogicState.Death;
                    case AnimationSemantic.BasicAttack:case AnimationSemantic.BasicAttackSecond:return A.EAnimationLogicState.Ability;default:return A.EAnimationLogicState.Idle;}
            }
            public void Hide(){if(meshRenderer!=null)meshRenderer.enabled=false;composer?.ClearAllIntents();handle?.ClearAll();}
            public void Dispose()
            {
                if(Animation!=null){Animation.gameObject.SetActive(false);UnityEngine.Object.Destroy(Animation.gameObject);}
                Animation=null;spineRenderer=null;meshRenderer=null;bodyBone=null;grip=null;bodySlot=null;socket=null;socketRoute=null;direction=null;
            }
        }
    }
}
