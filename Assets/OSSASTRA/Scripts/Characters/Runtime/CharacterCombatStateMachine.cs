/* 脚本：CharacterCombatStateMachine
 * 功能：注册战斗状态机，唯一拥有并推进 BasicActionRuntime、普攻预输入、受理快照与主动作取消。
 * 边界：不采设备、不写运动/HP；能力Run和费用账本仍由既有端口所有。
 * 协作：仲裁器按原阶段调用；投递完成后向队伍登记切换连携机会；Root公开方法仅转发，释放仍走同一战斗网关。
 */
using System;
using Ossastra.CharacterCore;
using Ossastra.Combat;
using UnityEngine;
namespace Ossastra.Characters
{
 public sealed partial class CharacterCombatStateMachine:CharacterStateMachine,IParallelCharacterStateMachine
 {
  private BasicActionRuntime runtime;
  private ActionImpactRun impactRun;
  internal ActionImpactRun ImpactFor(Guid id) => impactRun?.Id == id ? impactRun : null;
  internal void OnImpactSpine(Guid action, Guid phase, string eventName, int occurrence = -1, string animation = null)
  {
      if (owner.IsSimulationSuspended || runtime.ActionId != action || phase == Guid.Empty ||
          !(owner.StatePresentation is Ossastra.CharacterAnimation.CharacterAnimationController controller) ||
          controller.CapturedPhaseId != phase) return;
      ImpactFor(action)?.Spine(eventName, occurrence, animation);
  }
  private BasicAttackDefinition definition,secondDefinition;
  private BasicAttackDefinition[] configuredAttacks;
  private BasicChainState chain;
  private BasicChainProfile chainProfile;
  private ICombatHitReceiptSource hitReceipts;
  private CombatVitals autonomousTarget;
  private bool autonomousAction;
  private readonly AttackIntent intent=new AttackIntent();
  private bool readyPressDuringHitStop;
  private bool comboInputTracked;
  private int comboControlRevision, comboGestureGeneration;
  private string lastFact="Idle";
  private int animationGeneration;
  public int AttackStage {get;private set;}
  public int ReleaseCount {get;private set;}
  public string LastActionSource {get;private set;}="none";
  public string LastFact=>lastFact;
  public int AnimationGeneration=>animationGeneration;
  public bool HasBufferedAttack=>chain!=null&&chain.HasBuffer;
  public UnityEngine.Object ComboSource { get; private set; }
  public BasicComboSettings? EffectiveComboSettings { get; private set; }
  public double ComboWindowUntil=>chain==null?-1:chain.WindowUntil;
  public BasicActionRuntime Runtime=>runtime;
  public override string StateMachineName=>"CombatStateMachine";
  public override string CurrentStateName=>runtime==null||!runtime.IsActive?"Idle":runtime.AttackReady?"AttackReady":runtime.MoveReady?"MoveReady":"Action";
  public override bool IsActive=>runtime!=null&&runtime.IsActive;
  public override bool AllowsRequest(CharacterActionKind kind,out string reason)
  {
   reason=kind==CharacterActionKind.Main&&(!owner.DataBus.AllowsAttack||!owner.DataBus.AttackReady)?"main-not-ready":null;
   return reason==null;
  }
  private bool ChainAvailable => ComboSource != null && (ComboSource != chainProfile || chainProfile.isActiveAndEnabled);
  private ActionDefinitionSO[] ActionLibrary
  {
      get
      {
#if UNITY_EDITOR
          return AttackLabBindings.ResolveActions(owner,owner.StateActionLibrary);
#else
          return owner.StateActionLibrary;
#endif
      }
  }
  public override void Initialize(CharacterRoot root)
  {
   base.Initialize(root);
   runtime = new BasicActionRuntime(root.ActorId);
   chainProfile = root.GetComponent<BasicChainProfile>();
   RebuildMainSequence();
   hitReceipts = root.StateMelee;
   if (hitReceipts != null) hitReceipts.Resolved += OnHitReceipt;
  }
  public void ClearBuffer()
  {
      readyPressDuringHitStop = false;
      comboInputTracked = false;
      chain?.Clear();
  }
  public void SuppressInput()=>intent.SuppressUntilRelease();
  public void OnHitReceipt(Guid id,bool applied){if(!autonomousAction)chain?.Hit(id,applied);}
  public void Dispose(){impactRun?.End();impactRun=null;if(hitReceipts!=null)hitReceipts.Resolved-=OnHitReceipt;hitReceipts=null;}
  public void RequestInput(CharacterInputFrame frame)
  {
            var pressed=frame.hasAttackEdges?intent.Observe(frame.attackHeld,frame.attackPressed):intent.Observe(frame.attackHeld);
            pressed |= readyPressDuringHitStop;
            readyPressDuringHitStop = false;
            var trace = CombatTraceProbe.For(owner);
            if (pressed || frame.attackPressed)
                trace?.Note("输入","主手输入交给战斗状态机",$"有效边沿={pressed}，界面捕获={frame.attackPointerBlocked}，请求封锁={frame.attackRequestsBlocked}，焦点={owner.StateFocused}，攀越限制={owner.StateArbitrator.Movement.TraversalBlocksAttack}，瞄准={frame.aimSource}");
            if(owner.StateArbitrator.Movement.TraversalBlocksAttack){ClearBuffer();if(frame.attackHeld)SuppressInput();return;}
            if (pressed && !frame.attackRequestsBlocked && owner.StateFocused && !frame.attackPointerBlocked)
            {
                bool wasBuffered = HasBufferedAttack;
                var stage=chain==null?1:chain.Request(runtime.SimulationTime,runtime.AttackReady,
                    owner.StateInput.HasPlayerInput && !owner.IsPaused && owner.StateAuthority.Allows(ControlChannel.Attack) && ChainAvailable && (owner.StateMainAction==null||owner.StateMainAction.AllowsBasicRequest));
                if (HasBufferedAttack)
                {
                    TrackComboInputContext();
                    trace?.Note("输入", wasBuffered ? "普攻更新预输入" : "普攻入缓冲", "等待当前动作开放", runtime.ActionId);
                }
                else if (stage==0) trace?.Note("输入","未发起新普攻",$"再次攻击许可={runtime.AttackReady}，已有缓冲={HasBufferedAttack}，整套收尾许可={owner.StateMainAction?.AllowsBasicRequest}",runtime.ActionId);
                if(stage>0) RequestStage(stage);
            }

  }
        internal void BufferInputDuringHitStop(CharacterInputFrame frame)
        {
            var pressed = frame.hasAttackEdges ? intent.Observe(frame.attackHeld, frame.attackPressed) : intent.Observe(frame.attackHeld);
            if (!owner.HasPlayerInput || owner.IsSimulationSuspended || frame.attackRequestsBlocked || frame.attackPointerBlocked ||
                owner.StateArbitrator.Movement.TraversalBlocksAttack || !owner.StateAuthority.Allows(ControlChannel.Attack))
            {
                DiscardPendingComboInput();
                if (frame.attackHeld) SuppressInput();
                return;
            }
            ClearStaleComboInput(frame);
            if (!pressed || (owner.StateMainAction != null && !owner.StateMainAction.AllowsBasicRequest)) return;

            // 保留一条边沿，原动作时钟暂停。恢复后由既有主手请求路径检查当前资格。
            if (runtime.AttackReady) readyPressDuringHitStop = true;
            else if (ChainAvailable) chain?.Request(runtime.SimulationTime, false, true);
            if (readyPressDuringHitStop || HasBufferedAttack)
            {
                TrackComboInputContext();
                CombatTraceProbe.For(owner)?.Note("输入", "卡肉中记录攻击缓冲", "恢复后按原动作资格消费", runtime.ActionId);
            }
        }

        private void TrackComboInputContext()
        {
            comboInputTracked = true;
            comboControlRevision = owner.ActionControlRevision;
            comboGestureGeneration = owner.StateInput.GestureGeneration;
        }

        private void ClearStaleComboInput(CharacterInputFrame frame)
        {
            if (!comboInputTracked) return;
            // UI 捕获或输入代次变化时，普通与卡肉期间的旧预输入都不能在恢复后消费。
            if (frame.attackRequestsBlocked || frame.attackPointerBlocked ||
                comboControlRevision != owner.ActionControlRevision || comboGestureGeneration != owner.StateInput.GestureGeneration)
                DiscardPendingComboInput();
            else if (!HasBufferedAttack && !readyPressDuringHitStop)
                comboInputTracked = false;
        }

        private void DiscardPendingComboInput()
        {
            comboInputTracked = readyPressDuringHitStop = false;
            chain?.DiscardBufferedPress();
        }
  public void RequestCompanion()
  {
            bool blocksNewCompanion = owner.StateInput.BlocksSwitchLinkCompanion(owner);
            var inventoryAccepted=!blocksNewCompanion&&owner.StateInventoryConsumer!=null&&owner.StateInventoryConsumer.TickCandidate();
            if(!blocksNewCompanion&&!inventoryAccepted&&!owner.StateInput.HasPlayerInput && owner.CanSimulateIntent && owner.StateCombatBrain!=null &&
                ((ICompanionActionCandidate)owner.StateCombatBrain).TryGetAttackTarget(out var aiTarget))
                RequestStage(1,aiTarget);
            if(autonomousAction && runtime.IsActive)
            {
                if(owner.StateCombatBrain==null || !owner.StateCombatBrain.IsAcceptedTargetValid(autonomousTarget)) CancelAction("ai-target-invalid");
                else owner.StateOrienter.OrientTowards(autonomousTarget.transform.position);
            }

  }
  public override void Update(float deltaTime)
  {
            owner.StateMainAction?.PrepareMotion(runtime.SimulationTime+(owner.IsSimulationSuspended?0:deltaTime),owner.IsSimulationSuspended?0:deltaTime);
            foreach (var fact in runtime.Advance(deltaTime, owner.IsPaused || !owner.StateFocused)) DispatchFact(fact);
  }

  internal bool ConsumeAnimationMarker(Guid id, Guid phase, ActionMarkerKind marker)
  {
      if (owner.IsSimulationSuspended || !runtime.EventDriven || runtime.ActionId != id ||
          !(owner.StatePresentation is Ossastra.CharacterAnimation.CharacterAnimationController controller) ||
          controller.CapturedActionId != id || phase == Guid.Empty || controller.CapturedPhaseId != phase) return false;
      foreach (var fact in runtime.ConsumeMarker(id, marker)) DispatchFact(fact);
      return true;
  }

  private void DispatchFact(ActionFact fact)
  {
                if (fact.Kind == ActionFactKind.AttackReady) chain?.MarkReady(fact.ActionId, runtime.SimulationTime);
                bool switchAttack = fact.Release != null && fact.ActionId == switchAttackId;
                if (switchAttack) owner.StateOrienter.OrientTowards(owner.transform.position + switchAttackDirection);
                ObserveFact(fact);
                if (fact.Release != null)
                {
                    ReleaseCount++;
                    // 在释放当帧按值采样方向；不是接受动作时锁定，前摇中仍可重新瞄准。
                    var releaseDirection = switchAttack ? switchAttackDirection : owner.StateOrienter.AttackDirection;
                    owner.StateOrienter.FaceAttackDirection();
                    if(owner.StateAbilities!=null&&owner.StateAbilities.OwnsRelease(fact.ActionId))return;
                    if(owner.StateMainAction!=null&&owner.StateMainAction.TryRelease(fact,releaseDirection,out bool delivered))
                    {
                        if (delivered) RecordBasicRelease(fact.ActionId);
                        return;
                    }
                    ImpactFor(fact.ActionId)?.Bind(fact.Release, null);
                    if(owner.StateSkills!=null&&owner.StateSkills.TryHandleRelease(fact.Release,releaseDirection))return;
                    if(owner.StateHitResolver is CombatMeleeHit melee)
                        melee.Resolve(fact.Release,releaseDirection,AttackStage);
                    else owner.StateHitResolver.Resolve(fact.Release,releaseDirection);
                    RecordBasicRelease(fact.ActionId);
                }
  }
  public void CompleteFrame(float deltaTime)
  {
            owner.StateMainAction?.TickEffects(owner.IsSimulationSuspended?0:deltaTime);
            if(chain!=null && !owner.StateArbitrator.Movement.TraversalBlocksAttack &&
                owner.StateInput.HasPlayerInput && !owner.IsPaused && owner.StateFocused && owner.StateAuthority.Allows(ControlChannel.Attack))
            {
                bool hadBuffer = chain.HasBuffer;
                var bufferedStage=chain.Poll(runtime.SimulationTime,runtime.AttackReady);
                if (hadBuffer && !chain.HasBuffer)
                    CombatTraceProbe.For(owner)?.Note("输入", bufferedStage>0?"消费缓冲":"缓冲过期或接续资格结束", "接续段 "+bufferedStage,runtime.ActionId);
                if(bufferedStage>0) RequestStage(bufferedStage);
            }

  }
  public void PrepareFrame(CharacterInputFrame frame)
  {
      if (!owner.HasPlayerInput || owner.IsPaused || !owner.StateFocused) ClearBuffer();
      else if (chain != null && !ChainAvailable) chain.Clear();
      ClearStaleComboInput(frame);
  }
        public void ObserveFact(ActionFact fact) => ObserveFact(fact, null);
        private void ObserveFact(ActionFact fact, string phaseClip)
        {
            if(fact.Kind==ActionFactKind.Accepted)owner.StateVitals?.Recovery?.RetainParent(fact.ActionId);
            LastActionSource=fact.Source;
            lastFact = fact.Kind + " @" + fact.At.ToString("F3") + " " + fact.ActionId.ToString("N").Substring(0, 8);
            if (fact.Reason != null) lastFact += " " + fact.Reason;
            if (owner.StateVerboseTrace) Debug.Log("Character " + lastFact, owner);
            owner.StateArbitrator.ObserveCombatEdge();
            ImpactFor(fact.ActionId)?.Observe(fact, phaseClip);
            if ((fact.Kind == ActionFactKind.Finished || fact.Kind == ActionFactKind.Cancelled) && impactRun?.Id == fact.ActionId) impactRun = null;
            owner.StateAbilities?.ObserveFact(fact);
            owner.StateMainAction?.ObserveFact(fact);
            ObserveSwitchActionFact(fact);
            owner.PublishActionFact(fact);
            if (fact.Kind == ActionFactKind.Finished || fact.Kind == ActionFactKind.Cancelled)
            {
                if (owner.StateAbilities?.OwnsRelease(fact.ActionId) != true) owner.EndActionExposure(fact.ActionId);
                if (fact.Kind == ActionFactKind.Cancelled) owner.StateVitals?.CancelHitStopForParent(fact.ActionId);
                owner.StateVitals?.Recovery?.ReleaseParent(fact.ActionId);
            }
        }
        private void BindPostureRecovery(Guid id, ConfiguredActionSnapshot phase, string marker)
        {
            if (!owner.UsesActionPosture ||
                !(owner.StatePresentation is Ossastra.CharacterAnimation.CharacterAnimationController controller)) return;
            var recovery = controller.ResolvePostureRecoveryEvent(phase, marker);
            if (recovery != null) owner.BindActionRecoveryEvent(id, recovery);
        }

        private bool TryResolveConfiguredAction(ActionDefinitionSO config,out ConfiguredActionSnapshot frozen,out string reason,CharacterAnimationPreparation? preparation=null)
        {
            frozen=null;reason=null;
            if(!(owner.StatePresentation is ICharacterConfiguredAnimation target)){reason="missing-animation-port";return false;}
            try{frozen=owner.FreezeAbilityPhase(config,preparation);}catch(ArgumentException error){reason="invalid-definition:"+error.Message;return false;}
            return ValidatePrepared(frozen,out reason);
        }
        private bool ValidatePrepared(ConfiguredActionSnapshot phase,out string reason)
        {
            reason=phase?.Timeline==null?"missing-phase":null;
            if(reason!=null)return false;
            return !(owner.StatePresentation is ICharacterPreparedAnimation validator)||validator.CanCapturePhase(phase,out reason);
        }
        internal bool TryAbilityBodyAction(ActionDefinitionSO config,CharacterActionSource source,string label,float cost,out Guid actionId,Func<bool> prepareSource=null,Action rollbackSource=null,ConfiguredActionSnapshot phase=null,CharacterAnimationPreparation? preparation=null,bool isAttack=true,AbilityDefinitionSO ability=null, Vector3? effectAim=null, string equipmentSource=null)
        {
            actionId=Guid.Empty;if(config==null||!owner.StateReady)return false;
            BasicAttackDefinition frozen;
            if(phase==null&&!TryResolveConfiguredAction(config,out phase,out var configurationError,preparation)){lastFact="Rejected: "+configurationError;return false;}
            if(!ValidatePrepared(phase,out var phaseError)){lastFact="Rejected: "+phaseError;return false;}
            frozen=phase.Timeline;
            var signature=owner.Actions.CreateRequest(CharacterActionKind.Main,source,label).WithAttackSemantics(isAttack).WithPostureTarget(config.actionPostureExposure == -1 ? (isAttack ? 30 : 0) : config.actionPostureExposure);
            ActionRequestResult result=null;
            if(!owner.Actions.TryCommit(signature,cost,()=>
            {
                if(prepareSource!=null&&!prepareSource())return Guid.Empty;
                var hitStop = isAttack ? owner.StateVitals?.CaptureHitStop(config.hitStop, false) ?? config.hitStop : default;
                result=runtime.Request(frozen,label,owner.StateAuthority,owner.IsPaused,hitStop);
                if(!result.Accepted)rollbackSource?.Invoke();
                return result.Accepted?result.Fact.ActionId:Guid.Empty;
            },out var rejection))
            {lastFact="Rejected: "+rejection;return false;}
            if(result.PreviousTerminal!=null)ObserveFact(result.PreviousTerminal);
            ClearBuffer();autonomousAction=false;autonomousTarget=null;AttackStage=0;
            BindPostureRecovery(result.Fact.ActionId, phase, config.postureRecoveryEvent);
            (owner.StatePresentation as ICharacterConfiguredAnimation)?.CapturePhase(result.Fact.ActionId,phase);
            ScriptableObject effectSource = ability != null ? (ScriptableObject)ability : config;
            impactRun = ActionImpactRun.Accept(owner, effectSource, ability != null ? ability.effects : config.effects,
                result.Fact.ActionId, effectAim ?? owner.StateOrienter.AttackDirection, (equipmentSource ?? "") + "/" + effectSource.name);
            ObserveFact(result.Fact);owner.StateOrienter.FaceAttackDirection();actionId=result.Fact.ActionId;return true;
        }
        internal bool CancelOwnedMainAction(Guid actionId,string reason)
        {
            if(runtime==null||!runtime.IsActive||runtime.ActionId!=actionId)return false;
            animationGeneration++;autonomousAction=false;autonomousTarget=null;ClearBuffer();
            var terminal=runtime.Cancel(reason);if(terminal!=null)ObserveFact(terminal);return true;
        }
        public void RefreshMainProfile()
        {
            if(owner.StateMainAction==null||!owner.StateMainAction.RefreshWhenIdle())return;
            RebuildMainSequence();
        }
#if UNITY_EDITOR
        public void RebindLaboratorySequence()
        {
            if(owner == null || owner.ActionActive) return;
            owner.StateMainAction?.InvalidateLaboratoryProfile();
            owner.StateMainAction?.RefreshWhenIdle();
            RebuildMainSequence();
        }
#endif
        private void RebuildMainSequence()
        {
            ClearBuffer();
            var main = owner.StateMainAction;
            var sequence = main?.Sequence;
            if (sequence != null)
            {
                configuredAttacks = new BasicAttackDefinition[sequence.Count];
                for (int i = 0; i < sequence.Count; i++) configuredAttacks[i] = sequence.Stage(i).Timeline;
                definition = configuredAttacks[0];
                SetCombo(main.ComboSource, new BasicComboSettings((float)sequence.BufferSeconds,
                    (float)sequence.ContinuationSeconds, sequence.RequireHit), sequence.Count);
            }
            else if (owner.StateCharacterDefinition != null)
            {
                if (ActionLibrary.Length == 0) throw new ArgumentException("Character basic attacks required");
                configuredAttacks = Array.ConvertAll(ActionLibrary, a => a.FreezeTiming(owner.Definition?.eventCalibration));
                definition = configuredAttacks[0];
                var chainSource = owner.StateCharacterDefinition;
                var effective = chainSource;
#if UNITY_EDITOR
                effective = AttackLabBindings.Resolve(owner, chainSource);
#endif
                SetCombo(chainSource, effective.ComboSettings, configuredAttacks.Length);
            }
            else
            {
                configuredAttacks = null;
                if (chainProfile != null && chainProfile.isActiveAndEnabled)
                {
                    definition = chainProfile.stage1.Freeze();
                    secondDefinition = chainProfile.stage2.Freeze();
                    SetCombo(chainProfile, chainProfile.ComboSettings, 2);
                }
                else
                {
                    definition = new BasicAttackDefinition(owner.StateReleaseAt, owner.StateMoveReadyAt,
                        owner.StateAttackReadyAt, owner.StateFinishAt, owner.StateDamage, owner.StatePostureImpact);
                    chain = null;
                    ComboSource = null;
                    EffectiveComboSettings = null;
                }
            }
        }

        private void SetCombo(UnityEngine.Object source, BasicComboSettings settings, int stageCount)
        {
            chain = settings.CreateState(stageCount);
            ComboSource = source;
            EffectiveComboSettings = settings;
        }
        private void RequestStage(int stage, CombatVitals aiTarget = null)
        {
            if(owner.StateMainAction!=null&&!owner.StateMainAction.AllowsBasicRequest){lastFact="Rejected: full-combo-recovery";return;}
            if(configuredAttacks!=null&&(stage<1||stage>configuredAttacks.Length)){lastFact="Rejected: invalid-stage";return;}
            CharacterMainAction.PreparedStage prepared=null;
            if(owner.StateMainAction?.Sequence!=null&&!owner.StateMainAction.TryPrepare(stage-1,out prepared)){lastFact=owner.StateMainAction.LastResult;return;}
            var actionConfig=prepared!=null?prepared.Definition:owner.StateCharacterDefinition==null?null:ActionLibrary[stage-1];
            var selected=stage==2?secondDefinition:definition;
            ConfiguredActionSnapshot phase=prepared?.Phase;
            if (phase == null && actionConfig != null && !TryResolveConfiguredAction(actionConfig, out phase,
                out var configurationError, new CharacterAnimationPreparation(true, stage, useAuthoredLogic: true)))
            {
                lastFact = "Rejected: " + configurationError;
                return;
            }
            if(phase==null&&owner.StatePresentation is ICharacterDefaultActionAnimation defaults)
            {
                try{phase=defaults.FreezeDefaultAction(selected,stage);}catch(ArgumentException error){lastFact="Rejected: "+error.Message;return;}
            }
            if(phase!=null){if(!ValidatePrepared(phase,out var phaseError)){lastFact="Rejected: "+phaseError;return;}selected=phase.Timeline;}
            var signature=owner.Actions.CreateRequest(CharacterActionKind.Main,aiTarget==null?CharacterActionSource.Player:CharacterActionSource.Companion,aiTarget==null?"player-input:Stage"+stage:"companion:Stage1").WithPostureTarget(
                actionConfig == null || actionConfig.actionPostureExposure == -1 ? 20 : actionConfig.actionPostureExposure);
            ActionRequestResult request=null;
            var authoredStop = actionConfig?.hitStop ?? default;
            var hitStop = owner.StateVitals?.CaptureHitStop(authoredStop, stage == 1 && selected.EventDriven) ?? authoredStop;
            var accepted=owner.Actions.TryCommit(signature,owner.StateStamina==null?0:prepared!=null?prepared.Cost:actionConfig!=null?actionConfig.staminaCost:owner.StateStamina.AttackCost,()=>
            {
                request=runtime.Request(selected,aiTarget==null?"player-input:Stage"+stage:"companion:Stage1",owner.StateAuthority,owner.IsPaused,hitStop);
                return request.Accepted?request.Fact.ActionId:Guid.Empty;
            },out var rejection);
            if(!accepted){lastFact="Rejected: "+rejection;return;}
            if(request.PreviousTerminal!=null)ObserveFact(request.PreviousTerminal);
            if(request.Accepted)
            {
                var marker = string.IsNullOrWhiteSpace(actionConfig?.postureRecoveryEvent)
                    ? owner.PostureBasicRecoveryEvent : actionConfig.postureRecoveryEvent;
                BindPostureRecovery(request.Fact.ActionId, phase, marker);
                if(prepared!=null)
                {
                    (owner.StatePresentation as ICharacterConfiguredAnimation)?.CapturePhase(request.Fact.ActionId,prepared.Phase);
                    owner.StateMainAction.Accept(request.Fact.ActionId,prepared,aiTarget==null?owner.StateOrienter.AttackDirection:aiTarget.transform.position-owner.transform.position);
                }
                else if(phase!=null)(owner.StatePresentation as ICharacterConfiguredAnimation)?.CapturePhase(request.Fact.ActionId,phase);
                AttackStage=stage; autonomousAction=aiTarget!=null; autonomousTarget=aiTarget;
                if(autonomousAction) { chain?.Clear(); owner.StateCombatBrain.OnAccepted(); owner.StateOrienter.OrientTowards(aiTarget.transform.position); }
                else chain?.Accepted(stage,request.Fact.ActionId,runtime.SimulationTime,selected.AttackReadyAt,selected.EventDriven);
                TrackBasicAttack(request.Fact.ActionId, aiTarget == null);
                var effectDefinition = actionConfig;
                impactRun = ActionImpactRun.Accept(owner, effectDefinition, effectDefinition?.effects, request.Fact.ActionId,
                    owner.StateOrienter.AttackDirection, $"{owner.StateEquipment?.State?.RightInstanceId}/段{stage}", prepared?.Stage.DashDistance > 0);
                ObserveFact(request.Fact); owner.StateOrienter.FaceAttackDirection();
            }
            else lastFact="Rejected: "+request.Reason;
        }
        internal bool ChangeAbilityPhase(Guid id, ConfiguredActionSnapshot phase, string postureRecoveryEvent = null)
        {
            if (!ValidatePrepared(phase, out var error))
            {
                lastFact = "Rejected: " + error;
                return false;
            }
            var fact = runtime.Rephase(id, phase.Timeline);
            if (fact == null) return false;
            ((ICharacterConfiguredAnimation)owner.StatePresentation).CapturePhase(id, phase);
            // 仅绑定新阶段实际拥有的标记；切相本身继续持有同一份架势恢复锁。
            BindPostureRecovery(id, phase, postureRecoveryEvent);
            ObserveFact(fact, phase.Clip);
            return true;
        }
        public void CancelAction(string reason)
        {
            owner.StateAbilities?.CancelPending(reason);
            owner.StateDodge?.Cancel();
            owner.StateGuard?.Cancel();
            owner.StateOffhand?.CancelPending();
            animationGeneration++; autonomousAction=false; autonomousTarget=null; owner.StateCombatBrain?.ResetCandidate();
            ClearBuffer();
            // 取消必须具名；抑制仍按住的输入，不能把下一帧误判为新请求。
            intent.SuppressUntilRelease();
            var terminal = runtime == null ? null : runtime.Cancel(reason);
            if (terminal != null) ObserveFact(terminal);
        }
 }
}
