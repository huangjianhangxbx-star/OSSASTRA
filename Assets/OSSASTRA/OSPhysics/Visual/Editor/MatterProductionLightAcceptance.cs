/* 功能：在现有实验场相机上验证正式灯图注册、加灯/熄灯及 Renderer 还原。
 * 边界：只在 Play 模式调用，不改变模拟，也不保存测试对象。 */
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace Ossastra.OSPhysics.Editor
{
    public static class MatterProductionLightAcceptance
    {
        public static IEnumerator Verify(Camera camera, Action<string> finish)
        {
            Require(camera != null && Application.isPlaying, "需要实验场正在渲染的 Play 相机。");
            var before = ReadRenderer(camera);
            Require(MatterProductionLight.TryCreate(camera, out var bridge, out string reason), reason);
            int activeDraws;
            int remainingDraws;
            Vector2Int lightMapSize;
            int textureCount;
            try
            {
                var origin = new Vector2(-12.8f, -6.4f);
                bridge.Refresh(new[] { new Vector4(85, 26, 22, .65f) }, origin, .1f, 1f);
                for (int i = 0; i < 15 && (!bridge.IsLive || bridge.DrawCount != 2); i++) yield return null;
                activeDraws = bridge.DrawCount;
                lightMapSize = bridge.TextureSize;
                textureCount = bridge.TextureCount;
                Require(bridge.IsLive && textureCount == 8 && lightMapSize.x > 0 && lightMapSize.y > 0,
                    "正式 Pass 未在该相机帧中发布世界/角色共八张光图。");
                Require(activeDraws == 2, "单个聚合火团和主灯应产生两次真实世界灯绘制。");
                bridge.Refresh(null, origin, .1f, 1f);
                for (int i = 0; i < 15 && bridge.DrawCount != 1; i++) yield return null;
                remainingDraws = bridge.DrawCount;
                Require(bridge.IsLive && remainingDraws == 1, "火团撤回后只应保留主灯。");
                bridge.PreviewLampEnabled = false;
                bridge.Refresh(null, origin, .1f, 1f);
                for (int i = 0; i < 15 && bridge.DrawCount != 0; i++) yield return null;
                Require(bridge.IsLive && bridge.DrawCount == 0, "关主灯后底光图应仍正常，灯绘制应归零。");
            }
            finally { bridge.Dispose(); }
            yield return null;
            yield return null;
            Require(ReadRenderer(camera) == before, "关闭试照后，实验相机的 Renderer 或附加组件未恢复。");
            Require(Shader.GetGlobalFloat("_SideViewAceLightMapsReady") < .5f,
                "实验相机的全局灯图状态未清理。");
            finish?.Invoke($"OSLight 世界灯图 {lightMapSize.x} × {lightMapSize.y}，{textureCount} 张，灯绘制 {activeDraws} → {remainingDraws} → 0，已恢复相机。");
        }

        private static int? ReadRenderer(Camera camera)
        {
            foreach (var component in camera.GetComponents<Component>())
            {
                if (!component || component.GetType().FullName !=
                    "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData") continue;
                return (int)component.GetType().GetField("m_RendererIndex",
                    BindingFlags.NonPublic | BindingFlags.Instance).GetValue(component);
            }
            return null;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
