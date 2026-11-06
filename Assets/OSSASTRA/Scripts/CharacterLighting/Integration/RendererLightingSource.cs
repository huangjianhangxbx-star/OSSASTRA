/* 功能：为独立渲染实体提供无玩家依赖的只读来源，用于静态实体与最小接入样板。
 * 边界：不替代NPC/敌人生命根；具有业务生命的根应自行实现ICharacterLightingSource。
 * 生命周期：显式接收者引用；启用重绑，禁用撤销自身登记，不扫描场景。
 */
using System.Collections.Generic;using UnityEngine;
namespace Ossastra.CharacterLighting
{
 [DisallowMultipleComponent] public sealed class RendererLightingSource:MonoBehaviour,ICharacterLightingSource
 {
  public string entityId="render-entity";
  public CharacterLightingReceiver receiver;
  public Renderer[] equipmentRenderers=System.Array.Empty<Renderer>();
  public Component Owner=>this;public string EntityId=>entityId;public bool IsAvailable=>isActiveAndEnabled;
  public void AppendEquipmentRenderers(List<Renderer> target){if(equipmentRenderers!=null)foreach(var r in equipmentRenderers)target.Add(r);}
  void OnEnable(){if(receiver!=null)receiver.Initialize(this);}
  void LateUpdate(){if(receiver!=null)receiver.TickPresentation();}
  void OnDisable(){if(receiver!=null)receiver.Suspend();}
 }
}
