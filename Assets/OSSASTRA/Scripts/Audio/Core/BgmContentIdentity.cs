/*
 * 脚本：BgmContentIdentity
 * 功能：建立可跨配置规则比较的、无平台依赖的音乐内容身份。
 * 负责：检查内容／版本／段落／资源身份和循环边界，生成稳定、无分隔符碰撞的值键。
 * 不负责：读取 AudioClip、异步加载、播放时钟或规则优先级。
 * 关键约束：ruleId、强度和对话音量不进入此键；实际播放秒数不写回配置。
 */
using System;
using System.Globalization;

namespace Ossastra.Audio
{
    public readonly struct BgmContentIdentity : IEquatable<BgmContentIdentity>
    {
        private readonly string key;
        public string Key => key ?? string.Empty;

        private BgmContentIdentity(string value) { key = value; }

        /// <summary>loopEndSeconds=0 代表整曲结束点；实际素材长度由 Transport 在就绪后验证。</summary>
        public static bool TryCreate(string contentId, string contentVersion, string sectionId, string assetId,
            float loopStartSeconds, float loopEndSeconds, bool loop, out BgmContentIdentity identity, out string error)
        {
            identity = default;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(contentId) || string.IsNullOrWhiteSpace(contentVersion) ||
                string.IsNullOrWhiteSpace(sectionId) || string.IsNullOrWhiteSpace(assetId))
            {
                error = "音乐内容、版本、段落和资源身份均不能为空。";
                return false;
            }
            if (!IsFinite(loopStartSeconds) || !IsFinite(loopEndSeconds) ||
                loopStartSeconds < 0 || loopEndSeconds < 0 ||
                (loopEndSeconds > 0 && loopEndSeconds <= loopStartSeconds))
            {
                error = "音乐循环边界必须有限且非负，显式结束点必须晚于起点。";
                return false;
            }

            // 每个字符串使用长度前缀，避免内容自身含分隔符时碰撞；浮点格式不受电脑区域影响。
            string value = "bgm-v1|" + Part(contentId) + Part(contentVersion) + Part(sectionId) + Part(assetId) +
                (loop ? "L|" : "O|") + loopStartSeconds.ToString("R", CultureInfo.InvariantCulture) + "|" +
                loopEndSeconds.ToString("R", CultureInfo.InvariantCulture);
            identity = new BgmContentIdentity(value);
            return true;
        }

        private static string Part(string value) => value.Length.ToString(CultureInfo.InvariantCulture) + ":" + value;
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public bool Equals(BgmContentIdentity other) => string.Equals(Key, other.Key, StringComparison.Ordinal);
        public override bool Equals(object other) => other is BgmContentIdentity identity && Equals(identity);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Key);
        public override string ToString() => Key;
        public static bool operator ==(BgmContentIdentity left, BgmContentIdentity right) => left.Equals(right);
        public static bool operator !=(BgmContentIdentity left, BgmContentIdentity right) => !left.Equals(right);
    }
}
