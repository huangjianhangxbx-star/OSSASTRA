/* 脚本：EquipmentSpinePresentationSO
 * 功能：独立装备Spine、身体骨骼/插槽、装备握持点与语义动画的作者配置。
 * 边界：不含角色输入、生命值或能力；映射在动作受理时冻结，运行中不回写SO。
 */
using System;
using System.Collections.Generic;
using Ossastra.AnimationCore;
using Spine.Unity;
using UnityEngine;
namespace Ossastra.Characters
{
    [Serializable]
    public sealed class EquipmentSpineAnimationBinding
    {
        public AnimationSemantic semantic;
        public string clip;
        public bool loop;
        [Min(0)] public float mixDuration=.08f;
        [Min(.01f)] public float playbackRate=1;
    }
    [CreateAssetMenu(menuName="OSSASTRA/Equipment/Spine Presentation")]
    public sealed class EquipmentSpinePresentationSO:ScriptableObject
    {
        public SkeletonDataAsset skeletonData;
        [Tooltip("正式原式装备动画映射；旧animations只供未迁移资产读取。")]
        public Ossastra.CharacterAnimation.AnimationPlanMappingSO animationMap;
        [Min(1)] public int contentVersion=1;
        [Tooltip("非空时从角色RigProfile解析，旧bodyBone字段仅保留历史场兼容。")] public string semanticSocketId;
        [Tooltip("角色的真实骨骼；不使用寻找第一个骨骼的隐式兜底。")] public string bodyBone;
        [Tooltip("可选；提供颜色/显隐依据，空表示显式不跟随插槽。")] public string bodySlot;
        [Tooltip("装备模型自身握持骨骼；动画后将此点对齐身体挂点。")] public string gripBone="grip";
        public bool followBoneRotation=true,followSlotColor=true,followSlotAttachmentVisibility;
        public Vector3 localOffset;
        [Min(.001f)] public float localScale=1;
        public float rotationOffset;
        public int sortingOrderOffset=3;
        [TextArea] public string presentationEvidence;
        [Sirenix.OdinInspector.HideIf("@animationMap != null")]
        public EquipmentSpineAnimationBinding[] animations=Array.Empty<EquipmentSpineAnimationBinding>();
        public EquipmentAnimationCatalog Freeze(Spine.SkeletonData body,CharacterRigProfileSO rig=null)
        {
            if(skeletonData==null)throw new ArgumentException(name+".skeletonData missing");
            var data=skeletonData.GetSkeletonData(false);
            var resolved=string.IsNullOrEmpty(semanticSocketId)?null:rig!=null?rig.Resolve(semanticSocketId,body):throw new ArgumentException(name+".semanticSocketId requires CharacterRigProfileSO: "+semanticSocketId);
            var boneName=resolved?.BoneName??bodyBone;var slotName=resolved?.SlotName??bodySlot;
            if(data==null||body==null||string.IsNullOrEmpty(boneName)||body.FindBone(boneName)==null)
                throw new ArgumentException(name+".bodyBone missing: "+boneName);
            if(string.IsNullOrEmpty(gripBone)||data.FindBone(gripBone)==null)throw new ArgumentException(name+".gripBone missing: "+gripBone);
            if(!string.IsNullOrEmpty(slotName)&&body.FindSlot(slotName)==null)throw new ArgumentException(name+".bodySlot missing: "+slotName);
            if(!Finite(localScale)||localScale<=0||!Finite(localOffset.x)||!Finite(localOffset.y)||!Finite(localOffset.z)||!Finite(rotationOffset))
                throw new ArgumentException(name+".mount transform invalid");
            var bindings=new List<EquipmentAnimationClip>();
            if(animationMap!=null)
            {
                foreach(AnimationSemantic semantic in Enum.GetValues(typeof(AnimationSemantic)))
                {
                    var logic=semantic==AnimationSemantic.Idle?Ossastra.CharacterAnimation.EAnimationLogicState.Idle:
                        semantic==AnimationSemantic.Move?Ossastra.CharacterAnimation.EAnimationLogicState.Jog:
                        semantic==AnimationSemantic.Guard?Ossastra.CharacterAnimation.EAnimationLogicState.Guard:
                        semantic==AnimationSemantic.Dodge?Ossastra.CharacterAnimation.EAnimationLogicState.Dodge:
                        semantic==AnimationSemantic.HitReaction?Ossastra.CharacterAnimation.EAnimationLogicState.HitReact:
                        semantic==AnimationSemantic.PostureBroken?Ossastra.CharacterAnimation.EAnimationLogicState.Stagger:
                        semantic==AnimationSemantic.Death?Ossastra.CharacterAnimation.EAnimationLogicState.Death:Ossastra.CharacterAnimation.EAnimationLogicState.Ability;
                    var plan=animationMap.GetDefault(logic);var clip=plan?.clips.Find(c=>c!=null&&c.track==0);
                    if(clip==null||data.FindAnimation(clip.animaName)==null)throw new ArgumentException(name+".animationMap missing semantic/clip: "+semantic);
                    bindings.Add(new EquipmentAnimationClip(semantic,clip.animaName,clip.loop,clip.mixDuration,clip.playbackRate));
                }
            }
            else foreach(var entry in animations??Array.Empty<EquipmentSpineAnimationBinding>())
            {
                if(entry==null||data.FindAnimation(entry.clip)==null)throw new ArgumentException(name+".animations missing clip: "+entry?.clip);
                bindings.Add(new EquipmentAnimationClip(entry.semantic,entry.clip,entry.loop,entry.mixDuration,entry.playbackRate));
            }
            var frozen=new EquipmentAnimationCatalog(bindings);
            // 当前候选每种身体语义均要求显式映射。复用可以配置，但不能静默回退idle。
            foreach(AnimationSemantic semantic in Enum.GetValues(typeof(AnimationSemantic)))
                if(frozen.Find(semantic)==null)throw new ArgumentException(name+".animations missing semantic: "+semantic);
            return frozen;
        }
        private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    }
}
