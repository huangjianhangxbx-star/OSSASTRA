/* 同一配置的按设备克隆：保留composite/processors/interactions/override，不额外派发请求。
 * 一个消费者只读取一份合并结果；克隆不发UI点击。变更配置/设备时显式重建并等待真实释放。
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
namespace Ossastra.Characters
{
    /// <summary>一台设备的一次物理手势；按下边沿不依赖帧末是否仍保持。只作来源凭据，不接受动作。</summary>
    public readonly struct CharacterInputButton
    {
        public readonly int DeviceId;
        public readonly long GestureId;
        public readonly string Control;
        public readonly bool Held, Pressed, Released;
        public bool HasIdentity => GestureId != 0;
        public CharacterInputButton(int deviceId, long gestureId, string control, bool held, bool pressed, bool released)
        { DeviceId=deviceId; GestureId=gestureId; Control=control; Held=held; Pressed=pressed; Released=released; }
        public bool SameGesture(CharacterInputButton other)
            => HasIdentity && other.HasIdentity && DeviceId==other.DeviceId && GestureId==other.GestureId && Control==other.Control;
    }
    public sealed class DeviceActionSampler : IDisposable
    {
        public sealed class DeviceSample : IDisposable
        {
            public readonly InputDevice Device;
            private readonly InputActionMap map;
            private readonly Dictionary<InputAction, InputAction> copies = new Dictionary<InputAction, InputAction>();
            private readonly Dictionary<InputAction, bool> armed = new Dictionary<InputAction, bool>();
            private readonly Dictionary<InputAction, double> eventTimes = new Dictionary<InputAction, double>();
            private sealed class ButtonOwnership { public bool eligible; public long gesture; public string control; public InputControl initiatingControl; }
            private readonly Dictionary<InputAction, ButtonOwnership> buttons = new Dictionary<InputAction, ButtonOwnership>();
            private static long nextGesture;
            private readonly Func<Vector2, bool> blocked;
            private readonly InputAction pointer;
            private readonly InputAction orderedButton;
            private readonly Func<bool> acceptsOrderedPress;
            private readonly List<CharacterInputButton> orderedEdges = new List<CharacterInputButton>();
            private CharacterInputButton orderedHeld;
            private Vector2 previousMove, previousAim;
            private bool axesInitialized;
            private bool navigationReleaseRequired;
            public DeviceInputCandidate Candidate { get; private set; }

            internal DeviceSample(InputDevice device, InputAction[] actions, InputAction pointer, Func<Vector2, bool> blocked,
                InputAction orderedButton, Func<bool> acceptsOrderedPress)
            {
                Device = device; this.pointer = pointer; this.blocked = blocked;
                this.orderedButton = orderedButton;
                this.acceptsOrderedPress = acceptsOrderedPress;
                map = new InputActionMap("PerDeviceSampling");
                foreach (var source in actions)
                {
                    var copy = map.AddAction("Sample" + copies.Count, source.type, interactions: source.interactions,
                        processors: source.processors, expectedControlLayout: source.expectedControlType);
                    foreach (var binding in source.bindings) copy.AddBinding(binding);
                    copy.bindingMask = source.bindingMask;
                    // 有序手势必须始于实际新按下，重新启用时已有按住不算边沿。
                    copy.wantsInitialStateCheck = source != orderedButton && source.wantsInitialStateCheck;
                    copies.Add(source, copy); armed.Add(source, false);
                    var ownership=new ButtonOwnership(); buttons.Add(source,ownership);
                    copy.performed += c => eventTimes[source] = c.time;
                    if (source.type == InputActionType.Button)
                    {
                        copy.started += c => {
                            var ui=device is Pointer && blocked != null && blocked(ReadPointer());
                            ownership.eligible=armed[source]&&!ui;
                            ownership.gesture=++nextGesture; ownership.control=c.control?.path;
                            ownership.initiatingControl=c.control;
                        };
                        if (source == orderedButton)
                        {
                            copy.performed += c => BeginOrderedGesture(ownership, c);
                            copy.canceled += c => EndOrderedGesture(copy);
                        }
                        // canceled不清初始归属：同次输入更新的down/up仍必须按down时的归属判定。
                    }
                }
                map.devices = new[] { device }; map.Enable();
                if (orderedButton != null && copies.TryGetValue(orderedButton, out var orderedCopy))
                    RearmOrderedIfNeutral(orderedCopy);
            }

            private void BeginOrderedGesture(ButtonOwnership ownership, InputAction.CallbackContext context)
            {
                if (orderedHeld.HasIdentity || !ownership.eligible || !context.ReadValueAsButton() ||
                    (acceptsOrderedPress != null && !acceptsOrderedPress())) return;
                orderedHeld = new CharacterInputButton(Device.deviceId, ownership.gesture, ownership.control, true, false, false);
                orderedEdges.Add(new CharacterInputButton(Device.deviceId, ownership.gesture, ownership.control, true, true, false));
            }

            private void EndOrderedGesture(InputAction copy)
            {
                if (orderedHeld.HasIdentity)
                {
                    var lastIndex = orderedEdges.Count - 1;
                    // 一次采样前的快按快松共享同一个身份。前一个已采样的按下不在此重发。
                    if (lastIndex >= 0 && orderedEdges[lastIndex].SameGesture(orderedHeld))
                        orderedEdges[lastIndex] = new CharacterInputButton(Device.deviceId, orderedHeld.GestureId, orderedHeld.Control, false, true, true);
                    else
                        orderedEdges.Add(new CharacterInputButton(Device.deviceId, orderedHeld.GestureId, orderedHeld.Control, false, false, true));
                    orderedHeld = default;
                }
                RearmOrderedIfNeutral(copy);
            }

            private void RearmOrderedIfNeutral(InputAction copy)
            {
                // Button Action重建时尚无已解析的按下状态，直接核对配置所指向的实体控件。
                if (!copy.IsPressed() && !copy.controls.Any(control => control is ButtonControl button && button.isPressed))
                    armed[orderedButton] = true;
            }

            public void ConsumeOrderedButton(List<CharacterInputButton> destination)
            {
                if (orderedButton == null || !copies.TryGetValue(orderedButton, out var copy)) return;
                RearmOrderedIfNeutral(copy);
                if (orderedEdges.Count > 0)
                {
                    destination.AddRange(orderedEdges);
                    orderedEdges.Clear();
                }
                else if (orderedHeld.HasIdentity)
                    destination.Add(orderedHeld);
            }
            public InputAction Action(InputAction source) => source != null && copies.TryGetValue(source, out var value) ? value : null;
            public Vector2 Vector(InputAction source) => Action(source)?.ReadValue<Vector2>() ?? Vector2.zero;
            public Vector2 ReadPointer() => Vector(pointer);
            public double Time(InputAction source) => source != null && eventTimes.TryGetValue(source, out var t) ? t : -1;
            public bool Held(InputAction source)
            {
                var a = Action(source); if (a == null) return false;
                if (!a.IsPressed()) { armed[source] = true; return false; }
                var origin=buttons[source].initiatingControl as UnityEngine.InputSystem.Controls.ButtonControl;
                return armed[source] && buttons[source].eligible && (origin==null||origin.isPressed);
            }
            public bool Pressed(InputAction source)
            { var a=Action(source); if(a==null)return false; Held(source); return buttons[source].eligible&&a.WasPressedThisFrame(); }
            public CharacterInputButton Button(InputAction source)
            {
                var a=Action(source); if(a==null)return default;
                var held=Held(source);var b=buttons[source];
                return new CharacterInputButton(Device.deviceId,b.gesture,b.control,held,b.eligible&&a.WasPressedThisFrame(),b.eligible&&a.WasReleasedThisFrame());
            }
            public bool ConsumedByComposite(InputAction source)
            {
                var basic = Action(source); if (!(Device is Gamepad) || basic == null) return false;
                foreach (var pair in copies)
                {
                    if (pair.Key == source || !(pair.Value.IsPressed()||pair.Value.WasPressedThisFrame())) continue;
                    foreach (var b in pair.Value.bindings)
                        if (b.isPartOfComposite && string.Equals(b.name,"button", StringComparison.OrdinalIgnoreCase) &&
                            basic.controls.Any(control => InputControlPath.Matches(b.effectivePath, control))) return true;
                }
                return false;
            }
            public DeviceInputCandidate SampleAxes(InputAction move, InputAction aim, float threshold, float change)
            {
                var m = Vector(move); var a = Vector(aim);
                bool ma = m.sqrMagnitude >= threshold * threshold, aa = a.sqrMagnitude >= threshold * threshold;
                Candidate = new DeviceInputCandidate { DeviceId = Device.deviceId, IsGamepad = Device is Gamepad,
                    Move = ma ? m : Vector2.zero, Aim = aa ? a : Vector2.zero, MoveActive = ma, AimActive = aa,
                    MoveChanged = axesInitialized && ma && (previousMove.sqrMagnitude < threshold * threshold || (m - previousMove).sqrMagnitude >= change * change),
                    AimChanged = axesInitialized && aa && (previousAim.sqrMagnitude < threshold * threshold || (a - previousAim).sqrMagnitude >= change * change),
                    MoveTime = Time(move), AimTime = Time(aim) };
                // 保留上次有意义方向，细小逐帧变化累积到门槛仍可接管。
                if (!axesInitialized || Candidate.MoveChanged || !ma) previousMove = m;
                if (!axesInitialized || Candidate.AimChanged || !aa) previousAim = a;
                axesInitialized = true; return Candidate;
            }
            public void CaptureNavigation(bool captured)
            {
                if(captured)navigationReleaseRequired=true;
                if(!navigationReleaseRequired||Candidate==null)return;
                if(!captured&&!Candidate.MoveActive)navigationReleaseRequired=false;
                Candidate.MoveActive=Candidate.MoveChanged=false;Candidate.Move=Vector2.zero;
            }
            public void Suppress(InputAction preserve = null)
            {
                foreach (var source in copies.Keys)
                {
                    if (source == preserve) continue;
                    armed[source] = false;
                    buttons[source].eligible = false;
                }
                if (orderedButton != null && orderedButton != preserve)
                {
                    orderedEdges.Clear();
                    orderedHeld = default;
                    if (copies.TryGetValue(orderedButton, out var copy)) RearmOrderedIfNeutral(copy);
                }
                axesInitialized = false;
                previousMove = previousAim = Vector2.zero;
            }
            public void SuppressNewPresses()
            { foreach (var pair in copies) if (pair.Key != orderedButton && pair.Value.WasPressedThisFrame()) {armed[pair.Key] = false;buttons[pair.Key].eligible=false;} }
            public void Dispose() { map.Disable(); map.Dispose(); }
        }
        private readonly List<DeviceSample> devices = new List<DeviceSample>();
        private readonly InputAction orderedButton;
        private readonly Func<bool> acceptsOrderedPress;
        private string signature;
        public DeviceActionSampler() { }
        public DeviceActionSampler(InputAction orderedButton, Func<bool> acceptsOrderedPress)
        {
            this.orderedButton = orderedButton;
            this.acceptsOrderedPress = acceptsOrderedPress;
        }
        public IReadOnlyList<DeviceSample> Devices => devices;
        public void Synchronize(IEnumerable<InputAction> actions, InputAction pointer, Func<Vector2, bool> blocked)
        {
            var inputs = actions.Where(a => a != null).Distinct().ToArray();
            var available = InputSystem.devices.Where(d => d is Keyboard || d is Pointer || d is Gamepad).ToArray();
            var text = new StringBuilder();
            foreach (var a in inputs)
            {
                text.Append(a.id).Append(a.interactions).Append(a.processors).Append(a.bindingMask);
                foreach (var b in a.bindings) text.Append(b.effectivePath).Append(b.effectiveInteractions).Append(b.effectiveProcessors).Append(b.groups).Append(b.isComposite).Append(b.isPartOfComposite);
            }
            var next = text.ToString();
            if (next != signature) { Dispose(); signature = next; }
            foreach (var sample in devices.Where(s => !available.Contains(s.Device) || !s.Device.enabled).ToArray())
            { sample.Dispose(); devices.Remove(sample); }
            foreach (var d in available) if (d.enabled && !devices.Any(s => s.Device == d))
                devices.Add(new DeviceSample(d, inputs, pointer, blocked, orderedButton, acceptsOrderedPress));
        }
        public void Suppress(InputAction preserve = null) { foreach (var d in devices) d.Suppress(preserve); }
        public void SuppressNewPresses() { foreach (var d in devices) d.SuppressNewPresses(); }
        public void Dispose() { foreach (var d in devices) d.Dispose(); devices.Clear(); signature = null; }
    }
}
