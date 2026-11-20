#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Ossastra.Development
{
    [Serializable] public sealed class AttackLabChange
    {
        public string id;
        public float value;
    }

    // 实验预设存储来源定位、情境与字段差异。角色运行状态和正式装配由原系统管理。
    public sealed class AttackLabPreset : ScriptableObject
    {
        public int schema = 1;
        public string binding, baselineRevision, conditionJson;
        public AttackLabChange[] changes = Array.Empty<AttackLabChange>();
    }
}
#endif
