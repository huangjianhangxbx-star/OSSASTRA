// 功能：离线执行真实环境纯计算合同，不加载Unity场景或调用对象/灯光API。
using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
class Program
{
 static Type Find(string name)=>Assembly.GetExecutingAssembly().GetType("Ossastra.EnvironmentSimulation."+name);
 static object Call(Type t,string name,params object[] args)=>t.GetMethod(name).Invoke(null,args);
 static object Property(object o,string name)=>o.GetType().GetProperty(name).GetValue(o);
 static int Main()
 {
  var c=Find("EnvironmentClock");var s=Find("SunPath");var w=Find("WeatherBlend");
  if(c==null||s==null||w==null){Console.WriteLine("RED: missing approved clock/sun/weather behavior");return 1;}
  int checks=0;var failures=new List<string>();Action<bool,string> need=(ok,name)=>{checks++;if(!ok)failures.Add(name);};
  try
  {
   need(Math.Abs((double)Call(c,"WrapHours",-1d)-23)<1e-9,"negative wraps");
   need(Math.Abs((double)Call(c,"AdvanceHours",23.9d,6d,360d)-.3)<1e-8,"midnight continuity");
   double one=0,many=0;for(int i=0;i<60;i++)one=(double)Call(c,"AdvanceHours",one,1d,360d);for(int i=0;i<3600;i++)many=(double)Call(c,"AdvanceHours",many,1d/60,360d);
   need(Math.Abs(one-4)<1e-8&&Math.Abs(many-4)<1e-7,"update rate independence");
   foreach(double invalid in new[]{0d,-1d,double.NaN,double.PositiveInfinity}){bool rejects=false;try{Call(c,"AdvanceHours",0d,1d,invalid);}catch(TargetInvocationException e){rejects=e.InnerException is ArgumentException;}need(rejects,"bad cycle rejected "+invalid);}
   bool badHour=false;try{Call(c,"WrapHours",double.NaN);}catch(TargetInvocationException e){badHour=e.InnerException is ArgumentException;}need(badHour,"NaN hour rejected");
   var dawn=Call(s,"Sample",6d,60f,0f);var noon=Call(s,"Sample",12d,60f,0f);var dusk=Call(s,"Sample",18d,60f,0f);var night=Call(s,"Sample",0d,60f,0f);
   need(Math.Abs((float)Property(dawn,"ElevationDegrees"))<.001,"dawn horizon");need(Math.Abs((float)Property(noon,"ElevationDegrees")-60)<.001,"noon elevation");need(Math.Abs((float)Property(dusk,"AzimuthDegrees")-270)<.001,"west sunset");need((float)Property(night,"ElevationDegrees")<0,"midnight below horizon");
   need(Math.Abs(((Vector3)Property(noon,"DirectionToSun")).magnitude-1)<.00001,"normalized direction");
   var east=(Vector3)Property(dawn,"DirectionToSun");need(east.x>.999&&Math.Abs(east.y)<.001&&Math.Abs(east.z)<.001,"world east convention");
   need(Math.Abs((double)Call(w,"Progress",2.5d,5d)-.5)<1e-9,"weather halfway");need((double)Call(w,"Progress",1d,0d)==1,"immediate weather");need((double)Call(w,"Progress",8d,5d)==1,"weather end clamps");need((double)Call(w,"Progress",-1d,5d)==0,"weather start clamps");
  }
  catch(Exception e){failures.Add(e.ToString());}
  Console.WriteLine((failures.Count==0?"GREEN":"FAIL")+": "+checks+" checks; "+string.Join(" | ",failures));return failures.Count==0?0:1;
 }
}
