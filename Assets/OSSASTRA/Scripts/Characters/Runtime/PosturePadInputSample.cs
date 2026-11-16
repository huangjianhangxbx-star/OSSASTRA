using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace Ossastra.Characters
{
    // The existing device sampler owns this only for the formal gamepad layout.
    internal sealed class PosturePadInputSample : IDisposable
    {
        private static readonly PosturePadButtons[] Physical =
        {
            PosturePadButtons.X, PosturePadButtons.Y, PosturePadButtons.B, PosturePadButtons.A,
            PosturePadButtons.LeftShoulder, PosturePadButtons.RightShoulder,
            PosturePadButtons.LeftTrigger, PosturePadButtons.RightTrigger
        };

        private sealed class Gesture
        {
            public CharacterInputButton Held;
            public readonly List<SampledEdge> Pending = new List<SampledEdge>(2);
            public SampledEdge LastRead;
        }

        private readonly struct SampledEdge
        {
            public readonly CharacterInputButton Button;
            public readonly double Time;
            public readonly Vector2 Move;

            public SampledEdge(CharacterInputButton button, double time, Vector2 move)
            {
                Button = button;
                Time = time;
                Move = move;
            }
        }

        private readonly Gamepad device;
        private readonly PlayerInputLayoutSO layout;
        private readonly ButtonControl[] controls;
        private readonly InputControl<Vector2> move;
        private readonly Func<bool> acceptsPress;
        private readonly PostureInputGestures resolver = new PostureInputGestures();
        private readonly Gesture[] commands = new Gesture[(int)PostureInputCommand.QuickSlotFour + 1];
        private readonly List<PosturePadButtons> snapshots = new List<PosturePadButtons>(8);
        private readonly List<(Vector2 move, double time)> details = new List<(Vector2 move, double time)>(8);
        private readonly List<PosturePadEdge> resolved = new List<PosturePadEdge>(12);
        private PosturePadButtons physical;

        public PosturePadInputSample(Gamepad device, InputActionMap map, PlayerInputLayoutSO layout,
            InputAction moveAction, Func<bool> acceptsPress)
        {
            this.device = device;
            this.layout = layout;
            this.acceptsPress = acceptsPress;
            controls = new[]
            {
                FindControl<ButtonControl>(layout.rightHand) ?? device.buttonWest,
                FindControl<ButtonControl>(layout.skillOne) ?? device.buttonNorth,
                FindControl<ButtonControl>(layout.dodge) ?? device.buttonEast,
                FindControl<ButtonControl>(layout.jump) ?? device.buttonSouth,
                FindControl<ButtonControl>(layout.switchCharacter, "modifier") ?? device.leftShoulder,
                FindControl<ButtonControl>(layout.switchCharacter, "button") ?? device.rightShoulder,
                FindControl<ButtonControl>(layout.weaponMode, "modifier") ?? device.leftTrigger,
                FindControl<ButtonControl>(layout.weaponMode, "button") ?? device.rightTrigger
            };
            move = FindControl<InputControl<Vector2>>(moveAction);
            var buttons = map.AddAction("PosturePadPhysicalButtons", InputActionType.PassThrough);
            foreach (var control in controls) buttons.AddBinding(control.path);
            buttons.performed += change => Observe(change.time);
            if (move != null)
            {
                var axis = map.AddAction("PosturePadMoveAxis", InputActionType.PassThrough);
                axis.AddBinding(move.path);
                axis.performed += change => Observe(change.time);
            }
            for (int index = 0; index < commands.Length; index++) commands[index] = new Gesture();
            physical = ReadPhysical();
            resolver.Reset(physical);
            InputSystem.onAfterUpdate += CompleteInputUpdate;
        }

        private T FindControl<T>(InputAction action, string part = null) where T : InputControl
        {
            if (action == null) return null;
            foreach (var binding in action.bindings)
            {
                if (binding.isComposite || binding.isPartOfComposite != (part != null) ||
                    (part != null && !string.Equals(binding.name, part, StringComparison.OrdinalIgnoreCase)) ||
                    string.IsNullOrEmpty(binding.effectivePath)) continue;
                var match = InputControlPath.TryFindControl<T>(device, binding.effectivePath);
                if (match != null) return match;
            }
            return null;
        }

        private Vector2 ReadMove() => device.added && device.enabled ? move?.ReadValue() ?? Vector2.zero : Vector2.zero;

        private PosturePadButtons ReadPhysical()
        {
            if (!device.added || !device.enabled) return PosturePadButtons.None;
            PosturePadButtons value = PosturePadButtons.None;
            for (int index = 0; index < controls.Length; index++)
                if (IsHeld(controls[index].ReadValue(), controls[index].pressPointOrDefault, Physical[index]))
                    value |= Physical[index];
            if (IsHeld(ReadMove().y, InputSystem.settings.defaultButtonPressPoint, PosturePadButtons.Up))
                value |= PosturePadButtons.Up;
            return value;
        }

        private bool IsHeld(float value, float pressPoint, PosturePadButtons button)
        {
            bool held = (physical & button) != 0;
            return held
                ? value > pressPoint * InputSystem.settings.buttonReleaseThreshold
                : value >= pressPoint;
        }

        private void Observe(double time)
        {
            if (InputState.currentUpdateType == InputUpdateType.Editor) return;
            var next = ReadPhysical();
            if (next == physical) return;
            physical = next;
            snapshots.Add(next);
            details.Add((ReadMove(), time));
        }

        private void CompleteInputUpdate()
        {
            if (InputState.currentUpdateType == InputUpdateType.Editor) return;
            Observe(InputState.currentTime);
            if (snapshots.Count == 0) return;
            resolver.Resolve(snapshots, acceptsPress == null || acceptsPress(), resolved);
            foreach (var edge in resolved) Apply(edge);
            resolved.Clear();
            snapshots.Clear();
            details.Clear();
        }

        private string ControlName(PosturePadButtons button)
        {
            int index = Array.IndexOf(Physical, button);
            if (index >= 0) return controls[index].path;
            if (button == PosturePadButtons.Up) return move?.path;
            return button == (PosturePadButtons.LeftShoulder | PosturePadButtons.RightShoulder)
                ? controls[4].path + "+" + controls[5].path
                : controls[6].path + "+" + controls[7].path;
        }

        private bool Assigned(PosturePadEdge edge)
        {
            if (edge.Command == PostureInputCommand.Interact) return move != null;
            var action = layout.ActionForPostureCommand(edge.Command);
            int controlIndex = Array.IndexOf(Physical, edge.Button);
            if (controlIndex < 0) return FindControl<ButtonControl>(action, "button") != null;
            bool modified = edge.Command == PostureInputCommand.Heal || edge.Command == PostureInputCommand.SkillTwo ||
                edge.Command == PostureInputCommand.SkillThree || edge.Command >= PostureInputCommand.QuickSlotOne;
            return FindControl<ButtonControl>(action, modified ? "button" : null) == controls[controlIndex];
        }

        private void Apply(PosturePadEdge edge)
        {
            var command = commands[(int)edge.Command];
            var held = command.Held;
            var detail = details[edge.UpdateIndex];
            if (edge.Pressed)
            {
                if (!Assigned(edge)) return;
                held = new CharacterInputButton(device.deviceId, DeviceActionSampler.DeviceSample.NextGesture(),
                    ControlName(edge.Button), true, false, false);
            }
            else if (!held.HasIdentity) return;

            command.Held = edge.Pressed ? held : default;
            int last = command.Pending.Count - 1;
            if (!edge.Pressed && last >= 0 && command.Pending[last].Button.SameGesture(held))
            {
                var pending = command.Pending[last];
                var tap = new CharacterInputButton(device.deviceId, held.GestureId, held.Control, false, true, true);
                command.Pending[last] = new SampledEdge(tap, pending.Time, pending.Move);
                return;
            }
            // Input System and the player loop have different update boundaries. Keep the physical
            // edge, time and aim until the actual consumer samples it.
            var button = new CharacterInputButton(device.deviceId, held.GestureId, held.Control,
                edge.Pressed, edge.Pressed, !edge.Pressed);
            command.Pending.Add(new SampledEdge(button, detail.time, detail.move));
        }

        public CharacterInputButton ReadButton(PostureInputCommand command, bool acceptsNewPress)
        {
            var state = commands[(int)command];
            // Ability gesture recognizers observe neutral on each physical device to arm
            // before its first genuine press in a new control generation.
            var neutral = new CharacterInputButton(device.deviceId, 0, null, false, false, false);
            if (state.Pending.Count == 0) return state.Held.HasIdentity ? state.Held : neutral;
            var edge = state.Pending[0];
            state.Pending.RemoveAt(0);
            if (edge.Button.Pressed && !acceptsNewPress)
            {
                if (state.Held.SameGesture(edge.Button)) state.Held = default;
                return neutral;
            }
            state.LastRead = edge;
            return edge.Button;
        }

        public bool ConsumePress(PostureInputCommand command)
        {
            var state = commands[(int)command];
            bool pressed = false;
            foreach (var edge in state.Pending) pressed |= edge.Button.Pressed;
            state.Pending.Clear();
            return pressed;
        }

        public double EventTime(PostureInputCommand command) => commands[(int)command].LastRead.Time;

        public Vector2 MainHandPressMove => commands[(int)PostureInputCommand.MainHand].LastRead.Move;

        public void ConsumeJumps(List<CharacterInputButton> destination)
        {
            var state = commands[(int)PostureInputCommand.Jump];
            if (state.Pending.Count > 0)
            {
                foreach (var edge in state.Pending) destination.Add(edge.Button);
                state.Pending.Clear();
            }
            else if (state.Held.HasIdentity)
                destination.Add(state.Held);
        }

        public void Suppress(bool preserveJump)
        {
            CompleteInputUpdate();
            snapshots.Clear();
            details.Clear();
            physical = ReadPhysical();
            resolver.Reset(physical, preserveJump);
            for (int index = 0; index < commands.Length; index++)
            {
                if (preserveJump && index == (int)PostureInputCommand.Jump) continue;
                commands[index].Held = default;
                commands[index].Pending.Clear();
            }
        }

        public void SuppressNewPresses()
        {
            for (int index = 0; index < commands.Length; index++)
            {
                var state = commands[index];
                foreach (var edge in state.Pending)
                    if (edge.Button.Pressed && state.Held.SameGesture(edge.Button)) state.Held = default;
                state.Pending.RemoveAll(edge => edge.Button.Pressed);
            }
        }

        public void Dispose() => InputSystem.onAfterUpdate -= CompleteInputUpdate;
    }
}
