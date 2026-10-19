/*
 * 脚本：ControlAuthority
 * 功能：按调用者身份保存移动和攻击禁令，查询多个禁令的合并结果。
 * 边界：调用者只能解除自己的禁令；不是玩家当前选中角色的全局真值。
 * 协作：Root写入自身角色权限；DataBus只读查询。
 */
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Ossastra.CharacterCore
{
    [Flags]
    public enum ControlChannel { None = 0, Move = 1, Attack = 2 }

    // 每个调用者只拥有自己的禁令；没有一个调用者可以清空别人的限制。
    public sealed class ControlAuthority
    {
        private readonly Dictionary<object, ControlChannel> blocks =
            new Dictionary<object, ControlChannel>(new OwnerComparer());

        public void Block(object owner, ControlChannel channels)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            Validate(channels);
            blocks[owner] = channels;
        }

        public void Release(object owner)
        {
            if (owner != null) blocks.Remove(owner);
        }

        public bool Allows(ControlChannel channels)
        {
            Validate(channels);
            foreach (var block in blocks.Values)
                if ((block & channels) != 0) return false;
            return true;
        }

        private static void Validate(ControlChannel channels)
        {
            if ((channels & ~(ControlChannel.Move | ControlChannel.Attack)) != 0)
                throw new ArgumentOutOfRangeException(nameof(channels));
        }

        // owner按对象身份区分，避免两个值相同的owner互相覆盖。
        private sealed class OwnerComparer : IEqualityComparer<object>
        {
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
