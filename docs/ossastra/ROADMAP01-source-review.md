# ROADMAP01 变化源码完整审查

基准108ea250至本轮最终源。以下为各自有源完整内容和SHA256；行号用于核对，复制代码时移除左侧行号。差异请配合Git提交查看。旧场与共享Prefab未改。

## Assets/OSSASTRA/Scripts/Animation/Core/AnimationRouting.cs

SHA256: 43F112070AC62FC57B9BC32C5E2E8E186727A94169936BB175277A22E03F7B63；审查行1–50

```csharp
   1 | /*
   2 |  * 脚本：AnimationRouting
   3 |  * 功能：只读动画语义、同帧优先级与播放身份；可由三类实体分别产出。
   4 |  * 边界：没有Unity、伤害、输入或动画完成回调；代际由本体根失效处理推进。
   5 |  */
   6 | using System;
   7 | using System.Collections.Generic;
   8 | namespace Ossastra.AnimationCore
   9 | {
  10 |     public enum AnimationSemantic { Idle, Move, BasicAttack, HitReaction, PostureBroken, Death, BasicAttackSecond }
  11 |     public readonly struct AnimationIntent
  12 |     {
  13 |         public string ActorId { get; }
  14 |         public AnimationSemantic Semantic { get; }
  15 |         public string Source { get; }
  16 |         public Guid ReplayId { get; }
  17 |         public int Generation { get; }
  18 |         public AnimationIntent(string actorId, AnimationSemantic semantic, string source, Guid replayId, int generation)
  19 |         { ActorId=actorId; Semantic=semantic; Source=source; ReplayId=replayId; Generation=generation; }
  20 |     }
  21 |     public sealed class AnimationResolvePolicy
  22 |     {
  23 |         private readonly string actorId;
  24 |         public AnimationResolvePolicy(string actorId) { this.actorId=actorId; }
  25 |         public AnimationIntent? Resolve(IEnumerable<AnimationIntent> candidates, int generation)
  26 |         {
  27 |             AnimationIntent? best=null;
  28 |             foreach(var intent in candidates)
  29 |             {
  30 |                 if(intent.ActorId!=actorId || intent.Generation!=generation) continue;
  31 |                 if(!best.HasValue || Priority(intent.Semantic)>Priority(best.Value.Semantic)) best=intent;
  32 |             }
  33 |             return best;
  34 |         }
  35 |         // 明确主轨优先级：死亡>破势>受击>动作>移动>待机，与具体Spine片段名称无关。
  36 |         private static int Priority(AnimationSemantic semantic) => semantic==AnimationSemantic.BasicAttackSecond?2:(int)semantic;
  37 |     }
  38 |     public sealed class AnimationPlaybackIdentity
  39 |     {
  40 |         private string clip;
  41 |         private AnimationIntent last;
  42 |         private bool hasLast;
  43 |         public bool ShouldCommit(string nextClip, AnimationIntent intent)
  44 |         {
  45 |             var changed=!hasLast || clip!=nextClip || last.ActorId!=intent.ActorId || last.Semantic!=intent.Semantic ||
  46 |                 last.Generation!=intent.Generation || last.ReplayId!=intent.ReplayId;
  47 |             clip=nextClip; last=intent; hasLast=true; return changed;
  48 |         }
  49 |     }
  50 | }
```

## Assets/OSSASTRA/Scripts/Animation/Runtime/AnimationClipMap.cs

SHA256: 660932CF1CE2270F09A26EECDFB6BF9F2A09D3EB6B5A2625925226801BE7912B；审查行1–23

```csharp
   1 | /*
   2 |  * 脚本：AnimationClipMap
   3 |  * 功能：本体可配置语义到片段映射及主轨过渡数据。
   4 |  * 边界：资源缺失由表现适配器报告与回退；不定义动作Release或伤害时刻。
   5 |  */
   6 | using System;
   7 | using Ossastra.AnimationCore;
   8 | using UnityEngine;
   9 | namespace Ossastra.AnimationPresentation
  10 | {
  11 |     [Serializable]
  12 |     public sealed class AnimationClipMap
  13 |     {
  14 |         public AnimationSemantic semantic;
  15 |         public string clip;
  16 |         public bool loop;
  17 |         [Min(0)] public float mixDuration=.08f;
  18 |     }
  19 |     public interface ICharacterAnimationFacts
  20 |     {
  21 |         void CollectFacts(bool alive, bool staggered, bool hitReaction, int generation, int attackStage=1);
  22 |     }
  23 | }
```

## Assets/OSSASTRA/Scripts/Characters/Core/BasicActionRuntime.cs

SHA256: 64E9FDB2B76F3A78D7D1ADC3612BF55F1BE812FD5731BC2DB335E701A5A4BF63；审查行1–148

```csharp
   1 | /*
   2 |  * 脚本：BasicActionRuntime
   3 |  * 功能：维护单角色的动作身份、时钟与Accepted／Released／Ready／终结事实。
   4 |  * 边界：不采设备、不驱动Spine、不查询碰撞、不直接改HP。
   5 |  * 协作：Root请求并推进；Release对象交世界适配器；暂停不推进时钟。
   6 |  */
   7 | using System;
   8 | using System.Collections.Generic;
   9 | using System.Linq;
  10 | 
  11 | namespace Ossastra.CharacterCore
  12 | {
  13 |     public enum ActionFactKind { Accepted, Released, MoveReady, AttackReady, Finished, Cancelled }
  14 | 
  15 |     // 事实是结果，不是任意广播；调用者按返回顺序处理。
  16 |     public sealed class ActionFact
  17 |     {
  18 |         public ActionFactKind Kind { get; }
  19 |         public Guid ActionId { get; }
  20 |         public string Source { get; }
  21 |         public string Reason { get; }
  22 |         public double At { get; }
  23 |         public AttackRelease Release { get; }
  24 | 
  25 |         internal ActionFact(ActionFactKind kind, Guid id, string source, double at,
  26 |             string reason = null, AttackRelease release = null)
  27 |         {
  28 |             Kind = kind;
  29 |             ActionId = id;
  30 |             Source = source;
  31 |             At = at;
  32 |             Reason = reason;
  33 |             Release = release;
  34 |         }
  35 |     }
  36 | 
  37 |     public sealed class ActionRequestResult
  38 |     {
  39 |         public bool Accepted => Fact != null;
  40 |         public string Reason { get; }
  41 |         public ActionFact Fact { get; }
  42 |         public ActionFact PreviousTerminal { get; }
  43 | 
  44 |         internal ActionRequestResult(string reason, ActionFact fact = null, ActionFact previousTerminal = null)
  45 |         {
  46 |             Reason = reason;
  47 |             Fact = fact;
  48 |             PreviousTerminal = previousTerminal;
  49 |         }
  50 |     }
  51 | 
  52 |     // 单角色单动作权威，不读设备/Spine/世界碰撞，也不直接写HP。
  53 |     public sealed class BasicActionRuntime
  54 |     {
  55 |         private readonly string actorId;
  56 |         private BasicAttackDefinition definition;
  57 |         private (double At, ActionFactKind Kind)[] nodes;
  58 |         private int nextNode;
  59 |         private double elapsed;
  60 |         private double timeCorrection;
  61 |         private Guid actionId;
  62 |         private string source;
  63 |         private bool moveReady;
  64 |         private bool attackReady;
  65 | 
  66 |         public bool IsActive => definition != null;
  67 |         public bool MoveReady => !IsActive || moveReady;
  68 |         public bool AttackReady => !IsActive || attackReady;
  69 |         public Guid ActionId => IsActive ? actionId : Guid.Empty;
  70 |         // 同一时钟的绝对读数供有限接续窗口使用；没有动作时也推进，不另建连段计时器。
  71 |         public double SimulationTime { get; private set; }
  72 | 
  73 |         public BasicActionRuntime(string actorId)
  74 |         {
  75 |             if (string.IsNullOrWhiteSpace(actorId)) throw new ArgumentException("Actor identity required", nameof(actorId));
  76 |             this.actorId = actorId;
  77 |         }
  78 | 
  79 |         public ActionRequestResult Request(BasicAttackDefinition attack, string requestSource,
  80 |             ControlAuthority gate, bool paused = false)
  81 |         {
  82 |             if (attack == null) throw new ArgumentNullException(nameof(attack));
  83 |             if (gate == null) throw new ArgumentNullException(nameof(gate));
  84 |             if (string.IsNullOrWhiteSpace(requestSource)) throw new ArgumentException("Request source required", nameof(requestSource));
  85 |             if (paused) return new ActionRequestResult("paused");
  86 |             if (!gate.Allows(ControlChannel.Attack)) return new ActionRequestResult("attack-blocked");
  87 |             if (IsActive && !attackReady) return new ActionRequestResult("busy");
  88 |             // Ready允许一次新按下；显式终结旧身体动作，不遗失其已释放效果。
  89 |             var previousTerminal = IsActive ? Cancel("replaced-by-next-attack") : null;
  90 |             definition = attack;
  91 |             source = requestSource;
  92 |             actionId = Guid.NewGuid();
  93 |             elapsed = 0;
  94 |             timeCorrection = 0;
  95 |             nextNode = 0;
  96 |             moveReady = false;
  97 |             attackReady = false;
  98 |             // 两种Ready可互换先后；同一时刻固定Release→Move→Attack→Finish。
  99 |             nodes = new[] {
 100 |                 (attack.ReleaseAt, ActionFactKind.Released),
 101 |                 (attack.MoveReadyAt, ActionFactKind.MoveReady),
 102 |                 (attack.AttackReadyAt, ActionFactKind.AttackReady),
 103 |                 (attack.FinishAt, ActionFactKind.Finished)
 104 |             }.OrderBy(n => n.Item1).ThenBy(n => n.Item2).ToArray();
 105 |             return new ActionRequestResult(null, Fact(ActionFactKind.Accepted, 0), previousTerminal);
 106 |         }
 107 | 
 108 |         public IReadOnlyList<ActionFact> Advance(double delta, bool paused)
 109 |         {
 110 |             if (!BasicAttackDefinition.Finite(delta) || delta < 0)
 111 |                 throw new ArgumentOutOfRangeException(nameof(delta));
 112 |             if (paused) return Array.Empty<ActionFact>();
 113 |             SimulationTime += delta;
 114 |             if (!IsActive) return Array.Empty<ActionFact>();
 115 |             // 补偿累计舍入误差；不靠提前Release的任意epsilon修补帧边界。
 116 |             var correctedDelta = delta - timeCorrection;
 117 |             var sum = elapsed + correctedDelta;
 118 |             timeCorrection = (sum - elapsed) - correctedDelta;
 119 |             elapsed = Math.Min(definition.FinishAt, sum);
 120 |             if (elapsed == definition.FinishAt) timeCorrection = 0;
 121 |             var facts = new List<ActionFact>();
 122 |             while (nextNode < nodes.Length && elapsed >= nodes[nextNode].At)
 123 |             {
 124 |                 var node = nodes[nextNode++];
 125 |                 AttackRelease release = null;
 126 |                 if (node.Kind == ActionFactKind.Released)
 127 |                     release = new AttackRelease(actionId, actorId, definition.Damage, definition.PostureImpact);
 128 |                 if (node.Kind == ActionFactKind.MoveReady) moveReady = true;
 129 |                 if (node.Kind == ActionFactKind.AttackReady) attackReady = true;
 130 |                 facts.Add(Fact(node.Kind, node.At, release: release));
 131 |                 if (node.Kind == ActionFactKind.Finished) definition = null;
 132 |             }
 133 |             return facts;
 134 |         }
 135 | 
 136 |         public ActionFact Cancel(string reason)
 137 |         {
 138 |             if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Cancellation cause required", nameof(reason));
 139 |             if (!IsActive) return null;
 140 |             var terminal = Fact(ActionFactKind.Cancelled, elapsed, reason);
 141 |             definition = null;
 142 |             return terminal;
 143 |         }
 144 | 
 145 |         private ActionFact Fact(ActionFactKind kind, double at, string reason = null, AttackRelease release = null)
 146 |             => new ActionFact(kind, actionId, source, at, reason, release);
 147 |     }
 148 | }
```

## Assets/OSSASTRA/Scripts/Characters/Core/BasicChainState.cs

SHA256: B3A0125C371167D654EADD52D0DFBAA0B7C2B5649DE3C67E70DA35A2578B8295；审查行1–44

```csharp
   1 | /*
   2 |  * 脚本：BasicChainState
   3 |  * 功能：两段手动普攻的接续资格和最多一条预输入。
   4 |  * 边界：不累计delta，不产生动作/伤害；绝对时间只读原BasicActionRuntime模拟时钟。
   5 |  */
   6 | using System;
   7 | namespace Ossastra.CharacterCore
   8 | {
   9 |     public sealed class BasicChainState
  10 |     {
  11 |         private readonly double preInput, bufferLifetime, continuationWindow;
  12 |         private Guid actionId;
  13 |         private int stage;
  14 |         private bool hit, buffered;
  15 |         private double readyAt, bufferUntil;
  16 |         public bool HasBuffer => buffered;
  17 |         public double WindowUntil => stage==1&&hit?readyAt+continuationWindow:-1;
  18 |         public BasicChainState(double preInput,double bufferLifetime,double continuationWindow)
  19 |         {
  20 |             if(!Valid(preInput)||!Valid(bufferLifetime)||!Valid(continuationWindow))throw new ArgumentOutOfRangeException();
  21 |             this.preInput=preInput;this.bufferLifetime=bufferLifetime;this.continuationWindow=continuationWindow;
  22 |         }
  23 |         public int Request(double now,bool attackReady,bool eligible)
  24 |         {
  25 |             if(!eligible){Clear();return 0;}
  26 |             if(attackReady) {buffered=false;return stage==1&&hit&&now<=WindowUntil?2:1;}
  27 |             if(stage==1&&hit&&!buffered&&now>=readyAt-preInput&&now<=readyAt)
  28 |             {buffered=true;bufferUntil=now+bufferLifetime;}
  29 |             return 0;
  30 |         }
  31 |         public int Poll(double now,bool attackReady)
  32 |         {
  33 |             if(!buffered)return 0;
  34 |             if(now>bufferUntil||now>WindowUntil){buffered=false;return 0;}
  35 |             if(!attackReady)return 0;
  36 |             buffered=false;return stage==1&&hit?2:0;
  37 |         }
  38 |         public void Accepted(int stage,Guid id,double now,double attackReadyAt)
  39 |         {this.stage=stage;actionId=id;readyAt=now+attackReadyAt;hit=false;buffered=false;}
  40 |         public void Hit(Guid id,bool applied){if(actionId==id)hit=applied;}
  41 |         public void Clear(){stage=0;actionId=Guid.Empty;hit=false;buffered=false;}
  42 |         private static bool Valid(double v)=>!double.IsNaN(v)&&!double.IsInfinity(v)&&v>=0;
  43 |     }
  44 | }
```

## Assets/OSSASTRA/Scripts/Characters/Core/Health.cs

SHA256: 17312E2E0601EDA6E54C6ACC56EF031DA57011C339777167049C9A0C12DED1B7；审查行1–40

```csharp
   1 | /*
   2 |  * 脚本：Health
   3 |  * 功能：保存目标身份与唯一可写的当前HP，限制伤害结果不低于零。
   4 |  * 边界：不管理动画、控制权、敌人AI或正式死亡流程。
   5 |  * 协作：AttackRelease经内部ApplyDamage写入；真实目标和开发重置保留同一个Health引用。
   6 |  */
   7 | using System;
   8 | 
   9 | namespace Ossastra.CharacterCore
  10 | {
  11 |     // HP只有本对象能够写；测试目标将来引用同一个Health，不能维护另一份真实HP。
  12 |     public sealed class Health
  13 |     {
  14 |         public string Id { get; }
  15 |         public int Maximum { get; }
  16 |         public int Current { get; private set; }
  17 | 
  18 |         public Health(string id, int maximum)
  19 |         {
  20 |             if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Target identity required", nameof(id));
  21 |             if (maximum <= 0) throw new ArgumentOutOfRangeException(nameof(maximum));
  22 |             Id = id;
  23 |             Maximum = maximum;
  24 |             Current = maximum;
  25 |         }
  26 | 
  27 |         internal void ApplyDamage(int damage)
  28 |         {
  29 |             if (damage <= 0) throw new ArgumentOutOfRangeException(nameof(damage));
  30 |             Current = Math.Max(0, Current - damage);
  31 |         }
  32 | 
  33 |         /// <summary>显式开发回放入口；不代表生产自动返场，不更换目标身份或Health引用。</summary>
  34 |         public void RestoreForDevelopment() { Current=Maximum; }
  35 | 
  36 |         /// <summary>生命服务经本体验证后恢复同一Health；不更换身份或重播死亡事件。</summary>
  37 |         public bool RestoreAfterDeath()
  38 |         { if(Current>0) return false; Current=Maximum; return true; }
  39 |     }
  40 | }
```

## Assets/OSSASTRA/Scripts/Characters/Core/RespawnSchedule.cs

SHA256: 033B4C67A726B3B93531B9CC99E907EA899B7E538710E930DBBD10F8D08F2319；审查行1–63

```csharp
   1 | /*
   2 |  * 脚本：RespawnSchedule
   3 |  * 功能：场景队伍返场的模拟时间和待处理事务，不复制HP或Current。
   4 |  * 边界：消费真实存活/可用快照；落点失败不完成，恢复成功才递增代际。
   5 |  */
   6 | using System;
   7 | using System.Collections.Generic;
   8 | namespace Ossastra.CharacterCore
   9 | {
  10 |     public sealed class RespawnSchedule
  11 |     {
  12 |         private readonly double singleDelay, wipeDelay;
  13 |         private readonly double[] due, diedAt;
  14 |         private readonly bool[] pending;
  15 |         private readonly int[] generations;
  16 |         private double wipeDue;
  17 |         private int[] ready = Array.Empty<int>();
  18 |         public double Time { get; private set; }
  19 |         public bool IsWipe { get; private set; }
  20 |         public int[] ReadyIndices => (int[])ready.Clone();
  21 |         public int Generation(int index) => generations[index];
  22 |         public double DiedAt(int index) => diedAt[index];
  23 |         public double DueAt(int index) => pending[index] ? (IsWipe ? wipeDue : due[index]) : -1;
  24 |         public RespawnSchedule(int count, double singleDelay, double wipeDelay)
  25 |         {
  26 |             if (count < 1 || !Valid(singleDelay) || !Valid(wipeDelay)) throw new ArgumentOutOfRangeException();
  27 |             this.singleDelay=singleDelay; this.wipeDelay=wipeDelay;
  28 |             due=new double[count]; diedAt=new double[count]; pending=new bool[count]; generations=new int[count];
  29 |             for(var i=0;i<count;i++) diedAt[i]=-1;
  30 |         }
  31 |         // 原死亡事件只登记本场模拟时刻；HP仍由调用方的本体Health决定。
  32 |         public void RecordDeath(int index)
  33 |         {
  34 |             if(pending[index]) return;
  35 |             pending[index]=true; diedAt[index]=Time; due[index]=Time+singleDelay;
  36 |         }
  37 |         public void Tick(bool[] alive, bool[] available, double delta, bool suspended)
  38 |         {
  39 |             if (alive.Length!=pending.Length || available.Length!=pending.Length) throw new ArgumentException("Party size changed");
  40 |             ready=Array.Empty<int>();
  41 |             if (suspended || !Valid(delta)) return;
  42 |             var allDead=true;
  43 |             for(var i=0;i<pending.Length;i++)
  44 |             {
  45 |                 if(alive[i] || !available[i]) { pending[i]=false; allDead=false; }
  46 |                 else if(!pending[i]) RecordDeath(i);
  47 |             }
  48 |             if(allDead && !IsWipe) wipeDue=Time+wipeDelay;
  49 |             IsWipe=allDead;
  50 |             Time+=delta;
  51 |             var list=new List<int>();
  52 |             for(var i=0;i<pending.Length;i++)
  53 |                 if(pending[i] && Time+1e-9 >= (IsWipe?wipeDue:due[i])) list.Add(i);
  54 |             ready=list.ToArray();
  55 |         }
  56 |         public void Complete(int index)
  57 |         {
  58 |             if(!pending[index] || Array.IndexOf(ready,index)<0) throw new InvalidOperationException("Not due");
  59 |             pending[index]=false; generations[index]++;
  60 |         }
  61 |         private static bool Valid(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value>=0;
  62 |     }
  63 | }
```

## Assets/OSSASTRA/Scripts/Characters/Runtime/BasicChainProfile.cs

SHA256: 6C3B7A21A88F7FF069B5A8E0693DDBAE7671C98B3C5926C10A8C7542E9625669；审查行1–27

```csharp
   1 | /*
   2 |  * 脚本：BasicChainProfile
   3 |  * 功能：可选两段SAMPLE参数；仅新验证场装配，旧场保留Root单段参数。
   4 |  * 边界：提供不可变定义，不自己Update或产生第二动作时钟。
   5 |  */
   6 | using System;
   7 | using Ossastra.CharacterCore;
   8 | using UnityEngine;
   9 | namespace Ossastra.Characters
  10 | {
  11 |     [Serializable]
  12 |     public sealed class BasicStageParameters
  13 |     {
  14 |         public float releaseAt=.3f, moveReadyAt=.5f, attackReadyAt=.8f, finishAt=1.1f;
  15 |         public int damage=12;
  16 |         public float postureImpact=35;
  17 |         public BasicAttackDefinition Freeze()=>new BasicAttackDefinition(releaseAt,moveReadyAt,attackReadyAt,finishAt,damage,postureImpact);
  18 |     }
  19 |     public sealed class BasicChainProfile : MonoBehaviour
  20 |     {
  21 |         [Header("两段手动普攻 SAMPLE")]
  22 |         public BasicStageParameters stage1=new BasicStageParameters();
  23 |         public BasicStageParameters stage2=new BasicStageParameters{releaseAt=.35f,moveReadyAt=.55f,attackReadyAt=.9f,finishAt=1.2f,damage=18,postureImpact=45};
  24 |         [Min(0)] public float preInput=.12f, bufferLifetime=.17f, continuationWindow=.45f;
  25 |         public BasicChainState CreateState()=>new BasicChainState(preInput,bufferLifetime,continuationWindow);
  26 |     }
  27 | }
```

## Assets/OSSASTRA/Scripts/Characters/Runtime/CapsuleRespawnPlacement.cs

SHA256: E70F8ED332E338121F42182AA6B586A0A5E6DF2C5AFA965D6C057ED5D45F8E95；审查行1–50

```csharp
   1 | /*
   2 |  * 脚本：CapsuleRespawnPlacement
   3 |  * 功能：检查本层地面、真实胶囊阻挡与同批次间距，返回安全返场位置。
   4 |  * 边界：只查询物理世界，不移动实体；失败保持pending，不使用原点兜底。
   5 |  */
   6 | using System.Collections.Generic;
   7 | using UnityEngine;
   8 | namespace Ossastra.Characters
   9 | {
  10 |     public interface IRespawnPlacement
  11 |     {
  12 |         bool TryFind(CharacterController body, Vector3 checkpoint, IReadOnlyList<Vector3> reserved, out Vector3 position);
  13 |     }
  14 |     public sealed class CapsuleRespawnPlacement : MonoBehaviour, IRespawnPlacement
  15 |     {
  16 |         [SerializeField] private LayerMask environment = ~0;
  17 |         [SerializeField, Min(.01f)] private float sameLayerTolerance=.2f;
  18 |         [SerializeField, Min(.1f)] private float searchSpacing=.8f;
  19 |         public bool TryFind(CharacterController body, Vector3 checkpoint, IReadOnlyList<Vector3> reserved, out Vector3 position)
  20 |         {
  21 |             position=checkpoint;
  22 |             if(body==null) return false;
  23 |             // 本工程角色胶囊保持竖直；拒绝不支持的倾斜/非等比配置，不能假装验证通过。
  24 |             var scale=body.transform.lossyScale;
  25 |             if(Vector3.Dot(body.transform.up,Vector3.up)<.999f || Mathf.Abs(scale.x-scale.z)>.001f) return false;
  26 |             var radius=body.radius*Mathf.Abs(scale.x);
  27 |             var height=Mathf.Max(body.height*Mathf.Abs(scale.y),radius*2);
  28 |             for(var i=0;i<9;i++)
  29 |             {
  30 |                 var offset=i==0?Vector3.zero:new Vector3(Mathf.Cos((i-1)*Mathf.PI/4),0,Mathf.Sin((i-1)*Mathf.PI/4))*searchSpacing;
  31 |                 var candidate=checkpoint+offset;
  32 |                 if(!Physics.Raycast(candidate+Vector3.up*.5f,Vector3.down,out var floor,1f,environment,QueryTriggerInteraction.Ignore)) continue;
  33 |                 if(Mathf.Abs(floor.point.y-checkpoint.y)>sameLayerTolerance || floor.normal.y<.7f) continue;
  34 |                 candidate.y=floor.point.y+.03f;
  35 |                 var center=candidate+Vector3.Scale(body.center,scale);
  36 |                 var a=center+Vector3.up*(height*.5f-radius);
  37 |                 var b=center-Vector3.up*(height*.5f-radius);
  38 |                 var blocked=false;
  39 |                 foreach(var collider in Physics.OverlapCapsule(a,b,radius+.01f,environment,QueryTriggerInteraction.Ignore))
  40 |                     if(collider!=null && !collider.transform.IsChildOf(body.transform)) { blocked=true; break; }
  41 |                 if(blocked) continue;
  42 |                 foreach(var other in reserved)
  43 |                     if((other-candidate).sqrMagnitude<Mathf.Pow(height+radius,2)) {blocked=true;break;}
  44 |                 if(blocked) continue;
  45 |                 position=candidate; return true;
  46 |             }
  47 |             return false;
  48 |         }
  49 |     }
  50 | }
```

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterControlCoordinator.cs

SHA256: 8C83E3B26AE8CF1C36A1AA7040520EFCF290A2C3AB4484FCD3701176AACC11B0；审查行1–179

```csharp
   1 | /*
   2 |  * 脚本：CharacterControlCoordinator
   3 |  * 功能：保存同组角色唯一的玩家实控对象，并读取Inspector切换输入。
   4 |  * 边界：不接管各角色动作时钟、HP或AI；不使用全局单例。
   5 |  * 协作：在Root之前更新；订阅本体死亡立即交接，玩家/伙伴只读同一许可/代际，不复制Current。
   6 |  */
   7 | using System;
   8 | using System.Collections.Generic;
   9 | using Ossastra.CharacterCore;
  10 | using UnityEngine;
  11 | using UnityEngine.InputSystem;
  12 | 
  13 | namespace Ossastra.Characters
  14 | {
  15 |     [DefaultExecutionOrder(-200)]
  16 |     [DisallowMultipleComponent]
  17 |     public sealed class CharacterControlCoordinator : MonoBehaviour, ICharacterPlayerControlSource, ICharacterControlSession
  18 |     {
  19 |         [SerializeField] private CharacterRoot[] characters = Array.Empty<CharacterRoot>();
  20 |         [SerializeField, Min(0)] private int initialIndex;
  21 |         [Header("切换实控角色 / 可在Inspector修改")]
  22 |         public InputAction switchAction = new InputAction("SwitchCharacter", InputActionType.Button);
  23 |         private readonly AttackIntent switchIntent = new AttackIntent();
  24 |         private CharacterInputHandler[] inputs;
  25 |         private bool configured, bindingsOwned, focused = true, applicationPaused;
  26 |         private int handoverFrame = -1;
  27 |         public CharacterRoot Current { get; private set; }
  28 |         public IReadOnlyList<CharacterRoot> Characters => Array.AsReadOnly(characters);
  29 |         public string Status { get; private set; } = "未初始化";
  30 |         public int ControlRevision { get; private set; }
  31 |         public bool IsIntentSamplingAllowed => configured && isActiveAndEnabled && focused && !applicationPaused &&
  32 |             Current != null && Current.IsAlive && Current.isActiveAndEnabled && Current.DataBus != null && Time.frameCount > handoverFrame;
  33 | 
  34 |         private void Awake()
  35 |         {
  36 |             if (characters == null) characters = Array.Empty<CharacterRoot>();
  37 |             inputs = new CharacterInputHandler[characters.Length];
  38 |             var identities = new HashSet<string>(StringComparer.Ordinal);
  39 |             var sources = new HashSet<CharacterInputHandler>();
  40 |             var valid = characters.Length >= 2 && initialIndex < characters.Length && initialIndex >= 0 &&
  41 |                 switchAction != null && switchAction.bindings.Count > 0;
  42 |             // 先完整校验，再绑定；合法成员在配置错误时也归本协调者关闭，不能退回双控。
  43 |             for (var i = 0; i < characters.Length; i++)
  44 |             {
  45 |                 var character = characters[i];
  46 |                 if (character == null) { valid = false; continue; }
  47 |                 inputs[i] = character.GetComponent<CharacterInputHandler>();
  48 |                 if (inputs[i] == null || !sources.Add(inputs[i]) ||
  49 |                     string.IsNullOrWhiteSpace(character.ActorId) || !identities.Add(character.ActorId)) valid = false;
  50 |             }
  51 |             foreach (var input in inputs)
  52 |                 if (input != null && !input.CanBindPlayerControl(this))
  53 |                 { Status = "输入已归另一协调者；拒绝抢占"; enabled = false; return; }
  54 |             foreach (var input in inputs) if (input != null) input.BindPlayerControl(this);
  55 |             bindingsOwned = true;
  56 |             foreach (var character in characters) if (character!=null) character.Died+=OnCharacterDied;
  57 |             configured = valid;
  58 |             if (!valid) { Status = "角色／身份／输入配置无效，输入关闭"; enabled = false; }
  59 |         }
  60 | 
  61 |         private void OnEnable()
  62 |         {
  63 |             switchIntent.SuppressUntilRelease();
  64 |             if (!configured) return;
  65 |             switchAction.Enable();
  66 |             if (characters[initialIndex]!=null && characters[initialIndex].IsAlive) Select(characters[initialIndex]);
  67 |             else SelectSurvivor(false);
  68 |         }
  69 | 
  70 |         private void Update()
  71 |         {
  72 |             if (Current != null && !Current.IsAlive) SelectSurvivor(true);
  73 |             else if (Current != null && (!Current.isActiveAndEnabled || Current.DataBus == null)) Revoke("当前角色不可用");
  74 |             var held = switchAction != null && switchAction.enabled && switchAction.IsPressed();
  75 |             if (switchIntent.Observe(held) && focused && !applicationPaused) SwitchNext();
  76 |         }
  77 | 
  78 |         /// <summary>只有Current对应的输入源可消费玩家输入；切换当帧新源禁止采样。</summary>
  79 |         public bool IsPlayerInputAllowed(ICharacterInputSource source)
  80 |         {
  81 |             if (!IsIntentSamplingAllowed) return false;
  82 |             for (var i = 0; i < characters.Length; i++)
  83 |                 if (characters[i] == Current) return ReferenceEquals(inputs[i], source);
  84 |             return false;
  85 |         }
  86 | 
  87 |         /// <summary>场景级切人入口；不会取消、复制或重建任一角色的已接受动作。</summary>
  88 |         public bool SwitchNext()
  89 |         {
  90 |             if (!configured || !isActiveAndEnabled || !focused || applicationPaused) return false;
  91 |             var index = Array.IndexOf(characters, Current);
  92 |             for (var step = 1; step <= characters.Length; step++)
  93 |             {
  94 |                 var candidate = characters[(index + step) % characters.Length];
  95 |                 if (candidate != null && candidate.IsAlive && candidate.isActiveAndEnabled && candidate.DataBus != null)
  96 |                 { Select(candidate); return true; }
  97 |             }
  98 |             Revoke("没有可控制角色");
  99 |             return false;
 100 |         }
 101 | 
 102 |         private void Select(CharacterRoot next, bool requireMovementRelease=false)
 103 |         {
 104 |             // 先撤回旧权威并清所有快照，再授予新身份。整帧禁入避免外部晚调用导致同帧双控。
 105 |             Current = null;
 106 |             ControlRevision++;
 107 |             foreach (var input in inputs) if (input != null) input.ResetPlayerInput(requireMovementRelease);
 108 |             handoverFrame = Time.frameCount;
 109 |             Current = next;
 110 |             Status = next == null ? "无实控" : "玩家输入 → " + next.ActorId;
 111 |         }
 112 | 
 113 |         private void Revoke(string reason) { Select(null); Status = reason; }
 114 | 
 115 |         /// <summary>返场只补空缺的实控；不抢存活者Current。恢复者本体已重武装输入。</summary>
 116 |         internal void ReconcileAfterRespawn()
 117 |         {
 118 |             if(!configured || !isActiveAndEnabled) return;
 119 |             if(Current!=null && Current.IsAlive && Current.isActiveAndEnabled) return;
 120 |             SelectSurvivor(true);
 121 |         }
 122 |         private void OnCharacterDied(CharacterRoot character)
 123 |         { if (bindingsOwned && configured && character==Current) SelectSurvivor(true); }
 124 | 
 125 |         private void SelectSurvivor(bool deathTransfer)
 126 |         {
 127 |             if (!configured || !isActiveAndEnabled) return;
 128 |             var index=Array.IndexOf(characters,Current);
 129 |             for (var step=1;step<=characters.Length;step++)
 130 |             {
 131 |                 var candidate=characters[(index+step)%characters.Length];
 132 |                 if (candidate!=null && candidate.IsAlive && candidate.isActiveAndEnabled && candidate.DataBus!=null)
 133 |                 { Select(candidate,deathTransfer); return; }
 134 |             }
 135 |             Select(null,deathTransfer); Status="无可控制角色";
 136 |         }
 137 | 
 138 |         /// <summary>显式开发回放：由外部先恢复同一组实体，随后重新选合法存活者；无自动返场。</summary>
 139 |         public void RestoreControlForDevelopment()
 140 |         {
 141 |             if (!configured || !isActiveAndEnabled) return;
 142 |             foreach (var candidate in characters)
 143 |                 if (candidate!=null && candidate.IsAlive && candidate.isActiveAndEnabled && candidate.DataBus!=null)
 144 |                 { Select(candidate,true); return; }
 145 |             Select(null,true); Status="无可控制角色";
 146 |         }
 147 |         private void OnDisable()
 148 |         {
 149 |             if (switchAction != null) switchAction.Disable();
 150 |             switchIntent.SuppressUntilRelease();
 151 |             // 未取得绑定所有权的失败实例不能清理其它协调者的快照或重武装状态。
 152 |             if (bindingsOwned)
 153 |             {
 154 |                 if (configured) Revoke("协调者禁用，输入关闭");
 155 |                 else foreach (var input in inputs) if (input != null) input.ResetPlayerInput();
 156 |             }
 157 |         }
 158 |         private void OnApplicationFocus(bool value)
 159 |         {
 160 |             focused = value;
 161 |             ControlRevision++;
 162 |             switchIntent.SuppressUntilRelease();
 163 |             if (bindingsOwned) foreach (var input in inputs) if (input != null) input.ResetPlayerInput();
 164 |         }
 165 |         private void OnApplicationPause(bool value)
 166 |         {
 167 |             applicationPaused = value;
 168 |             ControlRevision++;
 169 |             switchIntent.SuppressUntilRelease();
 170 |             if (bindingsOwned) foreach (var input in inputs) if (input != null) input.ResetPlayerInput();
 171 |         }
 172 |         // 输入仍保留“曾受管理”的关系；本对象销毁后查询失败关闭，不能突然恢复多个玩家源。
 173 |         private void OnDestroy()
 174 |         {
 175 |             if (bindingsOwned) foreach (var character in characters) if (character!=null) character.Died-=OnCharacterDied;
 176 |             if (switchAction != null) switchAction.Dispose();
 177 |         }
 178 |     }
 179 | }
```

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterOrientation.cs

SHA256: 89CA1CF5FF9AA6E3DD0A5AB5472CEEB26D80B3928FA2BA3307E25F0579745C7B；审查行1–44

```csharp
   1 | /*
   2 |  * 脚本：CharacterOrientation
   3 |  * 功能：分别保存可见左右翻转和实际攻击方向。
   4 |  * 边界：有效鼠标优先，其次摇杆、移动，最后保留；方向有限、非零且平面归一。
   5 |  * 协作：Root解析输入并在接受／释放时要求面向攻击；Spine只读取显示翻转。
   6 |  */
   7 | using UnityEngine;
   8 | 
   9 | namespace Ossastra.Characters
  10 | {
  11 |     [DisallowMultipleComponent]
  12 |     public sealed class CharacterOrientation : MonoBehaviour, ICharacterOrientation
  13 |     {
  14 |         public Vector3 AttackDirection { get; private set; } = Vector3.right;
  15 |         public float VisualFacingX { get; private set; } = 1;
  16 | 
  17 |         public void ResolveDirections(Vector2 moveInput, Vector3? pointerDirection, Vector2 stickDirection, bool movementFacing)
  18 |         {
  19 |             var pointerValid = pointerDirection.HasValue && TrySetAttackDirection(pointerDirection.Value);
  20 |             if (!pointerValid && !TrySetAttackDirection(new Vector3(stickDirection.x, 0, stickDirection.y)))
  21 |                 TrySetAttackDirection(new Vector3(moveInput.x, 0, moveInput.y));
  22 |             if (movementFacing && Mathf.Abs(moveInput.x) > .01f) VisualFacingX = Mathf.Sign(moveInput.x);
  23 |         }
  24 | 
  25 |         public void OrientTowards(Vector3 worldPosition) { TrySetAttackDirection(worldPosition - transform.position); }
  26 | 
  27 |         /// <summary>本体生命循环清除死亡前的朝向残留，后续正常输入重新采样。</summary>
  28 |         public void ResetOrientation() { AttackDirection=Vector3.right; VisualFacingX=1; }
  29 | 
  30 |         public void FaceAttackDirection()
  31 |         {
  32 |             if (Mathf.Abs(AttackDirection.x) > .01f) VisualFacingX = Mathf.Sign(AttackDirection.x);
  33 |         }
  34 | 
  35 |         private bool TrySetAttackDirection(Vector3 direction)
  36 |         {
  37 |             direction.y = 0;
  38 |             if (float.IsNaN(direction.x) || float.IsNaN(direction.z) || float.IsInfinity(direction.x) ||
  39 |                 float.IsInfinity(direction.z) || direction.sqrMagnitude < .0001f) return false;
  40 |             AttackDirection = direction.normalized;
  41 |             return true;
  42 |         }
  43 |     }
  44 | }
```

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterRoot.cs

SHA256: CF482E94DE06095FA5E55E000635CAD9316AFE28C60E5F548DBF2F67EB7CEF5B；审查行1–311

```csharp
   1 | /*
   2 |  * 脚本：CharacterRoot
   3 |  * 功能：组织单角色的接口接线、输入、动作事实、运动和表现。
   4 |  * 边界：持有本角色运行时；只读生命资格，不实现HP写入、设备绑定或开发GUI。
   5 |  * 协作：初始化可选本体Follow/Posture；显式按输入、架势、朝向、动作、运动、表现推进。
   6 |  */
   7 | using System;
   8 | using Ossastra.CharacterCore;
   9 | using Ossastra.Combat;
  10 | using UnityEngine;
  11 | using UnityEngine.Serialization;
  12 | 
  13 | namespace Ossastra.Characters
  14 | {
  15 |     [DefaultExecutionOrder(-100)]
  16 |     [RequireComponent(typeof(CharacterMovement), typeof(CombatMeleeHit), typeof(CharacterInputHandler))]
  17 |     [RequireComponent(typeof(CharacterOrientation))]
  18 |     public sealed class CharacterRoot : MonoBehaviour
  19 |     {
  20 |         [SerializeField] private string actorId = "player";
  21 |         [FormerlySerializedAs("motor"), SerializeField] private CharacterMovement movement;
  22 |         [SerializeField] private CharacterOrientation orientation;
  23 |         [SerializeField] private CharacterSpineView view;
  24 |         [SerializeField] private CombatMeleeHit melee;
  25 |         [SerializeField] private CharacterInputHandler input;
  26 |         [SerializeField] private Camera viewCamera;
  27 |         [SerializeField] private CharacterFollow follow;
  28 |         [SerializeField] private CombatVitals vitals;
  29 |         [SerializeField] private CombatPosture posture;
  30 |         private ICombatLifeState lifeState;
  31 |         [Header("Prototype timing — not final balance")]
  32 |         [SerializeField] private float releaseAt = .3f, moveReadyAt = .5f, attackReadyAt = .8f, finishAt = 1.1f;
  33 |         [SerializeField, Min(1)] private int damage = 12;
  34 |         [SerializeField, Min(0)] private float postureImpact;
  35 |         [SerializeField] private bool verboseTrace;
  36 |         private readonly ControlAuthority authority = new ControlAuthority();
  37 |         private readonly AttackIntent intent = new AttackIntent();
  38 |         private ICharacterInputSource inputSource;
  39 |         private CharacterIntentSelector intentSelector;
  40 |         private ICharacterMovement mover;
  41 |         private ICharacterOrientation orienter;
  42 |         private ICharacterPresentation presentation;
  43 |         private ICombatHitResolver hitResolver;
  44 |         private BasicActionRuntime runtime;
  45 |         private BasicAttackDefinition definition;
  46 |         private BasicAttackDefinition secondDefinition;
  47 |         private BasicChainState chain;
  48 |         private BasicChainProfile chainProfile;
  49 |         private ICombatHitReceiptSource hitReceipts;
  50 |         public int AttackStage { get; private set; }
  51 |         public int ReleaseCount { get; private set; }
  52 |         public bool HasBufferedAttack => chain!=null && chain.HasBuffer;
  53 |         public double ComboWindowUntil => chain==null?-1:chain.WindowUntil;
  54 |         private bool ready, focused = true, paused, applicationPaused;
  55 |         private string lastFact = "Idle";
  56 |         private object respawnOwner;
  57 |         private int lifeGeneration, animationGeneration;
  58 |         public int LifeGeneration => lifeGeneration;
  59 |         public bool IsSimulationSuspended => !focused || IsPaused;
  60 |         public CharacterDataBus DataBus { get; private set; }
  61 |         public string ActorId => actorId;
  62 |         public string LastFact => lastFact;
  63 |         public bool IsPaused => paused || applicationPaused;
  64 |         public bool ActionActive => DataBus != null && DataBus.ActionActive;
  65 |         public bool IsAlive => lifeState==null || lifeState.IsAlive;
  66 |         public bool CanSimulateIntent => ready && IsAlive && isActiveAndEnabled && focused && !IsPaused && (posture == null || !posture.IsStaggered);
  67 |         public CombatVitals Vitals => vitals;
  68 |         public event Action<CharacterRoot> Died;
  69 |         public CharacterFollow Follow => follow;
  70 |         public CharacterIntentKind EffectiveIntentKind => intentSelector == null ? CharacterIntentKind.Neutral : intentSelector.EffectiveKind;
  71 | 
  72 |         public bool CanBindCompanionIntent(ICharacterControlSession session, ICompanionIntentSource source)
  73 |             => ready && follow != null && ReferenceEquals(source,follow) && intentSelector != null && intentSelector.CanBind(session,source);
  74 |         public bool BindCompanionIntent(ICharacterControlSession session, ICompanionIntentSource source)
  75 |             => CanBindCompanionIntent(session,source) && intentSelector.Bind(session,source);
  76 |         public void ClearCompanionIntent(ICompanionIntentSource owner)
  77 |         { if (intentSelector != null) intentSelector.Clear(owner); }
  78 | 
  79 |         internal bool BindRespawnOwner(object owner)
  80 |         {
  81 |             if(owner==null || (respawnOwner!=null && !ReferenceEquals(respawnOwner,owner))) return false;
  82 |             respawnOwner=owner; return true;
  83 |         }
  84 |         internal void UnbindRespawnOwner(object owner)
  85 |         { if(ReferenceEquals(respawnOwner,owner)) respawnOwner=null; }
  86 |         internal bool CanRespawn(object owner) => ready && isActiveAndEnabled && !IsAlive &&
  87 |             vitals!=null && vitals.isActiveAndEnabled && ReferenceEquals(respawnOwner,owner);
  88 |         internal bool Respawn(object owner, Vector3 position)
  89 |         {
  90 |             if(!CanRespawn(owner)) return false;
  91 |             CancelAction("scene-respawn");
  92 |             if(follow!=null) follow.ResetIntent();
  93 |             if(intentSelector!=null) intentSelector.Clear();
  94 |             input.ResetPlayerInput(true);
  95 |             orientation.ResetOrientation();
  96 |             movement.Teleport(position);
  97 |             if(posture!=null) posture.ResetState();
  98 |             if(!vitals.RestoreAfterDeath()) return false;
  99 |             lifeGeneration++;
 100 |             return true;
 101 |         }
 102 | 
 103 |         private void Awake()
 104 |         {
 105 |             if (viewCamera == null) viewCamera = Camera.main;
 106 |             if (vitals!=null)
 107 |             {
 108 |                 if (!vitals.Initialize(actorId)) { Debug.LogError("Character vitals identity/configuration invalid",this); enabled=false; return; }
 109 |                 lifeState=vitals; vitals.Died+=OnDied;
 110 |             }
 111 |             if (movement == null || orientation == null || view == null || melee == null || input == null || viewCamera == null)
 112 |             { Debug.LogError("Character requires registered input/movement/orientation/view/melee/camera", this); enabled = false; return; }
 113 |             // Inspector保留具体组件槽；运行期通过窄接口消费，不反向依赖开发GUI或测试靶。
 114 |             // 每角色只有这一条有效Frame入口；没有Director绑定时保持原玩家适配器行为。
 115 |             intentSelector = new CharacterIntentSelector(this,input);
 116 |             inputSource = intentSelector;
 117 |             mover = movement;
 118 |             orienter = orientation;
 119 |             presentation = view;
 120 |             hitResolver = melee;
 121 |             if (!inputSource.IsConfigured) { Debug.LogError("Character actions are not configured", this); enabled = false; return; }
 122 |             try
 123 |             {
 124 |                 definition = new BasicAttackDefinition(releaseAt, moveReadyAt, attackReadyAt, finishAt, damage, postureImpact);
 125 |                 runtime = new BasicActionRuntime(actorId);
 126 |                 chainProfile=GetComponent<BasicChainProfile>();
 127 |                 // Root的早期Awake可能先于Profile.OnEnable；此处读取序列化enabled，运行期再查isActiveAndEnabled。
 128 |                 if(chainProfile!=null && chainProfile.enabled)
 129 |                 {
 130 |                     definition=chainProfile.stage1.Freeze(); secondDefinition=chainProfile.stage2.Freeze();
 131 |                     chain=chainProfile.CreateState(); hitReceipts=melee; hitReceipts.Resolved+=OnHitReceipt;
 132 |                 }
 133 |             }
 134 |             catch (ArgumentException error) { Debug.LogError("Character configuration: " + error.Message, this); enabled = false; return; }
 135 |             // 每角色独立Bus只读取原所有者；只有确需Bus的运动和表现执行初始化。
 136 |             DataBus = new CharacterDataBus(actorId, inputSource, mover, orienter, runtime, authority);
 137 |             ready = InitializeSubsystems(DataBus, mover, presentation);
 138 |             if (!ready) { Debug.LogError("Character subsystem initialization failed", this); enabled = false; }
 139 |             // 可选能力只接受本角色初始化；无组件/无assignment的旧场保持普通玩家行为。
 140 |             if (follow != null && !follow.Initialize(this)) Debug.LogWarning("Character follow unavailable: configuration or owner invalid", this);
 141 |             if (posture != null)
 142 |             {
 143 |                 if (!posture.Initialize(vitals, this)) { Debug.LogError("Character posture configuration invalid", this); enabled = false; return; }
 144 |                 posture.Broken += OnPostureBroken;
 145 |                 posture.Recovered += OnPostureRecovered;
 146 |             }
 147 |         }
 148 | 
 149 |         private static bool InitializeSubsystems(CharacterDataBus dataBus, params ICharacterSubsystem[] subsystems)
 150 |         {
 151 |             foreach (var subsystem in subsystems) if (subsystem == null || !subsystem.Initialize(dataBus)) return false;
 152 |             return true;
 153 |         }
 154 | 
 155 |         private void Update()
 156 |         {
 157 |             if (!ready) return;
 158 |             if (!IsAlive) { CommitPresentation(); return; }
 159 |             // 本体显式调度入口：以下阶段在一次Update中按书写顺序执行。
 160 |             // DefaultExecutionOrder只约束跨组件先后，不能替代这里的模块调用顺序。
 161 |             // 1. 采唯一意图源并处理暂停/取消；不由各子模块另开输入Update。
 162 |             // 选择器只采一个源；伙伴只能提供移动，切人屏障为空帧，动作时钟仍继续。
 163 |             inputSource.Read();
 164 |             var frame = DataBus.Input;
 165 |             if(chain!=null && (!input.HasPlayerInput || IsPaused || !focused || chainProfile==null || !chainProfile.isActiveAndEnabled)) chain.Clear();
 166 |             if (frame.pause) SetPaused(!paused);
 167 |             if (frame.cancel) CancelAction("player-cancel");
 168 |             // 2. 同一根时钟推进架势；主动作暂停自然恢复，暂停/失焦完全停钟。
 169 |             // 在Request前解除到期的本owner禁令；不会回滚其他根已释放的命中。
 170 |             if (posture != null) posture.Tick(IsPaused || !focused ? 0 : Time.deltaTime, runtime.IsActive);
 171 |             // 3. 先解算朝向，攻击释放才能读取本帧鼠标/键盘方向。
 172 |             var canMove = focused && !IsPaused && DataBus.AllowsMovement && DataBus.MoveReady;
 173 |             if (focused && !IsPaused)
 174 |                 orienter.ResolveDirections(frame.move, PointerDirection(frame), frame.hasDirectionalAim ? frame.aimDirection : Vector2.zero, canMove);
 175 |             // 4. 观察按键边沿，再由Runtime的暂停/权限/Ready资格决定是否接受动作。
 176 |             if (intent.Observe(frame.attackHeld) && focused && !frame.attackPointerBlocked)
 177 |             {
 178 |                 var stage=chain==null?1:chain.Request(runtime.SimulationTime,runtime.AttackReady,
 179 |                     input.HasPlayerInput && !IsPaused && authority.Allows(ControlChannel.Attack) && chainProfile!=null && chainProfile.isActiveAndEnabled);
 180 |                 if(stage>0) RequestStage(stage);
 181 |             }
 182 |             // 5. 唯一动作时钟推进与Release命中。Accepted不扣血，切人不重发事实。
 183 |             foreach (var fact in runtime.Advance(Time.deltaTime, IsPaused || !focused))
 184 |             {
 185 |                 Observe(fact);
 186 |                 if (fact.Release != null)
 187 |                 {
 188 |                     ReleaseCount++;
 189 |                     // 在释放当帧按值采样方向；不是接受动作时锁定，前摇中仍可重新瞄准。
 190 |                     var releaseDirection = orienter.AttackDirection;
 191 |                     orienter.FaceAttackDirection();
 192 |                     hitResolver.Resolve(fact.Release, releaseDirection);
 193 |                 }
 194 |             }
 195 |             if(chain!=null && input.HasPlayerInput && !IsPaused && focused && authority.Allows(ControlChannel.Attack))
 196 |             {
 197 |                 var bufferedStage=chain.Poll(runtime.SimulationTime,runtime.AttackReady);
 198 |                 if(bufferedStage>0) RequestStage(bufferedStage);
 199 |             }
 200 |             // 6. 动作/命中可能改变MoveReady或架势权限，重新查询后才执行真实移动。
 201 |             canMove = focused && !IsPaused && DataBus.AllowsMovement && DataBus.MoveReady;
 202 |             mover.Tick(canMove, IsPaused || !focused ? 0 : Time.deltaTime);
 203 |             // 7. 最后提交表现，Run读取已完成的实际位移。
 204 |             // 当前没有本体IK/骨骼后处理，故不虚设LateUpdate；Spine内部播放仍由插件驱动。
 205 |             CommitPresentation();
 206 |         }
 207 | 
 208 |         // 7. Animation Intent → One Commit；无子模块Update争抢主轨。
 209 |         private void CommitPresentation()
 210 |         {
 211 |             (presentation as Ossastra.AnimationPresentation.ICharacterAnimationFacts)?.CollectFacts(IsAlive, posture!=null && posture.IsStaggered, vitals!=null && vitals.IsHitReacting, animationGeneration, AttackStage);
 212 |             presentation.SetPaused(IsPaused || !focused);
 213 |             presentation.Present();
 214 |         }
 215 |         private Vector3? PointerDirection(CharacterInputFrame frame)
 216 |         {
 217 |             if (!frame.hasPointer) return null;
 218 |             var plane = new Plane(Vector3.up, transform.position);
 219 |             var ray = viewCamera.ScreenPointToRay(frame.pointer);
 220 |             if (!plane.Raycast(ray, out var distance)) return null;
 221 |             var direction = ray.GetPoint(distance) - transform.position;
 222 |             direction.y = 0;
 223 |             return direction.sqrMagnitude < .0001f ? (Vector3?)null : direction;
 224 |         }
 225 | 
 226 |         private void Observe(ActionFact fact)
 227 |         {
 228 |             lastFact = fact.Kind + " @" + fact.At.ToString("F3") + " " + fact.ActionId.ToString("N").Substring(0, 8);
 229 |             if (fact.Reason != null) lastFact += " " + fact.Reason;
 230 |             if (verboseTrace) Debug.Log("Character " + lastFact, this);
 231 |         }
 232 |         private void RequestStage(int stage)
 233 |         {
 234 |             var selected=stage==2?secondDefinition:definition;
 235 |             var request=runtime.Request(selected,"player-input:Stage"+stage,authority,IsPaused);
 236 |             if(request.PreviousTerminal!=null) Observe(request.PreviousTerminal);
 237 |             if(request.Accepted)
 238 |             {
 239 |                 AttackStage=stage; chain?.Accepted(stage,request.Fact.ActionId,runtime.SimulationTime,selected.AttackReadyAt);
 240 |                 Observe(request.Fact); orienter.FaceAttackDirection();
 241 |             }
 242 |             else lastFact="Rejected: "+request.Reason;
 243 |         }
 244 |         private void OnHitReceipt(Guid id,bool applied){chain?.Hit(id,applied);}
 245 | 
 246 |         public void CancelAction(string reason)
 247 |         {
 248 |             animationGeneration++;
 249 |             chain?.Clear();
 250 |             // 取消必须具名；抑制仍按住的输入，不能把下一帧误判为新请求。
 251 |             intent.SuppressUntilRelease();
 252 |             var terminal = runtime == null ? null : runtime.Cancel(reason);
 253 |             if (terminal != null) Observe(terminal);
 254 |         }
 255 | 
 256 |         private void OnPostureBroken(string cause)
 257 |         {
 258 |             authority.Block(posture, ControlChannel.Move | ControlChannel.Attack);
 259 |             CancelAction("posture-broken:" + cause);
 260 |             if (follow != null) follow.ResetIntent();
 261 |         }
 262 |         private void OnPostureRecovered()
 263 |         {
 264 |             authority.Release(posture);
 265 |             intent.SuppressUntilRelease();
 266 |             if (follow != null) follow.ResetIntent();
 267 |         }
 268 | 
 269 |         private void OnDied(HitOutcome outcome)
 270 |         {
 271 |             // HP已由结算归零；这里仅终止本体未释放动作/有效帧，再交唯一会话处理实控。
 272 |             CancelAction("death:"+outcome.ActorId+":"+outcome.ActionId.ToString("N"));
 273 |             if (follow!=null) follow.ResetIntent();
 274 |             if (intentSelector!=null) intentSelector.Clear();
 275 |             Died?.Invoke(this);
 276 |         }
 277 | 
 278 |         public void SetPaused(bool value) { paused = value; chain?.Clear(); intent.SuppressUntilRelease(); if (follow != null) follow.ResetIntent(); }
 279 |         public void SetAttackRestriction(object owner, bool restricted, string reason = "attack-restricted")
 280 |         {
 281 |             if (restricted) { authority.Block(owner, ControlChannel.Attack); CancelAction(reason); }
 282 |             else authority.Release(owner);
 283 |         }
 284 | 
 285 |         private void OnApplicationFocus(bool hasFocus)
 286 |         { focused = hasFocus; if (follow != null) follow.ResetIntent(); if (!hasFocus) CancelAction("focus-lost"); else intent.SuppressUntilRelease(); }
 287 |         private void OnApplicationPause(bool value)
 288 |           { applicationPaused = value; if(value)chain?.Clear(); if (follow != null) follow.ResetIntent(); if (value) intent.SuppressUntilRelease(); }
 289 |         private void OnEnable() { intent.SuppressUntilRelease(); if (intentSelector != null) intentSelector.Clear(); }
 290 |         private void OnDisable() { CancelAction("disabled"); if (posture != null) posture.ResetState(); authority.Release(posture); if (follow != null) follow.ResetIntent(); if (intentSelector != null) intentSelector.Clear(); if (ready && presentation != null) presentation.SetPaused(false); }
 291 |         private void OnDestroy()
 292 |         {
 293 |             if(hitReceipts!=null) hitReceipts.Resolved-=OnHitReceipt;
 294 |             if (vitals!=null) vitals.Died-=OnDied;
 295 |             if (posture != null) { posture.Broken -= OnPostureBroken; posture.Recovered -= OnPostureRecovered; }
 296 |             authority.Release(posture);
 297 |             // 终止本角色动作后解除本对象引用；不清其它角色或场景控制权威。
 298 |             CancelAction("destroyed");
 299 |             ready = false;
 300 |             DataBus = null;
 301 |             runtime = null;
 302 |             inputSource = null;
 303 |             if (intentSelector != null) intentSelector.Clear();
 304 |             intentSelector = null;
 305 |             mover = null;
 306 |             orienter = null;
 307 |             presentation = null;
 308 |             hitResolver = null;
 309 |         }
 310 |     }
 311 | }
```

## Assets/OSSASTRA/Scripts/Characters/Runtime/CharacterSpineView.cs

SHA256: 371128DB4EAEAE18A561B3A9B25D09C0433BCC22FA7C667D48EF8BD4AEE78169；审查行1–75

```csharp
   1 | /*
   2 |  * 脚本：CharacterSpineView
   3 |  * 功能：Root运动后采集只读语义，经公共仲裁和配置映射，单次提交Spine主轨。
   4 |  * 边界：不定义战斗时钟、不监听Complete扣血；死亡仍由Vitals立即隐藏。
   5 |  * 协作：旧idle/move/attack序列化引用保留；可选catalog覆盖，缺片段明确回退。
   6 |  */
   7 | using System;
   8 | using System.Collections.Generic;
   9 | using Ossastra.AnimationCore;
  10 | using SemanticIntent = Ossastra.AnimationCore.AnimationIntent;
  11 | using PlaybackIdentity = Ossastra.AnimationCore.AnimationPlaybackIdentity;
  12 | using Ossastra.AnimationPresentation;
  13 | using Spine.Unity;
  14 | using UnityEngine;
  15 | namespace Ossastra.Characters
  16 | {
  17 |     public sealed class CharacterSpineView : MonoBehaviour, ICharacterPresentation, ICharacterAnimationFacts
  18 |     {
  19 |         [SerializeField] private SkeletonAnimation skeleton;
  20 |         [SerializeField] private string idle="idle1_1", move="run", attack="atk1";
  21 |         [SerializeField] private AnimationClipMap[] catalog=Array.Empty<AnimationClipMap>();
  22 |         private CharacterDataBus dataBus;
  23 |         private AnimationResolvePolicy policy;
  24 |         private readonly PlaybackIdentity identity=new PlaybackIdentity();
  25 |         private readonly List<SemanticIntent> intents=new List<SemanticIntent>(6);
  26 |         private float originalScaleX, originalTimeScale;
  27 |         private bool alive=true, staggered, hitReaction;
  28 |         private int generation, attackStage;
  29 |         public string CurrentClip { get; private set; }
  30 |         public AnimationSemantic CurrentSemantic { get; private set; }
  31 |         public string CurrentSource { get; private set; }
  32 |         public string FallbackReason { get; private set; }
  33 |         public Guid CurrentReplayId { get; private set; }
  34 |         public int CommitCount { get; private set; }
  35 |         public bool Initialize(CharacterDataBus dataBus)
  36 |         {
  37 |             if(dataBus==null)return false;this.dataBus=dataBus;policy=new AnimationResolvePolicy(dataBus.ActorId);
  38 |             if(skeleton==null)skeleton=GetComponent<SkeletonAnimation>();
  39 |             if(skeleton==null||skeleton.skeletonDataAsset==null)return false;
  40 |             skeleton.Initialize(false);var data=skeleton.skeletonDataAsset.GetSkeletonData(false);
  41 |             if(data==null||data.FindAnimation(idle)==null||data.FindAnimation(move)==null||data.FindAnimation(attack)==null||skeleton.Skeleton==null)return false;
  42 |             originalScaleX=Mathf.Abs(skeleton.Skeleton.ScaleX);originalTimeScale=skeleton.timeScale;return true;
  43 |         }
  44 |         public void SetPaused(bool paused){if(skeleton!=null)skeleton.timeScale=paused?0:originalTimeScale;}
  45 |         public void CollectFacts(bool alive,bool staggered,bool hitReaction,int generation,int attackStage=1)
  46 |         {this.alive=alive;this.staggered=staggered;this.hitReaction=hitReaction;this.generation=generation;this.attackStage=attackStage;}
  47 |         private void Add(AnimationSemantic semantic,string source,Guid replay)
  48 |         {intents.Add(new SemanticIntent(dataBus.ActorId,semantic,source,replay,generation));}
  49 |         public void Present()
  50 |         {
  51 |             if(dataBus==null||skeleton==null||skeleton.Skeleton==null||skeleton.AnimationState==null)return;
  52 |             // 每帧重新采集，不保留过期请求；Root本帧事实是唯一资格来源。
  53 |             intents.Clear();Add(AnimationSemantic.Idle,"movement",Guid.Empty);
  54 |             if(dataBus.IsMoving)Add(AnimationSemantic.Move,"movement",Guid.Empty);
  55 |             if(dataBus.ActionActive)Add(attackStage==2?AnimationSemantic.BasicAttackSecond:AnimationSemantic.BasicAttack,"action",dataBus.ActionId);
  56 |             if(hitReaction)Add(AnimationSemantic.HitReaction,"vitals",Guid.Empty);
  57 |             if(staggered)Add(AnimationSemantic.PostureBroken,"posture",Guid.Empty);
  58 |             if(!alive)Add(AnimationSemantic.Death,"life",Guid.Empty);
  59 |             var selected=policy.Resolve(intents,generation);if(!selected.HasValue)return;
  60 |             var intent=selected.Value;var clip=idle;var loop=true;var mix=.08f;FallbackReason="";
  61 |             // 旧三片段为兼容默认映射；Inspector catalog可覆盖全部语义，无需改战斗代码。
  62 |             if(intent.Semantic==AnimationSemantic.Move)clip=move;
  63 |             else if(intent.Semantic==AnimationSemantic.BasicAttack || intent.Semantic==AnimationSemantic.BasicAttackSecond){clip=attack;loop=false;if(intent.Semantic==AnimationSemantic.BasicAttackSecond)FallbackReason="Stage2无独立片段，复用atk1并按新ActionId重播";}
  64 |             else if(intent.Semantic!=AnimationSemantic.Idle)FallbackReason="缺少 "+intent.Semantic+" 映射，回退Idle（玩法事实不变）";
  65 |             foreach(var entry in catalog??Array.Empty<AnimationClipMap>())
  66 |                 if(entry!=null&&entry.semantic==intent.Semantic){clip=entry.clip;loop=entry.loop;mix=Mathf.Max(0,entry.mixDuration);FallbackReason="";break;}
  67 |             if(string.IsNullOrEmpty(clip)||skeleton.Skeleton.Data.FindAnimation(clip)==null)
  68 |             {FallbackReason="片段缺失："+clip+"，回退Idle";clip=idle;loop=true;}
  69 |             skeleton.Skeleton.ScaleX=originalScaleX*dataBus.VisualFacingX;
  70 |             if(identity.ShouldCommit(clip,intent))
  71 |             {var track=skeleton.AnimationState.SetAnimation(0,clip,loop);track.MixDuration=mix;CommitCount++;}
  72 |             CurrentClip=clip;CurrentSemantic=intent.Semantic;CurrentSource=intent.Source;CurrentReplayId=intent.ReplayId;
  73 |         }
  74 |     }
  75 | }
```

## Assets/OSSASTRA/Scripts/Characters/Runtime/PartyRespawnCoordinator.cs

SHA256: 6E25917BBA6A6E99BF0FE0D77670D0EACD09367B06750C9C7DBB4AC00C617AE4；审查行1–91

```csharp
   1 | /*
   2 |  * 脚本：PartyRespawnCoordinator
   3 |  * 功能：原会话成员的场景内返场；唯一模拟计时，先检查全批落点再恢复本体。
   4 |  * 边界：不保存第二份HP/Current，不重置敌人或物品；没有组件的旧场不会自动复生。
   5 |  */
   6 | using System;
   7 | using System.Collections.Generic;
   8 | using System.Linq;
   9 | using Ossastra.CharacterCore;
  10 | using UnityEngine;
  11 | namespace Ossastra.Characters
  12 | {
  13 |     [DefaultExecutionOrder(-150)]
  14 |     public sealed class PartyRespawnCoordinator : MonoBehaviour
  15 |     {
  16 |         [SerializeField] private CharacterControlCoordinator session;
  17 |         [SerializeField] private CapsuleRespawnPlacement placement;
  18 |         [SerializeField] private Vector3[] checkpoints=Array.Empty<Vector3>();
  19 |         [SerializeField, Min(0)] private float singleDelay=10, wipeDelay=.5f;
  20 |         private CharacterRoot[] members;
  21 |         private RespawnSchedule schedule;
  22 |         private Action<HitOutcome>[] deathHandlers;
  23 |         private bool focused=true, applicationPaused;
  24 |         public string Status { get; private set; }="未初始化";
  25 |         public double SimulationTime => schedule==null?0:schedule.Time;
  26 |         public double DueAt(int index) => schedule==null?-1:schedule.DueAt(index);
  27 |         public double DiedAt(int index) => schedule==null?-1:schedule.DiedAt(index);
  28 |         private void Start()
  29 |         {
  30 |             if(session==null || placement==null || checkpoints.Length!=session.Characters.Count)
  31 |             {Status="返场配置无效"; enabled=false; return;}
  32 |             members=session.Characters.ToArray();
  33 |             if(members.Any(x=>x==null) || members.Distinct().Count()!=members.Length)
  34 |             {Status="成员重复或缺失";enabled=false;return;}
  35 |             foreach(var member in members)
  36 |                 if(!member.BindRespawnOwner(this))
  37 |                 {
  38 |                     foreach(var owned in members) owned.UnbindRespawnOwner(this);
  39 |                     Status="返场所有权冲突";enabled=false;return;
  40 |                 }
  41 |             schedule=new RespawnSchedule(members.Length,singleDelay,wipeDelay);
  42 |             deathHandlers=new Action<HitOutcome>[members.Length];
  43 |             for(var i=0;i<members.Length;i++)
  44 |             {
  45 |                 var index=i;
  46 |                 deathHandlers[i]=outcome=>schedule.RecordDeath(index);
  47 |                 members[i].Vitals.Died+=deathHandlers[i];
  48 |             }
  49 |             Status="等待死亡";
  50 |         }
  51 |         private void Update()
  52 |         {
  53 |             if(schedule==null || session==null || !session.isActiveAndEnabled || placement==null || !placement.isActiveAndEnabled) return;
  54 |             var available=members.Select(x=>x!=null && x.isActiveAndEnabled && session.Characters.Contains(x)).ToArray();
  55 |             var alive=members.Select(x=>x!=null && x.IsAlive).ToArray();
  56 |             var suspended=!focused || applicationPaused || members.Any(x=>x!=null && x.IsSimulationSuspended);
  57 |             schedule.Tick(alive,available,Time.deltaTime,suspended);
  58 |             var due=schedule.ReadyIndices;
  59 |             if(due.Length==0) {Status=suspended?"模拟暂停":"等待到期";return;}
  60 |             var positions=new List<Vector3>();
  61 |             foreach(var index in due)
  62 |             {
  63 |                 var member=members[index];
  64 |                 if(!member.CanRespawn(this) || !placement.TryFind(member.GetComponent<CharacterController>(),checkpoints[index],positions,out var point))
  65 |                 {Status="等待合法落点";return;}
  66 |                 positions.Add(point);
  67 |             }
  68 |             // 单线程事务：所有资格/落点均已验证，恢复不调用外部事件，不允许部分失败后继续下一批。
  69 |             for(var i=0;i<due.Length;i++)
  70 |             {
  71 |                 if(!members[due[i]].Respawn(this,positions[i])) {Status="返场事务失效";enabled=false;return;}
  72 |                 schedule.Complete(due[i]);
  73 |             }
  74 |             Physics.SyncTransforms();
  75 |             session.ReconcileAfterRespawn();
  76 |             Status="场景内返场完成";
  77 |         }
  78 |         private void OnApplicationFocus(bool value) {focused=value;}
  79 |         private void OnApplicationPause(bool value) {applicationPaused=value;}
  80 |         private void OnDestroy()
  81 |         {
  82 |             if(members==null) return;
  83 |             for(var i=0;i<members.Length;i++)
  84 |             {
  85 |                 var member=members[i]; if(member==null)continue;
  86 |                 if(deathHandlers!=null && member.Vitals!=null) member.Vitals.Died-=deathHandlers[i];
  87 |                 member.UnbindRespawnOwner(this);
  88 |             }
  89 |         }
  90 |     }
  91 | }
```

## Assets/OSSASTRA/Scripts/Combat/Runtime/CombatContracts.cs

SHA256: 30B2ED98B92641B10528326617F79E8506642D727A3CECF5582110B3B26BC338；审查行1–46

```csharp
   1 | /*
   2 |  * 脚本：CombatContracts
   3 |  * 功能：中立命中、生命及世界目标契约；玩家输入和敌方决策共用。
   4 |  * 边界：Health为唯一HP权威；接口通知不重复扣血，不保存玩家Current。
   5 |  * 协作：玩家根/敌人根/物理解析/感知真实消费；旧开发靶只需IHitTarget。
   6 |  */
   7 | using Ossastra.CharacterCore;
   8 | using UnityEngine;
   9 | namespace Ossastra.Combat
  10 | {
  11 |     /// <summary>世界命中器向同一合法存活目标递交架势冲击，不拥有第二次HP写入。</summary>
  12 |     public interface ICombatPostureReceiver
  13 |     {
  14 |         void ReceiveImpact(HitOutcome outcome, double impact);
  15 |     }
  16 |     public interface ICombatLifeState { bool IsAlive { get; } }
  17 |     public interface IHitTarget { Health Health { get; } void ShowHit(); }
  18 |     public interface ICombatTarget : IHitTarget, ICombatLifeState
  19 |     {
  20 |         bool IsAvailable { get; }
  21 |         CombatFaction Faction { get; }
  22 |         Vector3 WorldPosition { get; }
  23 |         Transform TargetTransform { get; }
  24 |         void OnHitResolved(HitOutcome outcome);
  25 |     }
  26 |     public interface ICombatHitResolver
  27 |     {
  28 |         string LastResult { get; }
  29 |         void Resolve(AttackRelease release, Vector3 releaseDirection);
  30 |     }
  31 |     /// <summary>原物理结算的只读回执；禁止消费者再查物理或再扣血。</summary>
  32 |     public interface ICombatHitReceiptSource
  33 |     {
  34 |         event System.Action<System.Guid, bool> Resolved;
  35 |     }
  36 |     /// <summary>接口非null不代表Unity宿主有效；销毁/禁用/无生命立即失效。</summary>
  37 |     public static class CombatTargetValidity
  38 |     {
  39 |         public static bool IsAvailable(IHitTarget target)
  40 |         {
  41 |             var component=target as MonoBehaviour;
  42 |             return component!=null && component.isActiveAndEnabled && target.Health!=null &&
  43 |                 target.Health.Current>0 && (!(target is ICombatTarget combat) || combat.IsAvailable);
  44 |         }
  45 |     }
  46 | }
```

## Assets/OSSASTRA/Scripts/Combat/Runtime/CombatMeleeHit.cs

SHA256: 6171818EA358EB89A98CC3ED70D43FAD71923ABF48ACD9853E4D7E5BCAEF10B9；审查行1–54

```csharp
   1 | /*
   2 |  * 脚本：CombatMeleeHit
   3 |  * 功能：将一次释放转换为物理候选、身份/阵营资格和唯一伤害批次。
   4 |  * 边界：中立能力，无玩家Root依赖，不建动作时钟或HP；空挥也关闭Release。
   5 |  * 协作：两类战斗根各自释放；Applied先反馈/死亡，存活者再接可选架势，不重复扣血。
   6 |  */
   7 | using System;
   8 | using System.Linq;
   9 | using Ossastra.CharacterCore;
  10 | using UnityEngine;
  11 | namespace Ossastra.Combat
  12 | {
  13 |     public sealed class CombatMeleeHit : MonoBehaviour, ICombatHitResolver, ICombatHitReceiptSource
  14 |     {
  15 |         [SerializeField, Min(0)] private float reach=.8f;
  16 |         [SerializeField, Min(.01f)] private float radius=.65f;
  17 |         [SerializeField] private float height=.8f;
  18 |         [SerializeField] private LayerMask targetLayers=~0;
  19 |         public string LastResult { get; private set; }="未释放";
  20 |         public event Action<Guid, bool> Resolved;
  21 |         public void Resolve(AttackRelease release, Vector3 releaseDirection)
  22 |         {
  23 |             if(release==null) throw new ArgumentNullException(nameof(release));
  24 |             var source=GetComponent<CombatVitals>();
  25 |             if(!isActiveAndEnabled || (source!=null && (!source.IsAvailable || source.Health.Id!=release.ActorId)))
  26 |             { release.Resolve(Array.Empty<Health>()); LastResult="来源失效或释放身份不符"; Resolved?.Invoke(release.ActionId,false); return; }
  27 |             var center=transform.position+Vector3.up*height+releaseDirection*reach;
  28 |             var candidates=Physics.OverlapSphere(center,radius,targetLayers,QueryTriggerInteraction.Collide)
  29 |                 .Select(c=>c.GetComponentsInParent<MonoBehaviour>().OfType<IHitTarget>().FirstOrDefault())
  30 |                 .Where(CombatTargetValidity.IsAvailable).Distinct()
  31 |                 .Where(t=>t is MonoBehaviour body && CombatSight.IsClear(transform,body.transform,height)).ToArray();
  32 |             // 同一接收者多个Collider合并；不同接收者冒用同ID整组拒绝，不能任取一个扣HP。
  33 |             var targets=candidates.GroupBy(t=>t.Health.Id,StringComparer.Ordinal)
  34 |                 .Where(g=>g.Count()==1).Select(g=>g.Single())
  35 |                 .Where(t=>CombatTargetPolicy.CanHit(release.ActorId,
  36 |                     source==null?CombatFaction.Neutral:source.Faction,source==null||source.IsAlive,
  37 |                     t.Health.Id,t is ICombatTarget combat?combat.Faction:CombatFaction.Neutral,true)).ToArray();
  38 |             var outcomes=release.Resolve(targets.Select(t=>t.Health));
  39 |             foreach(var outcome in outcomes)
  40 |                 if(outcome.Reason==HitReason.Applied)
  41 |                 {
  42 |                     var target=targets.First(t=>t.Health.Id==outcome.TargetId);
  43 |                     if(target is ICombatTarget combat) combat.OnHitResolved(outcome); else target.ShowHit();
  44 |                     // HP/死亡通知已完成；同批存活者才消费架势。不会再做空间查询或伤害结算。
  45 |                     if (outcome.HpAfter > 0 && target is MonoBehaviour component && component != null)
  46 |                         foreach (var receiver in component.GetComponents<MonoBehaviour>().OfType<ICombatPostureReceiver>())
  47 |                             receiver.ReceiveImpact(outcome, release.PostureImpact);
  48 |                 }
  49 |             LastResult=outcomes.Count==0?"空挥／无合法敌对目标":string.Join("; ",outcomes.Select(o=>
  50 |                 o.ActorId+" → "+o.TargetId+" "+o.HpBefore+"→"+o.HpAfter+" "+o.Reason+" #"+o.ActionId.ToString("N")));
  51 |             Resolved?.Invoke(release.ActionId,outcomes.Any(o=>o.Reason==HitReason.Applied));
  52 |         }
  53 |     }
  54 | }
```

## Assets/OSSASTRA/Scripts/Combat/Runtime/CombatSight.cs

SHA256: DAE4D1B586A482517AC60C84E7605D119926F19E0EA3A81B7876E55ECBB6B105；审查行1–20

```csharp
   1 | /*
   2 |  * 脚本：CombatSight
   3 |  * 功能：三维世界实体间的中立视线查询，忽略双方本体碰撞，阻挡物不可穿透。
   4 |  * 边界：只读物理，不选Current、不扣HP；敌感知和正式近战共用。
   5 |  */
   6 | using UnityEngine;
   7 | namespace Ossastra.Combat
   8 | {
   9 |     public static class CombatSight
  10 |     {
  11 |         public static bool IsClear(Transform source, Transform target,float height=.8f)
  12 |         {
  13 |             if(source==null||target==null)return false;
  14 |             var start=source.position+Vector3.up*height;var end=target.position+Vector3.up*height;
  15 |             foreach(var hit in Physics.RaycastAll(start,end-start,Vector3.Distance(start,end),~0,QueryTriggerInteraction.Ignore))
  16 |                 if(!hit.transform.IsChildOf(source)&&!hit.transform.IsChildOf(target))return false;
  17 |             return true;
  18 |         }
  19 |     }
  20 | }
```

## Assets/OSSASTRA/Scripts/Combat/Runtime/CombatVitals.cs

SHA256: 6782EF455ACE8BA9BEC668A320CAEC2AA39322D1F2307609493D44D8C3A3516D；审查行1–102

```csharp
   1 | /*
   2 |  * 脚本：CombatVitals
   3 |  * 功能：持有本体唯一Health/阵营，接收正式结算反馈并单次通知死亡、关闭自身表现和碰撞。
   4 |  * 边界：IsAlive只读Health；不另存isDead，不选Current、不计算攻击、不自动复活。
   5 |  * 协作：Root/敌人显式Initialize；Melee反馈已Applied结果；生产返场与开发回放分别进入同一本体恢复。
   6 |  */
   7 | using System;
   8 | using Ossastra.CharacterCore;
   9 | using Ossastra.Combat;
  10 | using UnityEngine;
  11 | 
  12 | namespace Ossastra.Combat
  13 | {
  14 |     [DisallowMultipleComponent]
  15 |     public sealed class CombatVitals : MonoBehaviour, ICombatTarget
  16 |     {
  17 |         [SerializeField, Min(1)] private int maximumHp=100;
  18 |         [SerializeField] private CombatFaction faction=CombatFaction.Allied;
  19 |         private Renderer[] renderers;
  20 |         private Collider[] colliders;
  21 |         private bool[] rendererEnabled, colliderEnabled;
  22 |         private MaterialPropertyBlock[] originalBlocks;
  23 |         private MaterialPropertyBlock flashBlock;
  24 |         private bool deathNotified, flashing;
  25 |         private float flashUntil;
  26 |         public Health Health { get; private set; }
  27 |         public bool IsAlive => Health != null && Health.Current>0;
  28 |         public bool IsHitReacting => flashing && IsAlive;
  29 |         public bool IsAvailable => isActiveAndEnabled && IsAlive;
  30 |         public Vector3 WorldPosition => transform.position;
  31 |         public Transform TargetTransform => transform;
  32 |         public CombatFaction Faction => faction;
  33 |         public int DeathCount { get; private set; }
  34 |         public string LastResult { get; private set; }="尚未受击";
  35 |         public event Action<HitOutcome> Died;
  36 | 
  37 |         public bool Initialize(string actorId)
  38 |         {
  39 |             if (Health != null) return Health.Id==actorId;
  40 |             if (string.IsNullOrWhiteSpace(actorId) || maximumHp<=0) return false;
  41 |             Health=new Health(actorId,maximumHp);
  42 |             // Unity原生对象必须在显式主线程初始化中创建，不能依赖MonoBehaviour字段构造时机。
  43 |             flashBlock=new MaterialPropertyBlock();
  44 |             renderers=GetComponentsInChildren<Renderer>(true);
  45 |             colliders=GetComponentsInChildren<Collider>(true);
  46 |             rendererEnabled=new bool[renderers.Length]; colliderEnabled=new bool[colliders.Length];
  47 |             originalBlocks=new MaterialPropertyBlock[renderers.Length];
  48 |             for (var i=0;i<renderers.Length;i++)
  49 |             { rendererEnabled[i]=renderers[i].enabled; originalBlocks[i]=new MaterialPropertyBlock(); renderers[i].GetPropertyBlock(originalBlocks[i]); }
  50 |             for (var i=0;i<colliders.Length;i++) colliderEnabled[i]=colliders[i].enabled;
  51 |             return true;
  52 |         }
  53 | 
  54 |         public void OnHitResolved(HitOutcome outcome)
  55 |         {
  56 |             if (Health==null || outcome==null || outcome.TargetId!=Health.Id || outcome.Reason!=HitReason.Applied) return;
  57 |             LastResult=outcome.ActorId+" → "+outcome.TargetId+" "+outcome.HpBefore+"→"+outcome.HpAfter+" #"+outcome.ActionId.ToString("N");
  58 |             if (IsAlive) { ShowHit(); return; }
  59 |             if (deathNotified) return;
  60 |             deathNotified=true; DeathCount++; flashing=false;
  61 |             // 只退本体表现/碰撞，保留Root/身份/Health供会话与调试读取；死亡不靠禁用全组Root。
  62 |             foreach (var r in renderers) if (r!=null) r.enabled=false;
  63 |             foreach (var c in colliders) if (c!=null) c.enabled=false;
  64 |             Died?.Invoke(outcome);
  65 |         }
  66 | 
  67 |         public void ShowHit()
  68 |         {
  69 |             if (!IsAlive || renderers==null) return;
  70 |             flashUntil=Time.time+.15f; flashing=true;
  71 |             flashBlock.SetColor("_Color",Color.white); flashBlock.SetColor("_BaseColor",Color.white);
  72 |             foreach (var r in renderers) if (r!=null) r.SetPropertyBlock(flashBlock);
  73 |         }
  74 | 
  75 |         private void Update()
  76 |         {
  77 |             if (!flashing || Time.time<flashUntil) return;
  78 |             flashing=false; RestoreBlocks();
  79 |         }
  80 |         private void RestoreBlocks()
  81 |         { for (var i=0;i<renderers.Length;i++) if (renderers[i]!=null) renderers[i].SetPropertyBlock(originalBlocks[i]); }
  82 | 
  83 |         /// <summary>开发回放恢复同一身份/Health，仅还原本组件关掉的Renderer/Collider；无生产计时返场。</summary>
  84 |         public void RestoreForDevelopment()
  85 |         {
  86 |             if (Health==null) return;
  87 |             Health.RestoreForDevelopment(); RestoreBody("Development重置");
  88 |         }
  89 |         internal bool RestoreAfterDeath()
  90 |         {
  91 |             if(Health==null || !isActiveAndEnabled || !Health.RestoreAfterDeath()) return false;
  92 |             RestoreBody("场景内返场"); return true;
  93 |         }
  94 |         private void RestoreBody(string reason)
  95 |         {
  96 |             deathNotified=false; flashing=false; LastResult=reason;
  97 |             for (var i=0;i<renderers.Length;i++) if (renderers[i]!=null) renderers[i].enabled=rendererEnabled[i];
  98 |             for (var i=0;i<colliders.Length;i++) if (colliders[i]!=null) colliders[i].enabled=colliderEnabled[i];
  99 |             RestoreBlocks();
 100 |         }
 101 |     }
 102 | }
```

## Assets/OSSASTRA/Scripts/Development/Combat/CombatDeathDebugView.cs

SHA256: 193D4A3A7C4A5CA80946D40563363AB66E684D550DB109AF7DEC1446ACA844BA；审查行1–86

```csharp
   1 | /*
   2 |  * 脚本：CombatDeathDebugView
   3 |  * 功能：开发HP/可选架势读数、正式结算入口注入受击和同实体重置，便于复验生命周期。
   4 |  * 边界：Development Only，不代表自动返场；不直接写HP私有字段、不伪造UI成功。
   5 |  * 协作：注入独立开发来源的Released→Health→目标通知；恢复顺序为取消/恢复原对象/重选实控。
   6 |  */
   7 | using System.Linq;
   8 | using Ossastra.CharacterCore;
   9 | using Ossastra.Combat;
  10 | using Ossastra.Characters;
  11 | using Ossastra.Enemies;
  12 | using UnityEngine;
  13 | namespace Ossastra.Development
  14 | {
  15 |     public sealed class CombatDeathDebugView : MonoBehaviour
  16 |     {
  17 |         [SerializeField] private CharacterControlCoordinator coordinator;
  18 |         [SerializeField] private CompanionFollowDirector director;
  19 |         [SerializeField] private Ossastra.Enemies.EnemyRoot enemy;
  20 |         private Vector3[] spawns;
  21 |         private Vector2 postureScroll;
  22 |         private static readonly Rect panel=new Rect(490,12,650,370);
  23 |         private void Start() { spawns=coordinator.Characters.Select(c=>c.transform.position).ToArray(); Register(true); }
  24 |         private void Register(bool add)
  25 |         {
  26 |             if (coordinator==null) return;
  27 |             foreach (var c in coordinator.Characters)
  28 |             { var input=c==null?null:c.GetComponent<CharacterInputHandler>(); if (input==null) continue; if (add) input.RegisterPointerRegion(this,panel); else input.RemovePointerRegion(this); }
  29 |         }
  30 |         private void OnDisable() { Register(false); }
  31 |         private void OnEnable() { if (spawns!=null) Register(true); }
  32 | 
  33 |         public static void ApplyDevelopmentHit(CombatVitals target, int damage)
  34 |         {
  35 |             if (target==null || !target.IsAlive || damage<=0) return;
  36 |             var runtime=new BasicActionRuntime("development-hit-probe");
  37 |             runtime.Request(new BasicAttackDefinition(0,0,0,.01,damage),"development-only",new ControlAuthority());
  38 |             var release=runtime.Advance(.001,false).Single(f=>f.Release!=null).Release;
  39 |             foreach (var outcome in release.Resolve(new[]{target.Health})) target.OnHitResolved(outcome);
  40 |         }
  41 |         public void RestartForDevelopment()
  42 |         {
  43 |             if (spawns==null) return;
  44 |             director.enabled=false;
  45 |             for (var i=0;i<coordinator.Characters.Count;i++)
  46 |             {
  47 |                 var c=coordinator.Characters[i]; c.CancelAction("development-reset"); c.SetPaused(false);
  48 |                 c.Vitals.RestoreForDevelopment();
  49 |                 var posture=c.GetComponent<CombatPosture>();
  50 |                 if(posture!=null) posture.ResetState();
  51 |                 c.GetComponent<CharacterMovement>().Teleport(spawns[i]);
  52 |             }
  53 |             enemy.RestoreForDevelopment(); coordinator.RestoreControlForDevelopment(); director.enabled=true;
  54 |         }
  55 |         private void OnGUI()
  56 |         {
  57 |             if (coordinator==null || enemy==null) return;
  58 |             GUILayout.BeginArea(panel,GUI.skin.box);
  59 |             var showPosture=enemy.GetComponent<CombatPosture>()!=null;
  60 |             if(showPosture) postureScroll=GUILayout.BeginScrollView(postureScroll);
  61 |             GUILayout.Label("Development Only · 双向攻防 / 真死亡 · 返场由场景服务决定");
  62 |             GUILayout.Label("Current: "+(coordinator.Current==null?"无可控制角色":coordinator.Current.ActorId));
  63 |             foreach (var c in coordinator.Characters)
  64 |             {
  65 |                 var v=c.Vitals; GUILayout.Label(c.ActorId+" HP "+v.Health.Current+"/"+v.Health.Maximum+" "+(v.IsAlive?"Alive":"Dead")+" "+c.EffectiveIntentKind+" | "+c.LastFact);
  66 |                 DrawPosture(c.GetComponent<CombatPosture>(),v.LastResult);
  67 |                 if (GUILayout.Button("合法受击检查："+c.ActorId+" -20HP")) ApplyDevelopmentHit(v,20);
  68 |                 if (GUILayout.Button("死亡边界："+c.ActorId+" 致死一击")) ApplyDevelopmentHit(v,v.Health.Maximum);
  69 |             }
  70 |             GUILayout.Label("Enemy HP "+enemy.Vitals.Health.Current+"/"+enemy.Vitals.Health.Maximum+" | "+enemy.LastFact);
  71 |             DrawPosture(enemy.GetComponent<CombatPosture>(),enemy.LastHit);
  72 |             if (GUILayout.Button("敌人攻击："+(enemy.AttacksEnabled?"开启（点击关闭）":"关闭（点击开启）"))) enemy.AttacksEnabled=!enemy.AttacksEnabled;
  73 |             if (GUILayout.Button("Development Only：同实体重新开始测试")) RestartForDevelopment();
  74 |             if(showPosture) GUILayout.EndScrollView();
  75 |             GUILayout.EndArea();
  76 |         }
  77 |         // 只读真实能力；旧场没挂架势时仍显示原命中信息，不伪造一份UI状态。
  78 |         private static void DrawPosture(CombatPosture posture,string fallback)
  79 |         {
  80 |             if(posture==null) { GUILayout.Label(fallback); return; }
  81 |             GUILayout.Label("架势 "+posture.Current.ToString("F1")+"/"+posture.Maximum+" · "+posture.Phase+
  82 |                 " · 硬直 "+posture.StaggerRemaining.ToString("F2")+"s · 等待 "+posture.RegenDelayRemaining.ToString("F2")+"s");
  83 |             GUILayout.Label(new GUIContent("来源："+posture.LastImpact,posture.LastImpact));
  84 |         }
  85 |     }
  86 | }
```

## Assets/OSSASTRA/Scripts/Development/Combat/EnemyChaseDebugView.cs

SHA256: 2CF67ED2656567B83DB7E3CB0F95F54DEA52A17ADE540D36B41C599CEC2FFFCB；审查行1–25

```csharp
   1 | /*
   2 |  * 脚本：EnemyChaseDebugView
   3 |  * 功能：只读有限敌动状态、真实位移和战斗事实；无输入/动作权威。
   4 |  */
   5 | using Ossastra.Enemies;
   6 | using UnityEngine;
   7 | namespace Ossastra.Development
   8 | {
   9 |     public sealed class EnemyChaseDebugView : MonoBehaviour
  10 |     {
  11 |         [SerializeField] private Ossastra.Enemies.EnemyRoot enemy;
  12 |         private void OnGUI()
  13 |         {
  14 |             if(enemy==null)return;
  15 |             var brain=enemy.GetComponent<ChaseEnemyBrain>();var movement=enemy.GetComponent<EnemyMovement>();
  16 |             if(brain==null||movement==null)return;
  17 |             GUILayout.BeginArea(new Rect(1150,390,410,180),GUI.skin.box);
  18 |             GUILayout.Label("EN-AI-01 / 胶囊仅技术占位 / SAMPLE");
  19 |             GUILayout.Label(brain.Phase+" target="+brain.TargetId);
  20 |             GUILayout.Label("实际速度="+movement.Velocity.magnitude.ToString("F2")+" HP="+(enemy.Vitals==null?0:enemy.Vitals.Health.Current));
  21 |             GUILayout.Label(enemy.LastFact);GUILayout.Label(enemy.LastHit);
  22 |             GUILayout.EndArea();
  23 |         }
  24 |     }
  25 | }
```

## Assets/OSSASTRA/Scripts/Development/Combat/PartyLifeDebugView.cs

SHA256: EE7D8BDDF346AA7640AA11B06EE245FE40F468B3AE694C8CEC71B03DD575E4E1；审查行1–51

```csharp
   1 | /*
   2 |  * 脚本：PartyLifeDebugView
   3 |  * 功能：只读呈现真实场景返场时钟、成员生命/代际；提供明确开发致死入口。
   4 |  * 边界：不保存生命真值、不执行生产恢复，生产返场由PartyRespawnCoordinator负责。
   5 |  */
   6 | using Ossastra.Characters;
   7 | using UnityEngine;
   8 | namespace Ossastra.Development
   9 | {
  10 |     public sealed class PartyLifeDebugView : MonoBehaviour
  11 |     {
  12 |         [SerializeField] private PartyRespawnCoordinator respawn;
  13 |         [SerializeField] private CharacterControlCoordinator session;
  14 |         private static readonly Rect panel=new Rect(1150,12,410,360);
  15 |         private void OnEnable(){Register(true);}
  16 |         private void OnDisable(){Register(false);}
  17 |         private void Register(bool add)
  18 |         {
  19 |             if(session==null)return;
  20 |             foreach(var root in session.Characters)
  21 |             {
  22 |                 if(root==null)continue;
  23 |                 var input=root.GetComponent<CharacterInputHandler>();if(input==null)continue;
  24 |                 if(add)input.RegisterPointerRegion(this,panel);else input.RemovePointerRegion(this);
  25 |             }
  26 |         }
  27 |         private void OnGUI()
  28 |         {
  29 |             if(respawn==null || session==null)return;
  30 |             GUILayout.BeginArea(panel,GUI.skin.box);
  31 |             GUILayout.Label("场景内返场 SAMPLE / Development读数");
  32 |             GUILayout.Label(respawn.Status+" / 模拟秒 "+respawn.SimulationTime.ToString("F2"));
  33 |             for(var i=0;i<session.Characters.Count;i++)
  34 |             {
  35 |                 var root=session.Characters[i];if(root==null)continue;
  36 |                 GUILayout.Label(root.ActorId+" Alive="+root.IsAlive+" Gen="+root.LifeGeneration+" Current="+(session.Current==root));
  37 |                 GUILayout.Label("HP="+root.Vitals.Health.Current+" Posture="+root.GetComponent<Ossastra.Combat.CombatPosture>().Current+" Died="+respawn.DiedAt(i).ToString("F2")+" Due="+respawn.DueAt(i).ToString("F2"));
  38 |                 GUILayout.Label("Stage="+root.AttackStage+" Buffer="+(root.HasBufferedAttack?1:0)+" Window="+root.ComboWindowUntil.ToString("F2")+" Releases="+root.ReleaseCount+" Source="+root.EffectiveIntentKind);
  39 |                 var animation=root.GetComponentInChildren<CharacterSpineView>();
  40 |                 if(animation!=null)
  41 |                 {
  42 |                     GUILayout.Label(animation.CurrentSemantic+" → "+animation.CurrentClip+" / "+animation.CurrentSource+" #"+animation.CurrentReplayId.ToString("N").Substring(0,8));
  43 |                     if(!string.IsNullOrEmpty(animation.FallbackReason)) GUILayout.Label(animation.FallbackReason);
  44 |                 }
  45 |                 if(GUILayout.Button(new GUIContent("开发致死："+root.ActorId,"通过真实伤害结算触发死亡；由生产场内返场服务等待恢复。")))
  46 |                     CombatDeathDebugView.ApplyDevelopmentHit(root.Vitals,10000);
  47 |             }
  48 |             GUILayout.EndArea();
  49 |         }
  50 |     }
  51 | }
```

## Assets/OSSASTRA/Scripts/Enemies/Core/ChasePolicy.cs

SHA256: B23FDA6D90B51F10FF7482697F7E48BD014EC901342DFC6D01D3351C0EF9E39A；审查行1–21

```csharp
   1 | /*
   2 |  * 脚本：ChasePolicy
   3 |  * 功能：有限追击状态选择，输入为世界适配器已验证的距离/视线/阻挡快照。
   4 |  * 边界：不移动、不查玩家Current、不写生命，不实现寻路或技能时钟。
   5 |  */
   6 | namespace Ossastra.EnemyCore
   7 | {
   8 |     public enum ChasePhase { Idle, Approach, InRangeAttack, ReturnHome, Blocked }
   9 |     public static class ChasePolicy
  10 |     {
  11 |         public static ChasePhase Decide(bool available,bool hasTarget,bool sight,float distance,float homeDistance,
  12 |             float stopDistance,float leash,float homeTolerance,bool blocked)
  13 |         {
  14 |             if(!available)return ChasePhase.Idle;
  15 |             if(blocked)return ChasePhase.Blocked;
  16 |             if(homeDistance>leash || !hasTarget || !sight)
  17 |                 return homeDistance>homeTolerance?ChasePhase.ReturnHome:ChasePhase.Idle;
  18 |             return distance<=stopDistance?ChasePhase.InRangeAttack:ChasePhase.Approach;
  19 |         }
  20 |     }
  21 | }
```

## Assets/OSSASTRA/Scripts/Enemies/Runtime/ChaseEnemyBrain.cs

SHA256: B20423C533C63C60F035F3F457901225D51D8647DFAAF13B8982929D529EB058；审查行1–75

```csharp
   1 | /*
   2 |  * 脚本：ChaseEnemyBrain
   3 |  * 功能：显式中立候选的有限感知、追击、停距、返回和受阻重评。
   4 |  * 边界：无玩家Root/Input/Current；不直接移动，不推进攻击，不回血/传送。
   5 |  * 协作：EnemyRoot唯一调用Tick，EnemyMovement提供上帧真实位移反馈。
   6 |  */
   7 | using System;
   8 | using Ossastra.CharacterCore;
   9 | using Ossastra.Combat;
  10 | using Ossastra.EnemyCore;
  11 | using UnityEngine;
  12 | namespace Ossastra.Enemies
  13 | {
  14 |     public sealed class ChaseEnemyBrain : MonoBehaviour,IEnemyBrain
  15 |     {
  16 |         [SerializeField] private CombatVitals[] targets=Array.Empty<CombatVitals>();
  17 |         [Header("有限直线追击 SAMPLE；无NavMesh")]
  18 |         [SerializeField,Min(.1f)] private float detectRadius=4,leashRadius=6,stopDistance=1.2f,attackRange=1.7f,attackInterval=1.8f;
  19 |         private CombatVitals owner;
  20 |         private IEnemyMovement movement;
  21 |         private EnemyRoot root;
  22 |         private ICombatTarget selected;
  23 |         private Vector3 home;
  24 |         private float nextAttack,blockedFor,holdUntil;
  25 |         private bool returning;
  26 |         public bool IsAvailable=>isActiveAndEnabled && owner!=null && owner.IsAvailable;
  27 |         public Vector3 MoveDirection { get; private set; }
  28 |         public ChasePhase Phase { get; private set; }
  29 |         public Vector3 Home=>home;
  30 |         public string TargetId=>CombatTargetValidity.IsAvailable(selected)?selected.Health.Id:"none";
  31 |         public bool Initialize(CombatVitals value)
  32 |         {
  33 |             if(value==null||value.gameObject!=gameObject||value.Health==null)return false;
  34 |             owner=value;root=GetComponent<EnemyRoot>();movement=GetComponent<EnemyMovement>();home=transform.position;
  35 |             ResetDecision(Time.time);return root!=null&&movement!=null;
  36 |         }
  37 |         private bool Valid(ICombatTarget candidate)
  38 |         {
  39 |             if(!IsAvailable||!CombatTargetValidity.IsAvailable(candidate)||candidate.TargetTransform==null)return false;
  40 |             var found=false;
  41 |             foreach(var t in targets)
  42 |             {if(t==null)continue;if(ReferenceEquals(t,candidate))found=true;else if(t.Health!=null&&t.Health.Id==candidate.Health.Id)return false;}
  43 |             return found && CombatTargetPolicy.CanHit(owner.Health.Id,owner.Faction,true,candidate.Health.Id,candidate.Faction,true)
  44 |                 && FlatDistance(home,candidate.WorldPosition)<=leashRadius && CombatSight.IsClear(transform,candidate.TargetTransform);
  45 |         }
  46 |         public void Tick(float now,float delta)
  47 |         {
  48 |             if(!IsAvailable){ResetDecision(now);return;}
  49 |             // 有移动意图却没有实际进展才累计受阻；攻击停手不被误判卡墙。
  50 |             if(movement.HasMovementRequest && movement.Velocity.sqrMagnitude<.01f)blockedFor+=delta;else blockedFor=0;
  51 |             if(blockedFor>.6f){holdUntil=now+1;blockedFor=0;returning=true;selected=null;}
  52 |             if(returning && FlatDistance(home,transform.position)<.15f)returning=false;
  53 |             if(selected!=null&&!Valid(selected)){selected=null;returning=true;}
  54 |             if(!returning && selected==null)
  55 |             {
  56 |                 var nearest=detectRadius;
  57 |                 foreach(var t in targets)
  58 |                 {if(!Valid(t))continue;var distance=FlatDistance(transform.position,t.WorldPosition);if(distance<nearest){nearest=distance;selected=t;}}
  59 |             }
  60 |             Phase=ChasePolicy.Decide(true,!returning&&selected!=null,selected!=null,
  61 |                 selected==null?0:FlatDistance(transform.position,selected.WorldPosition),FlatDistance(home,transform.position),stopDistance,leashRadius,.15f,now<holdUntil);
  62 |             if(Phase==ChasePhase.ReturnHome){returning=true;selected=null;MoveDirection=(home-transform.position).normalized;}
  63 |             else if(Phase==ChasePhase.Approach)MoveDirection=(selected.WorldPosition-transform.position).normalized;
  64 |             else MoveDirection=Vector3.zero;
  65 |         }
  66 |         public bool IsEligible(ICombatTarget candidate)=>ReferenceEquals(candidate,selected)&&Valid(candidate)&&FlatDistance(transform.position,candidate.WorldPosition)<=attackRange;
  67 |         public bool TrySelect(float now,out ICombatTarget target)
  68 |         {target=null;if(now<nextAttack||Phase!=ChasePhase.InRangeAttack||!IsEligible(selected))return false;target=selected;return true;}
  69 |         public void OnAccepted(float now){nextAttack=now+attackInterval;}
  70 |         public void ResetDecision(float now)
  71 |         {selected=null;MoveDirection=Vector3.zero;Phase=ChasePhase.Idle;blockedFor=0;holdUntil=0;nextAttack=now+attackInterval;returning=FlatDistance(home,transform.position)>.15f;}
  72 |         private void OnDisable(){if(root!=null)root.Cancel("chase-brain-disabled");MoveDirection=Vector3.zero;selected=null;movement?.Stop();}
  73 |         private static float FlatDistance(Vector3 a,Vector3 b){var d=a-b;d.y=0;return d.magnitude;}
  74 |     }
  75 | }
```

## Assets/OSSASTRA/Scripts/Enemies/Runtime/EnemyContracts.cs

SHA256: 282C22EC3ED4B0D27ED82E0F4C40D1B56F9D196C9C9615D8B2B71DF2431967B1；审查行1–28

```csharp
   1 | /*
   2 |  * 脚本：EnemyContracts
   3 |  * 功能：敌根消费的决策与实际运动窄接口；固定/追击策略可替换。
   4 |  * 边界：接口只交意图，动作事实与伤害仍由EnemyRoot原链条负责。
   5 |  */
   6 | using Ossastra.Combat;
   7 | using UnityEngine;
   8 | namespace Ossastra.Enemies
   9 | {
  10 |     public interface IEnemyBrain
  11 |     {
  12 |         bool IsAvailable { get; }
  13 |         Vector3 MoveDirection { get; }
  14 |         bool Initialize(CombatVitals owner);
  15 |         void Tick(float now,float delta);
  16 |         bool IsEligible(ICombatTarget target);
  17 |         bool TrySelect(float now,out ICombatTarget target);
  18 |         void OnAccepted(float now);
  19 |         void ResetDecision(float now);
  20 |     }
  21 |     public interface IEnemyMovement
  22 |     {
  23 |         Vector3 Velocity { get; }
  24 |         bool HasMovementRequest { get; }
  25 |         void Tick(Vector3 direction,float delta);
  26 |         void Stop();
  27 |     }
  28 | }
```

## Assets/OSSASTRA/Scripts/Enemies/Runtime/EnemyMovement.cs

SHA256: 0EA9EE408D7E45D047B0DC063BC01E54884DE8712E569210B8F8604A6B66597D；审查行1–27

```csharp
   1 | /*
   2 |  * 脚本：EnemyMovement
   3 |  * 功能：敌方物理位移适配；由根显式Tick，回报碰撞后的实际速度。
   4 |  * 边界：不选择目标/Current，不直接改Transform，不自动寻路。
   5 |  */
   6 | using UnityEngine;
   7 | namespace Ossastra.Enemies
   8 | {
   9 |     [RequireComponent(typeof(CharacterController))]
  10 |     public sealed class EnemyMovement : MonoBehaviour,IEnemyMovement
  11 |     {
  12 |         [SerializeField,Min(0)] private float speed=1.8f,groundStick=2;
  13 |         private CharacterController body;
  14 |         public Vector3 Velocity { get; private set; }
  15 |         public bool HasMovementRequest { get; private set; }
  16 |         private void Awake(){body=GetComponent<CharacterController>();}
  17 |         public void Stop(){Velocity=Vector3.zero;HasMovementRequest=false;}
  18 |         public void Tick(Vector3 direction,float delta)
  19 |         {
  20 |             Stop();if(!isActiveAndEnabled||body==null||!body.enabled||delta<=0)return;
  21 |             direction.y=0;var before=transform.position;
  22 |             HasMovementRequest=direction.sqrMagnitude>.01f;
  23 |             body.Move((Vector3.ClampMagnitude(direction,1)*speed+Vector3.down*groundStick)*delta);
  24 |             var displacement=transform.position-before;displacement.y=0;Velocity=displacement/delta;
  25 |         }
  26 |     }
  27 | }
```

## Assets/OSSASTRA/Scripts/Enemies/Runtime/EnemyRoot.cs

SHA256: 757B50764F9E5CD8FB91DF8AF8AC3FBC105E6EFDD5CBA1C1684D51F14777E378；审查行1–119

```csharp
   1 | /*
   2 |  * 脚本：EnemyRoot
   3 |  * 功能：敌方唯一组合根，组织身份、生命、决策与单一动作推进/释放。
   4 |  * 边界：无玩家Root/Input/Bus/Current，不选最近目标、不直接扣HP。
   5 |  * 协作：Brain提交并验证目标；根独占动作和可选架势推进，破势/生命周期撤销未释放动作。
   6 |  */
   7 | using System;
   8 | using Ossastra.CharacterCore;
   9 | using Ossastra.Combat;
  10 | using UnityEngine;
  11 | namespace Ossastra.Enemies
  12 | {
  13 |     [RequireComponent(typeof(CombatVitals),typeof(CombatMeleeHit),typeof(StationaryEnemyBrain))]
  14 |     public sealed class EnemyRoot : MonoBehaviour
  15 |     {
  16 |         [SerializeField] private string actorId="enemy-stationary-01";
  17 |         [SerializeField] private float releaseAt=.5f, moveReadyAt=.6f, attackReadyAt=.9f, finishAt=1.1f;
  18 |         [SerializeField, Min(1)] private int damage=20;
  19 |         [SerializeField, Min(0)] private float postureImpact;
  20 |         [SerializeField] private CombatPosture posture;
  21 |         [SerializeField] private bool attacksEnabled=true;
  22 |         private CombatVitals vitals;
  23 |         private ICombatHitResolver melee;
  24 |         private IEnemyBrain brain;
  25 |         private IEnemyMovement movement;
  26 |         private BasicActionRuntime runtime;
  27 |         private BasicAttackDefinition attack;
  28 |         private readonly ControlAuthority authority=new ControlAuthority();
  29 |         private ICombatTarget target;
  30 |         private Health targetHealth;
  31 |         private bool focused=true, applicationPaused, paused;
  32 |         public string ActorId => actorId;
  33 |         public CombatVitals Vitals => vitals;
  34 |         public string LastFact { get; private set; }="Idle";
  35 |         public string LastHit => melee==null?"未初始化":melee.LastResult;
  36 |         public bool ActionActive => runtime!=null && runtime.IsActive;
  37 |         public bool AttacksEnabled { get => attacksEnabled; set { attacksEnabled=value; if(!value) Cancel("development-attacks-disabled"); } }
  38 |         private void Awake()
  39 |         {
  40 |             vitals=GetComponent<CombatVitals>(); melee=GetComponent<CombatMeleeHit>();
  41 |             var chase=GetComponent<ChaseEnemyBrain>();
  42 |             brain=chase!=null&&chase.enabled?(IEnemyBrain)chase:GetComponent<StationaryEnemyBrain>();
  43 |             movement=GetComponent<EnemyMovement>();
  44 |             try { attack=new BasicAttackDefinition(releaseAt,moveReadyAt,attackReadyAt,finishAt,damage,postureImpact); runtime=new BasicActionRuntime(actorId); }
  45 |             catch(ArgumentException e) { Debug.LogError("Enemy configuration: "+e.Message,this); enabled=false; return; }
  46 |             if(!vitals.Initialize(actorId) || brain==null || !brain.Initialize(vitals))
  47 |             { Debug.LogError("Enemy identity/brain invalid",this); enabled=false; return; }
  48 |             vitals.Died+=OnDied;
  49 |             if (posture != null)
  50 |             {
  51 |                 if (!posture.Initialize(vitals, this)) { Debug.LogError("Enemy posture configuration invalid", this); enabled=false; return; }
  52 |                 posture.Broken += OnPostureBroken;
  53 |                 posture.Recovered += OnPostureRecovered;
  54 |             }
  55 |         }
  56 |         private void Update()
  57 |         {
  58 |             if(runtime==null) return;
  59 |             if(vitals==null || !vitals.IsAvailable) { Cancel("source-unavailable"); return; }
  60 |             // 1. 根独占架势时钟，禁用开发攻击不冻结恢复；失焦/暂停停钟。
  61 |             if (posture != null) posture.Tick(!focused || applicationPaused || paused ? 0 : Time.deltaTime, runtime.IsActive);
  62 |             if(!attacksEnabled || !focused || applicationPaused || paused || !authority.Allows(ControlChannel.Attack)) {movement?.Stop();return;}
  63 |             // 2. 决策资格先于动作请求；Brain不掌握架势数值或第二时钟。
  64 |             if(brain==null || !brain.IsAvailable) { Cancel("brain-disabled"); return; }
  65 |             brain.Tick(Time.time,Time.deltaTime);
  66 |             if(runtime.IsActive && (!brain.IsEligible(target) || !ReferenceEquals(target.Health,targetHealth)))
  67 |                 Cancel("target-dead-out-of-range-or-occluded-or-stale");
  68 |             if(!runtime.IsActive && brain.TrySelect(Time.time,out var selected))
  69 |             {
  70 |                 var request=runtime.Request(attack,"enemy-brain",authority);
  71 |                 if(request.Accepted)
  72 |                 {
  73 |                     target=selected; targetHealth=selected.Health; brain.OnAccepted(Time.time);
  74 |                     LastFact="Accepted #"+request.Fact.ActionId.ToString("N");
  75 |                 }
  76 |             }
  77 |             // 3. 唯一动作推进/命中阶段；破势回调立即Cancel，不保留冻结的待释放动作。
  78 |             foreach(var fact in runtime.Advance(Time.deltaTime,false))
  79 |             {
  80 |                 LastFact=fact.Kind+" #"+fact.ActionId.ToString("N");
  81 |                 if(fact.Release!=null)
  82 |                 {
  83 |                     var direction=target.WorldPosition-transform.position; direction.y=0;
  84 |                     melee.Resolve(fact.Release,direction.normalized);
  85 |                 }
  86 |             }
  87 |             // 根的最后物理阶段：只消费Brain方向；已接受攻击全段停步，避免贴人重叠攻击。
  88 |             movement?.Tick(runtime.IsActive?Vector3.zero:brain.MoveDirection,Time.deltaTime);
  89 |         }
  90 |         public void Cancel(string reason)
  91 |         {
  92 |             var fact=runtime==null?null:runtime.Cancel(reason);
  93 |             if(fact!=null) LastFact="Cancelled "+reason+" #"+fact.ActionId.ToString("N");
  94 |             target=null; targetHealth=null;
  95 |             movement?.Stop();
  96 |             brain?.ResetDecision(Time.time);
  97 |         }
  98 |         public void SetPaused(bool value) { paused=value; if(value) Cancel("enemy-paused"); }
  99 |         private void OnPostureBroken(string cause)
 100 |         {
 101 |             authority.Block(posture, ControlChannel.Move | ControlChannel.Attack);
 102 |             Cancel("posture-broken:" + cause);
 103 |         }
 104 |         private void OnPostureRecovered() { authority.Release(posture); }
 105 |         private void OnDied(HitOutcome outcome) { Cancel("death:"+outcome.ActorId); }
 106 |         private void OnDisable() { Cancel("enemy-disabled"); if (posture != null) posture.ResetState(); authority.Release(posture); }
 107 |         private void OnApplicationFocus(bool value) { focused=value; if(!value) Cancel("focus-lost"); }
 108 |         private void OnApplicationPause(bool value) { applicationPaused=value; if(value) Cancel("application-paused"); }
 109 |         private void OnDestroy()
 110 |         {
 111 |             if(vitals!=null) vitals.Died-=OnDied;
 112 |             if(posture!=null) { posture.Broken-=OnPostureBroken; posture.Recovered-=OnPostureRecovered; }
 113 |             authority.Release(posture);
 114 |             Cancel("destroyed");
 115 |         }
 116 |         public void RestoreForDevelopment()
 117 |         { Cancel("development-reset"); vitals.RestoreForDevelopment(); if(posture!=null) posture.ResetState(); brain.ResetDecision(Time.time); }
 118 |     }
 119 | }
```

## Assets/OSSASTRA/Scripts/Enemies/Runtime/StationaryEnemyBrain.cs

SHA256: E3048058DC828A6F86D61F5B6C94DA8953BFA9D13441544FC1A7DFBF982A8B52；审查行1–70

```csharp
   1 | /*
   2 |  * 脚本：StationaryEnemyBrain
   3 |  * 功能：固定站位有限感知/决策：显式候选、距离、LOS、最近选择及请求间隔。
   4 |  * 边界：无Update/动作时钟/HP写入，无玩家类型、寻路或追击；不执行攻击。
   5 |  * 协作：Inspector保存中立Vitals，逻辑消费ICombatTarget；EnemyRoot单调度。
   6 |  */
   7 | using System;
   8 | using Ossastra.CharacterCore;
   9 | using Ossastra.Combat;
  10 | using UnityEngine;
  11 | namespace Ossastra.Enemies
  12 | {
  13 |     public sealed class StationaryEnemyBrain : MonoBehaviour,IEnemyBrain
  14 |     {
  15 |         [SerializeField] private CombatVitals[] targets=Array.Empty<CombatVitals>();
  16 |         [SerializeField, Min(.1f)] private float triggerDistance=1.7f, attackInterval=1.8f;
  17 |         private CombatVitals owner;
  18 |         private EnemyRoot root;
  19 |         private float nextAttack;
  20 |         public bool IsAvailable=>isActiveAndEnabled;
  21 |         public Vector3 MoveDirection=>Vector3.zero;
  22 |         public void Tick(float now,float delta) { }
  23 |         public bool Initialize(CombatVitals value)
  24 |         {
  25 |             if(value==null || value.gameObject!=gameObject || value.Health==null) return false;
  26 |             if(owner!=null && owner!=value) return false;
  27 |             owner=value; root=GetComponent<EnemyRoot>(); ResetDecision(Time.time); return root!=null;
  28 |         }
  29 |         public void ResetDecision(float now) { nextAttack=now+attackInterval; }
  30 |         public void OnAccepted(float now) { nextAttack=now+attackInterval; }
  31 |         public bool IsEligible(ICombatTarget candidate)
  32 |         {
  33 |             if(!isActiveAndEnabled || owner==null || !owner.IsAvailable ||
  34 |                 !CombatTargetValidity.IsAvailable(candidate) || candidate.TargetTransform==null) return false;
  35 |             // 必须仍在显式候选表；同ID不同实体拒绝，防止过期身份指向新对象。
  36 |             var found=false;
  37 |             foreach(var configured in targets)
  38 |             {
  39 |                 if(configured==null) continue;
  40 |                 if(ReferenceEquals(configured,candidate)) found=true;
  41 |                 else if(configured.Health!=null && configured.Health.Id==candidate.Health.Id) return false;
  42 |             }
  43 |             if(!found || !CombatTargetPolicy.CanHit(owner.Health.Id,owner.Faction,owner.IsAlive,
  44 |                 candidate.Health.Id,candidate.Faction,candidate.IsAlive)) return false;
  45 |             var offset=candidate.WorldPosition-transform.position; offset.y=0;
  46 |             if(offset.magnitude>triggerDistance) return false;
  47 |             var start=transform.position+Vector3.up*.8f;
  48 |             var end=candidate.WorldPosition+Vector3.up*.8f;
  49 |             foreach(var hit in Physics.RaycastAll(start,end-start,Vector3.Distance(start,end),~0,QueryTriggerInteraction.Ignore))
  50 |                 if(!hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(candidate.TargetTransform)) return false;
  51 |             return true;
  52 |         }
  53 |         public bool TrySelect(float now,out ICombatTarget selected)
  54 |         {
  55 |             selected=null;
  56 |             if(now<nextAttack || !isActiveAndEnabled) return false;
  57 |             var nearest=float.MaxValue;
  58 |             foreach(var candidate in targets)
  59 |             {
  60 |                 if(!IsEligible(candidate)) continue;
  61 |                 var distance=(candidate.WorldPosition-transform.position).sqrMagnitude;
  62 |                 if(distance<nearest) { nearest=distance; selected=candidate; }
  63 |             }
  64 |             return selected!=null;
  65 |         }
  66 |         private void OnDisable() { if(root!=null) root.Cancel("brain-disabled"); }
  67 |         private void OnDrawGizmosSelected()
  68 |         { Gizmos.color=Color.red; Gizmos.DrawWireSphere(transform.position+Vector3.up*.8f,triggerDistance); }
  69 |     }
  70 | }
```
