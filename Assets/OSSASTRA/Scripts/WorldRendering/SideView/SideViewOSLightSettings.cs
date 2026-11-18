using UnityEngine;

namespace Ossastra.WorldRendering.SideView
{
    // 一个候选文件只拥有一个正式角色或共同成片设置。Play 内仅编辑临时副本。
    public sealed class SideViewOSLightSettings : ScriptableObject
    {
        public float midtone = 1, environmentTint = .32f, highlightProtection = .65f;
        public bool innerEnabled = true, outerEnabled = true;
        public float innerWidth = 2.5f, innerStrength = .8f, outerWidth = 3.8f, outerStrength = .48f;
        public bool bloom = true, grade = true, heroLight;
    }
}
