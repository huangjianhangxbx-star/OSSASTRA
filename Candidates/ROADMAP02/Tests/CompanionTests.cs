using System; using Ossastra.CharacterCore;
class Tests { static int n; static void Check(bool x,string s){if(!x)throw new Exception(s);n++;} static void Main(){
Check(CompanionCombatPolicy.Decide(true,false,true,true,true,true,true,true,false)==CompanionCombatDecision.Attack,"ready attack");
Check(CompanionCombatPolicy.Decide(true,false,true,true,true,true,false,true,false)==CompanionCombatDecision.Approach,"out of range");
Check(CompanionCombatPolicy.Decide(true,true,true,true,true,true,true,true,false)==CompanionCombatDecision.Inactive,"current");
Check(CompanionCombatPolicy.Decide(false,false,true,true,true,true,true,true,false)==CompanionCombatDecision.Inactive,"session");
Check(CompanionCombatPolicy.Decide(true,false,false,true,true,true,true,true,false)==CompanionCombatDecision.Inactive,"safe");
Check(CompanionCombatPolicy.Decide(true,false,true,false,true,true,true,true,false)==CompanionCombatDecision.Inactive,"dead blocked");
Check(CompanionCombatPolicy.Decide(true,false,true,true,false,true,true,true,false)==CompanionCombatDecision.Follow,"target");
Check(CompanionCombatPolicy.Decide(true,false,true,true,true,false,true,true,false)==CompanionCombatDecision.Follow,"leash");
Check(CompanionCombatPolicy.Decide(true,false,true,true,true,true,true,false,false)==CompanionCombatDecision.Wait,"cooldown");
Check(CompanionCombatPolicy.Decide(true,false,true,true,true,true,false,true,true)==CompanionCombatDecision.Follow,"blocked");
Console.WriteLine(n+" passed"); }}
