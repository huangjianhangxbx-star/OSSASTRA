/* 脚本：CombatIceWorld
 * 功能：一个装备来源的冰路、接触周期、延迟冰柱和碎冰范围消费者。
 * 边界：无自主Update/角色根/装备费用；调用方以模拟delta推进并提供生命代际与来源持有许可。
 * 协作：脉冲/碎冰都经AttackRelease+CombatContactGateway；减速只提交CombatStatusEffects，不移动目标。
 * 来源：c21d4fc trial-ice.ts与ability-trial.ts；几何线条为技术表现，不冒称正式冰特效。
 */
using System;
using System.Collections.Generic;
using System.Linq;
using Ossastra.CharacterCore;
using UnityEngine;
namespace Ossastra.Combat
{
 public sealed class CombatIceWorld:IDisposable
 {
  private sealed class Patch {public Guid parent;public Vector3 position;public double expires,scanAt;public CombatVitals[] members=Array.Empty<CombatVitals>();public GameObject visual;}
  private sealed class PendingColumn {public Guid parent;public Vector3 from,position;public double due;public Func<bool> held;}
  private sealed class Trail {public Guid parent;public Vector3 last;public double expires;public float remainder;public Func<bool> active;}
  private sealed class ColumnLease {public Guid parent;public CombatIceColumn column;public bool burstQueued,released;}
  private readonly CombatVitals owner;
  private readonly Func<bool> lifetimeValid;
  private readonly List<Patch> patches=new List<Patch>();
  private readonly List<PendingColumn> pending=new List<PendingColumn>();
  private readonly List<ColumnLease> columns=new List<ColumnLease>();
  private readonly List<(ColumnLease lease,Vector3 position)> bursts=new List<(ColumnLease,Vector3)>();
  private readonly Dictionary<string,Guid> contactIds=new Dictionary<string,Guid>();
  private readonly Dictionary<string,CombatVitals> contacts=new Dictionary<string,CombatVitals>();
  private readonly AreaContactClock contactClock=new AreaContactClock(.5);
  private Trail trail;
  private Material material;
  private double time;
  private int emission;
  public int PatchCount=>patches.Count;
  public int ColumnCount=>columns.Count(c=>c.column!=null&&c.column.IsAvailable);
  public int PendingCount=>pending.Count;
  public int PulseCount {get;private set;}
  public int BurstCount {get;private set;}
  public bool IsOnIce(CombatVitals target)=>target!=null&&contactIds.ContainsKey(target.Health.Id);
  public CombatIceWorld(CombatVitals owner,Func<bool> lifetimeValid)
  {this.owner=owner??throw new ArgumentNullException(nameof(owner));this.lifetimeValid=lifetimeValid??throw new ArgumentNullException(nameof(lifetimeValid));}
  public void StartTrail(Guid parent,Func<bool> stillActive)
  {
   if(!Valid()||parent==Guid.Empty||stillActive==null||!stillActive())return;
   trail=new Trail{parent=parent,last=owner.transform.position,expires=time+.2,active=stillActive};SpawnPatch(parent,trail.last);
  }
  public bool SpawnPatch(Guid parent,Vector3 position)
  {
   if(!Valid()||parent==Guid.Empty||patches.Count>=512||!Fits(position))return false;
   patches.Add(new Patch{parent=parent,position=position,expires=time+8,scanAt=time+.1,visual=Visual("IcePatch",position,1,false)});return true;
  }
  public bool ScheduleColumn(Guid parent,Vector3 from,Vector3 position,Func<bool> sourceHeld)
  {
   if(!Valid()||parent==Guid.Empty||sourceHeld==null||!sourceHeld()||pending.Count>=64)return false;
   owner.Recovery?.RetainParent(parent);pending.Add(new PendingColumn{parent=parent,from=from,position=position,due=time+.2,held=sourceHeld});return true;
  }
  private bool Valid()=>owner!=null&&owner.IsAvailable&&lifetimeValid();
  public void Tick(float delta)
  {
   if(float.IsNaN(delta)||float.IsInfinity(delta)||delta<0)throw new ArgumentException("Invalid ice simulation delta");
   if(!Valid()){Clear();return;}if(delta==0)return;time+=delta;
   TickTrail();
   for(int i=pending.Count-1;i>=0;i--)
   {
    var p=pending[i];if(!p.held()){owner.Recovery?.ReleaseParent(p.parent);pending.RemoveAt(i);continue;}if(time+1e-9<p.due)continue;pending.RemoveAt(i);
    if(!Fits(p.position)||!CombatDeliveryQueue.Clear(p.from,p.position)){owner.Recovery?.ReleaseParent(p.parent);continue;}
    var obj=Visual("IceColumn",p.position,.35f,true);var collider=obj.AddComponent<CapsuleCollider>();collider.center=Vector3.up*.4f;collider.radius=.35f;collider.height=.8f;
    var column=obj.AddComponent<CombatIceColumn>();var lease=new ColumnLease{parent=p.parent,column=column};
    column.Initialize(p.parent,time,c=>{lease.burstQueued=true;bursts.Add((lease,c.WorldPosition));});columns.Add(lease);
   }
   foreach(var lease in columns.ToArray())if(lease.column!=null)lease.column.TickLifetime(time);
   for(int i=columns.Count-1;i>=0;i--)if(columns[i].column==null||!columns[i].column.IsAvailable){if(!columns[i].burstQueued)Release(columns[i]);columns.RemoveAt(i);}
   for(int i=patches.Count-1;i>=0;i--)
   {
    var patch=patches[i];if(time>=patch.expires){UnityEngine.Object.Destroy(patch.visual);patches.RemoveAt(i);continue;}
    if(time+1e-9<patch.scanAt)continue;patch.scanAt+=.1;
    // OverlapSphere本身包含目标Collider半径，等价源中“1 + 目标半径”的覆盖。
    patch.members=Physics.OverlapSphere(patch.position+Vector3.up*.4f,1,~0,QueryTriggerInteraction.Ignore)
       .Select(c=>c.GetComponentInParent<CombatVitals>()).Where(v=>v!=null&&v.IsAvailable).Distinct().ToArray();
   }
   var present=patches.SelectMany(p=>p.members).Where(v=>v!=null&&v.IsAvailable).Distinct().ToArray();
   var ids=present.Select(v=>v.Health.Id).ToArray();contactClock.Retain(ids);
   foreach(var absent in contactIds.Keys.Where(id=>!ids.Contains(id)).ToArray())RemoveContact(absent);
   foreach(var target in present)
   {
    if(!contactIds.TryGetValue(target.Health.Id,out var effect)){effect=Guid.NewGuid();contactIds.Add(target.Health.Id,effect);contacts.Add(target.Health.Id,target);}
    if(target.Faction==owner.Faction)continue;
    target.GetComponent<CombatStatusEffects>()?.ApplySlow(effect,.35f,.15f);
    if(!contactClock.TryPulse(target.Health.Id,time))continue;
    var patch=patches.First(p=>p.members.Contains(target));
    var release=AttackRelease.ForEmission(patch.parent,emission++,owner.Health.Id,15,0,0,0);
    CombatContactGateway.Resolve(release,owner,new IHitTarget[]{target},patch.position,CombatContactCause.Periodic,"ice.pulse");PulseCount++;
   }
   // 等原命中批次返回后在世界推进中爆炸；不会在柱OnHit里递归进入其他伤害批次。
   foreach(var burst in bursts.ToArray())
   {
    var targets=Physics.OverlapSphere(burst.position+Vector3.up*.8f,2,~0,QueryTriggerInteraction.Ignore)
       .Select(c=>c.GetComponentInParent<CombatVitals>()).Where(v=>v!=null&&v.IsAvailable&&CombatDeliveryQueue.Clear(burst.position,v.WorldPosition)).Distinct().Cast<IHitTarget>().ToArray();
    var release=AttackRelease.ForEmission(burst.lease.parent,emission++,owner.Health.Id,80,0,.05,.5);
    CombatContactGateway.Resolve(release,owner,targets,burst.position,CombatContactCause.Secondary,"ice.shatter");BurstCount++;Release(burst.lease);
   }
   bursts.Clear();
  }
  private void TickTrail()
  {
   if(trail==null)return;if(time>=trail.expires||!trail.active()){trail=null;return;}
   var delta=owner.WorldPosition-trail.last;delta.y=0;var distance=delta.magnitude;if(distance<.000001f)return;
   // 保留原型按采样距离累积的行为（含其帧依赖），每次最多生成64块；不私自改成另一种轨迹采样规则。
   var total=distance+trail.remainder;var count=Mathf.FloorToInt(total);if(count==0){trail.remainder=total;return;}
   var origin=trail.last;for(int i=1;i<=Math.Min(64,count);i++){var position=origin+delta/distance*(i-trail.remainder);SpawnPatch(trail.parent,position);trail.last=position;}trail.remainder=total-count;
  }
  private static bool Fits(Vector3 position)=>!Physics.OverlapSphere(position+Vector3.up*.3f,.08f,~0,QueryTriggerInteraction.Ignore).Any(c=>CombatSpatialTarget.Find(c)==null);
  private GameObject Visual(string name,Vector3 position,float radius,bool pillar)
  {
   var obj=new GameObject(name);obj.transform.position=position;
   if(material==null){var shader=Shader.Find("Sprites/Default");if(shader==null)return obj;material=new Material(shader){name="OwnedIceTechnicalMaterial"};}
   var line=obj.AddComponent<LineRenderer>();line.sharedMaterial=material;line.useWorldSpace=false;line.widthMultiplier=.035f;line.loop=true;line.startColor=line.endColor=new Color(.3f,.85f,1,.75f);line.sortingOrder=25;
   if(pillar){line.positionCount=4;line.SetPositions(new[]{new Vector3(-radius,.08f,0),new Vector3(0,1,0),new Vector3(radius,.08f,0),new Vector3(0,.35f,radius)});}
   else{line.positionCount=24;for(int i=0;i<24;i++){var angle=i*Mathf.PI/12;line.SetPosition(i,new Vector3(Mathf.Cos(angle)*radius,.025f,Mathf.Sin(angle)*radius));}}
   return obj;
  }
  public void Clear()
  {
   trail=null;foreach(var p in pending)owner?.Recovery?.ReleaseParent(p.parent);pending.Clear();foreach(var burst in bursts)Release(burst.lease);bursts.Clear();
   foreach(var id in contactIds.Keys.ToArray())RemoveContact(id);contactClock.Clear();
   foreach(var patch in patches)if(patch.visual!=null)UnityEngine.Object.Destroy(patch.visual);patches.Clear();
   foreach(var lease in columns){if(lease.column!=null)lease.column.Expire();Release(lease);}columns.Clear();
  }
  private void RemoveContact(string id){if(contacts.TryGetValue(id,out var target)&&target!=null)target.GetComponent<CombatStatusEffects>()?.Remove(contactIds[id]);contacts.Remove(id);contactIds.Remove(id);}
  private void Release(ColumnLease lease){if(lease.released)return;lease.released=true;owner?.Recovery?.ReleaseParent(lease.parent);}
  public void Dispose(){Clear();if(material!=null)UnityEngine.Object.Destroy(material);material=null;}
 }
}
