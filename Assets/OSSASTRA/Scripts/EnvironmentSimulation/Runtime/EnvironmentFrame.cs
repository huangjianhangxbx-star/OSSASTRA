/* 功能：发布一份不可变的实际环境显示快照，供天空、光照、材质、空气与开发面板共同读取。
 * 边界：不携带可修改Profile、角色、许可或业务状态，不拥有任何Unity/GPU对象。
 */
using UnityEngine;
namespace Ossastra.EnvironmentSimulation
{
 public sealed class EnvironmentFrame
 {
  public bool Enabled{get;} public EnvironmentContribution Contribution{get;}
  public long Revision{get;} public double Hours{get;} public double Seconds{get;}
  public SunPose Sun{get;} public Vector3 SunDirection=>Sun.DirectionToSun;
  public Vector3 KeyLightDirection{get;} public Vector3 MoonDirection{get;} public float MoonIntensity{get;}
  public Color SunColor{get;} public float SunIntensity{get;} public Color FillColor{get;} public float FillIntensity{get;}
  public Color AmbientSky{get;} public Color AmbientEquator{get;} public Color AmbientGround{get;} public float AmbientIntensity{get;}
  public Color SkyZenith{get;} public Color SkyHorizon{get;} public Color SkyNight{get;} public float SkyIntensity{get;} public float NightFactor{get;}
  public float CloudCoverage{get;} public float CloudDensity{get;} public float CloudSpeed{get;} public float CloudShadowStrength{get;} public float CloudWorldScale{get;}
  public Vector2 WindDirection{get;} public float WindSpeed{get;}
  public float FogDensity{get;} public Color FogColor{get;} public float Rain{get;} public float Wetness{get;}
  public float IndoorAmbientScale{get;} public float CharacterReadability{get;} public float CharacterMinimum=>CharacterReadability;
  public float GroundLightResponse{get;} public float DecorationLightResponse{get;} public float CharacterLightResponse{get;}
  public float ShadowStrength{get;} public float Temperature{get;} public float Humidity{get;}
  public string WeatherName{get;} public string ClimateName{get;}
  internal EnvironmentFrame(EnvironmentFrameData d)
  {
   Enabled=d.enabled;Contribution=d.contribution;Revision=d.revision;Hours=d.hours;Seconds=d.seconds;Sun=d.sun;KeyLightDirection=d.keyLightDirection;MoonDirection=d.moonDirection;MoonIntensity=d.moonIntensity;
   SunColor=d.sunColor;SunIntensity=d.sunIntensity;FillColor=d.fillColor;FillIntensity=d.fillIntensity;
   AmbientSky=d.ambientSky;AmbientEquator=d.ambientEquator;AmbientGround=d.ambientGround;AmbientIntensity=d.ambientIntensity;
   SkyZenith=d.skyZenith;SkyHorizon=d.skyHorizon;SkyNight=d.skyNight;SkyIntensity=d.skyIntensity;NightFactor=d.nightFactor;
   CloudCoverage=d.cloudCoverage;CloudDensity=d.cloudDensity;CloudSpeed=d.cloudSpeed;CloudShadowStrength=d.cloudShadowStrength;CloudWorldScale=d.cloudWorldScale;WindDirection=d.windDirection;WindSpeed=d.windSpeed;
   FogDensity=d.fogDensity;FogColor=d.fogColor;Rain=d.rain;Wetness=d.wetness;IndoorAmbientScale=d.indoorAmbientScale;CharacterReadability=d.characterReadability;
   GroundLightResponse=d.groundLightResponse;DecorationLightResponse=d.decorationLightResponse;CharacterLightResponse=d.characterLightResponse;ShadowStrength=d.shadowStrength;Temperature=d.temperature;Humidity=d.humidity;WeatherName=d.weatherName;ClimateName=d.climateName;
  }
 }
 internal sealed class EnvironmentFrameData
 {
  internal bool enabled;internal EnvironmentContribution contribution;internal long revision;internal double hours,seconds;internal SunPose sun;
  internal Vector3 keyLightDirection,moonDirection;internal float moonIntensity,sunIntensity,fillIntensity,ambientIntensity,skyIntensity,nightFactor;
  internal Color sunColor,fillColor,ambientSky,ambientEquator,ambientGround,skyZenith,skyHorizon,skyNight,fogColor;
  internal float cloudCoverage,cloudDensity,cloudSpeed,cloudShadowStrength,cloudWorldScale,windSpeed,fogDensity,rain,wetness,indoorAmbientScale,characterReadability,groundLightResponse,decorationLightResponse,characterLightResponse,shadowStrength,temperature,humidity;
  internal Vector2 windDirection;internal string weatherName,climateName;
 }
}
