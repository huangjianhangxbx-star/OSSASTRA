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
    }
}
