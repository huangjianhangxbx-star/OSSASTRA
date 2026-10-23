/* 同一配置的按设备克隆：保留composite/processors/interactions/override，不额外派发请求。
 * 一个消费者只读取一份合并结果；克隆不发UI点击。变更配置/设备时显式重建并等待真实释放。
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
namespace Ossastra.Characters
{
    public sealed class DeviceActionSampler : IDisposable
    {
        public sealed class DeviceSample : IDisposable
        {
            public readonly InputDevice Device;
            private readonly InputActionMap map;
            private readonly Dictionary<InputAction, InputAction> copies = new Dictionary<InputAction, InputAction>();
            private readonly Dictionary<InputAction, bool> armed = new Dictionary<InputAction, bool>();
            private readonly Dictionary<InputAction, bool> pointerOwnedByUi = new Dictionary<InputAction, bool>();
            private readonly Dictionary<InputAction, double> eventTimes = new Dictionary<InputAction, double>();
            private readonly Func<Vector2, bool> blocked;
            private readonly InputAction pointer;
            private Vector2 previousMove, previousAim;
            private bool axesInitialized;
            private bool navigationReleaseRequired;
            public DeviceInputCandidate Candidate { get; private set; }

            internal DeviceSample(InputDevice device, InputAction[] actions, InputAction pointer, Func<Vector2, bool> blocked)
            {
                Device = device; this.pointer = pointer; this.blocked = blocked;
                map = new InputActionMap("PerDeviceSampling");
                foreach (var source in actions)
                {
                    var copy = map.AddAction("Sample" + copies.Count, source.type, interactions: source.interactions,
                        processors: source.processors, expectedControlLayout: source.expectedControlType);
                    foreach (var binding in source.bindings) copy.AddBinding(binding);
                    copy.bindingMask = source.bindingMask;
                    copy.wantsInitialStateCheck = source.wantsInitialStateCheck;
                    copies.Add(source, copy); armed.Add(source, false);
                    copy.performed += c => eventTimes[source] = c.time;
                    if (device is Pointer && source.type == InputActionType.Button)
                    {
                        copy.started += c => pointerOwnedByUi[source] = blocked != null && blocked(ReadPointer());
                        copy.canceled += c => { /* 在Read看到释放后才清归属，保留同帧关闭UI的按下。 */ };
                    }
                }
                map.devices = new[] { device }; map.Enable();
            }
            public InputAction Action(InputAction source) => source != null && copies.TryGetValue(source, out var value) ? value : null;
            public Vector2 Vector(InputAction source) => Action(source)?.ReadValue<Vector2>() ?? Vector2.zero;
            public Vector2 ReadPointer() => Vector(pointer);
            public double Time(InputAction source) => source != null && eventTimes.TryGetValue(source, out var t) ? t : -1;
            public bool Held(InputAction source)
            {
                var a = Action(source); if (a == null) return false;
                if (!a.IsPressed()) { armed[source] = true; pointerOwnedByUi.Remove(source); return false; }
                return armed[source] && !(pointerOwnedByUi.TryGetValue(source, out var ui) && ui);
            }
            public bool Pressed(InputAction source) => Held(source) && Action(source).WasPressedThisFrame();
            public bool ConsumedByComposite(InputAction source)
            {
                var basic = Action(source); if (!(Device is Gamepad) || basic == null) return false;
                foreach (var pair in copies)
                {
                    if (pair.Key == source || !pair.Value.IsPressed()) continue;
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
            public void Suppress()
            { foreach (var source in copies.Keys) armed[source] = false; axesInitialized = false; previousMove = previousAim = Vector2.zero; }
            public void SuppressNewPresses()
            { foreach (var pair in copies) if (pair.Value.WasPressedThisFrame()) armed[pair.Key] = false; }
            public void Dispose() { map.Disable(); map.Dispose(); }
        }
        private readonly List<DeviceSample> devices = new List<DeviceSample>();
        private string signature;
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
            foreach (var d in available) if (d.enabled && !devices.Any(s => s.Device == d)) devices.Add(new DeviceSample(d, inputs, pointer, blocked));
        }
        public void Suppress() { foreach (var d in devices) d.Suppress(); }
        public void SuppressNewPresses() { foreach (var d in devices) d.SuppressNewPresses(); }
        public void Dispose() { foreach (var d in devices) d.Dispose(); devices.Clear(); signature = null; }
    }
}
