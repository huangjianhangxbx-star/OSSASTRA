/* 脚本：AudioContentProfileSO
 * 功能：把短音目录、真实事实语义和音乐规则组成可整体替换的内容档案。
 * 边界：只含作者数据；受限媒体档案只保存在本机忽略目录，不持有播放权。
 * 协作：GameAudioRoot先完整验证再原子应用；失败保留现有世界和曲目。
 */
using UnityEngine;
namespace Ossastra.Audio
{
 [CreateAssetMenu(menuName="OSSASTRA/Audio/Content Profile",fileName="AudioContentProfile")]
 public sealed class AudioContentProfileSO:ScriptableObject
 {
  public string profileId;
  [TextArea] public string provenance;
  public bool referenceLocal;
  public SfxCatalogSO catalog;
  public CombatSoundProfileSO combatProfile;
  public BgmRuleSetSO musicRules;
  public bool worldAudio;
  public bool acoustics;
  public int randomSeed=27027;
  public bool Validate(out string reason)
  {
   if(string.IsNullOrWhiteSpace(profileId)){reason="INVALID_PROFILE_ID";return false;}
   if(catalog==null||combatProfile==null||musicRules==null){reason="REFERENCE_CONTENT_UNAVAILABLE";return false;}
   if(!catalog.Validate(out reason)||!combatProfile.Validate(out reason))return false;
   if(musicRules.rules==null||musicRules.rules.Length==0){reason="MISSING_MUSIC_RULES";return false;}
   foreach(var rule in musicRules.rules)
   {
    if(rule==null||string.IsNullOrWhiteSpace(rule.ruleId)||!System.Enum.IsDefined(typeof(BgmRuleCondition),rule.condition)||float.IsNaN(rule.intensity)||float.IsInfinity(rule.intensity)||rule.intensity<0||rule.intensity>1){reason="INVALID_MUSIC_RULE";return false;}
    if(rule==null||rule.track==null||!rule.track.TryGetIdentity(out _,out reason)){reason="INVALID_MUSIC_TRACK";return false;}
    // 参考模式不能以missingFallback里的旧音乐冒充Bloodborne。
    if(referenceLocal&&(rule.track.clip==null||rule.track.clip.loadState==AudioDataLoadState.Failed))
    {reason="REFERENCE_MUSIC_UNAVAILABLE";return false;}
   }
   reason="VALID";return true;
  }
 }
}
