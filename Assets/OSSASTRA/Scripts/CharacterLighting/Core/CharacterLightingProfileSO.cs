/* 功能：供多名角色复用的角色光感作者配置；三效果独立开启，不携带运行态与材质实例。
 * 生命周期：接收端Clone参数后调试；显式另存作者候选才能写资产，运行时不修改此对象。
 */
using UnityEngine;

namespace Ossastra.CharacterLighting
{
    [CreateAssetMenu(menuName = "OSSASTRA/角色/角色光感配置", fileName = "CharacterLightingProfile")]
    public sealed class CharacterLightingProfileSO : ScriptableObject
    {
        public CharacterLightingSettings settings = new CharacterLightingSettings();

        void OnValidate()
        {
            if (settings == null) settings = new CharacterLightingSettings();
            settings.Sanitize();
        }
    }
}
