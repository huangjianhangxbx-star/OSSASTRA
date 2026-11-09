/* 功能：一次生成的未受光地表颜色及世界XZ映射；每个显式surface单独归属，避免上下平台串色。
 * 生命周期：只保存作者数据；不拥有相机、不每帧重拍，不授予Room可视许可。
 */
using UnityEngine;
namespace Ossastra.WorldRendering.Vegetation
{
 public sealed class GrassTerrainColorSourceSO:ScriptableObject
 {
  public string surfaceId;
  public Texture2D texture;
  public Rect worldXZ;
  public int version=1;
  public string semantics="UnlitLinearBaseColor; alpha=valid; UV.v=+worldZ";
 }
}
