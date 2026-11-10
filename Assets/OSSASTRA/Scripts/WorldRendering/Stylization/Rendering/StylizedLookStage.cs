// Camera-local style composition. The caller owns legal color/depth/coverage/glow;
// this stage owns only its intermediate HDR chain and cached materials.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ossastra.WorldRendering.Stylization
{
 public sealed class StylizedLookStage : IDisposable
 {
  static readonly ProfilingSampler hdrSourceSampler = new ProfilingSampler("Style.HDRSource");
  static readonly ProfilingSampler logicalGridSampler = new ProfilingSampler("Style.LogicalGrid");
  static readonly ProfilingSampler bloomDownSampler = new ProfilingSampler("Style.Bloom.Down");
  static readonly ProfilingSampler bloomUpSampler = new ProfilingSampler("Style.Bloom.Up");
  static readonly ProfilingSampler compositeSampler = new ProfilingSampler("Style.Composite");
  // Recorders stay off unless the bounded diagnostic caller explicitly enables
  // them. A zero GPU sample count means unavailable/not-executed, not a 0 ms cost.
  public static IReadOnlyList<ProfilingSampler> ProfilingSamplers { get; } = Array.AsReadOnly(new[]
   { hdrSourceSampler, logicalGridSampler, bloomDownSampler, bloomUpSampler, compositeSampler });
  readonly Material[] composite = new Material[3];
  readonly Material[] downMaterials = new Material[4], upMaterials = new Material[3];
  readonly RTHandle[] down = new RTHandle[4], up = new RTHandle[3];
  RTHandle styleHDR, grid;
  StylizedLookSettings current;
  int width, height;
  bool prepared;
  public string Status { get; private set; } = "Waiting for a qualified HDR frame.";
  public int TextureCount { get { int n=Count(styleHDR)+Count(grid);foreach(var t in down)n+=Count(t);foreach(var t in up)n+=Count(t);return n; } }
  public int MaterialCount { get { int n=0;foreach(var m in composite)if(m!=null)n++;foreach(var m in downMaterials)if(m!=null)n++;foreach(var m in upMaterials)if(m!=null)n++;return n; } }
  public long EstimatedBytes { get { long n=Bytes(styleHDR)+Bytes(grid);foreach(var t in down)n+=Bytes(t);foreach(var t in up)n+=Bytes(t);return n; } }
  static int Count(RTHandle t)=>t!=null?1:0;
  static long Bytes(RTHandle t)=>t!=null&&t.rt!=null?(long)t.rt.width*t.rt.height*8:0;

  public StylizedLookStage(Shader compositeShader, Shader bloomShader)
  {
   if(compositeShader!=null&&compositeShader.isSupported)
    for(int i=0;i<composite.Length;i++) composite[i]=Create(compositeShader,"Stylized composite "+i);
   if(bloomShader!=null&&bloomShader.isSupported)
   {
    for(int i=0;i<downMaterials.Length;i++)downMaterials[i]=Create(bloomShader,"Stylized down "+i);
    for(int i=0;i<upMaterials.Length;i++)upMaterials[i]=Create(bloomShader,"Stylized up "+i);
   }
  }
  static Material Create(Shader shader,string name) { var m=CoreUtils.CreateEngineMaterial(shader);m.name=name;return m; }
  public bool Prepare(RenderTextureDescriptor descriptor, StylizedLookSettings settings)
  {
   prepared=false;current=settings;
   if(settings==null||!settings.Enabled) { ReleaseTextures();Status="Style disabled; original WorldLook remains authoritative.";return false; }
   if(descriptor.width<2||descriptor.height<2||descriptor.dimension!=TextureDimension.Tex2D||descriptor.volumeDepth!=1)
    return Reject("Only a non-XR 2D camera target is certified.");
   if(!SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat,FormatUsage.Render)||!SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat,FormatUsage.Sample))return Reject("RGBA16F render/sample support is unavailable.");
   if(!Ready(composite[0],"StyleHDR")||!Ready(composite[1],"LogicalGrid")||!Ready(composite[2],"Composite"))return Reject("Style shader/pass unavailable.");
   if(settings.BloomEnabled)
   {
    for(int i=0;i<4;i++)if(!Ready(downMaterials[i],i==0?"Prefilter":"Downsample"))return Reject("Bloom reduction shader/pass unavailable.");
    for(int i=0;i<3;i++)if(!Ready(upMaterials[i],"Upsample"))return Reject("Bloom upsample shader/pass unavailable.");
   }
   width=descriptor.width;height=descriptor.height;
   descriptor.depthBufferBits=0;descriptor.depthStencilFormat=GraphicsFormat.None;
   descriptor.graphicsFormat=GraphicsFormat.R16G16B16A16_SFloat;
   descriptor.msaaSamples=1;descriptor.bindMS=false;descriptor.useMipMap=false;descriptor.autoGenerateMips=false;
   descriptor.enableRandomWrite=false;descriptor.memoryless=RenderTextureMemoryless.None;descriptor.useDynamicScale=false;
   if(settings.BloomEnabled)Allocate(ref styleHDR,descriptor,width,height,"Stylized linear HDR");
   else Release(ref styleHDR);
   if(settings.PixelSize>1) Allocate(ref grid,descriptor,(width+settings.PixelSize-1)/settings.PixelSize,(height+settings.PixelSize-1)/settings.PixelSize,"Stylized area mean grid");
   else Release(ref grid);
   if(settings.BloomEnabled)
   {
    for(int i=0;i<4;i++)
    {
     int w=Mathf.Max(2,width>>(i+1)),h=Mathf.Max(2,height>>(i+1));
     Allocate(ref down[i],descriptor,w,h,"Stylized bloom down "+i);
     if(i<3)Allocate(ref up[i],descriptor,w,h,"Stylized bloom up "+i);
    }
   }
   else { for(int i=0;i<4;i++)Release(ref down[i]);for(int i=0;i<3;i++)Release(ref up[i]); }
   prepared=(!settings.BloomEnabled||Valid(styleHDR))&&(settings.PixelSize==1||Valid(grid));
   if(settings.BloomEnabled) {foreach(var t in down)prepared&=Valid(t);foreach(var t in up)prepared&=Valid(t);}
   if(!prepared)return Reject("HDR intermediate allocation failed.");
   Status="Prepared, awaiting current-frame legal inputs.";return true;
  }
  bool Reject(string reason) { ReleaseTextures();Status=reason;return false; }
  static bool Ready(Material m,string pass)=>m!=null&&m.shader!=null&&m.shader.isSupported&&m.FindPass(pass)>=0;
  static bool Valid(RTHandle t)=>t!=null&&t.rt!=null&&t.rt.IsCreated();
  static void Allocate(ref RTHandle handle,RenderTextureDescriptor d,int w,int h,string name)
  {d.width=w;d.height=h;RenderingUtils.ReAllocateIfNeeded(ref handle,d,FilterMode.Point,TextureWrapMode.Clamp,name:name);}
  static Vector4 Size(RTHandle t)=>new Vector4(t.rt.width,t.rt.height,1f/t.rt.width,1f/t.rt.height);
  bool SameSize(RTHandle t,bool sampled=true)=>Valid(t)&&t.rt.width==width&&t.rt.height==height&&(!sampled||t.rt.antiAliasing==1);
  static bool IsHDR(GraphicsFormat format)=>format==GraphicsFormat.R16G16B16A16_SFloat||format==GraphicsFormat.R32G32B32A32_SFloat||format==GraphicsFormat.B10G11R11_UFloatPack32;

  public bool Execute(CommandBuffer cmd, RTHandle source, RTHandle destination, RTHandle colorCopy,
   RTHandle legalDepth, RTHandle styleCoverage, RTHandle characterGlow, CameraData cameraData, float observerEyeDepth)
  {
   if(!prepared||current==null||!current.Enabled||cmd==null) {Status="Stage was not prepared; no writes recorded.";return false;}
   var camera=cameraData.camera;
   if(float.IsNaN(cameraData.renderScale)||float.IsInfinity(cameraData.renderScale)||Mathf.Abs(cameraData.renderScale-1f)>.0001f)
   {Status="Only renderScale 1 is certified for final-output pixel blocks; original chain retained.";return false;}
   if(camera==null||camera.stereoEnabled||camera.rect!=new Rect(0,0,1,1)||cameraData.postProcessEnabled)
   {Status="Full viewport, non-XR, post-processing-off camera required; no writes recorded.";return false;}
   if(!SameSize(source,false)||!SameSize(destination,false)||!SameSize(colorCopy)||!SameSize(legalDepth)||!SameSize(styleCoverage,false)||
      (styleCoverage.rt.antiAliasing>1&&styleCoverage.rt.bindTextureMS)||colorCopy.rt==destination.rt)
   {Status="Legal input size/sample/alias contract failed; no writes recorded.";return false;}
   // A regular UNorm color target would already have discarded the HDR bright
   // range. Fail before touching the destination rather than pretending to recover it.
   if(!IsHDR(source.rt.graphicsFormat)||!IsHDR(colorCopy.rt.graphicsFormat))
   {Status="Legal source is not a linear HDR target; original chain retained.";return false;}
   bool hasGlow=characterGlow!=null;
   if(hasGlow&&!SameSize(characterGlow)) {Status="Independent glow dimensions invalid; no writes recorded.";return false;}
   if(float.IsNaN(observerEyeDepth)||float.IsInfinity(observerEyeDepth)) {Status="Observer depth is non-finite; no writes recorded.";return false;}
   // Complete preflight above: no partial composite/fallback can duplicate glow.
   if(current.PixelSize>1)
   {
    using(new ProfilingScope(cmd,logicalGridSampler))
    {
     Bind(composite[1],legalDepth,styleCoverage,characterGlow,cameraData,observerEyeDepth);
     Blitter.BlitCameraTexture(cmd,colorCopy,grid,composite[1],1);
    }
   }
   if(current.BloomEnabled)
   {
    using(new ProfilingScope(cmd,hdrSourceSampler))
    {
     Bind(composite[0],legalDepth,styleCoverage,characterGlow,cameraData,observerEyeDepth);
     Blitter.BlitCameraTexture(cmd,colorCopy,styleHDR,composite[0],0);
    }
    using(new ProfilingScope(cmd,bloomDownSampler))
    {
     RTHandle input=styleHDR;
     for(int i=0;i<4;i++)
     {
      var m=downMaterials[i];BindBloom(m,input,down[i],styleCoverage);
      Blitter.BlitCameraTexture(cmd,input,down[i],m,i==0?0:1);input=down[i];
     }
    }
    using(new ProfilingScope(cmd,bloomUpSampler))
    {
     RTHandle low=down[3];
     for(int i=2;i>=0;i--)
     {
      var m=upMaterials[i];BindBloom(m,down[i],up[i],styleCoverage);
      m.SetTexture("_LD_LowTexture",low.rt);m.SetVector("_LD_LowSize",Size(low));
      Blitter.BlitCameraTexture(cmd,down[i],up[i],m,2);low=up[i];
     }
    }
   }
   using(new ProfilingScope(cmd,compositeSampler))
   {
    var final=composite[2];Bind(final,legalDepth,styleCoverage,characterGlow,cameraData,observerEyeDepth);
    final.SetTexture("_LD_Grid",grid!=null?grid.rt:Texture2D.blackTexture);
    final.SetVector("_LD_GridSize",grid!=null?Size(grid):Vector4.one);
    final.SetTexture("_LD_BloomTexture",current.BloomEnabled?up[0].rt:Texture2D.blackTexture);
    final.SetVector("_LD_BloomSize",current.BloomEnabled?Size(up[0]):Vector4.one);
    // Only this last command touches destination. It reads caller's copy, never
    // source/destination themselves; passing source==destination is intentional.
    Blitter.BlitCameraTexture(cmd,colorCopy,destination,final,2);
   }
   Status="Linear style + legal four-level HDR Bloom + area grid + one final NeutralTonemap.";return true;
  }
  void Bind(Material m,RTHandle depth,RTHandle coverage,RTHandle glow,CameraData data,float observer)
  {
   var s=current;var camera=data.camera;
   m.SetTexture("_LD_Depth",depth.rt);m.SetTexture("_LD_Coverage",coverage.rt);
   m.SetTexture("_LD_CharacterGlow",glow!=null?glow.rt:Texture2D.blackTexture);
   m.SetVector("_LD_Size",new Vector4(width,height,1f/width,1f/height));
   m.SetVector("_LD_Features",new Vector4(s.ThemeEnabled?1:0,s.AirEnabled?1:0,s.BandsEnabled?1:0,s.ToneMapping?1:0));
   m.SetVector("_LD_PixelDebug",new Vector4(s.PixelSize,s.DebugView,s.BloomEnabled?1:0,glow!=null?1:0));
   m.SetColor("_LD_Shadow",s.ShadowColor);m.SetColor("_LD_Mid",s.MidColor);m.SetColor("_LD_Light",s.LightColor);
   m.SetVector("_LD_Theme",new Vector4(s.EnvironmentStrength,s.ActorStrength,s.Pivot,s.Contrast));
   m.SetVector("_LD_Warm",new Vector4(s.WarmGuardStrength,s.WarmGuardLow,s.WarmGuardHigh,0));
   m.SetVector("_LD_Mode",new Vector4((float)s.ThemeMode,s.VisibleSourceBloom?1:0,0,0));
   m.SetVector("_LD_Grade",new Vector4(s.LedEnvironmentStrength,s.LedActorStrength,0,0));
   m.SetVector("_LD_GradeShadow",s.LedShadowGain);m.SetVector("_LD_GradeMid",s.LedMidGain);m.SetVector("_LD_GradeLight",s.LedLightGain);
   m.SetColor("_LD_AirNear",s.AirNear);m.SetColor("_LD_AirMid",s.AirMid);m.SetColor("_LD_AirFar",s.AirFar);
   m.SetVector("_LD_Density",new Vector4(s.DistanceDensity,s.HeightDensity,s.HeightFalloff,s.BaseHeight));
   m.SetVector("_LD_AirRange",new Vector4(observer,s.StartOffset,s.FarOffset,s.MaxOpacity));
   m.SetVector("_LD_Bands",new Vector4(s.BandLevels,s.EnvironmentBandStrength,s.ActorBandStrength,s.ReferenceWhite));
   var p=camera.transform.position;m.SetVector("_LD_Camera",new Vector4(p.x,p.y,p.z,camera.orthographic?1:0));
   m.SetVector("_LD_Clip",new Vector4(camera.nearClipPlane,s.BloomIntensity,0,0));
   m.SetMatrix("_LD_InverseVP",(data.GetGPUProjectionMatrix()*data.GetViewMatrix()).inverse);m.SetMatrix("_LD_View",data.GetViewMatrix());
  }
  void BindBloom(Material m,RTHandle input,RTHandle output,RTHandle coverage)
  {
   m.SetVector("_LD_Mode",new Vector4((float)current.ThemeMode,current.VisibleSourceBloom?1:0,0,0));
   var s=current;m.SetVector("_LD_SourceSize",new Vector4(input.rt.width,input.rt.height,output.rt.width,output.rt.height));
   m.SetTexture("_LD_Coverage",coverage.rt);m.SetVector("_LD_Bloom",new Vector4(s.BloomThreshold,s.BloomKnee,s.BloomMaxPeak,s.ActorBloomWeight));m.SetFloat("_LD_Scatter",s.BloomScatter);
  }
  static void Release(ref RTHandle t) {t?.Release();t=null;}
  void ReleaseTextures() {prepared=false;Release(ref styleHDR);Release(ref grid);for(int i=0;i<4;i++)Release(ref down[i]);for(int i=0;i<3;i++)Release(ref up[i]);}
  public void Dispose()
  {
   ReleaseTextures();current=null;
   for(int i=0;i<composite.Length;i++){CoreUtils.Destroy(composite[i]);composite[i]=null;}
   for(int i=0;i<downMaterials.Length;i++){CoreUtils.Destroy(downMaterials[i]);downMaterials[i]=null;}
   for(int i=0;i<upMaterials.Length;i++){CoreUtils.Destroy(upMaterials[i]);upMaterials[i]=null;}
   Status="All style-owned textures and materials released.";
  }
 }
}
