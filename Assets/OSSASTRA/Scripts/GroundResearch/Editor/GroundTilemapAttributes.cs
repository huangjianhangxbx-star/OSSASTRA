/* 脚本：GroundTilemapAttributes
 * 功能：在一批Native RuleTile刷新前暂存颜色/flags/有效手动矩阵，批末还原仍对应的作者格。
 * 边界：仅事务栈上的瞬态快照，不序列化、不另存格子表；Sprite与锁定的规则显示矩阵仍由新规则计算。
 * 原因：逐格SetTile会刷新相邻格，若每格立即恢复，后一个邻格仍会把前一个手工矩阵重置。
 */
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
namespace Ossastra.GroundResearch.Editor
{
    internal sealed class GroundTilemapAttributes
    {
        private readonly Tilemap source;
        private readonly List<Row> rows=new List<Row>();
        private struct Row {public Vector3Int cell;public TileBase tile;public Color color;public TileFlags flags;public Matrix4x4 matrix;}
        private GroundTilemapAttributes(Tilemap source,BoundsInt bounds)
        {
            this.source=source;
            foreach(var cell in bounds.allPositionsWithin)
            {
                var tile=source.GetTile(cell);if(tile==null)continue;
                rows.Add(new Row{cell=cell,tile=tile,color=source.GetColor(cell),flags=source.GetTileFlags(cell),matrix=source.GetTransformMatrix(cell)});
            }
        }
        public static GroundTilemapAttributes Capture(Tilemap map,BoundsInt bounds)=>new GroundTilemapAttributes(map,bounds);
        public void Restore(TileBase replaced=null,TileBase replacement=null)
        {
            foreach(var row in rows)
            {
                var current=source.GetTile(row.cell);
                if(current!=null&&(current==row.tile||(replaced!=null&&row.tile==replaced&&current==replacement)))Apply(source,row);
            }
        }
        public void CopyTo(Tilemap target,TileBase original,TileBase replacement)
        {foreach(var row in rows)if(row.tile==original&&target.GetTile(row.cell)==replacement)Apply(target,row);}
        private static void Apply(Tilemap map,Row row)
        {
            map.SetTileFlags(row.cell,TileFlags.None);map.SetColor(row.cell,row.color);
            if((row.flags&TileFlags.LockTransform)==0)map.SetTransformMatrix(row.cell,row.matrix);
            map.SetTileFlags(row.cell,row.flags);
        }
    }
}
#endif
