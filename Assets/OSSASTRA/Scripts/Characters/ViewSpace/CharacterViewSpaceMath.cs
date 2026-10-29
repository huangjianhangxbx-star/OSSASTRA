/*
 * 脚本：CharacterViewSpaceMath
 * 功能：在实际相机水平基与世界XZ之间进行无状态投影，保留输入幅值。
 * 边界：不采设备、不量化连续玩法方向、不保存角色状态或修改Transform。
 * 协作：适配器验证正交基后调用；世界攻击向量只生成只读表现投影。
 */
using UnityEngine;

namespace Ossastra.Characters.ViewSpace
{
    public static class CharacterViewSpaceMath
    {
        /// <summary>只用于玩家尚未转成世界坐标的移动/手柄记忆意图；AI和鼠标世界点不经过这里。</summary>
        public static Vector2 MapInput(Vector2 input, Vector3 right, Vector3 forward)
        {
            var world = right * input.x + forward * input.y;
            return new Vector2(world.x, world.z);
        }

        /// <summary>只读观察投影，结果保留旧方向配置采用的XZ约定，不反写原世界向量。</summary>
        public static Vector3 ToPresentation(Vector3 world, Vector3 right, Vector3 forward)
            => new Vector3(Vector3.Dot(world, right), 0, Vector3.Dot(world, forward));

        public static bool IsValidBasis(Vector3 right, Vector3 forward, Quaternion rotation)
            => Finite(right.x) && Finite(right.y) && Finite(right.z) && Finite(forward.x) && Finite(forward.y) && Finite(forward.z)
                && Finite(rotation.x) && Finite(rotation.y) && Finite(rotation.z) && Finite(rotation.w)
                && Mathf.Abs(right.y) < .001f && Mathf.Abs(forward.y) < .001f
                && Mathf.Abs(right.sqrMagnitude - 1) < .002f && Mathf.Abs(forward.sqrMagnitude - 1) < .002f
                && Mathf.Abs(Vector3.Dot(right, forward)) < .001f
                && Vector3.Dot(Vector3.Cross(Vector3.up, forward), right) > .999f
                && Mathf.Abs(rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w - 1) < .002f;

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
