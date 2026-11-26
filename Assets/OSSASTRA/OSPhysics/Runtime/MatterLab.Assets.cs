// 职责：把资产样板、落笔事务和预览接到实验室的正常输入与预设生命周期。
using UnityEngine;

namespace Ossastra.OSPhysics
{
    public sealed partial class MatterLab
    {
        private MatterAssetStudy assetStudy;
        private MatterAssetBrush assetBrush;
        private int grassVariant, woodVariant;
        private bool assetFlip;
        private bool assetOverlay;
        private bool assetRectangle;
        private Vector2 assetStart, lastAssetPoint;
        private bool assetGesture;
        private bool assetHasPoint;
        private Color legacyCameraBackground;
        private MatterPaintLayers legacyLayers;
        private bool legacyPrivateArt;
        private float legacyMood;

        public MatterAssetBrush AssetBrush => assetBrush;
        public string AssetStatus => assetStudy?.Status ?? "选择资产交互样板";
        public int AssetCount => assetStudy?.Fuel.Instances.Count ?? 0;
        public int AssetBlocks => assetStudy?.Count ?? 0;
        public int AssetPieces => assetStudy?.PieceCount ?? 0;
        public int AssetBatches => assetStudy?.DrawCount ?? 0;
        public float AssetRebuildMilliseconds => assetStudy?.BuildTime ?? 0;
        public float AssetAuthorCell => assetStudy?.TileSize ?? 0;

        public void SetAssetBrush(MatterAssetBrush tool)
        {
            assetBrush = assetStudy != null ? tool : MatterAssetBrush.Matter;
            pendingStroke = paintHeld = strokeHasStart = worldGesture = false;
            assetGesture = assetRectangle = assetHasPoint = false;
        }

        public bool ApplyAssetBrush(Vector2 worldPoint, MatterAssetBrush tool, int variant = 0, bool flip = false)
        {
            if (assetStudy == null || !WorldToCell(worldPoint, out _, out _)) return false;
            string reason = null;
            bool changed;
            switch (tool)
            {
                case MatterAssetBrush.Block:
                    changed = assetStudy.Edit(assetStudy.TileArea(worldPoint, worldPoint), true, out reason);
                    break;
                case MatterAssetBrush.Grass:
                case MatterAssetBrush.Wood:
                    changed = assetStudy.Place(worldPoint, tool, variant, flip, out reason);
                    break;
                case MatterAssetBrush.Delete:
                    changed = assetStudy.DeleteAt(worldPoint) ||
                        assetStudy.Edit(assetStudy.TileArea(worldPoint, worldPoint), false, out reason);
                    break;
                default:
                    return false;
            }
            if (!changed && reason != null) Notify(reason);
            return changed;
        }

        private bool OpenAssetStudy(bool seeded)
        {
            var kit = MatterAssetTerrain.Load?.Invoke();
            if (kit == null)
            {
                Notify("地图套件桥接不可用，原实验布置保留。");
                return false;
            }
            if (kit.CellSize < CellSize || kit.CellSize > 1.6f)
            {
                kit.Dispose();
                Notify("作者格尺度无法在当前实验室中表达。");
                return false;
            }
            MatterAssetStudy next;
            try
            {
                next = new MatterAssetStudy(kit, World, transform, Origin, CellSize);
            }
            catch (System.Exception error)
            {
                kit.Dispose();
                Notify(error.Message);
                return false;
            }

            if (assetStudy == null)
            {
                legacyCameraBackground = labCamera.backgroundColor;
                legacyLayers = paintLayers;
                legacyPrivateArt = privateArt;
                legacyMood = paintMood;
            }
            ResetLayout(true);
            CloseAssetStudy(false);
            SetBaselineTerrain(false);
            assetStudy = next;
            currentPreset = seeded ? MatterLabPreset.AssetStudy : MatterLabPreset.AssetEmpty;
            SetAssetBrush(MatterAssetBrush.Block);
            SetDisplay(MatterDisplay.MapAssets);
            paintLayers &= ~(MatterPaintLayers.Background | MatterPaintLayers.Foreground);
            paintMood = 1;
            privateArt = true;
            labCamera.backgroundColor = new Color(.10f, .129f, .158f, 1);
            int width = assetStudy.Columns;
            bool Layout(int x, int y, int w, int h) =>
                assetStudy.Edit(new RectInt(x, y, w, h), true, out _);
            Layout(0, 0, width, 1);
            Layout(0, 1, 1, assetStudy.Rows - 1);
            Layout(width - 1, 1, 1, assetStudy.Rows - 1);
            if (seeded && width >= 20)
            {
                Layout(1, 1, 3, 1);
                Layout(5, 1, 1, 3);
                Layout(6, 3, 4, 1);
                Layout(2, 7, 4, 1);
                Layout(width - 12, 1, 10, 1);
                Layout(width - 10, 2, 3, 1);
                Layout(width - 12, 5, 5, 1);
                Layout(width - 7, 6, 4, 1);
                assetStudy.Place(new Vector2(assetStudy.TileRect(new Vector2Int(width - 10, 2)).center.x,
                    assetStudy.TileRect(new Vector2Int(width - 10, 2)).yMax + .1f),
                    MatterAssetBrush.Grass, 0, false, out _);
                assetStudy.Place(new Vector2(assetStudy.TileRect(new Vector2Int(width - 6, 1)).center.x,
                    assetStudy.TileRect(new Vector2Int(width - 6, 1)).yMax + .1f),
                    MatterAssetBrush.Grass, 1, false, out _);
                Rect branch = assetStudy.TileRect(new Vector2Int(width - 4, 5));
                var rootPoint = new Vector2(branch.center.x, branch.yMax - .04f);
                assetStudy.Place(rootPoint, MatterAssetBrush.Wood, 0, false, out _);

                int x0 = Mathf.RoundToInt(width * .23f * assetStudy.TileSize / CellSize);
                int x1 = Mathf.RoundToInt(width * .39f * assetStudy.TileSize / CellSize);
                int floor = Mathf.CeilToInt(assetStudy.TileSize / CellSize);
                Fill(Matter.Water, x0, floor, x1, floor + 10);
                Fill(Matter.Water, x0 - 4, floor + 50, x0 + 12, floor + 60);
            }
            SetGate(false);
            assetStudy.FlushTerrain();
            return true;
        }

        private void CloseAssetStudy(bool restoreOriginal)
        {
            if (assetStudy == null) return;
            assetStudy.Dispose();
            assetStudy = null;
            SetAssetBrush(MatterAssetBrush.Matter);
            if (!restoreOriginal) return;
            SetBaselineTerrain(true);
            labCamera.backgroundColor = legacyCameraBackground;
            paintLayers = legacyLayers;
            privateArt = legacyPrivateArt;
            paintMood = legacyMood;
        }

        private bool HandleAssetInput(Vector2 pixels, bool onWorld, bool pressed, bool held, bool released)
        {
            if (assetStudy == null || assetBrush == MatterAssetBrush.Matter) return false;
            if (!onWorld || !TryScreenPoint(pixels, out var point))
            {
                assetGesture = assetRectangle = assetHasPoint = false;
                return true;
            }
            if (pressed)
            {
                assetStart = point;
                assetGesture = true;
                assetHasPoint = false;
                assetRectangle = assetBrush == MatterAssetBrush.Block || assetBrush == MatterAssetBrush.Delete;
            }
            if (assetGesture && assetRectangle && (released || !held))
            {
                if (assetBrush == MatterAssetBrush.Delete &&
                    Vector2.Distance(assetStart, point) < assetStudy.TileSize * .34f && assetStudy.DeleteAt(point))
                {
                    assetGesture = assetRectangle = false;
                    return true;
                }
                if (!assetStudy.Edit(assetStudy.TileArea(assetStart, point), assetBrush == MatterAssetBrush.Block,
                    out string reason)) Notify(reason);
                assetGesture = assetRectangle = false;
            }
            else if (assetGesture && !assetRectangle && held)
            {
                var stamps = assetStudy.Stamps(assetBrush);
                int variant = assetBrush == MatterAssetBrush.Grass ? grassVariant : woodVariant;
                float spacing = stamps.Count > 0 ? stamps[Mathf.Clamp(variant, 0, stamps.Count - 1)].Size.x * .8f : 1;
                if (!assetHasPoint || Vector2.Distance(lastAssetPoint, point) >= Mathf.Max(.17f, spacing))
                {
                    ApplyAssetBrush(point, assetBrush, variant, assetFlip);
                    assetHasPoint = true;
                    lastAssetPoint = point;
                }
            }
            if (!held && !pressed) assetGesture = assetHasPoint = false;
            return true;
        }

        private void PresentAssets()
        {
            if (assetStudy == null) return;
            bool show = display == MatterDisplay.MapAssets || display == MatterDisplay.MapAssetsWorldLight;
            assetStudy.Present(show, HasPaintLayer(MatterPaintLayers.Body), HasPaintLayer(MatterPaintLayers.Trim),
                HasPaintLayer(MatterPaintLayers.Decoration), paintMood, visual.WorldLightLive);
        }

        private void DrawAssetOverlay(Vector2 pointer, Vector2 gui)
        {
            if (assetStudy == null) return;
            if (assetOverlay)
            {
                foreach (var tile in assetStudy.Blocks)
                    OutlineAsset(GuiRect(assetStudy.TileRect(tile)), new Color(.5f, .62f, .9f, .5f));
                foreach (var piece in assetStudy.Fuel.Instances)
                {
                    Color ink = new Color(.34f, .87f, .77f, .82f);
                    OutlineAsset(GuiRect(piece.Fuel), ink);
                    foreach (int index in piece.Cells)
                    {
                        if (!assetStudy.Fuel.IsOwner(index, piece.Id)) continue;
                        Vector2 center = CellToWorld(index % Width, index / Width);
                        Rect marker = GuiRect(new Rect(center - Vector2.one * .022f, Vector2.one * .044f));
                        Box(marker, ink);
                    }
                }
            }

            if (PanelRect.Contains(gui) || assetBrush == MatterAssetBrush.Matter ||
                !TryScreenPoint(pointer, out var position) || !WorldToCell(position, out _, out _)) return;
            if (assetBrush == MatterAssetBrush.Block || assetBrush == MatterAssetBrush.Delete)
            {
                RectInt cells = assetStudy.TileArea(assetRectangle ? assetStart : position, position);
                Rect first = assetStudy.TileRect(cells.min);
                Rect last = assetStudy.TileRect(cells.max - Vector2Int.one);
                Rect rect = GuiRect(Rect.MinMaxRect(first.xMin, first.yMin, last.xMax, last.yMax));
                Color ink = assetBrush == MatterAssetBrush.Delete ? Amber : BlueGreen;
                Box(rect, new Color(ink.r, ink.g, ink.b, .16f));
                OutlineAsset(rect, ink);
                return;
            }
            int variant = assetBrush == MatterAssetBrush.Grass ? grassVariant : woodVariant;
            bool valid = assetStudy.Preview(position, assetBrush, variant, assetFlip, out var art, out var bounds,
                out var fuel, out var rootPoint, out _, out _);
            Color line = valid ? BlueGreen : Amber;
            if (art.Image != null)
            {
                Rect uv = art.UV;
                if (assetFlip && art.Flip) { uv.x += uv.width; uv.width = -uv.width; }
                Color old = GUI.color;
                GUI.color = new Color(1, 1, 1, valid ? .72f : .32f);
                GUI.DrawTextureWithTexCoords(GuiRect(bounds), art.Image, uv, true);
                GUI.color = old;
                OutlineAsset(GuiRect(fuel), line);
                Rect anchor = GuiRect(new Rect(rootPoint - Vector2.one * .045f, Vector2.one * .09f));
                Box(anchor, line);
            }
        }

        private Rect GuiRect(Rect worldRect)
        {
            Vector3 lo = labCamera.WorldToScreenPoint(worldRect.min);
            Vector3 hi = labCamera.WorldToScreenPoint(worldRect.max);
            return Rect.MinMaxRect(lo.x, Screen.height - hi.y, hi.x, Screen.height - lo.y);
        }

        private static void OutlineAsset(Rect rect, Color color)
        {
            Box(new Rect(rect.xMin, rect.yMin, rect.width, 1), color);
            Box(new Rect(rect.xMin, rect.yMax - 1, rect.width, 1), color);
            Box(new Rect(rect.xMin, rect.yMin, 1, rect.height), color);
            Box(new Rect(rect.xMax - 1, rect.yMin, 1, rect.height), color);
        }

        private void ShutdownAssets() => CloseAssetStudy(false);
    }
}
