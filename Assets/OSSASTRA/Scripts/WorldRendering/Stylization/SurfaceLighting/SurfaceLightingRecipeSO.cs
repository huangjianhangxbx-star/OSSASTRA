// Candidate-only complete appearance recipes. Authored profiles are never mutated at runtime.
using System;
using UnityEngine;
using Ossastra.EnvironmentSimulation;
namespace Ossastra.WorldRendering.Stylization
{
 public enum SurfaceLightingState { Previous, MaterialLighting, BlueComplete }
 [Serializable] public sealed class SurfaceLightingEnvironmentSettings
 {
  public float azimuth=135,elevation=35,sunMultiplier=1,ambientMultiplier=.8f,skyMultiplier=1,shadowStrength=.85f;
  public bool Valid=>Finite(azimuth)&&Finite(elevation)&&elevation>=-89&&elevation<=89&&Finite(sunMultiplier)&&sunMultiplier>=0&&Finite(ambientMultiplier)&&ambientMultiplier>=0&&Finite(skyMultiplier)&&skyMultiplier>=0&&Finite(shadowStrength)&&shadowStrength>=0&&shadowStrength<=1;
  static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
 }
 [CreateAssetMenu(menuName="OSSASTRA/场景/卡通材质与光影配方",fileName="SurfaceLightingRecipe")]
 public sealed class SurfaceLightingRecipeSO:ScriptableObject
 {
  public StylizedLookProfileSO previous,neutral,blue;
  public DayCycleProfileSO neutralDay,blueDay;
  public SceneEnvironmentProfileSO blueSceneEnvironment; // C-only Ground/Grass base opt-in; null preserves old recipes
  [Tooltip("Explicit local wrapper variant dependencies; no runtime processing.")] public Material[] additionalRetentionMaterials=Array.Empty<Material>();
  public SurfaceLightingEnvironmentSettings neutralLighting=new SurfaceLightingEnvironmentSettings(),blueLighting=new SurfaceLightingEnvironmentSettings();
  public LookDevToonVariant materialBackend=LookDevToonVariant.RealToon;
  public bool outline;
  [Range(.5f,1.5f)] public float outlinePixels=1;
  public Color outlineDarkSrgb=new Color(.025f,.04f,.10f,1);
 }
}
