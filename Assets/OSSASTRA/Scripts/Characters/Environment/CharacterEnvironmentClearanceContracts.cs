/* 可选环境净空契约：只返回原 Movement 本次允许的位移与真实侧向受阻，不执行 Move。 */
using UnityEngine;
namespace Ossastra.Characters
{
    public readonly struct CharacterClearanceResult
    {
        public Vector3 Displacement { get; }
        public bool SideBlocked { get; }
        public CharacterClearanceResult(Vector3 displacement, bool sideBlocked)
        { Displacement = displacement; SideBlocked = sideBlocked; }
    }

    public interface ICharacterEnvironmentClearance
    {
        bool Initialize(Transform owner, CharacterController controller);
        CharacterClearanceResult Constrain(Vector3 requestedDisplacement);
        void Suspend();
        void Dispose();
    }
}
