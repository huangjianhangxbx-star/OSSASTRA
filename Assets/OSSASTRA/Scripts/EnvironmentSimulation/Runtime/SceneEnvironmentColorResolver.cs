/* 功能：从同一环境帧和有效天空覆盖解析场景色度，输出归一化线性RGB；不生成光能。
 * 边界：自然夕照使用昼夜档固定sunset色，在0度连续，不误用已切换月光的Frame.SunColor。
 * 无状态；输入颜色沿用作者gamma约定，在入口仅解码一次；无效目标w=0。 */
using System;
using UnityEngine;
namespace Ossastra.EnvironmentSimulation
{
 public static class SceneEnvironmentColorResolver
 {
  public static Vector4 ResolveTint(Color ambientSky,Color ambientEquator,Color sunsetColor,float elevation,
   bool overrideSkyColors,Color zenith,Color horizon,SceneEnvironmentSettings settings)
  {
   if(!Finite(elevation))return Neutral();
   var values=(settings??new SceneEnvironmentSettings()).Sanitize();
   Color top=overrideSkyColors?zenith:ambientSky,edge=overrideSkyColors?horizon:ambientEquator;
   if(!Valid(top)||!Valid(edge)||(!overrideSkyColors&&!Valid(sunsetColor)))return Neutral();
   float red=Lerp(Linear(top.r),Linear(edge.r),.65f);
   float green=Lerp(Linear(top.g),Linear(edge.g),.65f);
   float blue=Lerp(Linear(top.b),Linear(edge.b),.65f);
   if(!overrideSkyColors)
   {
    float warm=Smooth(values.twilightStartElevation,values.twilightFullElevation,elevation)
     *(1-Smooth(values.sunsetFullElevation,values.sunsetEndElevation,elevation));
    red=Lerp(red,Linear(sunsetColor.r),warm);
    green=Lerp(green,Linear(sunsetColor.g),warm);
    blue=Lerp(blue,Linear(sunsetColor.b),warm);
   }
   float peak=Math.Max(red,Math.Max(green,blue));
   return peak>1e-6f&&Finite(peak)?new Vector4(red/peak,green/peak,blue/peak,1):Neutral();
  }
  static Vector4 Neutral()=>new Vector4(1,1,1,0);
  static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
  static bool Valid(Color value)=>Finite(value.r)&&Finite(value.g)&&Finite(value.b)&&value.r>=0&&value.g>=0&&value.b>=0;
  static float Linear(float value)=>value<=.04045f?value/12.92f:(float)Math.Pow((value+.055f)/1.055f,2.4);
  static float Lerp(float from,float to,float weight)=>from+(to-from)*weight;
  static float Smooth(float from,float to,float value){float t=Math.Min(1,Math.Max(0,(value-from)/(to-from)));return t*t*(3-2*t);}
 }
}
