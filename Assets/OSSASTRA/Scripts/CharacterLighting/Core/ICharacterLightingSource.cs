/* 功能：供各实体根提供光感所需的最小只读身份、可用性和装备表现。
 * 边界：不开放HP/移动/动画写入权；接收者只消费已提交的Renderer，不要求继承玩家根。
 */
using System.Collections.Generic;
using UnityEngine;
namespace Ossastra.CharacterLighting
{
 public interface ICharacterLightingSource
 {
  Component Owner {get;}
  string EntityId {get;}
  bool IsAvailable {get;}
  void AppendEquipmentRenderers(List<Renderer> destination);
 }
}
