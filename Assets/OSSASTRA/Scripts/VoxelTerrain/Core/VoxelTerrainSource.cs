using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Ossastra.VoxelTerrain
{
    /// <summary>原生地编母体与冻结表面的校验接缝；不修改源数据，不重建或修补表面。</summary>
    public sealed class VoxelTerrainSource
    {
        public const int MaximumCells = 250000;
        public const int MaximumQuads = 250000;
        public const int MaximumVertices = 1000000;
        public string AssetId { get; }
        public string SourceSha256 { get; }
        public float VoxelSize { get; }
        public int CellCount { get; }
        public int QuadCount => Quads.Length;
        public int ExposedUnitFaceCount { get; }
        internal readonly Color[] Palette;
        internal readonly int[] MaterialRoles;
        internal readonly FrozenQuad[] Quads;

        private VoxelTerrainSource(string assetId, string hash, int cellCount, int faces, Color[] palette, int[] roles, FrozenQuad[] quads)
        {
            AssetId = assetId; SourceSha256 = hash; VoxelSize = 0.25f;
            CellCount = cellCount; ExposedUnitFaceCount = faces;
            Palette = palette; MaterialRoles = roles; Quads = quads;
        }

        public static VoxelTerrainSource Parse(string authorJson, string surfaceJson)
        {
            var author = Load(authorJson, 32 * 1024 * 1024, "author");
            var surface = Load(surfaceJson, 64 * 1024 * 1024, "surface");
            Fields(author, "schema assetId revision name voxelSize cells palette sideColor materialProfile protectedColumns editor anchorM rootMode provenance legacyNative");
            Fields(surface, "schema sourceSha256 assetId revision voxelSize axes materialRoles quads");
            Require(Text(author, "schema") == "xinghai-workshop-asset-1", "Unsupported author schema");
            Require(Text(surface, "schema") == "ossastra-voxel-surface-1", "Unsupported frozen surface schema");
            Require(Number(author, "voxelSize") == 0.25 && Number(surface, "voxelSize") == 0.25, "Terrain voxel size must be 0.25 meters");
            Require(Text(surface, "axes") == "RH_Z_UP", "Frozen surface axes must be RH_Z_UP");
            string id = Text(author, "assetId");
            Require(Regex.IsMatch(id, "^[A-Za-z0-9_-]{1,80}$"), "Invalid native asset identity");
            Require(Text(surface, "assetId") == id && Integer(surface, "revision", 0, int.MaxValue) == Integer(author, "revision", 0, int.MaxValue), "Source identity or revision mismatch");
            Require(Text(author, "name").Length <= 200, "Invalid asset name");
            Require(Text(author, "rootMode") == "grid", "Terrain requires native grid rootMode");
            var anchor = Array(author, "anchorM", 3, 3);
            Require(anchor.All(v => v.Type == JTokenType.Integer || v.Type == JTokenType.Float) && anchor.All(v => (double)v == 0), "Terrain anchor must remain zero; place through owning scene root");
            Array(author, "protectedColumns", 0, 10000);
            string hash;
            using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(authorJson))).Replace("-", "").ToLowerInvariant();
            Require(Text(surface, "sourceSha256") == hash, "Frozen surface source SHA256 mismatch");

            var paletteTokens = Array(author, "palette", 1, 64);
            var palette = new Color[paletteTokens.Count];
            for (int i = 0; i < palette.Length; i++)
            {
                string hex = (string)paletteTokens[i];
                Require(paletteTokens[i].Type == JTokenType.String && Regex.IsMatch(hex, "^#[0-9a-fA-F]{6}$") && ColorUtility.TryParseHtmlString(hex, out palette[i]), "Invalid palette color");
            }
            ValidateProfile(author["materialProfile"] as JObject, palette);
            var roleTokens = Array(surface, "materialRoles", palette.Length, palette.Length);
            var roles = new int[palette.Length];
            for (int i = 0; i < roles.Length; i++)
            {
                string role = (string)roleTokens[i];
                Require(roleTokens[i].Type == JTokenType.String && (role == "grass" || role == "soil" || role == "basin"), "Unknown material role");
                roles[i] = role == "grass" ? 0 : role == "soil" ? 1 : 2;
            }
            int sideColor = author["sideColor"] == null ? -1 : Integer(author, "sideColor", 0, palette.Length - 1);
            var cellTokens = Array(author, "cells", 1, MaximumCells);
            var cells = new Dictionary<Vector3Int, int>(cellTokens.Count);
            foreach (var token in cellTokens)
            {
                var cell = Object(token, "cell"); Fields(cell, "x y z color owner");
                var point = new Vector3Int(Integer(cell, "x", -8192, 8192), Integer(cell, "y", -8192, 8192), Integer(cell, "z", -8192, 8192));
                int color = Integer(cell, "color", 0, palette.Length - 1);
                Require(!cells.ContainsKey(point), "Duplicate source voxel");
                Require(cell["owner"] == null || Text(cell, "owner") == "height" || Text(cell, "owner") == "volume", "Invalid source voxel owner");
                cells.Add(point, color);
            }

            var quadTokens = Array(surface, "quads", 1, MaximumQuads);
            Require((long)quadTokens.Count * 4 <= MaximumVertices, "Frozen surface vertex budget exceeded");
            var quads = new FrozenQuad[quadTokens.Count];
            var covered = new HashSet<UnitFace>();
            for (int i = 0; i < quads.Length; i++)
            {
                var row = Object(quadTokens[i], "quad"); Fields(row, "axis sign plane a b w h color chunk");
                var quad = new FrozenQuad {
                    Axis = Integer(row, "axis", 0, 2), Sign = Integer(row, "sign", -1, 1),
                    Plane = Integer(row, "plane", -8192, 8193), A = Integer(row, "a", -8192, 8192), B = Integer(row, "b", -8192, 8192),
                    Width = Integer(row, "w", 1, 16), Height = Integer(row, "h", 1, 16), Color = Integer(row, "color", 0, palette.Length - 1), Chunk = Text(row, "chunk")
                };
                Require(quad.Sign != 0, "Quad sign must be -1 or +1");
                Require(roles[quad.Color] != 0 || (quad.Axis == 2 && quad.Sign == 1), "Grass role may only cover source upward faces");
                var chunk = Chunk(quad.Chunk);
                for (int b = quad.B; b < quad.B + quad.Height; b++)
                    for (int a = quad.A; a < quad.A + quad.Width; a++)
                    {
                        Vector3Int point = quad.CellAt(a, b);
                        Require(point.x >= -8192 && point.x <= 8192 && point.y >= -8192 && point.y <= 8192 && point.z >= -8192 && point.z <= 8192, "Quad source coordinate out of range");
                        Require(FloorChunk(point.x) == chunk.x && FloorChunk(point.y) == chunk.y && FloorChunk(point.z) == chunk.z, "Quad crosses or lies outside its native chunk");
                        Require(cells.TryGetValue(point, out int sourceColor), "Quad has no source voxel");
                        Require(!cells.ContainsKey(point + Direction(quad.Axis, quad.Sign)), "Frozen surface contains an internal face");
                        int expected = quad.Axis == 2 && quad.Sign == 1 ? sourceColor : sideColor >= 0 ? sideColor : sourceColor;
                        Require(expected == quad.Color, "Frozen surface violates native top/side palette mapping");
                        Require(covered.Add(new UnitFace(point, quad.Axis * 2 + (quad.Sign > 0 ? 1 : 0))), "Frozen surface contains a duplicate unit face");
                    }
                quads[i] = quad;
            }
            int exposed = 0;
            foreach (var cell in cells)
                for (int axis = 0; axis < 3; axis++)
                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        if (cells.ContainsKey(cell.Key + Direction(axis, sign))) continue;
                        exposed++;
                        Require(covered.Contains(new UnitFace(cell.Key, axis * 2 + (sign > 0 ? 1 : 0))), "Frozen surface omits an exterior source face");
                    }
            Require(exposed == covered.Count, "Frozen surface coverage mismatch");
            return new VoxelTerrainSource(id, hash, cells.Count, exposed, palette, roles, quads);
        }

        private static void ValidateProfile(JObject profile, Color[] palette)
        {
            Require(profile != null, "Missing native material profile");
            Require(Text(profile, "schema") == "xinghai-palette-1" && Text(profile, "unit") == "meters" && Text(profile, "axes") == "RH_Z_UP" && Number(profile, "gridStep") == 0.25, "Native material profile coordinate contract mismatch");
            Require(!string.IsNullOrWhiteSpace(Text(profile, "profileId")) && !string.IsNullOrWhiteSpace(Text(profile, "paletteId")), "Native material profile identity missing");
            Integer(profile, "profileRevision", 1, int.MaxValue); Integer(profile, "paletteRevision", 1, int.MaxValue);
            var colors = Array(profile, "colors", palette.Length, palette.Length);
            var identities = new HashSet<string>();
            for (int i = 0; i < colors.Count; i++)
            {
                var color = Object(colors[i], "profile color");
                string colorId = Text(color, "colorId"); Require(!string.IsNullOrWhiteSpace(colorId) && identities.Add(colorId), "Native color identity missing or duplicate");
                var rgb = Array(color, "baseColor_sRGB", 3, 3); var emissive = Array(color, "emissive", 3, 3);
                for (int c = 0; c < 3; c++)
                {
                    double component = Number(rgb[c], "baseColor_sRGB");
                    Require(component >= 0 && component <= 1 && Math.Abs(component - palette[i][c]) < 0.00001, "Profile palette color mismatch");
                    double emission = Number(emissive[c], "emissive"); Require(emission >= 0 && emission <= 1, "Invalid emission color");
                }
                foreach (string field in new[] { "alpha", "roughness", "metallic" }) { double value = Number(color, field); Require(value >= 0 && value <= 1, "Invalid native material parameter"); }
                Require(Number(color, "alpha") == 1 && Text(color, "alphaMode") == "OPAQUE", "Terrain surface must be opaque");
                Require(Number(color, "emissiveStrength") >= 0 && color["doubleSided"] != null && color["doubleSided"].Type == JTokenType.Boolean, "Invalid native material parameters");
            }
        }

        private static JObject Load(string json, int maxLength, string label)
        {
            Require(json != null && json.Length > 0 && json.Length <= maxLength && json[0] != '\ufeff', label + " JSON absent, BOM-prefixed or over budget");
            try
            {
                using (var input = new StringReader(json))
                using (var reader = new JsonTextReader(input) { MaxDepth = 32, DateParseHandling = DateParseHandling.None })
                {
                    var result = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    Require(!reader.Read(), "Trailing JSON is not permitted"); return result;
                }
            }
            catch (JsonException exception) { throw new FormatException("Invalid " + label + " JSON", exception); }
        }
        private static JObject Object(JToken value, string label) { var row = value as JObject; Require(row != null, "Invalid " + label); return row; }
        private static JArray Array(JObject row, string field, int min, int max) { var array = row[field] as JArray; Require(array != null && array.Count >= min && array.Count <= max, field + " absent or over budget"); return array; }
        private static string Text(JObject row, string field) { var value = row[field]; Require(value != null && value.Type == JTokenType.String, "Invalid " + field); return (string)value; }
        private static double Number(JObject row, string field) { return Number(row[field], field); }
        private static double Number(JToken value, string field) { Require(value != null && (value.Type == JTokenType.Integer || value.Type == JTokenType.Float), "Invalid " + field); double number = (double)value; Require(!double.IsNaN(number) && !double.IsInfinity(number), "Non-finite " + field); return number; }
        private static int Integer(JObject row, string field, int min, int max) { var value = row[field]; Require(value != null && value.Type == JTokenType.Integer, "Invalid integer " + field); long number; try { number = (long)value; } catch (Exception) { throw new FormatException("Integer out of range " + field); } Require(number >= min && number <= max, field + " outside allowed range"); return (int)number; }
        private static void Fields(JObject row, string allowed) { var set = new HashSet<string>(allowed.Split(' ')); Require(row.Properties().All(p => set.Contains(p.Name)), "Unsupported JSON field"); }
        private static Vector3Int Chunk(string key) { var parts = key.Split(','); int x = 0, y = 0, z = 0; Require(parts.Length == 3 && int.TryParse(parts[0], out x) && int.TryParse(parts[1], out y) && int.TryParse(parts[2], out z) && key == x + "," + y + "," + z, "Invalid native chunk identity"); return new Vector3Int(x, y, z); }
        private static int FloorChunk(int value) { return Mathf.FloorToInt(value / 16f); }
        private static Vector3Int Direction(int axis, int sign) { return axis == 0 ? new Vector3Int(sign, 0, 0) : axis == 1 ? new Vector3Int(0, sign, 0) : new Vector3Int(0, 0, sign); }
        private static void Require(bool condition, string message) { if (!condition) throw new FormatException(message); }

        internal struct FrozenQuad
        {
            internal int Axis, Sign, Plane, A, B, Width, Height, Color;
            internal string Chunk;
            internal Vector3Int CellAt(int a, int b) { int p = Plane - (Sign > 0 ? 1 : 0); return Axis == 0 ? new Vector3Int(p, a, b) : Axis == 1 ? new Vector3Int(a, p, b) : new Vector3Int(a, b, p); }
            internal Vector3 PositionAt(int a, int b) { return Axis == 0 ? new Vector3(Plane, b, a) : Axis == 1 ? new Vector3(a, b, Plane) : new Vector3(a, Plane, b); }
        }
        private struct UnitFace : IEquatable<UnitFace>
        {
            private readonly Vector3Int point; private readonly int face;
            internal UnitFace(Vector3Int point, int face) { this.point = point; this.face = face; }
            public bool Equals(UnitFace other) { return point == other.point && face == other.face; }
            public override bool Equals(object obj) { return obj is UnitFace other && Equals(other); }
            public override int GetHashCode() { unchecked { return point.GetHashCode() * 31 + face; } }
        }
    }
}
