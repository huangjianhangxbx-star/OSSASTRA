// Catches absent scene hue, moon-color discontinuity at 0 degrees, duplicate gamma decode,
// invalid author input entering shader globals, and mutation of the author's settings.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using UnityEngine;
class ResolverTests
{
 static readonly List<object> checks=new List<object>();
 static Type resolver,settingsType;static object settings;
 static void Check(string name,bool pass){checks.Add(new{name,pass});}
 static bool Close(float a,float b,float epsilon=.0001f)=>Math.Abs(a-b)<epsilon;
 static Vector4 Resolve(float elevation=0,bool manual=false,Color? ambient=null,Color? sunset=null,Color? sky=null)
 {
  Color a=ambient??new Color(.5f,.5f,.5f),s=sunset??new Color(1,.5f,.25f),m=sky??new Color(.5f,.25f,.125f);
  return (Vector4)resolver.GetMethod("ResolveTint").Invoke(null,new object[]{a,a,s,elevation,manual,m,m,settings});
 }
 static void Main()
 {
  resolver=Type.GetType("Ossastra.EnvironmentSimulation.SceneEnvironmentColorResolver");
  settingsType=Type.GetType("Ossastra.EnvironmentSimulation.SceneEnvironmentSettings");
  Check("Scene chroma resolver exists (missing feature is valid RED)",resolver!=null&&settingsType!=null);
  if(resolver!=null&&settingsType!=null)
  {
   try
   {
    settings=Activator.CreateInstance(settingsType);
    var noon=Resolve(60);Check("Noon keeps neutral ambient chroma",Close(noon.x,1)&&Close(noon.y,1)&&Close(noon.z,1)&&Close(noon.w,1));
    var night=Resolve(-30);Check("Night returns ambient rather than daytime sunset",Close(night.x,1)&&Close(night.y,1)&&Close(night.z,1));
    var sunset=Resolve();Check("Horizon remains visibly warm with no direct-sun energy",Close(sunset.x,1)&&Close(sunset.y,.214041f)&&Close(sunset.z,.050876f));
    var before=Resolve(-.0001f);var after=Resolve(.0001f);Check("Crossing horizon never switches to moon hue",Close(before.x,after.x)&&Close(before.y,after.y)&&Close(before.z,after.z));
    var manual=Resolve(60,true);Check("Manual sky decodes gamma exactly once and overrides natural color",Close(manual.x,1)&&Close(manual.y,.237692f,.0002f)&&Close(manual.z,.067039f,.0002f));
    var manualNight=Resolve(-30,true);Check("Manual effective sky is independent of sun position",Close(manual.x,manualNight.x)&&Close(manual.y,manualNight.y)&&Close(manual.z,manualNight.z));
    var black=Resolve(60,false,new Color(0,0,0));Check("No ambient target fails neutral without generating light",Close(black.w,0));
    var bad=Resolve(float.NaN);Check("Nonfinite frame elevation fails neutral",Close(bad.w,0));
    var invalidColor=Resolve(60,true,null,null,new Color(float.PositiveInfinity,1,1));Check("Nonfinite effective sky fails neutral",Close(invalidColor.w,0));
    var field=settingsType.GetField("groundStrength");field.SetValue(settings,float.NaN);
    var sanitized=settingsType.GetMethod("Sanitize").Invoke(settings,null);
    Check("Sanitize repairs NaN strength without mutating author input",float.IsNaN((float)field.GetValue(settings))&&!float.IsNaN((float)field.GetValue(sanitized)));
    settingsType.GetField("twilightFullElevation").SetValue(settings,-20f);
    sanitized=settingsType.GetMethod("Sanitize").Invoke(settings,null);
    float start=(float)settingsType.GetField("twilightStartElevation").GetValue(sanitized),full=(float)settingsType.GetField("twilightFullElevation").GetValue(sanitized);
    Check("Invalid twilight window is ordered before interpolation",full>start);
   }
   catch(Exception e){Check("Resolver numerical contract exception: "+(e.InnerException??e).Message,false);}
  }
  System.Console.WriteLine(JsonSerializer.Serialize(new{checks}));
  Environment.ExitCode=checks.Exists(x=>(bool)x.GetType().GetProperty("pass").GetValue(x)==false)?1:0;
 }
}
