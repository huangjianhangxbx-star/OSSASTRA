/* 仲裁器：集中注册和显式阶段调度；不增加独立Update、不替代动作事务许可。
 * 输入→资源/架势→朝向→候选受理→唯一动作时钟→世界效果→最新许可运动。
 * Idle维护与Enter/Exit分离：未激活状态仍需维护时钟，但不能重复进入/退出。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using Ossastra.CharacterCore;
namespace Ossastra.Characters
{
    [DisallowMultipleComponent]
    public sealed class StateArbitrator : MonoBehaviour
    {
        [Serializable] public sealed class StateSnapshot
        {
            public string machine, state;
            public bool active;
            public int initialized, enters, exits;
        }
        [SerializeField] private List<StateSnapshot> snapshots = new List<StateSnapshot>();
        [SerializeField] private string lastRejection;
        private readonly Dictionary<string, CharacterStateMachine> registrations = new Dictionary<string, CharacterStateMachine>();
        private readonly List<CharacterStateMachine> ordered = new List<CharacterStateMachine>();
        private readonly HashSet<CharacterStateMachine> entered = new HashSet<CharacterStateMachine>();
        private CharacterRoot owner;
        public CharacterCombatStateMachine Combat { get; private set; }
        public MovementStateMachine Movement { get; private set; }
        public DodgeStateMachine Dodge { get; private set; }
        public AbilityStateMachine Ability { get; private set; }
        public WeaponPostureStateMachine WeaponPosture { get; private set; }
        public int FrameCount { get; private set; }
        public IReadOnlyList<StateSnapshot> Snapshots => snapshots;
        public void Initialize(CharacterRoot root)
        {
            if (ReferenceEquals(owner, root)) return;
            if (owner != null) throw new InvalidOperationException("Arbitrator rebound to another owner");
            owner = root;
            // 多个请求名映射到同一实例；只按首次注册顺序初始化一次。
            Movement = new MovementStateMachine();
            Register("Idle", Movement); Register("Move", Movement);
            Combat = new CharacterCombatStateMachine(); Register("Combat", Combat);
            Dodge = new DodgeStateMachine(); Register("Dodge", Dodge);
            Ability = new AbilityStateMachine(); Register("Ability", Ability);
            WeaponPosture = new WeaponPostureStateMachine(); Register("WeaponPosture", WeaponPosture);
            foreach (var machine in ordered) machine.Initialize(root);
        }
        private void Register(string request, CharacterStateMachine machine)
        {
            registrations.Add(request, machine);
            if (!ordered.Contains(machine)) ordered.Add(machine);
        }
        public bool AllowsRequest(CharacterActionKind kind, out string reason)
        {
            // 按集中注册顺序询问真实状态规则；只检查，不Enter、取消或扣费。
            foreach (var machine in ordered)
                if (!machine.AllowsRequest(kind, out reason)) { lastRejection = reason; return false; }
            reason = null;
            return true;
        }
        private void ObserveEdge(CharacterStateMachine machine)
        {
            if (machine.IsActive) { if (entered.Add(machine)) machine.Enter(); }
            else if (entered.Remove(machine)) machine.Exit();
        }
        // 外部能力也可同步提交/取消身体动作；接受事实当场建立边沿，不等下一帧。
        internal void ObserveCombatEdge() { if (Combat != null) ObserveEdge(Combat); }
        private void Tick(CharacterStateMachine machine, float deltaTime)
        {
            ObserveEdge(machine);
            machine.Update(deltaTime); // 明确允许Idle维护，尤其战斗时钟不能因Idle停钟。
            ObserveEdge(machine);
        }
        public void ManagedUpdate(float deltaTime)
        {
            if (owner == null) return;
            FrameCount++;
            if (!owner.IsAlive) { WeaponPosture.MaintainDeath(); RefreshSnapshots(); return; }
            Combat.RefreshMainProfile();
            owner.StateCombatBrain?.Sample(owner.SimulationTime, deltaTime);
            owner.StateInputSource.Read();
            var frame = owner.DataBus.Input;
            var main = owner.StateMainAction;
            if (frame.weaponModePressed && main != null)
            {
                var choices = main.Choices;
                var index = Array.FindIndex(choices, p => p != null && p.definitionId == main.SelectedId);
                if (choices.Length > 0) main.Configure(choices[(index + 1) % choices.Length].definitionId);
            }
            Combat.PrepareFrame(frame);
            var resourceDelta = owner.IsSimulationSuspended ? 0 : deltaTime;
            owner.StateStatusEffects?.Tick(resourceDelta);
            owner.StateVitals?.TickResources(resourceDelta);
            owner.StatePosture?.Tick(resourceDelta, Combat.Runtime.IsActive);
            Movement.ResolveDirections(frame);
            Dodge.Prepare(frame);
            Ability.Prepare(frame, deltaTime);
            Combat.RequestInput(frame);
            WeaponPosture.Prepare(frame);
            Tick(WeaponPosture, deltaTime);
            Combat.RequestCompanion();
            Tick(Combat, deltaTime);
            Tick(Ability, deltaTime);
            Combat.CompleteFrame(deltaTime);
            Tick(Dodge, deltaTime);
            Tick(Movement, deltaTime);
            owner.StateStamina?.Tick(owner.SimulationTime, !Combat.Runtime.IsActive && !Dodge.IsActive && !WeaponPosture.IsActive && owner.CanSimulateIntent);
            RefreshSnapshots();
        }
        private void RefreshSnapshots()
        {
            if (Combat.LastFact.StartsWith("Rejected", StringComparison.Ordinal)) lastRejection = Combat.LastFact;
            while (snapshots.Count < ordered.Count) snapshots.Add(new StateSnapshot());
            for (var i = 0; i < ordered.Count; i++)
            {
                var machine = ordered[i]; ObserveEdge(machine);
                var snapshot = snapshots[i]; snapshot.machine = machine.StateMachineName; snapshot.state = machine.CurrentStateName;
                snapshot.active = machine.IsActive; snapshot.initialized = machine.InitializationCount;
                snapshot.enters = machine.EnterCount; snapshot.exits = machine.ExitCount;
            }
        }
        public void Cancel(string reason)
        {
            Combat?.CancelAction(reason);
            foreach (var machine in ordered) ObserveEdge(machine);
        }
        public void Dispose()
        {
            Combat?.Dispose();
            foreach (var machine in entered) machine.Exit();
            entered.Clear();
        }
    }
}
