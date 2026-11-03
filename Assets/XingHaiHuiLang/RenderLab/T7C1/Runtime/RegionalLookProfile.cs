using UnityEngine;
namespace XingHai.T7C1 {
 public enum RegionalMaterialRole { None,Ground,Stone,PaleStone,Wood,Vegetation,Water,Accent,DampStone }
 public enum RegionalContribution { Full,BaseOnly,EnvironmentOnly,GradeOnly,GradeOff }
 [CreateAssetMenu(menuName="XingHai/Regional Look Profile")]
 public sealed class RegionalLookProfile:ScriptableObject {
  public string label,description;
  public Color ground,stone,paleStone,wood,vegetation,accent,waterShallow,waterDeep;
  public Color shadow,highlight,sun,skyAmbient,horizonAmbient,groundAmbient,zenith,horizon,sunDisk,airNear,airMid,airFar;
  public float exposureOffset,contrastOffset,saturationOffset,temperatureOffset;
  public Color gradeFilter=Color.white;
 }
}
