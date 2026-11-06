/* 功能：单相机角色增强阶段及其资源所有者；持有完整覆盖/外缘缓存，房间Pass只协调调用顺序。
 * 边界：不产生观察许可、不接管世界清除或最终颜色权威；每个可选阶段只构造自己的命令，失败弃掉半成命令并局部降级。
 * 生命周期：owner、Shader、尺寸或成员改变后可恢复；构造异常按配置签名锁存，既有CoverageInvalidated也会清理并解除锁存。
 * 限制：命令构造可丢弃，GPU提交并非可回滚事务；底层提交失败不伪装为已恢复的可选错误。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Ossastra.SceneVisibility.Rooms.Rendering;

namespace Ossastra.CharacterLighting.Rendering
{
 public sealed class CharacterLightingRenderStage:IDisposable
 {
  readonly Camera camera;
  readonly List<Material> signatureSlots=new List<Material>(8);
  readonly Action<CommandBuffer> buildCoverage,buildOuter;
  // 仅在一次同步构造调用内借用；finally归还引用，不获取相机RT/许可图的所有权。
  Matrix4x4 currentViewProjection;
  RenderTextureDescriptor currentDescriptor;
  RTHandle currentColor,currentDepth,currentPermission;
  RoomMaskFrame currentFrame;
  Action<CommandBuffer,RoomPositiveSurface> currentUploadOwner;
  CharacterLightingSystem owner;
  CharacterCoverageCache coverage;
  CharacterOuterGlowCache outer;
  Shader coverageShader,seedShader,compositeShader,depthShader;
  bool coverageFault,outerFault,outerComplete,diagnosticRequested;
  int coverageFaultKey,outerFaultKey;
  public CharacterCoverageCache Coverage=>coverage;
  public RTHandle OuterEnergy=>outerComplete&&outer!=null?outer.Energy:null;
  public bool DiagnosticActive=>diagnosticRequested;
  public string CoverageError{get;private set;}="";
  public string OuterError{get;private set;}="";
  public bool HasFault=>!string.IsNullOrEmpty(CoverageError)||!string.IsNullOrEmpty(OuterError);
  public int OwnedTextureCount=>(coverage!=null&&coverage.Atlas!=null?1:0)+(outer==null?0:outer.OwnedTextureCount);
  public int OwnedMaterialCount=>(coverage==null?0:coverage.OwnedMaterialCount)+(outer==null?0:outer.OwnedMaterialCount);
  public CharacterLightingRenderStage(Camera camera)
  {this.camera=camera;buildCoverage=BuildCoverage;buildOuter=BuildOuter;}

  public void PrepareCoverage(ScriptableRenderContext context,Matrix4x4 viewProjection,RenderTextureDescriptor descriptor,RTHandle color,RTHandle depth)
  {
   // 旧能量不能跨本相机的新一次绘制继续被WorldLook消费。
   outerComplete=diagnosticRequested=false;
   if(!CharacterLightingSystem.TryGet(camera,out var next)){SetOwner(null);return;}
   SetOwner(next);int key=Signature(descriptor);
   if(coverageFault&&coverageFaultKey==key)return;
   if(coverageFault){coverageFault=false;CoverageError="";}
   if(owner.receivers==null){FailCoverage(key,"角色接收者数组无效");return;}
   currentViewProjection=viewProjection;currentDescriptor=descriptor;currentColor=color;currentDepth=depth;
   var cmd=CommandBufferPool.Get("Character optional coverage");
   try
   {
    if(!TryBuildOptional(cmd,buildCoverage,out string error)){FailCoverage(key,error);return;}
    // 提交失败属于更低层的渲染故障，交还核心调用者报告，不承诺已经回滚GPU。
    context.ExecuteCommandBuffer(cmd);CoverageError="";owner.ReportCoverage(coverage);
   }
   finally{ClearBorrowedState();CommandBufferPool.Release(cmd);}
  }

  void BuildCoverage(CommandBuffer cmd)
  {
   if(!owner.WantsCoverage||owner.coverageShader==null){ReleaseCoverage();return;}
   if(coverage==null||coverageShader!=owner.coverageShader){ReleaseCoverage();coverageShader=owner.coverageShader;coverage=new CharacterCoverageCache(coverageShader);}
   coverage.Append(cmd,camera,currentViewProjection,currentDescriptor.width,currentDescriptor.height);
   CoreUtils.SetRenderTarget(cmd,currentColor,currentDepth);cmd.SetViewport(new Rect(0,0,currentDescriptor.width,currentDescriptor.height));
  }

  public void AppendOuter(ScriptableRenderContext context,RenderTextureDescriptor descriptor,RTHandle color,RTHandle depth,RTHandle permission,RoomMaskFrame frame,Action<CommandBuffer,RoomPositiveSurface> uploadOwner)
  {
   outerComplete=diagnosticRequested=false;
   if(owner==null||owner.receivers==null||coverage==null||coverage.ActiveActorCount==0||
      owner.outerGlowSeedShader==null||owner.outerGlowCompositeShader==null||owner.outerGlowDepthShader==null)
   {ReleaseOuter();return;}
   int key=Signature(descriptor);
   if(outerFault&&outerFaultKey==key)return;
   if(outerFault){outerFault=false;OuterError="";}
   currentDescriptor=descriptor;currentColor=color;currentDepth=depth;currentPermission=permission;currentFrame=frame;currentUploadOwner=uploadOwner;
   var cmd=CommandBufferPool.Get("Character optional outer glow");
   try
   {
    if(!TryBuildOptional(cmd,buildOuter,out string error))
    {ReleaseOuter();outerFault=true;outerFaultKey=key;OuterError="外缘命令构造失败："+error;owner.ReportOuterGlow(0,0,OuterError);return;}
    context.ExecuteCommandBuffer(cmd);OuterError="";
    outerComplete=outer!=null&&outer.ActiveActorCount>0&&outer.Energy!=null&&outer.Energy.rt!=null&&outer.Energy.rt.IsCreated();
   }
   finally{ClearBorrowedState();CommandBufferPool.Release(cmd);}
  }

  void BuildOuter(CommandBuffer cmd)
  {
   if(!owner.WantsOuterGlow){ReleaseOuter();return;}
   if(outer==null||seedShader!=owner.outerGlowSeedShader||compositeShader!=owner.outerGlowCompositeShader||depthShader!=owner.outerGlowDepthShader)
   {ReleaseOuter();seedShader=owner.outerGlowSeedShader;compositeShader=owner.outerGlowCompositeShader;depthShader=owner.outerGlowDepthShader;outer=new CharacterOuterGlowCache(seedShader,compositeShader,depthShader);}
   outer.Append(cmd,camera,currentDescriptor,currentColor,currentDepth,currentPermission,owner,coverage,currentFrame,currentUploadOwner);
   // 诊断只有确实构造了该输出才撤后续Look；受支持性早退不算已清黑诊断。
   diagnosticRequested=owner.OuterDiagnostic&&outer.DrawCount>0;
  }
  void ClearBorrowedState()
  {currentColor=currentDepth=currentPermission=null;currentFrame=null;currentUploadOwner=null;currentDescriptor=default;currentViewProjection=default;}

  // 核心颜色/深度与标签许可已由调用者提交；此边界不执行命令，因此失败时可以真正丢弃半成GPU工作。
  static bool TryBuildOptional(CommandBuffer cmd,Action<CommandBuffer> build,out string error)
  {
   cmd.Clear();error="";
   try{using(new Ossastra.Rendering.RenderCommandScope(cmd))build(cmd);return true;}
   catch(Exception failure){cmd.Clear();error=failure.GetType().Name+": "+failure.Message;return false;}
  }
  void FailCoverage(int key,string error)
  {ReleaseCoverage();coverageFault=true;coverageFaultKey=key;CoverageError="轮廓命令构造失败："+error;}
  void SetOwner(CharacterLightingSystem next)
  {
   if(ReferenceEquals(owner,next))return;
   if(!ReferenceEquals(owner,null))owner.CoverageInvalidated-=Invalidate;
   ReleaseCoverage();owner=next;coverageFault=outerFault=false;CoverageError=OuterError="";
   if(owner!=null)owner.CoverageInvalidated+=Invalidate;
  }
  void Invalidate(){ReleaseCoverage();coverageFault=outerFault=false;CoverageError=OuterError="";}
  void ReleaseCoverage(){coverage?.Dispose();coverage=null;coverageShader=null;ReleaseOuter();if(owner!=null)owner.ReportCoverage(null);}
  void ReleaseOuter(){outerComplete=diagnosticRequested=false;outer?.Dispose();outer=null;seedShader=compositeShader=depthShader=null;if(owner!=null)owner.ReportOuterGlow(0,0,"外缘已释放");}
  int Signature(RenderTextureDescriptor descriptor)
  {
   unchecked
   {
    int h=descriptor.width*397^descriptor.height;h=h*397^descriptor.msaaSamples;h=h*397^(int)descriptor.dimension;
    if(owner==null)return h;h=h*397^owner.GetInstanceID();
    h=ShaderSignature(h,owner.coverageShader);h=ShaderSignature(h,owner.outerGlowSeedShader);h=ShaderSignature(h,owner.outerGlowCompositeShader);h=ShaderSignature(h,owner.outerGlowDepthShader);
    if(owner.receivers!=null)foreach(var receiver in owner.receivers)
    {
     if(receiver==null){h=h*397;continue;}h=h*397^receiver.GetInstanceID();h=h*397^(receiver.isActiveAndEnabled?1:0);
     var settings=receiver.Settings;h=h*397^(settings.rim?1:0);h=h*397^(settings.outerGlow?1:0);h=h*397^(settings.outerDiagnostic?1:0);h=h*397^settings.innerWidth.GetHashCode();h=h*397^settings.outerWidth.GetHashCode();
     h=h*397^settings.rimStrength.GetHashCode();h=h*397^settings.outerStrength.GetHashCode();h=h*397^settings.maxOuterGain.GetHashCode();h=h*397^settings.outerSoftness.GetHashCode();
     foreach(var r in receiver.Members)
     {
      if(r==null)continue;h=h*397^r.GetInstanceID();h=h*397^(r.enabled&&r.gameObject.activeInHierarchy?1:0);r.GetSharedMaterials(signatureSlots);
      foreach(var material in signatureSlots)
      {if(material==null){h=h*397;continue;}h=h*397^material.GetInstanceID();h=ShaderSignature(h,material.shader);var texture=material.HasProperty("_MainTex")?material.GetTexture("_MainTex"):null;h=h*397^(texture==null?0:texture.GetInstanceID());}
     }
    }
    return h;
   }
  }
  static int ShaderSignature(int hash,Shader shader){unchecked{return hash*397^(shader==null?0:shader.GetInstanceID());}}
  public void Suspend(){SetOwner(null);}
  public void Dispose(){SetOwner(null);ReleaseCoverage();ClearBorrowedState();signatureSlots.Clear();}
 }
}
