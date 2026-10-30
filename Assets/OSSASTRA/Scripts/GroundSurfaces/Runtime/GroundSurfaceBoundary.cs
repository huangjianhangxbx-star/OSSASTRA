/*
 * 脚本：GroundSurfaceBoundary
 * 功能：保存从指定侧壁Mesh读回并校验的连接环、来源指纹和版本。
 * 边界：仅是可重建的模型接合证据，不保存Tile格、不改变源模型、不猜测最高轮廓。
 * 协作：Editor提边服务写入，几何域/顶面缓存/Collider共同消费同一环。
 */
using System;
using UnityEngine;
namespace Ossastra.GroundSurfaces
{
    public sealed class GroundSurfaceBoundary : ScriptableObject
    {
        public Vector2[] rim = Array.Empty<Vector2>();
        public float localTopY;
        [Min(.000001f)] public float weldTolerance = .0001f;
        [Min(.000001f)] public float planeTolerance = .0005f;
        public string sourceFingerprint;
        public string[] sourceMeshGuids = Array.Empty<string>();
        public int revision;
    }
}
