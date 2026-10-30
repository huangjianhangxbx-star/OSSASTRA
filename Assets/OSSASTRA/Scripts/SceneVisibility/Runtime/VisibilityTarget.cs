/* 功能：把显式角色引用和稳定身体范围投影成可见性目标。
 * 边界：不采样设备、不选择Current、不扫描武器或隐藏Spine分支，不修改角色位置或动画。
 * 协作：SceneVisibilityRoot在正式GameplayCamera绘制前调用；检测点与可选直立深度使用同一脚点平面。
 */
using C = Ossastra.Characters;
using Ossastra.Characters.Rendering;
using UnityEngine;

namespace Ossastra.SceneVisibility
{
    [DisallowMultipleComponent]
    public sealed class VisibilityTarget : MonoBehaviour
    {
        [Header("显式角色与稳定身体范围（相机朝向显示平面，米）")]
        public C.CharacterRoot actor;
        [Min(.1f)] public float bodyHeight = 2.2f;
        [Min(.1f)] public float bodyWidth = 1.1f;
        [Tooltip("仅与已启用的CharacterUprightDepth配套；探测点使用同一逐像素竖直平面深度。")]
        public bool uprightDepth = true;
        readonly Vector3[] stablePoints = new Vector3[5];
        public Vector3[] StablePoints => stablePoints;
        public Vector2 CenterViewport { get; private set; }
        public float BodyPixels { get; private set; }
        public float CorePixels { get; private set; }
        public bool Eligible => actor != null && actor.isActiveAndEnabled && actor.IsAlive && isActiveAndEnabled;

        public bool Project(Camera camera, float padding)
        {
            if (!Eligible || camera == null || !Finite(bodyHeight) || !Finite(bodyWidth) ||
                bodyHeight <= 0f || bodyWidth <= 0f || camera.pixelHeight <= 0 || camera.pixelWidth <= 0) return false;
            var depth = uprightDepth ? actor.GetComponent<CharacterUprightDepth>() : null;
            if (uprightDepth && (depth == null || !depth.isActiveAndEnabled || depth.PreparedCamera != camera)) return false;
            Vector3 foot = actor.transform.position;
            Vector3 bottom = camera.WorldToViewportPoint(foot);
            Vector3 top = camera.WorldToViewportPoint(foot + camera.transform.up * bodyHeight);
            Vector3 left = camera.WorldToViewportPoint(foot + camera.transform.up * (bodyHeight * .5f) - camera.transform.right * (bodyWidth * .5f));
            Vector3 right = camera.WorldToViewportPoint(foot + camera.transform.up * (bodyHeight * .5f) + camera.transform.right * (bodyWidth * .5f));
            if (!VisibleDepth(bottom, camera) || !VisibleDepth(top, camera)) return false;
            CenterViewport = new Vector2((top.x + bottom.x) * .5f, (top.y + bottom.y) * .5f);
            if (CenterViewport.x < 0f || CenterViewport.x > 1f || CenterViewport.y < 0f || CenterViewport.y > 1f) return false;
            BodyPixels = Mathf.Abs(top.y - bottom.y) * camera.pixelHeight;
            float halfWidth = Mathf.Abs(right.x - left.x) * camera.pixelWidth * .5f;
            CorePixels = Mathf.Sqrt(halfWidth * halfWidth + BodyPixels * BodyPixels * .25f) + BodyPixels * padding;
            if (!Finite(CorePixels) || CorePixels <= 0f) return false;
            // 不使用脚底接触点检测，避免把承载表面误判为挡身；两肩、胸、头和下身覆盖稳定身体。
            float[] heights = Heights;
            for (int i = 0; i < 5; ++i)
            {
                float horizontal = i == 1 ? -.32f : i == 2 ? .32f : 0f;
                Vector3 display = foot + camera.transform.up * (bodyHeight * heights[i]) + camera.transform.right * (bodyWidth * horizontal);
                if (uprightDepth)
                {
                    if (!TryUprightPoint(camera, foot, display, out stablePoints[i])) return false;
                }
                else stablePoints[i] = display;
            }
            return true;
        }

        static readonly float[] Heights = { .18f, .55f, .55f, .62f, .9f };
        // 与Shader的逐像素ray/脚点XZ竖直平面相交一致；不是全角色共用脚点常量深度。
        public static bool TryUprightPoint(Camera camera, Vector3 foot, Vector3 display, out Vector3 result)
        {
            result = display;
            Vector3 n = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);
            if (n.sqrMagnitude < .01f) return false;
            n.Normalize();
            Ray ray = camera.ViewportPointToRay(camera.WorldToViewportPoint(display));
            float denominator = Vector3.Dot(n, ray.direction);
            if (Mathf.Abs(denominator) < .01f) return false;
            float distance = Vector3.Dot(n, foot - ray.origin) / denominator;
            if (!Finite(distance) || distance < 0f) return false;
            result = ray.GetPoint(distance);
            return true;
        }
        static bool VisibleDepth(Vector3 point, Camera camera) => Finite(point.x) && Finite(point.y) &&
            Finite(point.z) && point.z > camera.nearClipPlane && point.z < camera.farClipPlane;
        static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
