/* 功能：按相机协调可选环境命令的准备、构造隔离与局部降级，保持世界许可独立。
 * 边界：不持有天空/雨资源、不修改环境快照；资源仍由EnvironmentSkyBinding归还。本阶段不授予观察许可。
 * 生命周期：委托只创建一次；每次同步调用结束立即归还借用的frame/RenderingData引用。
 * 故障：命令构造失败丢弃半成缓冲，Surface失败只停自有环境全局；GPU提交失败仍交核心安全处理。
 */
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Ossastra.EnvironmentSimulation.Rendering;

namespace Ossastra.SceneVisibility.Rooms.Rendering
{
 internal sealed class RoomEnvironmentRenderStage
 {
  readonly Camera camera;
  readonly Action<CommandBuffer> uploadSurface,appendSky,appendWeather;
  RoomMaskFrame currentFrame;
  RenderingData currentData;
  bool surfacePrepareFailed;
  string surfaceError="",skyError="",rainError="";
  internal bool HasFault=>surfaceError.Length!=0||skyError.Length!=0||rainError.Length!=0;
  internal string Error=>surfaceError+" "+skyError+" "+rainError;
  internal RoomEnvironmentRenderStage(Camera camera)
  {this.camera=camera;uploadSurface=BuildSurface;appendSky=BuildSky;appendWeather=BuildWeather;}

  internal void PrepareSurface()
  {
   surfaceError=skyError=rainError="";surfacePrepareFailed=false;
   try{EnvironmentSkyBinding.PrepareSurfaceForCamera(camera,out _);}
   catch(Exception failure)
   {surfacePrepareFailed=true;surfaceError="环境工作集准备失败；仅停环境受光："+Describe(failure);}
  }

  internal void UploadSurface(ScriptableRenderContext context,RoomMaskFrame frame)
  {
   currentFrame=frame;
   try
   {
    if(RunOptional(context,"Room optional surface environment",uploadSurface,out string error))return;
    surfaceError="环境受光命令构造失败；仅停环境受光："+error;
    // 先前未执行任何半成环境命令，只需关掉本模块上次可能留下的全局量，不碰真实灯光/世界许可。
    var cmd=CommandBufferPool.Get("Room optional environment disabled");
    try
    {using(new Ossastra.Rendering.RenderCommandScope(cmd))EnvironmentSkyBinding.ClearSurfaceForCamera(cmd);context.ExecuteCommandBuffer(cmd);}
    finally{CommandBufferPool.Release(cmd);}
   }
   finally{currentFrame=null;}
  }

  internal void AppendSky(ScriptableRenderContext context,RoomMaskFrame frame)
  {
   currentFrame=frame;
   try{if(!RunOptional(context,"Room optional sky",appendSky,out string error))skyError="天空命令构造失败；仅停天空："+error;}
   finally{currentFrame=null;}
  }

  internal void AppendWeather(ScriptableRenderContext context,RoomMaskFrame frame,ref RenderingData renderingData)
  {
   currentFrame=frame;currentData=renderingData;
   try{if(!RunOptional(context,"Room optional rain",appendWeather,out string error))rainError="降雨命令构造失败；仅停雨："+error;}
   finally{currentFrame=null;currentData=default;}
  }

  void BuildSurface(CommandBuffer cmd)
  {if(surfacePrepareFailed)EnvironmentSkyBinding.ClearSurfaceForCamera(cmd);else EnvironmentSkyBinding.UploadForCamera(cmd,camera,currentFrame);}
  void BuildSky(CommandBuffer cmd){EnvironmentSkyBinding.AppendSkyForCamera(cmd,camera,currentFrame);}
  void BuildWeather(CommandBuffer cmd){EnvironmentSkyBinding.AppendWeatherForCamera(cmd,camera,currentFrame,ref currentData);}

  static bool RunOptional(ScriptableRenderContext context,string name,Action<CommandBuffer> build,out string error)
  {
   var cmd=CommandBufferPool.Get(name);
   try
   {
    if(!TryBuildOptional(cmd,build,out error))return false;
    // 提交错误不在构造保护内：已经提交的GPU工作无法撤回，不能假报局部恢复。
    context.ExecuteCommandBuffer(cmd);return true;
   }
   finally{CommandBufferPool.Release(cmd);}
  }
  static bool TryBuildOptional(CommandBuffer cmd,Action<CommandBuffer> build,out string error)
  {
   cmd.Clear();error="";
   try{using(new Ossastra.Rendering.RenderCommandScope(cmd))build(cmd);return true;}
   catch(Exception failure){cmd.Clear();error=Describe(failure);return false;}
  }
  static string Describe(Exception failure)=>failure.GetType().Name+": "+failure.Message;
 }
}
