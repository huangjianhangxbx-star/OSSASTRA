/* 功能：冰地重叠共享周期、离开重入与暂停时基的纯契约测试；柱来源过滤由真实Physics探针验证。 */
using System;
using Ossastra.CharacterCore;
static class IceContractTests
{
 static int passed;static void Check(bool ok,string name){if(!ok)throw new Exception(name);passed++;}
 static void Main(){var clock=new AreaContactClock(.5);Check(clock.TryPulse("a",.1),"enter immediate pulse");Check(!clock.TryPulse("a",.1),"overlap cannot double pulse");Check(!clock.TryPulse("a",.59),"before deadline");Check(clock.TryPulse("a",.6),"at deadline");Check(clock.TryPulse("b",.6),"separate target");Check(!clock.TryPulse("a",.6),"paused clock cannot repeat");clock.Retain(new[]{"b"});Check(clock.TryPulse("a",.61),"leave reenter owns new contact");clock.Clear();Check(clock.Count==0,"clear releases ledger");bool bad=false;try{clock.TryPulse("a",double.NaN);}catch(ArgumentException){bad=true;}Check(bad,"invalid time rejected");Console.WriteLine("PASS "+passed+" ice contracts");}
}
