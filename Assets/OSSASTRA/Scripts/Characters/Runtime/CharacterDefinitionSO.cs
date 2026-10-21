/* 脚本：CharacterDefinitionSO
 * 功能：角色基础动作库与表现配置引用；双角色可共享定义但独立执行。
 * 边界：不持有HP、体力、动作、耐久与CD；SO替换在初始化时生效。
 */
using System;
using UnityEngine;
namespace Ossastra.Characters
{
    [CreateAssetMenu(menuName="OSSASTRA/Characters/Character Definition")]
    public sealed class CharacterDefinitionSO:ScriptableObject
    {
        public string definitionId,displayName;
        public CharacterAnimationProfileSO animationProfile;
        public ActionDefinitionSO[] basicAttacks=Array.Empty<ActionDefinitionSO>();
        [Min(.01f)] public float dodgeDuration=.45f,dodgeDistance=2.4f;
        [Min(0)] public float dodgeCost=22,invulnerableFrom=.12f,invulnerableTo=.32f;
        [Min(0)] public float preInput=.12f,bufferLifetime=.17f,continuationWindow=.45f;
    }
}
