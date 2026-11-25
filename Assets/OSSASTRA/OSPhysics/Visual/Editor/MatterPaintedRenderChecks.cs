// 职责：在独立的小型夹具上验证手绘层共享快照、可切换和可见性边界。
// 通过正式渲染器接口观察资产绑定，图像效果及正式灯光由实验场的实际 Game View 验收。
using System;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ossastra.OSPhysics.Editor
{
    public static class MatterPaintedRenderChecks
    {
        private const string SnapshotProperty = "_MatterState";
        private const float FrozenTime = 2f;

        /// <summary>
        /// 创建与场景中已有对象无关的短期夹具，检查同帧 A/B/C、两个主题及局部刷新。
        /// </summary>
        public static string Verify()
        {
            var world = CreateWorld();
            var control = CreateWorld();
            var cameraNode = new GameObject("手绘 QA 镜头") { hideFlags = HideFlags.DontSave };
            var isolated = new GameObject("手绘 QA 夹具") { hideFlags = HideFlags.DontSave };
            try
            {
                var camera = cameraNode.AddComponent<Camera>();
                camera.orthographic = true;
                camera.enabled = false;
                camera.transform.position = new Vector3(0, 0, -10);

                var visual = isolated.AddComponent<MatterVisual>();
                visual.Bind(world, new Vector2(-3.2f, -1.6f), .1f);
                var legacy = isolated.GetComponent<MeshRenderer>();
                var snapshot = legacy.sharedMaterial.GetTexture(SnapshotProperty) as Texture2D;
                Require(snapshot != null && snapshot.filterMode == FilterMode.Point && snapshot.mipmapCount == 1,
                    "分类快照应采用 Point 和单层 Mip。");
                Require(visual.LastUploadedChunks == 2, "两块夹具初始快照未完整上载。");

                int tick = world.Tick;
                ulong fingerprint = MatterVisualAcceptance.Fingerprint(world);
                Require(fingerprint == MatterVisualAcceptance.Fingerprint(control), "夹具初始状态与对照不一致。");
                var revisions = new[] { world.GetChunkRevision(0, 0), world.GetChunkRevision(1, 0) };
                foreach (MatterDisplay display in new[] { MatterDisplay.Grid, MatterDisplay.Natural, MatterDisplay.Heat, MatterDisplay.Collision })
                {
                    Show(visual, camera, display, MatterPaintStyle.Ruins, MatterPaintLayers.All);
                    Require(legacy.enabled && visual.LastUploadedChunks == 0, "旧诊断模式应沿用既有快照。");
                }

                Show(visual, camera, MatterDisplay.Painted, MatterPaintStyle.Ruins, MatterPaintLayers.All);
                Require(visual.PaintedReady, "手绘层没有完成初始化。" + visual.PaintedArtStatus);
                Require(!legacy.enabled && visual.PaintedPrivateSlots == 0, "原创模式应隐藏旧画面且不载入私有文件。");
                var painted = FindMatterRenderers(visual);
                VerifySharedSnapshot(painted, snapshot, FrozenTime);
                var ids = painted.Select(x => x.GetInstanceID()).ToArray();
                Color ruinsWater = painted[1].sharedMaterial.GetColor("_WaterColor");
                VaryLayers(visual, camera, painted);
                Show(visual, camera, MatterDisplay.Painted, MatterPaintStyle.Moss, MatterPaintLayers.All);
                Require(painted[1].sharedMaterial.GetColor("_WaterColor") != ruinsWater, "两个主题未产生不同的材质参数。");
                VerifySharedSnapshot(painted, snapshot, FrozenTime);

                // 反复离开及恢复手绘，不应重建材质快照和常驻分层对象。
                for (int cycle = 0; cycle < 6; cycle++)
                {
                    Show(visual, camera, MatterDisplay.Grid, MatterPaintStyle.Moss, MatterPaintLayers.All);
                    Require(legacy.enabled && painted.All(x => !x.gameObject.activeInHierarchy), "返回 A 时手绘层仍可见。");
                    Show(visual, camera, MatterDisplay.Painted, MatterPaintStyle.Moss, MatterPaintLayers.All);
                    Require(!legacy.enabled && FindMatterRenderers(visual).Select(x => x.GetInstanceID()).SequenceEqual(ids),
                        "反复切换重新分配了已有分层对象。");
                    Require(visual.LastUploadedChunks == 0 && visual.PaintedLightMaps == 0, "C 不应另上传快照或启动世界灯图。");
                }

                Require(world.Tick == tick && MatterVisualAcceptance.Fingerprint(world) == fingerprint,
                    "显示或主题切换改变了模拟 Tick、状态、热量或阻挡。");
                Require(world.GetChunkRevision(0, 0) == revisions[0] && world.GetChunkRevision(1, 0) == revisions[1],
                    "显示切换改变了物质区块修订号。");
                CompareLedger(world, control);
                Require(world.Add(60, 12, Matter.Water, 47) == 47 && control.Add(60, 12, Matter.Water, 47) == 47,
                    "单区块刷入夹具没有完整接收物质。");
                Show(visual, camera, MatterDisplay.Painted, MatterPaintStyle.Moss, MatterPaintLayers.All);
                Require(visual.LastUploadedChunks == 1, "手绘模式单区块改动应只上传一个区块。");
                VerifySharedSnapshot(painted, snapshot, FrozenTime);

                for (int step = 0; step < 8; step++)
                {
                    world.Step();
                    control.Step();
                }
                Require(world.Tick == control.Tick && MatterVisualAcceptance.Fingerprint(world) == MatterVisualAcceptance.Fingerprint(control),
                    "暂停切换后继续推进，相同输入与种子的轨迹发生分歧。");
                CompareLedger(world, control);
                return $"手绘夹具通过。A/B/C 同一快照 {snapshot.GetInstanceID()}，4 个动态批次、2 套主题、6 次来回切换，单区块上传 1；继续 8 步与无渲染对照一致。";
            }
            finally
            {
                DestroyFixture(isolated);
                DestroyFixture(cameraNode);
            }
        }

        /// <summary>读取现有实验场的真实材质绑定，不切换模式和世界。</summary>
        public static string VerifyLiveBindings(MatterVisual visual)
        {
            Require(visual != null && visual.PaintedReady, "请传入已经打开手绘的实验场渲染器。");
            var legacy = visual.GetComponent<MeshRenderer>();
            Require(legacy != null && !legacy.enabled, "手绘开启时旧的整屏材质仍启用。");
            var snapshot = legacy.sharedMaterial.GetTexture(SnapshotProperty) as Texture2D;
            Require(snapshot != null, "实验场快照纹理未绑定。");
            VerifySharedSnapshot(FindMatterRenderers(visual), snapshot);
            return $"现有场景的四个动态层共用状态贴图 {snapshot.width}×{snapshot.height}。";
        }

        private static MatterWorld CreateWorld()
        {
            var world = new MatterWorld(64, 32, 28491);
            for (int x = 0; x < world.Width; x++)
                world.SetFixed(x, 0, true);
            world.SetBarrier(31, 4, true);
            world.Add(30, 3, Matter.Water, 170);
            world.Add(32, 3, Matter.Oil, 85);
            world.Add(40, 2, Matter.Grass, 180);
            world.Add(41, 2, Matter.Wood);
            world.AddHeat(40, 2, 720);
            for (int step = 0; step < 3; step++)
                world.Step();
            Require(world.Metrics.BurningCells > 0 && world.LastReactions.SmokeProduced > 0,
                "夹具未覆盖实际的燃烧和烟状态。");
            return world;
        }

        private static void Show(MatterVisual visual, Camera camera, MatterDisplay display,
            MatterPaintStyle style, MatterPaintLayers layers)
        {
            visual.Refresh(display, FrozenTime);
            visual.PresentPainted(camera, display, style, false, layers, FrozenTime, 1f, 1f, 1f, 0f);
        }

        private static void VaryLayers(MatterVisual visual, Camera camera, MeshRenderer[] painted)
        {
            var scenery = visual.GetComponentsInChildren<MeshRenderer>(true);
            Require(scenery.Any(r => r.enabled && r.sharedMaterial.renderQueue == 2900) &&
                    scenery.Any(r => r.enabled && r.sharedMaterial.renderQueue >= 3040),
                "全层模式缺少默认远景或前景。");

            Show(visual, camera, MatterDisplay.Painted, MatterPaintStyle.Ruins, 0);
            Require(!scenery.Any(r => r.enabled && (r.sharedMaterial.renderQueue == 2900 || r.sharedMaterial.renderQueue >= 3040)),
                "关闭远景或前景时仍提交了对应渲染器。");
            Material material = painted[0].sharedMaterial;
            Vector4 none = material.GetVector("_LayerVisibility");
            Require(none.x == 0 && none.y == 0, "隐藏地形未清除主体和饰边材质位。");

            Show(visual, camera, MatterDisplay.Painted, MatterPaintStyle.Ruins, MatterPaintLayers.Trim);
            Vector4 trim = material.GetVector("_LayerVisibility");
            Require(trim.x == 0 && trim.y == 1, "独显饰边改变了主体的显示位。");
            Require(painted[1].enabled && painted[2].enabled && painted[3].enabled,
                "切换布景可见性影响了液体、燃料或烟火批次。");
        }

        private static MeshRenderer[] FindMatterRenderers(MatterVisual visual)
        {
            return visual.GetComponentsInChildren<MeshRenderer>(true)
                .Where(renderer => renderer.gameObject != visual.gameObject && renderer.sharedMaterial != null &&
                                   renderer.sharedMaterial.HasProperty("_PaintLayer"))
                .OrderBy(renderer => renderer.sharedMaterial.GetFloat("_PaintLayer"))
                .ToArray();
        }

        private static void VerifySharedSnapshot(MeshRenderer[] batches, Texture snapshot, float? frozenTime = null)
        {
            Require(batches.Length == 4, "手绘应有四个读取物质快照的动态材料批次。");
            for (int i = 0; i < batches.Length; i++)
            {
                var material = batches[i].sharedMaterial;
                Require(Mathf.Approximately(material.GetFloat("_PaintLayer"), i), "手绘批次缺少稳定的材质层号。");
                Require(material.GetTexture(SnapshotProperty) == snapshot, "手绘建立了另一份物质快照。");
                Require(!frozenTime.HasValue || Mathf.Approximately(material.GetFloat("_VisualTime"), frozenTime.Value),
                    "渲染没有使用夹具提供的固定对照时钟。");
            }
        }

        private static void CompareLedger(MatterWorld world, MatterWorld control)
        {
            foreach (Matter kind in Enum.GetValues(typeof(Matter)))
            {
                var actual = world.GetLedger(kind);
                var expected = control.GetLedger(kind);
                Require(actual.Initial == expected.Initial && actual.Injected == expected.Injected &&
                        actual.Produced == expected.Produced && actual.Removed == expected.Removed &&
                        actual.Reacted == expected.Reacted && actual.Lost == expected.Lost &&
                        actual.Current == expected.Current, kind + " 的质量账本与控制场不一致。");
            }
        }

        private static void DestroyFixture(GameObject value)
        {
            if (value == null) return;
            if (Application.isPlaying) Object.Destroy(value);
            else Object.DestroyImmediate(value);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
