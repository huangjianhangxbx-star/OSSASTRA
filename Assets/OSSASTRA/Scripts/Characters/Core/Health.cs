/*
 * 脚本：Health
 * 功能：保存目标身份与唯一可写的当前HP，限制伤害结果不低于零。
 * 边界：不管理动画、控制权、敌人AI或正式死亡流程。
 * 协作：AttackRelease经内部ApplyDamage写入；恢复器可在开放交互前从已验证快照创建，不重播伤害/死亡。
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

        /// <summary>持久化恢复构造：只创建尚未发布的生命对象，不对已运行实体任意赋值或重播命中。</summary>
        public static Health FromSnapshot(string id, int maximum, int current)
        {
            if (current < 0 || current > maximum) throw new ArgumentOutOfRangeException(nameof(current));
            var restored = new Health(id, maximum);
            restored.Current = current;
            return restored;
        }

        /// <summary>唯一治疗入口：返回实际恢复值；正值、存活且不超过上限。</summary>
        public int Heal(int amount)
        { if(amount<=0)throw new ArgumentOutOfRangeException(nameof(amount)); if(Current<=0)return 0; var healed=Math.Min(amount,Maximum-Current);Current+=healed;return healed; }

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
