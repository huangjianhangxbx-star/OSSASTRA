using UnityEngine;
namespace XingHai.T7C2 {
 public enum PostContribution { Full,FocusOnly,BloomOnly }
 [CreateAssetMenu(menuName="XingHai/HD2D Camera Post Profile")]
 public sealed class HD2DPostProfile:ScriptableObject {
  public string label;
  public bool presentationEnabled=true;
  public float sharpBand=8,transition=16,blurRadius=1.2f;
  public bool perspectiveBokeh;
  public float focalLength=50,aperture=5.6f;
  public float bloomIntensity=.15f,bloomThreshold=1.15f,bloomScatter=.55f;
  public float vignette=.08f,grain;
 }
}
