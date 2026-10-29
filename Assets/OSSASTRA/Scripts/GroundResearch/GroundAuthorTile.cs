/* 脚本：GroundAuthorTile
 * 功能：满覆盖Tile在原生刷新/Undo后保留已初始化格的flags和已解锁矩阵。
 * 边界：所有作者值仍在原生Tilemap；定义自身不存坐标、格表或运行时作者状态。
 * 协作：与GroundAuthorRuleTile遵循相同锁语义，供工坊克隆基层定义。
 */
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Ossastra.GroundResearch
{
    public sealed class GroundAuthorTile : Tile
    {
        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            bool initialized = tilemap.GetSprite(position) != null;
            TileFlags flags = tilemap.GetTileFlags(position);
            bool manualTransform = initialized && (flags & TileFlags.LockTransform) == 0;
            Matrix4x4 authored = manualTransform ? tilemap.GetTransformMatrix(position) : Matrix4x4.identity;
            base.GetTileData(position, tilemap, ref tileData);
            if (initialized) tileData.flags = flags;
            if (manualTransform) tileData.transform = authored;
        }
    }
}
