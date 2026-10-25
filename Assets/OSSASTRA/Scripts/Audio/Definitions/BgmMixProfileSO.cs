/*
 * 脚本：BgmMixProfileSO
 * 功能：配置音乐基准 gain、对话快照候选倍数以及显式 Tick 淡变时长。
 * 边界：用户 master／music 设置由运行时混音所有者另行持有，不写回 SO；配置不直接写 AudioSource。
 * 协作：GameAudioRoot／BgmMixController 计算唯一最终音量，避免 MixerSnapshot 双重覆盖。
 */
using UnityEngine;

namespace Ossastra.Audio
{
    [CreateAssetMenu(menuName = "OSSASTRA/Audio/BGM Mix Profile", fileName = "BgmMixProfile")]
    public sealed class BgmMixProfileSO : ScriptableObject
    {
        [Header("基准与作用域快照（用户音量另存）")]
        [Range(0, 2)] public float baselineGain = 1;
        [Range(0, 2)] public float dialogueGain = .45f;
        [Header("秒级淡变，不依赖协程")]
        [Min(0)] public float crossfadeSeconds = .5f;
        [Min(0)] public float duckSeconds = .15f;
    }
}
