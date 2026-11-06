// 功能：链接真实Registry，隔离测试通知提交时序；Unity相机外壳仅提供稳定identity，Native合同另验真实Unity对象。
using System;using Ossastra.EnvironmentSimulation;using UnityEngine;
public static class RegistryContract {
 static int checks;static void Check(bool pass,string name){checks++;if(!pass)throw new Exception(name);}
 public static int Main(){var owner=new object();var camera=new Camera();var frame=new EnvironmentFrame();int notified=0;
 Action<Camera> bad=c=>{if(c==camera)throw new InvalidOperationException("expected isolated consumer failure");};Action<Camera> good=c=>{if(c==camera)notified++;};
 try{EnvironmentCameraRegistry.Published+=bad;EnvironmentCameraRegistry.Published+=good;bool escaped=false;try{EnvironmentCameraRegistry.Publish(owner,camera,frame);}catch(InvalidOperationException){escaped=true;}
 Check(!escaped,"A failing Published observer escaped and can revoke an upstream publisher");Check(notified==1,"A failing optional observer prevented later observer notification");EnvironmentFrame read;Check(EnvironmentCameraRegistry.TryGet(camera,out read)&&ReferenceEquals(frame,read),"Committed frame was lost");
 EnvironmentCameraRegistry.Published-=bad;EnvironmentCameraRegistry.Published-=good;int released=0;Action<int> badRelease=i=>{throw new ArgumentException("expected release observer failure");};Action<int> goodRelease=i=>released++;
 EnvironmentCameraRegistry.Released+=badRelease;EnvironmentCameraRegistry.Released+=goodRelease;try{escaped=false;try{EnvironmentCameraRegistry.Release(owner,camera);}catch(ArgumentException){escaped=true;}Check(!escaped&&released==1,"Release observer prevented cleanup notification");Check(!EnvironmentCameraRegistry.TryGet(camera,out read),"Release did not remove committed state");}finally{EnvironmentCameraRegistry.Released-=badRelease;EnvironmentCameraRegistry.Released-=goodRelease;}
 Console.WriteLine("PASS "+checks+" actual Registry isolation checks");return 0;
 }catch(Exception e){Console.WriteLine("FAIL "+e.Message);return 1;}finally{EnvironmentCameraRegistry.Published-=bad;EnvironmentCameraRegistry.Published-=good;EnvironmentCameraRegistry.Release(owner,camera);}}
}
