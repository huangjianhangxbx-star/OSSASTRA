/* 脚本：CharacterRigProfileSO
 * 功能：将功能语义显式映射到指定模型的真实bone、slot关联骨或point附件；不猜名字、不改商业骨架。
 * 边界：定义只保存绑定，冻结结果供同帧姿态消费者只读；缺必需映射立即给出字段路径。
 * 协作：角色定义引用模型Rig，装备/临时物品通过semanticSocketId消费；版本变更要求重绑定。
 */
using System;
using System.Linq;
using UnityEngine;
using Spine.Unity;
namespace Ossastra.Characters
{
    public enum RigSocketKind { Bone,SlotBone,Point }
    [Serializable]
    public sealed class RigSocketBinding
    {
        public string semanticSocketId;
        public RigSocketKind kind;
        public string boneName,slotName,pointAttachmentName,skinName;
        public Vector3 boneLocalOffset;
        [TextArea] public string evidence;
    }
    [Serializable]
    public sealed class RigSocketVariant
    {
        public string faceId,bodyClip;
        [Range(0,1)] public float fromNormalized;
        [Range(0,1)] public float toNormalized=1;
        public RigSocketBinding binding;
    }
    // 编译时解析名字；换面/阶段只选择已解析的引用，不重建装备或逐帧FindBone。
    public sealed class RigSocketRoute
    {
        private readonly ResolvedRigSocket fallback;
        private readonly RigSocketVariant[] variants;
        private readonly ResolvedRigSocket[] sockets;
        internal RigSocketRoute(ResolvedRigSocket fallback,RigSocketVariant[] variants,ResolvedRigSocket[] sockets)
        {this.fallback=fallback;this.variants=variants;this.sockets=sockets;}
        public ResolvedRigSocket Select(string face,string clip,float normalized)
        {
            for(int i=0;i<variants.Length;i++)
            {
                var rule=variants[i];
                if(rule.faceId==face&&(string.IsNullOrEmpty(rule.bodyClip)||rule.bodyClip==clip)&&normalized>=rule.fromNormalized&&(normalized<rule.toNormalized||rule.toNormalized==1&&normalized==1))return sockets[i];
            }
            return fallback;
        }
    }
    public sealed class ResolvedRigSocket
    {
        public string BoneName {get;}
        public string SlotName {get;}
        public int BoneIndex {get;internal set;}
        public int SlotIndex {get;internal set;}=-1;
        public Spine.PointAttachment Point {get;}
        public Vector3 BoneLocalOffset {get;}
        internal ResolvedRigSocket(string bone,string slot,Spine.PointAttachment point,Vector3 offset)
        {BoneName=bone;SlotName=slot;Point=point;BoneLocalOffset=offset;}
        public Vector3 Position(Spine.BonePose pose)
        {
            float x=pose.WorldX,y=pose.WorldY;if(Point!=null)Point.ComputeWorldPosition(pose,out x,out y);
            return new Vector3(x+pose.A*BoneLocalOffset.x+pose.B*BoneLocalOffset.y,y+pose.C*BoneLocalOffset.x+pose.D*BoneLocalOffset.y,BoneLocalOffset.z);
        }
        public float Rotation(Spine.BonePose pose)=>Point!=null?Point.ComputeWorldRotation(pose):Mathf.Atan2(pose.C,pose.A)*Mathf.Rad2Deg;
    }
    [CreateAssetMenu(menuName="OSSASTRA/Characters/Rig Profile")]
    public sealed class CharacterRigProfileSO:ScriptableObject
    {
        public const int CurrentSchema=1;
        public int schemaVersion=CurrentSchema,contentVersion=1;
        public SkeletonDataAsset model;
        [TextArea] public string evidence;
        public RigSocketBinding[] sockets=Array.Empty<RigSocketBinding>();
        [Tooltip("按顺序匹配：具名动作阶段应放在该面的通用绑定前。空clip匹配该面的所有动作。")]
        public RigSocketVariant[] variants=Array.Empty<RigSocketVariant>();
        public RigSocketRoute CompileRoute(string semantic,Spine.SkeletonData actualModel)
        {
            var fallback=Resolve(semantic,actualModel);
            var selected=(variants??Array.Empty<RigSocketVariant>()).Where(v=>v!=null&&v.binding?.semanticSocketId==semantic).ToArray();
            var resolved=new ResolvedRigSocket[selected.Length];
            for(int i=0;i<selected.Length;i++)
            {
                var rule=selected[i];
                if(string.IsNullOrWhiteSpace(rule.faceId)||!float.IsFinite(rule.fromNormalized)||!float.IsFinite(rule.toNormalized)||rule.fromNormalized<0||rule.toNormalized>1||rule.fromNormalized>rule.toNormalized)throw new ArgumentException(name+".variants["+i+"]: invalid face/phase");
                if(!string.IsNullOrEmpty(rule.bodyClip)&&actualModel.FindAnimation(rule.bodyClip)==null)throw new ArgumentException(name+".variants["+i+"].bodyClip missing");
                resolved[i]=ResolveBinding(rule.binding,actualModel,name+".variants["+i+"]");
            }
            return new RigSocketRoute(fallback,selected,resolved);
        }
        public ResolvedRigSocket Resolve(string semantic,Spine.SkeletonData actualModel)
        {
            string field=name+".sockets["+semantic+"]";
            if(schemaVersion!=CurrentSchema||contentVersion<1||model==null||actualModel==null||!ReferenceEquals(model.GetSkeletonData(false),actualModel))throw new ArgumentException(field+": model/schema mismatch");
            if(sockets==null||sockets.Any(s=>s==null||string.IsNullOrWhiteSpace(s.semanticSocketId))||sockets.Select(s=>s.semanticSocketId).Distinct(StringComparer.Ordinal).Count()!=sockets.Length)throw new ArgumentException(field+": duplicate/missing semanticSocketId");
            var binding=sockets.SingleOrDefault(s=>s.semanticSocketId==semantic);if(binding==null)throw new ArgumentException(field+": required semantic missing");
            return ResolveBinding(binding,actualModel,field);
        }
        private static ResolvedRigSocket ResolveBinding(RigSocketBinding binding,Spine.SkeletonData actualModel,string field)
        {
            if(!Enum.IsDefined(typeof(RigSocketKind),binding.kind)||!Finite(binding.boneLocalOffset))throw new ArgumentException(field+": kind/offset invalid");
            var slot=string.IsNullOrEmpty(binding.slotName)?null:actualModel.FindSlot(binding.slotName);
            if(!string.IsNullOrEmpty(binding.slotName)&&slot==null)throw new ArgumentException(field+".slotName missing: "+binding.slotName);
            var bone=binding.kind==RigSocketKind.Bone?actualModel.FindBone(binding.boneName):slot?.BoneData;
            if(bone==null)throw new ArgumentException(field+".boneName/slotName missing: "+binding.boneName+" / "+binding.slotName);
            Spine.PointAttachment point=null;
            if(binding.kind==RigSocketKind.Point)
            {
                var skin=string.IsNullOrEmpty(binding.skinName)?actualModel.DefaultSkin:actualModel.FindSkin(binding.skinName);
                point=skin?.GetAttachment(slot.Index,binding.pointAttachmentName) as Spine.PointAttachment;
                if(point==null)throw new ArgumentException(field+".pointAttachmentName missing/not Point in skin "+(binding.skinName??"default")+": "+binding.pointAttachmentName);
                point=(Spine.PointAttachment)point.Copy();
            }
            return new ResolvedRigSocket(bone.Name,slot?.Name,point,binding.boneLocalOffset){BoneIndex=bone.Index,SlotIndex=slot?.Index??-1};
        }
        private static bool Finite(Vector3 p)=>!float.IsNaN(p.x)&&!float.IsInfinity(p.x)&&!float.IsNaN(p.y)&&!float.IsInfinity(p.y)&&!float.IsNaN(p.z)&&!float.IsInfinity(p.z);
    }
}
