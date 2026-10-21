/* 脚本：HudSkinSO
 * 功能：原型HUD迁移的明确素材映射，不持有玩法状态。
 * 边界：本机引用媒体与公开代码分开；资源缺失时报告，不代画另一套风格。
 */
using UnityEngine;
namespace Ossastra.UserInterface
{
    [CreateAssetMenu(menuName="OSSASTRA/UI/HUD Skin")]
    public sealed class HudSkinSO:ScriptableObject
    {
        public Sprite barBase,barFrame,barFill,square,metalSquare,skillFrame,portraitBase,portraitFrame,portrait;
        public Sprite slash,rocket,shield,empty;
    }
}
