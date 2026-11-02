/*
 * 功能：将普通玩家自然视野与独立有限观察距离分开，避免旧门窗局部参数限制所有观察者。
 * 边界：本轮均使用观察者前半空间；不实现敌人侦察、手电光锥、照明或业务可见状态。
 * 生命周期：不可变值策略，无场景引用、缓存和释放职责；默认值保留旧二参语义。
 */
using System;

namespace Ossastra.SceneVisibility.Rooms.Core
{
    public readonly struct PortalSightPolicy
    {
        private readonly byte mode;
        public float ObserverRange { get; }
        public float MaxSightDistance { get; }
        internal bool UsesLegacyPortalLimits => mode == 0;
        internal bool HasFiniteLimits => mode == 2;

        public static PortalSightPolicy Legacy => default;
        public static PortalSightPolicy PlayerNatural => new PortalSightPolicy(1, float.PositiveInfinity, float.PositiveInfinity);

        /// <summary>
        /// 独立有限入口：ObserverRange 为到真实孔洞的最近距离，MaxSightDistance 为孔洞后沿目标法线的长度。
        /// 不等于任意角度的聚光锥，也不自动接入敌人或道具业务。
        /// </summary>
        public static PortalSightPolicy Finite(float observerRange, float maxSightDistance)
        {
            if (!SightVector3.Finite(observerRange) || observerRange < 0)
                throw new ArgumentOutOfRangeException(nameof(observerRange));
            if (!SightVector3.Finite(maxSightDistance) || maxSightDistance <= PortalSightGeometry.Epsilon)
                throw new ArgumentOutOfRangeException(nameof(maxSightDistance));
            return new PortalSightPolicy(2, observerRange, maxSightDistance);
        }

        private PortalSightPolicy(byte mode, float observerRange, float maxSightDistance)
        { this.mode = mode; ObserverRange = observerRange; MaxSightDistance = maxSightDistance; }
    }
}
