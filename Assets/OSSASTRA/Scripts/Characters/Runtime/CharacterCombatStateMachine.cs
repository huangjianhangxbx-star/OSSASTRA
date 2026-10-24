/* 脚本：CharacterCombatStateMachine
 * 功能：注册战斗状态机，唯一拥有并推进BasicActionRuntime、连段、受理快照与主动作取消。
 * 边界：不采设备、不写运动/HP；能力Run和费用账本仍由既有端口所有。
 * 协作：仲裁器按原阶段调用；Root公开方法仅转发，释放仍走同一战斗网关。
 */
using System;
using Ossastra.CharacterCore;
using Ossastra.Combat;
using UnityEngine;
namespace Ossastra.Characters
{
 public sealed class CharacterCombatStateMachine:CharacterStateMachine,IParallelCharacterStateMachine
 {
  private BasicActionRuntime runtime;
  private BasicAttackDefinition definition,secondDefinition;
  private BasicAttackDefinition[] configuredAttacks;
  private BasicChainState chain;
  private BasicChainProfile chainProfile;
  private ICombatHitReceiptSource hitReceipts;
  private CombatVitals autonomousTarget;
  private bool autonomousAction;
  private readonly AttackIntent intent=new AttackIntent();
  private string lastFact="Idle";
  private int animationGeneration;
  public int AttackStage {get;private set;}
  public int ReleaseCount {get;private set;}
  public string LastActionSource {get;private set;}="none";
  public string LastFact=>lastFact;
  public int AnimationGeneration=>animationGeneration;
  public bool HasBufferedAttack=>chain!=null&&chain.HasBuffer;
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
  private bool ChainAvailable=>owner.StateCharacterDefinition!=null||(chainProfile!=null&&chainProfile.isActiveAndEnabled);
  private ActionDefinitionSO[] ActionLibrary=>owner.StateActionLibrary;
  public override void Initialize(CharacterRoot root)
  {
   base.Initialize(root);
   definition=new BasicAttackDefinition(root.StateReleaseAt,root.StateMoveReadyAt,root.StateAttackReadyAt,root.StateFinishAt,root.StateDamage,root.StatePostureImpact);
   runtime=new BasicActionRuntime(root.ActorId);chainProfile=root.GetComponent<BasicChainProfile>();
   var config=root.Definition;
   if(config!=null){if(ActionLibrary.Length==0)throw new ArgumentException("Character basic attacks required");configuredAttacks=Array.ConvertAll(ActionLibrary,a=>a.Freeze());definition=configuredAttacks[0];chain=new BasicChainState(config.preInput,config.bufferLifetime,config.continuationWindow,configuredAttacks.Length,config.requireHitForContinuation);}
   else if(chainProfile!=null&&chainProfile.enabled){definition=chainProfile.stage1.Freeze();secondDefinition=chainProfile.stage2.Freeze();chain=chainProfile.CreateState();}
   if(chain!=null){hitReceipts=root.StateMelee;hitReceipts.Resolved+=OnHitReceipt;}
  }
  public void ClearBuffer()=>chain?.Clear();
  public void SuppressInput()=>intent.SuppressUntilRelease();
  public void OnHitReceipt(Guid id,bool applied){if(!autonomousAction)chain?.Hit(id,applied);}
  public void Dispose(){if(hitReceipts!=null)hitReceipts.Resolved-=OnHitReceipt;hitReceipts=null;}
  public void RequestInput(CharacterInputFrame frame)
  {
            var pressed=frame.hasAttackEdges?intent.Observe(frame.attackHeld,frame.attackPressed):intent.Observe(frame.attackHeld);
            if (pressed && !frame.attackRequestsBlocked && owner.StateFocused && !frame.attackPointerBlocked)
            {
                var stage=chain==null?1:chain.Request(runtime.SimulationTime,runtime.AttackReady,
                    owner.StateInput.HasPlayerInput && !owner.IsPaused && owner.StateAuthority.Allows(ControlChannel.Attack) && ChainAvailable && (owner.StateMainAction==null||owner.StateMainAction.AllowsBasicRequest));
                if(stage>0) RequestStage(stage);
            }

  }
  public void RequestCompanion()
  {
            var inventoryAccepted=owner.StateInventoryConsumer!=null&&owner.StateInventoryConsumer.TickCandidate();
            if(!inventoryAccepted&&!owner.StateInput.HasPlayerInput && owner.CanSimulateIntent && owner.StateCombatBrain!=null &&
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
            foreach (var fact in runtime.Advance(deltaTime, owner.IsPaused || !owner.StateFocused))
            {
                ObserveFact(fact);
                if (fact.Release != null)
                {
                    ReleaseCount++;
                    // 在释放当帧按值采样方向；不是接受动作时锁定，前摇中仍可重新瞄准。
                    var releaseDirection = owner.StateOrienter.AttackDirection;
                    owner.StateOrienter.FaceAttackDirection();
                    if(owner.StateAbilities!=null&&owner.StateAbilities.OwnsRelease(fact.ActionId))continue;
                    if(owner.StateMainAction!=null&&owner.StateMainAction.TryRelease(fact,releaseDirection))continue;
                    if(owner.StateSkills==null||!owner.StateSkills.TryHandleRelease(fact.Release,releaseDirection))owner.StateHitResolver.Resolve(fact.Release, releaseDirection);
                }
            }

  }
  public void CompleteFrame(float deltaTime)
  {
            owner.StateMainAction?.TickEffects(owner.IsSimulationSuspended?0:deltaTime);
            if(chain!=null && owner.StateInput.HasPlayerInput && !owner.IsPaused && owner.StateFocused && owner.StateAuthority.Allows(ControlChannel.Attack))
            {
                var bufferedStage=chain.Poll(runtime.SimulationTime,runtime.AttackReady);
                if(bufferedStage>0) RequestStage(bufferedStage);
            }

  }
  public void PrepareFrame(CharacterInputFrame frame)
  {if(chain!=null&&(!owner.HasPlayerInput||owner.IsPaused||!owner.StateFocused||!ChainAvailable))chain.Clear();}
        public void ObserveFact(ActionFact fact)
        {
            if(fact.Kind==ActionFactKind.Accepted)owner.StateVitals?.Recovery?.RetainParent(fact.ActionId);
            LastActionSource=fact.Source;
            lastFact = fact.Kind + " @" + fact.At.ToString("F3") + " " + fact.ActionId.ToString("N").Substring(0, 8);
            if (fact.Reason != null) lastFact += " " + fact.Reason;
            if (owner.StateVerboseTrace) Debug.Log("Character " + lastFact, owner);
            owner.StateArbitrator.ObserveCombatEdge();
            owner.StateAbilities?.ObserveFact(fact);
            owner.StateMainAction?.ObserveFact(fact);
            owner.PublishActionFact(fact);
            if(fact.Kind==ActionFactKind.Finished||fact.Kind==ActionFactKind.Cancelled)owner.StateVitals?.Recovery?.ReleaseParent(fact.ActionId);
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
        internal bool TryAbilityBodyAction(ActionDefinitionSO config,CharacterActionSource source,string label,float cost,out Guid actionId,Func<bool> prepareSource=null,Action rollbackSource=null,ConfiguredActionSnapshot phase=null,CharacterAnimationPreparation? preparation=null)
        {
            actionId=Guid.Empty;if(config==null||!owner.StateReady)return false;
            BasicAttackDefinition frozen;
            if(phase==null&&!TryResolveConfiguredAction(config,out phase,out var configurationError,preparation)){lastFact="Rejected: "+configurationError;return false;}
            if(!ValidatePrepared(phase,out var phaseError)){lastFact="Rejected: "+phaseError;return false;}
            frozen=phase.Timeline;
            var signature=owner.Actions.CreateRequest(CharacterActionKind.Main,source,label);
            ActionRequestResult result=null;
            if(!owner.Actions.TryCommit(signature,cost,()=>
            {
                if(prepareSource!=null&&!prepareSource())return Guid.Empty;
                result=runtime.Request(frozen,label,owner.StateAuthority,owner.IsPaused);
                if(!result.Accepted)rollbackSource?.Invoke();
                return result.Accepted?result.Fact.ActionId:Guid.Empty;
            },out var rejection))
            {lastFact="Rejected: "+rejection;return false;}
            if(result.PreviousTerminal!=null)ObserveFact(result.PreviousTerminal);
            chain?.Clear();autonomousAction=false;autonomousTarget=null;AttackStage=0;
            (owner.StatePresentation as ICharacterConfiguredAnimation)?.CapturePhase(result.Fact.ActionId,phase);
            ObserveFact(result.Fact);owner.StateOrienter.FaceAttackDirection();actionId=result.Fact.ActionId;return true;
        }
        internal bool CancelOwnedMainAction(Guid actionId,string reason)
        {
            if(runtime==null||!runtime.IsActive||runtime.ActionId!=actionId)return false;
            animationGeneration++;autonomousAction=false;autonomousTarget=null;chain?.Clear();
            var terminal=runtime.Cancel(reason);if(terminal!=null)ObserveFact(terminal);return true;
        }
        public void RefreshMainProfile()
        {
            if(owner.StateMainAction==null||!owner.StateMainAction.RefreshWhenIdle())return;
            var sequence=owner.StateMainAction.Sequence;
            if(sequence!=null)
            {
                configuredAttacks=new BasicAttackDefinition[sequence.Count];
                for(int i=0;i<sequence.Count;i++)configuredAttacks[i]=sequence.Stage(i).Timeline;
                definition=configuredAttacks[0];chain=new BasicChainState(sequence.BufferSeconds,sequence.BufferSeconds,sequence.ContinuationSeconds,sequence.Count,false);
            }
            else if(owner.StateCharacterDefinition!=null)
            {
                configuredAttacks=Array.ConvertAll(ActionLibrary,a=>a.Freeze());definition=configuredAttacks[0];
                chain=new BasicChainState(owner.StateCharacterDefinition.preInput,owner.StateCharacterDefinition.bufferLifetime,owner.StateCharacterDefinition.continuationWindow,configuredAttacks.Length,owner.StateCharacterDefinition.requireHitForContinuation);
            }
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
            if(phase==null&&actionConfig!=null&&!TryResolveConfiguredAction(actionConfig,out phase,out var configurationError,new CharacterAnimationPreparation(true,stage))){lastFact="Rejected: "+configurationError;return;}
            if(phase==null&&owner.StatePresentation is ICharacterDefaultActionAnimation defaults)
            {
                try{phase=defaults.FreezeDefaultAction(selected,stage);}catch(ArgumentException error){lastFact="Rejected: "+error.Message;return;}
            }
            if(phase!=null){if(!ValidatePrepared(phase,out var phaseError)){lastFact="Rejected: "+phaseError;return;}selected=phase.Timeline;}
            var signature=owner.Actions.CreateRequest(CharacterActionKind.Main,aiTarget==null?CharacterActionSource.Player:CharacterActionSource.Companion,aiTarget==null?"player-input:Stage"+stage:"companion:Stage1");
            ActionRequestResult request=null;
            var accepted=owner.Actions.TryCommit(signature,owner.StateStamina==null?0:prepared!=null?prepared.Cost:actionConfig!=null?actionConfig.staminaCost:owner.StateStamina.AttackCost,()=>
            {
                request=runtime.Request(selected,aiTarget==null?"player-input:Stage"+stage:"companion:Stage1",owner.StateAuthority,owner.IsPaused);
                return request.Accepted?request.Fact.ActionId:Guid.Empty;
            },out var rejection);
            if(!accepted){lastFact="Rejected: "+rejection;return;}
            if(request.PreviousTerminal!=null)ObserveFact(request.PreviousTerminal);
            if(request.Accepted)
            {
                if(prepared!=null)
                {
                    (owner.StatePresentation as ICharacterConfiguredAnimation)?.CapturePhase(request.Fact.ActionId,prepared.Phase);
                    owner.StateMainAction.Accept(request.Fact.ActionId,prepared,aiTarget==null?owner.StateOrienter.AttackDirection:aiTarget.transform.position-owner.transform.position);
                }
                else if(phase!=null)(owner.StatePresentation as ICharacterConfiguredAnimation)?.CapturePhase(request.Fact.ActionId,phase);
                AttackStage=stage; autonomousAction=aiTarget!=null; autonomousTarget=aiTarget;
                if(autonomousAction) { chain?.Clear(); owner.StateCombatBrain.OnAccepted(); owner.StateOrienter.OrientTowards(aiTarget.transform.position); }
                else chain?.Accepted(stage,request.Fact.ActionId,runtime.SimulationTime,selected.AttackReadyAt);
                ObserveFact(request.Fact); owner.StateOrienter.FaceAttackDirection();
            }
            else lastFact="Rejected: "+request.Reason;
        }
        internal bool ChangeAbilityPhase(Guid id,ConfiguredActionSnapshot phase)
        {
            if(!ValidatePrepared(phase,out var error)){lastFact="Rejected: "+error;return false;}
            var fact=runtime.Rephase(id,phase.Timeline);if(fact==null)return false;
            ((ICharacterConfiguredAnimation)owner.StatePresentation).CapturePhase(id,phase);ObserveFact(fact);return true;
        }
        public void CancelAction(string reason)
        {
            owner.StateAbilities?.CancelPending(reason);
            owner.StateDodge?.Cancel();
            owner.StateGuard?.Cancel();
            owner.StateOffhand?.CancelPending();
            animationGeneration++; autonomousAction=false; autonomousTarget=null; owner.StateCombatBrain?.ResetCandidate();
            chain?.Clear();
            // 取消必须具名；抑制仍按住的输入，不能把下一帧误判为新请求。
            intent.SuppressUntilRelease();
            var terminal = runtime == null ? null : runtime.Cancel(reason);
            if (terminal != null) ObserveFact(terminal);
        }
 }
}
