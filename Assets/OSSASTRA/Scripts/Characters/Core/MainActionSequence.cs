/* 脚本：MainActionSequence
 * 功能：冻结主武器动作组、阶段几何/运动和连段规则，按来源组合凿冰与迅猛连击。
 * 边界：不付款、不推进角色、不持有SO；组合只生成本次候选，不改共享定义或资源。
 * 协作：Root接受阶段后使用同一Timeline，运动/投递分别进入既有唯一端口。
 */
using System;
using System.Linq;
using Ossastra.CharacterCore.Abilities;
namespace Ossastra.CharacterCore
{
    public sealed class MainActionStage
    {
        public string Id {get;}
        public BasicAttackDefinition Timeline {get;}
        public double Range {get;}
        public double ArcDegrees {get;}
        public double DashAt {get;}
        public double DashDistance {get;}
        public double DashDuration {get;}
        public bool PrefixReplaceable {get;}
        public string SourceId {get;}
        public MainActionStage(string id,BasicAttackDefinition timeline,double range,double arcDegrees,double dashAt=0,double dashDistance=0,double dashDuration=0,bool prefixReplaceable=false,string sourceId=null)
        {
            if(string.IsNullOrWhiteSpace(id)||timeline==null||new[]{range,arcDegrees,dashAt,dashDistance,dashDuration}.Any(v=>!BasicAttackDefinition.Finite(v)||v<0)||
                range<=0||arcDegrees>360||dashDistance>0&&(dashDuration<=0||dashAt+dashDuration>timeline.FinishAt))throw new ArgumentException("Invalid main stage geometry/dash: "+id);
            Id=id;Timeline=timeline;Range=range;ArcDegrees=arcDegrees;DashAt=dashAt;DashDistance=dashDistance;DashDuration=dashDuration;PrefixReplaceable=prefixReplaceable;SourceId=sourceId;
        }
        public MainActionStage WithDamageAndSource(int damage,string sourceId)=>new MainActionStage(Id,
            new BasicAttackDefinition(Timeline.ReleaseAt,Timeline.MoveReadyAt,Timeline.AttackReadyAt,Timeline.FinishAt,damage,Timeline.PostureImpact),
            Range,ArcDegrees,DashAt,DashDistance,DashDuration,PrefixReplaceable,sourceId);
        public double TravelBetween(double from,double to)
        {
            if(!BasicAttackDefinition.Finite(from)||!BasicAttackDefinition.Finite(to)||to<from)throw new ArgumentException("Invalid motion interval");
            return DashDuration<=0?0:DashDistance*Math.Max(0,Math.Min(to,DashAt+DashDuration)-Math.Max(from,DashAt))/DashDuration;
        }
    }
    public sealed class MainActionSequence
    {
        private readonly MainActionStage[] stages;
        public string Id {get;}
        public int Count=>stages.Length;
        public double BufferSeconds {get;}
        public double ContinuationSeconds {get;}
        public double FullComboRecovery {get;}
        public double PostureScale {get;}
        public MainActionStage Stage(int index)=>stages[index];
        public MainActionSequence(string id,MainActionStage[] stages,double bufferSeconds,double continuationSeconds,double fullComboRecovery,double postureScale=1)
        {
            if(string.IsNullOrWhiteSpace(id)||stages==null||stages.Length<1||stages.Length>32||stages.Any(s=>s==null)||stages.Select(s=>s.Id).Distinct().Count()!=stages.Length||
                new[]{bufferSeconds,continuationSeconds,fullComboRecovery,postureScale}.Any(v=>!BasicAttackDefinition.Finite(v)||v<0))throw new ArgumentException("Invalid main sequence: "+id);
            Id=id;this.stages=(MainActionStage[])stages.Clone();BufferSeconds=bufferSeconds;ContinuationSeconds=continuationSeconds;FullComboRecovery=fullComboRecovery;PostureScale=postureScale;
        }
        /// <summary>先替换第三段，再替换被标记的前缀；符合冻结原型trialBasic→applyTrialCombo顺序。</summary>
        public MainActionSequence Compose(MainActionStage chisel,MainActionStage[] rapid,MainActionStage[] rapidPlus,EffectSnapshot effects)
        {
            if(effects==null)return this;
            var result=(MainActionStage[])stages.Clone();var replacement=effects.FindEffect("chisel");
            if(replacement!=null&&chisel!=null&&result.Length>=3)result[2]=chisel.WithDamageAndSource(replacement.Definition.Damage,replacement.InstanceId);
            var prefix=effects.FindEffect("combo-prefix");
            if(prefix!=null)
            {
                var entries=prefix.Definition.ComboPrefix=="rapid-plus"?rapidPlus:prefix.Definition.ComboPrefix=="rapid"?rapid:null;
                if(entries!=null&&entries.Length>0)result=entries.Concat(result.Where(s=>!s.PrefixReplaceable)).ToArray();
            }
            return new MainActionSequence(Id,result,BufferSeconds,ContinuationSeconds,FullComboRecovery,PostureScale);
        }
    }
}
