/* 功能：按美术可调日内路径计算太阳；06时东升、12时南向最高、18时西落，不宣称天文精度。 */
using System;
namespace Ossastra.EnvironmentSimulation
{
 public static class SunPath
 {
  public static SunPose Sample(double hours,float maximumElevation,float azimuthOffset)
  {
   if(float.IsNaN(maximumElevation)||float.IsInfinity(maximumElevation)||maximumElevation<=0||maximumElevation>89.5f)throw new ArgumentException("太阳最大高度须在0至89.5度之间。",nameof(maximumElevation));
   double time=EnvironmentClock.WrapHours(hours);
   return new SunPose((float)(time*15d)+azimuthOffset,(float)(-Math.Cos(time/24d*Math.PI*2d)*maximumElevation));
  }
 }
}
