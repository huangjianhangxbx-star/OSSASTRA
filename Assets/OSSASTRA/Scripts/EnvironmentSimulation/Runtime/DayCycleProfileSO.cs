/* 功能：保存可编辑昼夜光色/强度和太阳路径，运行根只读采样；不是游戏日历或全局驱动。 */
using UnityEngine;
namespace Ossastra.EnvironmentSimulation
{
 [CreateAssetMenu(menuName="Ossastra/Environment/Day Cycle")]
 public sealed class DayCycleProfileSO:ScriptableObject
 {
  [Range(1,89)] public float maximumSunElevation=60;
  public float sunAzimuthOffset=-45;
  [Min(0)] public float daySunIntensity=1.35f,nightSunIntensity=.12f,dayFillIntensity=.10f,nightFillIntensity=.008f;
  public Color daySunColor=new Color(1,.94f,.82f),nightSunColor=new Color(.42f,.55f,.82f),sunsetSunColor=new Color(1,.52f,.25f);
  [Header("环境底光；屋内区域响应由材质接收方负责，不冒充实时GI")]
  public Color dayAmbientSky=new Color(.38f,.51f,.66f),nightAmbientSky=new Color(.15f,.20f,.30f);
  public Color dayAmbientEquator=new Color(.34f,.39f,.39f),nightAmbientEquator=new Color(.10f,.135f,.20f);
  public Color dayAmbientGround=new Color(.17f,.20f,.14f),nightAmbientGround=new Color(.05f,.07f,.10f);
  [Min(0)] public float dayAmbientIntensity=1,nightAmbientIntensity=1;
  [Header("天空与反射输入；本档不直接写RenderSettings")]
  public Color daySkyZenith=new Color(.13f,.36f,.65f),nightSkyZenith=new Color(.008f,.014f,.04f);
  public Color daySkyHorizon=new Color(.67f,.78f,.82f),nightSkyHorizon=new Color(.035f,.055f,.09f);
  [Min(0)] public float daySkyIntensity=1,nightSkyIntensity=.6f;
  public AnimationCurve sunlightResponse=AnimationCurve.EaseInOut(0,0,1,1);
  public AnimationCurve ambientResponse=AnimationCurve.EaseInOut(0,0,1,1);
 }
}
