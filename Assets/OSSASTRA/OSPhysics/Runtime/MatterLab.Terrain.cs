// 职责：管理旧预设碰撞、闸门和原物质工具；资产样板持有自己的几何与物质阻挡。
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ossastra.OSPhysics
{
    public sealed partial class MatterLab
    {
        private const int GateX = 102;
        private const int GateY = 6;
        private const int GateWidth = 2;
        private const int GateHeight = 30;

        private GameObject terrain;
        private List<Collider2D> baselineColliders;
        private readonly List<Vector2Int> baselineCells = new List<Vector2Int>();
        private BoxCollider2D gate;
        private PolygonCollider2D paintedCollider;
        private bool[] paintedBarriers;
        private bool barrierColliderDirty;
        private readonly List<RectInt> barrierRuns = new List<RectInt>();
        private readonly Vector2[] barrierPath = new Vector2[4];

        private void BuildTerrain()
        {
            terrain = new GameObject("Lab Collision");
            terrain.transform.SetParent(transform, false);
            baselineColliders = new List<Collider2D>
            {
                Box("基岩", 0, 0, Width, 6),
                Box("左侧界墙", 0, 6, 2, Height - 6),
                Box("右侧界墙", Width - 2, 6, 2, Height - 6),
                Box("顶部界墙", 0, Height - 2, Width, 2),
                Box("自由区矮台", 12, 22, 18, 3),
                Box("水槽左墙", 56, 6, 2, 31),
                Box("水槽右墙", 149, 6, 3, 26),
                Box("水槽前凸台", 58, 14, 10, 3),
                Box("窄缝顶板", 68, 24, 18, 3),
                Box("窄缝底板", 80, 17, 15, 3),
                Box("低平台", 112, 28, 13, 3),
                Box("高平台", 129, 35, 12, 3),
                Box("燃烧台", 158, 14, 54, 2),
                Box("防火隔断", 186, 16, 3, 11),
                Box("储油槽底", 214, 17, 16, 2),
                Box("储油槽左", 214, 19, 2, 10),
                Box("储油槽右", 228, 19, 2, 10),
                Box("观测台阶", 232, 6, 22, 7),
                Box("观测立石", 244, 13, 10, 4),
                Polygon("斜坡", new Vector2(35, 6), new Vector2(55, 6),
                    new Vector2(55, 23), new Vector2(48, 19))
            };

            gate = Box("可开关闸门", GateX, GateY, GateWidth, GateHeight);
            paintedCollider = Part("笔刷障碍").AddComponent<PolygonCollider2D>();
            paintedCollider.enabled = false;
            paintedBarriers = new bool[Width * Height];
            Physics2D.SyncTransforms();
            foreach (var collider in baselineColliders)
            {
                Bounds bounds = collider.bounds;
                WorldToCell(bounds.min, out int minX, out int minY);
                WorldToCell(bounds.max, out int maxX, out int maxY);
                for (int y = Mathf.Max(0, minY); y <= Mathf.Min(Height - 1, maxY); y++)
                for (int x = Mathf.Max(0, minX); x <= Mathf.Min(Width - 1, maxX); x++)
                    if (collider.OverlapPoint(CellToWorld(x, y)))
                    {
                        World.SetFixed(x, y, true);
                        baselineCells.Add(new Vector2Int(x, y));
                    }
            }
        }

        private void SetBaselineTerrain(bool enabled)
        {
            foreach (var collider in baselineColliders) collider.enabled = enabled;
            foreach (var cell in baselineCells) World.SetFixed(cell.x, cell.y, enabled);
            if (!enabled) gate.enabled = false;
        }

        private GameObject Part(string title)
        {
            var part = new GameObject(title) { layer = gameObject.layer };
            part.transform.SetParent(terrain.transform, false);
            return part;
        }

        private BoxCollider2D Box(string title, int x, int y, int width, int height)
        {
            var part = Part(title);
            part.transform.position = Origin + new Vector2(x + width * .5f, y + height * .5f) * CellSize;
            var shape = part.AddComponent<BoxCollider2D>();
            shape.size = new Vector2(width, height) * CellSize;
            return shape;
        }

        private PolygonCollider2D Polygon(string title, params Vector2[] cellVertices)
        {
            var part = Part(title);
            part.transform.position = Origin;
            for (int i = 0; i < cellVertices.Length; i++) cellVertices[i] *= CellSize;
            var shape = part.AddComponent<PolygonCollider2D>();
            shape.points = cellVertices;
            return shape;
        }

        private static bool InGate(int x, int y) => x >= GateX && x < GateX + GateWidth &&
            y >= GateY && y < GateY + GateHeight;

        public bool SetGate(bool open)
        {
            if (!Ready) return false;
            if (assetStudy != null)
            {
                if (!assetStudy.SetGate(open)) return false;
                GateOpen = open;
                return true;
            }
            if (!open)
            {
                // The whole gate closes atomically. Refuse if there is anything in the doorway.
                for (int y = GateY; y < GateY + GateHeight; y++)
                for (int x = GateX; x < GateX + GateWidth; x++)
                    if (!World.TrySnapshot(x, y, out var sample) || sample.Amount > 0 || sample.Fixed)
                        return false;
            }

            for (int y = GateY; y < GateY + GateHeight; y++)
            for (int x = GateX; x < GateX + GateWidth; x++)
                World.SetBarrier(x, y, !open);
            GateOpen = open;
            gate.enabled = !open;
            Physics2D.SyncTransforms();
            return true;
        }

        private void ToggleGate()
        {
            if (!SetGate(!GateOpen)) Notify("闸口尚有物质，等它流过再关闭。");
        }

        private bool PaintBarrier(int x, int y, bool enabled)
        {
            if (assetStudy != null ? assetStudy.IsGate(x, y) : InGate(x, y)) return false;
            int index = y * Width + x;
            if (paintedBarriers[index] == enabled || !World.SetBarrier(x, y, enabled)) return false;
            paintedBarriers[index] = enabled;
            PaintedBarrierCount += enabled ? 1 : -1;
            barrierColliderDirty = true;
            return true;
        }

        private void RefreshPaintedCollider()
        {
            barrierColliderDirty = false;
            barrierRuns.Clear();
            if (PaintedBarrierCount > 0)
            {
                for (int y = 0; y < Height; y++)
                {
                    int x = 0;
                    while (x < Width)
                    {
                        if (!paintedBarriers[y * Width + x]) { x++; continue; }
                        int left = x;
                        while (++x < Width && paintedBarriers[y * Width + x]) { }
                        barrierRuns.Add(new RectInt(left, y, x - left, 1));
                    }
                }
            }

            paintedCollider.enabled = barrierRuns.Count > 0;
            paintedCollider.pathCount = barrierRuns.Count;
            for (int i = 0; i < barrierRuns.Count; i++)
            {
                RectInt run = barrierRuns[i];
                barrierPath[0] = Origin + new Vector2(run.xMin, run.yMin) * CellSize;
                barrierPath[1] = Origin + new Vector2(run.xMax, run.yMin) * CellSize;
                barrierPath[2] = Origin + new Vector2(run.xMax, run.yMax) * CellSize;
                barrierPath[3] = Origin + new Vector2(run.xMin, run.yMax) * CellSize;
                paintedCollider.SetPath(i, barrierPath);
            }
            Physics2D.SyncTransforms();
        }

        public void ClearDynamic()
        {
            if (!Ready) return;
            ResetLayout(GateOpen);
        }

        public void ResetLab()
        {
            if (Ready) SelectPreset(currentPreset);
        }

        private void ResetLayout(bool leaveGateOpen)
        {
            World.ClearDynamic();
            assetStudy?.ClearFuel();
            assetGesture = assetRectangle = assetHasPoint = false;
            Array.Clear(paintedBarriers, 0, paintedBarriers.Length);
            PaintedBarrierCount = 0;
            if (paintedCollider.enabled || barrierColliderDirty) RefreshPaintedCollider();
            SetGate(leaveGateOpen);
            pendingStroke = worldGesture = strokeHasStart = paintHeld = false;
            simDebt = brushDebt = 0;
            feedback = null;
        }

        public void SelectPreset(MatterLabPreset value)
        {
            if (!Ready) return;
            if (value == MatterLabPreset.AssetStudy || value == MatterLabPreset.AssetEmpty)
            {
                OpenAssetStudy(value == MatterLabPreset.AssetStudy);
                return;
            }
            if (assetStudy != null)
            {
                ResetLayout(true);
                CloseAssetStudy(true);
                if (display == MatterDisplay.MapAssets || display == MatterDisplay.MapAssetsWorldLight)
                    SetDisplay(MatterDisplay.Painted);
            }
            currentPreset = value;
            ResetLayout(false);
            switch (value)
            {
                case MatterLabPreset.Flow:
                    Fill(Matter.Water, 60, 7, 100, 21);
                    Fill(Matter.Water, 62, 45, 75, 52);
                    Fill(Matter.Sand, 14, 43, 27, 46);
                    Fill(Matter.Grass, 163, 16, 179, 17);
                    Fill(Matter.Wood, 190, 16, 207, 19);
                    Fill(Matter.Oil, 216, 19, 227, 24);
                    break;
                case MatterLabPreset.Layering:
                    Fill(Matter.Oil, 62, 6, 96, 11);
                    Fill(Matter.Water, 62, 12, 96, 25);
                    break;
                case MatterLabPreset.Combustion:
                    Fill(Matter.Grass, 161, 16, 178, 18);
                    Fill(Matter.Wood, 178, 16, 186, 21);
                    Fill(Matter.Wood, 189, 16, 204, 20);
                    Fill(Matter.Oil, 216, 19, 228, 23);
                    World.AddHeat(164, 16, 850);
                    World.AddHeat(220, 22, 850);
                    break;
            }
        }

        private void Fill(Matter kind, int left, int bottom, int right, int top)
        {
            for (int y = bottom; y < top; y++)
            for (int x = left; x < right; x++) World.Add(x, y, kind);
        }
    }
}
