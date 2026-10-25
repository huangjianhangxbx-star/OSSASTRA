/*
 * 脚本：SfxCatalogSO
 * 功能：以稳定事件ID解析短音SO，并显式拒绝重复身份及无效定义。
 * 边界：目录不播放、不缓存运行时声部；编辑器修改后每次读取均以当前配置为准。
 * 协作：统一音频根解析请求，再交给独立短音仲裁器。
 */
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ossastra.Audio
{
    [CreateAssetMenu(menuName = "OSSASTRA/Audio/SFX Catalog", fileName = "SfxCatalog")]
    public sealed class SfxCatalogSO : ScriptableObject
    {
        public SfxEventDefinitionSO[] events;

        public bool Validate(out string reason)
        {
            if (events == null || events.Length == 0) { reason = "EMPTY_CATALOG"; return false; }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < events.Length; index++)
            {
                SfxEventDefinitionSO definition = events[index];
                if (definition == null) { reason = "NULL_DEFINITION:" + index; return false; }
                if (!definition.TryValidate(out reason)) { reason = definition.eventId + ":" + reason; return false; }
                if (!ids.Add(definition.eventId)) { reason = "DUPLICATE_EVENT_ID:" + definition.eventId; return false; }
            }
            reason = "VALID";
            return true;
        }

        /// <summary>重复ID不得任意选首项；作者配置错误与未知事件分别可诊断。</summary>
        public bool TryResolve(string eventId, out SfxEventDefinitionSO definition, out string reason)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(eventId)) { reason = "INVALID_EVENT_ID"; return false; }
            if (!Validate(out reason)) return false;
            for (int index = 0; index < events.Length; index++)
            {
                if (!string.Equals(events[index].eventId, eventId, StringComparison.Ordinal)) continue;
                definition = events[index];
                reason = "RESOLVED";
                return true;
            }
            reason = "UNKNOWN_EVENT_ID:" + eventId;
            return false;
        }
    }
}
