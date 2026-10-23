/* 脚本：CombatIceColumn
 * 功能：具备唯一4点Health的可受击冰柱；首个合法Applied接触提交碎冰事实。
 * 边界：没有Update/角色根/输入；寿命由CombatIceWorld推进，攻击必须经共享网关而不是靠近触发。
 * 协作：在结算前拒绝生成自己的执行；破碎回调只排队，不在网关调用栈重入AOE。
 */
using System;
using Ossastra.CharacterCore;
using UnityEngine;
namespace Ossastra.Combat
{
 public sealed class CombatIceColumn:MonoBehaviour,ICombatTarget,ICombatContactFilter,ICombatReclaimPolicy,ICombatProjectileContact
 {
  private Guid originExecution;
  private Action<CombatIceColumn> shatteredCallback;
  private bool shattered;
  private double expires;
  public Health Health {get;private set;}
  public bool IsAlive=>Health!=null&&Health.Current>0&&!shattered;
  public bool IsAvailable=>isActiveAndEnabled&&IsAlive;
  public bool AllowsReclaim=>false;
  // c21d4fc只让实际区域/近战包络碎柱；AL普通bullet被明确排除。
  public bool AllowsDirectProjectile=>false;
  public CombatFaction Faction=>CombatFaction.Neutral;
  public Vector3 WorldPosition=>transform.position;
  public Transform TargetTransform=>transform;
  public bool Shattered=>shattered;
  public Guid OriginExecution=>originExecution;
  public void Initialize(Guid parent,double now,Action<CombatIceColumn> onShattered)
  {
   if(Health!=null||parent==Guid.Empty)throw new InvalidOperationException("Column identity may initialize only once");
   originExecution=parent;expires=now+7;shatteredCallback=onShattered;Health=new Health("ice-column:"+Guid.NewGuid().ToString("N"),4);
  }
  public bool Accepts(AttackRelease release)=>IsAvailable&&release!=null&&release.ActionId!=originExecution&&release.ParentExecutionId!=originExecution;
  public void OnHitResolved(HitOutcome outcome)
  {
   if(shattered||outcome==null||outcome.TargetId!=Health?.Id||outcome.Reason!=HitReason.Applied)return;
   // 原型明确首个被接受接触即碎；Health保存真实扣血结果，实体破碎不额外伪造一次伤害。
   shattered=true;GetComponent<Collider>().enabled=false;shatteredCallback?.Invoke(this);Destroy(gameObject);
  }
  public void TickLifetime(double now){if(!shattered&&now>=expires)Expire();}
  public void Expire(){shattered=true;shatteredCallback=null;if(this!=null)Destroy(gameObject);}
  public void ShowHit(){}
 }
}
