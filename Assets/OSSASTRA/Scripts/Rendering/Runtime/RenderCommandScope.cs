/* 功能：仅为本模块命令缓冲的实际绘制限定Shader编译策略，避免编辑器青色占位写进世界/许可/光感数据。
 * 边界：不修改项目全局异步选项，不全量预热；成对的命令恢复上游状态，Player不引入UnityEditor依赖。
 * 取舍：新变体首次使用可能局部等待编译，以正确输出优先；不宣称消除GPU平台或变体裁剪故障。
 */
using System;using UnityEngine.Rendering;
namespace Ossastra.Rendering
{
 public readonly struct RenderCommandScope:IDisposable
 {
  readonly CommandBuffer commands;
  public RenderCommandScope(CommandBuffer commands)
  {
   this.commands=commands??throw new ArgumentNullException(nameof(commands));
#if UNITY_EDITOR
   UnityEditor.ShaderUtil.SetAsyncCompilation(commands,false);
#endif
  }
  public void Dispose()
  {
#if UNITY_EDITOR
   if(commands!=null)UnityEditor.ShaderUtil.RestoreAsyncCompilation(commands);
#endif
  }
 }
}
