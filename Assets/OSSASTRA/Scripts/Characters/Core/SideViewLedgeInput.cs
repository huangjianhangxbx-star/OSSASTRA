/*
 * 横版攀越持续意图。只采样当前有效玩家方向与来源，挂边几何不能改变控制代际。
 */
namespace Ossastra.CharacterCore
{
    public enum LedgeInputCommand { None, Climb, Drop }

    public sealed class SideViewLedgeInput
    {
        private int controlRevision, deviceId, heldDirection;
        private bool controlled, suppressed;

        public bool HasControl => controlled;
        public int HeldDirection => heldDirection;
        public int Direction => controlled && !suppressed ? heldDirection : 0;
        public int Revision { get; private set; }

        public void Observe(int direction, int revision, bool playerControl, int movementDeviceId)
        {
            if (!playerControl) direction = 0;
            bool controlChanged = controlled != playerControl || controlRevision != revision;
            bool deviceChangedWhileHeld = playerControl && heldDirection != 0 && direction != 0 &&
                deviceId != movementDeviceId;
            if (controlChanged || deviceChangedWhileHeld)
                suppressed = direction != 0;
            else if (heldDirection != direction)
                suppressed = false;

            if (controlChanged || deviceChangedWhileHeld || heldDirection != direction)
                Revision++;
            controlled = playerControl;
            controlRevision = revision;
            deviceId = movementDeviceId;
            heldDirection = direction;
        }

        public void Suspend() => Observe(0, controlRevision, false, -1);

        public LedgeInputCommand ForWall(int side)
        {
            int current = Direction;
            if (current == 0 || side == 0) return LedgeInputCommand.None;
            return current == side ? LedgeInputCommand.Climb : LedgeInputCommand.Drop;
        }
    }
}
