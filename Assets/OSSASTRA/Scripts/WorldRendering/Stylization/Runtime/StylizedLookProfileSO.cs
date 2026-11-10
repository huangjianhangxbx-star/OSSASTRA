// Author values for the optional WorldLook branch. All colors are stored LINEAR;
// preset import converts sRGB hex once, never in both C# and HLSL.
using UnityEngine;
namespace Ossastra.WorldRendering.Stylization
{
 [CreateAssetMenu(menuName="OSSASTRA/场景/实机风格候选",fileName="StylizedLookProfile")]
 public sealed class StylizedLookProfileSO : ScriptableObject
 {
  public bool enabled=true,themeEnabled=true,airEnabled=true,bloomEnabled=true,bandsEnabled;
  [Range(1,4)] public int pixelSize=1;
  [Range(0,5)] public int debugView;
  public bool toneMapping=true;
  [Header("线性主题色；导入时sRGB只转换一次")]
  public Color shadowColor=new Color(.002428216f,.004024717f,.026241222f,1);
  public Color midColor=new Color(.010329823f,.049706566f,.456411023f,1);
  public Color lightColor=new Color(.088655586f,.514917665f,.799102738f,1);
  [Range(0,1)] public float environmentStrength=.88f,actorStrength=.62f;
  [Min(.0001f)] public float pivot=.18f;
  [Range(.1f,3)] public float contrast=1.08f;
  [Range(0,1)] public float warmGuardStrength=.85f;
  public float warmGuardLow=1.5f,warmGuardHigh=3;
  [Header("解析空气；世界米，不修改天气或光源")]
  public Color airNear=new Color(.005605392f,.049706566f,.304987314f,1);
  public Color airMid=new Color(.015208514f,.109461711f,.462077f,1);
  public Color airFar=new Color(.035601315f,.191201683f,.584078418f,1);
  [Min(0)] public float distanceDensity=.035f,heightDensity=.065f,heightFalloff=.5f;
  public float baseHeight=.4f,startOffset=-4,farOffset=16;
  [Range(0,1)] public float maxOpacity=.72f;
  [Header("宽Bloom；阈值为线性HDR")]
  public float bloomThreshold=1.1f,bloomKnee=.5f,bloomIntensity=.75f,bloomScatter=.65f,bloomMaxPeak=8,actorBloomWeight=.2f;
  [Header("仅雾前主体亮度分档")]
  [Range(2,16)] public int bandLevels=6;
  [Range(0,1)] public float environmentBandStrength=.6f,actorBandStrength=.3f;
  [Min(.0001f)] public float referenceWhite=1;
 }
}
