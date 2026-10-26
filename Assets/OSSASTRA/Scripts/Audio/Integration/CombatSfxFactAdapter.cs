/* 脚本：CombatSfxFactAdapter
 * 功能：按真实释放/接触及冻结语义选声，统一提交到唯一音频根。
 * 边界：只读战斗，不写输入/HP/动画；没有自有播放器或第二个动作时钟。
 * 协作：接受时缓存招式；接触保留子击/目标身份；同帧释放先于接触声提交，玩法顺序不变。
 */
using System;using System.Collections.Generic;using System.Linq;
using Ossastra.CharacterCore;using Ossastra.Characters;using Ossastra.Combat;using UnityEngine;
using ActorRoot=Ossastra.Characters.CharacterRoot;using EnemyEntityRoot=Ossastra.Enemies.EnemyRoot;
namespace Ossastra.Audio
{
 [DefaultExecutionOrder(260),DisallowMultipleComponent]
 public sealed class CombatSfxFactAdapter:MonoBehaviour
 {
  private sealed class ActionSound {public string selector;public int stage;}
  private sealed class Observation
  {
   public ActorRoot actor;public EnemyEntityRoot enemy;public CombatVitals vitals;public CharacterAbilities abilities;public CharacterMainAction main;public CharacterDodge dodge;
   public SfxSourceHandle source;public int hp,generation;public Guid dodgeId;public long sequence;public bool connected;
   public readonly List<Action> remove=new List<Action>();
   public readonly Dictionary<Guid,ActionSound> actions=new Dictionary<Guid,ActionSound>();public readonly Queue<Guid> actionOrder=new Queue<Guid>();
   public readonly HashSet<ActionFact> facts=new HashSet<ActionFact>();public readonly Queue<ActionFact> factOrder=new Queue<ActionFact>();
   public Component Entity=>(Component)actor??enemy;
  }
  private sealed class Pending {public Observation owner;public SfxSourceHandle lease;public string cue,key,kind,prefix;public Vector3 position;public int order;}
  [SerializeField,Tooltip("语义配置只选内容，不决定动作；空引用时读取统一Resources入口。")]
  private CombatSoundProfileSO soundProfile;
  private readonly List<Observation> observed=new List<Observation>();private readonly List<Pending> pending=new List<Pending>();
  private readonly Dictionary<(Guid,int),Vector3> contactPositions=new Dictionary<(Guid,int),Vector3>();
  private readonly Queue<(Guid,int)> contactOrder=new Queue<(Guid,int)>();
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
   CombatContactGateway.Resolved+=OnContact;root.ContentProfileApplied+=OnProfileApplied;Discover();foreach(var o in observed)Connect(o);Status="真实语义 → 精选内容；未匹配留白";return true;
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
   Action<HitOutcome> died=f=>{if(CanObserve(o))Queue(o,"target","death",0,"","death:"+o.generation+":"+(++o.sequence),o.Entity.transform.position);};
   o.vitals.Died+=died;o.remove.Add(()=>{if(o.vitals!=null)o.vitals.Died-=died;});
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
    o.actions[f.ActionId]=new ActionSound{selector=selector,stage=o.actor==null?1:o.actor.AttackStage};o.actionOrder.Enqueue(f.ActionId);
    while(o.actionOrder.Count>256)o.actions.Remove(o.actionOrder.Dequeue());
   }
   // 能力离手和身体由丰富反馈唯一出声。即使Run结束，Root的Source也保留其身份。
   if(f.Kind!=ActionFactKind.Released||(f.Source??"").StartsWith("ability:",StringComparison.Ordinal)||o.abilities!=null&&o.abilities.OwnsRelease(f.ActionId))return;
   if(o.actions.TryGetValue(f.ActionId,out var sound))Queue(o,sound.selector,"release",sound.stage,"","release:"+f.ActionId.ToString("N"),o.Entity.transform.position);
  }
  private void OnAbility(Observation o,AbilityFeedbackFact f)
  {
   if(f==null||!CanObserve(o))return;
   if(f.Kind=="accepted"||f.Kind=="cancel"||f.Kind=="end")return;
   var selector=string.IsNullOrEmpty(f.DefinitionId)?"resource":f.DefinitionId;
   string key="ability:"+f.ExecutionId.ToString("N")+":"+f.EmissionIndex+":"+f.Channel+":"+f.Kind;
   if(f.ExecutionId==Guid.Empty||f.EmissionIndex<0)key+=":"+(++o.sequence);
   // hit/miss按每个投递节点返回；只有SO明确登记爆炸时，空落也允许真实爆炸声，绝不冒充命中。
   var position=f.Position;
   if((f.Kind=="hit"||f.Kind=="miss")&&contactPositions.TryGetValue((f.ExecutionId,f.EmissionIndex),out var actual))position=actual;
   Queue(o,selector,f.Kind,0,f.Channel,key,position,f.DeliveryMode);
  }
  private void OnContact(CombatContactBatch batch)
  {
   if(batch==null||audioRoot==null||audioRoot.WorldScopeId!=world)return;
   var contactKey=(batch.ParentExecutionId,batch.EmissionIndex);
   if(!contactPositions.ContainsKey(contactKey))contactOrder.Enqueue(contactKey);
   contactPositions[contactKey]=batch.Origin;
   while(contactOrder.Count>256)contactPositions.Remove(contactOrder.Dequeue());
   var source=observed.FirstOrDefault(o=>o.connected&&o.vitals==batch.Source);
   // 只消费生产方明确发布的效果语义；不能把任意周期伤害冒充冰效果。
   if(source!=null&&CanObserve(source)&&!string.IsNullOrEmpty(batch.EffectSemantic)&&batch.Receipts.Any(r=>r.Outcome.Reason==HitReason.Applied))
    Queue(source,batch.EffectSemantic,"hit",0,"","effect:"+batch.ActionId+":"+batch.EmissionIndex,batch.Origin);
   foreach(var receipt in batch.Receipts)
   {
    var hit=receipt.Outcome;if(hit==null)continue;
    string key="contact:"+batch.ActionId.ToString("N")+":"+batch.EmissionIndex+":"+hit.TargetId;
    // 能力的hit/miss拥有实际channel和投递语义；这里只接管普通近战，未知冰路脉冲不会变成重刀音。
    if(batch.Cause==CombatContactCause.Direct&&source!=null&&CanObserve(source)&&source.actions.TryGetValue(batch.ParentExecutionId,out var sound)&&
      (sound.selector=="body.basic"||sound.selector=="enemy.basic"||sound.selector.StartsWith("main:",StringComparison.Ordinal)))
    {
     if(hit.Reason==HitReason.Applied){var surface=receipt.Target==null?null:receipt.Target.GetComponentInParent<AudioSurfaceFact>();Queue(source,sound.selector,"hit",sound.stage,"",key,receipt.Position,null,surface==null?null:surface.impactMaterial);}
    }
    var target=observed.FirstOrDefault(o=>o.connected&&o.vitals==receipt.Target);
    if(target==null||!CanObserve(target))continue;
    if(hit.Reason==HitReason.Applied&&hit.HpAfter>0&&hit.HpAfter<hit.HpBefore)Queue(target,"target","hurt",0,"",key,receipt.Position);
    if(hit.Reason==HitReason.Guarded)Queue(target,"target","guard",0,"",key,receipt.Position);
    // 同批已经有精确损血，轮询只作为其它来源的兼容观察；不重复播同一笔HP下降。
    target.hp=target.vitals.Health.Current;
   }
  }
  private void Queue(Observation o,string selector,string kind,int stage,string channel,string key,Vector3 position,string deliveryMode=null,string material=null)
  {
   if(!CanObserve(o))return;
   var prefix=Prefix(o);
   if(SoundFactObserved!=null){var fact=new CombatSoundFact(o.source.SourceId,prefix.TrimEnd('.'),selector,kind,stage,channel,deliveryMode,material,key,position,Time.realtimeSinceStartupAsDouble);foreach(Action<CombatSoundFact> observer in SoundFactObserved.GetInvocationList())try{observer(fact);}catch(Exception error){Debug.LogWarning("AUDIO_SEMANTIC_OBSERVER: "+error.Message);}}
   if(!soundProfile.TryResolve(selector,kind,stage,channel,out var cue,deliveryMode,prefix.TrimEnd('.'),material)){UnmappedFacts++;LastUnmapped=selector+" / "+kind+" / "+channel+" / "+deliveryMode;return;}
   if(pending.Count>=512){Rejected++;return;}
   pending.Add(new Pending{owner=o,lease=o.source,prefix=prefix,cue=cue,key=key,kind=kind,position=position,order=kind=="release"||kind=="launch"?0:1});
  }
  private string Prefix(Observation o)
  {
   if(o.enemy!=null)return "enemy.";
   bool player=coordinator!=null&&coordinator.Characters.Contains(o.actor)?coordinator.Current==o.actor||previousCurrent==o.actor&&!o.actor.IsAlive:o.actor.GetComponent<CharacterInputHandler>()?.HasPlayerControl==true;
   return player?"player.":"companion.";
  }
  private void Flush()
  {
   foreach(var fact in pending.OrderBy(f=>f.order))
   {
    // 复活/来源重连可以复用Observation，但绝不能给旧队列换上新租约。
    if(!CanObserve(fact.owner)||fact.lease==null||fact.lease.IsDisposed||!ReferenceEquals(fact.owner.source,fact.lease))continue;var id=fact.prefix+fact.cue;
    if(fact.kind=="release"||fact.kind=="launch")Releases++;else if(fact.kind=="hit")Hits++;else if(fact.kind=="hurt")Hurts++;
    Record(id,fact.key,fact.owner.source.Submit(id,fact.key,fact.position));
   }
   pending.Clear();
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
  private void DisconnectAll(){CombatContactGateway.Resolved-=OnContact;if(audioRoot!=null){audioRoot.ContentProfileApplied-=OnProfileApplied;audioRoot.ReleaseSfxFacts(this);}foreach(var o in observed)Disconnect(o);observed.Clear();pending.Clear();contactPositions.Clear();contactOrder.Clear();interfaceSource?.Dispose();interfaceSource=null;audioRoot=null;world=null;}
  private void OnDisable(){DisconnectAll();}private void OnDestroy(){DisconnectAll();}
 }
}
