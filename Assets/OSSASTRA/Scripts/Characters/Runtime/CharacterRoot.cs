using UnityEngine.Serialization;
using Ossastra.Development;
using System;
using Ossastra.CharacterCore;
using UnityEngine;

namespace Ossastra.Characters
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(CharacterMovement), typeof(CharacterMeleeHit), typeof(CharacterInputHandler))]
    [RequireComponent(typeof(CharacterOrientation))]
    public sealed class CharacterRoot : MonoBehaviour
    {
        [SerializeField] private string actorId = "player";
        [FormerlySerializedAs("motor")]
        [SerializeField] private CharacterMovement movement;
        [SerializeField] private CharacterOrientation orientation;
        [SerializeField] private CharacterSpineView view;
        [SerializeField] private CharacterMeleeHit melee;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private CombatTestTarget debugTarget;
        [Header("Prototype timing — not final balance")]
        [SerializeField] private float releaseAt = .3f, moveReadyAt = .5f, attackReadyAt = .8f, finishAt = 1.1f;
        [SerializeField, Min(1)] private int damage = 12;
        [SerializeField] private bool verboseTrace;
        private readonly ControlAuthority authority = new ControlAuthority();
        private readonly AttackIntent intent = new AttackIntent();
        [SerializeField] private CharacterInputHandler input;
        private readonly object debugOwner = new object();
        private BasicActionRuntime runtime;
        private BasicAttackDefinition definition;
        private bool ready, focused = true, paused, applicationPaused, debugBlocked;
        private string lastFact = "Idle";
        private static readonly Rect debugPanel = new Rect(12, 12, 400, 270);
        public CharacterDataBus DataBus { get; private set; }
        public string LastFact => lastFact;
        public bool IsPaused => paused || applicationPaused;
        public bool ActionActive => DataBus != null && DataBus.ActionActive;

        private void Awake()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            if (movement == null || orientation == null || view == null || melee == null || viewCamera == null || input == null || !input.IsConfigured)
            { Debug.LogError("Character requires registered input/movement/orientation/view/melee/camera, configured actions and valid Spine clips", this); enabled = false; return; }
            try { definition = new BasicAttackDefinition(releaseAt, moveReadyAt, attackReadyAt, finishAt, damage); }
            catch (ArgumentException e) { Debug.LogError("Character invalid timing: " + e.Message, this); enabled = false; return; }
            runtime = new BasicActionRuntime(actorId);
            DataBus = new CharacterDataBus(actorId, input, movement, orientation, runtime, authority);
            movement.Initialize(DataBus);
            melee.Initialize(DataBus);
            if (!view.Initialize(DataBus)) { Debug.LogError("Character requires valid Spine clips", this); enabled = false; return; }
            ready = true;
        }

        private void Update()
        {
            if (!ready) return;
            input.Read();
            var frame = DataBus.Input;
            if (frame.debugCommand != 0) DebugCommand(frame.debugCommand);
            if (frame.pause) { paused = !paused; intent.SuppressUntilRelease(); }
            if (frame.cancel) Interrupt("player-cancel");
            if (frame.hasDirectionalAim && focused)
                orientation.OrientTowards(transform.position + new Vector3(frame.aimDirection.x, 0, frame.aimDirection.y));
            else if (frame.hasPointer && focused)
            {
                var plane = new Plane(Vector3.up, transform.position);
                var ray = viewCamera.ScreenPointToRay(frame.pointer);
                if (plane.Raycast(ray, out var distance)) orientation.OrientTowards(ray.GetPoint(distance));
            }
            var overDebug = frame.hasPointer && debugPanel.Contains(new Vector2(frame.pointer.x, Screen.height - frame.pointer.y));
            if (intent.Observe(frame.attackHeld) && focused && !overDebug)
            {
                var request = runtime.Request(definition, "player-input", authority, IsPaused);
                if (request.PreviousTerminal != null) Observe(request.PreviousTerminal);
                if (request.Accepted) Observe(request.Fact);
                else lastFact = "Rejected: " + request.Reason;
            }
            foreach (var fact in runtime.Advance(Time.deltaTime, IsPaused || !focused))
            {
                Observe(fact);
                if (fact.Release != null) melee.Resolve(fact.Release);
            }
            var canMove = focused && !IsPaused && DataBus.AllowsMovement && DataBus.MoveReady;
            movement.Tick(canMove, IsPaused ? 0 : Time.deltaTime);
            view.SetPaused(IsPaused || !focused);
            view.Present();
        }

        private void Observe(ActionFact fact)
        {
            lastFact = fact.Kind + " @" + fact.At.ToString("F3") + " " + fact.ActionId.ToString("N").Substring(0, 8);
            if (fact.Reason != null) lastFact += " " + fact.Reason;
            if (verboseTrace) Debug.Log("Character " + lastFact, this);
        }

        private void Interrupt(string reason)
        {
            intent.SuppressUntilRelease();
            if (runtime == null) return;
            var terminal = runtime.Cancel(reason);
            if (terminal != null) Observe(terminal);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            focused = hasFocus;
            if (!hasFocus) Interrupt("focus-lost");
            else intent.SuppressUntilRelease();
        }
        private void OnApplicationPause(bool value)
        { applicationPaused = value; if (value) intent.SuppressUntilRelease(); }
        private void OnDisable() { Interrupt("disabled"); if (ready && view != null) view.SetPaused(false); }
        private void OnEnable() { intent.SuppressUntilRelease(); }

        public void DebugCommand(int command)
        {
            if (!ready) return;
            if (command == 1 && debugTarget != null)
            {
                Interrupt("debug-near-target");
                debugTarget.RestoreHealth();
                movement.Teleport(debugTarget.transform.position + Vector3.left * 1.15f + Vector3.up * .03f);
                orientation.OrientTowards(debugTarget.transform.position);
            }
            if (command == 2)
            {
                debugBlocked = !debugBlocked;
                if (debugBlocked) { authority.Block(debugOwner, ControlChannel.Attack); Interrupt("debug-attack-block"); }
                else authority.Release(debugOwner);
            }
            if (command == 3)
            {
                Interrupt("debug-clear");
                authority.Release(debugOwner);
                debugBlocked = false;
                paused = false;
                if (debugTarget != null) debugTarget.RestoreHealth();
            }
            if (command == 4)
                Debug.Log("Character state active=" + runtime.IsActive + " moveReady=" + runtime.MoveReady +
                    " attackReady=" + runtime.AttackReady + " paused=" + IsPaused + " last=" + lastFact, this);
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(debugPanel, GUI.skin.box);
            GUILayout.Label("角色控制候选 · WASD移动 / 鼠标左键单普攻 / X取消 / P暂停");
            GUILayout.Label("动画: " + (view == null ? "missing" : view.CurrentClip) + " | " + lastFact);
            GUILayout.Label("命中: " + (melee == null ? "missing" : melee.LastResult));
            GUILayout.Label("暂停: " + IsPaused + " | 禁攻: " + debugBlocked);
            if (GUILayout.Button("F1 合法状态：恢复靶HP并站到攻击距离")) DebugCommand(1);
            if (GUILayout.Button("F2 非法状态：切换具名禁攻")) DebugCommand(2);
            if (GUILayout.Button("F3 清理：取消动作／解除禁攻／恢复靶")) DebugCommand(3);
            if (GUILayout.Button("F4 检查：输出当前领域状态")) DebugCommand(4);
            GUILayout.EndArea();
        }
    }
}
