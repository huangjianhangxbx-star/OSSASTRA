/*
 * 脚本：CombinedVfxLayer
 * 功能：保存组合特效的层、局部时间窗和二维刀路。
 * 预览与正式播放共享宽度、UV 和时段计算，配置无需引用角色或玩法对象。
 */
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ossastra.Combat
{
    public enum VfxLayerKind { Ribbon, Stamp, Particles }
    public enum VfxAnchorMode { Follow, FreezeAtStart }

    [Serializable]
    public struct VfxInterval
    {
        public bool enabled;
        [Min(0)] public float start;
        [Min(0)] public float end;

        public float Progress(float seconds)
        {
            float begin = Mathf.Max(0f, start);
            float finish = Mathf.Max(begin, end);
            return finish > begin ? Mathf.Clamp01((seconds - begin) / (finish - begin))
                : seconds < begin ? 0f : 1f;
        }

        public float EndTime => enabled ? Mathf.Max(0f, start, end) : 0f;
    }

    [Serializable]
    public sealed class VfxPath
    {
        public List<Vector2> points = new List<Vector2> {
            Vector2.zero, new Vector2(.6f, 0), new Vector2(1.2f, 0), new Vector2(1.8f, 0)
        };

        public static VfxPath Line() => new VfxPath();

        public Vector2 Sample(float t)
        {
            if (!TrySegment(t, out int i, out float u)) return Vector2.zero;
            float v = 1f - u;
            return v * v * v * points[i] + 3f * v * v * u * points[i + 1]
                + 3f * v * u * u * points[i + 2] + u * u * u * points[i + 3];
        }

        public Vector2 Normal(float t)
        {
            if (!TrySegment(t, out int i, out float u)) return Vector2.up;
            float v = 1f - u;
            Vector2 tangent = v * v * (points[i + 1] - points[i])
                + 2f * v * u * (points[i + 2] - points[i + 1])
                + u * u * (points[i + 3] - points[i + 2]);
            if (tangent.sqrMagnitude < .000001f)
                tangent = Sample(Mathf.Clamp01(t + .01f)) - Sample(Mathf.Clamp01(t - .01f));
            return tangent.sqrMagnitude < .000001f ? Vector2.up : new Vector2(-tangent.y, tangent.x).normalized;
        }

        public static VfxPath FromArc(Vector2 radius, float startAngle, float endAngle, float tilt)
        {
            float turn = Mathf.Clamp(endAngle - startAngle, -360f, 360f);
            int sections = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(turn) / 90f));
            float delta = turn * Mathf.Deg2Rad / sections;
            float tangentLength = 4f / 3f * Mathf.Tan(delta / 4f);
            float tiltRadians = tilt * Mathf.Deg2Rad;
            float cos = Mathf.Cos(tiltRadians), sin = Mathf.Sin(tiltRadians);
            radius.x = Mathf.Max(.001f, Mathf.Abs(radius.x));
            radius.y = Mathf.Max(.001f, Mathf.Abs(radius.y));
            var path = new VfxPath();
            path.points.Clear();

            // 每段最多转 90 度，将旧椭圆参数烘成可独立编辑的三次曲线控制点。
            for (int section = 0; section < sections; section++)
            {
                float a = startAngle * Mathf.Deg2Rad + section * delta;
                float b = a + delta;
                Vector2 first = Point(a), last = Point(b);
                Vector2 firstControl = first + tangentLength * Derivative(a);
                Vector2 lastControl = last - tangentLength * Derivative(b);
                if (section == 0) path.points.Add(Rotate(first));
                path.points.Add(Rotate(firstControl));
                path.points.Add(Rotate(lastControl));
                path.points.Add(Rotate(last));
            }
            return path;

            Vector2 Point(float a) => new Vector2(radius.x * Mathf.Cos(a), radius.y * Mathf.Sin(a));
            Vector2 Derivative(float a) => new Vector2(-radius.x * Mathf.Sin(a), radius.y * Mathf.Cos(a));
            Vector2 Rotate(Vector2 p) => new Vector2(p.x * cos - p.y * sin, p.x * sin + p.y * cos);
        }

        public void Mirror()
        {
            for (int i = 0; i < points.Count; i++) points[i] = new Vector2(-points[i].x, points[i].y);
        }

        public void Reverse() => points.Reverse();

        private bool TrySegment(float t, out int index, out float u)
        {
            int count = points == null ? 0 : (points.Count - 1) / 3;
            if (count <= 0)
            {
                index = 0;
                u = 0f;
                return false;
            }

            float progress = Mathf.Clamp01(t) * count;
            int segment = Mathf.Min(count - 1, Mathf.FloorToInt(progress));
            index = segment * 3;
            u = progress - segment;
            return true;
        }
    }

    [Serializable]
    public sealed class VfxLayer
    {
        public string name = "笔触带";
        public bool visible = true;
        public VfxLayerKind kind;
        public Texture2D texture;
        [Min(1)] public int columns = 4, rows = 2;
        [Min(0)] public int tile;
        public int order;
        public Color color = Color.white;
        public Vector2 offset;
        public float rotation;
        public Vector2 scale = Vector2.one;
        public VfxAnchorMode anchorMode;
        public string anchor;
        [Min(0)] public float start;
        [Min(0)] public float duration = .24f;

        public bool useSharedPath = true;
        public VfxPath path = VfxPath.Line();
        [Min(0)] public float startWidth = .15f, middleWidth = .5f, endWidth = .05f;
        public float normalOffset;
        public bool flipWidthUv;
        public AnimationCurve widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
        public Vector2 size = Vector2.one;

        public VfxInterval expand = new VfxInterval { enabled = true, end = .06f };
        public VfxInterval fade = new VfxInterval { enabled = true, start = .08f, end = .24f };
        public AnimationCurve fadeCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        public VfxInterval erase;
        public bool eraseFromEnd;
        [Range(0f, 1f)] public float softness = .06f;

        [Range(0, 128)] public int count = 6;
        [Min(0)] public float emission = .04f;
        public float direction;
        [Range(0, 360)] public float spread = 60f;
        public Vector2 speed = new Vector2(1f, 3f);
        public Vector2 particleLife = new Vector2(.12f, .2f);
        public Vector2 particleSize = new Vector2(.12f, .22f);
        public float spin;
        public float gravity;
        public bool particlesAlongPath;
        public AnimationCurve particleAlpha = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        public float BeginTime => Mathf.Max(0f, start);
        public float ActiveDuration => kind == VfxLayerKind.Particles
            ? Mathf.Max(0f, emission) + Mathf.Max(0f, particleLife.x, particleLife.y)
            : Mathf.Max(0f, duration, fade.EndTime, kind == VfxLayerKind.Ribbon ? Mathf.Max(expand.EndTime, erase.EndTime) : 0f);

        public float EndTime => BeginTime + ActiveDuration;

        public float WidthAt(float u)
        {
            u = Mathf.Clamp01(u);
            float width = u <= .5f ? Mathf.Lerp(startWidth, middleWidth, u * 2f)
                : Mathf.Lerp(middleWidth, endWidth, u * 2f - 1f);
            return Mathf.Max(0f, width * (widthCurve?.Evaluate(u) ?? 1f));
        }

        public Rect AtlasRect()
        {
            int across = Mathf.Clamp(columns, 1, 256), down = Mathf.Clamp(rows, 1, 256);
            int cell = Mathf.Clamp(tile, 0, across * down - 1);
            return new Rect((float)(cell % across) / across, (float)(down - 1 - cell / across) / down,
                1f / across, 1f / down);
        }

        public Matrix4x4 LocalMatrix => Matrix4x4.TRS(offset, Quaternion.Euler(0f, 0f, rotation), new Vector3(scale.x, scale.y, 1f));
    }
}
