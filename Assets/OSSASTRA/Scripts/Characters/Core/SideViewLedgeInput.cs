/*
 * 挂边输入边沿。挂边子状态保存入边方向，控制代际变化时重新消费现有输入。
 * 只解释方向意图，由运动状态机决定空间资格和最终命令。
 */
namespace Ossastra.CharacterCore
{
    public enum LedgeInputCommand { None, Climb, Drop }

    public sealed class SideViewLedgeInput
    {
        private int side, heldDirection, controlRevision;
        private bool playerControl;

        public void Enter(int side, int revision)
        {
            this.side = side;
            heldDirection = side;
            controlRevision = revision;
            playerControl = true;
        }

        public LedgeInputCommand Sample(int direction, int revision, bool controlled)
        {
            bool inherited = revision != controlRevision || controlled != playerControl;
            bool changed = direction != heldDirection;
            heldDirection = direction;
            controlRevision = revision;
            playerControl = controlled;
            if (inherited || !controlled || !changed || direction == 0) return LedgeInputCommand.None;
            return direction == side ? LedgeInputCommand.Climb : LedgeInputCommand.Drop;
        }
    }
}
