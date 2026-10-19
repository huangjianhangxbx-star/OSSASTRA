/*
 * 脚本：AttackIntent
 * 功能：将按住状态转换为一次按下意图，并在失焦、禁启或移交输入后要求松键重武装。
 * 边界：只维护边沿；不读设备、不判断动作资格、不释放攻击。
 * 协作：Root调用Observe／SuppressUntilRelease；每角色独立。
 */
namespace Ossastra.CharacterCore
{
    // 设备采样由Unity输入适配器负责；这里只保留按下边沿和重新武装规则。
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
