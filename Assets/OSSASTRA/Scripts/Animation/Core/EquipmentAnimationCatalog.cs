/* 脚本：EquipmentAnimationCatalog
 * 功能：冻结装备语义与片段映射，支持身体和装备使用不同名称及长度的动画。
 * 边界：没有Unity/Spine或伤害回调；缺映射显式返回空，由表现层报告。
 */
using System;
using System.Collections.Generic;
namespace Ossastra.AnimationCore
{
    public sealed class EquipmentAnimationClip
    {
        public AnimationSemantic Semantic { get; }
        public string Clip { get; }
        public bool Loop { get; }
        public float Mix { get; }
        public float Rate { get; }
        public EquipmentAnimationClip(AnimationSemantic semantic,string clip,bool loop,float mix,float rate)
        {
            if(string.IsNullOrWhiteSpace(clip)||float.IsNaN(mix)||float.IsInfinity(mix)||mix<0||
                float.IsNaN(rate)||float.IsInfinity(rate)||rate<=0)throw new ArgumentException("Invalid equipment animation binding");
            Semantic=semantic;Clip=clip;Loop=loop;Mix=mix;Rate=rate;
        }
    }
    public sealed class EquipmentAnimationCatalog
    {
        private readonly Dictionary<AnimationSemantic,EquipmentAnimationClip> clips=new Dictionary<AnimationSemantic,EquipmentAnimationClip>();
        public EquipmentAnimationCatalog(IEnumerable<EquipmentAnimationClip> bindings)
        {
            if(bindings==null)throw new ArgumentNullException(nameof(bindings));
            foreach(var binding in bindings)
            {
                if(binding==null||clips.ContainsKey(binding.Semantic))throw new ArgumentException("Null or duplicate equipment animation semantic");
                clips.Add(binding.Semantic,binding);
            }
        }
        public EquipmentAnimationClip Find(AnimationSemantic semantic)=>clips.TryGetValue(semantic,out var clip)?clip:null;
    }
}
