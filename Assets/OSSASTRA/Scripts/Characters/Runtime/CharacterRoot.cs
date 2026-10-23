/*
 * 脚本：CharacterRoot
 * 功能：角色组装与明确调度；注册仲裁器/状态机拥有动作组织，接受费用沿原协调器。
 * 边界：主动作运行时由战斗状态机唯一持有；只读生命资格，不实现HP写入、设备绑定或开发GUI。
 * 协作：初始化可选Follow/Posture与定义驱动能力；显式按输入、架势、朝向、原动作事实、投递、运动、表现推进。
 * 兼容：没有能力控制端口的历史场继续旧Skills/Guard/Offhand路径，新候选与旧路径互斥。
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
    [RequireComponent(typeof(CharacterOrientation),typeof(StateArbitrator))]
    public sealed class CharacterRoot : MonoBehaviour
    {
        [SerializeField] private CharacterDefinitionSO characterDefinition;
        public CharacterDefinitionSO Definition=>characterDefinition;
        [SerializeField] private string actorId = "player";
        [FormerlySerializedAs("motor"), SerializeField] private CharacterMovement movement;
        [SerializeField] private CharacterOrientation orientation;
        [SerializeField] private MonoBehaviour view;
        [SerializeField] private CombatMeleeHit melee;
        [SerializeField] private CharacterInputHandler input;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private CharacterFollow follow;
        [SerializeField] private CombatVitals vitals;
        [SerializeField] private CombatPosture posture;
        [SerializeField] private StateArbitrator stateArbitrator;
        public StateArbitrator StateArbitrator=>stateArbitrator;
        private BasicActionRuntime runtime=>stateArbitrator?.Combat?.Runtime;
        internal ActionDefinitionSO[] StateActionLibrary=>ActionLibrary;
        internal void PublishActionFact(ActionFact fact)=>ActionObserved?.Invoke(fact);
        internal Vector3? StatePointerDirection(CharacterInputFrame frame)=>PointerDirection(frame);
        internal Vector3? StatePointerGroundPoint(CharacterInputFrame frame)=>PointerGroundPoint(frame);
        internal CharacterDefinitionSO StateCharacterDefinition=>characterDefinition;
        internal CharacterMovement StateMovement=>movement;
        internal CharacterOrientation StateOrientation=>orientation;
        internal CombatMeleeHit StateMelee=>melee;
        internal CharacterInputHandler StateInput=>input;
        internal Camera StateViewCamera=>viewCamera;
        internal CompanionCombatBrain StateCombatBrain=>combatBrain;
        internal ICharacterInventoryConsumer StateInventoryConsumer=>inventoryConsumer;
        internal CharacterStamina StateStamina=>stamina;
        internal CombatStatusEffects StateStatusEffects=>statusEffects;
        internal CharacterDodge StateDodge=>dodge;
        internal CharacterEquipment StateEquipment=>equipment;
        internal ShieldGuard StateGuard=>guard;
        internal CharacterOffhand StateOffhand=>offhand;
        internal ICharacterSkills StateSkills=>skills;
        internal ICharacterAbilityController StateAbilities=>abilities;
        internal CharacterMainAction StateMainAction=>mainAction;
        internal ControlAuthority StateAuthority=>authority;
        internal ICharacterInputSource StateInputSource=>inputSource;
        internal ICharacterMovement StateMover=>mover;
        internal ICharacterOrientation StateOrienter=>orienter;
        internal ICharacterPresentation StatePresentation=>presentation;
        internal ICombatHitResolver StateHitResolver=>hitResolver;
        internal CombatVitals StateVitals=>vitals;
        internal CombatPosture StatePosture=>posture;
        internal bool StateFocused=>focused;
        internal bool StateReady=>ready;
        internal float StateReleaseAt=>releaseAt;
        internal float StateMoveReadyAt=>moveReadyAt;
        internal float StateAttackReadyAt=>attackReadyAt;
        internal float StateFinishAt=>finishAt;
        internal int StateDamage=>damage;
        internal float StatePostureImpact=>postureImpact;
        internal bool StateVerboseTrace=>verboseTrace;
        private ICombatLifeState lifeState;
        private CompanionCombatBrain combatBrain;
        private ICharacterInventoryConsumer inventoryConsumer;
        private ICharacterTransientItemPresentation transientItems;
        private CharacterStamina stamina;
        private CombatStatusEffects statusEffects;
        private CharacterDodge dodge;
        private CharacterEquipment equipment;
        private ShieldGuard guard;
        private CharacterOffhand offhand;
        private CharacterEquipmentView equipmentView;
        private CharacterEquipmentSpineView equipmentSpineView;
        private ICharacterSkills skills;
        private ICharacterAbilityController abilities;
        private CharacterMainAction mainAction;
        public event Action<ActionFact> ActionObserved;
        public double SimulationTime => runtime == null ? 0 : runtime.SimulationTime;
        public ICompanionMoveCandidate CombatMove => combatBrain;
        [Header("Prototype timing — not final balance")]
        [SerializeField] private float releaseAt = .3f, moveReadyAt = .5f, attackReadyAt = .8f, finishAt = 1.1f;
        [SerializeField, Min(1)] private int damage = 12;
        [SerializeField, Min(0)] private float postureImpact;
        [SerializeField] private bool verboseTrace;
        private readonly ControlAuthority authority = new ControlAuthority();
        private ICharacterInputSource inputSource;
        private CharacterIntentSelector intentSelector;
        private ICharacterMovement mover;
        private ICharacterOrientation orienter;
        private ICharacterPresentation presentation;
        private ICombatHitResolver hitResolver;
        public int AttackStage=>stateArbitrator?.Combat?.AttackStage??0;
        public int ReleaseCount=>stateArbitrator?.Combat?.ReleaseCount??0;
        public bool HasBufferedAttack=>stateArbitrator?.Combat?.HasBufferedAttack??false;
        public double ComboWindowUntil=>stateArbitrator?.Combat?.ComboWindowUntil??-1;
        private bool ready, focused = true, applicationPaused;
        private string lastFact=>stateArbitrator?.Combat?.LastFact??"Idle";
        private object respawnOwner;
        private int lifeGeneration;
        private int animationGeneration=>stateArbitrator?.Combat?.AnimationGeneration??0;
        public int LifeGeneration => lifeGeneration;
        public bool IsSimulationSuspended => !focused || IsPaused;
        public CharacterDataBus DataBus { get; private set; }
        public ICharacterActionCoordinator Actions {get;private set;}
        public ICharacterAbilityController Abilities=>abilities;
        public int ActionControlRevision=>intentSelector==null?0:intentSelector.ControlRevision;
        public bool HasPlayerInput=>input!=null&&input.HasPlayerInput;
        public string ActorId => actorId;
        public string LastFact => lastFact;
        public string LastActionSource=>stateArbitrator?.Combat?.LastActionSource??"none";
        public bool IsPaused => applicationPaused;
        public bool ActionActive => DataBus != null && DataBus.ActionActive;
        public bool IsAlive => lifeState==null || lifeState.IsAlive;
        public bool CanSimulateIntent => ready && IsAlive && isActiveAndEnabled && focused && !IsPaused && (posture == null || !posture.IsStaggered) && (statusEffects==null||!statusEffects.BlocksIntent);
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
            statusEffects?.Clear();mainAction?.ResetState();
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

        // 装备有动作库时优先采用；角色动作库只作为旧资产/未装配主手的兼容默认。
        private ActionDefinitionSO[] ActionLibrary
        {
            get { var gear=equipment!=null?equipment:GetComponent<CharacterEquipment>();var actions=gear?.RightDefinition?.actions;
                return actions!=null&&actions.Length>0?actions:characterDefinition?.basicAttacks; }
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
            foreach(var component in GetComponents<MonoBehaviour>())if(component is ICharacterInventoryConsumer consumer){inventoryConsumer=consumer;consumer.Initialize(this);break;}
            foreach(var component in GetComponents<MonoBehaviour>())if(component is ICharacterTransientItemPresentation itemView){transientItems=itemView;itemView.Initialize(this);break;}
            mover = movement;
            orienter = orientation;
            presentation = view as ICharacterPresentation;
            hitResolver = melee;
            if (!inputSource.IsConfigured) { Debug.LogError("Character actions are not configured", this); enabled = false; return; }
            try
            {
                stateArbitrator=GetComponent<StateArbitrator>();
                if(stateArbitrator==null)throw new ArgumentException("StateArbitrator component required");
                stateArbitrator.Initialize(this);
            }
            catch (ArgumentException error) { Debug.LogError("Character configuration: " + error.Message, this); enabled = false; return; }
            // 每角色独立Bus只读取原所有者；只有确需Bus的运动和表现执行初始化。
            DataBus = new CharacterDataBus(actorId, inputSource, mover, orienter, runtime, authority);
            statusEffects=GetComponent<CombatStatusEffects>();if(statusEffects!=null)statusEffects.ControlChanged+=OnStatusControlChanged;
            stamina=GetComponent<CharacterStamina>();if(stamina!=null)stamina.Initialize();
            dodge=GetComponent<CharacterDodge>();if(dodge!=null)dodge.Initialize(this,stamina);
            equipment=GetComponent<CharacterEquipment>();if(equipment!=null){equipment.Initialize(this);if(equipment.State==null){enabled=false;return;}}
            guard=GetComponent<ShieldGuard>();if(guard!=null)guard.Initialize(this,stamina,equipment);
            DataBus.BindResources(stamina,dodge);
            offhand=GetComponent<CharacterOffhand>(); offhand?.Initialize(this,equipment,stamina);
            equipmentView=GetComponent<CharacterEquipmentView>();equipmentView?.Initialize(this);
            equipmentSpineView=GetComponent<CharacterEquipmentSpineView>();equipmentSpineView?.Initialize(this);
            Actions=new CharacterActionCoordinator(this,stamina,dodge,offhand);
            skills=GetComponent<CharacterSkills>();skills?.Initialize(this);
            var abilityComponent=GetComponent<CharacterAbilities>();
            if(abilityComponent!=null&&abilityComponent.enabled){abilities=abilityComponent;abilities.Initialize(this);DataBus.BindAbilityMotion(abilityComponent);}
            ready = InitializeSubsystems(DataBus, mover, presentation);
            mainAction=GetComponent<CharacterMainAction>();
            if(mainAction!=null&&mainAction.enabled){mainAction.Initialize(this);mainAction.Resolved+=OnHitReceipt;DataBus.BindMainMotion(mainAction);RefreshMainProfile();}
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
            if(!ready)return;
            stateArbitrator.ManagedUpdate(Time.deltaTime);
            CommitPresentation();
        }

        // Spine在Update求骨骼；本根LateUpdate消费同帧挂点，Renderer稍后构建网格。
        // 此处仅推进装备表现，不执行第二次Move或结算。
        private void LateUpdate(){if(ready){(presentation as Ossastra.CharacterAnimation.CharacterAnimationController)?.FlushSignals();equipmentView?.TickPresentation();equipmentSpineView?.TickPresentation();transientItems?.TickPresentation();}}
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
        /// <summary>鼠标必须有合法世界点；方向输入按当前执行器实际射程生成，绝不统一猜投掷距离。</summary>
        internal Vector3? AbilityAimPoint(CharacterInputFrame frame,float range)
            =>frame.aimSource==PlayerInputSource.KeyboardMouse?PointerGroundPoint(frame):
                frame.hasDirectionalAim&&range>0?transform.position+DataBus.AttackDirection*range:(Vector3?)null;
        internal ConfiguredActionSnapshot FreezeAbilityPhase(ActionDefinitionSO config)
            =>((ICharacterConfiguredAnimation)presentation).FreezeAction(config);
        internal bool ChangeAbilityPhase(Guid id,ConfiguredActionSnapshot phase)=>stateArbitrator.Combat.ChangeAbilityPhase(id,phase);
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

        private void Observe(ActionFact fact)=>stateArbitrator.Combat.ObserveFact(fact);
        /// <summary>主动技能使用同一主动作接受端口、Runtime和费用事务；不借用副手通道。</summary>
        public bool TryConfiguredAction(ActionDefinitionSO config,CharacterActionSource source,string label,out Guid actionId)
            =>TryAbilityBodyAction(config,source,label,config==null?0:config.staminaCost,out actionId);
        /// <summary>定义驱动身体能力复用原Runtime与接受账本；费用来自已冻结能力，不再由片段定义另付一次。</summary>
        internal bool TryAbilityBodyAction(ActionDefinitionSO config,CharacterActionSource source,string label,float cost,out Guid actionId,Func<bool> prepareSource=null,Action rollbackSource=null,ConfiguredActionSnapshot phase=null)
            =>stateArbitrator.Combat.TryAbilityBodyAction(config,source,label,cost,out actionId,prepareSource,rollbackSource,phase);
        /// <summary>能力按已接受身份只撤回自己的主通道；副手、盾和机动不借此被全局取消。</summary>
        internal bool CancelOwnedMainAction(Guid actionId,string reason)=>stateArbitrator.Combat.CancelOwnedMainAction(actionId,reason);

        // 动作组只更新空闲候选；已受理动作继续用冻结时间线与表现，不因来源切换重新计费。
        private void RefreshMainProfile()=>stateArbitrator.Combat.RefreshMainProfile();

        private void OnHitReceipt(Guid id,bool applied)=>stateArbitrator?.Combat?.OnHitReceipt(id,applied);

        public void CancelAction(string reason){if(stateArbitrator!=null&&stateArbitrator.Combat!=null)stateArbitrator.Cancel(reason);}

        private void OnPostureBroken(string cause)
        {
            authority.Block(posture, ControlChannel.Move | ControlChannel.Attack);
            CancelAction("posture-broken:" + cause);
            if (follow != null) follow.ResetIntent();
        }
        private void OnStatusControlChanged()
        {
            if(statusEffects!=null&&statusEffects.BlocksIntent)
            {authority.Block(statusEffects,ControlChannel.Move|ControlChannel.Attack);CancelAction("combat-status");dodge?.Cancel();abilities?.CancelPending("combat-status");}
            else {authority.Release(statusEffects);stateArbitrator?.Combat?.SuppressInput();}
        }
        private void OnPostureRecovered()
        {
            authority.Release(posture);
            stateArbitrator?.Combat?.SuppressInput();
            if (follow != null) follow.ResetIntent();
        }

        private void OnDied(HitOutcome outcome)
        {
            statusEffects?.Clear();
            mainAction?.ResetState();
            equipmentView?.Hide();equipmentSpineView?.Hide();skills?.Clear();
            // HP已由结算归零；这里仅终止本体未释放动作/有效帧，再交唯一会话处理实控。
            offhand?.CancelAll();
            CancelAction("death:"+outcome.ActorId+":"+outcome.ActionId.ToString("N"));
            if (follow!=null) follow.ResetIntent();
            if (intentSelector!=null) intentSelector.Clear();
            Died?.Invoke(this);
        }


        public void SetAttackRestriction(object owner, bool restricted, string reason = "attack-restricted")
        {
            if (restricted) { authority.Block(owner, ControlChannel.Attack); CancelAction(reason); }
            else authority.Release(owner);
        }

        private void OnApplicationFocus(bool hasFocus)
        { focused = hasFocus; if (follow != null) follow.ResetIntent(); if (!hasFocus) CancelAction("focus-lost"); else stateArbitrator?.Combat?.SuppressInput(); }
        private void OnApplicationPause(bool value)
          { applicationPaused = value; if(value)stateArbitrator?.Combat?.ClearBuffer(); if (follow != null) follow.ResetIntent(); if (value) stateArbitrator?.Combat?.SuppressInput(); }
        private void OnEnable() { stateArbitrator?.Combat?.SuppressInput(); if (intentSelector != null) intentSelector.Clear(); }
        private void OnDisable() { transientItems?.Hide();statusEffects?.Clear();mainAction?.ResetState();equipmentView?.Hide();equipmentSpineView?.Hide();skills?.Clear();offhand?.CancelAll(); CancelAction("disabled"); if (posture != null) posture.ResetState(); authority.Release(posture); if (follow != null) follow.ResetIntent(); if (intentSelector != null) intentSelector.Clear(); if (ready && presentation != null) presentation.SetPaused(false); }
        private void OnDestroy()
        {
            if(mainAction!=null)mainAction.Resolved-=OnHitReceipt;
            if(statusEffects!=null)statusEffects.ControlChanged-=OnStatusControlChanged;
            if (vitals!=null) vitals.Died-=OnDied;
            if (posture != null) { posture.Broken -= OnPostureBroken; posture.Recovered -= OnPostureRecovered; }
            authority.Release(posture);
            // 终止本角色动作后解除本对象引用；不清其它角色或场景控制权威。
            CancelAction("destroyed");
            if(stateArbitrator!=null)stateArbitrator.Dispose();
            ready = false;
            DataBus = null;

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
