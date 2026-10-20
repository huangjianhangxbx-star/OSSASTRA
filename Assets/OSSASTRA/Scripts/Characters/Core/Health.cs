/*
 * 脚本：Health
 * 功能：保存目标身份与唯一可写的当前HP，限制伤害结果不低于零。
 * 边界：不管理动画、控制权、敌人AI或正式死亡流程。
 * 协作：AttackRelease经内部ApplyDamage写入；真实目标和开发重置保留同一个Health引用。
 */
using System;

namespace Ossastra.CharacterCore
{
    // HP只有本对象能够写；测试目标将来引用同一个Health，不能维护另一份真实HP。
    public sealed class Health
    {
        public string Id { get; }
        public int Maximum { get; }
        public int Current { get; private set; }

        public Health(string id, int maximum)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Target identity required", nameof(id));
            if (maximum <= 0) throw new ArgumentOutOfRangeException(nameof(maximum));
            Id = id;
            Maximum = maximum;
            Current = maximum;
        }

        internal void ApplyDamage(int damage)
        {
            if (damage <= 0) throw new ArgumentOutOfRangeException(nameof(damage));
            Current = Math.Max(0, Current - damage);
        }

        /// <summary>显式开发回放入口；不代表生产自动返场，不更换目标身份或Health引用。</summary>
        public void RestoreForDevelopment() { Current=Maximum; }

        /// <summary>生命服务经本体验证后恢复同一Health；不更换身份或重播死亡事件。</summary>
        public bool RestoreAfterDeath()
        { if(Current>0) return false; Current=Maximum; return true; }
    }
}
