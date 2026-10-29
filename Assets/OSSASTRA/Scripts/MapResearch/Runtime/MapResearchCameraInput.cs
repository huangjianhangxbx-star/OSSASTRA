/*
 * 脚本：MapResearchCameraInput
 * 功能：采集实验镜头、缩放与结构观察的可配置InputAction。
 * 边界：不读取WASD、方向键、Q/E或鼠标攻击，不生成角色Frame；只有研究根持有启用权。
 * 协作：MapResearchRoot每帧消费一次；滚轮同一采样窗口合并为至多一档，退出/失焦清空。
 */
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ossastra.MapResearch
{
    [DisallowMultipleComponent]
    public sealed class MapResearchCameraInput : MonoBehaviour
    {
        [Header("旋转：滚轮；不占用技能Q/E及角色方向键")]
        public InputAction rotateWheel = new InputAction("MapRotateWheel", InputActionType.Value, "<Mouse>/scroll/y", expectedControlType: "Axis");
        public InputAction rotateLeft = new InputAction("MapRotateLeft", InputActionType.Button, "<Gamepad>/dpad/left");
        public InputAction rotateRight = new InputAction("MapRotateRight", InputActionType.Button, "<Gamepad>/dpad/right");
        [Header("缩放与恢复（可以在Inspector更改绑定）")]
        public InputAction zoomIn = new InputAction("MapZoomIn", InputActionType.Button, "<Keyboard>/pageUp");
        public InputAction zoomOut = new InputAction("MapZoomOut", InputActionType.Button, "<Keyboard>/pageDown");
        public InputAction resetView = new InputAction("MapResetView", InputActionType.Button, "<Keyboard>/home");
        [Header("结构观察：冻结正常显示基，用另一相机看纸片")]
        public InputAction toggleStructure = new InputAction("MapStructure", InputActionType.Button, "<Keyboard>/f6");
        public InputAction sideView = new InputAction("MapSideView", InputActionType.Button, "<Keyboard>/f7");
        public InputAction topView = new InputAction("MapTopView", InputActionType.Button, "<Keyboard>/f8");

        private object owner;
        private int pendingWheel;

        public readonly struct Frame
        {
            public readonly int rotation;
            public readonly float zoom;
            public readonly bool reset, structure, side, top;
            public Frame(int rotation, float zoom, bool reset, bool structure, bool side, bool top)
            {
                this.rotation = rotation; this.zoom = zoom; this.reset = reset;
                this.structure = structure; this.side = side; this.top = top;
            }
        }

        public bool Begin(object newOwner)
        {
            if (newOwner == null || (owner != null && !ReferenceEquals(owner, newOwner))) return false;
            if (owner != null) return true;
            foreach (InputAction action in Actions()) if (action == null) return false;
            owner = newOwner;
            rotateWheel.performed += OnWheel;
            foreach (InputAction action in Actions()) action.Enable();
            pendingWheel = 0;
            return true;
        }

        public void End(object oldOwner)
        {
            if (!ReferenceEquals(owner, oldOwner)) return;
            rotateWheel.performed -= OnWheel;
            foreach (InputAction action in Actions()) if (action != null) action.Disable();
            pendingWheel = 0;
            owner = null;
        }

        public Frame ReadFrame(bool blocked)
        {
            int wheel = pendingWheel;
            pendingWheel = 0;
            if (owner == null || blocked) return default;
            int buttons = (rotateRight.WasPressedThisFrame() ? 1 : 0) - (rotateLeft.WasPressedThisFrame() ? 1 : 0);
            int rotation = Mathf.Clamp(wheel + buttons, -1, 1);
            float zoom = (zoomOut.IsPressed() ? 1f : 0f) - (zoomIn.IsPressed() ? 1f : 0f);
            return new Frame(rotation, zoom, resetView.WasPressedThisFrame(), toggleStructure.WasPressedThisFrame(), sideView.WasPressedThisFrame(), topView.WasPressedThisFrame());
        }

        public void DiscardPending() => pendingWheel = 0;

        private void OnWheel(InputAction.CallbackContext context)
        {
            float delta = context.ReadValue<float>();
            if (float.IsNaN(delta) || float.IsInfinity(delta) || Mathf.Abs(delta) < 0.01f) return;
            // 鼠标驱动可能上报120等幅度；幅度不等于档数，同一帧多次回调也不排队。
            int step = delta > 0f ? 1 : -1;
            pendingWheel = Mathf.Clamp(pendingWheel + step, -1, 1);
        }

        private InputAction[] Actions() => new[] { rotateWheel, rotateLeft, rotateRight, zoomIn, zoomOut, resetView, toggleStructure, sideView, topView };
        private void OnDisable() { if (owner != null) End(owner); }
        private void OnDestroy()
        {
            if (owner != null) End(owner);
            foreach (InputAction action in Actions()) if (action != null) action.Dispose();
        }
    }
}
