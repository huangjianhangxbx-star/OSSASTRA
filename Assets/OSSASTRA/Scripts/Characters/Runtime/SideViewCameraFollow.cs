using UnityEngine;

namespace Ossastra.Characters
{
    /// <summary>唯一横版镜头写入者。消费主控的显示锚点和运动事实，投影由实际相机提供。</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class SideViewCameraFollow : MonoBehaviour
    {
        [SerializeField] private CharacterControlCoordinator session;
        [SerializeField] private SideViewCameraProfile profile;
        [SerializeField] private Vector2 minimumCenter = new Vector2(14.5f, 6.3f);
        [SerializeField] private Vector2 maximumCenter = new Vector2(98f, 22f);
        [SerializeField] private float verticalOffset = 5.4f;
        [SerializeField] private float depth = -38.1f;
        [SerializeField, Min(0f)] private float smoothTime = .12f;

        private Camera view;
        private CharacterRoot followed;
        private CharacterMovement motor;
        private uint contactRevision;
        private Vector2 velocity;
        private float lookX, lookY, switchRemaining;

        public Vector2 TargetCenter { get; private set; }

        public void Configure(CharacterControlCoordinator coordinator) => session = coordinator;

        private void Awake() => view = GetComponent<Camera>();

        private void OnDisable()
        {
            followed = null;
            motor = null;
            velocity = Vector2.zero;
            lookX = lookY = switchRemaining = 0f;
        }

        private void LateUpdate()
        {
            var current = session == null ? null : session.Current;
            if (current == null)
            {
                if (followed != null) OnDisable();
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
            var center = (Vector2)transform.position;
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
                ReadViewportOffsets(anchor.z, out Vector2 minOffset, out Vector2 maxOffset);
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
            transform.position = new Vector3(center.x, center.y, depth);
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
                if (!plane.Raycast(ray, out float distance)) continue;
                Vector2 offset = ray.GetPoint(distance) - transform.position;
                minimum = Vector2.Min(minimum, offset);
                maximum = Vector2.Max(maximum, offset);
            }
        }
    }
}
