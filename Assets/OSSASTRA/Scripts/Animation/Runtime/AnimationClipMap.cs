/*
 * 脚本：AnimationClipMap
 * 功能：本体可配置语义到片段映射及主轨过渡数据。
 * 边界：资源缺失由表现适配器报告与回退；不定义动作Release或伤害时刻。
 */
using System;
using Ossastra.AnimationCore;
using UnityEngine;
namespace Ossastra.AnimationPresentation
{
    [Serializable]
    public sealed class AnimationClipMap
    {
        public AnimationSemantic semantic;
        public string clip;
        public bool loop;
        [Min(0)] public float mixDuration=.08f;
    }
    public interface ICharacterAnimationFacts
    {
        void CollectFacts(bool alive, bool staggered, bool hitReaction, int generation);
    }
}
