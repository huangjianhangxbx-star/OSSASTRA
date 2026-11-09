/* 功能：显式烘焙一层地表的未受光Texture/tint基础色与有效alpha。
 * 边界：只绘给定MeshRenderer，不创建相机/扫描世界，不捕获草/角色/灯光/阴影，不每帧更新。
 * 生命周期：私有材质/RT/命令状态finally归还；返回Texture由Result持有，DetachTexture后转给调用者。
 */
#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ossastra.WorldRendering.Vegetation.Editor
{
 public static class TerrainBaseColorBaker
 {
  public sealed class Result:IDisposable
  {
   public Texture2D texture{get;private set;}
   public string surfaceId{get;}
   public Rect worldXZ{get;}
   public Matrix4x4 worldToUv{get;}
   public int version{get;}
   public int sourceCount{get;}
   public bool unlitBaseColor=>true;
   public bool linearTexture=>true;
   public string validity=>"Alpha1=explicit surface coverage; alpha0=outside/baked alpha cutout";
   internal Result(Texture2D value,string id,Rect rect,int revision,int count)
   {
    texture=value;surfaceId=id;worldXZ=rect;version=revision;sourceCount=count;
    var m=Matrix4x4.zero;m.SetRow(0,new Vector4(1/rect.width,0,0,-rect.x/rect.width));
    m.SetRow(1,new Vector4(0,0,1/rect.height,-rect.y/rect.height));m.SetRow(3,new Vector4(0,0,0,1));worldToUv=m;
   }
   public Texture2D DetachTexture(){var value=texture;texture=null;return value;}
   public void Dispose(){if(texture!=null)UnityEngine.Object.DestroyImmediate(texture);texture=null;}
  }

  public static Result Bake(string surfaceId,MeshRenderer[] sources,Bounds worldBounds,int resolution,Shader bakeShader,int version=1)
  {
   if(Application.isPlaying)throw new InvalidOperationException("地表基础色只在编辑态显式烘焙；运行时不拍摄世界。");
   if(string.IsNullOrWhiteSpace(surfaceId)||version<1)throw new ArgumentException("需要稳定surfaceId与正版本号。");
   if(sources==null||sources.Length==0||sources.Length>256)throw new ArgumentException("需要1到256个明确同层地表MeshRenderer。");
   if(!Finite(worldBounds.center)||!Finite(worldBounds.size)||worldBounds.size.x<=0||worldBounds.size.z<=0||worldBounds.size.y<0)
    throw new ArgumentException("世界Bounds必须有限且XZ覆盖尺寸为正。");
   if(resolution<16||resolution>2048)throw new ArgumentOutOfRangeException(nameof(resolution),"基础色图分辨率范围16至2048。");
   if(bakeShader==null||!bakeShader.isSupported)throw new InvalidOperationException("缺少可运行的专用基础色烘焙Shader。");
   // 在创建私有GPU资源之前校验明确源布局；拒绝混入自定义世界UV/多层合成源后假称正确还原。
   foreach(var renderer in sources)
   {
    if(renderer==null)throw new InvalidOperationException("明确地表集合含空引用。");
    if(renderer.HasPropertyBlock())throw new InvalidOperationException("地表含MaterialPropertyBlock覆写；基础色烘焙尚未适配，拒绝静默忽略："+renderer.name);
    var filter=renderer.GetComponent<MeshFilter>();var mesh=filter==null?null:filter.sharedMesh;
    if(mesh==null||mesh.vertexCount==0)throw new InvalidOperationException("明确地表没有可绘Mesh："+renderer.name);
    var materials=renderer.sharedMaterials;
    if(materials.Length!=mesh.subMeshCount)throw new InvalidOperationException("材质槽和submesh数量必须一致："+renderer.name);
    for(int slot=0;slot<materials.Length;slot++)
    {
     var source=materials[slot];if(source==null||source.shader==null||!Supported(source.shader.name))
      throw new InvalidOperationException("仅支持显式mesh UV的常规基础色源，拒绝未适配的自定义Shader："+renderer.name);
     if(mesh.GetTopology(slot)!=MeshTopology.Triangles)throw new InvalidOperationException("地表submesh必须为三角形。");
     if(source.HasProperty("_Surface")&&source.GetFloat("_Surface")>.5f)throw new InvalidOperationException("透明混合地表须另有明确基础色来源；本烘焙不猜合成顺序。");
    }
   }

   var rectangle=new Rect(worldBounds.min.x,worldBounds.min.z,worldBounds.size.x,worldBounds.size.z);
   var oldActive=RenderTexture.active;bool oldSrgbWrite=GL.sRGBWrite;Material material=null;RenderTexture target=null;Texture2D output=null;
   try
   {
    material=new Material(bakeShader){name="Terrain base color bake [private]",hideFlags=HideFlags.HideAndDontSave};
    int pass=material.FindPass("TerrainBaseColor");if(pass<0)throw new InvalidOperationException("烘焙Shader缺少TerrainBaseColor Pass。");
    target=new RenderTexture(resolution,resolution,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear)
     {name="Terrain base color [private]",antiAliasing=1,useMipMap=false,autoGenerateMips=false,hideFlags=HideFlags.HideAndDontSave};
    if(!target.Create())throw new InvalidOperationException("未创建私有基础色RT。");
    float top=worldBounds.max.y+1,far=worldBounds.size.y+2;
    var position=new Vector3(worldBounds.center.x,top,worldBounds.center.z);
    var view=Matrix4x4.Scale(new Vector3(1,1,-1))*Matrix4x4.TRS(position,Quaternion.Euler(90,0,0),Vector3.one).inverse;
    var projection=GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-rectangle.width*.5f,rectangle.width*.5f,-rectangle.height*.5f,rectangle.height*.5f,.01f,far),true);
    material.SetMatrix("_BakeViewProjection",projection*view);
    RenderTexture.active=target;GL.sRGBWrite=false;GL.Clear(true,true,Color.clear);
    foreach(var renderer in sources)
    {
     var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;var materials=renderer.sharedMaterials;
     for(int slot=0;slot<materials.Length;slot++)
     {
      var source=materials[slot];string textureName=source.HasProperty("_BaseMap")&&source.GetTexture("_BaseMap")!=null?"_BaseMap":source.HasProperty("_MainTex")?"_MainTex":null;
      Texture texture=textureName==null?null:source.GetTexture(textureName);material.SetTexture("_BakeMap",texture==null?Texture2D.whiteTexture:texture);
      Vector2 scale=textureName==null?Vector2.one:source.GetTextureScale(textureName),offset=textureName==null?Vector2.zero:source.GetTextureOffset(textureName);
      material.SetVector("_BakeST",new Vector4(scale.x,scale.y,offset.x,offset.y));
      material.SetColor("_BakeTint",source.HasProperty("_BaseColor")?source.GetColor("_BaseColor"):source.HasProperty("_Color")?source.GetColor("_Color"):Color.white);
      bool clip=source.IsKeywordEnabled("_ALPHATEST_ON")||(source.HasProperty("_AlphaClip")&&source.GetFloat("_AlphaClip")>.5f);
      material.SetFloat("_BakeClipAlpha",clip?1:0);material.SetFloat("_BakeCutoff",source.HasProperty("_Cutoff")?source.GetFloat("_Cutoff"):.5f);
      material.SetFloat("_BakeVertexColor",mesh.HasVertexAttribute(VertexAttribute.Color)?1:0);
      if(!material.SetPass(pass))throw new InvalidOperationException("专用基础色Pass不可绘制。");
      Graphics.DrawMeshNow(mesh,renderer.localToWorldMatrix,slot);
     }
    }
    output=new Texture2D(resolution,resolution,TextureFormat.RGBA32,false,true)
     {name="TerrainBaseColor_"+surfaceId,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
    output.ReadPixels(new Rect(0,0,resolution,resolution),0,0,false);output.Apply(false,false);
    var result=new Result(output,surfaceId,rectangle,version,sources.Length);output=null;return result;
   }
   finally
   {
    RenderTexture.active=oldActive;GL.sRGBWrite=oldSrgbWrite;
    if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);}
    if(material!=null)UnityEngine.Object.DestroyImmediate(material);
    if(output!=null)UnityEngine.Object.DestroyImmediate(output);
   }
  }
  static bool Supported(string name)=>name=="Universal Render Pipeline/Lit"||name=="Universal Render Pipeline/Simple Lit"||name=="Universal Render Pipeline/Unlit"||name=="Unlit/Texture"||name=="Unlit/Color"||name=="Standard";
  static bool Finite(Vector3 v)=>Finite(v.x)&&Finite(v.y)&&Finite(v.z);
  static bool Finite(float n)=>!float.IsNaN(n)&&!float.IsInfinity(n);
 }
}
#endif
