/* 脚本：CharacterOffhand
 * 功能：本体副手炸弹请求、费用、冻结来源与演示投掷体生命周期。
 * 边界：Root显式推进；主手Runtime完全独立；伤害只进入CombatContactGateway。
 * 协作：Equipment持有实例与耐久，Stamina付款；新配置冻结实际炸弹sprite；没有定义的旧场保留几何演示兼容。
 */
using System;
using System.Linq;
using UnityEngine;
using Ossastra.CharacterCore;
using Ossastra.Combat;
namespace Ossastra.Characters
{
    [DisallowMultipleComponent]
    public sealed class CharacterOffhand:MonoBehaviour,ICharacterOffhandStatus
    {
        private CharacterRoot owner;
        private CharacterEquipment gear;
        private CharacterStamina stamina;
        private OffhandActionState state;
        private Vector3 origin,landing;
        private AttackRelease flight;
        private GameObject visual;
        private Sprite projectileSprite;
        private float projectileScale;
        private float flightTime;
        private int flightGeneration;
        public bool IsAccepting=>state!=null&&state.IsAccepting;
        public Vector3 LastLanding=>landing;
        public double CooldownRemaining=>state==null?0:state.CooldownRemaining;
        public bool InFlight=>flight!=null;
        public int AcceptedCount {get;private set;}
        public int ReleasedCount {get;private set;}
        public int LandedCount {get;private set;}
        public string LastResult {get;private set;}="未投掷";
        public string FrozenEquipmentId {get;private set;}
        public Guid LastActionId {get;private set;}
        public void Initialize(CharacterRoot root,CharacterEquipment equipment,CharacterStamina resource)
        {owner=root;gear=equipment;stamina=resource;state=new OffhandActionState(root.ActorId);}
        public bool Request(Vector3? point,bool controlled)
        {
            if(!isActiveAndEnabled||owner==null||!owner.CanSimulateIntent||!controlled||gear==null||!gear.isActiveAndEnabled||gear.State.LeftKind!=OffhandKind.Bomb||gear.State.Durability<=0)
                return Reject("副手装备/控制资格不足");
            if(IsAccepting||InFlight||CooldownRemaining>0)return Reject("副手冷却中");
            if(point==null)return Reject("鼠标不是世界落点");
            var p=point.Value;var delta=p-owner.transform.position;delta.y=0;
            if(float.IsNaN(p.x)||float.IsInfinity(p.x)||float.IsNaN(p.z)||float.IsInfinity(p.z)||delta.magnitude>4.5f)return Reject("超出4.5范围");
            // 脚下平面投影之后必须再证实地面；不把墙后或无地面的点自动修正为合法点。
            var floor=Physics.RaycastAll(p+Vector3.up*.4f,Vector3.down,.8f,~0,QueryTriggerInteraction.Ignore)
                .Where(h=>h.normal.y>.7f&&h.collider.GetComponentInParent<CombatVitals>()==null).OrderBy(h=>h.distance).ToArray();
            if(floor.Length==0)return Reject("落点无可用地面");
            p=floor[0].point;
            if(!ClearPath(owner.transform.position,p))return Reject("落点被墙遮挡");
            if(stamina==null||!stamina.CanSpend(20))return Reject("体力不足");
            var request=owner.Actions.CreateRequest(CharacterActionKind.Offhand,CharacterActionSource.Player);
            var id=request.RequestId;
            if(!owner.Actions.TryCommit(request,20,()=>state.TryAccept(id,gear.State.LeftInstanceId,owner.LifeGeneration)?id:Guid.Empty,out var reason))return Reject(reason);
            projectileSprite=gear.LeftDefinition==null?null:gear.LeftDefinition.worldSprite;projectileScale=gear.LeftDefinition==null?.35f:gear.LeftDefinition.worldScale;
            origin=owner.transform.position;landing=p;FrozenEquipmentId=state.EquipmentId;LastActionId=id;AcceptedCount++;LastResult="Accepted 炸弹 SAMPLE";return true;
        }
        private bool Reject(string reason){LastResult="拒绝："+reason;return false;}
        public void Tick(float delta,bool controlled)
        {
            if(owner==null||state==null)return;
            if(!isActiveAndEnabled||!owner.IsAlive){CancelAll();return;}
            if(owner.IsSimulationSuspended)return;
            if(!owner.CanSimulateIntent||!controlled)state.CancelPending();
            var released=state.Advance(delta,controlled,false);
            if(released!=null)
            {
                if(gear.State.LeftInstanceId!=state.EquipmentId||owner.LifeGeneration!=state.LifeGeneration){LastResult="释放身份失效";return;}
                gear.State.Wear(released.ActionId);flight=released;flightGeneration=owner.LifeGeneration;flightTime=0;ReleasedCount++;
                if(projectileSprite!=null)
                {
                    visual=new GameObject("OffhandProjectile_"+released.ActionId.ToString("N"));
                    var sprite=visual.AddComponent<SpriteRenderer>();sprite.sprite=projectileSprite;sprite.sortingOrder=50;
                    visual.transform.localScale=Vector3.one*projectileScale;
                    if(Camera.main!=null)visual.transform.rotation=Camera.main.transform.rotation;
                }
                else
                {
                    // 无新定义的旧验证场保持原演示图形，不修改其资源或玩法参数。
                    visual=GameObject.CreatePrimitive(PrimitiveType.Sphere);visual.name="SAMPLE_Bomb_"+released.ActionId.ToString("N");visual.transform.localScale=Vector3.one*.16f;
                    var collider=visual.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
                }
                visual.transform.position=origin+Vector3.up*.3f;
                LastResult="Released #"+released.ActionId.ToString("N");return;
            }
            if(flight==null)return;
            if(owner.LifeGeneration!=flightGeneration){CancelAll();return;}
            flightTime+=delta;var t=Mathf.Clamp01(flightTime/.45f);
            if(visual!=null)visual.transform.position=Vector3.Lerp(origin,landing,t)+Vector3.up*(.15f+Mathf.Sin(t*Mathf.PI));
            if(t<1)return;
            // 再查发射至落点及爆点至目标的直线遮挡。并非连续扫掠式物理弹道。
            var candidates=ClearPath(origin,landing)?Physics.OverlapSphere(landing,1.25f,~0,QueryTriggerInteraction.Ignore)
                .Select(c=>c.GetComponentInParent<CombatVitals>()).Where(v=>v!=null&&ClearPath(landing,v.transform.position)).Distinct().Cast<IHitTarget>().ToArray():Array.Empty<IHitTarget>();
            var receipts=CombatContactGateway.Resolve(flight,owner.Vitals,candidates,landing);
            LandedCount++;LastResult="落地 #"+flight.ActionId.ToString("N")+" 接触 "+receipts.Count;ClearFlight();
        }
        private static bool ClearPath(Vector3 a,Vector3 b)
        {
            a+=Vector3.up*.25f;b+=Vector3.up*.25f;var d=b-a;
            foreach(var hit in Physics.RaycastAll(a,d,d.magnitude,~0,QueryTriggerInteraction.Ignore))
                if(hit.collider.GetComponentInParent<CombatVitals>()==null)return false;
            return true;
        }
        public void CancelPending(){state?.CancelPending();}
        public void CancelAll(){state?.CancelPending();ClearFlight();}
        private void ClearFlight(){flight=null;if(visual!=null)Destroy(visual);visual=null;}
        private void OnDisable(){CancelAll();}
    }
}
