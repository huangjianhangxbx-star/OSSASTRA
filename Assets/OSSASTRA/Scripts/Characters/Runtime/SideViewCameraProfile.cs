using System;
using UnityEngine;

namespace Ossastra.Characters
{
    /// <summary>横版场独立镜头参数，房间与镜头屏幕边界使用世界坐标。</summary>
    [CreateAssetMenu(menuName = "Ossastra/Characters/Side View Camera")]
    public sealed class SideViewCameraProfile : ScriptableObject
    {
        [Serializable]
        public struct RoomFraming
        {
            public Rect actorArea;
            public float cameraY;
            public float maximumCameraX;
        }

        [SerializeField] internal Rect worldBounds = new Rect(0f, 0f, 120f, 32f);
        [SerializeField] internal float verticalOffset = 5.4f;
        [SerializeField, Min(0f)] internal float horizontalLookAhead = 1f;
        [SerializeField, Min(0f)] internal float horizontalLookSpeed = 6f;
        [SerializeField, Min(0f)] internal float fallingSpeedThreshold = 22f;
        [SerializeField, Min(0f)] internal float fallingLookAhead = 1f;
        [SerializeField, Min(0f)] internal float fallingLookSpeed = 4f;
        [SerializeField, Min(.01f)] internal float horizontalSmoothTime = .10f;
        [SerializeField, Min(.01f)] internal float verticalSmoothTime = .10f;
        [SerializeField, Min(.01f)] internal float fallingSmoothTime = .045f;
        [SerializeField, Min(.01f)] internal float switchSmoothTime = .14f;
        [SerializeField, Min(.01f)] internal float switchBlendSeconds = .28f;
        [SerializeField, Min(0f)] internal float switchSnapDistance = 12f;
        [SerializeField] internal RoomFraming[] roomFraming = Array.Empty<RoomFraming>();
    }
}
