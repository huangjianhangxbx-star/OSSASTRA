/* 功能：保存一档天气的数值目标；运行时捕获值副本，不改源档，也不创建灯光、雨点或声音。 */
using UnityEngine;
namespace Ossastra.EnvironmentSimulation
{
 public enum WeatherKind{Clear,PartlyCloudy,Overcast,Mist,LightRain}
 [CreateAssetMenu(menuName="Ossastra/Environment/Weather")]
 public sealed class WeatherProfileSO:ScriptableObject
 {
  public string displayName="晴";
  public WeatherKind kind;
  [Min(0)] public float sunMultiplier=1,ambientMultiplier=1,skyMultiplier=1;
  [Range(0,1)] public float cloudCoverage=.18f;
  [Range(0,2)] public float cloudDensity=.6f;
  [Min(0)] public float cloudSpeed=1;
  [Range(0,1)] public float cloudShadowStrength;
  [Min(.0001f)] public float cloudWorldScale=.055f;
  [Min(0)] public float windSpeed=.35f;
  public float windYawDegrees=45;
  [Range(0,.2f)] public float fogDensity;
  public Color fogColor=new Color(.55f,.64f,.67f);
  [Range(0,1)] public float rain,wetness;
  public float temperature=18;
  [Range(0,1)] public float humidity=.4f;
  public Color sunlightTint=Color.white,ambientTint=Color.white;
  internal WeatherValues Capture()
  {
   return new WeatherValues{sun=EnvironmentNumbers.Nonnegative(sunMultiplier),ambient=EnvironmentNumbers.Nonnegative(ambientMultiplier),sky=EnvironmentNumbers.Nonnegative(skyMultiplier),
    coverage=EnvironmentNumbers.Unit(cloudCoverage),density=EnvironmentNumbers.Range(cloudDensity,0,2),speed=EnvironmentNumbers.Nonnegative(cloudSpeed),shadow=EnvironmentNumbers.Unit(cloudShadowStrength),scale=EnvironmentNumbers.Range(cloudWorldScale,.0001f,10),
    wind=EnvironmentNumbers.Nonnegative(windSpeed),yaw=EnvironmentNumbers.Finite(windYawDegrees),fog=EnvironmentNumbers.Range(fogDensity,0,.2f),fogColor=EnvironmentNumbers.Color(fogColor),rain=EnvironmentNumbers.Unit(rain),wetness=EnvironmentNumbers.Unit(wetness),
    temperature=EnvironmentNumbers.Finite(temperature),humidity=EnvironmentNumbers.Unit(humidity),sunTint=EnvironmentNumbers.Color(sunlightTint),ambientTint=EnvironmentNumbers.Color(ambientTint)};
  }
 }
 internal struct WeatherValues
 {
  internal float sun,ambient,sky,coverage,density,speed,shadow,scale,wind,yaw,fog,rain,wetness,temperature,humidity;
  internal Color fogColor,sunTint,ambientTint;
  internal static WeatherValues Lerp(WeatherValues a,WeatherValues b,float t)
  {
   return new WeatherValues{sun=Mathf.Lerp(a.sun,b.sun,t),ambient=Mathf.Lerp(a.ambient,b.ambient,t),sky=Mathf.Lerp(a.sky,b.sky,t),coverage=Mathf.Lerp(a.coverage,b.coverage,t),density=Mathf.Lerp(a.density,b.density,t),speed=Mathf.Lerp(a.speed,b.speed,t),shadow=Mathf.Lerp(a.shadow,b.shadow,t),scale=Mathf.Lerp(a.scale,b.scale,t),
    wind=Mathf.Lerp(a.wind,b.wind,t),yaw=Mathf.LerpAngle(a.yaw,b.yaw,t),fog=Mathf.Lerp(a.fog,b.fog,t),fogColor=UnityEngine.Color.Lerp(a.fogColor,b.fogColor,t),rain=Mathf.Lerp(a.rain,b.rain,t),wetness=Mathf.Lerp(a.wetness,b.wetness,t),temperature=Mathf.Lerp(a.temperature,b.temperature,t),humidity=Mathf.Lerp(a.humidity,b.humidity,t),sunTint=UnityEngine.Color.Lerp(a.sunTint,b.sunTint,t),ambientTint=UnityEngine.Color.Lerp(a.ambientTint,b.ambientTint,t)};
  }
 }
}
