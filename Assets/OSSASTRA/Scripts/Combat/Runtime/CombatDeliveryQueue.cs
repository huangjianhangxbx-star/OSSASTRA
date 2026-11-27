/* 脚本：CombatDeliveryQueue
 * 功能：共享近战扇区、射线、飞行弹、抛物和落点投递，向唯一网关提交接触和释放空间事实。
 * 边界：不认识玩家根/装备/库存、不付款；每释放索引唯一，伤害和姿态仍由AttackRelease结算。
 * 协作：调用者注入来源Vitals、可选既有释放和生命周期许可。飞行按模拟步扫掠；需要首次物理阻挡的投递还检查起点重叠。
 */
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Ossastra.CharacterCore;
namespace Ossastra.Combat
{
    public enum DeliveryMode { Sector, Ray, Projectile, Parabolic, RayLandingArc }
    [Serializable]
    public sealed class DeliveryParameters
    {
        public DeliveryMode mode;
        [Min(.01f)] public float range=2,radius=.6f;
        [Range(0,360)] public float arc=90;
        [Min(.01f)] public float speed=10,flightTime=.45f,collisionRadius=.08f;
        [Min(0)] public float arcHeight=1;
        [Min(0)] public float activeDuration;
        public bool followSource;
        public LayerMask collisionLayers=~0;
        [Tooltip("投射物在首个非来源实物接触结束，包括不合法目标及发射点重叠。")]
        public bool stopAtFirstContact;
        [Tooltip("横版射线或飞行沿平面的左右轴投递。")]
        public bool horizontalSideView;
        public DeliveryParameters Freeze()
        {
            if(!Enum.IsDefined(typeof(DeliveryMode),mode)||!Positive(range)||!Positive(radius)||!Positive(speed)||!Positive(flightTime)||!Positive(collisionRadius)||!Finite(arc)||arc<0||arc>360||!Finite(arcHeight)||arcHeight<0||!Finite(activeDuration)||activeDuration<0)
                throw new ArgumentException("delivery range/radius/arc/speed/time invalid");
            return (DeliveryParameters)MemberwiseClone();
        }
        private static bool Finite(float n)=>!float.IsNaN(n)&&!float.IsInfinity(n);
        private static bool Positive(float n)=>Finite(n)&&n>0;
    }
    public sealed class CombatDeliveryQueue
    {
        private sealed class Flight
        {
            public CombatVitals source;
            public DeliveryParameters config;
            public AttackRelease release;
            public CombatContactGeometry Geometry;
            public Vector3 origin,landing,position,direction;
            public float elapsed;
            public int emittedFrame;
            public Func<bool> valid;
            public Action<IReadOnlyList<HitOutcome>> resolved;
            public Action<Vector3> flightPosition;
            public GameObject visual;
            public Guid parent;
            public string channel;
        }
        // 保留批次的空结果，向行为区分实物挡路与射程耗尽。
        public const string RejectedProjectileContact = "projectile-contact-rejected";
        private readonly List<Flight> flights=new List<Flight>();
        private readonly HashSet<(Guid,int)> emitted=new HashSet<(Guid,int)>();
        private readonly HashSet<Guid> activeParents=new HashSet<Guid>();
        public void RegisterParent(Guid parent){if(parent!=Guid.Empty)activeParents.Add(parent);}
        public void CompleteParent(Guid parent){activeParents.Remove(parent);ForgetCompleted(parent);}
        /// <summary>取消确切父动作拥有的投递。父动作注册和重复释放记录仍由调用者收尾。</summary>
        public void CancelParentFlights(Guid parent)
        {
            for(int i=flights.Count-1;i>=0;i--)
                if(flights[i].parent==parent)Remove(i);
        }
        /// <summary>取消随身体移动的扇区窗口。已发出的独立投射物保留原生命周期。</summary>
        public void CloseAttachedContacts(Guid parent)
        {
            for (int i = flights.Count - 1; i >= 0; i--)
            {
                var flight = flights[i];
                if (flight.parent == parent && flight.config.mode == DeliveryMode.Sector && flight.config.followSource)
                    Remove(i);
            }
        }
        private void ForgetCompleted(Guid parent)
        {if(!activeParents.Contains(parent)&&!flights.Any(f=>f.parent==parent))emitted.RemoveWhere(key=>key.Item1==parent);}
        public int InFlightCount=>flights.Count;
        public int ResolvedCount {get;private set;}
        public bool Emit(CombatVitals source,Guid parent,int index,DeliveryParameters config,int damage,float posture,Vector3 origin,Vector3 direction,Vector3 landing,Func<bool> valid,Action<IReadOnlyList<HitOutcome>> resolved,Sprite sprite=null,float scale=.25f,double reclaimBudget=.05,double reclaimRate=.5,int basicAttackStage=0,AttackHitStop hitStop=default,string channel=null,Action<AttackRelease,string> bindRelease=null,
            AttackRelease preparedRelease=null,Action<Vector3> flightPosition=null)
        {
            if(source==null||!source.IsAvailable||parent==Guid.Empty||!activeParents.Contains(parent)||index<0||damage<=0||config==null||valid==null||!valid())return false;
            // 已接受普通动作可直接提交它的动画释放事实；拒绝错配身份及瞬时释放被冒用为持续窗口。
            if(preparedRelease!=null && (preparedRelease.ActorId!=source.Health.Id || preparedRelease.ParentExecutionId!=parent ||
                preparedRelease.EmissionIndex!=index || config.mode==DeliveryMode.Sector && config.activeDuration>0))return false;
            var frozen=config.Freeze();
            if(!emitted.Add((parent,index)))return false;
            var plane = CombatSpatialTarget.PlaneFor(source);
            var travelDirection = plane == CombatPlane.SideXY ? CombatContactGeometry.Project(direction, plane) : direction;
            if(plane==CombatPlane.SideXY && frozen.horizontalSideView)
                travelDirection = Mathf.Abs(travelDirection.x) < .001f ? Vector3.zero : Vector3.right * Mathf.Sign(travelDirection.x);
            var release=preparedRelease??(config.mode==DeliveryMode.Sector&&config.activeDuration>0?
                AttackRelease.ForContactWindow(parent,index,source.Health.Id,damage,posture,reclaimBudget,reclaimRate,hitStop):
                AttackRelease.ForEmission(parent,index,source.Health.Id,damage,posture,reclaimBudget,reclaimRate,hitStop));
            var flight=new Flight{source=source,parent=parent,config=frozen,
                Geometry=new CombatContactGeometry(plane,origin,travelDirection,travelDirection,basicAttackStage),release=release,
                origin=origin,landing=landing,direction=travelDirection.normalized,position=origin+Vector3.up*.8f,
                valid=valid,resolved=resolved,emittedFrame=Time.frameCount,channel=channel,flightPosition=flightPosition};
            // 取得本次真实释放身份后绑定招式快照；同步射线与扇区可能在本帧立即返回命中。
            bindRelease?.Invoke(flight.release, channel);
            CombatTraceProbe.For(source,true)?.Note("判定", "发射", config.mode + " · 持续 " + config.activeDuration + " s",
                flight.release.ActionId, parent, index);
            if(config.mode==DeliveryMode.Sector)
            {
                CombatContactGateway.PublishSectorRelease(flight.release, source, flight.Geometry, .8f);
                var targets = Nearby(flight, origin, config.range, true, out var contacts);
                // 墙面没有伤害身份；仅释放时对刀口前向的实物接触给一次墙面反馈。
                var wallHit = plane == CombatPlane.SideXY &&
                    !targets.Any(target => CombatContactGateway.AllowsTargetFaction(flight.release, source, target))
                    ? FirstContact(flight, flight.position, flight.position + flight.direction * config.range)
                    : null;
                Resolve(flight, origin, targets, flight.direction, contacts, wallHit);
                if(config.activeDuration>0){flights.Add(flight);source.Recovery?.RetainParent(parent);}
                return true;
            }
            if(config.mode==DeliveryMode.Ray)
            {
                var hit=FirstContact(flight,flight.position,flight.position+flight.direction*config.range);
                var targets = hit.HasValue ? Target(hit.Value.collider) : Array.Empty<IHitTarget>();
                Resolve(flight, hit?.point ?? origin, targets, flight.direction, null, hit);
                return true;
            }
            ReportPosition(flight,flight.position);
            if(frozen.mode==DeliveryMode.Projectile && frozen.stopAtFirstContact)
            {
                // SphereCast不会可靠返回已重叠的碰撞体。明确在注册阶段检查同一个球及同一个遮罩。
                var overlap=InitialBlocker(flight);
                if(overlap!=null)
                {
                    var contact=overlap.bounds.ClosestPoint(flight.position);
                    ReportPosition(flight,contact);
                    Resolve(flight,contact,ProjectileTargets(flight,overlap),flight.direction,null,null,overlap);
                    return true;
                }
            }
            if(sprite!=null)
            {
                flight.visual=new GameObject("AbilityProjectile_"+parent.ToString("N")+"_"+index);
                var renderer=flight.visual.AddComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.sortingOrder=50;
                flight.visual.transform.localScale=Vector3.one*scale;if(Camera.main!=null)flight.visual.transform.rotation=Camera.main.transform.rotation;
                flight.visual.transform.position=flight.position;
                Ossastra.SceneVisibility.Rooms.RoomPresentationBinding.Attach(renderer,flight.position,true,flight.visual.transform);
            }
            source.Recovery?.RetainParent(parent);flights.Add(flight);return true;
        }
        public void Tick(float delta, bool detachedOnly = false)
        {
            for(int i=flights.Count-1;i>=0;i--)
            {
                if(i>=flights.Count)continue;
                var f=flights[i];if(f.source==null||!f.valid()){Remove(i);continue;}
                if(f.emittedFrame==Time.frameCount || (detachedOnly && f.config.mode==DeliveryMode.Sector && f.config.followSource))continue;
                var previousElapsed=f.elapsed;f.elapsed+=delta;
                if(f.config.mode==DeliveryMode.Sector)
                {
                    var origin=f.config.followSource?f.source.transform.position:f.origin;
                    var targets = Nearby(f, origin, f.config.range, true, out var contacts);
                    Resolve(f, origin, targets, f.direction, contacts, null);
                    if(f.elapsed>=f.config.activeDuration)Remove(i);
                    continue;
                }
                var t=Mathf.Clamp01(f.elapsed/f.config.flightTime);
                var travel=Mathf.Max(0,Mathf.Min(f.config.range,f.elapsed*f.config.speed)-Mathf.Min(f.config.range,previousElapsed*f.config.speed));
                var next=f.config.mode==DeliveryMode.Projectile?f.position+f.direction*travel:
                    Vector3.Lerp(f.origin+Vector3.up*.8f,f.landing,t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*f.config.arcHeight);
                // RayLandingArc明确沿源组合合同：先验直线落点，弧线仅表现；Parabolic才逐段实际碰撞。
                var hit=f.config.mode==DeliveryMode.RayLandingArc?(RaycastHit?)null:FirstContact(f,f.position,next);
                var expired=f.config.mode==DeliveryMode.Projectile?f.elapsed*f.config.speed>=f.config.range:t>=1;
                if(hit.HasValue||expired)
                {
                    var point=hit.HasValue && hit.Value.distance<=.0001f
                        ? hit.Value.collider.bounds.ClosestPoint(f.position) : hit?.point??next;
                    Dictionary<IHitTarget, CombatContactPoint> contacts = null;
                    var targets=f.config.mode==DeliveryMode.Projectile?ProjectileTargets(f,hit?.collider):
                        Clear(f.origin,f.landing)?Nearby(f,point,f.config.radius,false,out contacts):Array.Empty<IHitTarget>();
                    ReportPosition(f,point);
                    Resolve(f, point, targets, next - f.position, contacts, hit);
                    // 网关同步回调可撤销本动作。按对象读取当前索引，避免删到邻近投递。
                    int completed=flights.IndexOf(f);
                    if(completed>=0)Remove(completed);
                    continue;
                }
                f.position=next;if(f.visual!=null)f.visual.transform.position=next;
                ReportPosition(f,next);
            }
        }
        private static IHitTarget[] Target(Collider collider)
        {
            var target=CombatSpatialTarget.Find(collider);
            return CombatTargetValidity.IsAvailable(target)?new[]{target}:Array.Empty<IHitTarget>();
        }
        private static Collider InitialBlocker(Flight flight)
        {
            var overlaps=Physics.OverlapSphere(flight.position,flight.config.collisionRadius,
                flight.config.collisionLayers,QueryTriggerInteraction.Ignore);
            return overlaps.Where(collider => !IsSource(flight,collider))
                .OrderBy(collider => (collider.bounds.ClosestPoint(flight.position)-flight.position).sqrMagnitude)
                .ThenBy(collider => (collider.bounds.center-flight.position).sqrMagnitude)
                .ThenBy(collider => collider.GetInstanceID()).FirstOrDefault();
        }
        private static bool IsSource(Flight flight,Collider collider)
            => collider.transform.IsChildOf(flight.source.transform) ||
                ReferenceEquals(CombatSpatialTarget.Find(collider),flight.source);

        private static IHitTarget[] ProjectileTargets(Flight flight,Collider collider)
        {
            if(!flight.config.stopAtFirstContact)return Target(collider);
            var target=CombatSpatialTarget.Find(collider);
            return CombatTargetValidity.IsAvailable(target) && !(target is ICombatProjectileContact policy && !policy.AllowsDirectProjectile) &&
                CombatContactGateway.AllowsTargetFaction(flight.release,flight.source,target)
                ? new[]{target} : Array.Empty<IHitTarget>();
        }
        private static void ReportPosition(Flight flight,Vector3 position)
        {
            var observe=flight.flightPosition;
            if(observe==null)return;
            try { observe(position); }
            catch(Exception error) { Debug.LogWarning("CombatDeliveryQueue飞行表现观察者异常："+error); }
        }
        private static RaycastHit? FirstContact(Flight f,Vector3 a,Vector3 b)
        {
            var delta=b-a;if(delta.sqrMagnitude<.000001f)return null;
            var hits = Physics.SphereCastAll(a,f.config.collisionRadius,delta.normalized,delta.magnitude,f.config.collisionLayers,QueryTriggerInteraction.Ignore);
            var trace = CombatTraceProbe.For(f.source, true);
            trace?.Query(f.release, "SphereCastAll", a, b, delta.normalized, f.config.collisionRadius, 0, f.config.collisionLayers, hits.Length, "Ignore", f.config.activeDuration, f.elapsed);
            foreach(var hit in hits.OrderBy(h=>h.distance))
            {
                if(f.config.mode==DeliveryMode.Projectile && f.config.stopAtFirstContact)
                {
                    if(IsSource(f,hit.collider))continue;
                    return hit;
                }
                var target=CombatSpatialTarget.Find(hit.collider);
                if(ReferenceEquals(target,f.source)||target!=null&&(!CombatTargetValidity.IsAvailable(target)||target is ICombatTarget combat&&combat.Faction==f.source.Faction))
                {
                    trace?.Note("判定","投射候选已排除","来源自身、无效身份或同阵营 · "+hit.collider.name, f.release.ActionId,f.parent,f.release.EmissionIndex);
                    continue;
                }
                if((f.config.mode==DeliveryMode.Ray||f.config.mode==DeliveryMode.Projectile)&&target is ICombatProjectileContact policy&&!policy.AllowsDirectProjectile)
                {
                    trace?.Note("判定","投射候选已排除","目标禁止直接投射物",f.release.ActionId,f.parent,f.release.EmissionIndex);
                    continue;
                }
                return hit;
            }
            return null;
        }
        private static IHitTarget[] Nearby(Flight f, Vector3 point, float radius, bool sector,
            out Dictionary<IHitTarget, CombatContactPoint> contacts)
        {
            var center = point + Vector3.up * .8f;
            var overlaps = Physics.OverlapSphere(center, radius, f.config.collisionLayers, QueryTriggerInteraction.Ignore);
            var trace = CombatTraceProbe.For(f.source, true);
            trace?.Query(f.release, "OverlapSphere", center, center, f.direction, radius, sector ? f.config.arc : 360,
                f.config.collisionLayers, overlaps.Length, "Ignore", f.config.activeDuration, f.elapsed, sector ? point : (Vector3?)null);
            return CombatSpatialTarget.OverlapContacts(overlaps, center, target =>
            {
                var reason = "";
                if (ReferenceEquals(target, f.source)) reason = "来源自身";
                else
                {
                    var position = CombatSpatialTarget.Position(target);
                    if (sector && !CombatContactGeometry.ContainsSector(position - point, f.direction, f.config.arc, f.Geometry.Plane))
                        reason = "角度外";
                    else if (!Clear(point, position)) reason = "视线阻挡";
                }
                trace?.Note("判定","空间候选",reason.Length == 0 ? "通过" : reason, f.release.ActionId, f.parent, f.release.EmissionIndex, target.Health.Id);
                return reason.Length == 0;
            }, out contacts, f.Geometry.Plane);
        }
        public static bool Clear(Vector3 a,Vector3 b)
        {
            a+=Vector3.up*.8f;b+=Vector3.up*.8f;var d=b-a;if(d.sqrMagnitude<.00001f)return true;
            return !Physics.RaycastAll(a,d.normalized,d.magnitude,~0,QueryTriggerInteraction.Ignore).Any(h=>CombatSpatialTarget.Find(h.collider)==null);
        }
        private void Resolve(Flight f, Vector3 point, IHitTarget[] targets, Vector3 arrival,
            Dictionary<IHitTarget, CombatContactPoint> contacts, RaycastHit? physicsHit, Collider overlap=null)
        {
            var geometry = f.Geometry.WithArrival(arrival);
            CombatContactPoint? cast = null;
            if (physicsHit.HasValue)
            {
                var hit = physicsHit.Value;
                // SphereCast从重叠内开始时，point可能是零向量，保留估计质量而不把特效送去世界原点。
                cast = hit.distance > .0001f
                    ? new CombatContactPoint(hit.point, CombatContactPointQuality.PhysicsCast, hit.normal,
                        null, Ossastra.Audio.SideViewFeedbackRuntime.MaterialOf(hit.collider, false))
                    : new CombatContactPoint(hit.collider.bounds.ClosestPoint(f.position), CombatContactPointQuality.EstimatedOverlap,
                        null, null, Ossastra.Audio.SideViewFeedbackRuntime.MaterialOf(hit.collider, false));
            }
            if(overlap!=null)
                cast=new CombatContactPoint(point,CombatContactPointQuality.EstimatedOverlap,null,null,
                    Ossastra.Audio.SideViewFeedbackRuntime.MaterialOf(overlap,false));
            var hitTarget = CombatSpatialTarget.Find(physicsHit?.collider??overlap);
            CombatContactPoint? ReadContact(IHitTarget target)
            {
                if (cast.HasValue && ReferenceEquals(target, hitTarget)) return cast;
                return contacts != null && contacts.TryGetValue(target, out var contact) ? contact : (CombatContactPoint?)null;
            }
            var rejection=f.config.mode==DeliveryMode.Projectile && f.config.stopAtFirstContact &&
                hitTarget!=null && targets.Length==0 ? RejectedProjectileContact : null;
            var receipts = CombatContactGateway.Resolve(f.release, f.source, targets, point,
                CombatContactCause.Direct, rejection, geometry, ReadContact, hitTarget == null ? cast : null, f.config.mode, f.channel);
            ResolvedCount++;
            f.resolved?.Invoke(receipts);
        }
        private void Remove(int i)
        {
            var f = flights[i];
            CombatTraceProbe.For(f.source,true)?.Note("判定","投递窗口已移除", f.config.mode + " · 已推进 " + f.elapsed + " s",
                f.release.ActionId, f.parent, f.release.EmissionIndex);
            if (f.visual != null) UnityEngine.Object.Destroy(f.visual);
            f.source?.Recovery?.ReleaseParent(f.parent);
            flights.RemoveAt(i);
            ForgetCompleted(f.parent);
        }
        public void Clear(){for(int i=flights.Count-1;i>=0;i--)Remove(i);emitted.Clear();activeParents.Clear();}
    }
}
