/* 功能：只读适配已有XingHai作者Profile，不驱动历史预览，不修改资源或接管灯光。
 * 边界：旧类型仅停留在此适配层；运行控制器消费纯值，错误类型明确拒绝，不猜档案。
 */
using UnityEngine;using System;using XingHai.T7B;using XingHai.T7C1;using XingHai.T7C2;using Ossastra.EnvironmentSimulation;
namespace Ossastra.WorldRendering.Integration {
 [DisallowMultipleComponent] public sealed class LegacyWorldLookProfileAdapter:MonoBehaviour,IWorldLookProfileAdapter {
 public WorldLookProfileValues Capture(ScriptableObject presentation,ScriptableObject atmosphere,ScriptableObject palette){
 if(presentation!=null&&!(presentation is HD2DPostProfile))throw new ArgumentException("展示Profile类型不匹配");
 if(atmosphere!=null&&!(atmosphere is OutdoorAtmosphereProfile))throw new ArgumentException("空气Profile类型不匹配");
 if(palette!=null&&!(palette is RegionalLookProfile))throw new ArgumentException("色板Profile类型不匹配");
 return new WorldLookProfileValues{Presentation=Post(presentation as HD2DPostProfile),Atmosphere=Air(atmosphere as OutdoorAtmosphereProfile),Palette=Palette(palette as RegionalLookProfile)};}
 static WorldLookPresentationValues Post(HD2DPostProfile v)=>v==null?null:new WorldLookPresentationValues{bloomIntensity=v.bloomIntensity,bloomScatter=v.bloomScatter,bloomThreshold=v.bloomThreshold,blurRadius=v.blurRadius,presentationEnabled=v.presentationEnabled,sharpBand=v.sharpBand,transition=v.transition,vignette=v.vignette};
 static WorldLookAtmosphereValues Air(OutdoorAtmosphereProfile v)=>v==null?null:new WorldLookAtmosphereValues{airEnabled=v.airEnabled,contrast=v.contrast,contrastLoss=v.contrastLoss,exposure=v.exposure,extinction=v.extinction,farColor=v.farColor,farDistance=v.farDistance,midColor=v.midColor,midDistance=v.midDistance,nearColor=v.nearColor,nearColorInfluence=v.nearColorInfluence,nearDistance=v.nearDistance,saturation=v.saturation,saturationLoss=v.saturationLoss,temperature=v.temperature};
 static WorldLookPaletteValues Palette(RegionalLookProfile v)=>v==null?null:new WorldLookPaletteValues{airFar=v.airFar,airMid=v.airMid,airNear=v.airNear,contrastOffset=v.contrastOffset,exposureOffset=v.exposureOffset,gradeFilter=v.gradeFilter,saturationOffset=v.saturationOffset,temperatureOffset=v.temperatureOffset};
 public static WorldLookSettings BuildLegacy(Camera camera,Vector3 observerPosition,HD2DPostProfile presentation,OutdoorAtmosphereProfile atmosphere,RegionalLookProfile palette,WorldLookEffectOptions options,EnvironmentFrame environment=null,bool allowWeatherFog=true)=>WorldLookSettings.Build(camera,observerPosition,Post(presentation),Air(atmosphere),Palette(palette),options,environment,allowWeatherFog);
 } }
