/*
 * 脚本：SideViewCameraFollow
 * 功能：跟随现有会话当前控制角色，沿XY移动并维持复刻场景的正侧视透视。
 * 边界：只调整相机，不采集输入、改变角色姿态或控制权。
 * 协作：角色在Update完成移动，相机在LateUpdate采样最终位置。
 */
using UnityEngine;

namespace Ossastra.Characters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class SideViewCameraFollow : MonoBehaviour
    {
        [SerializeField] private CharacterControlCoordinator session;
        [SerializeField] private Vector2 minimumCenter = new Vector2(14.5f, 6.3f);
        [SerializeField] private Vector2 maximumCenter = new Vector2(98f, 22f);
        [SerializeField] private float verticalOffset = 5.4f;
        [SerializeField] private float depth = -38.1f;
        [SerializeField, Min(0f)] private float smoothTime = 0.12f;

        private CharacterRoot followed;
        private Vector2 velocity;

        public void Configure(CharacterControlCoordinator coordinator)
        {
            session = coordinator;
        }

        private void LateUpdate()
        {
            if (session == null || session.Current == null) return;
            CharacterRoot current = session.Current;
            Vector3 position = current.transform.position;
            Vector2 desired = new Vector2(
                Mathf.Clamp(position.x, minimumCenter.x, maximumCenter.x),
                Mathf.Clamp(position.y + verticalOffset, minimumCenter.y, maximumCenter.y));
            bool switched = current != followed;
            followed = current;
            if (switched) velocity = Vector2.zero;
            Vector2 center = switched || smoothTime == 0f
                ? desired
                : Vector2.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
            transform.position = new Vector3(center.x, center.y, depth);
        }
    }
}
