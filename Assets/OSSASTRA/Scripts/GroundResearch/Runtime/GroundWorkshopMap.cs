/* 脚本：GroundWorkshopMap
 * 功能：把已注册的地图身份、原生Surface、主题实例与资源绑定组织为可保存作者根。
 * 边界：layerBindings只记录资源所有权，不复制Tilemap格子；不接管角色/镜头/碰撞结算。
 * 生命周期：无自动生成或重置，重开与Play保留用户手绘和装饰实例。
 */
using System;
using UnityEngine;
using UnityEngine.Tilemaps;
namespace Ossastra.GroundResearch
{
    [Serializable]
    public sealed class GroundMapLayerBinding
    {
        public string slotId;
        public GroundRecipe recipe;
        public TileBase ownedTile;
        public Material ownedMaterial;
    }
    [DisallowMultipleComponent]
    public sealed class GroundWorkshopMap : MonoBehaviour
    {
        public int schemaVersion = 1;
        public GroundResearchSurface surface;
        public GroundMapRegistration registration;
        public GroundTheme theme;
        public GroundMapLayerBinding[] layerBindings = Array.Empty<GroundMapLayerBinding>();
        public Transform decorationRoot;
    }
}
