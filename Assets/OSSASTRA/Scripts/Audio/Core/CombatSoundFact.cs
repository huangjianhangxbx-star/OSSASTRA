/* 脚本：CombatSoundFact
 * 功能：只读记录声音语义解析前的既成战斗事实，供同条件内容对照。
 * 边界：不是玩法命令，不重新执行攻击或伤害；不持有角色/媒体引用。
 * 协作：适配器发布冻结来源/角色/阶段/材质；开发记录器换Profile重放同一事实。
 */
using UnityEngine;
namespace Ossastra.Audio
{
 public sealed class CombatSoundFact
 {
  public string SourceId{get;}public string Role{get;}public string Selector{get;}public string Kind{get;}
  public int Stage{get;}public string Channel{get;}public string DeliveryMode{get;}public string Material{get;}public string FactKey{get;}
  public Vector3 Position{get;}public double Time{get;}
  public CombatSoundFact(string sourceId,string role,string selector,string kind,int stage,string channel,string mode,string material,string factKey,Vector3 position,double time)
  {SourceId=sourceId;Role=role;Selector=selector;Kind=kind;Stage=stage;Channel=channel;DeliveryMode=mode;Material=material;FactKey=factKey;Position=position;Time=time;}
 }
}
