using System;using Ossastra.CharacterCore;
class ComboTests
{
 static int checks;
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
 static void Main()
 {
  var ctor=typeof(BasicChainState).GetConstructor(new[]{typeof(double),typeof(double),typeof(double),typeof(int),typeof(bool)});
  Check(ctor!=null,"explicit hit requirement policy exists");
  var chain=(BasicChainState)ctor.Invoke(new object[]{.12,.17,.45,4,false});
  for(int stage=1;stage<=4;stage++){
   var now=(stage-1)*.2;
   Check(chain.Request(now,true,true)==stage,"empty swing stage "+stage);
   var id=Guid.NewGuid();chain.Accepted(stage,id,now,.1);chain.Hit(id,false);
   if(stage<4){chain.Request(now+.09,false,true);Check(chain.HasBuffer,"empty swing preinput "+stage);Check(chain.Poll(now+.1,true)==stage+1,"empty swing buffer releases once "+stage);Check(chain.Poll(now+.11,true)==0,"no automatic repeat "+stage);}
  }
  Check(chain.Request(.8,true,true)==1,"fourth returns to first");
  var token=Guid.NewGuid();chain.Accepted(1,token,1,.1);Check(chain.Request(2,true,true)==1,"expired window restarts");
  chain.Accepted(1,token,3,.1);chain.Request(3.09,false,true);chain.Clear();Check(chain.Poll(3.1,true)==0,"cancel clears buffer");Check(chain.Request(4,true,false)==0,"ineligible blocked");
  var legacy=new BasicChainState(.12,.17,.45,4);legacy.Accepted(1,token,0,.1);Check(legacy.Request(.1,true,true)==1,"legacy hit policy retained");legacy.Hit(token,true);Check(legacy.Request(.1,true,true)==2,"legacy hit confirms next");
  Console.WriteLine(checks+" combo contracts passed");
 }
}
