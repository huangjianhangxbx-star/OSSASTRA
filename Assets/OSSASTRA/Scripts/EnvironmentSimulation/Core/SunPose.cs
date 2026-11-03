/* 功能：保存归一化太阳方向及其高度/方位的不可变值；方向指向太阳，不是灯的照射方向。 */
using System;
using UnityEngine;
namespace Ossastra.EnvironmentSimulation
{
 public readonly struct SunPose
 {
  public float ElevationDegrees{get;}
  public float AzimuthDegrees{get;}
  public Vector3 DirectionToSun{get;}
  public SunPose(float azimuthDegrees,float elevationDegrees)
  {
   if(float.IsNaN(azimuthDegrees)||float.IsInfinity(azimuthDegrees)||float.IsNaN(elevationDegrees)||float.IsInfinity(elevationDegrees)||elevationDegrees< -89.5f||elevationDegrees>89.5f)throw new ArgumentException("太阳角度必须有限，高度位于-89.5至89.5度。 ");
   AzimuthDegrees=(azimuthDegrees%360f+360f)%360f;ElevationDegrees=elevationDegrees;
   double elevation=elevationDegrees*Math.PI/180d,azimuth=AzimuthDegrees*Math.PI/180d;
   DirectionToSun=new Vector3((float)(Math.Sin(azimuth)*Math.Cos(elevation)),(float)Math.Sin(elevation),(float)(Math.Cos(azimuth)*Math.Cos(elevation)));
  }
 }
}
