/*
 * 脚本：CombatSfxFactAdapter
 * 功能：只读消费角色、敌人及共享治疗的既成事实，把战斗短音提交给唯一音频根。
 * 边界：不采按键、不接受动作、不写HP/费用/动画，不创建或持有AudioSource；视觉仍归CombatFeedbackView。
 * 协作：Root释放、能力反馈、近战/动作组收据各取唯一来源；来源租约随禁用、生命代次和世界撤销。
 * 约束：旧回调未暴露子击身份时使用本适配器观察序号，不以整个ActionId或时间窗吞掉合法多击。
 */
using System;
using System.Collections.Generic;
using System.Linq;
using Ossastra.CharacterCore;
using Ossastra.Characters;
using Ossastra.Combat;
using UnityEngine;
using ActorRoot = Ossastra.Characters.CharacterRoot;
using EnemyEntityRoot = Ossastra.Enemies.EnemyRoot;

namespace Ossastra.Audio
{
    [DefaultExecutionOrder(260), DisallowMultipleComponent]
    public sealed class CombatSfxFactAdapter : MonoBehaviour
    {
        private sealed class ActorObservation
        {
            public ActorRoot root;
            public CombatVitals vitals;
            public CharacterAbilities abilities;
            public CharacterMainAction main;
            public CombatMeleeHit melee;
            public ShieldGuard guard;
            public CharacterDodge dodge;
            public CharacterOffhand offhand;
            public CharacterSkills skills;
            public SfxSourceHandle source;
            public bool connected;
            public int hp, lifeGeneration, bombs, landings, skillLandings;
            public Guid dodgeId;
            public long sequence;
            public readonly List<Action> remove = new List<Action>();
            public readonly HashSet<ActionFact> rootFacts = new HashSet<ActionFact>();
            public readonly Queue<ActionFact> rootFactOrder = new Queue<ActionFact>();
        }

        private sealed class EnemyObservation
        {
            public EnemyEntityRoot root;
            public CombatVitals vitals;
            public CombatMeleeHit melee;
            public SfxSourceHandle source;
            public int hp;
            public long sequence;
            public bool connected;
            public Action<ActionFact> action;
            public Action<Guid, bool> receipt;
            public readonly HashSet<ActionFact> rootFacts = new HashSet<ActionFact>();
            public readonly Queue<ActionFact> rootFactOrder = new Queue<ActionFact>();
        }

        private readonly List<ActorObservation> actors = new List<ActorObservation>();
        private readonly List<EnemyObservation> enemies = new List<EnemyObservation>();
        private GameAudioRoot audioRoot;
        private HealingBottleService bottle;
        private CharacterControlCoordinator coordinator;
        private ActorRoot previousCurrent;
        private SfxSourceHandle interfaceSource;
        private string observedWorld;
        private float nextDiscovery;
        private int bottleUses;
        private long interfaceSequence;

        public int FactSubmissions { get; private set; }
        public int Accepted { get; private set; }
        public int Rejected { get; private set; }
        public int Releases { get; private set; }
        public int Hits { get; private set; }
        public int Hurts { get; private set; }
        public string LastFact { get; private set; } = "尚未观察事实";
        public string Status { get; private set; } = "等待统一音频根";
        public int ActorSources => actors.Count(a => a.connected);
        public int EnemySources => enemies.Count(e => e.connected);

        private void OnEnable() { TryInitialize(); }
        private void Start() { TryInitialize(); }

        private bool TryInitialize()
        {
            if (!Application.isPlaying || !isActiveAndEnabled) return false;
            var current = GameAudioRoot.Instance;
            if (current == null || !current.IsOwner || !current.SfxAvailable)
            {
                Status = "没有已配置的统一音频根；不创建本地回退播放器";
                return false;
            }
            if (audioRoot == current && observedWorld == current.WorldScopeId && interfaceSource != null && !interfaceSource.IsDisposed)
                return current.TryClaimSfxFacts(this);

            DisconnectAll();
            if(!current.TryClaimSfxFacts(this)){Status="已有同场音效事实所有者；拒绝重复订阅";return false;}
            audioRoot = current;
            observedWorld = current.WorldScopeId;
            interfaceSource = audioRoot.RegisterSfxSource("ui-control@" + GetInstanceID());
            bottle = FindObjectsOfType<HealingBottleService>().FirstOrDefault(s => s.isActiveAndEnabled && InOwnScene(s));
            coordinator = FindObjectsOfType<CharacterControlCoordinator>().FirstOrDefault(s => s.isActiveAndEnabled && InOwnScene(s));
            previousCurrent = coordinator == null ? null : coordinator.Current;
            bottleUses = bottle == null ? 0 : bottle.UseCount;
            DiscoverSources();
            Status = "只读事实 → 单一音频根；音频源状态不代表实际可听";
            return true;
        }

        private bool InOwnScene(Component component) => component != null && component.gameObject.scene == gameObject.scene;

        private void DiscoverSources()
        {
            foreach (var root in FindObjectsOfType<ActorRoot>().Where(InOwnScene))
            {
                if (actors.Any(a => a.root == root)) continue;
                actors.Add(new ActorObservation
                {
                    root = root,
                    vitals = root.Vitals,
                    abilities = root.GetComponent<CharacterAbilities>(),
                    main = root.GetComponent<CharacterMainAction>(),
                    melee = root.GetComponent<CombatMeleeHit>(),
                    guard = root.GetComponent<ShieldGuard>(),
                    dodge = root.GetComponent<CharacterDodge>(),
                    offhand = root.GetComponent<CharacterOffhand>(),
                    skills = root.GetComponent<CharacterSkills>()
                });
            }
            foreach (var root in FindObjectsOfType<EnemyEntityRoot>().Where(InOwnScene))
            {
                if (enemies.Any(e => e.root == root)) continue;
                enemies.Add(new EnemyObservation { root = root, vitals = root.Vitals, melee = root.GetComponent<CombatMeleeHit>() });
            }
            nextDiscovery = Time.unscaledTime + .5f;
        }

        private void Connect(ActorObservation observed)
        {
            if (observed.connected || observed.root == null || !observed.root.isActiveAndEnabled || observed.root.Vitals?.Health == null) return;
            observed.vitals = observed.root.Vitals;
            observed.source = audioRoot.RegisterSfxSource(observed.root.ActorId + "@" + observed.root.GetInstanceID());
            if (observed.source == null) return;
            observed.connected = true;
            observed.lifeGeneration = observed.root.LifeGeneration;
            observed.hp = observed.vitals.Health.Current;
            observed.bombs = observed.offhand == null ? 0 : observed.offhand.ReleasedCount;
            observed.landings = observed.offhand == null ? 0 : observed.offhand.LandedCount;
            observed.skillLandings = observed.skills == null ? 0 : observed.skills.ResolvedCount;
            observed.dodgeId = observed.dodge == null ? Guid.Empty : observed.dodge.ActionId;

            Action<ActionFact> action = fact => OnActorFact(observed, fact);
            observed.root.ActionObserved += action;
            observed.remove.Add(() => { if (observed.root != null) observed.root.ActionObserved -= action; });
            if (observed.abilities != null)
            {
                Action<Guid, string, Vector3> feedback = (id, kind, position) => OnAbilityFeedback(observed, id, kind, position);
                observed.abilities.Feedback += feedback;
                observed.remove.Add(() => { if (observed.abilities != null) observed.abilities.Feedback -= feedback; });
            }
            if (observed.melee != null)
            {
                Action<Guid, bool> receipt = (id, applied) => OnMeleeReceipt(observed, id, applied, "melee");
                observed.melee.Resolved += receipt;
                observed.remove.Add(() => { if (observed.melee != null) observed.melee.Resolved -= receipt; });
            }
            if (observed.main != null)
            {
                // 配置动作组使用独立投递队列，不能假定它也会触发CombatMeleeHit。
                Action<Guid, bool> receipt = (id, applied) => OnMeleeReceipt(observed, id, applied, "main-profile");
                observed.main.Resolved += receipt;
                observed.remove.Add(() => { if (observed.main != null) observed.main.Resolved -= receipt; });
            }
            if (observed.guard != null)
            {
                Action<HitOutcome> blocked = outcome =>
                {
                    if (CanObserve(observed) && outcome != null && outcome.Reason == HitReason.Guarded)
                        Submit(observed, "combat.guard", "guard:" + outcome.ActionId.ToString("N") + ":" + outcome.TargetId, observed.root.transform.position);
                };
                observed.guard.Guarded += blocked;
                observed.remove.Add(() => { if (observed.guard != null) observed.guard.Guarded -= blocked; });
            }
        }

        private void Connect(EnemyObservation observed)
        {
            if (observed.connected || observed.root == null || !observed.root.isActiveAndEnabled || observed.root.Vitals?.Health == null) return;
            observed.vitals = observed.root.Vitals;
            observed.source = audioRoot.RegisterSfxSource(observed.root.ActorId + "@" + observed.root.GetInstanceID());
            if (observed.source == null) return;
            observed.hp = observed.vitals.Health.Current;
            observed.connected = true;
            observed.action = fact =>
            {
                if (CanObserve(observed) && fact != null && fact.Kind == ActionFactKind.Released && RememberRootFact(fact, observed.rootFacts, observed.rootFactOrder))
                    Submit(observed, "combat.release", "enemy-release:" + fact.ActionId.ToString("N"), observed.root.transform.position);
            };
            observed.root.ActionObserved += observed.action;
            if (observed.melee != null)
            {
                observed.receipt = (id, applied) =>
                {
                    if (applied && CanObserve(observed)) Submit(observed, "combat.hit", "enemy-receipt:" + id.ToString("N"), observed.root.transform.position);
                };
                observed.melee.Resolved += observed.receipt;
            }
        }

        private bool CanObserve(ActorObservation observed) => isActiveAndEnabled && observed.connected && observed.root != null &&
            observed.root.isActiveAndEnabled && !observed.root.IsSimulationSuspended && Time.timeScale > 0 &&
            observed.source != null && !observed.source.IsDisposed;
        private bool CanObserve(EnemyObservation observed) => isActiveAndEnabled && observed.connected && observed.root != null &&
            observed.root.isActiveAndEnabled && !observed.root.IsSimulationSuspended && Time.timeScale > 0 &&
            observed.source != null && !observed.source.IsDisposed;

        private void OnActorFact(ActorObservation observed, ActionFact fact)
        {
            if (!CanObserve(observed) || fact == null || fact.Kind != ActionFactKind.Released || observed.abilities != null && observed.abilities.OwnsRelease(fact.ActionId)) return;
            if (!RememberRootFact(fact, observed.rootFacts, observed.rootFactOrder)) return;
            // 能力会在根分派同一身体Released时先提交自己的释放反馈；只让其唯一主源发声。
            Submit(observed, "combat.release", "root-release:" + fact.ActionId.ToString("N"), observed.root.transform.position);
        }

        private void OnAbilityFeedback(ActorObservation observed, Guid executionId, string kind, Vector3 position)
        {
            string suffix;
            switch (kind)
            {
                case "release": case "launch": case "reload": suffix = "combat.release"; break;
                case "hit": suffix = "combat.hit"; break;
                case "guard": suffix = "combat.guard"; break;
                case "land": suffix = "delivery.land"; break;
                default: return; // accepted/cancel/end/miss不是释放或命中；没有默认成功音。
            }
            Submit(observed, suffix, "ability:" + executionId.ToString("N") + ":" + kind, position);
        }

        private void OnMeleeReceipt(ActorObservation observed, Guid actionId, bool applied, string authority)
        {
            if (!applied || !CanObserve(observed)) return;
            Submit(observed, "combat.hit", authority + ":" + actionId.ToString("N"), observed.root.transform.position);
        }

        private void PollHealth(ActorObservation observed)
        {
            if (observed.vitals == null || observed.vitals.Health == null) return;
            var hp = observed.vitals.Health.Current;
            if (hp < observed.hp) Submit(observed, "combat.hurt", "hp:" + observed.hp + ">" + hp, observed.vitals.WorldPosition);
            observed.hp = hp;
        }

        private void Submit(ActorObservation observed, string suffix, string fact, Vector3 position)
        {
            if (!CanObserve(observed)) return;
            // UI捕获会暂时关掉HasPlayerInput，但不会改变Current；来源总线按真正的控制归属分类。
            var isPlayer = coordinator != null && coordinator.Characters.Contains(observed.root)
                ? coordinator.Current == observed.root
                : observed.root.GetComponent<CharacterInputHandler>()?.HasPlayerControl == true;
            var prefix = isPlayer ? "player." : "companion.";
            // 回调只给父ActionId，没有节点/目标索引。每次真实回执保留音频局部序号；不吞同动作多段。
            var key = fact + ":observation:" + (++observed.sequence);
            CountSemantic(suffix);
            Record(prefix + suffix, key, observed.source.Submit(prefix + suffix, key, position));
        }

        private void Submit(EnemyObservation observed, string suffix, string fact, Vector3 position)
        {
            if (!CanObserve(observed)) return;
            var key = fact + ":observation:" + (++observed.sequence);
            CountSemantic(suffix);
            Record("enemy." + suffix, key, observed.source.Submit("enemy." + suffix, key, position));
        }

        private void CountSemantic(string suffix)
        {
            if (suffix == "combat.release") Releases++;
            else if (suffix == "combat.hit") Hits++;
            else if (suffix == "combat.hurt") Hurts++;
        }

        private static bool RememberRootFact(ActionFact fact, HashSet<ActionFact> seen, Queue<ActionFact> order)
        {
            // ActionFact不可变且未重写相等性；只屏蔽同一个事实对象的重复观察，不能屏蔽同ActionId的新阶段。
            if (!seen.Add(fact)) return false;
            order.Enqueue(fact);
            if (order.Count > 128) seen.Remove(order.Dequeue());
            return true;
        }

        private void Record(string eventId, string fact, SfxDecision decision)
        {
            FactSubmissions++;
            if (decision.Accepted) Accepted++; else Rejected++;
            LastFact = eventId + " / " + fact + " / " + decision.Reason;
        }

        private void LateUpdate()
        {
            if (!TryInitialize()) { DisconnectAll(); return; }
            if (Time.unscaledTime >= nextDiscovery) DiscoverSources();
            foreach (var observed in actors)
            {
                if (observed.root == null || !observed.root.isActiveAndEnabled)
                { Disconnect(observed); continue; }
                if (observed.connected && (observed.lifeGeneration != observed.root.LifeGeneration || observed.source == null || observed.source.IsDisposed))
                    Disconnect(observed);
                Connect(observed);
                if (!observed.connected) continue;
                // Health没有通用损血事件；每次可观测下降才响，同帧多笔损血合为一次。恢复和无变化保持静音。
                PollHealth(observed);
                if (observed.dodge != null && observed.dodge.IsActive && observed.dodge.ActionId != observed.dodgeId)
                {
                    observed.dodgeId = observed.dodge.ActionId;
                    Submit(observed, "movement.dodge", "dodge:" + observed.dodgeId.ToString("N"), observed.root.transform.position);
                }
                if (observed.offhand != null)
                {
                    if (observed.offhand.ReleasedCount != observed.bombs)
                    {
                        observed.bombs = observed.offhand.ReleasedCount;
                        Submit(observed, "combat.release", "offhand-release:" + observed.bombs, observed.root.transform.position);
                    }
                    if (observed.offhand.LandedCount != observed.landings)
                    {
                        observed.landings = observed.offhand.LandedCount;
                        Submit(observed, "delivery.land", "offhand-land:" + observed.landings, observed.offhand.LastLanding);
                    }
                }
                if (observed.skills != null && observed.skills.ResolvedCount != observed.skillLandings)
                {
                    observed.skillLandings = observed.skills.ResolvedCount;
                    // 旧Skills计数代表派生结算，即便空落也增长；只叫落地，不冒充Applied命中。
                    Submit(observed, "delivery.land", "legacy-skill-land:" + observed.skillLandings, observed.skills.LastEffectPosition);
                }
            }
            foreach (var observed in enemies)
            {
                if (observed.root == null || !observed.root.isActiveAndEnabled)
                { Disconnect(observed); continue; }
                if (observed.connected && (observed.source == null || observed.source.IsDisposed ||
                    observed.hp == 0 && observed.root.Vitals?.Health != null && observed.root.Vitals.Health.Current > 0)) Disconnect(observed);
                Connect(observed);
                if (!observed.connected || observed.vitals?.Health == null) continue;
                var hp = observed.vitals.Health.Current;
                if (hp < observed.hp) Submit(observed, "combat.hurt", "hp:" + observed.hp + ">" + hp, observed.vitals.WorldPosition);
                observed.hp = hp;
            }
            actors.RemoveAll(a => a.root == null);
            enemies.RemoveAll(e => e.root == null);
            ObserveSharedFacts();
        }

        private void ObserveSharedFacts()
        {
            if (bottle != null && bottle.isActiveAndEnabled && bottle.UseCount != bottleUses)
            {
                bottleUses = bottle.UseCount;
                var owner = bottle.Coordinator == null ? null : bottle.Coordinator.Current;
                var observed = actors.FirstOrDefault(a => a.root == owner && a.connected);
                if (observed != null) Submit(observed, "item.heal", "bottle-use:" + bottleUses, owner.transform.position);
            }
            var current = coordinator == null ? null : coordinator.Current;
            if (current == previousCurrent) return;
            // 初始化和失去全部控制权不虚构UI确认声；只有已存在的Current实际换到另一个合法成员。
            var changed = previousCurrent != null && current != null;
            previousCurrent = current;
            if (!changed || interfaceSource == null || interfaceSource.IsDisposed) return;
            var key = "control-switch:" + (++interfaceSequence) + ":" + current.ActorId;
            Record("ui.control.switch", key, interfaceSource.Submit("ui.control.switch", key, current.transform.position));
        }

        private static void Disconnect(ActorObservation observed)
        {
            foreach (var remove in observed.remove) remove();
            observed.remove.Clear();
            observed.source?.Dispose();
            observed.source = null;
            observed.connected = false;
            observed.rootFacts.Clear();
            observed.rootFactOrder.Clear();
        }

        private static void Disconnect(EnemyObservation observed)
        {
            if (observed.root != null && observed.action != null) observed.root.ActionObserved -= observed.action;
            if (observed.melee != null && observed.receipt != null) observed.melee.Resolved -= observed.receipt;
            observed.source?.Dispose();
            observed.source = null;
            observed.connected = false;
            observed.rootFacts.Clear();
            observed.rootFactOrder.Clear();
        }

        private void DisconnectAll()
        {
            if(audioRoot!=null)audioRoot.ReleaseSfxFacts(this);
            foreach (var observed in actors) Disconnect(observed);
            foreach (var observed in enemies) Disconnect(observed);
            actors.Clear();
            enemies.Clear();
            interfaceSource?.Dispose();
            interfaceSource = null;
            audioRoot = null;
            observedWorld = null;
        }

        private void OnDisable() { DisconnectAll(); }
        private void OnDestroy() { DisconnectAll(); }
    }
}
