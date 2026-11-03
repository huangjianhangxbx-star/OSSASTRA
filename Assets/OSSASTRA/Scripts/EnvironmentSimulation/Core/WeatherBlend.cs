/* 功能：将真实经过时间转为天气混合进度；零时长立即完成，不因帧率变化改变时长。 */
using System;
namespace Ossastra.EnvironmentSimulation
{
 public static class WeatherBlend
 {
  public static double Progress(double elapsed,double duration)
  {
   if(double.IsNaN(elapsed)||double.IsInfinity(elapsed)||double.IsNaN(duration)||double.IsInfinity(duration)||duration<0)throw new ArgumentException("天气过渡时长和经过时间必须有限，时长非负。 ");
   if(duration==0)return 1d;return Math.Max(0d,Math.Min(1d,elapsed/duration));
  }
 }
}
