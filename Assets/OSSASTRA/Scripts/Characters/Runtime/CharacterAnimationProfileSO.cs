/* 脚本：CharacterAnimationProfileSO
 * 功能：语义/内容键到Spine片段的配置映射，供角色换模型而不改玩法分支。
 * 边界：只存定义；播放身份和轨道由本体View持有，具体片段名不进入战斗资格。
 */
using System;
using System.Linq;
using UnityEngine;
using Ossastra.AnimationCore;
namespace Ossastra.Characters
{
    [Serializable]
    public sealed class CharacterAnimationBinding
    {
        public string key,clip;
        public AnimationSemantic semantic;
        public bool loop;
        [Min(0)] public float mixDuration=.08f,sourceDuration=1;
    }
    [CreateAssetMenu(menuName="OSSASTRA/Characters/Animation Profile")]
    public sealed class CharacterAnimationProfileSO:ScriptableObject
    {
        public CharacterAnimationBinding[] bindings=Array.Empty<CharacterAnimationBinding>();
        public CharacterAnimationBinding FindKey(string key)=>bindings.FirstOrDefault(b=>b!=null&&b.key==key);
        public CharacterAnimationBinding FindSemantic(AnimationSemantic semantic)=>bindings.FirstOrDefault(b=>b!=null&&b.semantic==semantic);
    }
}
