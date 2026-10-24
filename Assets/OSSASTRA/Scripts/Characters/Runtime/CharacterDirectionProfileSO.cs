/* 脚本：CharacterDirectionProfileSO
 * 功能：声明模型方向面、XZ坐标变换、闭区间优先表和每实例显隐来源；具体动画仍在原Mapping中。
 * 边界：SO不保存当前面/镜像；零输入沿用调用者的合法结果，不量化玩法方向。
 * 协作：DirectionPresentation消费已筛选事实；Composer只允许此Profile声明的结构逻辑与轨道。
 */
using System;
using System.Linq;
using UnityEngine;
using A=Ossastra.CharacterAnimation;
namespace Ossastra.Characters
{
    public enum CharacterDirectionMode { SingleFlip, MultiFace }
    public enum CharacterWeaponDisplayMode { SourceReference, IndependentEquipment }
    [Serializable] public sealed class CharacterDirectionFace
    {
        public string faceId,rootBone;
        public A.EAnimationLogicState logicId;
    }
    [Serializable] public sealed class CharacterDirectionSector
    {
        public float from,to;
        public string faceId;
        public bool mirrored;
    }
    public readonly struct CharacterDirectionSelection
    {
        public string FaceId {get;}
        public bool Mirrored {get;}
        public A.EAnimationLogicState LogicId {get;}
        public CharacterDirectionSelection(string id,bool mirrored,A.EAnimationLogicState logic){FaceId=id;Mirrored=mirrored;LogicId=logic;}
    }
    [CreateAssetMenu(menuName="OSSASTRA/Characters/Direction Profile")]
    public sealed class CharacterDirectionProfileSO:ScriptableObject
    {
        public CharacterDirectionMode mode;
        [Min(1)] public int contentVersion=1,structureTrack=9;
        [Tooltip("世界XZ角度：atan2(x,z)，再加此偏移；不是屏幕坐标。")]
        public float yawOffset;
        public string initialFace,boundarySlot;
        public CharacterDirectionFace[] faces=Array.Empty<CharacterDirectionFace>();
        public CharacterDirectionSector[] sectors=Array.Empty<CharacterDirectionSector>();
        public string[] builtInWeaponSlots=Array.Empty<string>();
        [TextArea] public string evidence;
        public bool Enabled=>mode==CharacterDirectionMode.MultiFace;
        public CharacterDirectionSelection Select(Vector3 direction,CharacterDirectionSelection previous)
        {
            if(!Finite(direction.x)||!Finite(direction.z)||direction.x*direction.x+direction.z*direction.z<.0001f)
            {
                var last=FindFace(previous.FaceId);
                if(last!=null)return new CharacterDirectionSelection(last.faceId,previous.Mirrored,last.logicId);
                var initial=FindFace(initialFace);
                if(initial==null)throw new ArgumentException(name+".initialFace missing");
                return new CharacterDirectionSelection(initial.faceId,false,initial.logicId);
            }
            float angle=Mathf.Repeat(Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg+yawOffset,360);
            foreach(var sector in sectors)
            {
                if(sector==null)continue;
                bool inside=sector.from<=sector.to?angle>=sector.from&&angle<=sector.to:angle>=sector.from||angle<=sector.to;
                if(!inside)continue;
                var face=FindFace(sector.faceId);
                if(face==null)throw new ArgumentException(name+".sectors face missing: "+sector.faceId);
                return new CharacterDirectionSelection(face.faceId,sector.mirrored,face.logicId);
            }
            throw new ArgumentException(name+".sectors do not cover angle "+angle);
        }
        private CharacterDirectionFace FindFace(string id)
        {if(faces!=null)foreach(var face in faces)if(face!=null&&face.faceId==id)return face;return null;}
        public bool Authorizes(A.EAnimationLogicState logic,int track)
        {
            if(!Enabled||structureTrack<1||track!=structureTrack||faces==null)return false;
            foreach(var face in faces)if(face!=null&&face.logicId==logic)return true;
            return false;
        }
        public void Validate(Spine.SkeletonData data,A.CharacterAnimationConfigManager config)
        {
            if(!Enabled)return;
            if(data==null||config?.baseMap==null||contentVersion<1||structureTrack<1||!Finite(yawOffset)||faces==null||sectors==null||builtInWeaponSlots==null||faces.Length==0||sectors.Length==0)
                throw new ArgumentException(name+": invalid direction profile/model");
            if(faces.Any(f=>f==null||string.IsNullOrEmpty(f.faceId)||data.FindBone(f.rootBone)==null)||faces.Select(f=>f.faceId).Distinct().Count()!=faces.Length||faces.Select(f=>f.rootBone).Distinct().Count()!=faces.Length)
                throw new ArgumentException(name+".faces: duplicate id/root or missing bone");
            if(sectors.Any(s=>s==null||!Finite(s.from)||!Finite(s.to)||s.from<0||s.from>=360||s.to<0||s.to>360||FindFace(s.faceId)==null))throw new ArgumentException(name+".sectors: invalid angles/face");
            foreach(var face in faces)
            {
                var plan=config.baseMap.GetDefault(face.logicId);
                var clip=plan?.clips?.SingleOrDefault();
                if(clip==null||clip.track!=structureTrack||clip.mixDuration!=0||clip.scalesWithAction||data.FindAnimation(clip.animaName)?.Duration!=0||plan.queuedClips.Count!=0||plan.clearTracks.Count!=0)
                    throw new ArgumentException(name+".faces: structural mapping must have one zero-duration discrete clip: "+face.faceId);
            }
            if(data.FindSlot(boundarySlot)==null)throw new ArgumentException(name+".boundarySlot missing: "+boundarySlot);
            foreach(var slot in builtInWeaponSlots)if(data.FindSlot(slot)==null)throw new ArgumentException(name+".builtInWeaponSlots missing: "+slot);
            Select(Vector3.zero,default);
            for(int i=0;i<3600;i++)Select(new Vector3(Mathf.Sin(i*.1f*Mathf.Deg2Rad),0,Mathf.Cos(i*.1f*Mathf.Deg2Rad)),default);
        }
        private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    }
}
