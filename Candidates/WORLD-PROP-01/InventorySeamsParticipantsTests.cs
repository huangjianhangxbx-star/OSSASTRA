/* 测试：真实参与者聚合与原Codec；防扩展载荷丢失、缺领域重播种、候选失败提前发布和销毁订阅泄漏。 */
using System;using System.Linq;using Ossastra.Commerce.Runtime;
sealed class Participant:ICommerceSnapshotParticipant
{
 public string SnapshotKey{get;set;}="world-props";public event Action Changed;public int current=3,prepared,published;public bool reject;
 public CommerceSnapshotSection Capture()=>new CommerceSnapshotSection{key=SnapshotKey,schema=1,payload="{\"value\":"+current+"}"};
 public bool TryPrepareRestore(CommerceSnapshotSection section,CommerceRuntimeSnapshot aggregate,out object value,out string error){prepared++;error=reject?"injected invalid prop state":null;value=error==null?(object)7:null;return error==null;}
 public void PublishRestore(object value){published++;current=(int)value;}
 public void Fire()=>Changed?.Invoke();
}
static class InventorySeamsParticipantsTests
{
 static int passed,failed;static void Require(bool v,string why){if(!v)throw new Exception(why);}static void Test(string name,Action body){try{body();Console.WriteLine("PASS "+name);passed++;}catch(Exception e){Console.WriteLine("FAIL "+name+": "+e.Message);failed++;}}
 static CommerceRuntimeSnapshot Snap(params CommerceSnapshotSection[] sections)=>new CommerceRuntimeSnapshot{revision=4,sections=sections};
 static void Main(){
 Test("same codec preserves typed section payload and revision",()=>{var a=new CommerceRuntimeSnapshot{revision=13,sections=new[]{new CommerceSnapshotSection{key="world-props",schema=1,payload="{\"hp\":23,\"phase\":\"Pending\",\"break\":\"a\"}"}}};var b=CommerceSnapshotCodec.Decode(CommerceSnapshotCodec.Encode(a));Require(b.revision==13&&b.sections.Length==1&&b.sections[0].payload.Contains("23")&&b.sections[0].key=="world-props","aggregate lost participant state");});
 Test("legacy schema2 without participants remains usable",()=>{using(var group=new CommerceSnapshotParticipants()){var old=CommerceSnapshotCodec.Decode(System.Text.Encoding.UTF8.GetBytes("{\"schema\":2,\"revision\":4}"));Require(group.TryPrepareRestore(old,out var token,out var error),error);group.PublishRestore(token);Require(group.Capture().Length==0,"legacy gained data");}});
 Test("registered domain rejects missing section instead of reseeding",()=>{using(var group=new CommerceSnapshotParticipants()){var p=new Participant();group.Register(p);Require(!group.TryPrepareRestore(Snap(),out var token,out var error)&&token==null&&error.Contains("world-props")&&p.published==0,"missing domain silently reset");}});
 Test("all participant candidates validate before any live publication",()=>{using(var group=new CommerceSnapshotParticipants()){var a=new Participant();var b=new Participant{SnapshotKey="other",reject=true};group.Register(a);group.Register(b);var snap=Snap(a.Capture(),b.Capture());Require(!group.TryPrepareRestore(snap,out var token,out var error)&&token==null&&a.current==3&&a.published==0&&b.published==0,"partial restore leaked");b.reject=false;Require(group.TryPrepareRestore(snap,out token,out error),error);Require(a.current==3&&b.current==3,"prepare touched live domain");group.PublishRestore(token);Require(a.current==7&&b.current==7&&a.published==1&&b.published==1,"publish missed domain");bool twice=false;try{group.PublishRestore(token);}catch(InvalidOperationException){twice=true;}Require(twice,"restore token reused");}});
 Test("duplicate unknown and malformed sections reject before publication",()=>{using(var group=new CommerceSnapshotParticipants()){var p=new Participant();group.Register(p);foreach(var snap in new[]{Snap(p.Capture(),p.Capture()),Snap(new CommerceSnapshotSection{key="foreign",schema=1,payload="{}"}),Snap(new CommerceSnapshotSection{key="world-props",schema=0,payload="{}"})})Require(!group.TryPrepareRestore(snap,out var token,out var error)&&p.published==0,"bad section accepted");}});
 Test("registration freezes and disposed dirty subscription is released",()=>{var group=new CommerceSnapshotParticipants();var p=new Participant();int events=0;group.Changed+=()=>events++;group.Register(p);p.Fire();Require(events==1,"dirty did not propagate");group.FreezeRegistration();bool late=false;try{group.Register(new Participant{SnapshotKey="late"});}catch(InvalidOperationException){late=true;}Require(late,"late domain bypassed startup validation");group.Dispose();p.Fire();Require(events==1,"destroyed aggregate still subscribes");});
 Console.WriteLine("TOTAL passed="+passed+" failed="+failed);Environment.ExitCode=failed==0?0:1;
 }
}