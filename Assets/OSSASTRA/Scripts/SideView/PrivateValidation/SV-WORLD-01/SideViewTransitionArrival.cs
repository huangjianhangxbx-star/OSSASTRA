/* 脚本：SideViewTransitionArrival
 * 功能：消费目标入口的在途票据，放置双角色并初始化朝向。
 * 协作：通过原角色移动与朝向组件，在目标场相机更新及淡入之前完成落点交接。
 */
using UnityEngine;
using UnityEngine.SceneManagement;
using Ossastra.Characters;

namespace Ossastra.SideViewWorld
{
    // All Awake calls finish before Start, and the camera follows in LateUpdate.
    [DefaultExecutionOrder(-160)]
    [DisallowMultipleComponent]
    public sealed class SideViewTransitionArrival : MonoBehaviour
    {
        public string gateId;
        public CharacterControlCoordinator party;
        public Vector3 firstActorPosition;
        public Vector3 secondActorPosition;

        public bool Arrived { get; private set; }
        public string Diagnostic { get; private set; } = "无在途换区";

        private void Awake() => SideViewTransitionRuntime.Ensure();

        private void Start()
        {
            if (party == null || party.Characters.Count != 2)
            {
                Diagnostic = "落点缺少双角色会话";
                return;
            }
            foreach (var character in party.Characters)
                if (character == null || character.GetComponent<CharacterMovement>() == null
                    || character.GetComponent<CharacterOrientation>() == null)
                {
                    Diagnostic = "落点角色缺少运动或朝向组件";
                    return;
                }
            var scene = SceneManager.GetActiveScene();
            var runtime = SideViewTransitionRuntime.Ensure();
            if (!runtime.TryConsumeArrival(scene.path, gateId)) return;
            for (var i = 0; i < party.Characters.Count; i++)
            {
                var character = party.Characters[i];
                var position = i == 0 ? firstActorPosition : secondActorPosition;
                character.GetComponent<CharacterMovement>().Teleport(position);
                // 横版门的安全落点在门内侧，朝门到落点的水平方向继续前进。
                var direction = Vector3.right * (position.x - transform.position.x);
                character.GetComponent<CharacterOrientation>().InitializeArrivalFacing(direction);
            }
            Arrived = true;
            Diagnostic = "已落在 " + gateId;
            runtime.ConfirmArrival(scene.path, gateId);
        }
    }
}
