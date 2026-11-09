/* 功能：以固定世界网格和稳定种子生成二维草层根点；密度使用同一参考网格的嵌套子集。
 * 边界：只接收明确表面的采样函数与排除谓词，不查Physics/场景，不读图像、材质或Unity随机源。
 * 生命周期：生成失败不返回半成品；不持有Unity对象或跨帧缓存。采样/排除仅在显式生成时调用。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
namespace Ossastra.WorldRendering.Vegetation
{
 [Serializable]
 public struct GrassPlacement
 {
  public int surfaceIndex;
  public Vector3 worldRoot,localRoot,worldNormal,localNormal;
  public float height,width,yaw,phase;
  public int cellX,cellZ;
 }
 public struct GrassSurfaceArea { public int surfaceIndex;public Bounds worldBounds; }
 public struct GrassSurfaceSample { public Vector3 worldRoot,localRoot,worldNormal,localNormal; }
 [Serializable]
 public sealed class GrassScatterSettings
 {
  public int seed;
  public float density,maxDensity,minHeight,maxHeight,minWidth,maxWidth,maxSlopeDegrees;
  public int maximumPlacements;
 }
 public static class GrassScatter
 {
  const long MaximumCandidateCells=10000000;
  struct AreaGrid
  {
   internal int surfaceIndex,minX,maxX,minZ,maxZ;
   internal float left,right,back,front;
  }
  /// <summary>
  /// maxDensity defines cells/m² for all density variants; density only selects a stable subset.
  /// Sample null declines that exact surface. Invalid input/corrupt samples/quota overflow abort before persistence.
  /// width/height are world units, yaw is degrees and phase is radians in [0,2π).
  /// </summary>
  public static GrassPlacement[] Generate(GrassScatterSettings settings,IReadOnlyList<GrassSurfaceArea> surfaces,
   Func<int,Vector2,GrassSurfaceSample?> sample,Func<Vector3,bool> excluded)
  {
   Validate(settings);
   if(surfaces==null)throw new ArgumentNullException(nameof(surfaces));
   if(sample==null)throw new ArgumentNullException(nameof(sample));
   double spacing=1.0/Math.Sqrt(settings.maxDensity);
   var grids=new List<AreaGrid>(surfaces.Count);var ids=new HashSet<int>();long candidates=0;
   for(int i=0;i<surfaces.Count;i++)
   {
    var surface=surfaces[i];var bounds=surface.worldBounds;
    if(surface.surfaceIndex<0||!ids.Add(surface.surfaceIndex))throw new ArgumentException("Surface identities must be unique nonnegative indices.",nameof(surfaces));
    if(!Finite(bounds.center)||!Finite(bounds.extents)||bounds.extents.x<0||bounds.extents.y<0||bounds.extents.z<0)
     throw new ArgumentException("Surface bounds must be finite and nonnegative.",nameof(surfaces));
    var min=bounds.min;var max=bounds.max;
    if(!Finite(min)||!Finite(max))throw new ArgumentException("Surface bounds overflow.",nameof(surfaces));
    if(max.x<=min.x||max.z<=min.z)continue;
    double loX=Math.Floor(min.x/spacing),hiX=Math.Ceiling(max.x/spacing)-1;
    double loZ=Math.Floor(min.z/spacing),hiZ=Math.Ceiling(max.z/spacing)-1;
    if(loX<int.MinValue||hiX>int.MaxValue||loZ<int.MinValue||hiZ>int.MaxValue)
     throw new ArgumentException("Surface/reference density exceeds the supported grid range.",nameof(surfaces));
    double count=(hiX-loX+1)*(hiZ-loZ+1);
    if(count>MaximumCandidateCells||count+candidates>MaximumCandidateCells)
     throw new ArgumentException("Requested generation exceeds ten million reference cells; split target regions.",nameof(surfaces));
    candidates+=(long)count;
    grids.Add(new AreaGrid{surfaceIndex=surface.surfaceIndex,minX=(int)loX,maxX=(int)hiX,minZ=(int)loZ,maxZ=(int)hiZ,left=min.x,right=max.x,back=min.z,front=max.z});
   }
   if(settings.density==0||grids.Count==0)return Array.Empty<GrassPlacement>();
   grids.Sort((a,b)=>a.surfaceIndex.CompareTo(b.surfaceIndex));
   var result=new List<GrassPlacement>((int)Math.Min(candidates,settings.maximumPlacements));
   double densityFraction=(double)settings.density/settings.maxDensity;
   double slopeCos=Math.Cos(settings.maxSlopeDegrees*Math.PI/180.0);
   for(int a=0;a<grids.Count;a++)
   {
    var grid=grids[a];
    // long counters avoid wrap at the largest supported signed cell index.
    for(long x=grid.minX;x<=grid.maxX;x++)for(long z=grid.minZ;z<=grid.maxZ;z++)
    {
     uint hash=CellHash(settings.seed,grid.surfaceIndex,(int)x,(int)z);
     if(Unit(Mix(hash+0x9e3779b9u))>=densityFraction)continue;
     float worldX=(float)((x+.15+.7*Unit(Mix(hash+0x68bc21ebu)))*spacing);
     float worldZ=(float)((z+.15+.7*Unit(Mix(hash+0x02e5be93u)))*spacing);
     if(worldX<grid.left||worldX>=grid.right||worldZ<grid.back||worldZ>=grid.front)continue;
     var hit=sample(grid.surfaceIndex,new Vector2(worldX,worldZ));if(!hit.HasValue)continue;
     var surface=hit.Value;
     if(!Finite(surface.worldRoot)||!Finite(surface.localRoot)||!Finite(surface.worldNormal)||!Finite(surface.localNormal))
      throw new InvalidOperationException("The explicit surface sampler returned nonfinite geometry.");
     double tolerance=Math.Max(.00001,spacing*.01);
     if(Math.Abs((double)surface.worldRoot.x-worldX)>tolerance||Math.Abs((double)surface.worldRoot.z-worldZ)>tolerance)
      throw new InvalidOperationException("The surface sampler returned an anchor outside the requested XZ location.");
     if(surface.worldRoot.x<grid.left||surface.worldRoot.x>=grid.right||surface.worldRoot.z<grid.back||surface.worldRoot.z>=grid.front)continue;
     if(!Normalize(surface.worldNormal,out var worldNormal)||!Normalize(surface.localNormal,out var localNormal))continue;
     if(worldNormal.y+0.000001<slopeCos)continue;
     if(excluded!=null&&excluded(surface.worldRoot))continue;
     if(result.Count>=settings.maximumPlacements)throw new InvalidOperationException("Grass generation exceeds maximumPlacements; previous author data must be retained.");
     result.Add(new GrassPlacement{surfaceIndex=grid.surfaceIndex,worldRoot=surface.worldRoot,localRoot=surface.localRoot,worldNormal=worldNormal,localNormal=localNormal,
      height=Lerp(settings.minHeight,settings.maxHeight,Unit(Mix(hash+0x967a889bu))),
      width=Lerp(settings.minWidth,settings.maxWidth,Unit(Mix(hash+0x368cc8b7u))),
      yaw=(float)(360*Unit(Mix(hash+0xb5297a4du))),phase=(float)(2*Math.PI*Unit(Mix(hash+0x1b56c4e9u))),cellX=(int)x,cellZ=(int)z});
    }
   }
   return result.ToArray();
  }
  static void Validate(GrassScatterSettings s)
  {
   if(s==null)throw new ArgumentNullException(nameof(s));
   if(!Finite(s.density)||!Finite(s.maxDensity)||s.maxDensity<=0||s.density<0||s.density>s.maxDensity)
    throw new ArgumentException("Density must be finite, nonnegative and no greater than positive maxDensity.",nameof(s));
   if(!Finite(s.minHeight)||!Finite(s.maxHeight)||!Finite(s.minWidth)||!Finite(s.maxWidth)||s.minHeight<=0||s.maxHeight<s.minHeight||s.minWidth<=0||s.maxWidth<s.minWidth)
    throw new ArgumentException("Height and width intervals must be finite, positive and ordered.",nameof(s));
   if(!Finite(s.maxSlopeDegrees)||s.maxSlopeDegrees<0||s.maxSlopeDegrees>90||s.maximumPlacements<=0)
    throw new ArgumentException("Slope must be 0..90 degrees and maximumPlacements must be positive.",nameof(s));
  }
  static bool Finite(float n)=>!float.IsNaN(n)&&!float.IsInfinity(n);
  static bool Finite(Vector3 n)=>Finite(n.x)&&Finite(n.y)&&Finite(n.z);
  static bool Normalize(Vector3 value,out Vector3 normalized)
  {
   double length=Math.Sqrt((double)value.x*value.x+(double)value.y*value.y+(double)value.z*value.z);
   if(length<.0000001){normalized=default;return false;}
   normalized=new Vector3((float)(value.x/length),(float)(value.y/length),(float)(value.z/length));return true;
  }
  static float Lerp(float min,float max,double t)=>(float)(min+((double)max-min)*t);
  static double Unit(uint value)=>(value>>8)*(1.0/16777216.0);
  static uint CellHash(int seed,int surface,int x,int z)
  {unchecked{return Mix((uint)seed^Mix((uint)surface+0x9e3779b9u)^Mix((uint)x+0x85ebca6bu)^Mix((uint)z+0xc2b2ae35u));}}
  static uint Mix(uint value)
  {unchecked{value^=value>>16;value*=0x7feb352du;value^=value>>15;value*=0x846ca68bu;return value^(value>>16);}}
 }
}
