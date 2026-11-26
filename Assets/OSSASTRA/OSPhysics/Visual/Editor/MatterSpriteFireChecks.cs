// 职责：用真实物质燃烧与实际火焰批次验证刷新、只读、熄火和释放。
using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ossastra.OSPhysics.Editor
{
    public static class MatterSpriteFireChecks
    {
        public static string Verify()
        {
            var root = new GameObject("OSPhysics 火焰验证");
            var snapshot = new Texture2D(34, 17, TextureFormat.RGBA32, false, true);
            MatterSpriteFire fire = null;
            try
            {
                var world = new MatterWorld(34, 17, 7);
                Require(MatterSpriteFire.TryCreate(root.transform, world, snapshot, Vector2.zero, .1f, out fire),
                    "完整火图批次尚未接通。");
                fire.Present(true, 0, null, Color.white, 1);
                Require(fire.ActiveCount == 0, "无燃料时应该没有火片。");

                world.Add(1, 1, Matter.Wood, 255);
                world.Add(32, 1, Matter.Wood, 255);
                world.AddHeat(1, 1, 850);
                world.AddHeat(32, 1, 850);
                for (int i = 0; i < 3; i++) world.Step();
                Require(world.TrySnapshot(1, 1, out var left) && left.Burning &&
                    world.TrySnapshot(32, 1, out var right) && right.Burning, "验证夹具未正常着火。");

                fire.Present(true, .1f, null, Color.white, 1);
                Require(fire.ActiveCount == 2, "两个隔开的真实火源需要两张完整火片。");
                var mesh = root.GetComponentInChildren<MeshFilter>().sharedMesh;
                var vertices = mesh.vertices;
                var masses = world.GetLedger(Matter.Wood);
                int tick = world.Tick;

                fire.Present(false, .1f, null, Color.white, 1);
                Require(fire.ActiveCount == 0, "切到诊断显示后应隐藏火图。");
                fire.Present(true, .1f, null, Color.white, 1);
                Require(fire.ActiveCount == 2 && vertices.Length == mesh.vertexCount && vertices[0] == mesh.vertices[0],
                    "暂停显示切换不应重抽火片位置。");
                var after = world.GetLedger(Matter.Wood);
                Require(world.Tick == tick && masses.Current == after.Current && masses.Reacted == after.Reacted,
                    "显示刷新不应推进真实物质或增减燃料。");

                world.Remove(1, 1, 255);
                fire.Present(true, .1f, null, Color.white, 1);
                Require(fire.ActiveCount == 1, "擦除左火源后应该只保留右火源。");
                world.ClearDynamic();
                fire.Present(true, .1f, null, Color.white, 1);
                Require(fire.ActiveCount == 0, "清空物质后应立刻撤下整批火图。");
                return "火片验证通过：空场、跨块隔离、同帧显示切换、只读、擦除及清空。";
            }
            finally
            {
                fire?.Dispose();
                Object.DestroyImmediate(snapshot);
                Object.DestroyImmediate(root);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}