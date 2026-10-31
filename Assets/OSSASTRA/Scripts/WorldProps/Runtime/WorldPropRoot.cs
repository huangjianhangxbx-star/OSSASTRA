/* 脚本：WorldPropRoot
 * 功能：独立场景物实体根，把原网关Applied回执连接唯一Health和可替换表现。
 * 边界：没有Update、输入、敌人AI或背包写入；只接受已登记队伍来源，禁虚血夺回。
 * 协作：WorldPropsRoot初始化/恢复/有界调度；破坏停用拥有的碰撞/透视组，视觉晚回调不能复活。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using Ossastra.CharacterCore;
using Ossastra.Combat;
using Ossastra.SceneVisibility;

namespace Ossastra.WorldProps
{
    [DisallowMultipleComponent]
    public sealed class WorldPropRoot : MonoBehaviour, ICombatTarget, ICombatContactFilter, ICombatReclaimPolicy, ICombatProjectileContact
    {
        public string instanceId;
        [Min(1)]
        public int lifeGeneration = 1;
        public WorldPropDefinitionSO definition;
        public Collider[] ownedColliders = Array.Empty<Collider>();
        public Collider support;
        [Tooltip("动态高台绑定优先于短命顶盖Collider；缓存重建后读取同一绑定的当前顶盖，不跨层寻地。")]
        public Ossastra.GroundSurfaces.GroundSurfaceBinding supportSurface;
        public string surfaceId;
        public Transform safeDropAnchor;
        public bool faceCamera;
        public Transform visualRoot;
        public WorldPropView presentation;
        public SceneOccluderGroup visibilityGroup;
        public WorldPropState State { get; private set; }
        public Health Health => State?.Health;
        public bool IsAlive => State != null && State.Phase == WorldPropPhase.Alive && Health.Current > 0;
        public bool IsAvailable => isActiveAndEnabled && owner != null && owner.Ready && definition != null && definition.destructible && IsAlive;
        public bool AllowsReclaim => false;
        public bool AllowsDirectProjectile => true;
        public CombatFaction Faction => CombatFaction.Neutral;
        public Vector3 WorldPosition => transform.position;
        public Transform TargetTransform => transform;

        WorldPropsRoot owner;
        HashSet<string> sources;
        public void Initialize(WorldPropsRoot root, WorldPropState state, HashSet<string> legalSources)
        {
            if (root == null || state == null || definition == null || state.InstanceId != instanceId || state.DefinitionId != definition.definitionId || visualRoot == null || visualRoot == transform)
                throw new ArgumentException("Invalid prop assembly/identity/visual child");
            owner = root;
            State = state;
            sources = legalSources;
            bool depleted = state.Phase != WorldPropPhase.Alive;
            if (presentation != null)
                presentation.ResetView(depleted);
            SetDepleted(depleted);
        }

        public bool Accepts(AttackRelease release) => IsAvailable && release != null && sources != null && sources.Contains(release.ActorId);
        public void OnHitResolved(HitOutcome outcome)
        {
            if (State == null || outcome == null || outcome.Reason != HitReason.Applied || outcome.TargetId != Health.Id)
                return;
            State.OnHit(outcome);
            if (presentation != null)
            {
                presentation.ShowHit(Health.Current, Health.Maximum);
                owner.Activate(presentation);
            }

            owner.PlayFact(this, outcome.ActionId.ToString("N"), State.Phase == WorldPropPhase.Alive ? definition.hitSoundEvent : definition.breakSoundEvent);
            if (State.Phase != WorldPropPhase.Alive)
            {
                SetDepleted(true);
                owner.Enqueue(this);
            }
        }

        public void SetDepleted(bool value)
        {
            foreach (var body in ownedColliders)
                if (body != null)
                    body.enabled = !value;
            if (visibilityGroup != null)
                visibilityGroup.enabled = !value;
            if (visualRoot != null)
                visualRoot.gameObject.SetActive(!value);
            if (value && presentation != null)
                presentation.ShowDepleted();
        }

        public void ShowHit()
        {
        } // 兼容中立旧接口；真实耐久只从OnHitResolved消费。
    }
}
