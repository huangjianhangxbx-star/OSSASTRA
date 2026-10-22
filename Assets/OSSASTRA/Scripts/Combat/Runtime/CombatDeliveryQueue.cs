/* 脚本：CombatDeliveryQueue
 * 功能：共享近战扇区、射线、飞行弹、抛物与合法落点组合投递，将接触交唯一Combat网关。
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
            public Vector3 origin,landing,position,direction;
            public float elapsed;
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
        public bool Emit(CombatVitals source,Guid parent,int index,DeliveryParameters config,int damage,float posture,Vector3 origin,Vector3 direction,Vector3 landing,Func<bool> valid,Action<IReadOnlyList<HitOutcome>> resolved,Sprite sprite=null,float scale=.25f,double reclaimBudget=.05,double reclaimRate=.5)
        {
            if(source==null||!source.IsAvailable||parent==Guid.Empty||!activeParents.Contains(parent)||index<0||damage<=0||config==null||valid==null||!valid()||!emitted.Add((parent,index)))return false;
            var flight=new Flight{source=source,parent=parent,config=config.Freeze(),release=config.mode==DeliveryMode.Sector&&config.activeDuration>0?
                AttackRelease.ForContactWindow(parent,index,source.Health.Id,damage,posture,reclaimBudget,reclaimRate):AttackRelease.ForEmission(parent,index,source.Health.Id,damage,posture,reclaimBudget,reclaimRate),origin=origin,landing=landing,direction=direction.normalized,position=origin+Vector3.up*.8f,valid=valid,resolved=resolved};
            if(config.mode==DeliveryMode.Sector)
            {
                Resolve(flight,origin,Nearby(flight,origin,config.range,true));
                if(config.activeDuration>0){flights.Add(flight);source.Recovery?.RetainParent(parent);}
                return true;
            }
            if(config.mode==DeliveryMode.Ray)
            {
                var hit=FirstContact(flight,flight.position,flight.position+flight.direction*config.range);
                Resolve(flight,hit?.point??origin,hit.HasValue?Target(hit.Value.collider):Array.Empty<IHitTarget>());return true;
            }
            if(sprite!=null)
            {
                flight.visual=new GameObject("AbilityProjectile_"+parent.ToString("N")+"_"+index);
                var renderer=flight.visual.AddComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.sortingOrder=50;
                flight.visual.transform.localScale=Vector3.one*scale;if(Camera.main!=null)flight.visual.transform.rotation=Camera.main.transform.rotation;
                flight.visual.transform.position=flight.position;
            }
            source.Recovery?.RetainParent(parent);flights.Add(flight);return true;
        }
        public void Tick(float delta)
        {
            for(int i=flights.Count-1;i>=0;i--)
            {
                var f=flights[i];if(f.source==null||!f.valid()){Remove(i);continue;}
                f.elapsed+=delta;
                if(f.config.mode==DeliveryMode.Sector)
                {
                    var origin=f.config.followSource?f.source.transform.position:f.origin;
                    Resolve(f,origin,Nearby(f,origin,f.config.range,true));
                    if(f.elapsed>=f.config.activeDuration)Remove(i);
                    continue;
                }
                var t=Mathf.Clamp01(f.elapsed/f.config.flightTime);
                var next=f.config.mode==DeliveryMode.Projectile?f.position+f.direction*f.config.speed*delta:
                    Vector3.Lerp(f.origin+Vector3.up*.8f,f.landing,t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*f.config.arcHeight);
                // RayLandingArc明确沿源组合合同：先验直线落点，弧线仅表现；Parabolic才逐段实际碰撞。
                var hit=f.config.mode==DeliveryMode.RayLandingArc?(RaycastHit?)null:FirstContact(f,f.position,next);
                var expired=f.config.mode==DeliveryMode.Projectile?f.elapsed*f.config.speed>=f.config.range:t>=1;
                if(hit.HasValue||expired)
                {
                    var point=hit?.point??next;
                    var targets=f.config.mode==DeliveryMode.Projectile?(hit.HasValue?Target(hit.Value.collider):Array.Empty<IHitTarget>()):
                        Clear(f.origin,f.landing)?Nearby(f,point,f.config.radius,false):Array.Empty<IHitTarget>();
                    Resolve(f,point,targets);Remove(i);continue;
                }
                f.position=next;if(f.visual!=null)f.visual.transform.position=next;
            }
        }
        private static IHitTarget[] Target(Collider collider)
        {
            var target=collider==null?null:collider.GetComponentInParent<CombatVitals>();
            return target!=null&&target.IsAvailable?new IHitTarget[]{target}:Array.Empty<IHitTarget>();
        }
        private static RaycastHit? FirstContact(Flight f,Vector3 a,Vector3 b)
        {
            var delta=b-a;if(delta.sqrMagnitude<.000001f)return null;
            foreach(var hit in Physics.SphereCastAll(a,f.config.collisionRadius,delta.normalized,delta.magnitude,f.config.collisionLayers,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))
            {var target=hit.collider.GetComponentInParent<CombatVitals>();if(target==f.source||target!=null&&!target.IsAvailable)continue;return hit;}
            return null;
        }
        private static IHitTarget[] Nearby(Flight f,Vector3 point,float radius,bool sector)
        {
            return Physics.OverlapSphere(point+Vector3.up*.8f,radius,f.config.collisionLayers,QueryTriggerInteraction.Ignore)
                .Select(c=>c.GetComponentInParent<CombatVitals>()).Where(v=>v!=null&&v.IsAvailable&&v!=f.source).Distinct()
                .Where(v=>Clear(point,v.transform.position)&&(!sector||InsideArc(v.transform.position-point,f.direction,f.config.arc))).Cast<IHitTarget>().ToArray();
        }
        private static bool InsideArc(Vector3 delta,Vector3 direction,float arc)
        {delta.y=0;direction.y=0;return delta.sqrMagnitude<.0001f||arc>=360||Vector3.Angle(delta,direction)<=arc*.5f;}
        public static bool Clear(Vector3 a,Vector3 b)
        {
            a+=Vector3.up*.8f;b+=Vector3.up*.8f;var d=b-a;if(d.sqrMagnitude<.00001f)return true;
            return !Physics.RaycastAll(a,d.normalized,d.magnitude,~0,QueryTriggerInteraction.Ignore).Any(h=>h.collider.GetComponentInParent<CombatVitals>()==null);
        }
        private void Resolve(Flight f,Vector3 point,IHitTarget[] targets)
        {var receipts=CombatContactGateway.Resolve(f.release,f.source,targets,point);ResolvedCount++;f.resolved?.Invoke(receipts);}
        private void Remove(int i)
        {var parent=flights[i].parent;if(flights[i].visual!=null)UnityEngine.Object.Destroy(flights[i].visual);flights[i].source?.Recovery?.ReleaseParent(parent);flights.RemoveAt(i);ForgetCompleted(parent);}
        public void Clear(){for(int i=flights.Count-1;i>=0;i--)Remove(i);emitted.Clear();activeParents.Clear();}
    }
}
