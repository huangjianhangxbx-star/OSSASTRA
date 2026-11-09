/* 功能：二维草图、承载规模与有界风响应的独立作者配置；不拥有天气或地形。 */
using UnityEngine;
namespace Ossastra.WorldRendering.Vegetation
{
 public enum GrassAppearance { Legacy, Tutorial, Environment }
 public enum GrassMotion { Legacy, Fine, Coarse, Combined, CombinedColor }
 [CreateAssetMenu(menuName="OSSASTRA/场景/二维草层配置",fileName="GrassLayerProfile")]
 public sealed class GrassLayerProfileSO:ScriptableObject
 {
  public Texture2D image;
  public Rect uvRect=new Rect(.15625f,.0546875f,.7109375f,.90625f);
  public Vector2 footpoint=new Vector2(.48351648f,.00862069f);
  public Material material;
  [Header("每平方米草簇数；maxDensity固定则各档为嵌套子集")]
  public int seed=18273;
  [Min(.1f)] public float density=24,maxDensity=48;
  public Vector2 heightRange=new Vector2(.24f,.42f),widthRange=new Vector2(.20f,.34f);
  [Range(0,75)] public float maximumSlope=48;
  [Min(.01f)] public float edgeInset=.12f,chunkSize=3;
  [Range(1,6)] public int verticalSegments=3;
  [Min(1)] public int maximumPlacements=30000;
  [Header("弯曲上限为世界单位；颜色外观与运动独立")]
  [Range(0,.5f)] public float maximumBend=.14f;
  [Range(.05f,2)] public float windResponse=1;
  [Range(0,1)] public float cutoff=.35f;
  [Header("远处逐簇稀疏，完全稀疏后才换半量网格") ] public float farStart=18,farEnd=26;
  public Color tint=Color.white;
  public Color rootTint=new Color(.78f,.83f,.70f),tipTint=new Color(1.03f,1.05f,.91f);
  [Header("视频颜色：Alpha轮廓 / Depth近远色 / 逐簇HeightBlend")]
  public GrassAppearance appearance;
  public GrassMotion motion;
  [Range(0,1)] public float rgbDetail;
  public bool useTerrainColor=true;
  public Color nearColor=new Color(.65f,.82f,.31f),farColor=new Color(.38f,.64f,.40f);
  public Color bottomColor=new Color(.26f,.46f,.13f),shadowColor=new Color(.30f,.47f,.50f);
  public Vector2 nearFarRange=new Vector2(5,25);
  [Range(.01f,1)] public float heightBlend=.6f;
  [Range(0,1)] public float depthColorStrength=.25f;
  [Header("视频风：细摆 + 世界纹理粗风 + 配色；环境仍是唯一时钟")]
  public Texture2D windTexture;
  public Vector2 windNoiseScale=new Vector2(6,9),windNoiseContrast=new Vector2(.2f,.8f);
  [Range(0,4)] public float windNoiseSpeed=1;
  [Range(0,1)] public float fineWindStrength=.35f,coarseWindStrength=.8f,windColorStrength=.25f;
  [Range(0,6)] public float windTextureLod;
  [Range(0,5)] public int artDebug;
 }
}
