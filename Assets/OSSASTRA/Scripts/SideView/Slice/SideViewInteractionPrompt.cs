/*
 * Shows the exact focus selected by InventoryInteraction. This view owns no gameplay input.
 */
using Ossastra.Inventory;
using Ossastra.UI.GameMenu;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;
using static Ossastra.SideView.Slice.SlicePresentation;

namespace Ossastra.SideView.Slice
{
    [DisallowMultipleComponent]
    public sealed class SideViewInteractionPrompt : MonoBehaviour
    {
        [SerializeField] private InventoryInteraction source;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private GameMenuTheme theme;
        [SerializeField, Min(0)] private float worldHeight = .85f;
        private RectTransform canvas, row;
        private CanvasGroup opacity;
        private RawImage glyph, arrow;
        private Text description, key;

        public bool IsConfigured => source != null && worldCamera != null &&
            source.gameObject.scene == gameObject.scene && worldCamera.gameObject.scene == gameObject.scene;

        public void Configure(InventoryInteraction interaction, Camera camera, GameMenuTheme referenceTheme = null)
        {
            source = interaction;
            worldCamera = camera;
            theme = referenceTheme;
        }

        private void Start()
        {
            if (!IsConfigured) { enabled = false; return; }
            var font = theme != null ? theme.Font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvas = Canvas(transform, "场景交互提示 Canvas", 120, false);
            row = Node(canvas, "当前目标提示", new Rect(0, 0, 320, 50));
            row.anchorMin = row.anchorMax = Vector2.zero;
            row.pivot = new Vector2(0, .5f);
            opacity = row.gameObject.AddComponent<CanvasGroup>();
            Image(row, "暗底", new Rect(-34, -20, 295, 95), new Color(0, 0, 0, .50f), "TalkGlow").enabled =
                Art("TalkGlow") != null;
            glyph = Image(row, "键帽", new Rect(0, 3, 45, 45), Color.white);
            arrow = ArtImage(row, "推杆方向", new Rect(30, 1, 24, 24), Color.white, "DpadUp");
            key = Label(row, "绑定", new Rect(0, 0, 54, 50), "", 23, font, TextColor);
            description = Label(row, "动作", new Rect(58, 0, 262, 50), "", 30, font, TextColor,
                TextAnchor.MiddleLeft);
            Shadow(key);
            Shadow(description);
            Hide();
        }

        private void LateUpdate()
        {
            if (row == null) return;
            if (!IsConfigured || !source.TryGetFocus(out var focus))
            {
                Hide();
                return;
            }

            var point = worldCamera.WorldToScreenPoint(focus.WorldAnchor + Vector3.up * worldHeight);
            if (point.z <= 0 || !worldCamera.pixelRect.Contains(new Vector2(point.x, point.y)) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, point, null, out var local))
            {
                Hide();
                return;
            }

            var device = FindDevice(source);
            string asset = null;
            string binding = BindingLabel(source.PromptAction, device, out asset);
            bool stickUp = false;
            if (device is Gamepad && source.PromptDirectionalAction != null)
                binding = DirectionalLabel(source.PromptDirectionalAction, device, out asset, out stickUp);
            if (string.IsNullOrEmpty(binding)) { Hide(); return; }

            glyph.texture = string.IsNullOrEmpty(asset) ? null : Art(asset);
            glyph.enabled = glyph.texture != null;
            arrow.enabled = stickUp && arrow.texture != null;
            key.enabled = !glyph.enabled;
            key.text = binding;
            description.text = focus.Text;
            row.anchoredPosition = local - canvas.rect.min - new Vector2(54, 0);
            if (!row.gameObject.activeSelf) row.gameObject.SetActive(true);
            opacity.alpha = Mathf.MoveTowards(opacity.alpha, 1f, Time.unscaledDeltaTime / .14f);
        }

        private void OnDisable() => Hide();

        private void Hide()
        {
            if (row == null) return;
            opacity.alpha = 0;
            row.gameObject.SetActive(false);
        }

        private static InputDevice FindDevice(InventoryInteraction interaction)
        {
            var inputs = interaction.Coordinator.InputSources;
            var device = InputSystem.GetDeviceById(inputs.MoveDeviceId) ?? InputSystem.GetDeviceById(inputs.AimDeviceId);
            if (device is Gamepad || device is Keyboard) return device;
            return Keyboard.current != null ? Keyboard.current : (InputDevice)Gamepad.current;
        }

        private static string BindingLabel(InputAction action, InputDevice device, out string art)
        {
            art = null;
            if (action == null || device == null) return null;
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];
                if (binding.isComposite || binding.isPartOfComposite || string.IsNullOrEmpty(binding.effectivePath)) continue;
                var control = InputControlPath.TryFindControl<InputControl>(device, binding.effectivePath);
                if (control == null) continue;
                art = control.name switch
                {
                    "w" => "W",
                    "f" => "F",
                    "buttonSouth" => "A",
                    "buttonEast" => "B",
                    _ => null
                };
                return action.GetBindingDisplayString(i, InputBinding.DisplayStringOptions.DontIncludeInteractions);
            }
            return null;
        }

        private static string DirectionalLabel(InputAction action, InputDevice device, out string art, out bool stickUp)
        {
            art = null;
            stickUp = false;
            foreach (var binding in action.bindings)
            {
                if (binding.isComposite || binding.isPartOfComposite || string.IsNullOrEmpty(binding.effectivePath)) continue;
                var direction = InputControlPath.TryFindControl<InputControl<Vector2>>(device, binding.effectivePath);
                if (direction == null) continue;
                if (direction is StickControl stick && stick == ((Gamepad)device).leftStick)
                {
                    art = "LeftStickUp";
                    stickUp = true;
                }
                if (direction is DpadControl) art = "DpadUp";
                return direction.name == "leftStick" ? "LS ↑" : direction.shortDisplayName + " ↑";
            }
            return null;
        }
    }
}
