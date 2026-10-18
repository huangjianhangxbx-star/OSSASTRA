using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Ossastra.M01
{
    public struct M01InputFrame
    {
        public Vector2 Move, Pointer;
        public bool AttackHeld, Cancel, Pause, HasPointer;
        public int DebugCommand;
    }

    // 只采集设备，不接受动作、不写HP、不播放动画。
    public sealed class M01PlayerInput
    {
        public M01InputFrame Read()
        {
            var f = new M01InputFrame();
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            var m = Mouse.current;
            if (k != null)
            {
                f.Move = new Vector2((k.dKey.isPressed || k.rightArrowKey.isPressed ? 1 : 0) -
                    (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1 : 0),
                    (k.wKey.isPressed || k.upArrowKey.isPressed ? 1 : 0) -
                    (k.sKey.isPressed || k.downArrowKey.isPressed ? 1 : 0));
                f.Cancel = k.xKey.wasPressedThisFrame;
                f.Pause = k.pKey.wasPressedThisFrame;
                f.DebugCommand = k.f1Key.wasPressedThisFrame ? 1 : k.f2Key.wasPressedThisFrame ? 2 :
                    k.f3Key.wasPressedThisFrame ? 3 : k.f4Key.wasPressedThisFrame ? 4 : 0;
            }
            if (m != null) { f.Pointer = m.position.ReadValue(); f.AttackHeld = m.leftButton.isPressed; f.HasPointer = true; }
#else
            f.Move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            f.AttackHeld = Input.GetMouseButton(0);
            f.Pointer = Input.mousePosition;
            f.HasPointer = true;
            f.Cancel = Input.GetKeyDown(KeyCode.X);
            f.Pause = Input.GetKeyDown(KeyCode.P);
            f.DebugCommand = Input.GetKeyDown(KeyCode.F1) ? 1 : Input.GetKeyDown(KeyCode.F2) ? 2 :
                Input.GetKeyDown(KeyCode.F3) ? 3 : Input.GetKeyDown(KeyCode.F4) ? 4 : 0;
#endif
            return f;
        }
    }
}
