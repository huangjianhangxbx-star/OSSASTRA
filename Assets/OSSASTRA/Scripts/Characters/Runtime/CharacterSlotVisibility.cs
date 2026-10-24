/* 脚本：CharacterSlotVisibility
 * 功能：基于4.3最终AppliedPose，临时屏蔽边界外及具名内置武器slot的alpha。
 * 边界：只修改本实例姿态，不动Attachment/共享数据/约束；裁切附件不屏蔽，保留clip起止生命周期。
 * 协作：姿态求解后Apply，网格生成后或下次应用前Restore；缓存实际被覆盖的Pose和原alpha，防止永久透明。
 */
using System;
using System.Collections.Generic;
using Spine;
using UnityEngine;
namespace Ossastra.Characters
{
    public sealed class CharacterSlotVisibility
    {
        private readonly Skeleton skeleton;
        private readonly Slot boundary;
        private readonly Slot[] slots;
        private readonly bool[] builtIn;
        private readonly SlotPose[] owned;
        private readonly float[] originalAlpha;
        private float[] vertices=Array.Empty<float>();
        public Rect Bounds {get;private set;}
        public int HiddenCount {get;private set;}
        public CharacterSlotVisibility(Skeleton source,string boundarySlot,string[] builtInSlots)
        {
            skeleton=source??throw new ArgumentNullException(nameof(source));
            boundary=source.FindSlot(boundarySlot)??throw new ArgumentException("Visibility boundary slot missing: "+boundarySlot);
            slots=new Slot[source.Slots.Count];builtIn=new bool[slots.Length];owned=new SlotPose[slots.Length];originalAlpha=new float[slots.Length];
            var names=new HashSet<string>(builtInSlots??Array.Empty<string>(),StringComparer.Ordinal);
            for(int i=0;i<slots.Length;i++){slots[i]=source.Slots.Items[i];builtIn[i]=names.Contains(slots[i].Data.Name);}
        }
        public void Restore()
        {
            for(int i=0;i<owned.Length;i++)
            {
                var pose=owned[i];if(pose==null)continue;
                var color=pose.GetColor();
                // 只恢复自己写下的0；若别的合法消费者已改值，不覆写它的新值/RGB。
                if(color.a==0){color.a=originalAlpha[i];pose.SetColor(color);}
                owned[i]=null;
            }
        }
        public void Apply(bool hideBuiltIn,bool showAll)
        {
            Restore();HiddenCount=0;
            if(showAll)return;
            var clip=boundary.AppliedPose.Attachment as ClippingAttachment;
            if(clip==null)throw new ArgumentException("Active visibility boundary is not a clipping attachment: "+boundary.Data.Name);
            if(vertices.Length!=clip.WorldVerticesLength)vertices=new float[clip.WorldVerticesLength];
            clip.ComputeWorldVertices(skeleton,boundary,vertices);
            float minX=float.PositiveInfinity,maxX=float.NegativeInfinity,minY=float.PositiveInfinity,maxY=float.NegativeInfinity;
            for(int i=0;i<vertices.Length;i+=2){minX=Mathf.Min(minX,vertices[i]);maxX=Mathf.Max(maxX,vertices[i]);minY=Mathf.Min(minY,vertices[i+1]);maxY=Mathf.Max(maxY,vertices[i+1]);}
            Bounds=Rect.MinMaxRect(minX,minY,maxX,maxY);
            for(int i=0;i<slots.Length;i++)
            {
                var slot=slots[i];var pose=slot.AppliedPose;var attachment=pose.Attachment;
                if(!(attachment is RegionAttachment)&&!(attachment is MeshAttachment))continue;
                var bone=slot.Bone.AppliedPose;
                if(Bounds.Contains(new Vector2(bone.WorldX,bone.WorldY))&&(!hideBuiltIn||!builtIn[i]))continue;
                HiddenCount++;var color=pose.GetColor();if(color.a==0)continue;
                owned[i]=pose;originalAlpha[i]=color.a;color.a=0;pose.SetColor(color);
            }
        }
        public bool Contains(Bone bone)=>bone!=null&&Bounds.Contains(new Vector2(bone.AppliedPose.WorldX,bone.AppliedPose.WorldY));
    }
}
