// Independent immutable values for one published camera frame. No author asset or
// GPU resource ownership. null Capture deliberately retains the old WorldLook path.
using UnityEngine;
namespace Ossastra.WorldRendering.Stylization
{
 public sealed class StylizedLookSettings
 {
  public bool Enabled{get;} public bool ThemeEnabled{get;} public bool AirEnabled{get;} public bool BloomEnabled{get;} public bool BandsEnabled{get;} public bool ToneMapping{get;}
  public int PixelSize{get;} public int DebugView{get;} public int BandLevels{get;}
  public Color ShadowColor{get;} public Color MidColor{get;} public Color LightColor{get;} public Color AirNear{get;} public Color AirMid{get;} public Color AirFar{get;}
  public float EnvironmentStrength{get;} public float ActorStrength{get;} public float Pivot{get;} public float Contrast{get;} public float WarmGuardStrength{get;} public float WarmGuardLow{get;} public float WarmGuardHigh{get;}
  public float DistanceDensity{get;} public float HeightDensity{get;} public float HeightFalloff{get;} public float BaseHeight{get;} public float StartOffset{get;} public float FarOffset{get;} public float MaxOpacity{get;}
  public float BloomThreshold{get;} public float BloomKnee{get;} public float BloomIntensity{get;} public float BloomScatter{get;} public float BloomMaxPeak{get;} public float ActorBloomWeight{get;}
  public float EnvironmentBandStrength{get;} public float ActorBandStrength{get;} public float ReferenceWhite{get;}
  StylizedLookSettings(StylizedLookProfileSO p)
  {
   ThemeEnabled=p.enabled&&p.themeEnabled;AirEnabled=p.enabled&&p.airEnabled;BloomEnabled=p.enabled&&p.bloomEnabled;BandsEnabled=p.enabled&&p.bandsEnabled;ToneMapping=p.toneMapping;
   PixelSize=p.pixelSize>=4?4:p.pixelSize>=2?2:1;DebugView=Mathf.Clamp(p.debugView,0,5);BandLevels=Mathf.Clamp(p.bandLevels,2,16);
   Enabled=p.enabled&&(ThemeEnabled||AirEnabled||BloomEnabled||BandsEnabled||PixelSize>1||DebugView>0);
   ShadowColor=C(p.shadowColor,Color.black);MidColor=C(p.midColor,Color.blue);LightColor=C(p.lightColor,Color.cyan);AirNear=C(p.airNear,Color.blue);AirMid=C(p.airMid,Color.blue);AirFar=C(p.airFar,Color.blue);
   EnvironmentStrength=F(p.environmentStrength,.88f,0,1);ActorStrength=F(p.actorStrength,.62f,0,1);Pivot=F(p.pivot,.18f,.0001f,8);Contrast=F(p.contrast,1.08f,.1f,3);WarmGuardStrength=F(p.warmGuardStrength,.85f,0,1);WarmGuardLow=F(p.warmGuardLow,1.5f,0,64);WarmGuardHigh=Mathf.Max(WarmGuardLow+.0001f,F(p.warmGuardHigh,3,0,128));
   DistanceDensity=F(p.distanceDensity,.035f,0,2);HeightDensity=F(p.heightDensity,.065f,0,2);HeightFalloff=F(p.heightFalloff,.5f,0,10);BaseHeight=F(p.baseHeight,.4f,-10000,10000);StartOffset=F(p.startOffset,-4,-10000,10000);FarOffset=Mathf.Max(StartOffset+.01f,F(p.farOffset,16,-9999,20000));MaxOpacity=F(p.maxOpacity,.72f,0,1);
   BloomThreshold=F(p.bloomThreshold,1.1f,0,64);BloomKnee=F(p.bloomKnee,.5f,0,1);BloomIntensity=F(p.bloomIntensity,.75f,0,8);BloomScatter=F(p.bloomScatter,.65f,0,1);BloomMaxPeak=F(p.bloomMaxPeak,8,.0001f,128);ActorBloomWeight=F(p.actorBloomWeight,.2f,0,1);
   EnvironmentBandStrength=F(p.environmentBandStrength,.6f,0,1);ActorBandStrength=F(p.actorBandStrength,.3f,0,1);ReferenceWhite=F(p.referenceWhite,1,.0001f,16);
  }
  public static StylizedLookSettings Capture(StylizedLookProfileSO profile)=>profile==null?null:new StylizedLookSettings(profile);
  static float F(float x,float fallback,float lo,float hi)=>float.IsNaN(x)||float.IsInfinity(x)?fallback:Mathf.Clamp(x,lo,hi);
  static Color C(Color x,Color fallback)=>new Color(F(x.r,fallback.r,0,64),F(x.g,fallback.g,0,64),F(x.b,fallback.b,0,64),1);
 }
}
