/*
 * 脚本：BgmTrackSO
 * 功能：以 SO 描述音乐内容、候选素材与循环政策，供仲裁和播放后端共享。
 * 边界：只保存作者配置，不保存播放进度；缺失本机素材可显式指向合法诊断回退。
 * 协作：规则引用本定义；Transport 读取内容身份、实际素材和已知节拍信息。
 * 关键约束：三层素材仅用于原创诊断，不把未知 BPM 的整曲误称为同步分轨。
 */
using UnityEngine;

namespace Ossastra.Audio
{
    [CreateAssetMenu(menuName = "OSSASTRA/Audio/BGM Track", fileName = "BgmTrack")]
    public sealed class BgmTrackSO : ScriptableObject
    {
        [Header("稳定内容身份（不含规则名）")]
        public string contentId;
        public string contentVersion = "1";
        public string sectionId = "whole";
        public string assetId;

        [Header("素材与缺失回退")]
        public AudioClip clip;
        [Tooltip("仅原创诊断素材使用的同步层；不是未知原曲的作者 stems。")]
        public AudioClip[] synchronizedLayers;
        public bool diagnosticOnly;
        public BgmTrackSO missingFallback;

        [Header("循环规格，结束秒数 0 表示整曲")]
        public bool loop = true;
        [Min(0)] public float loopStartSeconds;
        [Min(0)] public float loopEndSeconds;

        [Header("经确认的节拍；0 表示未知，不做小节同步")]
        [Min(0)] public float knownBpm;
        [Min(1)] public int beatsPerBar = 4;

        public string PlaybackKey => TryGetIdentity(out var identity, out _) ? identity.Key : string.Empty;

        public bool TryGetIdentity(out BgmContentIdentity identity, out string error)
        {
            return BgmContentIdentity.TryCreate(contentId, contentVersion, sectionId, assetId,
                loopStartSeconds, loopEndSeconds, loop, out identity, out error);
        }
    }
}
