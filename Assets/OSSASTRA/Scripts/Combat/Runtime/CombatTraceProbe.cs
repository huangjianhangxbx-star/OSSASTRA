using System;
using Ossastra.CharacterCore;
using UnityEngine;

namespace Ossastra.Combat
{
    // 观察正式查询与过滤。只有选定对象的活动调试会话会接收快照。
    internal interface ICombatTraceSink
    {
        bool Capturing { get; }
        bool CaptureQueries { get; }
        bool KnowsExecution(Guid id);
        void InputEdge(string command, string device, int deviceId, string control, long gesture,
            bool pressed, bool released, bool captured, double deviceSeconds);
        void Note(string track, string label, string detail, Guid action = default,
            Guid parent = default, int emission = -1, string target = "", double deviceSeconds = -1);
        void Query(AttackRelease release, string shape, Vector3 start, Vector3 end, Vector3 direction,
            float radius, float angle, int mask, int count, string triggers, float duration = 0, float elapsed = 0,
            Vector3? sectorOrigin = null);
    }

    internal static class CombatTraceProbe
    {
        internal static Component Owner;
        internal static ICombatTraceSink Sink;

        internal static ICombatTraceSink ForExecution(Component target, Guid execution)
        {
            var sink = Sink;
            return sink != null && sink.Capturing && target && Owner && target.gameObject.scene == Owner.gameObject.scene &&
                sink.KnowsExecution(execution) ? sink : null;
        }

        internal static ICombatTraceSink For(Component component, bool query = false)
        {
            var sink = Sink;
            return sink != null && sink.Capturing && (!query || sink.CaptureQueries) &&
                Owner != null && component != null && component.gameObject == Owner.gameObject ? sink : null;
        }
    }
}
