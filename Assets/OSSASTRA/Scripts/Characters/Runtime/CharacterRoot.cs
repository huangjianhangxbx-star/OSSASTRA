/*
 * 脚本：CharacterRoot
 * 功能：组织单角色的接口接线、输入、动作事实、运动和表现。
 * 边界：持有本角色运行时；不实现设备绑定、目标HP写入或开发GUI。
 * 协作：输入→朝向→请求→推进→释放命中→运动→Spine；Bus只读访问原状态。
 */
using System;
using Ossastra.CharacterCore;
using UnityEngine;
using UnityEngine.Serialization;

namespace Ossastra.Characters
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(CharacterMovement), typeof(CharacterMeleeHit), typeof(CharacterInputHandler))]
    [RequireComponent(typeof(CharacterOrientation))]
    public sealed class CharacterRoot : MonoBehaviour
    {
        [SerializeField] private string actorId = "player";
        [FormerlySerializedAs("motor"), SerializeField] private CharacterMovement movement;
        [SerializeField] private CharacterOrientation orientation;
        [SerializeField] private CharacterSpineView view;
        [SerializeField] private CharacterMeleeHit melee;
        [SerializeField] private CharacterInputHandler input;
        [SerializeField] private Camera viewCamera;
        [Header("Prototype timing — not final balance")]
        [SerializeField] private float releaseAt = .3f, moveReadyAt = .5f, attackReadyAt = .8f, finishAt = 1.1f;
        [SerializeField, Min(1)] private int damage = 12;
        [SerializeField] private bool verboseTrace;
        private readonly ControlAuthority authority = new ControlAuthority();
        private readonly AttackIntent intent = new AttackIntent();
        private ICharacterInputSource inputSource;
        private ICharacterMovement mover;
        private ICharacterOrientation orienter;
        private ICharacterPresentation presentation;
        private ICharacterHitResolver hitResolver;
        private BasicActionRuntime runtime;
        private BasicAttackDefinition definition;
        private bool ready, focused = true, paused, applicationPaused;
        private string lastFact = "Idle";
        public CharacterDataBus DataBus { get; private set; }
        public string ActorId => actorId;
        public string LastFact => lastFact;
        public bool IsPaused => paused || applicationPaused;
        public bool ActionActive => DataBus != null && DataBus.ActionActive;

        private void Awake()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            if (movement == null || orientation == null || view == null || melee == null || input == null || viewCamera == null)
            { Debug.LogError("Character requires registered input/movement/orientation/view/melee/camera", this); enabled = false; return; }
            // Inspector保留具体组件槽；运行期通过窄接口消费，不反向依赖开发GUI或测试靶。
            inputSource = input;
            mover = movement;
            orienter = orientation;
            presentation = view;
            hitResolver = melee;
            if (!inputSource.IsConfigured) { Debug.LogError("Character actions are not configured", this); enabled = false; return; }
            try
            {
                definition = new BasicAttackDefinition(releaseAt, moveReadyAt, attackReadyAt, finishAt, damage);
                runtime = new BasicActionRuntime(actorId);
            }
            catch (ArgumentException error) { Debug.LogError("Character configuration: " + error.Message, this); enabled = false; return; }
            // 每角色独立Bus只读取原所有者；只有确需Bus的运动和表现执行初始化。
            DataBus = new CharacterDataBus(actorId, inputSource, mover, orienter, runtime, authority);
            ready = InitializeSubsystems(DataBus, mover, presentation);
            if (!ready) { Debug.LogError("Character subsystem initialization failed", this); enabled = false; }
        }

        private static bool InitializeSubsystems(CharacterDataBus dataBus, params ICharacterSubsystem[] subsystems)
        {
            foreach (var subsystem in subsystems) if (subsystem == null || !subsystem.Initialize(dataBus)) return false;
            return true;
        }

        private void Update()
        {
            if (!ready) return;
            // 受管理输入源会先检查场景许可；非实控发布空帧，但下方动作时钟仍继续。
            inputSource.Read();
            var frame = DataBus.Input;
            if (frame.pause) SetPaused(!paused);
            if (frame.cancel) CancelAction("player-cancel");
            var canMove = focused && !IsPaused && DataBus.AllowsMovement && DataBus.MoveReady;
            if (focused && !IsPaused)
                orienter.ResolveDirections(frame.move, PointerDirection(frame), frame.hasDirectionalAim ? frame.aimDirection : Vector2.zero, canMove);
            // 输入边沿不等于动作成功：焦点、UI和Runtime的暂停/权限/Ready资格分别把关。
            if (intent.Observe(frame.attackHeld) && focused && !frame.attackPointerBlocked)
            {
                var request = runtime.Request(definition, "player-input", authority, IsPaused);
                if (request.PreviousTerminal != null) Observe(request.PreviousTerminal);
                if (request.Accepted) { Observe(request.Fact); orienter.FaceAttackDirection(); }
                else lastFact = "Rejected: " + request.Reason;
            }
            // Accepted只建立动作身份；只有Released带一次性命中凭据。切人不会重发事实。
            foreach (var fact in runtime.Advance(Time.deltaTime, IsPaused || !focused))
            {
                Observe(fact);
                if (fact.Release != null)
                {
                    // 在释放当帧按值采样方向；不是接受动作时锁定，前摇中仍可重新瞄准。
                    var releaseDirection = orienter.AttackDirection;
                    orienter.FaceAttackDirection();
                    hitResolver.Resolve(fact.Release, releaseDirection);
                }
            }
            // 本帧可能跨过MoveReady；推进事实后重新查询许可，Run依据真实位移。
            canMove = focused && !IsPaused && DataBus.AllowsMovement && DataBus.MoveReady;
            mover.Tick(canMove, IsPaused || !focused ? 0 : Time.deltaTime);
            presentation.SetPaused(IsPaused || !focused);
            presentation.Present();
        }

        private Vector3? PointerDirection(CharacterInputFrame frame)
        {
            if (!frame.hasPointer) return null;
            var plane = new Plane(Vector3.up, transform.position);
            var ray = viewCamera.ScreenPointToRay(frame.pointer);
            if (!plane.Raycast(ray, out var distance)) return null;
            var direction = ray.GetPoint(distance) - transform.position;
            direction.y = 0;
            return direction.sqrMagnitude < .0001f ? (Vector3?)null : direction;
        }

        private void Observe(ActionFact fact)
        {
            lastFact = fact.Kind + " @" + fact.At.ToString("F3") + " " + fact.ActionId.ToString("N").Substring(0, 8);
            if (fact.Reason != null) lastFact += " " + fact.Reason;
            if (verboseTrace) Debug.Log("Character " + lastFact, this);
        }

        public void CancelAction(string reason)
        {
            // 取消必须具名；抑制仍按住的输入，不能把下一帧误判为新请求。
            intent.SuppressUntilRelease();
            var terminal = runtime == null ? null : runtime.Cancel(reason);
            if (terminal != null) Observe(terminal);
        }

        public void SetPaused(bool value) { paused = value; intent.SuppressUntilRelease(); }
        public void SetAttackRestriction(object owner, bool restricted, string reason = "attack-restricted")
        {
            if (restricted) { authority.Block(owner, ControlChannel.Attack); CancelAction(reason); }
            else authority.Release(owner);
        }

        private void OnApplicationFocus(bool hasFocus)
        { focused = hasFocus; if (!hasFocus) CancelAction("focus-lost"); else intent.SuppressUntilRelease(); }
        private void OnApplicationPause(bool value)
        { applicationPaused = value; if (value) intent.SuppressUntilRelease(); }
        private void OnEnable() { intent.SuppressUntilRelease(); }
        private void OnDisable() { CancelAction("disabled"); if (ready && presentation != null) presentation.SetPaused(false); }
        private void OnDestroy()
        {
            // 终止本角色动作后解除本对象引用；不清其它角色或场景控制权威。
            CancelAction("destroyed");
            ready = false;
            DataBus = null;
            runtime = null;
            inputSource = null;
            mover = null;
            orienter = null;
            presentation = null;
            hitResolver = null;
        }
    }
}
