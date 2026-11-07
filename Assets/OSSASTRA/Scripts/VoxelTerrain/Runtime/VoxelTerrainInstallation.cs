/* 功能：保存已烘焙地表的明确来源、网格、碰撞和迁移前引用，供作者检查与回退。
 * 边界：纯场景装配数据；不推进角色、可见性、存档或逐帧重建地形。
 */
using System;
using UnityEngine;

namespace Ossastra.VoxelTerrain
{
    [DisallowMultipleComponent]
    public sealed class VoxelTerrainInstallation : MonoBehaviour
    {
        public TextAsset editableSource, frozenSurfaces;
        public string sourceSha256;
        public MeshRenderer[] chunks = Array.Empty<MeshRenderer>();
        public MeshCollider[] collision = Array.Empty<MeshCollider>();
        public Renderer water;
        public Transform timberHouse;
        public Renderer[] previousGroundRenderers = Array.Empty<Renderer>();
        public bool[] previousGroundEnabled = Array.Empty<bool>();
        public Ossastra.GroundSurfaces.GroundSurfaceBinding[] previousCacheBindings = Array.Empty<Ossastra.GroundSurfaces.GroundSurfaceBinding>();
        public bool[] previousCacheUse = Array.Empty<bool>();
        public Collider retiredLowSupport;
        public Ossastra.WorldProps.WorldPropRoot[] migratedProps = Array.Empty<Ossastra.WorldProps.WorldPropRoot>();
        public Collider[] previousPropSupports = Array.Empty<Collider>();
        public Vector3 arrival = new Vector3(4, .15f, -16);
        public Vector3 hillA = new Vector3(-24, .85f, -4);
        public Vector3 hillB = new Vector3(24, 1.35f, 12);
        public Vector3 pond = new Vector3(15, -.4f, -16);
    }
}
