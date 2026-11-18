/* 深色主体、轮廓和独立布景使用同一套求解结果。画笔预览、拾取和烘焙均消费这里的片段。 */
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ossastra.SideView.TerrainWorkshop
{
    public sealed class TerrainPart
    {
        public string Key, Kit, Reason;
        public TerrainSlot Slot;
        public TerrainLayerKind Layer;
        public TerrainArt Art;
        public TerrainRule Rule;
        public bool Locked, Overridden, FlipX, FlipY;
        public Rect Bounds, UV;
        public Color Tint;
        public Vector2 A, B, C, D, Start, End;
    }

    public sealed class TerrainIssue
    {
        public string Key, Kit, Message;
        public TerrainSlot Slot;
        public Vector2 Position;
    }

    public sealed class TerrainSolution
    {
        public IReadOnlyList<TerrainPart> Parts { get; }
        public IReadOnlyList<TerrainIssue> Issues { get; }

        internal TerrainSolution(List<TerrainPart> parts, List<TerrainIssue> issues)
            => (Parts, Issues) = (parts.AsReadOnly(), issues.AsReadOnly());

        public TerrainPart Pick(Vector2 localPoint, float tolerance = .3f)
        {
            TerrainPart result = null;
            float chosen = float.PositiveInfinity;
            foreach (var part in Parts.Where(part => part.Layer == TerrainLayerKind.Border ||
                         part.Layer == TerrainLayerKind.Body || part.Layer == TerrainLayerKind.Decor))
            {
                var bounds = part.Bounds;
                var closest = new Vector2(Mathf.Clamp(localPoint.x, bounds.xMin, bounds.xMax),
                    Mathf.Clamp(localPoint.y, bounds.yMin, bounds.yMax));
                float distance = (closest - localPoint).sqrMagnitude;
                if (distance > tolerance * tolerance) continue;
                Vector2 span = part.End - part.Start;
                float t = span.sqrMagnitude > 0 ? Mathf.Clamp01(Vector2.Dot(localPoint - part.Start, span) / span.sqrMagnitude) : 0;
                float centerBias = (localPoint - part.Start - span * t).sqrMagnitude * .01f;
                float score = distance + centerBias + (part.Layer == TerrainLayerKind.Body ? 10 : 0);
                if (score < chosen)
                {
                    chosen = score;
                    result = part;
                }
            }
            return result;
        }
    }

    public static class TerrainAssembly
    {
        public static TerrainSolution Solve(TerrainMap map, TerrainGrid grid = null)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            grid ??= map.Read();
            return new Solver(map, grid).Build();
        }

        private sealed class Solver
        {
            private readonly TerrainMap map;
            private readonly TerrainGrid grid;
            private readonly float cell;
            private readonly List<TerrainPart> parts = new List<TerrainPart>();
            private readonly List<TerrainIssue> issues = new List<TerrainIssue>();
            private readonly Dictionary<string, TerrainOverride> overrides;
            private readonly HashSet<string> anchors = new HashSet<string>(StringComparer.Ordinal);

            public Solver(TerrainMap map, TerrainGrid grid)
            {
                this.map = map;
                this.grid = grid;
                cell = Mathf.Max(.05f, map.cellSize);
                overrides = map.overrides.Where(value => value != null && !string.IsNullOrWhiteSpace(value.key))
                    .GroupBy(value => value.key, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);
            }

            public TerrainSolution Build()
            {
                var tiles = grid.Tiles.ToArray();
                if (map.Visible(TerrainLayerKind.Body))
                    foreach (var kit in tiles.Select(tile => tile.kit).Distinct(StringComparer.Ordinal))
                        Fill(kit);

                foreach (var corner in grid.Corners())
                    Corner(corner);
                foreach (var band in grid.Bands())
                {
                    Band(band, band.Slot, TerrainLayerKind.Border);
                    Band(band, TerrainSlot.Attached, TerrainLayerKind.Decor);
                }
                foreach (var row in overrides.Values)
                    if (!anchors.Contains(row.key))
                        AddIssue(row.key, TerrainSlot.Top, null, Vector2.zero,
                            "原边段已移动或失去支撑，保留人工记录，请重锚或解除覆盖。");
                PlaceLayers(tiles);
                PlaceStamps();
                return new TerrainSolution(parts, issues);
            }

            private void Fill(string kitId)
            {
                var kit = map.Kit(kitId);
                var rule = kit == null ? null : kit.Rule(TerrainSlot.Fill);
                foreach (var box in grid.CollisionBoxes(kitId))
                {
                    var rect = new Rect(box.X, box.Y, box.Width, box.Height);
                    string key = FormattableString.Invariant($"fill:{box.X}:{box.Y}");
                    Color tint = kit == null ? new Color(.11f, .07f, .12f, 1) : kit.fill;
                    if (kit == null)
                        AddIssue(key, TerrainSlot.Fill, kitId, rect.center * cell, $"地图引用的套件不存在：{kitId}");
                    AddRectangle(key, kitId, TerrainSlot.Fill, TerrainLayerKind.Body, rule?.art, rule, rect,
                        tint, null, "实体占位合并后的主体。");
                }
            }

            private void Band(TerrainBand band, TerrainSlot slot, TerrainLayerKind layer)
            {
                var kit = map.Kit(band.Kit);
                var layerRule = map.Layer(layer);
                var basis = kit == null ? null : kit.Rule(slot);
                var fallback = layerRule?.art;
                if (slot == TerrainSlot.Attached && (band.Slot != TerrainSlot.Top || basis == null))
                    return;
                bool vertical = band.Start.x == band.End.x;
                float low = vertical ? Math.Min(band.Start.y, band.End.y) : Math.Min(band.Start.x, band.End.x);
                float high = low + band.Length;
                var normal = new Vector2(band.OutwardX, band.OutwardY);
                Vector2 At(float along) => vertical ? new Vector2(band.Start.x, along) : new Vector2(along, band.Start.y);

                string bandKey = band.KeyAt(vertical ? new TerrainPoint(band.Start.x, (int)low) :
                    new TerrainPoint((int)low, band.Start.y));
                var rule = basis;
                var art = rule?.art == null ? fallback : rule.art;
                if (rule == null && fallback != null && slot != TerrainSlot.Attached)
                    rule = new TerrainRule { slot = slot, art = fallback };
                if (!HasArt(art) || rule == null)
                {
                    if (slot != TerrainSlot.Attached)
                        AddIssue(bandKey, slot, band.Kit, At((low + high) * .5f) * cell, "此朝向缺少可用裁片。");
                    return;
                }
                if (band.Length < rule.minLength)
                {
                    if (!TryCap(kit, band, bandKey, At((low + high) * .5f), normal))
                        AddIssue(bandKey, slot, band.Kit, At((low + high) * .5f) * cell, "边段短于规则要求，校准短边端头或最小长度。");
                    return;
                }

                float span = Mathf.Max(.05f, rule.span);
                if (rule.placement == TerrainPlacement.Scatter && slot == TerrainSlot.Attached)
                {
                    PlaceScatter(band, rule, art, normal, vertical, low, high);
                    return;
                }
                float step = Mathf.Max(.05f, span + rule.spacing - Mathf.Max(0, rule.overlap));
                if (span / step > 8)
                {
                    AddIssue(bandKey, slot, band.Kit, At(low) * cell, "重叠量过大，缩小重叠或增大间距。");
                    return;
                }
                if (step > span + .005f && slot != TerrainSlot.Attached)
                    AddIssue(bandKey, slot, band.Kit, At(low) * cell, "间距大于覆盖跨度，会在边带之间留下间隔。");
                bool whole = rule.placement == TerrainPlacement.Whole;
                var slices = TerrainStripPacking.Place(low, high, span, step, whole).ToArray();
                if (slices.Length == 0)
                {
                    if (!TryCap(kit, band, bandKey, At((low + high) * .5f), normal))
                        AddIssue(bandKey, slot, band.Kit, At((low + high) * .5f) * cell, "有效边长放不下完整构件，需短边端头。");
                    return;
                }
                if (whole && (slices[0].Low > low + .005f || slices[slices.Length - 1].High < high - .005f))
                    AddIssue(bandKey, slot, band.Kit, At(low) * cell, "完整构件未占满边段，剩余长度可另配短边。");

                foreach (var slice in slices)
                {
                    float rawAlong = slice.Origin;
                    string key = PieceKey(band, rawAlong, slot);
                    var manual = Override(key);
                    var selectedRule = manual?.rule ?? Choose(kit, slot, rule, key);
                    var selectedArt = manual?.art ?? selectedRule.art ?? art;
                    if (!HasArt(selectedArt))
                    {
                        AddIssue(key, slot, band.Kit, At(slice.Low) * cell, "当前变体或手工裁片无效。");
                        continue;
                    }
                    float start = Mathf.Clamp01(selectedRule.start), end = Mathf.Clamp01(selectedRule.end);
                    if (end - start < .005f)
                    {
                        AddIssue(key, slot, band.Kit, At(slice.Low) * cell, "两个有效接点过近或顺序相反。");
                        continue;
                    }
                    float imageSpan = Mathf.Max(.05f, selectedRule.span);
                    // 已排好的占位槽使用固定有效宽度。替换可修改画片与法向位置，不能在邻段里产生第二条占位。
                    float sourceLow = Mathf.Lerp(start, end, (slice.Low - rawAlong) / imageSpan);
                    float sourceHigh = Mathf.Lerp(start, end, (slice.High - rawAlong) / imageSpan);
                    bool alongU = vertical == selectedRule.rotate;
                    var size = selectedArt.PixelSize;
                    float aspect = alongU ? size.y / size.x : size.x / size.y;
                    float cross = (vertical ? selectedRule.size.x : selectedRule.size.y);
                    if (cross <= 0) cross = Mathf.Clamp(imageSpan / (end - start) * aspect, .03f, 64);
                    float edge = (vertical ? band.Start.x : band.Start.y) +
                        (vertical ? normal.x : normal.y) * selectedRule.offset;
                    float crossLow = edge - Mathf.Clamp01(selectedRule.anchor) * cross;
                    float crossHigh = crossLow + cross;
                    var rect = vertical ? Rect.MinMaxRect(crossLow, slice.Low, crossHigh, slice.High) :
                        Rect.MinMaxRect(slice.Low, crossLow, slice.High, crossHigh);
                    var part = AddRectangle(key, band.Kit, slot, layer, selectedArt, selectedRule, rect,
                        Color.white, manual, whole ? "完整构件按有效跨度放置。" : "实体边界朝向匹配，重复相位固定在地图坐标。");
                    if (part == null) continue;
                    part.Start = At(slice.Low) * cell;
                    part.End = At(slice.High) * cell;
                    var uv = selectedArt.UV;
                    if (selectedRule.flip)
                        (sourceLow, sourceHigh) = (1 - sourceHigh, 1 - sourceLow);
                    part.UV = alongU ? new Rect(uv.x + uv.width * sourceLow, uv.y, uv.width * (sourceHigh - sourceLow), uv.height) :
                        new Rect(uv.x, uv.y + uv.height * sourceLow, uv.width, uv.height * (sourceHigh - sourceLow));
                    if (selectedRule.rotate)
                        RotateCard(part, vertical);
                    if (alongU) part.FlipX = selectedRule.flip;
                    else part.FlipY = selectedRule.flip;
                }
            }

            private void PlaceScatter(TerrainBand band, TerrainRule rule, TerrainArt art, Vector2 normal,
                bool vertical, float low, float high)
            {
                float step = Mathf.Max(1, rule.span + rule.spacing);
                foreach (var slice in TerrainStripPacking.Place(low, high, step, step, true))
                {
                    string key = PieceKey(band, slice.Origin, TerrainSlot.Attached);
                    if (Stable01(key + band.Kit) > Mathf.Clamp01(rule.weight)) continue;
                    var manual = Override(key);
                    var selected = manual?.rule ?? rule;
                    var selectedArt = manual?.art ?? selected.art ?? art;
                    var size = Size(selectedArt, selected, .7f);
                    Vector2 point = vertical ? new Vector2(band.Start.x, slice.Origin + step * .5f) :
                        new Vector2(slice.Origin + step * .5f, band.Start.y);
                    point += normal * (selected.offset + size.y * .5f);
                    AddRectangle(key, band.Kit, TerrainSlot.Attached, TerrainLayerKind.Decor, selectedArt,
                        selected, new Rect(point - size * .5f, size), Color.white, manual, "装饰依附于有效外表面。");
                }
            }

            private bool TryCap(TerrainKit kit, TerrainBand band, string key, Vector2 position, Vector2 normal)
            {
                var rule = kit == null ? null : kit.Rule(TerrainSlot.Cap);
                if (rule == null || !HasArt(rule.art) || band.Length < rule.minLength || rule.span > band.Length + .01f)
                    return false;
                key += ":cap";
                var manual = Override(key);
                var art = manual?.art ?? rule.art;
                var size = Size(art, rule, Mathf.Min(1, band.Length));
                position += normal * rule.offset;
                AddRectangle(key, band.Kit, TerrainSlot.Cap, TerrainLayerKind.Border, art, rule,
                    new Rect(position - size * .5f, size), Color.white, manual, "短边匹配到独立端头。");
                return true;
            }

            private void Corner(TerrainCorner corner)
            {
                var kit = map.Kit(corner.Kit);
                var manual = Override(corner.Key);
                var rule = manual?.rule ?? (kit == null ? null : Choose(kit, corner.Slot, kit.Rule(corner.Slot), corner.Key));
                var art = manual?.art ?? rule?.art;
                var pivot = new Vector2(corner.Point.x, corner.Point.y);
                if (rule == null || !HasArt(art))
                {
                    AddIssue(corner.Key, corner.Slot, corner.Kit, pivot * cell, "当前套件缺少该转角图件。");
                    return;
                }
                var size = Size(art, rule, 1);
                var signs = new Vector2(corner.OutwardX, corner.OutwardY);
                var location = pivot - size * .5f + signs * rule.offset;
                var part = AddRectangle(corner.Key, corner.Kit, corner.Slot, TerrainLayerKind.Border, art, rule,
                    new Rect(location, size), Color.white, manual,
                    corner.Slot == TerrainSlot.Convex ? "实体外转角使用独立凸角片。" : "空域内转角使用独立凹角片。");
                if (part != null)
                {
                    part.Start = part.End = pivot * cell;
                    part.FlipX = rule.flip && corner.OutwardX < 0;
                    part.FlipY = rule.flip && corner.OutwardY < 0;
                    if (rule.rotate) RotateCard(part, false);
                }
            }

            private void PlaceLayers(TerrainTile[] tiles)
            {
                float minX = tiles.Length == 0 ? -16 : tiles.Min(tile => tile.point.x) - 18;
                float maxX = tiles.Length == 0 ? 16 : tiles.Max(tile => tile.point.x) + 19;
                float minY = tiles.Length == 0 ? -10 : tiles.Min(tile => tile.point.y) - 12;
                float maxY = tiles.Length == 0 ? 10 : tiles.Max(tile => tile.point.y) + 13;
                foreach (var layer in map.layers)
                {
                    if (layer == null || !HasArt(layer.art) || layer.kind == TerrainLayerKind.Body ||
                        layer.kind == TerrainLayerKind.Border || layer.kind == TerrainLayerKind.Decor)
                        continue;
                    var art = layer.art;
                    var source = art.PixelSize;
                    float worldHeight = maxY - minY, worldWidth = worldHeight * source.x / source.y;
                    worldWidth = Mathf.Max(1, worldWidth);
                    int count = Mathf.CeilToInt((maxX - minX) / worldWidth);
                    if (count > 1024)
                    {
                        AddIssue("layer:" + layer.kind, TerrainSlot.Attached, null, Vector2.zero,
                            "背景素材过窄，无法按原比例覆盖整个地图，请使用较宽的图件。");
                        continue;
                    }
                    for (int i = 0; i < count; i++)
                        AddRectangle($"layer:{layer.kind}:{i}", null, TerrainSlot.Attached, layer.kind, art, null,
                            new Rect(minX + i * worldWidth, minY, worldWidth, worldHeight), Color.white, null,
                            "独立图层重复构件，不参与实体和碰撞。");
                }
            }

            private void PlaceStamps()
            {
                for (int index = 0; index < map.stamps.Count; index++)
                {
                    var stamp = map.stamps[index];
                    if (stamp == null) continue;
                    string key = "stamp:" + index;
                    if (!string.IsNullOrEmpty(stamp.anchor) && !anchors.Contains(stamp.anchor))
                    {
                        AddIssue(key, TerrainSlot.Attached, null, stamp.position * cell, "装饰失去附着边，需要重新锚定。");
                        continue;
                    }
                    if (!HasArt(stamp.art))
                    {
                        AddIssue(key, TerrainSlot.Attached, null, stamp.position * cell, "装饰的素材引用已失效。");
                        continue;
                    }
                    var size = (stamp.size.x > 0 && stamp.size.y > 0) ? stamp.size : Size(stamp.art, null, 1);
                    var part = AddRectangle(key, null, TerrainSlot.Attached, stamp.layer, stamp.art, null,
                        new Rect(stamp.position - size * .5f, size), Color.white, null, "手工放置的装饰。");
                    if (part != null) part.Locked = stamp.locked;
                }
            }

            private TerrainPart AddRectangle(string key, string kit, TerrainSlot slot, TerrainLayerKind layer,
                TerrainArt art, TerrainRule rule, Rect rect, Color tint, TerrainOverride manual, string reason)
            {
                if (!map.Visible(layer)) return null;
                var bounds = new Rect(rect.position * cell, rect.size * cell);
                var part = new TerrainPart
                {
                    Key = key, Kit = kit, Slot = slot, Layer = layer, Art = art, Rule = rule, Bounds = bounds,
                    Tint = tint, Locked = manual?.locked ?? false, Overridden = manual != null,
                    Reason = manual == null ? reason : "此片段使用人工记录。" + reason,
                    UV = HasArt(art) ? art.UV : new Rect(0, 0, 1, 1),
                    A = bounds.min, B = new Vector2(bounds.xMin, bounds.yMax), C = bounds.max,
                    D = new Vector2(bounds.xMax, bounds.yMin), Start = bounds.center, End = bounds.center
                };
                parts.Add(part);
                return part;
            }

            private TerrainOverride Override(string key)
            {
                anchors.Add(key);
                return overrides.TryGetValue(key, out var value) ? value : null;
            }

            private void AddIssue(string key, TerrainSlot slot, string kit, Vector2 position, string message)
                => issues.Add(new TerrainIssue { Key = key, Kit = kit, Slot = slot, Position = position, Message = message });
        }

        private static TerrainRule Choose(TerrainKit kit, TerrainSlot slot, TerrainRule reference, string key)
        {
            if (kit == null || reference == null) return reference;
            var matches = kit.rules.Where(rule => rule != null && rule.slot == slot && HasArt(rule.art) && rule.weight > 0 &&
                rule.placement == reference.placement && Mathf.Approximately(rule.span, reference.span) &&
                Mathf.Approximately(rule.spacing, reference.spacing) && Mathf.Approximately(rule.overlap, reference.overlap)).ToArray();
            if (matches.Length == 0) return reference;
            if (matches.Length == 1) return matches[0];
            float value = Stable01(kit.Id + "|" + key) * matches.Sum(rule => rule.weight);
            foreach (var match in matches)
            {
                value -= match.weight;
                if (value <= 0) return match;
            }
            return matches[matches.Length - 1];
        }

        private static float Stable01(string text)
        {
            uint hash = 2166136261;
            foreach (char c in text) hash = unchecked((hash ^ c) * 16777619);
            return (hash & 0xFFFFFF) / 16777216f;
        }

        private static string PieceKey(TerrainBand band, float along, TerrainSlot slot)
        {
            int whole = Mathf.FloorToInt(along + .00005f);
            int fraction = Mathf.RoundToInt((along - whole) * 10000);
            var anchor = band.Start.x == band.End.x ? new TerrainPoint(band.Start.x, whole) : new TerrainPoint(whole, band.Start.y);
            return FormattableString.Invariant($"{band.KeyAt(anchor)}:{fraction}:{(int)slot}");
        }

        private static bool HasArt(TerrainArt art) => art != null && art.Image != null &&
            art.PixelSize.x > 0 && art.PixelSize.y > 0;

        private static Vector2 Size(TerrainArt art, TerrainRule rule, float fallback)
        {
            if (rule != null && rule.size.x > 0 && rule.size.y > 0) return rule.size;
            var source = HasArt(art) ? art.PixelSize : Vector2.one;
            float width = rule != null && rule.size.x > 0 ? rule.size.x : fallback;
            return new Vector2(width, Mathf.Clamp(width * source.y / source.x, .1f, 64));
        }

        private static void RotateCard(TerrainPart part, bool vertical)
        {
            (part.A, part.B, part.C, part.D) = vertical ? (part.D, part.A, part.B, part.C) :
                (part.B, part.C, part.D, part.A);
        }
    }
}
