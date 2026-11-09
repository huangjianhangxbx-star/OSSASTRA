/* Pure-data behavior contracts. Tests exercise the candidate itself; analytic samplers provide geometry inputs. */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Ossastra.WorldRendering.Vegetation;
internal static class ScatterContract
{
 static int passed,failed;
 static GrassScatterSettings Settings(float density=4)=>new GrassScatterSettings{seed=123,density=density,maxDensity=4,minHeight=.2f,maxHeight=.4f,minWidth=.15f,maxWidth=.3f,maxSlopeDegrees=35,maximumPlacements=10000};
 static GrassSurfaceArea Area(int index=0,float x=0,float z=0,float width=2,float depth=2)=>new GrassSurfaceArea{surfaceIndex=index,worldBounds=new Bounds(new Vector3(x,0,z),new Vector3(width,0,depth))};
 static GrassSurfaceSample? Plane(int surface,Vector2 p)=>new GrassSurfaceSample{worldRoot=new Vector3(p.x,surface*2,p.y),localRoot=new Vector3(p.x,0,p.y),worldNormal=new Vector3(0,1,0),localNormal=new Vector3(0,1,0)};
 static GrassPlacement[] Generate(GrassScatterSettings s,GrassSurfaceArea[] areas=null,Func<int,Vector2,GrassSurfaceSample?> sample=null,Func<Vector3,bool> exclude=null)=>GrassScatter.Generate(s,areas??new[]{Area()},sample??Plane,exclude);
 static void Check(bool value,string reason){if(!value)throw new Exception(reason);}
 static void Throws<T>(Action action) where T:Exception {try{action();}catch(T){return;}throw new Exception("Expected "+typeof(T).Name);}
 static void Run(string name,Action body){try{body();++passed;Console.WriteLine("PASS "+name);}catch(Exception e){++failed;Console.WriteLine("FAIL "+name+": "+e.Message);}}
 static string Key(GrassPlacement p)=>p.surfaceIndex+":"+p.cellX+":"+p.cellZ;
 static string Signature(GrassPlacement p)=>string.Join("|",Key(p),F(p.worldRoot.x),F(p.worldRoot.y),F(p.worldRoot.z),F(p.localRoot.x),F(p.localRoot.y),F(p.localRoot.z),F(p.height),F(p.width),F(p.yaw),F(p.phase));
 static string F(float value)=>value.ToString("R",CultureInfo.InvariantCulture);
 public static int Main()
 {
  Run("full-density valid 2x2 plane has sixteen .5m cells",()=>{var p=Generate(Settings());Check(p.Length==16,"flat valid surface must create 16 roots, got "+p.Length);Check(p.Select(Key).Distinct().Count()==16,"duplicate cells");foreach(var v in p){Check(v.worldRoot.x>=-1&&v.worldRoot.x<1&&v.worldRoot.z>=-1&&v.worldRoot.z<1,"out of area");Check(v.worldRoot.y==0&&v.localRoot.y==0,"root detached from sampled plane");Check(v.height>=.2f&&v.height<=.4f&&v.width>=.15f&&v.width<=.3f,"size outside configured range");}});
  Run("fixed seed restores exact roots and attributes",()=>{var a=Generate(Settings());var b=Generate(Settings());Check(a.Length>0&&a.Select(Signature).SequenceEqual(b.Select(Signature)),"replay changed");var changed=Settings();changed.seed=124;Check(!a.Select(Signature).SequenceEqual(Generate(changed).Select(Signature)),"seed ignored");});
  Run("half and double density are stable nested subsets",()=>{var full=Generate(Settings(4));var half=Generate(Settings(2));var quarter=Generate(Settings(1));Check(quarter.Length>0&&quarter.Length<half.Length&&half.Length<full.Length,"fixture did not produce graded density");var dict=full.ToDictionary(Key,Signature);foreach(var p in half.Concat(quarter))Check(dict.TryGetValue(Key(p),out var signature)&&signature==Signature(p),"density changed retained root attributes");var halfKeys=new HashSet<string>(half.Select(Key));Check(quarter.All(p=>halfKeys.Contains(Key(p))),"non-nested quarter density");});
  Run("hard exclusion uses actual sample height and location",()=>{var all=Generate(Settings(),new[]{Area(0),Area(1)});var kept=Generate(Settings(),new[]{Area(0),Area(1)},exclude:p=>p.y<1&&p.x<0);Check(all.Length==32&&kept.Length==24,"literal two-layer exclusion count wrong");Check(kept.Count(p=>p.surfaceIndex==1)==16,"upper layer was incorrectly excluded");Check(kept.Where(p=>p.surfaceIndex==0).All(p=>p.worldRoot.x>=0),"excluded roots remain");});
  Run("explicit surfaces remain distinct in overlapping XZ",()=>{var p=Generate(Settings(),new[]{Area(7),Area(2)});Check(p.Length==32,"overlapping layers lost");foreach(var v in p){Check(v.worldRoot.y==v.surfaceIndex*2&&v.localRoot.y==0,"surface identity or local anchor mixed");}var reordered=Generate(Settings(),new[]{Area(2),Area(7)});Check(p.Select(Signature).SequenceEqual(reordered.Select(Signature)),"surface input order changes result");});
  Run("missing exact-surface hit rejects a root",()=>{var p=Generate(Settings(),sample:(surface,pos)=>pos.x<0?(GrassSurfaceSample?)null:Plane(surface,pos));Check(p.Length==8&&p.All(v=>v.worldRoot.x>=0),"null sampler hit was planted");});
  Run("slope threshold uses normalized normal",()=>{Func<int,Vector2,GrassSurfaceSample?> sampler=(surface,pos)=>{var s=Plane(surface,pos).Value;s.worldNormal=pos.x<0?new Vector3(0,2,0):new Vector3(0,.5f,.8660254f);return s;};var p=Generate(Settings(),sample:sampler);Check(p.Length==8&&p.All(v=>v.worldRoot.x<0),"60-degree surface incorrectly accepted at35");Check(p.All(v=>Math.Abs(v.worldNormal.y-1)<.00001f),"world normal not normalized");});
  Run("zero density is empty without phantom placements",()=>{Check(Generate(Settings(0)).Length==0,"zero density is nonempty");});
  Run("nonfinite configuration is rejected",()=>{var s=Settings();s.density=float.NaN;Throws<ArgumentException>(()=>Generate(s));s=Settings();s.minHeight=float.PositiveInfinity;Throws<ArgumentException>(()=>Generate(s));s=Settings();s.maxDensity=0;Throws<ArgumentException>(()=>Generate(s));});
  Run("density above reference grid and invalid dimensions are rejected",()=>{Throws<ArgumentException>(()=>Generate(Settings(5)));var s=Settings();s.minWidth=-1;Throws<ArgumentException>(()=>Generate(s));s=Settings();s.maxHeight=.1f;Throws<ArgumentException>(()=>Generate(s));s=Settings();s.maxSlopeDegrees=91;Throws<ArgumentException>(()=>Generate(s));});
  Run("nonfinite sampled geometry aborts instead of persisting corrupt root",()=>{Throws<InvalidOperationException>(()=>Generate(Settings(),sample:(surface,pos)=>{var s=Plane(surface,pos).Value;s.localRoot.x=float.NaN;return s;}));});
  Run("placement quota aborts whole generation rather than biased truncation",()=>{var s=Settings();s.maximumPlacements=15;Throws<InvalidOperationException>(()=>Generate(s));s=Settings();s.maximumPlacements=16;Check(Generate(s).Length==16,"exact quota rejected");});
  Run("duplicate surface identity and unbounded grid are rejected",()=>{Throws<ArgumentException>(()=>Generate(Settings(),new[]{Area(0),Area(0)}));var s=Settings();s.maxDensity=1e20f;s.density=4;Throws<ArgumentException>(()=>Generate(s));});
  Run("adjacent target areas use global cells without duplicate roots",()=>{var p=Generate(Settings(),new[]{Area(0,-.5f,0,1,2),Area(1,.5f,0,1,2)});Check(p.Length==16,"split target area count differs");Check(p.Select(v=>F(v.worldRoot.x)+":"+F(v.worldRoot.z)).Distinct().Count()==p.Length,"duplicate world roots on adjacent targets");});
  Console.WriteLine("RESULT "+passed+" passed, "+failed+" failed");return failed==0?0:1;
 }
}
