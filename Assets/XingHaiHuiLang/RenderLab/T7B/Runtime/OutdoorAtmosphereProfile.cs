using UnityEngine;
namespace XingHai.T7B {
 public enum OutdoorSkyType{ClearBlue,SoftCloudy,WarmHaze}
 public enum OutdoorCloudRoute{Sky,Layers}
 [CreateAssetMenu(menuName="XingHai/Outdoor Atmosphere Profile")]
 public sealed class OutdoorAtmosphereProfile:ScriptableObject {
  public OutdoorSkyType skyType=OutdoorSkyType.SoftCloudy;public bool replaceSky=true;
  public Color zenithColor=new Color(.12f,.37f,.67f),horizonColor=new Color(.64f,.76f,.75f),sunDiskColor=new Color(1,.88f,.65f);
  public float sunDiskSize=.015f,skyIntensity=1;
  public Color sunColor=new Color(1,.92f,.78f);public float sunIntensity=1.6f,sunElevation=38,sunAzimuth=-35;public bool overrideSunDirection=true;
  public Color skyAmbient=new Color(.22f,.33f,.43f),horizonAmbient=new Color(.24f,.29f,.27f),groundAmbient=new Color(.12f,.14f,.12f);public float environmentIntensity=1;
  public bool airEnabled=true;public Color nearColor=new Color(.5f,.7f,.8f),midColor=new Color(.56f,.70f,.71f),farColor=new Color(.68f,.75f,.70f);
  public float nearDistance=25,midDistance=80,farDistance=180,extinction=.007f,saturationLoss=.4f,contrastLoss=.3f,nearColorInfluence=0;
  public bool cloudEnabled=true;public OutdoorCloudRoute cloudRoute=OutdoorCloudRoute.Sky;
  public float cloudCoverage=.45f,cloudDensity=.65f,cloudSpeed=1.1f,cloudShadowStrength=.07f,cloudShadowScale=.012f,cloudQuality=2;
  public bool waterEnabled=true,waterReflection=true;public Color shallowColor=new Color(.10f,.47f,.42f),deepColor=new Color(.03f,.16f,.23f);
  public float fresnel=4,skyReflection=.65f,sunSpecular=.6f,waveStrength=.055f,waterDepthScale=3,waterQuality=1;
  public bool windEnabled=true;public float windStrength=.16f,windSpeed=1.3f,gust=.18f,cutoutBias=.5f;
  public float wetness=.20f;public bool vegetationMedium=true;
  public float exposure=.0f,contrast=5,saturation=4,temperature=0,bloom=.07f,vignette=.06f;
  public int shadowResolution=4096,shadowCascades=2;public float shadowDistance=55;public bool ambientOcclusion=true;
  public void Validate(){T7BMath.DistanceWeights(0,nearDistance,midDistance,farDistance);T7BMath.ShoreWeight(0,waterDepthScale);if(!float.IsFinite(sunIntensity)||sunIntensity<0)throw new System.ArgumentException("Sun intensity invalid.");}
 }
}
