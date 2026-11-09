/* 功能：合并少量交叉分段贴图面；每簇不是对象，根/权重写UV1供所有Pass共用。 */
using System;using System.Collections.Generic;using UnityEngine;using UnityEngine.Rendering;
namespace Ossastra.WorldRendering.Vegetation
{
 public static class GrassMeshBuilder
 {
  public sealed class Block{public Mesh mesh;public int count;public Vector2Int cell;}
  public static List<Block> Build(GrassLayerProfileSO profile,GrassPatchDataSO data,GrassSurfaceBinding[] surfaces,Transform owner,bool far=false)
  {
   if(profile==null||data==null||owner==null)throw new ArgumentException("草层配置、数据与持有者不可缺失");
   if(profile.chunkSize<=0||profile.verticalSegments<1||profile.verticalSegments>6)throw new ArgumentException("分块或纵段无效");
   var groups=new SortedDictionary<Vector2Int,List<GrassPlacement>>(Comparer<Vector2Int>.Create((a,b)=>a.x!=b.x?a.x.CompareTo(b.x):a.y.CompareTo(b.y)));
   foreach(var p in data.placements){if(far&&p.phase>Mathf.PI)continue;if(p.surfaceIndex<0||p.surfaceIndex>=surfaces.Length||surfaces[p.surfaceIndex]==null||surfaces[p.surfaceIndex].collider==null||data.surfaceIds[p.surfaceIndex]!=surfaces[p.surfaceIndex].id)throw new InvalidOperationException("显式表面身份改变；请重建放置数据");var t=surfaces[p.surfaceIndex].collider.transform;var world=t.TransformPoint(p.localRoot);var cell=new Vector2Int(Mathf.FloorToInt(world.x/profile.chunkSize),Mathf.FloorToInt(world.z/profile.chunkSize));if(!groups.TryGetValue(cell,out var list)){list=new List<GrassPlacement>();groups.Add(cell,list);}list.Add(p);}
   var result=new List<Block>();
   try{foreach(var group in groups){var vertices=new List<Vector3>();var normals=new List<Vector3>();var colors=new List<Color>();var uvs=new List<Vector2>();var roots=new List<Vector4>();var triangles=new List<int>();
    foreach(var p in group.Value){var t=surfaces[p.surfaceIndex].collider.transform;var world=t.TransformPoint(p.localRoot);var root=owner.InverseTransformPoint(world);var n=t.localToWorldMatrix.inverse.transpose.MultiplyVector(p.localNormal).normalized;var up=Vector3.Lerp(Vector3.up,n,.65f).normalized;var side=Vector3.Cross(up,new Vector3(Mathf.Sin(p.yaw*Mathf.Deg2Rad),0,Mathf.Cos(p.yaw*Mathf.Deg2Rad))).normalized;
     for(int card=0;card<2;card++){var right=card==0?side:Vector3.Cross(up,side);var normal=Vector3.Cross(right,up).normalized;int start=vertices.Count;
      for(int row=0;row<=profile.verticalSegments;row++){float w=(float)row/profile.verticalSegments;for(int edge=0;edge<2;edge++){var v=world+right*((edge-profile.footpoint.x)*p.width)+up*(w*p.height);vertices.Add(owner.InverseTransformPoint(v));colors.Add(new Color(p.phase/(Mathf.PI*2),0,0,1));normals.Add(owner.localToWorldMatrix.transpose.MultiplyVector(normal).normalized);uvs.Add(new Vector2(profile.uvRect.x+edge*profile.uvRect.width,profile.uvRect.y+(profile.footpoint.y+(1-profile.footpoint.y)*w)*profile.uvRect.height));roots.Add(new Vector4(root.x,root.y,root.z,w));}}
      for(int row=0;row<profile.verticalSegments;row++){int a=start+row*2;triangles.Add(a);triangles.Add(a+2);triangles.Add(a+1);triangles.Add(a+1);triangles.Add(a+2);triangles.Add(a+3);}
     }}
    var mesh=new Mesh{name="GrassBlock_"+group.Key,hideFlags=HideFlags.DontSave,indexFormat=vertices.Count>65000?IndexFormat.UInt32:IndexFormat.UInt16};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetColors(colors);mesh.SetUVs(0,uvs);mesh.SetUVs(1,roots);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();var bounds=mesh.bounds;float expansion=Mathf.Max(0,profile.maximumBend)*1.5f;bounds.Expand(new Vector3(expansion*2,expansion*.4f,expansion*2));mesh.bounds=bounds;result.Add(new Block{mesh=mesh,count=group.Value.Count,cell=group.Key});
   }return result;}catch{foreach(var b in result)Dispose(b.mesh);throw;}
  }
  public static void Dispose(UnityEngine.Object value){if(value==null)return;if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
 }
}
