/* 脚本：CharacterDirectionPresentation
 * 功能：消费本体有效方向/已接受动作事实，提交原式结构动画意图，并在4.3姿态流水线执行每实例显隐。
 * 边界：无Update/设备采样/玩法计时器/SetAnimation；不改动作向量、碰撞、费用或共享骨架数据。
 * 协作：Controller先提交动作再提交方向并唯一Commit；BeforeApply恢复mask及拥有的根平移，UpdateComplete求显隐，网格后还原alpha。
 */
using System;
using System.Linq;
using Spine;
using Spine.Unity;
using UnityEngine;
using Ossastra.CharacterCore;
using A=Ossastra.CharacterAnimation;
namespace Ossastra.Characters
{
    [DisallowMultipleComponent]
    public sealed class CharacterDirectionPresentation:MonoBehaviour,ICharacterModelDirectionState
    {
        [SerializeField] private CharacterWeaponDisplayMode weaponDisplayMode;
        [SerializeField,Tooltip("研究诊断：展示全部分支。正常试玩必须关闭。")]
        private bool showAllBranches;
        private CharacterRoot owner;
        private SkeletonAnimation body;
        private A.CharacterAnimationConfigManager config;
        private A.CharacterAnimationComposer composer;
        private CharacterDirectionProfileSO profile;
        private Skeleton boundSkeleton;
        private Bone[] roots=Array.Empty<Bone>();
        private CharacterSlotVisibility visibility;
        private CharacterDirectionSelection selection;
        private Guid observedAction;
        private Vector3 releasedDirection;
        private bool released,hooked;
        private int profileVersion,life=-1;
        public string FaceId=>selection.FaceId;
        public bool Mirrored=>selection.Mirrored;
        public int Revision {get;private set;}
        public string DirectionSource {get;private set;}="initial";
        public int HiddenSlotCount=>visibility?.HiddenCount??0;
        public bool IsConfigured=>isActiveAndEnabled&&profile!=null&&profile.Enabled;
        public bool IndependentEquipmentVisible=>!IsConfigured||weaponDisplayMode==CharacterWeaponDisplayMode.IndependentEquipment;
        public CharacterDirectionProfileSO Profile=>profile;
        public bool Initialize(CharacterRoot root,SkeletonAnimation skeleton,A.CharacterAnimationConfigManager animationConfig)
        {
            Detach();owner=root;body=skeleton;config=animationConfig;composer=GetComponent<A.CharacterAnimationComposer>();
            profile=owner?.Definition?.directionProfile;
            if(profile==null||!profile.Enabled)return true;
            if(body?.Skeleton==null||composer==null)return false;
            profile.Validate(body.Skeleton.Data,config);profileVersion=profile.contentVersion;
            selection=profile.Select(Vector3.zero,default);CacheSkeleton();
            owner.ActionObserved+=Observe;Attach();return true;
        }
        private void CacheSkeleton()
        {
            visibility?.Restore();boundSkeleton=body.Skeleton;
            roots=profile.faces.Select(f=>boundSkeleton.FindBone(f.rootBone)).ToArray();
            visibility=new CharacterSlotVisibility(boundSkeleton,profile.boundarySlot,profile.builtInWeaponSlots);Revision++;
        }
        private void Observe(ActionFact fact)
        {
            if(fact.Kind==ActionFactKind.Accepted){observedAction=fact.ActionId;released=false;}
            if(fact.Release!=null){observedAction=fact.ActionId;releasedDirection=owner.DataBus.AttackDirection;released=true;}
        }
        public Vector3 ReadGameplayDirection()
        {
            var bus=owner?.DataBus;if(bus==null){DirectionSource="initial";return Vector3.zero;}
            var abilities=owner.Abilities;
            if(abilities is Ossastra.Combat.ICombatGuard guard&&guard.IsGuarding){DirectionSource="guard-direction";return guard.GuardDirection;}
            // 身体能力的朝向取已接受上下文；前摇后退等反向位移不应把攻击面翻过去。
            if(abilities is ICharacterActionDirectionSource source&&source.TryGetBodyActionDirection(out var direction)){DirectionSource="accepted-ability";return direction;}
            var motion=bus.MotionOverride;
            if(motion!=null&&motion.IsActive&&motion.MotionVelocity.sqrMagnitude>.0001f){DirectionSource="accepted-motion";return motion.MotionVelocity;}
            if(bus.ActionActive&&bus.ActionId==observedAction&&released&&ActionPresentationPolicy.RetainMainPose(bus.ActionActive,bus.MoveReady,bus.IsMoving))
            {DirectionSource="actual-release";return releasedDirection;}
            DirectionSource="effective-aim";return bus.AttackDirection;
        }
        public void SubmitDirection(A.CharacterAnimationComposer target,Vector3 direction,int generation)
        {
            if(!IsConfigured){target.RemoveIntent(A.AnimationIntentSourceStateMachine.StructuralDirection);return;}
            if(life!=owner.LifeGeneration){life=owner.LifeGeneration;selection=default;released=false;Revision++;}
            if(!ReferenceEquals(boundSkeleton,body.Skeleton))CacheSkeleton();
            if(profileVersion!=profile.contentVersion){profile.Validate(body.Skeleton.Data,config);profileVersion=profile.contentVersion;CacheSkeleton();}
            var next=profile.Select(direction,selection);
            if(next.FaceId!=selection.FaceId||next.Mirrored!=selection.Mirrored){selection=next;Revision++;}
            target.SubmitIntent(new A.AnimationIntent{root=owner,actorId=owner.ActorId,target="body",lifeGeneration=owner.LifeGeneration,presentationGeneration=generation,
                intentSourceStateMatine=A.AnimationIntentSourceStateMachine.StructuralDirection,layer=A.AnimationLayer.StructuralModel,semantic=A.EAnimSemantic.ModelDirection,
                logicId=selection.LogicId,structureProfile=profile,priority=100,replayId=Revision,configurationVersion=unchecked(config.ConfigurationVersion*31+profileVersion)});
        }
        private void BeforeApply(ISkeletonAnimation component)
        {
            if(!IsConfigured)return;
            visibility?.Restore();
            // 方向层只拥有这些根的X/Y。其余旋转/附件/约束仍交身体动画，绝不全身SetupPose。
            foreach(var bone in roots){bone.Pose.X=bone.Data.GetSetupPose().X;bone.Pose.Y=bone.Data.GetSetupPose().Y;}
        }
        private void PoseComplete(ISkeletonRenderer renderer)
        {
            if(!IsConfigured)return;
            UnityEngine.Profiling.Profiler.BeginSample("OSSASTRA.Direction.Visibility");
            try{visibility.Apply(weaponDisplayMode==CharacterWeaponDisplayMode.IndependentEquipment,showAllBranches);}
            finally{UnityEngine.Profiling.Profiler.EndSample();}
        }
        private void AfterMesh(ISkeletonRenderer renderer)=>visibility?.Restore();
        private void AnimationRebuilt(ISkeletonAnimation component)
        {
            if(!IsConfigured)return;
            CacheSkeleton();
            // 只重建原式表现计划，不推进非零delta；重建首帧不能闪出阵列。
            if(owner?.DataBus!=null)GetComponent<A.CharacterAnimationController>()?.Present();
        }
        public bool IsAnchorVisible(Bone bone)=>!IsConfigured||showAllBranches||visibility.Contains(bone);
        private void Attach()
        {
            if(hooked||body==null||profile==null||!profile.Enabled)return;
            body.BeforeApply+=BeforeApply;body.UpdateComplete+=PoseComplete;body.OnMeshAndMaterialsUpdated+=AfterMesh;body.OnAnimationRebuild+=AnimationRebuilt;hooked=true;
        }
        private void Detach()
        {
            visibility?.Restore();if(owner!=null)owner.ActionObserved-=Observe;
            if(body!=null&&hooked){body.BeforeApply-=BeforeApply;body.UpdateComplete-=PoseComplete;body.OnMeshAndMaterialsUpdated-=AfterMesh;body.OnAnimationRebuild-=AnimationRebuilt;}hooked=false;
        }
        private void OnEnable(){if(owner!=null){owner.ActionObserved-=Observe;owner.ActionObserved+=Observe;Attach();}}
        private void OnDisable(){Detach();composer?.RemoveIntent(A.AnimationIntentSourceStateMachine.StructuralDirection);}
        private void OnDestroy(){Detach();}
    }
}
