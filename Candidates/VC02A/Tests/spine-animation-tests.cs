/* 功能：装备动画映射冻结、显式缺失与独立播放身份的定向纯契约测试。 */
using System;
using Ossastra.AnimationCore;
static class EquipmentAnimationTests
{
    static int passed;
    static void Check(bool value,string name){if(!value)throw new Exception(name);passed++;}
    static void Main()
    {
        var entries=new[]{new EquipmentAnimationClip(AnimationSemantic.BasicAttack,"swing",false,.08f,1),new EquipmentAnimationClip(AnimationSemantic.Idle,"rest",true,0,1)};
        var frozen=new EquipmentAnimationCatalog(entries);
        entries[0]=new EquipmentAnimationClip(AnimationSemantic.BasicAttack,"changed",false,0,1);
        Check(frozen.Find(AnimationSemantic.BasicAttack).Clip=="swing","accepted mapping is immutable");
        Check(frozen.Find(AnimationSemantic.Move)==null,"missing mapping is explicit, not silent idle");
        var state=new AnimationPlaybackIdentity();var execution=Guid.NewGuid();
        var a=new AnimationIntent("a",AnimationSemantic.BasicAttack,"equipment",execution,0);
        Check(state.ShouldCommit("swing",a),"first body identity commits equipment");
        Check(!state.ShouldCommit("swing",a),"same body identity cannot restart equipment");
        Check(state.ShouldCommit("swing",new AnimationIntent("a",AnimationSemantic.BasicAttack,"equipment",Guid.NewGuid(),0)),"same clip new phase replays");
        var second=new AnimationPlaybackIdentity();Check(second.ShouldCommit("swing",a),"second instance has independent playback");
        Check(state.ShouldCommit("rest",new AnimationIntent("a",AnimationSemantic.Idle,"equipment",Guid.Empty,0)),"cancel/locomotion changes pose");
        bool bad=false;try{new EquipmentAnimationCatalog(new[]{entries[0],entries[0]});}catch(ArgumentException){bad=true;}Check(bad,"duplicate semantic rejected");
        bad=false;try{new EquipmentAnimationClip(AnimationSemantic.Move,"move",true,0,float.NaN);}catch(ArgumentException){bad=true;}Check(bad,"invalid rate rejected");
        Console.WriteLine("PASS "+passed+" equipment animation contracts");
    }
}
