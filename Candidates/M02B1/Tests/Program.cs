// B1阵营筛选合同：抓取友伤、自伤、死者仍命中等真实错误分支，不镜像被测算法。
using System;
using Ossastra.CharacterCore;
internal static class Program
{
    static int failed, total;
    static void Test(string name, bool actual) { total++; if (!actual) failed++; Console.WriteLine((actual?"PASS ":"FAIL ")+name); }
    static bool Hit(CombatFaction a, CombatFaction b, bool alive=true, bool targetAlive=true, string source="a", string target="b")
        => CombatTargetPolicy.CanHit(source,a,alive,target,b,targetAlive);
    static int Main()
    {
        Test("allies do not damage allies", !Hit(CombatFaction.Allied,CombatFaction.Allied));
        Test("enemy does not damage its own faction", !Hit(CombatFaction.Enemy,CombatFaction.Enemy));
        Test("allied damages enemy", Hit(CombatFaction.Allied,CombatFaction.Enemy));
        Test("enemy damages allied", Hit(CombatFaction.Enemy,CombatFaction.Allied));
        Test("legacy neutral development target remains damageable", Hit(CombatFaction.Allied,CombatFaction.Neutral));
        Test("dead source cannot release damage", !Hit(CombatFaction.Enemy,CombatFaction.Allied,false));
        Test("dead target cannot receive damage", !Hit(CombatFaction.Enemy,CombatFaction.Allied,true,false));
        Test("identity excludes self even with wrong faction", !Hit(CombatFaction.Enemy,CombatFaction.Allied,true,true,"same","same"));
        Test("missing source identity is rejected", !Hit(CombatFaction.Enemy,CombatFaction.Allied,true,true,"","b"));
        Test("missing target identity is rejected", !Hit(CombatFaction.Enemy,CombatFaction.Allied,true,true,"a",""));
        var health=new Health("persistent",100); var original=health;
        var action=new BasicActionRuntime("enemy");
        action.Request(new BasicAttackDefinition(0,0,0,.01,150),"test",new ControlAuthority());
        AttackRelease release=null;
        foreach (var fact in action.Advance(.001,false)) if (fact.Release!=null) release=fact.Release;
        release.Resolve(new[]{health});
        Test("lethal damage is clamped to zero",health.Current==0);
        health.RestoreForDevelopment();
        Test("explicit reset keeps same Health identity",ReferenceEquals(health,original)&&health.Id=="persistent"&&health.Current==100&&health.Maximum==100);
        bool refused=false; try { release.Resolve(new[]{health}); } catch (InvalidOperationException) { refused=true; }
        Test("reset does not reopen an old release",refused&&health.Current==100);
        Console.WriteLine("RESULT "+(total-failed)+"/"+total); return failed==0?0:1;
    }
}
