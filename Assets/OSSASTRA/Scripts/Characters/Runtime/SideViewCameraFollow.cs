/*
 * 脚本：SideViewCameraFollow
 * 功能：唯一写入横版游戏相机。跟随角色的显示位置，并合成独立的冲击偏移。
 * 边界：基础镜头不回读上一帧冲击，玩法鼠标射线和边界查询使用稳定视图。
 */
using Ossastra.Combat;
using UnityEngine;

namespace Ossastra.Characters
{
    /// <summary>唯一横版镜头写入者。消费主控的显示锚点和运动事实，投影由实际相机提供。</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class SideViewCameraFollow : MonoBehaviour, ICombatImpactShakeSink
    {
        [SerializeField] private CharacterControlCoordinator session;
        [SerializeField] private SideViewCameraProfile profile;
        [SerializeField] private Vector2 minimumCenter = new Vector2(14.5f, 6.3f);
        [SerializeField] private Vector2 maximumCenter = new Vector2(98f, 22f);
        [SerializeField] private float verticalOffset = 5.4f;
        [SerializeField] private float depth = -38.1f;
        [SerializeField, Min(0f)] private float smoothTime = .12f;
        [Header("命中震屏")]
        [SerializeField] private SideViewCameraShakeSettings shake = new SideViewCameraShakeSettings();

        private readonly SideViewCameraShake impactShake = new SideViewCameraShake();
        private Camera view;
        private CharacterRoot followed;
        private CharacterMovement motor;
        private uint contactRevision;
        private Vector2 velocity;
        private float lookX, lookY, switchRemaining;

        public Vector2 TargetCenter { get; private set; }
        public Vector3 BasePosition { get; private set; }
        public Vector2 AppliedShakeOffset { get; private set; }
        public SideViewCameraShakeSettings ShakeSettings => shake;

        public void Configure(CharacterControlCoordinator coordinator) => session = coordinator;

        private void Awake()
        {
            view = GetComponent<Camera>();
            BasePosition = transform.position;
            // 横版画片按固定世界深度排序，镜头横移或冲击不能交换同层画片。
            view.transparencySortMode = TransparencySortMode.CustomAxis;
            view.transparencySortAxis = Vector3.forward;
        }

        private void OnDisable()
        {
            ClearShake();
            ResetFollow();
        }

        private void ResetFollow()
        {
            followed = null;
            motor = null;
            velocity = Vector2.zero;
            lookX = lookY = switchRemaining = 0f;
        }

        public void RequestImpact(CombatImpactShakeRequest request)
        {
            if (isActiveAndEnabled && session != null && session.Current != null)
                impactShake.Request(request, shake);
        }

        /// <summary>用稳定相机读取玩法瞄准。原横版相机的震动只有平移，射线方向与投影保持一致。</summary>
        public static Ray GameplayRay(Camera camera, Vector2 pixel)
        {
            var ray = camera.ScreenPointToRay(pixel);
            if (camera.TryGetComponent<SideViewCameraFollow>(out var follow))
                ray.origin -= (Vector3)follow.AppliedShakeOffset;
            return ray;
        }

        private void ClearShake()
        {
            impactShake.Clear();
            if (AppliedShakeOffset != Vector2.zero) transform.position = BasePosition;
            AppliedShakeOffset = Vector2.zero;
        }

        private void LateUpdate()
        {
            var current = session == null ? null : session.Current;
            if (current == null)
            {
                ClearShake();
                if (followed != null) ResetFollow();
                return;
            }

            bool switched = current != followed;
            bool initial = followed == null;
            if (switched)
            {
                followed = current;
                motor = current.GetComponent<CharacterMovement>();
                velocity = Vector2.zero;
                lookX = lookY = 0f;
                switchRemaining = profile == null ? 0f : profile.switchBlendSeconds;
            }
            uint revision = motor == null ? 0 : motor.ContactRevision;
            bool relocated = !switched && revision != contactRevision;
            contactRevision = revision;
            Vector3 anchor = motor == null ? current.transform.position : motor.PresentedPosition;
            float elapsed = Time.deltaTime;
            var center = (Vector2)BasePosition;
            Vector2 minOffset = default, maxOffset = default;
            if (profile == null)
            {
                TargetCenter = new Vector2(Mathf.Clamp(anchor.x, minimumCenter.x, maximumCenter.x),
                    Mathf.Clamp(anchor.y + verticalOffset, minimumCenter.y, maximumCenter.y));
                center = switched || relocated || smoothTime <= 0f ? TargetCenter :
                    Vector2.SmoothDamp(center, TargetCenter, ref velocity, smoothTime);
            }
            else
            {
                bool falling = motor != null && motor.VerticalSpeed < -profile.fallingSpeedThreshold;
                float direction = motor == null || Mathf.Abs(motor.Velocity.x) < .01f ? 0f : Mathf.Sign(motor.Velocity.x);
                lookX = Mathf.MoveTowards(lookX, direction * profile.horizontalLookAhead, profile.horizontalLookSpeed * elapsed);
                lookY = Mathf.MoveTowards(lookY, falling ? -profile.fallingLookAhead : 0f, profile.fallingLookSpeed * elapsed);
                TargetCenter = new Vector2(anchor.x + lookX, anchor.y + profile.verticalOffset + lookY);
                foreach (var area in profile.roomFraming)
                {
                    if (!area.actorArea.Contains(anchor)) continue;
                    TargetCenter = new Vector2(Mathf.Min(TargetCenter.x, area.maximumCameraX), area.cameraY);
                    break;
                }
                ReadViewportOffsets(anchor.z, out minOffset, out maxOffset);
                TargetCenter = Confine(TargetCenter, minOffset, maxOffset);
                bool snap = initial || relocated || switched && Vector2.Distance(center, TargetCenter) > profile.switchSnapDistance;
                if (snap)
                {
                    center = TargetCenter;
                    velocity = Vector2.zero;
                    switchRemaining = 0f;
                }
                else if (elapsed > 0f)
                {
                    float tx = switchRemaining > 0f ? profile.switchSmoothTime : profile.horizontalSmoothTime;
                    float ty = switchRemaining > 0f ? profile.switchSmoothTime :
                        falling ? profile.fallingSmoothTime : profile.verticalSmoothTime;
                    center.x = Mathf.SmoothDamp(center.x, TargetCenter.x, ref velocity.x, tx);
                    center.y = Mathf.SmoothDamp(center.y, TargetCenter.y, ref velocity.y, ty);
                    center = Confine(center, minOffset, maxOffset);
                    switchRemaining = Mathf.Max(0f, switchRemaining - elapsed);
                }
            }
            BasePosition = new Vector3(center.x, center.y, depth);
            // 用角色平面的可见高度标定平移幅度，镜头缩放时仍保持相近的屏幕占比。
            float viewportHeight = view.orthographic ? view.orthographicSize * 2f :
                2f * Mathf.Abs(anchor.z - depth) * Mathf.Tan(view.fieldOfView * .5f * Mathf.Deg2Rad);
            var finalCenter = center + impactShake.Tick(elapsed, viewportHeight, shake);
            if (profile != null) finalCenter = Confine(finalCenter, minOffset, maxOffset);
            AppliedShakeOffset = finalCenter - center;
            transform.position = new Vector3(finalCenter.x, finalCenter.y, depth);
        }

        private Vector2 Confine(Vector2 center, Vector2 minOffset, Vector2 maxOffset)
            => new Vector2(ConfineAxis(center.x, profile.worldBounds.xMin - minOffset.x, profile.worldBounds.xMax - maxOffset.x),
                ConfineAxis(center.y, profile.worldBounds.yMin - minOffset.y, profile.worldBounds.yMax - maxOffset.y));

        private static float ConfineAxis(float desired, float minimum, float maximum)
            => minimum <= maximum ? Mathf.Clamp(desired, minimum, maximum) : (minimum + maximum) * .5f;

        private void ReadViewportOffsets(float planeZ, out Vector2 minimum, out Vector2 maximum)
        {
            minimum = maximum = Vector2.zero;
            var plane = new Plane(Vector3.forward, new Vector3(0f, 0f, planeZ));
            for (int i = 0; i < 4; i++)
            {
                var ray = view.ViewportPointToRay(new Vector3(i & 1, i >> 1, 0f));
                // 投影只含平移震动。先将射线还原到稳定基准，再计算边界相对偏移。
                ray.origin -= (Vector3)AppliedShakeOffset;
                if (!plane.Raycast(ray, out float distance)) continue;
                Vector2 offset = ray.GetPoint(distance) - BasePosition;
                minimum = Vector2.Min(minimum, offset);
                maximum = Vector2.Max(maximum, offset);
            }
        }
    }
}
