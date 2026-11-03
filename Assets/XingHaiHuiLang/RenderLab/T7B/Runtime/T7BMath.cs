using System;
using UnityEngine;
namespace XingHai.T7B {
 public static class T7BMath {
  public static Vector3 DistanceWeights(float distance,float near,float mid,float far){
   if(near<0||mid<=near||far<=mid)throw new ArgumentException("Outdoor distances must satisfy 0 <= near < mid < far.");
   float m=Mathf.SmoothStep(0,1,Mathf.InverseLerp(near,mid,distance)),f=Mathf.SmoothStep(0,1,Mathf.InverseLerp(mid,far,distance));
   return new Vector3(1-m,m*(1-f),m*f);
  }
  public static float Transmission(float distance,float near,float extinction)=>Mathf.Exp(-Mathf.Max(0,distance-near)*Mathf.Max(0,extinction));
  public static Vector3 SunDirection(float elevation,float azimuth){float e=elevation*Mathf.Deg2Rad,a=azimuth*Mathf.Deg2Rad;return new Vector3(Mathf.Sin(a)*Mathf.Cos(e),Mathf.Sin(e),Mathf.Cos(a)*Mathf.Cos(e));}
  public static float ShoreWeight(float gap,float depth){if(depth<=0)throw new ArgumentException("Water depth scale must be positive.");return Mathf.Clamp01(gap/depth);}
 }
}
