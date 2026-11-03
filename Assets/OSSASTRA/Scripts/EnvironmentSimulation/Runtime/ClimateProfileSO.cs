/* 功能：保存气候视觉基线及自动天气权重/时长；不植入生存、作物或真实气象业务。 */
using UnityEngine;
namespace Ossastra.EnvironmentSimulation
{
 [CreateAssetMenu(menuName="Ossastra/Environment/Climate")]
 public sealed class ClimateProfileSO:ScriptableObject
 {
  public string displayName="温和晴朗";
  public WeatherProfileSO[] weatherProfiles=System.Array.Empty<WeatherProfileSO>();
  public float[] weatherWeights=System.Array.Empty<float>();
  [Tooltip("自动天气每段至少1秒；避免过密切换占满主线程。")]
  public Vector2 weatherDurationSeconds=new Vector2(40,120);
  public int seed=12345;
  public float temperatureOffset,humidityOffset;
  [Min(0)] public float windMultiplier=1;
  public Color environmentTint=Color.white;
  public void ValidateWeatherSchedule()
  {
   if(weatherProfiles==null||weatherWeights==null||weatherProfiles.Length==0||weatherProfiles.Length!=weatherWeights.Length)throw new System.ArgumentException("气候天气和权重必须一一对应且非空。");
   float sum=0;for(int i=0;i<weatherWeights.Length;i++){if(weatherProfiles[i]==null)throw new System.ArgumentException("气候含空天气档。");float weight=EnvironmentNumbers.Finite(weatherWeights[i]);if(weight<0)throw new System.ArgumentException("气候天气权重不能为负数。");sum+=weight;}
   if(sum<=0)throw new System.ArgumentException("气候天气权重不能全为零。");
   if(EnvironmentNumbers.Finite(weatherDurationSeconds.x)<1||EnvironmentNumbers.Finite(weatherDurationSeconds.y)<weatherDurationSeconds.x)throw new System.ArgumentException("气候持续时间必须满足1秒<=最短<=最长。");
  }
 }
}
