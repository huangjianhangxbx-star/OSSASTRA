using System;
using Ossastra.CharacterCore;
using UnityEngine;

namespace Ossastra.M01
{
    [DefaultExecutionOrder(-100)]
    public sealed class M01CharacterRoot : MonoBehaviour
    {
        [SerializeField] private string actorId = "m01-player";
        [SerializeField] private M01Motor motor;
        [SerializeField] private M01SpineView view;
        [SerializeField] private M01MeleeHit melee;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private M01HealthTarget debugTarget;
        [Header("M01 temporary timing — not final balance")]
        [SerializeField] private float releaseAt = .3f, moveReadyAt = .5f, attackReadyAt = .8f, finishAt = 1.1f;
        [SerializeField, Min(1)] private int damage = 12;
        [SerializeField] private bool verboseTrace;
        private readonly ControlAuthority authority = new ControlAuthority();
        private readonly AttackIntent intent = new AttackIntent();
        private readonly M01PlayerInput input = new M01PlayerInput();
        private readonly object debugOwner = new object();
        private BasicActionRuntime runtime;
        private BasicAttackDefinition definition;
        private bool ready, focused = true, paused, applicationPaused, debugBlocked;
        private string lastFact = "Idle";
        private static readonly Rect DebugPanel = new Rect(12, 12, 400, 270);
        public string LastFact => lastFact;
        public bool IsPaused => paused || applicationPaused;
        public bool ActionActive => runtime != null && runtime.IsActive;

        private void Awake()
        {
            if (motor == null || view == null || melee == null || viewCamera == null || !view.Initialize())
            { Debug.LogError("M01 requires registered motor/view/melee/camera and valid Spine clips", this); enabled = false; return; }
            try { definition = new BasicAttackDefinition(releaseAt, moveReadyAt, attackReadyAt, finishAt, damage); }
            catch (ArgumentException e) { Debug.LogError("M01 invalid timing: " + e.Message, this); enabled = false; return; }
            runtime = new BasicActionRuntime(actorId);
            ready = true;
        }

        private void Update()
        {
            if (!ready) return;
            var frame = input.Read();
            if (frame.DebugCommand != 0) DebugCommand(frame.DebugCommand);
            if (frame.Pause) { paused = !paused; intent.SuppressUntilRelease(); }
            if (frame.Cancel) Interrupt("player-cancel");
            if (frame.HasPointer && focused)
            {
                var plane = new Plane(Vector3.up, transform.position);
                var ray = viewCamera.ScreenPointToRay(frame.Pointer);
                if (plane.Raycast(ray, out var distance)) motor.AimAt(ray.GetPoint(distance));
            }
            var overDebug = frame.HasPointer && DebugPanel.Contains(new Vector2(frame.Pointer.x, Screen.height - frame.Pointer.y));
            if (intent.Observe(frame.AttackHeld) && focused && !overDebug)
            {
                var request = runtime.Request(definition, "player-input", authority, IsPaused);
                if (request.PreviousTerminal != null) Observe(request.PreviousTerminal);
                if (request.Accepted) Observe(request.Fact);
                else lastFact = "Rejected: " + request.Reason;
            }
            foreach (var fact in runtime.Advance(Time.deltaTime, IsPaused || !focused))
            {
                Observe(fact);
                if (fact.Release != null) melee.Resolve(fact.Release, motor.Aim);
            }
            var canMove = focused && !IsPaused && authority.Allows(ControlChannel.Move) && runtime.MoveReady;
            motor.Tick(frame.Move, canMove, IsPaused ? 0 : Time.deltaTime);
            view.SetPaused(IsPaused || !focused);
            view.Present(runtime.IsActive, runtime.ActionId, motor.Moving, motor.FacingX);
        }

        private void Observe(ActionFact fact)
        {
            lastFact = fact.Kind + " @" + fact.At.ToString("F3") + " " + fact.ActionId.ToString("N").Substring(0, 8);
            if (fact.Reason != null) lastFact += " " + fact.Reason;
            if (verboseTrace) Debug.Log("M01 " + lastFact, this);
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
                motor.Teleport(debugTarget.transform.position + Vector3.left * 1.15f + Vector3.up * .03f);
                motor.AimAt(debugTarget.transform.position);
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
                Debug.Log("M01 state active=" + runtime.IsActive + " moveReady=" + runtime.MoveReady +
                    " attackReady=" + runtime.AttackReady + " paused=" + IsPaused + " last=" + lastFact, this);
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(DebugPanel, GUI.skin.box);
            GUILayout.Label("M01 本机候选 · WASD移动 / 鼠标左键单普攻 / X取消 / P暂停");
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
