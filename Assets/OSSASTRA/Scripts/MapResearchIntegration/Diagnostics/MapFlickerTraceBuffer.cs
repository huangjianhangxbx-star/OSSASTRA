/* 脚本：MapFlickerTraceBuffer
 * 功能：以固定容量保留最新记录，并提供按时间顺序排列的独立快照。
 * 边界：纯内存、按值存储；溢出覆盖最旧记录，容量只在构造时分配。
 * 协作：Recorder唯一追加；报告导出读取Snapshot，不暴露内部数组。
 */
using System;

namespace Ossastra.MapResearchIntegration.Diagnostics
{
    public sealed class MapFlickerTraceBuffer
    {
        private readonly MapFlickerTraceSample[] samples;
        private int next;
        public int Capacity => samples.Length;
        public int Count { get; private set; }
        public long Dropped { get; private set; }

        public MapFlickerTraceBuffer(int capacity)
        {
            samples = new MapFlickerTraceSample[Math.Max(1, Math.Min(4096, capacity))];
        }

        public void Append(MapFlickerTraceSample sample)
        {
            samples[next] = sample;
            next = (next + 1) % Capacity;
            if (Count < Capacity) Count++;
            else Dropped++;
        }

        public MapFlickerTraceSample[] Snapshot()
        {
            var copy = new MapFlickerTraceSample[Count];
            var oldest = Count == Capacity ? next : 0;
            for (var i = 0; i < Count; i++) copy[i] = samples[(oldest + i) % Capacity];
            return copy;
        }

        public void Clear()
        {
            Array.Clear(samples, 0, samples.Length);
            next = 0;
            Count = 0;
            Dropped = 0;
        }
    }
}
