/* 功能：在环境快照边界拒绝非有限数值并约束显示参数，不将NaN/Infinity发送到灯或Shader。 */
using System;
using UnityEngine;
namespace Ossastra.EnvironmentSimulation
{
 internal static class EnvironmentNumbers
 {
  internal static float Finite(float value){if(float.IsNaN(value)||float.IsInfinity(value))throw new ArgumentException("环境参数必须为有限值。");return value;}
  internal static float Range(float value,float min,float max)=>Mathf.Clamp(Finite(value),min,max);
  internal static float Unit(float value)=>Range(value,0,1);
  internal static float Nonnegative(float value)=>Range(value,0,10000);
  internal static Color Color(Color c)=>new Color(Range(c.r,0,16),Range(c.g,0,16),Range(c.b,0,16),Unit(c.a));
  internal static Color Temperature(float kelvin)
  {
   double t=Range(kelvin,1000,40000)/100d;
   double red=t<=66?255:329.698727446*Math.Pow(t-60,-.1332047592);
   double green=t<=66?99.4708025861*Math.Log(t)-161.1195681661:288.1221695283*Math.Pow(t-60,-.0755148492);
   double blue=t>=66?255:t<=19?0:138.5177312231*Math.Log(t-10)-305.0447927307;
   return new Color(Mathf.Clamp01((float)(red/255)),Mathf.Clamp01((float)(green/255)),Mathf.Clamp01((float)(blue/255)),1);
  }
 }
}
