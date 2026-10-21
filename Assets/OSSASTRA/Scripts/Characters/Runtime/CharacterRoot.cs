/*
 * 脚本：CharacterRoot
 * 功能：组织单角色接口接线和显式推进；动作接受/费用由本体协调器统一。
 * 边界：持有本角色运行时；只读生命资格，不实现HP写入、设备绑定或开发GUI。
 * 协作：初始化可选本体Follow/Posture；显式按输入、架势、朝向、动作、运动、表现推进。
 */
using System;
using Ossastra.CharacterCore;
using Ossastra.Combat;
using UnityEngine;
using UnityEngine.Serialization;

namespace Ossastra.Characters
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(CharacterMovement), typeof(CombatMeleeHit), typeof(CharacterInputHandler))]
    [RequireComponent(typeof(CharacterOrientation))]
    public sealed class CharacterRoot : MonoBehaviour
    {
        [SerializeField] private string actorId = "player";
        [FormerlySerializedAs("motor"), SerializeField] private CharacterMovement movement;
        [SerializeField] private CharacterOrientation orientation;
        [SerializeField] private CharacterSpineView view;
        [SerializeField] private CombatMeleeHit melee;
        [SerializeField] private CharacterInputHandler input;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private CharacterFollow follow;
        [SerializeField] private CombatVitals vitals;
        [SerializeField] private CombatPosture posture;
        private ICombatLifeState lifeState;
        private CompanionCombatBrain combatBrain;
        private CharacterStamina stamina;
        private CharacterDodge dodge;
        private CharacterEquipment equipment;
        private ShieldGuard guard;
        private CharacterOffhand offhand;
        private CombatVitals autonomousTarget;
        private bool autonomousAction;
        public event Action<ActionFact> ActionObserved;
        public double SimulationTime => runtime == null ? 0 : runtime.SimulationTime;
        public ICompanionMoveCandidate CombatMove => combatBrain;
        [Header("Prototype timing — not final balance")]
        [SerializeField] private float releaseAt = .3f, moveReadyAt = .5f, attackReadyAt = .8f, finishAt = 1.1f;
        [SerializeField, Min(1)] private int damage = 12;
        [SerializeField, Min(0)] private float postureImpact;
        [SerializeField] private bool verboseTrace;
        private readonly ControlAuthority authority = new ControlAuthority();
        private readonly AttackIntent intent = new AttackIntent();
        private ICharacterInputSource inputSource;
        private CharacterIntentSelector intentSelector;
        private ICharacterMovement mover;
        private ICharacterOrientation orienter;
        private ICharacterPresentation presentation;
        private ICombatHitResolver hitResolver;
        private BasicActionRuntime runtime;
        private BasicAttackDefinition definition;
        private BasicAttackDefinition secondDefinition;
        private BasicChainState chain;
        private BasicChainProfile chainProfile;
        private ICombatHitReceiptSource hitReceipts;
        public int AttackStage { get; private set; }
        public int ReleaseCount { get; private set; }
        public bool HasBufferedAttack => chain!=null && chain.HasBuffer;
        public double ComboWindowUntil => chain==null?-1:chain.WindowUntil;
        private bool ready, focused = true, paused, applicationPaused;
        private string lastFact = "Idle";
        private object respawnOwner;
        private int lifeGeneration, animationGeneration;
        public int LifeGeneration => lifeGeneration;
        public bool IsSimulationSuspended => !focused || IsPaused;
        public CharacterDataBus DataBus { get; private set; }
        public ICharacterActionCoordinator Actions {get;private set;}
        public int ActionControlRevision=>intentSelector==null?0:intentSelector.ControlRevision;
        public bool HasPlayerInput=>input!=null&&input.HasPlayerInput;
        public string ActorId => actorId;
        public string LastFact => lastFact;
        public string LastActionSource { get; private set; } = "none";
        public bool IsPaused => paused || applicationPaused;
        public bool ActionActive => DataBus != null && DataBus.ActionActive;
        public bool IsAlive => lifeState==null || lifeState.IsAlive;
        public bool CanSimulateIntent => ready && IsAlive && isActiveAndEnabled && focused && !IsPaused && (posture == null || !posture.IsStaggered);
        public CombatVitals Vitals => vitals;
        public event Action<CharacterRoot> Died;
        public CharacterFollow Follow => follow;
        public CharacterIntentKind EffectiveIntentKind => intentSelector == null ? CharacterIntentKind.Neutral : intentSelector.EffectiveKind;

        public bool CanBindCompanionIntent(ICharacterControlSession session, ICompanionIntentSource source)
            => ready && follow != null && ReferenceEquals(source,follow) && intentSelector != null && intentSelector.CanBind(session,source);
        public bool BindCompanionIntent(ICharacterControlSession session, ICompanionIntentSource source)
            => CanBindCompanionIntent(session,source) && intentSelector.Bind(session,source);
        public void ClearCompanionIntent(ICompanionIntentSource owner)
        { if (intentSelector != null) intentSelector.Clear(owner); }

        internal bool BindRespawnOwner(object owner)
        {
            if(owner==null || (respawnOwner!=null && !ReferenceEquals(respawnOwner,owner))) return false;
            respawnOwner=owner; return true;
        }
        internal void UnbindRespawnOwner(object owner)
        { if(ReferenceEquals(respawnOwner,owner)) respawnOwner=null; }
        internal bool CanRespawn(object owner) => ready && isActiveAndEnabled && !IsAlive &&
            vitals!=null && vitals.isActiveAndEnabled && ReferenceEquals(respawnOwner,owner);
        internal bool Respawn(object owner, Vector3 position)
        {
            if(!CanRespawn(owner)) return false;
            CancelAction("scene-respawn");
            if(follow!=null) follow.ResetIntent();
            if(intentSelector!=null) intentSelector.Clear();
            input.ResetPlayerInput(true);
            orientation.ResetOrientation();
            movement.Teleport(position);
            if(posture!=null) posture.ResetState();
            if(!vitals.RestoreAfterDeath()) return false;
            stamina?.Restore();
            lifeGeneration++;
            return true;
        }

        private void Awake()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            if (vitals!=null)
            {
                if (!vitals.Initialize(actorId)) { Debug.LogError("Character vitals identity/configuration invalid",this); enabled=false; return; }
                lifeState=vitals; vitals.Died+=OnDied;
            }
            if (movement == null || orientation == null || view == null || melee == null || input == null || viewCamera == null)
            { Debug.LogError("Character requires registered input/movement/orientation/view/melee/camera", this); enabled = false; return; }
            // Inspector保留具体组件槽；运行期通过窄接口消费，不反向依赖开发GUI或测试靶。
            // 每角色只有这一条有效Frame入口；没有Director绑定时保持原玩家适配器行为。
            intentSelector = new CharacterIntentSelector(this,input);
            inputSource = intentSelector;
            combatBrain=GetComponent<CompanionCombatBrain>();
            if(combatBrain!=null) combatBrain.Initialize(this);
            mover = movement;
            orienter = orientation;
            presentation = view;
            hitResolver = melee;
            if (!inputSource.IsConfigured) { Debug.LogError("Character actions are not configured", this); enabled = false; return; }
            try
            {
                definition = new BasicAttackDefinition(releaseAt, moveReadyAt, attackReadyAt, finishAt, damage, postureImpact);
                runtime = new BasicActionRuntime(actorId);
                chainProfile=GetComponent<BasicChainProfile>();
                // Root的早期Awake可能先于Profile.OnEnable；此处读取序列化enabled，运行期再查isActiveAndEnabled。
                if(chainProfile!=null && chainProfile.enabled)
                {
                    definition=chainProfile.stage1.Freeze(); secondDefinition=chainProfile.stage2.Freeze();
                    chain=chainProfile.CreateState(); hitReceipts=melee; hitReceipts.Resolved+=OnHitReceipt;
                }
            }
            catch (ArgumentException error) { Debug.LogError("Character configuration: " + error.Message, this); enabled = false; return; }
            // 每角色独立Bus只读取原所有者；只有确需Bus的运动和表现执行初始化。
            DataBus = new CharacterDataBus(actorId, inputSource, mover, orienter, runtime, authority);
            stamina=GetComponent<CharacterStamina>();if(stamina!=null)stamina.Initialize();
            dodge=GetComponent<CharacterDodge>();if(dodge!=null)dodge.Initialize(this,stamina);
            equipment=GetComponent<CharacterEquipment>();if(equipment!=null)equipment.Initialize(this);
            guard=GetComponent<ShieldGuard>();if(guard!=null)guard.Initialize(this,stamina,equipment);
            DataBus.BindResources(stamina,dodge);
            offhand=GetComponent<CharacterOffhand>(); offhand?.Initialize(this,equipment,stamina);
            Actions=new CharacterActionCoordinator(this,stamina,dodge,offhand);
            ready = InitializeSubsystems(DataBus, mover, presentation);
            if (!ready) { Debug.LogError("Character subsystem initialization failed", this); enabled = false; }
            // 可选能力只接受本角色初始化；无组件/无assignment的旧场保持普通玩家行为。
            if (follow != null && !follow.Initialize(this)) Debug.LogWarning("Character follow unavailable: configuration or owner invalid", this);
            if (posture != null)
            {
                if (!posture.Initialize(vitals, this)) { Debug.LogError("Character posture configuration invalid", this); enabled = false; return; }
                posture.Broken += OnPostureBroken;
                posture.Recovered += OnPostureRecovered;
            }
        }

        private static bool InitializeSubsystems(CharacterDataBus dataBus, params ICharacterSubsystem[] subsystems)
        {
            foreach (var subsystem in subsystems) if (subsystem == null || !subsystem.Initialize(dataBus)) return false;
            return true;
        }

        private void Update()
        {
            if (!ready) return;
            if (!IsAlive) { offhand?.CancelAll(); CommitPresentation(); return; }
            // 本体显式调度入口：以下阶段在一次Update中按书写顺序执行。
            // DefaultExecutionOrder只约束跨组件先后，不能替代这里的模块调用顺序。
            // 1. 采唯一意图源并处理暂停/取消；不由各子模块另开输入Update。
            // 选择器只采一个移动源；战斗能力另交窄动作候选，切人屏障为空帧，动作时钟仍继续。
            combatBrain?.Sample(runtime.SimulationTime, Time.deltaTime);
            inputSource.Read();
            var frame = DataBus.Input;
            if(chain!=null && (!input.HasPlayerInput || IsPaused || !focused || chainProfile==null || !chainProfile.isActiveAndEnabled)) chain.Clear();
            if (frame.pause) SetPaused(!paused);
            if (frame.cancel) CancelAction("player-cancel");
            // 2. 同一根时钟推进架势；主动作暂停自然恢复，暂停/失焦完全停钟。
            // 在Request前解除到期的本owner禁令；不会回滚其他根已释放的命中。
            if (posture != null) posture.Tick(IsPaused || !focused ? 0 : Time.deltaTime, runtime.IsActive);
            // 3. 先解算朝向，攻击释放才能读取本帧鼠标/键盘方向。
            var canMove = focused && !IsPaused && DataBus.AllowsMovement && DataBus.MoveReady;
            if (focused && !IsPaused)
                orienter.ResolveDirections(frame.move, PointerDirection(frame), frame.hasDirectionalAim ? frame.aimDirection : Vector2.zero, canMove);
            // 3b. 可选本体机动在同一根时钟采样/推进；失控、暂停、破势立即撤销免伤资格。
            if(dodge!=null)
            {
                if(!input.HasPlayerInput||!CanSimulateIntent)dodge.Cancel();
                dodge.Tick(runtime.SimulationTime);
                if(frame.dodgePressed)dodge.Request(frame.move,input.HasPlayerInput);
            }
            // 4. 观察按键边沿，再由Runtime的暂停/权限/Ready资格决定是否接受动作。
            guard?.Tick(frame.offhandHeld,input.HasPlayerInput,IsSimulationSuspended?0:Time.deltaTime);
            if (intent.Observe(frame.attackHeld) && focused && !frame.attackPointerBlocked)
            {
                var stage=chain==null?1:chain.Request(runtime.SimulationTime,runtime.AttackReady,
                    input.HasPlayerInput && !IsPaused && authority.Allows(ControlChannel.Attack) && chainProfile!=null && chainProfile.isActiveAndEnabled);
                if(stage>0) RequestStage(stage);
            }
            // 4a. 副手独立请求/时钟；同帧主手已按自身资格处理，二者不共享动作身份。
            if(frame.offhandPressed)offhand?.Request(PointerGroundPoint(frame),input.HasPlayerInput);
            offhand?.Tick(Time.deltaTime,input.HasPlayerInput);
            // 4b. 窄自治请求与玩家边沿互斥；仅Stage1，不通过输入帧注入攻击/暂停。
            if(!input.HasPlayerInput && CanSimulateIntent && combatBrain!=null &&
                ((ICompanionActionCandidate)combatBrain).TryGetAttackTarget(out var aiTarget))
                RequestStage(1,aiTarget);
            if(autonomousAction && runtime.IsActive)
            {
                if(combatBrain==null || !combatBrain.IsAcceptedTargetValid(autonomousTarget)) CancelAction("ai-target-invalid");
                else orienter.OrientTowards(autonomousTarget.transform.position);
            }
            // 5. 唯一动作时钟推进与Release命中。Accepted不扣血，切人不重发事实。
            foreach (var fact in runtime.Advance(Time.deltaTime, IsPaused || !focused))
            {
                Observe(fact);
                if (fact.Release != null)
                {
                    ReleaseCount++;
                    // 在释放当帧按值采样方向；不是接受动作时锁定，前摇中仍可重新瞄准。
                    var releaseDirection = orienter.AttackDirection;
                    orienter.FaceAttackDirection();
                    hitResolver.Resolve(fact.Release, releaseDirection);
                }
            }
            if(chain!=null && input.HasPlayerInput && !IsPaused && focused && authority.Allows(ControlChannel.Attack))
            {
                var bufferedStage=chain.Poll(runtime.SimulationTime,runtime.AttackReady);
                if(bufferedStage>0) RequestStage(bufferedStage);
            }
            // 6. 动作/命中可能改变MoveReady或架势权限，重新查询后才执行真实移动。
            dodge?.Tick(runtime.SimulationTime);
            canMove = focused && !IsPaused && DataBus.AllowsMovement && DataBus.MoveReady;
            mover.Tick(canMove, IsPaused || !focused ? 0 : Time.deltaTime);
            stamina?.Tick(runtime.SimulationTime,!runtime.IsActive&&(dodge==null||!dodge.IsActive)&&(guard==null||!guard.IsGuarding)&&CanSimulateIntent);
            // 7. 最后提交表现，Run读取已完成的实际位移。
            // 当前没有本体IK/骨骼后处理，故不虚设LateUpdate；Spine内部播放仍由插件驱动。
            CommitPresentation();
        }

        // 7. Animation Intent → One Commit；无子模块Update争抢主轨。
        private void CommitPresentation()
        {
            (presentation as Ossastra.AnimationPresentation.ICharacterAnimationFacts)?.CollectFacts(IsAlive, posture!=null && posture.IsStaggered, vitals!=null && vitals.IsHitReacting, animationGeneration, AttackStage);
            presentation.SetPaused(IsPaused || !focused);
            presentation.Present();
        }
        private Vector3? PointerGroundPoint(CharacterInputFrame frame)
        {
            if(!frame.hasPointer)return null; var plane=new Plane(Vector3.up,transform.position);
            var ray=viewCamera.ScreenPointToRay(frame.pointer); return plane.Raycast(ray,out var distance)?ray.GetPoint(distance):(Vector3?)null;
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
            LastActionSource=fact.Source;
            lastFact = fact.Kind + " @" + fact.At.ToString("F3") + " " + fact.ActionId.ToString("N").Substring(0, 8);
            if (fact.Reason != null) lastFact += " " + fact.Reason;
            if (verboseTrace) Debug.Log("Character " + lastFact, this);
            ActionObserved?.Invoke(fact);
        }
        private void RequestStage(int stage, CombatVitals aiTarget = null)
        {
            var selected=stage==2?secondDefinition:definition;
            var signature=Actions.CreateRequest(CharacterActionKind.Main,aiTarget==null?CharacterActionSource.Player:CharacterActionSource.Companion,aiTarget==null?"player-input:Stage"+stage:"companion:Stage1");
            ActionRequestResult request=null;
            var accepted=Actions.TryCommit(signature,stamina==null?0:stamina.AttackCost,()=>
            {
                request=runtime.Request(selected,aiTarget==null?"player-input:Stage"+stage:"companion:Stage1",authority,IsPaused);
                return request.Accepted?request.Fact.ActionId:Guid.Empty;
            },out var rejection);
            if(!accepted){lastFact="Rejected: "+rejection;return;}
            if(request.PreviousTerminal!=null)Observe(request.PreviousTerminal);
            if(request.Accepted)
            {
                AttackStage=stage; autonomousAction=aiTarget!=null; autonomousTarget=aiTarget;
                if(autonomousAction) { chain?.Clear(); combatBrain.OnAccepted(); orienter.OrientTowards(aiTarget.transform.position); }
                else chain?.Accepted(stage,request.Fact.ActionId,runtime.SimulationTime,selected.AttackReadyAt);
                Observe(request.Fact); orienter.FaceAttackDirection();
            }
            else lastFact="Rejected: "+request.Reason;
        }
        private void OnHitReceipt(Guid id,bool applied){if(!autonomousAction)chain?.Hit(id,applied);}

        public void CancelAction(string reason)
        {
            dodge?.Cancel();
            guard?.Cancel();
            offhand?.CancelPending();
            animationGeneration++; autonomousAction=false; autonomousTarget=null; combatBrain?.ResetCandidate();
            chain?.Clear();
            // 取消必须具名；抑制仍按住的输入，不能把下一帧误判为新请求。
            intent.SuppressUntilRelease();
            var terminal = runtime == null ? null : runtime.Cancel(reason);
            if (terminal != null) Observe(terminal);
        }

        private void OnPostureBroken(string cause)
        {
            authority.Block(posture, ControlChannel.Move | ControlChannel.Attack);
            CancelAction("posture-broken:" + cause);
            if (follow != null) follow.ResetIntent();
        }
        private void OnPostureRecovered()
        {
            authority.Release(posture);
            intent.SuppressUntilRelease();
            if (follow != null) follow.ResetIntent();
        }

        private void OnDied(HitOutcome outcome)
        {
            // HP已由结算归零；这里仅终止本体未释放动作/有效帧，再交唯一会话处理实控。
            offhand?.CancelAll();
            CancelAction("death:"+outcome.ActorId+":"+outcome.ActionId.ToString("N"));
            if (follow!=null) follow.ResetIntent();
            if (intentSelector!=null) intentSelector.Clear();
            Died?.Invoke(this);
        }

        public void SetPaused(bool value) { paused = value; if(value){dodge?.Cancel();guard?.Cancel();
            offhand?.CancelPending();} chain?.Clear(); intent.SuppressUntilRelease(); if (follow != null) follow.ResetIntent(); }
        public void SetAttackRestriction(object owner, bool restricted, string reason = "attack-restricted")
        {
            if (restricted) { authority.Block(owner, ControlChannel.Attack); CancelAction(reason); }
            else authority.Release(owner);
        }

        private void OnApplicationFocus(bool hasFocus)
        { focused = hasFocus; if (follow != null) follow.ResetIntent(); if (!hasFocus) CancelAction("focus-lost"); else intent.SuppressUntilRelease(); }
        private void OnApplicationPause(bool value)
          { applicationPaused = value; if(value)chain?.Clear(); if (follow != null) follow.ResetIntent(); if (value) intent.SuppressUntilRelease(); }
        private void OnEnable() { intent.SuppressUntilRelease(); if (intentSelector != null) intentSelector.Clear(); }
        private void OnDisable() { offhand?.CancelAll(); CancelAction("disabled"); if (posture != null) posture.ResetState(); authority.Release(posture); if (follow != null) follow.ResetIntent(); if (intentSelector != null) intentSelector.Clear(); if (ready && presentation != null) presentation.SetPaused(false); }
        private void OnDestroy()
        {
            if(hitReceipts!=null) hitReceipts.Resolved-=OnHitReceipt;
            if (vitals!=null) vitals.Died-=OnDied;
            if (posture != null) { posture.Broken -= OnPostureBroken; posture.Recovered -= OnPostureRecovered; }
            authority.Release(posture);
            // 终止本角色动作后解除本对象引用；不清其它角色或场景控制权威。
            CancelAction("destroyed");
            ready = false;
            DataBus = null;
            runtime = null;
            inputSource = null;
            if (intentSelector != null) intentSelector.Clear();
            intentSelector = null;
            mover = null;
            orienter = null;
            presentation = null;
            hitResolver = null;
        }
    }
}
