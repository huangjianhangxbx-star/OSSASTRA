/* 脚本：CharacterSlotVisibility
 * 功能：基于4.3最终AppliedPose，按裁切边界或明确面根归属临时屏蔽slot的alpha。
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
        private readonly Bone[] faceRoots;
        private readonly int[] slotFaces;
        private readonly Dictionary<Bone,int> boneFaces;
        private int selectedFace=-1;
        private float[] vertices=Array.Empty<float>();
        public Rect Bounds {get;private set;}
        public int HiddenCount {get;private set;}
        private CharacterSlotVisibility(Skeleton source,string[] builtInSlots)
        {
            skeleton=source??throw new ArgumentNullException(nameof(source));
            slots=new Slot[source.Slots.Count];builtIn=new bool[slots.Length];owned=new SlotPose[slots.Length];originalAlpha=new float[slots.Length];
            var names=new HashSet<string>(builtInSlots??Array.Empty<string>(),StringComparer.Ordinal);
            for(int i=0;i<slots.Length;i++){slots[i]=source.Slots.Items[i];builtIn[i]=names.Contains(slots[i].Data.Name);}
        }
        public CharacterSlotVisibility(Skeleton source,string boundarySlot,string[] builtInSlots):this(source,builtInSlots)
        {boundary=source.FindSlot(boundarySlot)??throw new ArgumentException("Visibility boundary slot missing: "+boundarySlot);}
        public CharacterSlotVisibility(Skeleton source,string[] roots,string[] builtInSlots):this(source,builtInSlots)
        {
            if(roots==null||roots.Length==0)throw new ArgumentException("Face roots missing");
            faceRoots=new Bone[roots.Length];boneFaces=new Dictionary<Bone,int>();slotFaces=new int[slots.Length];
            for(int i=0;i<roots.Length;i++)
            {faceRoots[i]=source.FindBone(roots[i])??throw new ArgumentException("Face root missing: "+roots[i]);for(int j=0;j<i;j++)if(faceRoots[j]==faceRoots[i])throw new ArgumentException("Duplicate face root: "+roots[i]);}
            foreach(var root in faceRoots)for(var p=root.Parent;p!=null;p=p.Parent)
                if(Array.IndexOf(faceRoots,p)>=0)throw new ArgumentException("Nested face roots");
            // 只在骨架绑定时遍历父链；每帧按缓存的整型归属显隐。共同根槽位(-1)仍保留。
            foreach(var bone in source.Bones)
            {
                int index=-1;for(var p=bone;p!=null;p=p.Parent){index=Array.IndexOf(faceRoots,p);if(index>=0)break;}
                boneFaces.Add(bone,index);
            }
            for(int i=0;i<slots.Length;i++)slotFaces[i]=boneFaces[slots[i].Bone];
        }
        public void SelectFace(string rootBone)
        {
            if(faceRoots==null)throw new InvalidOperationException("SelectFace requires face-branch visibility");
            int index=Array.FindIndex(faceRoots,b=>b.Data.Name==rootBone);
            if(index<0)throw new ArgumentException("Unknown face root: "+rootBone);
            selectedFace=index;
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
            if(faceRoots!=null)
            {
                if(selectedFace<0)throw new InvalidOperationException("No face selected");
                for(int i=0;i<slots.Length;i++)
                    if((slotFaces[i]>=0&&slotFaces[i]!=selectedFace)||(hideBuiltIn&&builtIn[i]))Hide(i);
                return;
            }
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
                Hide(i);
            }
        }
        private void Hide(int index)
        {
            var pose=slots[index].AppliedPose;var attachment=pose.Attachment;
            if(!(attachment is RegionAttachment)&&!(attachment is MeshAttachment))return;
            HiddenCount++;var color=pose.GetColor();if(color.a==0)return;
            owned[index]=pose;originalAlpha[index]=color.a;color.a=0;pose.SetColor(color);
        }
        public bool Contains(Bone bone)=>bone!=null&&(faceRoots==null?Bounds.Contains(new Vector2(bone.AppliedPose.WorldX,bone.AppliedPose.WorldY)):
            boneFaces.TryGetValue(bone,out var index)&&(index<0||index==selectedFace));
    }
}
