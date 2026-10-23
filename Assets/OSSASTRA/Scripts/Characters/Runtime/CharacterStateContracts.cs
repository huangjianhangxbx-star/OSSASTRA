/* 角色状态契约：注册只初始化，Enter/Exit只报告边沿；请求由各能力事务受理。 */
using Ossastra.CharacterCore;
namespace Ossastra.Characters
{
    public interface ICharacterStateMachine
    {
        string StateMachineName { get; }
        string CurrentStateName { get; }
        bool IsActive { get; }
        bool AllowsRequest(CharacterActionKind kind, out string reason);
        void Initialize(CharacterRoot root);
        void Enter();
        void Exit();
        void Update(float deltaTime);
    }

    public interface IParallelCharacterStateMachine : ICharacterStateMachine { }

    public abstract class CharacterStateMachine : ICharacterStateMachine
    {
        protected CharacterRoot owner;
        public int InitializationCount { get; private set; }
        public int EnterCount { get; private set; }
        public int ExitCount { get; private set; }
        public abstract string StateMachineName { get; }
        public abstract string CurrentStateName { get; }
        public abstract bool IsActive { get; }
        // 替换/并行资格必须在事务启动前查询；不能先Exit原动作再检查新请求。
        public virtual bool AllowsRequest(CharacterActionKind kind, out string reason) { reason = null; return true; }
        public virtual void Initialize(CharacterRoot root)
        {
            if (owner != null) throw new System.InvalidOperationException("State machine initialized twice");
            owner = root;
            InitializationCount++;
        }
        public virtual void Enter() { EnterCount++; }
        public virtual void Exit() { ExitCount++; }
        public abstract void Update(float deltaTime);
    }
}
