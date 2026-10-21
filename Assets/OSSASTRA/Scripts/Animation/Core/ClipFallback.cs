/* 片段选择纯函数：校验实际资源，缺失时只回退已存在Idle，不编造播放成功。 */
using System;
namespace Ossastra.AnimationCore
{
    public static class ClipFallback
    {
        public static string Resolve(string requested, string idle, Func<string,bool> exists)
        {
            if(!string.IsNullOrEmpty(requested) && exists(requested)) return requested;
            return !string.IsNullOrEmpty(idle) && exists(idle) ? idle : null;
        }
    }
}
