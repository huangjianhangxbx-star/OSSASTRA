namespace Ossastra.CharacterCore
{
    // 设备采样由未来Unity适配器负责；这里只保留按下边沿和重新武装规则。
    public sealed class AttackIntent
    {
        private bool previousHeld;
        private bool armed = true;

        public bool Observe(bool held)
        {
            if (!armed)
            {
                if (!held) armed = true;
                previousHeld = held;
                return false;
            }
            var pressed = held && !previousHeld;
            previousHeld = held;
            return pressed;
        }

        // 失焦/禁用后，仍按住的键不能被误当作一次新按下。
        public void SuppressUntilRelease()
        {
            previousHeld = false;
            armed = false;
        }
    }
}
