/* 功能：声明实验资产画笔与地编求解器的中立边界。
 * 负责：传递源图、裁区、实例锚点和已解算的绘制片。Unity 物质宿主管理实例与阻挡。 */
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ossastra.OSPhysics
{
    public static class MatterAssetTerrain
    {
        // 默认程序集在启动时注册窄适配器，实验室不直接引用地编程序集。
        public static Func<IMatterAssetTerrainKit> Load { get; set; }
    }

    public interface IMatterAssetTerrainKit : IDisposable
    {
        string Name { get; }
        string Status { get; }
        float CellSize { get; }
        IReadOnlyList<MatterAssetStamp> Grass { get; }
        IReadOnlyList<MatterAssetStamp> Wood { get; }

        /// <summary>使用作者格实体求解图件，返回相对作者原点的世界单位坐标。输出列表由调用方复用。</summary>
        void Solve(IReadOnlyCollection<Vector2Int> blocks, List<MatterAssetQuad> output);
    }

    public readonly struct MatterAssetStamp
    {
        public const int FuelCoverageSize = 48;
        private readonly byte[] fuelCoverage;
        public readonly string Name;
        public readonly Texture2D Image;
        public readonly Rect UV, Pixels, Fuel;
        public readonly Vector2 Size, Pivot;
        public readonly bool Flip, AttachToGround;

        /// <summary>Pivot、Fuel 相对未翻转裁片归一化，Size 使用世界单位。Flip 表示地编允许水平镜像。</summary>
        public MatterAssetStamp(string name, Texture2D image, Rect uv, Rect pixels, Vector2 size,
            Vector2 pivot, bool flip, bool attachToGround, Rect fuel = default, byte[] coverage = null)
        {
            Name = name;
            Image = image;
            UV = uv;
            Pixels = pixels;
            Size = size;
            Pivot = pivot;
            Flip = flip;
            AttachToGround = attachToGround;
            Fuel = fuel;
            fuelCoverage = coverage;
        }

        /// <summary>物质只进入标定范围内的可见主体，保持枝叶之间的透空。</summary>
        public bool ContainsFuel(Vector2 sourcePoint)
        {
            if (!Fuel.Contains(sourcePoint)) return false;
            if (fuelCoverage == null) return true;
            int x = Mathf.Clamp(Mathf.FloorToInt(sourcePoint.x * FuelCoverageSize), 0, FuelCoverageSize - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(sourcePoint.y * FuelCoverageSize), 0, FuelCoverageSize - 1);
            return fuelCoverage[x + y * FuelCoverageSize] >= 96;
        }
    }

    public readonly struct MatterAssetQuad
    {
        public readonly Texture2D Image;
        public readonly Vector2 A, B, C, D;
        public readonly Rect UV;
        public readonly Color Tint;
        public readonly int Order;
        public readonly byte FadeVertices;
        public readonly string Source, Slot;
        public readonly bool FlipX, FlipY;

        /// <summary>A 到 D 按左下、左上、右上、右下的源顶点绕序；FadeVertices 对应置零的顶点 Alpha。</summary>
        public MatterAssetQuad(Texture2D image, Vector2 a, Vector2 b, Vector2 c, Vector2 d,
            Rect uv, Color tint, int order, byte fadeVertices, string source, string slot,
            bool flipX, bool flipY)
        {
            Image = image;
            A = a;
            B = b;
            C = c;
            D = d;
            UV = uv;
            Tint = tint;
            Order = order;
            FadeVertices = fadeVertices;
            Source = source;
            Slot = slot;
            FlipX = flipX;
            FlipY = flipY;
        }
    }
}
