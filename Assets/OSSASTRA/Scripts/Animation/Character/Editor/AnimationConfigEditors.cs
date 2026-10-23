// 原式动画SO的正式Inspector入口：使用工程已有Odin，不退化为默认裸列表。
using Sirenix.OdinInspector.Editor;
using UnityEditor;
namespace Ossastra.CharacterAnimation.Editor
{
    [CustomEditor(typeof(AnimationPlanMappingSO))]
    public sealed class AnimationPlanMappingEditor : OdinEditor { }
    [CustomEditor(typeof(AnimationPlanOverrideRuleSetSO))]
    public sealed class AnimationPlanOverrideEditor : OdinEditor { }
    [CustomEditor(typeof(AnimationLogicRouterSO))]
    public sealed class AnimationLogicRouterEditor : OdinEditor { }
}
