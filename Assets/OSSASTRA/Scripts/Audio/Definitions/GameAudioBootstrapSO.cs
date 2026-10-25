/* 脚本：GameAudioBootstrapSO
 * 功能：为统一音频根提供明确的目录、规则与Mixer路由配置。
 * 边界：仅定义资源引用，不保存世界、声部或角色运行数据。
 */
using UnityEngine;
using UnityEngine.Audio;
namespace Ossastra.Audio
{
    [CreateAssetMenu(menuName="OSSASTRA/Audio/Bootstrap",fileName="OssastraAudioBootstrap")]
    public sealed class GameAudioBootstrapSO:ScriptableObject
    {
        public BgmRuleSetSO rules;
        public BgmMixProfileSO mixProfile;
        public SfxCatalogSO sfxCatalog;
        public AudioMixer mixer;
        public AudioMixerGroup bgmOutput;
        [Tooltip("顺序对应SfxBus枚举；本轮Voice/Ambience仅预留路由。")]
        public AudioMixerGroup[] sfxOutputs;
        [Min(1)] public int softVoices=24;
        [Min(1)] public int hardVoices=32;
        public AudioMixerGroup Route(SfxBus bus)
        {int index=(int)bus;return sfxOutputs!=null&&index>=0&&index<sfxOutputs.Length?sfxOutputs[index]:null;}
        public bool Validate(out string reason)
        {
            if(rules==null||mixProfile==null||mixer==null||bgmOutput==null||sfxCatalog==null){reason="BOOTSTRAP_REFERENCE_MISSING";return false;}
            if(bgmOutput.audioMixer!=mixer){reason="BGM_MIXER_MISMATCH";return false;}
            if(softVoices<1||hardVoices<softVoices||hardVoices>128){reason="BOOTSTRAP_BUDGET_INVALID";return false;}
            foreach(SfxBus bus in System.Enum.GetValues(typeof(SfxBus)))
                if(Route(bus)==null||Route(bus).audioMixer!=mixer){reason="BOOTSTRAP_ROUTE_MISSING:"+bus;return false;}
            return sfxCatalog.Validate(out reason);
        }
    }
}
