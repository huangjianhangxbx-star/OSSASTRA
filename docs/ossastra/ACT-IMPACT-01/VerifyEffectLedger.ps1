# 在正式工程根运行。直接编译生产台账，并引用本工程 CharacterCore 的真实 AttackRelease / Health。
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$core = Join-Path $root 'Library/ScriptAssemblies/Ossastra.CharacterCore.dll'
$ledger = Join-Path $root 'Assets/OSSASTRA/Scripts/Characters/Runtime/ActionEffectLedger.cs'
if (!(Test-Path $ledger)) { throw '缺少接触去重/终态台账' }
[void][Reflection.Assembly]::LoadFrom($core)
$tests = @'
namespace Ossastra.Characters
{
    using System;
    using Ossastra.CharacterCore;
    public static class VerifyEffectLedger
    {
        public static int Run()
        {
            int checks = 0;
            void Check(string label, bool result) {
                if (!result) throw new InvalidOperationException(label);
                ++checks;
            }
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var first = AttackRelease.ForContactWindow(a, 0, "attacker-a", 5, 0);
            var second = AttackRelease.ForEmission(a, 1, "attacker-a", 5, 0);
            var other = AttackRelease.ForEmission(b, 0, "attacker-b", 5, 0);
            var ledger = new ActionEffectLedger(a, 5);
            var otherLedger = new ActionEffectLedger(b, 5);
            Check("父动作匹配", ledger.Owns(first) && ledger.Owns(second));
            Check("另一次接受与另一本体隔离", !otherLedger.Owns(first) && !ledger.Owns(other));
            foreach (var row in new[] {
                (ActionEffectFrequency.Action, true, false, false, false),
                (ActionEffectFrequency.Release, true, false, false, true),
                (ActionEffectFrequency.TargetPerRelease, true, false, true, true) })
            {
                int i = (int)row.Item1;
                bool Claim(AttackRelease release, string victim) => ledger.Claim(i, row.Item1, 0, 0, 0, release.ActionId, victim);
                Check("首个目标 " + i, Claim(first, "light") == row.Item2);
                Check("重复 Collider " + i, Claim(first, "light") == row.Item3);
                Check("同释放第二目标 " + i, Claim(first, "heavy") == row.Item4);
                Check("下一次释放 " + i, Claim(second, "light") == row.Item5);
            }
            bool Timed(double time) => ledger.Claim(3, ActionEffectFrequency.Interval, 2, .25f, time, second.ActionId, "heavy");
            Check("固定间隔与次数上限", Timed(0) && !Timed(.24) && Timed(.25) && !Timed(.75));
            Check("第二本体独立去重", otherLedger.Claim(0, ActionEffectFrequency.Action, 0, 0, 0, other.ActionId, "light"));
            foreach (HitReason hit in Enum.GetValues(typeof(HitReason)))
            {
                Check("默认命中策略 "+hit, ledger.AllowsContact(hit, false, true, true) == (hit == HitReason.Applied));
                Check("格挡可选 "+hit, ledger.AllowsContact(hit, true, true, true) == (hit == HitReason.Applied || hit == HitReason.Guarded));
            }
            Check("致死后不再推目标", !ledger.AllowsContact(HitReason.Applied, false, true, false));
            Check("致死命中仍可合法反冲", ledger.AllowsContact(HitReason.Applied, false, false, false));
            ledger.End();
            Check("已释放投射物仍可推移其目标", ledger.AllowsContact(HitReason.Applied, false, true, true));
            Check("离手命中不能反冲旧主控", !ledger.AllowsContact(HitReason.Applied, false, false, true));
            Check("另一个动作不受结束影响", otherLedger.AllowsContact(HitReason.Applied, false, false, true));
            var light = new Health("light", 20);
            var guard = new Health("guard", 20);
            var dodge = new Health("dodge", 20);
            var failed = Health.FromSnapshot("fallen", 20, 0);
            var self = new Health("attacker-a", 20);
            var receipts = first.ResolveContacts(new[] {
                new AttackContact(light, DefenseSnapshot.Normal),
                new AttackContact(light, DefenseSnapshot.Normal),
                new AttackContact(guard, new DefenseSnapshot(false,true,1,0,.25)),
                new AttackContact(dodge, new DefenseSnapshot(true,false,1,0,1)),
                new AttackContact(failed, DefenseSnapshot.Normal),
                new AttackContact(self, DefenseSnapshot.Normal)
            });
            var reasons = new[] {HitReason.Applied, HitReason.Duplicate, HitReason.Guarded, HitReason.Evaded, HitReason.Dead, HitReason.Self};
            for (int i=0; i<reasons.Length; i++)
            {
                var r = receipts[i];
                Check("实际回执 "+reasons[i], r.Reason == reasons[i] &&
                    ledger.AllowsContact(r.Reason, false, true, r.HpAfter > 0) == (i == 0));
            }
            Check("真实双 Collider 只扣一次 HP", light.Current==15 && guard.Current==20 && dodge.Current==20);
            Check("跨帧重叠仍为 Duplicate", first.Resolve(new[]{light})[0].Reason==HitReason.Duplicate);
            var delayed = second.Resolve(new[]{light})[0];
            Check("终态后下一投递回执独立", delayed.Reason==HitReason.Applied && light.Current==10 &&
                ledger.AllowsContact(delayed.Reason, false, true, true) && !ledger.AllowsContact(delayed.Reason, false, false, true));
            Check("真实空挥无目标回执", other.Resolve(Array.Empty<Health>()).Count == 0);
            return checks;
        }
    }
}
'@
$src = (Get-Content -LiteralPath $ledger -Raw -Encoding UTF8) + [Environment]::NewLine + $tests
Add-Type -TypeDefinition $src -ReferencedAssemblies @($core, 'System.Collections.dll', 'System.Runtime.dll', 'netstandard.dll')
$count = [Ossastra.Characters.VerifyEffectLedger]::Run()
"PASS $count 项。正式动作台账、释放身份、接触政策与两本体隔离。"
