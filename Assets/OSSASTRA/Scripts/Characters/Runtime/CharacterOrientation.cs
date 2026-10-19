/*
 * 脚本：CharacterOrientation
 * 功能：分别保存可见左右翻转和实际攻击方向。
 * 边界：有效鼠标优先，其次摇杆、移动，最后保留；方向有限、非零且平面归一。
 * 协作：Root解析输入并在接受／释放时要求面向攻击；Spine只读取显示翻转。
 */
using UnityEngine;

namespace Ossastra.Characters
{
    [DisallowMultipleComponent]
    public sealed class CharacterOrientation : MonoBehaviour, ICharacterOrientation
    {
        public Vector3 AttackDirection { get; private set; } = Vector3.right;
        public float VisualFacingX { get; private set; } = 1;

        public void ResolveDirections(Vector2 moveInput, Vector3? pointerDirection, Vector2 stickDirection, bool movementFacing)
        {
            var pointerValid = pointerDirection.HasValue && TrySetAttackDirection(pointerDirection.Value);
            if (!pointerValid && !TrySetAttackDirection(new Vector3(stickDirection.x, 0, stickDirection.y)))
                TrySetAttackDirection(new Vector3(moveInput.x, 0, moveInput.y));
            if (movementFacing && Mathf.Abs(moveInput.x) > .01f) VisualFacingX = Mathf.Sign(moveInput.x);
        }

        public void OrientTowards(Vector3 worldPosition) { TrySetAttackDirection(worldPosition - transform.position); }

        public void FaceAttackDirection()
        {
            if (Mathf.Abs(AttackDirection.x) > .01f) VisualFacingX = Mathf.Sign(AttackDirection.x);
        }

        private bool TrySetAttackDirection(Vector3 direction)
        {
            direction.y = 0;
            if (float.IsNaN(direction.x) || float.IsNaN(direction.z) || float.IsInfinity(direction.x) ||
                float.IsInfinity(direction.z) || direction.sqrMagnitude < .0001f) return false;
            AttackDirection = direction.normalized;
            return true;
        }
    }
}