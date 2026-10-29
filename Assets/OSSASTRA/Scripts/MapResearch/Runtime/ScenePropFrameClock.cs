/*
 * 脚本：ScenePropFrameClock
 * 功能：按完整合成帧的独立时长寻找当前物件帧，不将图集小格误当单独素材。
 * 边界：没有设备/材质/文件操作；循环回绕与停止末帧均使用实际duration，不假设固定FPS。
 */
using System;
using UnityEngine;

namespace Ossastra.MapResearch
{
    [Serializable]
    public struct ScenePropFrame
    {
        public Rect uv;
        public float durationSeconds;
    }

    public static class ScenePropFrameClock
    {
        public static int ResolveFrame(ScenePropFrame[] frames, double elapsed, bool loop)
        {
            if (frames == null || frames.Length == 0 || double.IsNaN(elapsed) || double.IsInfinity(elapsed) || elapsed < 0d) return 0;
            double total = 0d;
            foreach (ScenePropFrame frame in frames)
            {
                if (float.IsNaN(frame.durationSeconds) || float.IsInfinity(frame.durationSeconds) || frame.durationSeconds <= 0f) return 0;
                total += frame.durationSeconds;
            }
            if (loop) elapsed %= total;
            else if (elapsed >= total) return frames.Length - 1;
            double end = 0d;
            for (int i = 0; i < frames.Length; i++)
            {
                end += frames[i].durationSeconds;
                if (elapsed < end) return i;
            }
            return frames.Length - 1;
        }
    }
}
