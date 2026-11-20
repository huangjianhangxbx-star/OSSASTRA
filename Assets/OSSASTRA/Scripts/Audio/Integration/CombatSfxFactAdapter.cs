/* 脚本：CombatSfxFactAdapter
 * 功能：按真实释放/接触及冻结语义选声，统一提交到唯一音频根。
 * 边界：只读战斗，不写输入/HP/动画；没有自有播放器或第二个动作时钟。
 * 协作：接受时缓存招式；接触保留子击/目标身份；同帧释放先于接触声提交，玩法顺序不变。
 */
using Ossastra.CharacterAnimation;
using System;using System.Collections.Generic;using System.Linq;
using Ossastra.CharacterCore;using Ossastra.Characters;using Ossastra.Combat;using UnityEngine;
using ActorRoot=Ossastra.Characters.CharacterRoot;using EnemyEntityRoot=Ossastra.Enemies.EnemyRoot;
namespace Ossastra.Audio
{
 [DefaultExecutionOrder(260),DisallowMultipleComponent]
 public sealed class CombatSfxFactAdapter:MonoBehaviour
 {
  private sealed class ActionSound
  {
   public string selector;
   public int stage;
   public CombatSoundBinding[] shared;
  }
  private sealed class Observation
  {
   public CharacterAnimationController animation;
   public ActorRoot actor;public EnemyEntityRoot enemy;public CombatVitals vitals;public CharacterAbilities abilities;public CharacterMainAction main;public CharacterDodge dodge;
   public SfxSourceHandle source;public int hp,generation;public Guid dodgeId;public long sequence;public bool connected;
   public readonly List<Action> remove=new List<Action>();
   public readonly Dictionary<Guid,ActionSound> actions=new Dictionary<Guid,ActionSound>();public readonly Queue<Guid> actionOrder=new Queue<Guid>();
   public readonly HashSet<ActionFact> facts=new HashSet<ActionFact>();public readonly Queue<ActionFact> factOrder=new Queue<ActionFact>();
   public Component Entity=>(Component)actor??enemy;
  }
  private sealed class Pending
  {
   public Observation owner;
   public SfxSourceHandle lease;
   public SfxEventDefinitionSO definition;
   public SfxPlaybackSettings? playback;
   public string cue,key,kind,prefix,confirmedSource;
   public Vector3 position;
   public int order,emission;
   public Guid action,parent;
   public double occurredAt,dueAt;
  }
  private sealed class ConfirmedLease { public SfxSourceHandle source; public double end; }
  [SerializeField,Tooltip("语义配置只选内容，不决定动作；空引用时读取统一Resources入口。")]
  private CombatSoundProfileSO soundProfile;
  private readonly List<Observation> observed=new List<Observation>();private readonly List<Pending> pending=new List<Pending>();
  private readonly Dictionary<(Guid,int),Vector3> contactPositions=new Dictionary<(Guid,int),Vector3>();
  private readonly Queue<(Guid,int)> contactOrder=new Queue<(Guid,int)>();
  private readonly Dictionary<string,ConfirmedLease> confirmedLeases = new Dictionary<string,ConfirmedLease>();
  private readonly HashSet<(Guid,int)> customAbilityHits = new HashSet<(Guid,int)>();
  private GameAudioRoot audioRoot;private CharacterControlCoordinator coordinator;private HealingBottleService bottle;
  private ActorRoot previousCurrent;private SfxSourceHandle interfaceSource;private string world;private float nextDiscovery;private int bottleUses;private long uiSequence;
  public int FactSubmissions{get;private set;}public int Accepted{get;private set;}public int Rejected{get;private set;}
  public int Releases{get;private set;}public int Hits{get;private set;}public int Hurts{get;private set;}
  public int UnmappedFacts{get;private set;}public string LastUnmapped{get;private set;}="无";
  public CombatSoundProfileSO SoundProfile=>soundProfile;
  public event Action<CombatSoundFact> SoundFactObserved;
  public string LastFact{get;private set;}="尚未观察事实";public string Status{get;private set;}="等待音频根";
  public int ActorSources=>observed.Count(o=>o.actor!=null&&o.connected);public int EnemySources=>observed.Count(o=>o.enemy!=null&&o.connected);
  private void OnEnable(){TryInitialize();}private void Start(){TryInitialize();}
  private bool InOwnScene(Component c)=>c!=null&&c.gameObject.scene==gameObject.scene;
  private bool TryInitialize()
  {
   if(!Application.isPlaying||!isActiveAndEnabled)return false;var root=GameAudioRoot.Instance;
   if(root==null||!root.SfxAvailable){Status="统一音频根不可用";return false;}
   if(audioRoot==root&&world==root.WorldScopeId&&interfaceSource!=null&&!interfaceSource.IsDisposed)return root.TryClaimSfxFacts(this);
   DisconnectAll();if(!root.TryClaimSfxFacts(this)){Status="拒绝重复事实观察者";return false;}
   if(root.CombatSoundProfile!=null)soundProfile=root.CombatSoundProfile;
   if(soundProfile==null||!soundProfile.Validate(out _)){root.ReleaseSfxFacts(this);Status="缺少有效音效语义配置；不回退为通用提示音";return false;}
   audioRoot=root;world=root.WorldScopeId;interfaceSource=root.RegisterSfxSource("sound-interface@"+GetInstanceID());
   coordinator=FindObjectsOfType<CharacterControlCoordinator>().FirstOrDefault(c=>c.isActiveAndEnabled&&InOwnScene(c));
   bottle=FindObjectsOfType<HealingBottleService>().FirstOrDefault(c=>c.isActiveAndEnabled&&InOwnScene(c));
   previousCurrent=coordinator==null?null:coordinator.Current;bottleUses=bottle==null?0:bottle.UseCount;
   CombatContactGateway.Resolved+=OnContact;SideViewFeedbackRuntime.TrialReset+=OnTrialReset;
   root.ContentProfileApplied+=OnProfileApplied;Discover();foreach(var o in observed)Connect(o);Status="真实语义 → 精选内容；未匹配留白";return true;
  }
  private void OnProfileApplied(){if(isActiveAndEnabled)TryInitialize();}
  private void Discover()
  {
   foreach(var actor in FindObjectsOfType<ActorRoot>().Where(InOwnScene))if(!observed.Any(o=>o.actor==actor))
    observed.Add(new Observation{actor=actor,vitals=actor.Vitals,abilities=actor.GetComponent<CharacterAbilities>(),main=actor.GetComponent<CharacterMainAction>(),dodge=actor.GetComponent<CharacterDodge>()});
   foreach(var enemy in FindObjectsOfType<EnemyEntityRoot>().Where(InOwnScene))if(!observed.Any(o=>o.enemy==enemy))observed.Add(new Observation{enemy=enemy,vitals=enemy.Vitals});
   nextDiscovery=Time.unscaledTime+.5f;
  }
  private static bool EntityActive(Observation o)=>o.actor!=null?o.actor.isActiveAndEnabled:o.enemy!=null&&o.enemy.isActiveAndEnabled;
  private bool CanObserve(Observation o)=>isActiveAndEnabled&&o.connected&&EntityActive(o)&&
   (o.actor==null?!o.enemy.IsSimulationSuspended:!o.actor.IsSimulationSuspended)&&Time.timeScale>0&&o.source!=null&&!o.source.IsDisposed;
  private void Connect(Observation o)
  {
   if(o.connected||o.Entity==null||!o.Entity.gameObject.activeInHierarchy||o.vitals?.Health==null)return;
   if(o.actor!=null&&!o.actor.isActiveAndEnabled||o.enemy!=null&&!o.enemy.isActiveAndEnabled)return;
   o.source=audioRoot.RegisterSfxSource(o.vitals.Health.Id+"@"+o.Entity.GetInstanceID());if(o.source==null)return;
   o.connected=true;o.hp=o.vitals.Health.Current;o.generation=o.actor==null?0:o.actor.LifeGeneration;o.dodgeId=o.dodge==null?Guid.Empty:o.dodge.ActionId;
   Action<HitOutcome> died=f=>{if(o.Entity!=null)Queue(o,"target","death",0,"","death:"+o.generation+":"+(++o.sequence),o.Entity.transform.position,confirmed:true);};
   o.vitals.Died+=died;o.remove.Add(()=>{if(o.vitals!=null)o.vitals.Died-=died;});
   if(o.actor!=null)
   {
    o.animation=o.actor.StatePresentation as CharacterAnimationController;
    if(o.animation!=null)
    {
     Action<AnimationSignal> audio=s=>{
      if(!CanObserve(o)||!s.Calibrated||s.Purpose!=AnimationSignalPurpose.Sound||s.Meaning==AnimationEventMeaning.Footstep||!s.Binding.sound)return;
      var key="anim:"+s.Generation+":"+s.Sequence;
      Record(o.vitals.Health.Id.ToString(),key,o.source.SubmitClip(s.Binding.sound,key,o.animation.AnchorPosition(s.Binding),s.Binding.volume));
     };
     o.animation.SpineSignal+=audio;
     o.remove.Add(()=>{if(o.animation!=null)o.animation.SpineSignal-=audio;});
    }
   }
   Action<ActionFact> action=f=>OnAction(o,f);
   if(o.actor!=null){o.actor.ActionObserved+=action;o.remove.Add(()=>{if(o.actor!=null)o.actor.ActionObserved-=action;});}
   else{o.enemy.ActionObserved+=action;o.remove.Add(()=>{if(o.enemy!=null)o.enemy.ActionObserved-=action;});}
   if(o.abilities!=null){Action<AbilityFeedbackFact> feedback=f=>OnAbility(o,f);o.abilities.FeedbackObserved+=feedback;o.remove.Add(()=>{if(o.abilities!=null)o.abilities.FeedbackObserved-=feedback;});}
  }
  private void OnAction(Observation o,ActionFact f)
  {
   if(!CanObserve(o)||f==null||!o.facts.Add(f))return;o.factOrder.Enqueue(f);if(o.factOrder.Count>128)o.facts.Remove(o.factOrder.Dequeue());
   if(f.Kind==ActionFactKind.Accepted)
   {
    var source=f.Source??"";string selector=source.StartsWith("ability:",StringComparison.Ordinal)?source.Substring(8):
     o.main!=null&&o.main.ExecutionId==f.ActionId&&!string.IsNullOrEmpty(o.main.SelectedId)?"main:"+o.main.SelectedId:o.enemy==null?"body.basic":"enemy.basic";
    o.actions[f.ActionId]=new ActionSound{selector=selector,stage=o.actor==null?1:o.actor.AttackStage,
        shared=soundProfile.FreezeFor(selector)};
    o.actionOrder.Enqueue(f.ActionId);
    while(o.actionOrder.Count>256)o.actions.Remove(o.actionOrder.Dequeue());
   }
   // 能力离手和身体由丰富反馈唯一出声。即使Run结束，Root的Source也保留其身份。
   if(f.Kind!=ActionFactKind.Released||(f.Source??"").StartsWith("ability:",StringComparison.Ordinal)||o.abilities!=null&&o.abilities.OwnsRelease(f.ActionId))return;
   if(o.animation!=null && o.animation.CustomReleaseSound==f.ActionId)return;
   if(o.actions.TryGetValue(f.ActionId,out var sound))Queue(o,sound.selector,"release",sound.stage,"","release:"+f.ActionId.ToString("N"),o.Entity.transform.position,action:f.ActionId,parent:f.Release?.ParentExecutionId??f.ActionId,emission:f.Release?.EmissionIndex??-1,
       frozen:sound.shared);
  }
  private void OnAbility(Observation o,AbilityFeedbackFact f)
  {
   if(f==null||!CanObserve(o))return;
   if(f.Kind=="release" && o.animation!=null && o.animation.CustomReleaseSound==f.ExecutionId)return;
   if(f.Kind=="accepted"||f.Kind=="cancel"||f.Kind=="end")return;
   if(f.Kind=="hit" && customAbilityHits.Contains((f.ExecutionId,f.EmissionIndex))) return;
   var selector=string.IsNullOrEmpty(f.DefinitionId)?"resource":f.DefinitionId;
   string key="ability:"+f.ExecutionId.ToString("N")+":"+f.EmissionIndex+":"+f.Channel+":"+f.Kind;
   if(f.ExecutionId==Guid.Empty||f.EmissionIndex<0)key+=":"+(++o.sequence);
   // hit/miss按每个投递节点返回；只有SO明确登记爆炸时，空落也允许真实爆炸声，绝不冒充命中。
   var position=f.Position;
   if((f.Kind=="hit"||f.Kind=="miss")&&contactPositions.TryGetValue((f.ExecutionId,f.EmissionIndex),out var actual))position=actual;
   o.actions.TryGetValue(f.ExecutionId, out var sound);
   Queue(o,selector,f.Kind,0,f.Channel,key,position,f.DeliveryMode,action:f.ExecutionId,parent:f.ExecutionId,emission:f.EmissionIndex,
       frozen:sound != null && sound.selector == selector ? sound.shared : null);
  }
  private void OnContact(CombatContactBatch batch)
  {
   if(batch==null||audioRoot==null||audioRoot.WorldScopeId!=world)return;
   var contactKey=(batch.ParentExecutionId,batch.EmissionIndex);
   if(!contactPositions.ContainsKey(contactKey))contactOrder.Enqueue(contactKey);
   contactPositions[contactKey]=batch.Origin;
   while(contactOrder.Count>256)
   {
    var old = contactOrder.Dequeue();
    contactPositions.Remove(old);
    customAbilityHits.Remove(old);
   }
   var source=observed.FirstOrDefault(o=>o.connected&&o.vitals==batch.Source);
   // 只消费生产方明确发布的效果语义；不能把任意周期伤害冒充冰效果。
   if(source!=null&&CanObserve(source)&&!string.IsNullOrEmpty(batch.EffectSemantic)&&batch.Receipts.Any(r=>r.Outcome.Reason==HitReason.Applied))
    Queue(source,batch.EffectSemantic,"hit",0,"","effect:"+batch.ActionId+":"+batch.EmissionIndex,batch.Origin,action:batch.ActionId,parent:batch.ParentExecutionId,emission:batch.EmissionIndex);
   foreach(var receipt in batch.Receipts)
   {
    var hit=receipt.Outcome;if(hit==null)continue;
    string key="contact:"+batch.ActionId.ToString("N")+":"+batch.EmissionIndex+":"+hit.TargetId;
    var result = ContactResult(hit.Reason);
    var settings = result.HasValue
        ? SideViewFeedbackRuntime.Contact(batch, result.Value, receipt.Material, out _ )?.settings : null;
    bool authored = settings != null && settings.overrideSound;
    if (authored && source != null)
    {
     if (result == SideViewContactResult.Hit) customAbilityHits.Add(contactKey);
     QueueAuthored(source, settings, result.Value.ToString(), key, receipt.Contact.Position, batch);
    }
    // 能力的hit/miss拥有实际channel和投递语义；这里只接管普通近战，未知冰路脉冲不会变成重刀音。
    if(!authored&&batch.Cause==CombatContactCause.Direct&&source!=null&&source.actions.TryGetValue(batch.ParentExecutionId,out var sound)&&
      (sound.selector=="body.basic"||sound.selector=="enemy.basic"||sound.selector.StartsWith("main:",StringComparison.Ordinal)))
    {
     if(hit.Reason==HitReason.Applied)Queue(source,sound.selector,"hit",sound.stage,batch.Channel,key,receipt.Contact.Position,
         batch.DeliveryMode,receipt.Material,batch.ActionId,batch.ParentExecutionId,batch.EmissionIndex,
         confirmed:hit.HpBefore>0 && hit.HpAfter==0,frozen:sound.shared);
    }
    var target=observed.FirstOrDefault(o=>o.connected&&o.vitals==receipt.Target);
    if(target==null||!CanObserve(target))continue;
    if(hit.Reason==HitReason.Applied&&hit.HpAfter>0&&hit.HpAfter<hit.HpBefore)Queue(target,"target","hurt",0,"",key,receipt.Position,action:batch.ActionId,parent:batch.ParentExecutionId,emission:batch.EmissionIndex);
    if(hit.Reason==HitReason.Guarded&&!authored)Queue(target,"target","guard",0,"",key,receipt.Position,action:batch.ActionId,parent:batch.ParentExecutionId,emission:batch.EmissionIndex);
    // 同批已经有精确损血，轮询只作为其它来源的兼容观察；不重复播同一笔HP下降。
    target.hp=target.vitals.Health.Current;
   }
   if (source != null && batch.EnvironmentContact.HasValue)
   {
    var contact = batch.EnvironmentContact.Value;
    var settings = SideViewFeedbackRuntime.Contact(batch, SideViewContactResult.Surface, contact.Material, out _ )?.settings;
    if (settings?.overrideSound == true)
     QueueAuthored(source, settings, "surface", "surface:"+batch.ActionId+":"+batch.EmissionIndex, contact.Position, batch);
   }
  }
  private static SideViewContactResult? ContactResult(HitReason reason)
  {
   switch (reason)
   {
    case HitReason.Applied: return SideViewContactResult.Hit;
    case HitReason.Guarded: return SideViewContactResult.Guard;
    case HitReason.Evaded: return SideViewContactResult.Evade;
    default: return null;
   }
  }

  private void QueueAuthored(Observation o, SideViewFeedbackSettings settings, string kind, string key, Vector3 position,
      CombatContactBatch batch)
  {
   var definition = settings.sound;
   if (definition == null || o.source == null || pending.Count >= 512) return;
   double now = Time.realtimeSinceStartupAsDouble;
   pending.Add(new Pending
   {
    owner=o, definition=definition, cue=definition.eventId, key=key, kind=kind.ToLowerInvariant(), position=position,
    action=batch.ActionId, parent=batch.ParentExecutionId, emission=batch.EmissionIndex, occurredAt=now,
    dueAt=now+Math.Max(0,settings.soundDelayMs)/1000.0, confirmedSource=ConfirmedKey(o),
    playback=new SfxPlaybackSettings(Mathf.Clamp01(settings.SourceVolume * settings.volume),
        settings.pitchMin,settings.pitchMax,settings.soundStartMs)
   });
  }

  private void Queue(Observation o,string selector,string kind,int stage,string channel,string key,Vector3 position,string deliveryMode=null,string material=null,Guid action=default,Guid parent=default,int emission=-1,bool confirmed=false,CombatSoundBinding[] frozen=null)
  {
   if(o.source==null || (!confirmed&&!CanObserve(o)))return;
   var prefix=Prefix(o);
   if(SoundFactObserved!=null){var fact=new CombatSoundFact(o.source.SourceId,prefix.TrimEnd('.'),selector,kind,stage,channel,deliveryMode,material,key,position,Time.realtimeSinceStartupAsDouble);foreach(Action<CombatSoundFact> observer in SoundFactObserved.GetInvocationList())try{observer(fact);}catch(Exception error){Debug.LogWarning("AUDIO_SEMANTIC_OBSERVER: "+error.Message);}}
   var role=prefix.TrimEnd('.');
   bool mapped = frozen == null
       ? soundProfile.TryResolve(selector,kind,stage,channel,out var cue,deliveryMode,role,material)
       : CombatSoundProfileSO.TryResolve(frozen,selector,kind,stage,channel,out cue,deliveryMode,role,material);
   if(!mapped){UnmappedFacts++;LastUnmapped=selector+" / "+kind+" / "+channel+" / "+deliveryMode;return;}
   if(pending.Count>=512){Rejected++;return;}
   double now = Time.realtimeSinceStartupAsDouble;
   pending.Add(new Pending{owner=o,lease=o.source,prefix=prefix,cue=cue,key=key,kind=kind,position=position,
    action=action,parent=parent,emission=emission,order=kind=="release"||kind=="launch"?0:1,
    occurredAt=now,dueAt=now,confirmedSource=confirmed?ConfirmedKey(o):null});
  }
  private string Prefix(Observation o)
  {
   if(o.enemy!=null)return "enemy.";
   bool player=coordinator!=null&&coordinator.Characters.Contains(o.actor)?coordinator.Current==o.actor||previousCurrent==o.actor&&!o.actor.IsAlive:o.actor.GetComponent<CharacterInputHandler>()?.HasPlayerControl==true;
   return player?"player.":"companion.";
  }
  private void Flush()
  {
   double now = Time.realtimeSinceStartupAsDouble;
   foreach (var key in confirmedLeases.Where(pair => pair.Value.end <= now).Select(pair => pair.Key).ToArray())
   {
    confirmedLeases[key].source.Dispose();
    confirmedLeases.Remove(key);
   }
   var due = pending.Where(f=>f.dueAt<=now).OrderBy(f=>f.order).ThenBy(f=>f.dueAt).ToArray();
   pending.RemoveAll(f=>f.dueAt<=now);
   foreach(var fact in due)
   {
    // 复活/来源重连可以复用Observation，但绝不能给旧队列换上新租约。
    var id=fact.prefix+fact.cue;
    SfxSourceHandle lease = fact.lease;
    if (fact.confirmedSource != null) lease = ConfirmedSource(fact, id, now);
    else if(!CanObserve(fact.owner)||lease==null||lease.IsDisposed||!ReferenceEquals(fact.owner.source,lease))continue;
    if(fact.kind=="release"||fact.kind=="launch")Releases++;else if(fact.kind=="hit")Hits++;else if(fact.kind=="hurt")Hurts++;
    var decision=lease==null ? new SfxDecision(false,"CONFIRMED_SOURCE_LIMIT") :
        lease.SubmitConfirmed(id,fact.definition,fact.key,fact.position,fact.occurredAt,fact.playback);
    var execution = fact.parent == Guid.Empty ? fact.action : fact.parent;
    var trace = execution == Guid.Empty ? CombatTraceProbe.For(fact.owner.Entity) : CombatTraceProbe.ForExecution(fact.owner.Entity, execution);
    trace?.Note("反馈","声音提交",id+" · "+decision.Reason+" · "+fact.key,fact.action,fact.parent,fact.emission);
    Record(id,fact.key,decision);
   }
  }
  private static string ConfirmedKey(Observation o) => o.source.SourceId+":"+o.source.Generation;

  // 已发生的命中交场景持有的短租约播完。原来源退场不会复活，世界/复位会撤销短租约。
  private SfxSourceHandle ConfirmedSource(Pending fact, string id, double now)
  {
   var definition = fact.definition;
   if (definition == null && (audioRoot.SfxCatalog == null || !audioRoot.SfxCatalog.TryResolve(id,out definition,out _)))
       return null;
   if (definition == null || definition.loop) return null;
   var key = fact.confirmedSource;
   if (!confirmedLeases.TryGetValue(key,out var lease))
   {
    if (confirmedLeases.Count >= 128) return null;
    var source = audioRoot.RegisterSfxSource("confirmed-contact@"+key);
    if (source == null) return null;
    confirmedLeases.Add(key,lease=new ConfirmedLease{source=source});
   }
   float maxClip = 0;
   if (definition.clips != null) foreach (var clip in definition.clips)
       if (clip != null) maxClip = Mathf.Max(maxClip,clip.length);
   float pitchMin = fact.playback?.PitchMin ?? definition.pitchMin;
   lease.end=Math.Max(lease.end,now+maxClip/Mathf.Max(.01f,pitchMin)+.25);
   return lease.source;
  }

  private void OnTrialReset(int scene)
  {
   if (scene != gameObject.scene.handle) return;
   pending.Clear();
   contactPositions.Clear();
   contactOrder.Clear();
   customAbilityHits.Clear();
   foreach (var lease in confirmedLeases.Values) lease.source.Dispose();
   confirmedLeases.Clear();
  }
  private void Record(string id,string key,SfxDecision decision){FactSubmissions++;if(decision.Accepted)Accepted++;else Rejected++;LastFact=id+" / "+key+" / "+decision.Reason;}
  private void LateUpdate()
  {
   if(!TryInitialize()){DisconnectAll();return;}if(Time.unscaledTime>=nextDiscovery)Discover();
   foreach(var o in observed)
   {
    if(!EntityActive(o)){Disconnect(o);continue;}
    if(o.connected&&(o.source==null||o.source.IsDisposed||o.actor!=null&&o.actor.LifeGeneration!=o.generation||
       o.enemy!=null&&o.hp==0&&o.vitals?.Health!=null&&o.vitals.Health.Current>0))Disconnect(o);
    Connect(o);if(!o.connected||o.vitals?.Health==null)continue;
    int hp=o.vitals.Health.Current;if(hp>0&&hp<o.hp)Queue(o,"target","hurt",0,"","hp:"+o.hp+">"+hp+":"+(++o.sequence),o.vitals.WorldPosition);o.hp=hp;
    if(o.dodge!=null&&o.dodge.IsActive&&o.dodge.ActionId!=o.dodgeId){o.dodgeId=o.dodge.ActionId;Queue(o,"movement","dodge",0,"","dodge:"+o.dodgeId,o.Entity.transform.position);}
   }
   observed.RemoveAll(o=>o.Entity==null);
   if(bottle!=null&&bottle.isActiveAndEnabled&&bottle.UseCount!=bottleUses)
   {bottleUses=bottle.UseCount;var current=bottle.Coordinator==null?null:bottle.Coordinator.Current;var owner=observed.FirstOrDefault(o=>o.actor==current&&o.connected);if(owner!=null)Queue(owner,"item","heal",0,"","heal:"+bottleUses,current.transform.position);}
   var selected=coordinator==null?null:coordinator.Current;
   if(selected!=previousCurrent){bool changed=selected!=null&&previousCurrent!=null;previousCurrent=selected;
    if(changed&&soundProfile.TryResolve("interface","switch",0,"",out var cue)){var key="switch:"+(++uiSequence);Record("ui."+cue,key,interfaceSource.Submit("ui."+cue,key,selected.transform.position));}}
   Flush();
  }
  private static void Disconnect(Observation o){foreach(var remove in o.remove)remove();o.remove.Clear();o.source?.Dispose();o.source=null;o.connected=false;o.actions.Clear();o.actionOrder.Clear();o.facts.Clear();o.factOrder.Clear();}
  private void DisconnectAll(){CombatContactGateway.Resolved-=OnContact;SideViewFeedbackRuntime.TrialReset-=OnTrialReset;if(audioRoot!=null){audioRoot.ContentProfileApplied-=OnProfileApplied;audioRoot.ReleaseSfxFacts(this);}foreach(var o in observed)Disconnect(o);observed.Clear();OnTrialReset(gameObject.scene.handle);interfaceSource?.Dispose();interfaceSource=null;audioRoot=null;world=null;}
  private void OnDisable(){DisconnectAll();}private void OnDestroy(){DisconnectAll();}
 }
}
