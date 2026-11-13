/* 脚本：CombatDeliveryQueue
 * 功能：共享近战扇区、射线、飞行弹、抛物和落点投递，向唯一网关提交接触和释放空间事实。
 * 边界：不认识玩家根/装备/库存、不付款；每释放索引唯一，伤害和姿态仍由AttackRelease结算。
 * 协作：调用者注入来源Vitals及生命周期许可并用原模拟delta推进；连续飞行按每段SphereCast检测。
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
            public GameObject visual;
            public Guid parent;
        }
        private readonly List<Flight> flights=new List<Flight>();
        private readonly HashSet<(Guid,int)> emitted=new HashSet<(Guid,int)>();
        private readonly HashSet<Guid> activeParents=new HashSet<Guid>();
        public void RegisterParent(Guid parent){if(parent!=Guid.Empty)activeParents.Add(parent);}
        public void CompleteParent(Guid parent){activeParents.Remove(parent);ForgetCompleted(parent);}
        private void ForgetCompleted(Guid parent)
        {if(!activeParents.Contains(parent)&&!flights.Any(f=>f.parent==parent))emitted.RemoveWhere(key=>key.Item1==parent);}
        public int InFlightCount=>flights.Count;
        public int ResolvedCount {get;private set;}
        public bool Emit(CombatVitals source,Guid parent,int index,DeliveryParameters config,int damage,float posture,Vector3 origin,Vector3 direction,Vector3 landing,Func<bool> valid,Action<IReadOnlyList<HitOutcome>> resolved,Sprite sprite=null,float scale=.25f,double reclaimBudget=.05,double reclaimRate=.5,int basicAttackStage=0)
        {
            if(source==null||!source.IsAvailable||parent==Guid.Empty||!activeParents.Contains(parent)||index<0||damage<=0||config==null||valid==null||!valid()||!emitted.Add((parent,index)))return false;
            var plane = CombatSpatialTarget.PlaneFor(source);
            var travelDirection = plane == CombatPlane.SideXY ? CombatContactGeometry.Project(direction, plane) : direction;
            var flight=new Flight{source=source,parent=parent,config=config.Freeze(),
                Geometry=new CombatContactGeometry(plane,origin,travelDirection,travelDirection,basicAttackStage),release=config.mode==DeliveryMode.Sector&&config.activeDuration>0?
                AttackRelease.ForContactWindow(parent,index,source.Health.Id,damage,posture,reclaimBudget,reclaimRate):AttackRelease.ForEmission(parent,index,source.Health.Id,damage,posture,reclaimBudget,reclaimRate),origin=origin,landing=landing,direction=travelDirection.normalized,position=origin+Vector3.up*.8f,valid=valid,resolved=resolved,emittedFrame=Time.frameCount};
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
        public void Tick(float delta)
        {
            for(int i=flights.Count-1;i>=0;i--)
            {
                var f=flights[i];if(f.source==null||!f.valid()){Remove(i);continue;}
                if(f.emittedFrame==Time.frameCount)continue;
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
                    var point=hit?.point??next;
                    Dictionary<IHitTarget, CombatContactPoint> contacts = null;
                    var targets=f.config.mode==DeliveryMode.Projectile?(hit.HasValue?Target(hit.Value.collider):Array.Empty<IHitTarget>()):
                        Clear(f.origin,f.landing)?Nearby(f,point,f.config.radius,false,out contacts):Array.Empty<IHitTarget>();
                    Resolve(f, point, targets, next - f.position, contacts, hit);
                    Remove(i);continue;
                }
                f.position=next;if(f.visual!=null)f.visual.transform.position=next;
            }
        }
        private static IHitTarget[] Target(Collider collider)
        {
            var target=CombatSpatialTarget.Find(collider);
            return CombatTargetValidity.IsAvailable(target)?new[]{target}:Array.Empty<IHitTarget>();
        }
        private static RaycastHit? FirstContact(Flight f,Vector3 a,Vector3 b)
        {
            var delta=b-a;if(delta.sqrMagnitude<.000001f)return null;
            foreach(var hit in Physics.SphereCastAll(a,f.config.collisionRadius,delta.normalized,delta.magnitude,f.config.collisionLayers,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))
            {
                var target=CombatSpatialTarget.Find(hit.collider);
                if(ReferenceEquals(target,f.source)||target!=null&&(!CombatTargetValidity.IsAvailable(target)||target is ICombatTarget combat&&combat.Faction==f.source.Faction))continue;
                if((f.config.mode==DeliveryMode.Ray||f.config.mode==DeliveryMode.Projectile)&&target is ICombatProjectileContact policy&&!policy.AllowsDirectProjectile)continue;
                return hit;
            }
            return null;
        }
        private static IHitTarget[] Nearby(Flight f, Vector3 point, float radius, bool sector,
            out Dictionary<IHitTarget, CombatContactPoint> contacts)
        {
            var center = point + Vector3.up * .8f;
            var overlaps = Physics.OverlapSphere(center, radius, f.config.collisionLayers, QueryTriggerInteraction.Ignore);
            return CombatSpatialTarget.OverlapContacts(overlaps, center, target =>
            {
                if (ReferenceEquals(target, f.source)) return false;
                var position = CombatSpatialTarget.Position(target);
                return (!sector || CombatContactGeometry.ContainsSector(position - point, f.direction, f.config.arc, f.Geometry.Plane)) &&
                    Clear(point, position);
            }, out contacts, f.Geometry.Plane);
        }
        public static bool Clear(Vector3 a,Vector3 b)
        {
            a+=Vector3.up*.8f;b+=Vector3.up*.8f;var d=b-a;if(d.sqrMagnitude<.00001f)return true;
            return !Physics.RaycastAll(a,d.normalized,d.magnitude,~0,QueryTriggerInteraction.Ignore).Any(h=>CombatSpatialTarget.Find(h.collider)==null);
        }
        private void Resolve(Flight f, Vector3 point, IHitTarget[] targets, Vector3 arrival,
            Dictionary<IHitTarget, CombatContactPoint> contacts, RaycastHit? physicsHit)
        {
            var geometry = f.Geometry.WithArrival(arrival);
            CombatContactPoint? cast = null;
            if (physicsHit.HasValue)
            {
                var hit = physicsHit.Value;
                // SphereCast从重叠内开始时，point可能是零向量，保留估计质量而不把特效送去世界原点。
                cast = hit.distance > .0001f
                    ? new CombatContactPoint(hit.point, CombatContactPointQuality.PhysicsCast, hit.normal)
                    : new CombatContactPoint(hit.collider.bounds.ClosestPoint(f.position), CombatContactPointQuality.EstimatedOverlap);
            }
            var hitTarget = physicsHit.HasValue ? CombatSpatialTarget.Find(physicsHit.Value.collider) : null;
            CombatContactPoint? ReadContact(IHitTarget target)
            {
                if (cast.HasValue && ReferenceEquals(target, hitTarget)) return cast;
                return contacts != null && contacts.TryGetValue(target, out var contact) ? contact : (CombatContactPoint?)null;
            }
            var receipts = CombatContactGateway.Resolve(f.release, f.source, targets, point,
                CombatContactCause.Direct, null, geometry, ReadContact, hitTarget == null ? cast : null);
            ResolvedCount++;
            f.resolved?.Invoke(receipts);
        }
        private void Remove(int i)
        {var parent=flights[i].parent;if(flights[i].visual!=null)UnityEngine.Object.Destroy(flights[i].visual);flights[i].source?.Recovery?.ReleaseParent(parent);flights.RemoveAt(i);ForgetCompleted(parent);}
        public void Clear(){for(int i=flights.Count-1;i>=0;i--)Remove(i);emitted.Clear();activeParents.Clear();}
    }
}
