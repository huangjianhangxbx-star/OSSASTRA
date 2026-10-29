/*
 * 脚本：GroundResearchSurface
 * 功能：声明研究地表的逻辑 XY 编辑范围、受控图层和真实世界 XZ 投影面积。
 * 边界：不写入格子、不创建材质、不更新角色或相机；范围来自 Grid 四角，不能硬编码样地面积。
 * 协作：Editor 作者工具通过 Owns/ContainsCell 限制写入，只读面积及包围盒供检查与展示。
 * 生命周期：无 Update；选中 Gizmo 仅画边界，缺配置或无有效地表时拒绝编辑。
 */
using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Ossastra.GroundResearch
{
    [DisallowMultipleComponent]
    public sealed class GroundResearchSurface : MonoBehaviour
    {
        [Header("地表坐标；逻辑 XY 通过 Grid swizzle 映射到世界地面")]
        public Grid grid;

        [Header("作者数据；同一份图层用于刷地、擦除与来源检查")]
        public GroundLayer[] layers = Array.Empty<GroundLayer>();

        [Tooltip("XY 为逻辑格子范围，z 起点必须为 0、厚度必须为 1；最大边界不包含在编辑范围内。")]
        public BoundsInt editableCells = new BoundsInt(0, 0, 0, 0, 0, 1);

        public BoundsInt EditableCells => editableCells;

        /// <summary>由 Grid 世界四角推导 XZ 占地面积，包含真实变换及格子尺度；无效配置返回零。</summary>
        public float WorldArea
        {
            get
            {
                if (!TryGetWorldCorners(out Vector3 minimum, out Vector3 alongX, out Vector3 opposite, out Vector3 alongY))
                    return 0f;
                return Mathf.Abs(Vector3.Cross(alongX - minimum, alongY - minimum).y);
            }
        }

        /// <summary>四个作者范围角点的世界包围盒；不会根据已刷内容偷偷扩大范围。</summary>
        public Bounds WorldBounds
        {
            get
            {
                if (!TryGetWorldCorners(out Vector3 minimum, out Vector3 alongX, out Vector3 opposite, out Vector3 alongY))
                    return new Bounds(transform.position, Vector3.zero);
                var bounds = new Bounds(minimum, Vector3.zero);
                bounds.Encapsulate(alongX);
                bounds.Encapsulate(opposite);
                bounds.Encapsulate(alongY);
                return bounds;
            }
        }

        /// <summary>只有已登记、位于本地表后代且使用同一 Grid 的 Tilemap 才可交给作者工具写入。</summary>
        public bool Owns(Tilemap target)
        {
            if (!IsLocalTilemap(target) || !TryGetWorldCorners(out _, out _, out _, out _)) return false;
            foreach (GroundLayer layer in layers)
            {
                if (layer != null && layer.tilemap == target) return true;
            }
            return false;
        }

        /// <summary>使用半开 XY 边界；非法配置及非零逻辑 z 都不能落笔。</summary>
        public bool ContainsCell(Vector3Int cell)
        {
            return cell.z == 0 && TryGetWorldCorners(out _, out _, out _, out _) && editableCells.Contains(cell);
        }

        private bool IsLocalTilemap(Tilemap target)
        {
            // 层级与 Grid 两道检查分别防止误改外部图层，以及错误使用嵌套 Grid 的另一套格坐标。
            return grid != null && target != null && target.transform.IsChildOf(transform) && target.layoutGrid == grid;
        }

        private bool HasConfiguration()
        {
            if (grid == null || layers == null || layers.Length == 0 ||
                editableCells.size.x <= 0 || editableCells.size.y <= 0 ||
                editableCells.zMin != 0 || editableCells.size.z != 1) return false;

            foreach (GroundLayer layer in layers)
            {
                if (layer != null && IsLocalTilemap(layer.tilemap)) return true;
            }
            return false;
        }

        private bool TryGetWorldCorners(out Vector3 minimum, out Vector3 alongX, out Vector3 opposite, out Vector3 alongY)
        {
            minimum = alongX = opposite = alongY = Vector3.zero;
            if (!HasConfiguration()) return false;

            // 直接转换逻辑格角点，保留 Grid swizzle、cellSize/gap 及完整父层级 TRS。
            minimum = grid.CellToWorld(new Vector3Int(editableCells.xMin, editableCells.yMin, 0));
            alongX = grid.CellToWorld(new Vector3Int(editableCells.xMax, editableCells.yMin, 0));
            opposite = grid.CellToWorld(new Vector3Int(editableCells.xMax, editableCells.yMax, 0));
            alongY = grid.CellToWorld(new Vector3Int(editableCells.xMin, editableCells.yMax, 0));
            if (!IsFinite(minimum) || !IsFinite(alongX) || !IsFinite(opposite) || !IsFinite(alongY)) return false;

            // 本研究核对地图 XZ 占地，而非倾斜后的三维表面积；退化地表不能成为可编辑范围。
            float area = Mathf.Abs(Vector3.Cross(alongX - minimum, alongY - minimum).y);
            return area > 0f && !float.IsInfinity(area) && !float.IsNaN(area);
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private void OnDrawGizmosSelected()
        {
            if (!TryGetWorldCorners(out Vector3 minimum, out Vector3 alongX, out Vector3 opposite, out Vector3 alongY)) return;
            Color previousColor = Gizmos.color;
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.color = new Color(0.35f, 0.95f, 0.65f, 0.9f);
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.DrawLine(minimum, alongX);
            Gizmos.DrawLine(alongX, opposite);
            Gizmos.DrawLine(opposite, alongY);
            Gizmos.DrawLine(alongY, minimum);
            Gizmos.color = previousColor;
            Gizmos.matrix = previousMatrix;
        }
    }
}
