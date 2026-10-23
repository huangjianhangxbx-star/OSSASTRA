/* 脚本：MainActionProfileSO
 * 功能：作者配置独立主手动作/形态组及凿冰、迅猛前缀；现有默认四段不被自动替换。
 * 边界：SO只持定义和动画引用，不保存装备选择/资源/执行；Freeze后原数组编辑不影响动作拓扑。
 * 协作：装备实例保存选择ID，Root接受时冻结动作动画，再把阶段几何/运动交共享执行器。
 */
using System;
using System.Linq;
using UnityEngine;
using Ossastra.CharacterCore;
using Ossastra.CharacterCore.Abilities;
namespace Ossastra.Characters
{
    [Serializable]
    public sealed class MainActionStageDefinition
    {
        public string stageId;
        public ActionDefinitionSO action;
        [Min(.01f)] public float range=1.8f;
        [Range(0,360)] public float arcDegrees=114.59156f;
        [Min(0)] public float dashAt,dashDistance,dashDuration;
        public bool prefixReplaceable;
        public MainActionStage Freeze()
        {
            if(action==null)throw new ArgumentException("Missing action for main stage: "+stageId);
            return new MainActionStage(stageId,action.Freeze(),range,arcDegrees,dashAt,dashDistance,dashDuration,prefixReplaceable);
        }
    }
    public sealed class FrozenMainActionProfile
    {
        private readonly ActionDefinitionSO[] animations;
        public MainActionSequence Sequence {get;}
        internal FrozenMainActionProfile(MainActionSequence sequence,ActionDefinitionSO[] animations){Sequence=sequence;this.animations=(ActionDefinitionSO[])animations.Clone();}
        public ActionDefinitionSO AnimationDefinition(int index)=>animations[index];
    }
    [CreateAssetMenu(menuName="OSSASTRA/Equipment/Main Action Profile")]
    public sealed class MainActionProfileSO:ScriptableObject
    {
        public string definitionId,displayName,professionId;
        public string formLabel="I";
        [TextArea] public string sourceEvidence;
        [Min(0)] public float bufferSeconds=.25f,continuationSeconds=.625f,fullComboRecovery=.25f,postureScale=1;
        [Min(0)] public float contactDuration=.1f;
        public MainActionStageDefinition[] stages=Array.Empty<MainActionStageDefinition>();
        public MainActionStageDefinition chisel;
        public MainActionStageDefinition[] rapidPrefix=Array.Empty<MainActionStageDefinition>(),rapidPlusPrefix=Array.Empty<MainActionStageDefinition>();
        public FrozenMainActionProfile Freeze(EffectSnapshot effects=null)
        {
            if(float.IsNaN(contactDuration)||float.IsInfinity(contactDuration)||contactDuration<0)throw new ArgumentException(name+": invalid contact duration");
            if(stages==null||rapidPrefix==null||rapidPlusPrefix==null)throw new ArgumentException(name+": missing main profile array");
            // Unity内联类重载后可能把null还原为空对象；空ID且无action才是缺省，半配置仍拒绝。
            var optionalChisel=chisel==null||chisel.action==null&&string.IsNullOrWhiteSpace(chisel.stageId)?null:chisel;
            var sequence=new MainActionSequence(definitionId,stages.Select(s=>s?.Freeze()??throw new ArgumentException(name+": missing stage")).ToArray(),bufferSeconds,continuationSeconds,fullComboRecovery,postureScale);
            var all=stages.Concat(optionalChisel==null?Array.Empty<MainActionStageDefinition>():new[]{optionalChisel}).Concat(rapidPrefix).Concat(rapidPlusPrefix).ToArray();
            if(all.Any(s=>s==null)||all.Select(s=>s.stageId).Distinct().Count()!=all.Length)throw new ArgumentException(name+": duplicate/missing stage identity");
            sequence=sequence.Compose(optionalChisel?.Freeze(),rapidPrefix.Select(s=>s.Freeze()).ToArray(),rapidPlusPrefix.Select(s=>s.Freeze()).ToArray(),effects);
            var animations=Enumerable.Range(0,sequence.Count).Select(i=>all.First(s=>s.stageId==sequence.Stage(i).Id).action).ToArray();
            return new FrozenMainActionProfile(sequence,animations);
        }
    }
}
