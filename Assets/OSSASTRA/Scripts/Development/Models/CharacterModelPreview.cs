/* 脚本：CharacterModelPreview
 * 功能：为独立模型试玩登记明确的攻击限制，允许原角色链继续处理移动和方向表现。
 * 边界：不采输入、不更新角色、不伪造缺失攻击动画；只持有自己的限制令牌。
 * 协作：Root先完成初始化，Start再限制攻击；组件禁用或移除只释放本组件的令牌。
 */
using PreviewCharacterRoot = Ossastra.Characters.CharacterRoot;
using UnityEngine;

namespace Ossastra.Development
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PreviewCharacterRoot))]
    public sealed class CharacterModelPreview : MonoBehaviour
    {
        [Header("独立模型试玩说明")]
        [SerializeField, TextArea]
        private string instructions = "WASD／方向键／手柄左摇杆移动，自动切换五面与镜像。骨骼数据缩放为 0.001。当前素材未提供攻击动画，因此不开放攻击。";

        private PreviewCharacterRoot owner;
        private bool started;

        public string Instructions => instructions;

        private void Start()
        {
            owner = GetComponent<PreviewCharacterRoot>();
            started = true;
            if (owner != null)
                owner.SetAttackRestriction(this, true, "model-preview-no-attack-animation");
        }

        private void OnEnable()
        {
            if (started && owner != null)
                owner.SetAttackRestriction(this, true, "model-preview-no-attack-animation");
        }

        private void OnDisable()
        {
            if (owner != null)
                owner.SetAttackRestriction(this, false);
        }
    }
}
